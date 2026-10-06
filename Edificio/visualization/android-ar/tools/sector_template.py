"""Generate an explicitly UNSURVEYED form; measured poses must be supplied in terrain."""
import argparse
import json
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parents[1]


def main():
    p=argparse.ArgumentParser();p.add_argument('--ids',required=True);p.add_argument('--output',required=True,type=Path);a=p.parse_args()
    data=json.loads((ROOT/'app/src/main/assets/structural_data.json').read_text(encoding='utf-8'))
    byid={m['id']:m for m in data['members']};ids=[int(x) for x in a.ids.split(',')]
    if len(set(ids))<3 or any(i not in byid for i in ids):raise ValueError('At least three distinct existing tags')
    points=[p for i in ids for p in (byid[i]['start'],byid[i]['end'])];origin=np.mean(points,axis=0).tolist()
    if any(np.linalg.norm(np.array(v)-origin)>8 for v in points):raise ValueError('Select a nearby sector within 8 m of origin')
    form=dict(schema=1,surveyed=False,modelHash=data['model_sha256'],origin=origin,elements=ids,
              instructions='Fill measured marker center XYZ and marker-to-model quaternion xyzw; only then set surveyed=true.',
              markers=[dict(elementTag=i,width_m=.2,position=None,quaternion=None) for i in ids[:3]])
    if a.output.exists():raise ValueError('Will not overwrite survey')
    a.output.write_text(json.dumps(form,indent=2),encoding='utf-8')


if __name__=='__main__':main()
