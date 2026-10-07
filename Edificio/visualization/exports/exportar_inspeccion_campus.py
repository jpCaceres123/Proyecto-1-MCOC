"""Regenerate campus geometry and inspector from the current analytical run."""
import csv
import hashlib
import json
from pathlib import Path
import shutil
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
CASES=('G','Q','EX','EY','R','EXG','EXQ','EYG','EYQ')
FIELDS=('n','vy','vz','t','my','mz')


def load(path):return json.loads(path.read_text(encoding='utf-8-sig'))


def export_campus(root=ROOT):
    root=Path(root);out=root/'results'
    model=load(out/'modelo_3d_manual.json');cfg=load(root/'data/parameters/parametros.json')
    nodes={n['id']:n for n in model['nodes']}
    xyz=lambda tag:np.array([nodes[tag][f'{a}_m'] for a in 'xyz'])
    responses={};hashes={}
    for case in CASES:
        fpath=out/f'{case}_fuerzas_locales.json';dpath=out/f'{case}_diagramas_barras.json'
        forces,diagrams=load(fpath),load(dpath)
        with np.load(out/f'{case}.npz') as r:
            movement={int(tag):u.tolist() for tag,u in zip(r['node_tags'],r['u'])}
            reactions=({int(tag):v.tolist() for tag,v in zip(r['support_tags'],r['reaction'])}
                       if 'support_tags' in r.files else {})
        responses[case]=(forces,diagrams,movement,reactions)
        for path in (fpath,dpath,out/f'{case}.npz'):hashes[path.name]=hashlib.sha256(path.read_bytes()).hexdigest()
    with (out/'demanda_muros.csv').open(encoding='utf-8-sig',newline='') as stream:
        demands={(r['caso'],int(r['panel_id'])):[float(r[k]) for k in
            ('P_compresion_kN','V_en_plano_kN','V_fuera_plano_kN','M_principal_kNm','M_vertical_kNm')]
            for r in csv.DictReader(stream)}
    def loads_for(rows):
        result=[]
        for row in rows:
            qq=row['q_SC_kN_m2'] if cfg['q_Q_kN_m2'] is None else cfg['q_Q_kN_m2']
            factor=qq/row['q_SC_kN_m2'] if row['q_SC_kN_m2'] else 0
            result.append(dict(label=f"Losa {row['slab_id']} · {row['zone']}",distribution=row['distribution'],
                g=row['dead_load_kN'],q=row['tributary_area_m2']*qq,qg=row['q_G_kN_m2'],qq=qq,
                wg=row['w_G_max_kN_m'],wq=row['w_SC_max_kN_m']*factor))
        return dict(loads=result,hasLoads=bool(result),loadG=sum(v['g'] for v in result),loadQ=sum(v['q'] for v in result))
    all_loads=model['beam_load_cases']+model.get('wall_load_cases',[])
    sections={'COLUMN':'section_columns','STEEL_COLUMN_SHS300x20':'section_steel_columns',
        'BEAM_SMALL':'section_small_beams','BEAM_VARIABLE':'section_variable_beams','BEAM_40x60':'section_40x60_beams'}
    entries=[]
    bars=[e for e in model['elements'] if e['type']!='WALL']
    for e in bars:
        cases=[];tag=str(e['id'])
        section=e.get('section_override',model[sections.get(e['type'],'section_beams')])
        length=float(np.linalg.norm(xyz(e['j'])-xyz(e['i'])))
        for name,(forces,diagrams,moves,reactions) in responses.items():
            f,b=forces[tag],diagrams[tag]
            if len(f)!=12 or any(len(b[k])!=len(b['s']) for k in FIELDS):raise ValueError('Invalid bar result')
            cases.append(dict(name=name,text='Resultados actuales OpenSees',detail='Secciones en ejes locales; N positivo en compresión',
                endI=f[:6],endJ=f[6:],moveI=moves[e['i']],moveJ=moves[e['j']],
                reactionI=reactions.get(e['i']),reactionJ=reactions.get(e['j']),
                graphs=[dict(label=label,unit='kN' if i<3 else 'kN·m',values=b[k],stations=b['s'])
                    for i,(k,label) in enumerate(zip(FIELDS,('N','Vy','Vz','T','My','Mz')))]))
        column='COLUMN' in e['type']
        entries.append(dict(key='E:'+tag,title=('Columna ' if column else 'Viga ')+tag,
            category=e['type'],summary='Corrida estructural actual',cases=cases,
            geometry=[f'Longitud {length:.3f} m',f"Nodos {e['i']} → {e['j']}"]+
                [f'{k} {section[k]}' for k in ('A_m2','Iy_m4','Iz_m4','J_m4')],
            **loads_for(r for r in model['beam_load_cases'] if r['beam_id']==e['id'])))
    for w in model['walls']:
        entries.append(dict(key=f"W:{w['id']}",title=f"Muro {w['id']}",category='PAÑO ESTRUCTURAL',
            summary='Corte inferior del piso',geometry=[f"Piso {w['floor']}",f"Espesor {w['thickness_m']} m",
                f"Cotas {w['z_i_m']}–{w['z_j_m']} m"],
            cases=[dict(name=c,wall=demands.get((c,w['id'])),graphs=[]) for c in CASES],
            **loads_for(r for r in model.get('wall_load_cases',[]) if r['wall_id']==w['id'])))
    for s in model['slabs']:
        entries.append(dict(key=f"S:{s['id']}",title=f"Losa {s['id']}",category='LOSA DE REPARTO',
            summary='Sin resultados de placa; reparto a barras y muros',
            geometry=[f"Cota {s['z_m']} m",f"Área {s['area_m2']:.3f} m²",f"Espesor {s['thickness_m']} m"],
            cases=[dict(name=c,graphs=[]) for c in CASES],
            **loads_for(r for r in all_loads if r['slab_id']==s['id'])))
    source=root/'visualization/unity/UnityVisualization/Assets/Resources/model_3d.csv'
    payload=dict(source='Corrida actual: kN, m, kN·m. Secciones N positivo en compresión; acciones de extremo OpenSees conservadas.',
        entries=entries,provenance=dict(model_sha256=hashlib.sha256((out/'modelo_3d_manual.json').read_bytes()).hexdigest(),
        geometry_csv_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),results_sha256=hashes))
    text=json.dumps(payload,ensure_ascii=False,allow_nan=False)
    for project in ('CampusPlayable','CampusCardboard'):
        resources=root/f'visualization/unity/{project}/Assets/Resources'
        if resources.is_dir():
            shutil.copyfile(source,resources/'estructura_principal.csv')
            (resources/'inspeccion_estructural.json').write_text(text,encoding='utf-8')
    return dict(bars=len(bars),walls=len(model['walls']),slabs=len(model['slabs']),cases=len(CASES))


if __name__=='__main__':print(json.dumps(export_campus()))
