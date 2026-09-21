"""SQ4: respuesta incremental elástica a una carga viva localizada (kN, m, rad).

python Edificio/analysis/load_cases/carga_movil.py
La losa es una regla de transferencia, no un elemento finito. Dos vigas
opuestas reciben P(1-eta), P eta en la proyección de la posición del usuario.
Cuatro bases de carga puntual por viga reproducen exactamente la dependencia
cúbica de las acciones de extremo y GDL del marco Euler-Bernoulli lineal.
"""
import hashlib
import json
from pathlib import Path
import sys

import numpy as np
import openseespy.opensees as ops

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'model/opensees'))
import modelo_opensees_3d as model
from casos import dump_json

POSITIONS = np.array([0., 1./3., 2./3., 1.])


def weights(s):
    return np.array([np.prod([(s-b)/(a-b) for j,b in enumerate(POSITIONS) if j != i])
                     for i,a in enumerate(POSITIONS)])


def prepare():
    data = model.build_model()
    for tag in (1, 2, 3, 6, 7):
        ops.remove('loadPattern', tag)
    nodes = sorted(ops.getNodeTags())
    bars = [e for e in data['elements'] if e['type'] != 'WALL']
    sections = {'COLUMN':'section_columns','STEEL_COLUMN_SHS300x20':'section_steel_columns',
                'BEAM_SMALL':'section_small_beams','BEAM_VARIABLE':'section_variable_beams',
                'BEAM_40x60':'section_40x60_beams'}
    geometry = []
    for b in bars:
        sec = data[sections.get(b['type'], 'section_beams')]
        mat = data['material_steel' if b['type'].startswith('STEEL') else 'material']
        geometry.append(dict(id=b['id'], i=nodes.index(b['i']), j=nodes.index(b['j']),
            x=ops.eleResponse(b['id'],'xlocal'), y=ops.eleResponse(b['id'],'ylocal'),
            z=ops.eleResponse(b['id'],'zlocal'), E=mat['E_kPa'], A=sec['A_m2'],
            Iy=sec['Iy_m4'], Iz=sec['Iz_m4']))
    panels = []
    by_id = {b['id']: b for b in bars}
    for s in data['slabs']:
        # Continuous demonstrator strip, four adjacent panels on each available floor.
        c = s['coordinates']; xmin=min(p['x_m'] for p in c); xmax=max(p['x_m'] for p in c)
        ymin=min(p['y_m'] for p in c); ymax=max(p['y_m'] for p in c)
        if abs(xmin+28.3)>1e-5 or abs(xmax+24.55)>1e-5 or s.get('voids'):
            continue
        edges=s.get('edge_beam_ids',{})
        if any(len(edges.get(key,[]))!=1 for key in ('bottom','top')):
            continue
        receivers=[edges['bottom'][0], edges['top'][0]]
        if any(tag not in by_id for tag in receivers):
            continue
        for tag,y in zip(receivers,(ymin,ymax)):
            b=by_id[tag]; a=np.array(ops.nodeCoord(b['i'])); z=np.array(ops.nodeCoord(b['j']))
            if not (np.allclose(sorted([a[0],z[0]]),[xmin,xmax],atol=1e-5)
                    and abs(a[1]-y)<1e-5 and abs(z[1]-y)<1e-5):
                raise ValueError(f'Panel {s["id"]}: borde receptor no coincide')
        panels.append(dict(id=s['id'], xmin=xmin,xmax=xmax,ymin=ymin,ymax=ymax,z=s['z_m'], receivers=receivers))
    panels.sort(key=lambda p:(p['z'],p['ymin']))
    if not panels:
        raise ValueError('No hay paneles completos para el recorrido SQ4')
    return data, nodes, geometry, panels


def solve(points, cfg):
    _, nodes, bars, _ = prepare()
    index={b['id']:b for b in bars}
    ops.timeSeries('Linear',100)
    ops.pattern('Plain',100,100)
    applied=np.zeros(3); moment=np.zeros(3)
    for tag,s,p in points:
        b=index[tag]; basis=np.array([b['x'],b['y'],b['z']])
        global_p=np.array([0.,0.,-p]); px,py,pz=basis@global_p
        ops.eleLoad('-ele',tag,'-type','-beamPoint',py,pz,s,px)
        a=np.array(ops.nodeCoord(nodes[b['i']])); z=np.array(ops.nodeCoord(nodes[b['j']]))
        applied+=global_p; moment+=np.cross(a+s*(z-a),global_p)
    ops.constraints('Penalty',cfg['penalty'],cfg['penalty'])
    ops.numberer('RCM'); ops.system('UmfPack'); ops.algorithm('Linear')
    ops.integrator('LoadControl',1.); ops.analysis('Static')
    if ops.analyze(1):
        raise RuntimeError('Carga móvil: OpenSees no converge')
    u=np.array([ops.nodeDisp(n) for n in nodes])
    f=np.array([ops.eleResponse(b['id'],'localForce') for b in bars])
    ops.reactions()
    reaction=np.zeros(6)
    for n in ops.getFixedNodes():
        r=np.zeros(6)
        for dof in ops.getFixedDOFs(n):
            r[dof-1]=-cfg['penalty']*ops.nodeDisp(n,dof)
        reaction[:3]+=r[:3]; reaction[3:]+=r[3:]+np.cross(ops.nodeCoord(n),r[:3])
    residual=np.r_[reaction[:3]+applied,reaction[3:]+moment]
    return dict(u=u, f=f, reaction=reaction, residual=residual)


def generate(cfg=None):
    cfg=cfg or json.loads((ROOT/'data/parameters/parametros.json').read_text())
    _,nodes,bars,panels=prepare()
    xyz=[ops.nodeCoord(n) for n in nodes]
    receivers=sorted({tag for p in panels for tag in p['receivers']})
    bases={}; checks=[]
    for number,tag in enumerate(receivers):
        print(f'Base móvil {number+1}/{len(receivers)}: viga {tag}',flush=True)
        bases[tag]=[solve([(tag,float(s),1.)],cfg) for s in POSITIONS]
        for s,r in zip(POSITIONS,bases[tag]):
            checks.append(dict(control=f'Equilibrio de fuerzas viga {tag}, s={s:.6f}',
                               error=float(np.max(np.abs(r['residual'][:3]))),tolerance=2e-4))
    by_id={b['id']:b for b in bars}
    # Independent explicit solves at non-base positions, edge, zero and shared boundary.
    for panel in (panels[0],panels[len(panels)//2],panels[-1]):
        for xi,eta,P in ((.217,.683,37.),(.739,.125,80.),(0.,1.,1.),(.5,.5,0.)):
            points=[]; combined={key:np.zeros_like(bases[receivers[0]][0][key]) for key in ('u','f','reaction')}
            for tag,portion in zip(panel['receivers'],(1-eta,eta)):
                b=by_id[tag]; s=xi if xyz[b['j']][0]>xyz[b['i']][0] else 1-xi
                points.append((tag,float(s),P*portion))
                for k,w in enumerate(weights(s)):
                    for key in combined:
                        combined[key]+=P*portion*w*bases[tag][k][key]
            explicit=solve(points,cfg)
            for key in combined:
                error=np.max(np.abs(combined[key]-explicit[key]))/max(1e-8,np.max(np.abs(explicit[key])))
                checks.append(dict(control=f'Panel {panel["id"]}, ({xi},{eta}), P={P}: {key}',error=float(error),tolerance=2e-5))
            pos=np.array([panel['xmin']+xi*(panel['xmax']-panel['xmin']), panel['ymin']+eta*(panel['ymax']-panel['ymin']),panel['z']])
            distributed=sum((np.cross(np.array(xyz[by_id[tag]['i']])+s*(np.array(xyz[by_id[tag]['j']])-xyz[by_id[tag]['i']]),[0,0,-p]) for tag,s,p in points),np.zeros(3))
            checks.append(dict(control=f'Conservación momento panel {panel["id"]}, {xi},{eta}',
                error=float(np.max(np.abs(distributed-np.cross(pos,[0,0,-P])))),tolerance=1e-10))
    for check in checks:
        check['estado']='OK' if np.isfinite(check['error']) and check['error']<=check['tolerance'] else 'REVISAR'
    report=dict(checks=checks, rule='P_bottom=P*(1-eta), P_top=P*eta; puntos proyectados en X',
                units='kN, m, rad, kN*m', panels=len(panels), receivers=len(receivers),
                inherited_constraint_note='El modelo heredado tiene equalDOF entre nodos no coincidentes. '
                    'Conserva fuerza global, pero esas restricciones pueden introducir pares. '
                    'No se afirma equilibrio de momento usando solamente las reacciones SP. '
                    'El reparto SQ4 sí conserva exactamente fuerza y momento de la carga aplicada.',
                max_support_only_moment_residual_kNm_per_kN=float(max(np.max(np.abs(r['residual'][3:]))
                    for samples in bases.values() for r in samples)))
    dump_json(ROOT/'results/verificacion_carga_movil.json',report,indent=2)
    if any(c['estado']!='OK' for c in checks):
        raise RuntimeError('REVISAR verificacion_carga_movil.json')
    payload=dict(schema=1,units='kN, m, rad, kN*m', modelHash=hashlib.sha256(model.MODEL.read_bytes()).hexdigest(),
        nodes=[dict(id=n,xyz=xyz[k]) for k,n in enumerate(nodes)],bars=bars,panels=panels,
        bases=[dict(id=tag, samples=[dict(u=r['u'].ravel().tolist(),f=r['f'].ravel().tolist(),reaction=r['reaction'].tolist())
                                   for r in bases[tag]]) for tag in receivers])
    destination=ROOT/'visualization/unity/UnityVisualization/Assets/Resources/carga_movil.json'
    dump_json(destination,payload,separators=(',',':'),allow_nan=False)
    print(f'SQ4 OK: {len(panels)} paneles, {len(receivers)} vigas, {len(checks)} controles',flush=True)


if __name__=='__main__':
    generate()
