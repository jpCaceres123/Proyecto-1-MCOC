"""All slab panels: eccentric transfer, consistent loads, elastic nodal bases."""
import gzip
import hashlib
import json
from pathlib import Path
import numpy as np
import openseespy.opensees as ops
from carga_movil import ROOT, model
from casos import dump_json

def prepare():
    data=model.build_model()
    for tag in (1,2,3,6,7): ops.remove('loadPattern',tag)
    nodes=sorted(ops.getNodeTags()); indices={n:k for k,n in enumerate(nodes)}
    sections={'COLUMN':'section_columns','STEEL_COLUMN_SHS300x20':'section_steel_columns',
              'BEAM_SMALL':'section_small_beams','BEAM_VARIABLE':'section_variable_beams','BEAM_40x60':'section_40x60_beams'}
    bars=[]
    for b in data['elements']:
        if b['type']=='WALL': continue
        sec=data[sections.get(b['type'],'section_beams')]
        mat=data['material_steel' if b['type'].startswith('STEEL') else 'material']
        bars.append(dict(id=b['id'],i=indices[b['i']],j=indices[b['j']],
            x=ops.eleResponse(b['id'],'xlocal'),y=ops.eleResponse(b['id'],'ylocal'),z=ops.eleResponse(b['id'],'zlocal'),
            E=mat['E_kPa'],A=sec['A_m2'],Iy=sec['Iy_m4'],Iz=sec['Iz_m4']))
    by_id={b['id']:b for b in bars}; xyz=np.array([ops.nodeCoord(n) for n in nodes]); panels=[]
    for slab in data['slabs']:
        c=slab['coordinates']; edges=slab['edge_beam_ids']
        groups=[edges['bottom'],edges['top']] if edges.get('bottom') and edges.get('top') else [edges.get('support',edges.get('nearest_support',[]))]
        if not all(groups) or any(tag not in by_id for g in groups for tag in g):
            raise ValueError(f'Panel {slab["id"]}: apoyo sin barra analítica')
        for tag in {tag for g in groups for tag in g}:
            b=by_id[tag]
            if abs(xyz[b['i'],2]-slab['z_m'])>.01 or abs(xyz[b['j'],2]-slab['z_m'])>.01:
                raise ValueError(f'Panel {slab["id"]}: receptor no horizontal/en su nivel: {tag}')
        panels.append(dict(id=slab['id'],xmin=min(p['x_m'] for p in c),xmax=max(p['x_m'] for p in c),
            ymin=min(p['y_m'] for p in c),ymax=max(p['y_m'] for p in c),z=slab['z_m'],
            groups=[dict(ids=sorted(set(g))) for g in groups],receivers=sorted({tag for g in groups for tag in g}),
            voids=[dict(xmin=v['x_min_m'],xmax=v['x_max_m'],ymin=v['y_min_m'],ymax=v['y_max_m']) for v in slab.get('voids',[])],
            rule='opposite' if len(groups)==2 else 'eccentric',status=slab['status']))
    panels.sort(key=lambda p:(p['z'],p['id']))
    return data,nodes,bars,panels

def valid(panel,x,y):
    return panel['xmin']<=x<=panel['xmax'] and panel['ymin']<=y<=panel['ymax'] and not any(
        v['xmin']<=x<=v['xmax'] and v['ymin']<=y<=v['ymax'] for v in panel['voids'])

def transfer(panel,x,y,P,bars,xyz):
    if not valid(panel,x,y): raise ValueError('Posición fuera de losa o dentro de vacío')
    r=np.array([x,y,panel['z']]); eta=(y-panel['ymin'])/(panel['ymax']-panel['ymin']); result=[]
    for group,w in zip(panel['groups'],[1-eta,eta] if panel['rule']=='opposite' else [1.]):
        candidates=[]
        for tag in group['ids']:
            b=bars[tag]; a=xyz[b['i']]; d=xyz[b['j']]-a
            s=float(np.clip(np.dot(r-a,d)/np.dot(d,d),0,1)); q=a+s*d
            candidates.append((float(np.dot(r-q,r-q)),tag,s,q))
        _,tag,s,q=min(candidates,key=lambda item:(round(item[0],10),item[1]))
        force=np.array([0.,0.,-P*w]); moment=np.cross(r-q,force)
        result.append(dict(id=tag,s=s,P=P*w,moment=moment,q=q))
    # Opposite-edge transfer already preserves its transverse first moment.
    # Couples only correct projection offsets along the edge (segmented/overhanging edges).
    if panel['rule']=='opposite':
        for t in result:
            r_edge=np.array([x,panel['ymin'] if t is result[0] else panel['ymax'],panel['z']])
            t['moment']=np.cross(r_edge-t['q'],[0,0,-t['P']])
    return result

def equivalent(b,xyz,s,P,moment):
    """Consistent nodal load by virtual work, including concentrated couples."""
    R=np.array([b['x'],b['y'],b['z']]); L=np.linalg.norm(xyz[b['j']]-xyz[b['i']])
    px,py,pz=R@np.array([0.,0.,-P]); mx,my,mz=R@moment
    h=np.array([1-3*s*s+2*s**3,L*(s-2*s*s+s**3),3*s*s-2*s**3,L*(-s*s+s**3)])
    dh=np.array([(-6*s+6*s*s)/L,1-4*s+3*s*s,(6*s-6*s*s)/L,-2*s+3*s*s])
    v=py*h+mz*dh; w=pz*h-my*dh
    local=np.array([px*(1-s),v[0],w[0],mx*(1-s),-w[1],v[1],px*s,v[2],w[2],mx*s,-w[3],v[3]])
    global_load=np.concatenate([R.T@local[k:k+3] for k in (0,3,6,9)])
    return local,global_load.reshape(2,6)

def solve_nodal(loads,cfg):
    _,nodes,bars,_=prepare()
    ops.timeSeries('Linear',100); ops.pattern('Plain',100,100)
    for index,load in loads.items(): ops.load(nodes[index],*load)
    ops.constraints('Penalty',cfg['penalty'],cfg['penalty']); ops.numberer('RCM'); ops.system('UmfPack')
    ops.algorithm('Linear');ops.integrator('LoadControl',1.);ops.analysis('Static')
    if ops.analyze(1): raise RuntimeError('SQ4: no converge')
    u=np.array([ops.nodeDisp(n) for n in nodes]); f=np.array([ops.eleResponse(b['id'],'localForce') for b in bars])
    reaction=np.zeros(6)
    for n in ops.getFixedNodes():
        r=np.zeros(6)
        for dof in ops.getFixedDOFs(n): r[dof-1]=-cfg['penalty']*ops.nodeDisp(n,dof)
        reaction[:3]+=r[:3];reaction[3:]+=r[3:]+np.cross(ops.nodeCoord(n),r[:3])
    return np.concatenate((u.ravel(),f.ravel(),reaction))

def generate(cfg=None):
    cfg=cfg or json.loads((ROOT/'data/parameters/parametros.json').read_text())
    source,nodes,bars,panels=prepare();xyz=np.array([ops.nodeCoord(n) for n in nodes]);by_id={b['id']:b for b in bars}
    receivers={tag for p in panels for tag in p['receivers']}; indices=sorted({b[k] for b in bars if b['id'] in receivers for k in ('i','j')})
    resources=ROOT/'visualization/unity/UnityVisualization/Assets/Resources'; folder=resources/'SQ4Nodes';folder.mkdir(exist_ok=True)
    cache=ROOT/'results/sq4_cache';cache.mkdir(exist_ok=True)
    fingerprint=hashlib.sha256(model.MODEL.read_bytes()+Path(model.__file__).read_bytes()+json.dumps(cfg,sort_keys=True).encode()).hexdigest()
    bases={}; checks=[]; length=len(nodes)*6+len(bars)*12+6
    def check(name,error,tol): checks.append(dict(control=name,error=float(error),tolerance=tol,estado='OK' if np.isfinite(error) and error<=tol else 'REVISAR'))
    for count,index in enumerate(indices):
        path=cache/f'{fingerprint}_{nodes[index]}.npz'
        if path.exists(): samples=np.load(path)['samples']
        else:
            samples=[]
            for dof in (2,3,4):
                load=np.zeros(6);load[dof]=1
                samples.append(solve_nodal({index:load},cfg))
            samples=np.array(samples);np.savez_compressed(path,samples=samples)
        bases[index]=samples
        for k,dof in enumerate((2,3,4)):
            expected=np.array([0.,0.,1. if dof==2 else 0.])
            check(f'Base nodo {nodes[index]}, DOF {dof+1}: fuerzas',np.max(np.abs(samples[k,-6:-3]+expected)),2e-4)
        # Three little-endian double arrays in order Fz, Mx, My, gzip compressed.
        (folder/f'n{nodes[index]}.bytes').write_bytes(gzip.compress(samples.astype('<f8').tobytes(),mtime=0))
        print(f'SQ4 bases {count+1}/{len(indices)} nodo {nodes[index]}',flush=True)
    explicit=[]
    for p in panels:
        for sx,sy in ((.217,.683),(.739,.125),(.5,.5),(0.,0.),(1.,1.)):
            x=p['xmin']+sx*(p['xmax']-p['xmin']);y=p['ymin']+sy*(p['ymax']-p['ymin'])
            if not valid(p,x,y): continue
            loads={}; parts=transfer(p,x,y,37,by_id,xyz)
            for t in parts:
                b=by_id[t['id']];_,g=equivalent(b,xyz,t['s'],t['P'],t['moment'])
                for index,row in zip((b['i'],b['j']),g):loads[index]=loads.get(index,np.zeros(6))+row
            force=sum((v[:3] for v in loads.values()),np.zeros(3))
            moment=sum((np.cross(xyz[n],v[:3])+v[3:] for n,v in loads.items()),np.zeros(3))
            check(f'Panel {p["id"]} {sx},{sy}: fuerza',np.max(np.abs(force-[0,0,-37])),1e-10)
            check(f'Panel {p["id"]} {sx},{sy}: momento',np.max(np.abs(moment-np.cross([x,y,p['z']],[0,0,-37]))),1e-9)
            check(f'Panel {p["id"]}: DOF admitidos',max(np.max(np.abs(v[[0,1,5]])) for v in loads.values()),1e-9)
            category=(p['status'],p['z'])
            if category not in explicit:
                explicit.append(category)
                approx=sum((v[dof]*bases[n][k] for n,v in loads.items() for k,dof in enumerate((2,3,4))),np.zeros(length))
                actual=solve_nodal(loads,cfg)
                for name,a,z in (('u',0,len(nodes)*6),('f',len(nodes)*6,length-6),('R',length-6,length)):
                    check(f'Contraste {p["id"]} {name}',np.max(np.abs(actual[a:z]-approx[a:z]))/max(1e-8,np.max(np.abs(actual[a:z]))),2e-5)
    audit=dict(schema=2,panels=len(panels),receivers=len(receivers),basisNodes=len(indices),checks=checks,
        inherited_constraint_note='equalDOF no coincidentes: no se afirma equilibrio global de momentos usando sólo apoyos SP.')
    dump_json(ROOT/'results/verificacion_carga_movil.json',audit,indent=2)
    if any(c['estado']!='OK' for c in checks):raise RuntimeError('REVISAR auditoría SQ4')
    payload=dict(schema=2,units='kN, m, rad, kN*m',modelHash=hashlib.sha256(model.MODEL.read_bytes()).hexdigest(),
        nodes=[dict(id=n,xyz=xyz[k].tolist()) for k,n in enumerate(nodes)],bars=bars,panels=panels,basisNodes=indices)
    dump_json(resources/'carga_movil.json',payload,separators=(',',':'),allow_nan=False)
    print(f'SQ4 OK: {len(panels)} paneles, {len(checks)} controles',flush=True)

if __name__=='__main__': generate()
