"""Read-only structural sources -> AR tributary polygons and shell nodal motions."""
import hashlib
import json
from pathlib import Path

PROJECT=Path(__file__).resolve().parents[1]
BUILDING=PROJECT.parents[1]


def export(building, output):
    model_path=building/'results/modelo_3d_manual.json'
    source=building/'visualization/unity/UnityVisualization/Assets/Resources/semana4_resultados.json'
    model=json.loads(model_path.read_text(encoding='utf-8'));data=json.loads(source.read_text(encoding='utf-8'))
    polygons=[]
    for slab in model['slabs']:
        for face in slab.get('global_partition',{}).get('faces',[]):
            ids=slab.get('edge_beam_ids',{}).get(face['edge'],[])
            if not ids:continue
            polygons.append(dict(slabTag=slab['id'],receivers=ids,area_m2=face['area_m2'],
                                 points=[[p['x_m'],p['y_m'],slab['z_m']] for p in face['polygon']]))
    xyz={n['id']:n['xyz'] for n in data['nodes']}
    shellnodes={i for shell in data['shells'] for i in shell['nodes']}
    out=dict(schema=1,modelHash=hashlib.sha256(model_path.read_bytes()).hexdigest(),
             resultsHash=hashlib.sha256(source.read_bytes()).hexdigest(),polygons=polygons,
             shells=[dict(id=s['id'],nodes=s['nodes'],points=[xyz[i] for i in s['nodes']]) for s in data['shells']],
             motions={c['name']:{str(n['id']):n['u'] for n in c['nodes'] if n['id'] in shellnodes} for c in data['cases']})
    output.write_text(json.dumps(out,separators=(',',':'),allow_nan=False),encoding='utf-8')


if __name__=='__main__':export(BUILDING,PROJECT/'app/src/main/assets/overlay_geometry.json')
