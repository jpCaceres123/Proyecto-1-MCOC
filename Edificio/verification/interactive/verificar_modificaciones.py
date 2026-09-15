"""Dos modificaciones completas y restauración del estado base incluso si fallan."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT.parent/'reports/semana05_evidencias'

def snapshot():
    summary=json.loads((ROOT/'results/resumen_global.json').read_text(encoding='utf-8'))
    resources=ROOT/'visualization/unity/UnityVisualization/Assets/Resources'
    return dict(equilibrio=summary['equilibrium'],
                uz_Q_max_m=float(np.max(np.abs(np.load(ROOT/'results/Q.npz')['u'][:,2]))),
                ux_EX_max_m=float(np.max(np.abs(np.load(ROOT/'results/EX.npz')['u'][:,0]))),
                sha256_resources={p.name:hashlib.sha256(p.read_bytes()).hexdigest()
                                  for p in sorted(resources.iterdir()) if p.suffix in ('.csv','.json')})

def execute(name, path):
    with (OUT/f'{name}.log').open('w',encoding='utf-8') as log:
        subprocess.run([sys.executable,str(ROOT/'analysis/load_cases/ejecutar.py'),
                        '--parametros',str(path)],cwd=ROOT.parent,stdout=log,stderr=subprocess.STDOUT,check=True)

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    source=ROOT/'data/parameters/parametros.json'
    cfg=json.loads(source.read_text(encoding='utf-8'))
    records={}
    execute('mod_base',source)
    records['base']=snapshot()
    try:
        for name,key,value in [('mod_Q','q_Q_kN_m2',4.0),('mod_sismo','aceleracion_fraccion_g',.25)]:
            variant={**cfg,key:value}
            path=OUT/f'{name}_parametros.json'
            path.write_text(json.dumps(variant,indent=2,ensure_ascii=False),encoding='utf-8')
            execute(name,path)
            records[name]={'cambio':{key:value},**snapshot()}
    finally:
        execute('mod_restauracion',source)
        records['restaurado']=snapshot()
        (OUT/'modificaciones.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
    assert records['base']==records['restaurado'], 'La salida base no se restauró idénticamente'
    assert records['mod_Q']['uz_Q_max_m']!=records['base']['uz_Q_max_m']
    ratio=records['mod_sismo']['ux_EX_max_m']/records['base']['ux_EX_max_m']
    assert abs(ratio-1.25)<1e-5, ratio
    print('Dos variantes ejecutadas y Resources base restaurados. Razón EX:',ratio)

if __name__=='__main__':
    main()
