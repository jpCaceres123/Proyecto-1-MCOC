"""Independent linear load cases, 3D elastic frames and equivalent wall sticks."""
import math
from collections import defaultdict
import numpy as np
import openseespy.opensees as ops

def create_domain(model,c):
    ops.wipe();ops.model('basic','-ndm',3,'-ndf',6)
    for n in model['nodes']:
        ops.node(n['id'],n['x'],n['y'],n['z'])
        if n['fix']:ops.fix(n['id'],1,1,1,1,1,1)
    for d in model['diaphragms']:
        ops.node(d['id'],d['x'],d['y'],d['z']);ops.fix(d['id'],0,0,1,1,1,0)
        ops.rigidDiaphragm(3,d['id'],*d['nodes'])
    E=c['E_kN_m2']*c['stiffness_multiplier'];G=E/(2*(1+c['nu']))
    for e in model['elements']:
        ops.geomTransf('Linear',e['id'],*e['ref'])
        f=e['stiffnessScale']
        ops.element('elasticBeamColumn',e['id'],e['i'],e['j'],e['A'],E*f,G*f,e['J'],e['Iy'],e['Iz'],e['id'])
    ops.constraints('Transformation');ops.numberer('RCM');ops.system('UmfPack')
    ops.algorithm('Linear');ops.integrator('LoadControl',1.0);ops.analysis('Static')

def load_cases(model,tables,c):
    cases={k:dict(nodal=defaultdict(lambda:np.zeros(6)),points=defaultdict(list),uniform={},floorWeights=defaultdict(float)) for k in ['G','Q','EX','EY']}
    by_source=defaultdict(list); nmap={n['id']:n for n in model['nodes']}; emap={e['id']:e for e in model['elements']}
    for e in model['elements']:
        if e['source']:by_source[e['source']].append(e)
    floor_weight=defaultdict(float); totals={'G_floor':0.,'Q_floor':0.,'G_members':0.}
    # Physical self weight; no fictitious arm weight and no finite element slabs.
    for e in model['elements']:
        if e['kind']=='RIGID_ARM':continue
        q=c['density_kN_m3']*e['A']*c['G_scale']
        cases['G']['uniform'][e['id']]=[0.,0.,-q]
        weight=q*e['L'];totals['G_members']+=weight
        z=max(nmap[e['i']]['z'],nmap[e['j']]['z'])
        if z>0:floor_weight[(e['building'],z)]+=weight

    slabs={s['slab_id']:s for s in tables['Losas']}
    gauss,gw=np.polynomial.legendre.leggauss(3)
    for row in tables['CasosCargaViga']:
        source=int(row['beam_id']); children=by_source.get(source,[])
        G=float(row['dead_load_kN'])*c['G_scale'];Q=float(row['live_load_kN'])*c['Q_scale']
        totals['G_floor']+=G;totals['Q_floor']+=Q
        if not children:
            if source not in model['removed']:raise ValueError(f'No load destination for source {source}')
            p=model['removed'][source]
            available=[n for n in model['nodes'] if n['kind']=='wall_centroid' and n['building']=='LT2' and abs(n['z']-p[2])<1e-6]
            dest=min(available,key=lambda n:math.dist([n['x'],n['y'],n['z']],p))
            for name,value in [('G',G),('Q',Q)]:cases[name]['nodal'][dest['id']][2]-=value
            floor_weight[(dest['building'],dest['z'])]+=G+c['seismic_live_fraction']*Q
            continue
        L=children[0]['parentLength'];s=slabs[row['slab_id']]
        kind=row['distribution']
        a=min(L/2,min(s['lx_m'],s['ly_m'])/2) if kind=='trapezoidal' else L/2
        if kind not in ('triangular','trapezoidal'):a=0.
        def shape(x):
            if a<1e-9:return 1.
            return max(0.,min(x/a,(L-x)/a,1.))
        integral=L-a
        for e in children:
            lo=e['parentStart']*L;hi=e['parentEnd']*L
            breaks=sorted(set([lo,hi]+[v for v in [a,L-a] if lo<v<hi]))
            for x0,x1 in zip(breaks,breaks[1:]):
                for g,w in zip(gauss,gw):
                    x=(x0+x1)/2+g*(x1-x0)/2
                    share=shape(x)*w*(x1-x0)/2/integral
                    relative=(x-lo)/e['L']
                    for name,value in [('G',G),('Q',Q)]:
                        if abs(value)>1e-12:cases[name]['points'][e['id']].append([float(relative),0.,0.,-value*share])
        group=children[0]['building'];z=float(row['level_z_m'])
        floor_weight[(group,z)]+=G+c['seismic_live_fraction']*Q
    # Idealized static pattern per building, not a normative seismic design.
    for b in ['LT1','LT2']:
        ds=[d for d in model['diaphragms'] if d['building']==b]
        W=sum(floor_weight[(b,d['z'])] for d in ds)
        denom=sum(floor_weight[(b,d['z'])]*d['z'] for d in ds)
        for d in ds:
            F=c['seismic_coefficient']*W*floor_weight[(b,d['z'])]*d['z']/denom if denom else 0
            cases['EX']['nodal'][d['id']][0]=F;cases['EY']['nodal'][d['id']][1]=F
            d['weight']=floor_weight[(b,d['z'])];d['force']=F;d['mass_t']=d['weight']/9.80665
    return cases,totals

def resultant(model,load):
    coords={n['id']:np.array([n['x'],n['y'],n['z']]) for n in model['nodes']}
    coords.update({d['id']:np.array([d['x'],d['y'],d['z']]) for d in model['diaphragms']})
    emap={e['id']:e for e in model['elements']};R=np.zeros(6)
    for tag,f in load['nodal'].items():R[:3]+=f[:3];R[3:]+=f[3:]+np.cross(coords[tag],f[:3])
    for tag,q in load['uniform'].items():
        e=emap[tag];f=np.array(q)*e['L'];p=(coords[e['i']]+coords[e['j']])/2
        R[:3]+=f;R[3:]+=np.cross(p,f)
    for tag,points in load['points'].items():
        e=emap[tag];a=coords[e['i']];v=coords[e['j']]-a
        for t,*f in points:R[:3]+=f;R[3:]+=np.cross(a+t*v,f)
    return R

def combine_loads(cases,factors):
    out=dict(nodal=defaultdict(lambda:np.zeros(6)),points=defaultdict(list),uniform={})
    for key,k in factors.items():
        for tag,v in cases[key]['nodal'].items():out['nodal'][tag]+=k*v
        for tag,ps in cases[key]['points'].items():out['points'][tag].extend([[p[0],*(np.array(p[1:])*k).tolist()] for p in ps])
        for tag,v in cases[key]['uniform'].items():out['uniform'][tag]=(np.array(out['uniform'].get(tag,[0,0,0]))+k*np.array(v)).tolist()
    return out

def run_case(model,c,load,name):
    create_domain(model,c);ops.timeSeries('Linear',1);ops.pattern('Plain',1,1)
    emap={e['id']:e for e in model['elements']}
    for tag,f in load['nodal'].items():ops.load(tag,*[float(x) for x in f])
    for tag,v in load['uniform'].items():
        e=emap[tag];R=np.array([e['ex'],e['ey'],e['ez']]);qx,qy,qz=R@v
        ops.eleLoad('-ele',tag,'-type','-beamUniform',float(qy),float(qz),float(qx))
    for tag,ps in load['points'].items():
        e=emap[tag];R=np.array([e['ex'],e['ey'],e['ez']])
        for t,*v in ps:
            px,py,pz=R@v
            ops.eleLoad('-ele',tag,'-type','-beamPoint',float(py),float(pz),float(t),float(px))
    rc=ops.analyze(1)
    if rc:raise RuntimeError(f'OpenSees failed {name}: code {rc}')
    ops.reactions()
    nr=[];reaction=np.zeros(6)
    for n in model['nodes']:
        u=list(ops.nodeDisp(n['id']));r=list(ops.nodeReaction(n['id'])) if n['fix'] else [0.]*6
        if n['fix']:
            reaction[:3]+=r[:3];reaction[3:]+=np.array(r[3:])+np.cross([n['x'],n['y'],n['z']],r[:3])
        nr.append(dict(id=n['id'],u=u,reaction=r))
    er=[]
    for e in model['elements']:
        f=list(ops.eleResponse(e['id'],'localForce'));diag=[]
        R=np.array([e['ex'],e['ey'],e['ez']]);q=R@load['uniform'].get(e['id'],[0.,0.,0.])
        ps=[(p[0]*e['L'],R@p[1:]) for p in load['points'].get(e['id'],[])]
        for t in np.linspace(0,1,21):
            x=t*e['L'];F=-np.array(f[:3])-q*x
            M=-np.array(f[3:6])+np.cross([x,0,0],f[:3])+np.cross([x*x/2,0,0],q)
            for xp,p in ps:
                if xp<=x:F-=p;M+=np.cross([x-xp,0,0],p)
            diag.append(dict(t=float(t),N=float(F[0]),Vy=float(F[1]),Vz=float(F[2]),T=float(M[0]),My=float(M[1]),Mz=float(M[2])))
        er.append(dict(id=e['id'],forces=f,diagram=diag))
    applied=resultant(model,load);residual=reaction+applied
    force_rel=float(np.linalg.norm(residual[:3])/max(1,np.linalg.norm(applied[:3])))
    moment_rel=float(np.linalg.norm(residual[3:])/max(1,np.linalg.norm(applied[3:])))
    if force_rel>1e-5 or moment_rel>1e-5:raise RuntimeError(f'Equilibrium {name}: {force_rel}, {moment_rel}')
    print(f'{name}: equilibrium F={force_rel:.2e}, M={moment_rel:.2e}',flush=True)
    return dict(name=name,nodes=nr,elements=er,applied=applied.tolist(),reactions=reaction.tolist(),
                forceResidual=force_rel,momentResidual=moment_rel,
                maxDisplacement_m=max(np.linalg.norm(n['u'][:3]) for n in nr))

def verify_superposition(base,explicit,factors):
    checks={}
    for label,field,datafield in [('displacements','nodes','u'),('forces','elements','forces')]:
        expected=sum(factors[k]*np.array([r[datafield] for r in base[k][field]]) for k in factors)
        actual=np.array([r[datafield] for r in explicit[field]])
        abs_err=float(np.max(np.abs(expected-actual)));rel=abs_err/max(1e-12,float(np.max(np.abs(actual))))
        checks[label]=dict(maxAbs=abs_err,relative=rel)
        if rel>1e-6:raise AssertionError(f'Superposition {label}: {rel}')
    return checks
