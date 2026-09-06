"""Workbook -> traceable analytical model. SI: kN, m, s. No slab FE."""
from pathlib import Path
from collections import defaultdict
import json, math
import numpy as np
import openpyxl

ROOT = Path(__file__).resolve().parents[1]

def read_tables(path):
    wb = openpyxl.load_workbook(path, read_only=True, data_only=True)
    result = {}
    for sheet in wb:
        it = iter(sheet.values); keys = next(it)
        result[sheet.title] = [dict(zip(keys, row)) for row in it if any(v is not None for v in row)]
    wb.close()
    return result

def rectangle(b, h):
    # b along local z, h along local y; Saint-Venant rectangular torsion approximation.
    a, t = max(b,h), min(b,h)
    return dict(A=b*h, Iy=h*b**3/12, Iz=b*h**3/12,
                J=a*t**3*(1/3-.21*(t/a)*(1-(t/a)**4/12)))

def build_geometry(config, tables):
    ns = {r['node_id']:r for r in tables['Nodos']}
    nodes=[]; elements=[]; changes=[]; lookup={}; used_ids=set(); elem_ids=set()
    def building(x): return 'LT2' if x < config['split_x_m'] else 'LT1'
    def node(xyz, tag=None, kind='structural', group=None):
        key=tuple(round(float(v),6) for v in xyz)
        if key in lookup: return lookup[key]
        tag = int(tag) if tag is not None and tag not in used_ids else 2000000+len(nodes)
        while tag in used_ids: tag+=1
        used_ids.add(tag); lookup[key]=tag
        nodes.append(dict(id=tag,x=key[0],y=key[1],z=key[2],building=group or building(key[0]),kind=kind,fix=False))
        return tag
    def pos(tag): return np.array(next(n for n in nodes if n['id']==tag).get('xyz', []))
    def getn(tag): return next(n for n in nodes if n['id']==tag)
    def xyz(tag):
        n=getn(tag);return np.array([n['x'],n['y'],n['z']])
    def add_elem(i,j,kind,b,h,tag=None,source=0,wall=0,vec=None,scale=1):
        if i==j:return None
        tag=int(tag) if tag is not None and tag not in elem_ids else 3000000+len(elements)
        while tag in elem_ids:tag+=1
        elem_ids.add(tag)
        a,c=xyz(i),xyz(j); L=float(np.linalg.norm(c-a)); ex=(c-a)/L
        # For horizontal beams local y is vertical (section depth h), local z is width b.
        ref=np.array(vec if vec is not None else ([0,1,0] if abs(ex[2])>.9 else np.cross(ex,[0,0,1])),float)
        ey=np.cross(ref,ex);ey/=np.linalg.norm(ey);ez=np.cross(ex,ey)
        if getn(i)['building'] != getn(j)['building']:
            raise ValueError(f'Connection across joint: {tag}')
        e=dict(id=tag,source=source,wall=wall,i=i,j=j,kind=kind,building=getn(i)['building'],
               b=float(b),h=float(h),L=L,ex=ex.tolist(),ey=ey.tolist(),ez=ez.tolist(),ref=ref.tolist(),
               stiffnessScale=scale,**rectangle(b,h))
        elements.append(e); return e

    # Preserve source member IDs wherever no subdivision is needed. Keep source lineage always.
    originals=[]; removed={}
    for r in tables['Elementos']:
        if r['tipo']=='WALL':continue
        a=np.array([r[k] for k in ('xi_m','yi_m','zi_m')],float)
        b=np.array([r[k] for k in ('xj_m','yj_m','zj_m')],float)
        tags=[r['node_i'],r['node_j']]
        if r['tipo']=='COLUMN':
            correction=next((c for c in config.get('column_base_corrections',[])
                if abs(a[0]-c['x_m'])<1e-6 and abs(b[0]-c['x_m'])<1e-6
                and abs(a[1]-c['y_m'])<1e-6 and abs(b[1]-c['y_m'])<1e-6),None)
            if correction:
                start=correction['start_z_m']
                if max(a[2],b[2])<=start+1e-6:
                    changes.append(dict(action='remove_column_below_confirmed_base',source=r['element_id'],
                        axis=correction['axis'],x_m=correction['x_m'],y_m=correction['y_m'],
                        start_z_m=start,authorization='User confirmed raised fixed base'))
                    continue
                if min(a[2],b[2])<start-1e-6:
                    raise ValueError('Corrected column base must coincide with an existing storey node')
        if building(a[0])!=building(b[0]):
            if max(a[0],b[0])>-.24: raise ValueError('Unexpected member across joint')
            k=0 if a[0]>b[0] else 1
            (a if k==0 else b)[0]=config['lt2_wall_axis_x_m'];tags[k]=None
            changes.append(dict(action='shorten_at_joint',source=r['element_id'],new_end_x_m=config['lt2_wall_axis_x_m']))
        if np.linalg.norm(a-b)<1e-6:
            removed[r['element_id']]=a.tolist();continue
        i=node(a,tags[0]); j=node(b,tags[1])
        mult=config['column_section_multiplier'] if r['tipo']=='COLUMN' else config['beam_section_multiplier']
        originals.append(dict(row=r,i=i,j=j,a=a,b=b,width=r['b_m']*mult,height=r['h_m']*mult))

    levels=sorted(set(n['z'] for n in nodes))
    wall_geometry=[]; wall_nodes=[]
    for w in tables['Muros']:
        a=np.array([w['xi_m'],w['yi_m']]); b=np.array([w['xj_m'],w['yj_m']]); c=(a+b)/2
        d=b-a; length=float(np.linalg.norm(d));direction=d/length
        # local x vertical; local y follows wall plan axis; local z = x cross y.
        ref=[-direction[1],direction[0],0]
        zs=sorted(set([w['zi_m'],w['zj_m']]+[z for z in levels if w['zi_m']<z<w['zj_m']]))
        ids=[node([*c,z],kind='wall_centroid') for z in zs]
        for z,tag in zip(zs,ids):wall_nodes.append((w,a,b,z,tag))
        for ni,nj in zip(ids,ids[1:]):
            add_elem(ni,nj,'WALL',w['espesor_m'],length,source=0,wall=w['wall_id'],vec=ref,scale=config['wall_stiffness_multiplier'])
        wall_geometry.append(dict(id=w['wall_id'],ax=float(a[0]),ay=float(a[1]),bx=float(b[0]),by=float(b[1]),
            z0=w['zi_m'],z1=w['zj_m'],thickness=w['espesor_m'],building=building(c[0])))
    changes.append(dict(action='replace_diagonal_wall_graph',source_count=sum(e['tipo']=='WALL' for e in tables['Elementos']),physical_walls=len(wall_geometry),description='Vertical centroid sticks by storey; gross rectangular section. No flanged-wall composite action.'))

    # Subdivide members at actual collinear nodes so joints are mechanically connected.
    pool=list(nodes)
    for o in originals:
        a,b=o['a'],o['b'];v=b-a;L2=float(v@v);points=[(0,o['i']),(1,o['j'])]
        for n in pool:
            if n['id'] in (o['i'],o['j']):continue
            p=np.array([n['x'],n['y'],n['z']]);t=float((p-a)@v/L2)
            if 1e-6<t<1-1e-6 and np.linalg.norm(p-a-t*v)<1e-5:points.append((t,n['id']))
        points.sort()
        for k,((s,ni),(t,nj)) in enumerate(zip(points,points[1:])):
            r=o['row']; e=add_elem(ni,nj,r['tipo'],o['width'],o['height'],r['element_id'] if k==0 else None,source=r['element_id'])
            e.update(parentStart=s,parentEnd=t,parentLength=math.sqrt(L2))
        if len(points)>2:changes.append(dict(action='split_at_existing_nodes',source=o['row']['element_id'],segments=len(points)-1))

    # Finite, massless arms avoid chains of rigidLink + rigidDiaphragm constraints.
    # Only nodes lying on/near physical wall trace are candidates. No cross-gap arms.
    arm_pairs=set()
    for w,a,b,z,centroid in wall_nodes:
        v=b-a;L2=float(v@v)
        for n in list(nodes):
            if n['id']==centroid or abs(n['z']-z)>1e-6 or n['building']!=getn(centroid)['building']:continue
            p=np.array([n['x'],n['y']]);t=float((p-a)@v/L2)
            tol=config['wall_connection_tolerance_m']
            if -.01 <= t <= 1.01 and np.linalg.norm(p-(a+np.clip(t,0,1)*v)) <= w['espesor_m']/2+tol:
                pair=tuple(sorted((centroid,n['id'])))
                if pair in arm_pairs or np.linalg.norm(xyz(centroid)-xyz(n['id']))<1e-6:continue
                arm_pairs.add(pair)
                add_elem(centroid,n['id'],'RIGID_ARM',.7,.7,wall=w['wall_id'],scale=config['rigid_arm_factor'])
    changes.append(dict(action='finite_stiff_arms',count=len(arm_pairs),factor=config['rigid_arm_factor'],tolerance_m=config['wall_connection_tolerance_m']))

    # Restrict the base explicitly; source fixed nodes above z=0 remain fixed and are logged.
    original_support_positions={tuple(round(r[k],6) for k in ('x_m','y_m','z_m')) for r in tables['Apoyos']}
    for n in nodes:
        n['fix']=abs(n['z'])<1e-6 or (n['x'],n['y'],n['z']) in original_support_positions
    for c in config.get('column_base_corrections',[]):
        found=[n for n in nodes if abs(n['x']-c['x_m'])<1e-6 and abs(n['y']-c['y_m'])<1e-6 and abs(n['z']-c['start_z_m'])<1e-6]
        if len(found)!=1:raise ValueError(f'Corrected column base node not unique: {c}')
        found[0]['fix']=bool(c['fixed'])
        changes.append(dict(action='confirmed_column_support',node=found[0]['id'],**c))
    if config.get('fix_all_column_bases',False):
        column_bases={}
        for e in elements:
            if e['kind']!='COLUMN':continue
            for tag in (e['i'],e['j']):
                n=getn(tag);key=(n['building'],round(n['x'],6),round(n['y'],6))
                if key not in column_bases or n['z']<column_bases[key]['z']:
                    column_bases[key]=n
        for n in column_bases.values():
            was_fixed=n['fix'];n['fix']=True
            changes.append(dict(action='fix_column_base',node=n['id'],building=n['building'],
                                x_m=n['x'],y_m=n['y'],z_m=n['z'],previously_fixed=was_fixed,
                                restrained_dofs=[1,2,3,4,5,6]))
    active={e[k] for e in elements for k in ('i','j')};nodes[:]=[n for n in nodes if n['id'] in active]
    # All components must have a physical path to a support before adding diaphragm constraints.
    adj=defaultdict(set)
    for e in elements:adj[e['i']].add(e['j']);adj[e['j']].add(e['i'])
    seen=set();components=[]
    for n in nodes:
        if n['id'] in seen:continue
        stack=[n['id']];comp=set()
        while stack:
            tag=stack.pop()
            if tag in comp:continue
            comp.add(tag);stack.extend(adj[tag]-comp)
        seen|=comp;components.append(dict(count=len(comp),supported=any(x['fix'] and x['id'] in comp for x in nodes),ids=sorted(comp)))
    unsupported=[c for c in components if not c['supported']]
    if unsupported:raise ValueError('Unsupported components: '+str(unsupported))

    diaphragms=[]
    for group in ('LT1','LT2'):
        for z in levels:
            ids=[n['id'] for n in nodes if n['building']==group and abs(n['z']-z)<1e-6 and not n['fix']]
            if not ids:continue
            pts=np.array([xyz(tag) for tag in ids]);c=pts.mean(axis=0)
            master=4000000+len(diaphragms)
            diaphragms.append(dict(id=master,building=group,z=z,x=float(c[0]),y=float(c[1]),nodes=ids))

    # Canonical model contains only analytic nodes. Slabs stay graphical/tributary objects.
    slabvertices=defaultdict(list)
    for v in tables['VerticesLosas']:slabvertices[v['slab_id']].append(v)
    slabs=[]
    for s in tables['Losas']:
        vs=sorted(slabvertices[s['slab_id']],key=lambda x:x['vertice'])
        ps=[dict(x=v['x_m'],y=v['y_m'],z=v['z_m']) for v in vs]
        slabs.append(dict(id=s['slab_id'],z=s['z_m'],area=s['area_neta_m2'],state=s['estado'],points=ps))
    trib=defaultdict(list)
    for v in tables['PoligTributarios']:trib[(v['slab_id'],v['borde'])].append(dict(x=v['x_m'],y=v['y_m']))
    tributaries=[dict(slab=k[0],edge=k[1],points=v) for k,v in trib.items()]
    return dict(nodes=nodes,elements=elements,walls=wall_geometry,diaphragms=diaphragms,slabs=slabs,tributaries=tributaries,
                changes=changes,removed=removed,components=components,levels=levels)

def load_config(override=None):
    c=json.loads((ROOT/'config.json').read_text(encoding='utf8'))
    if override:
        c.update(json.loads(Path(override).read_text(encoding='utf8')))
    for k in ['E_kN_m2','stiffness_multiplier','beam_section_multiplier','column_section_multiplier','wall_stiffness_multiplier']:
        if c[k]<=0:raise ValueError(f'{k} must be positive')
    return c
