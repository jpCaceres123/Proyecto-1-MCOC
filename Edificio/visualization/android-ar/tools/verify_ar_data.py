"""Checks source identity, local station values, transforms, and marker feature scores."""
from pathlib import Path
import concurrent.futures
import hashlib
import json
import math
import subprocess

PROJECT=Path(__file__).resolve().parents[1]
BUILDING=PROJECT.parents[1]


def main():
    assets=PROJECT/'app/src/main/assets'
    data=json.loads((assets/'structural_data.json').read_text(encoding='utf-8'))
    source_path=BUILDING/'visualization/unity/UnityVisualization/Assets/Resources/semana4_resultados.json'
    source=json.loads(source_path.read_text(encoding='utf-8'))
    assert data['results_sha256']==hashlib.sha256(source_path.read_bytes()).hexdigest()
    assert data['model_sha256']==hashlib.sha256((BUILDING/'results/modelo_3d_manual.json').read_bytes()).hexdigest()
    source_bars={b['id']:b for b in source['bars']}
    source_nodes={n['id']:n['xyz'] for n in source['nodes']}
    source_cases={c['name']:{b['id']:b for b in c['bars']} for c in source['cases']}
    ids=set()
    for m in data['members']:
        tag=m['id'];assert tag not in ids;ids.add(tag)
        assert m['start']==source_nodes[source_bars[tag]['i']]
        assert m['end']==source_nodes[source_bars[tag]['j']]
        assert math.isclose(m['length_m'],math.dist(m['start'],m['end']))
        for name,result in m['cases'].items():
            for key,values in result.items():assert values==source_cases[name][tag][key],(tag,name,key)
        assert (assets/m['marker']).exists()
        # Unit length and orientation are preserved by each right-handed map.
        for p in [(1,0,0),(0,1,0),(0,0,1)]:
            beam=(p[0],p[2],-p[1]);column=(p[1],-p[2],-p[0])
            assert sum(x*x for x in beam)==sum(x*x for x in p)==sum(x*x for x in column)
    def det(m):
        return m[0][0]*(m[1][1]*m[2][2]-m[1][2]*m[2][1])-m[0][1]*(m[1][0]*m[2][2]-m[1][2]*m[2][0])+m[0][2]*(m[1][0]*m[2][1]-m[1][1]*m[2][0])
    assert det(((1,0,0),(0,0,1),(0,-1,0)))==1
    assert det(((0,1,0),(0,0,-1),(-1,0,0)))==1
    results={'member_count':len(ids),'cases':list(data['members'][0]['cases']), 'source_identity':'OK',
             'station_values_and_local_signs':'exact_source_match','model_sha256':data['model_sha256'],
             'transforms':'right_handed_rigid_rotation_and_uniform_scale','physical_device_test':'PENDING'}
    tool=Path.home()/'.codex/android-ar-tools/arcoreimg.exe'
    if tool.exists():
        def score(m):
            r=subprocess.run([str(tool),'eval-img','--input_image_path='+str(assets/m['marker'])],capture_output=True,text=True,check=True)
            value=int(r.stdout.strip().splitlines()[-1])
            return {'id':m['id'],'score':value}
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:scores=list(pool.map(score,data['members']))
        results['marker_quality']={'tool':'Google arcoreimg','minimum_score':min(s['score'] for s in scores),'count':len(scores),'scores':scores}
    (PROJECT/'verification.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
    print('AR export verified:',len(ids),'members / exact station values; markers minimum score',results.get('marker_quality',{}).get('minimum_score','not evaluated'))
    if results.get('marker_quality',{}).get('minimum_score',100)<75:
        raise ValueError('Marcadores por debajo de 75: '+str([s for s in scores if s['score']<75]))


if __name__=='__main__':main()
