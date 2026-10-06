import argparse
import copy
import json
from pathlib import Path
import sys
import math


def main():
    p=argparse.ArgumentParser();p.add_argument('--root',required=True,type=Path);p.add_argument('--request',required=True,type=Path);a=p.parse_args()
    sys.path.insert(0,str(a.root/'analysis/capacity'))
    from reinforcement import generate
    request=json.loads(a.request.read_text(encoding='utf-8'))
    data=json.loads((a.root/'visualization/unity/UnityVisualization/Assets/Resources/semana4_resultados.json').read_text(encoding='utf-8'))
    meta=next(e for e in data['elementMetadata'] if e['id']==request['elementTag'])
    if meta['type']!='COLUMN':raise ValueError('Only RC columns have configured reinforcement')
    config=json.loads((a.root/'data/parameters/parametros.json').read_text(encoding='utf-8'))['columna']
    s=meta['sectionData'];config=copy.deepcopy(config)
    # Fiber y,z correspond to local bending Mz,My respectively.
    config['h_m']=math.sqrt(12*s['Iz_m4']/s['A_m2']);config['b_m']=math.sqrt(12*s['Iy_m4']/s['A_m2'])
    result=generate(config,request['barsPerFace'],request['diameter_m'])
    result['elementTag']=request['elementTag'];result['modelHash']=request['modelHash']
    result['previous']=generate(config,config['barras_por_cara'],config['diametro_m'])['curves']
    (a.request.parent/'capacity.json').write_text(json.dumps(result,allow_nan=False),encoding='utf-8')


if __name__=='__main__':main()
