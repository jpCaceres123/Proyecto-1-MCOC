"""Independent direct rerun of an isolated variant, compared with backend output."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import numpy as np


def run(job):
    source=job/'Edificio';direct=Path(tempfile.mkdtemp(prefix='MCOC-direct-'))/'Edificio'
    for folder in ('analysis','model','data','verification','visualization/exports','visualization/plots'):
        shutil.copytree(source/folder,direct/folder,ignore=shutil.ignore_patterns('__pycache__'))
    (direct/'documentation').mkdir()
    (direct/'visualization/unity/UnityVisualization/Assets/Resources').mkdir(parents=True)
    log=direct.parent/'direct.log'
    with log.open('w',encoding='utf-8') as stream:
        for script in ('model/builders/generar_modelo_manual.py','analysis/load_cases/ejecutar.py'):
            subprocess.run([sys.executable,str(direct/script)],check=True,cwd=direct.parent,stdout=stream,stderr=subprocess.STDOUT)
    comparisons=[]
    for case in ('G','Q','EX','EY','R'):
        with np.load(source/'results'/f'{case}.npz') as a,np.load(direct/'results'/f'{case}.npz') as b:
            np.testing.assert_array_equal(a['node_tags'],b['node_tags'])
            np.testing.assert_allclose(a['u'][:,:3],b['u'][:,:3],rtol=1e-5,atol=1e-8)
            np.testing.assert_allclose(a['u'][:,3:],b['u'][:,3:],rtol=1e-5,atol=1e-9)
            comparisons.append(dict(case=case,maxDisplacementDifference_m=float(np.max(abs(a['u'][:,:3]-b['u'][:,:3]))),maxRotationDifference_rad=float(np.max(abs(a['u'][:,3:]-b['u'][:,3:])))))
        a=json.loads((source/'results'/f'{case}_fuerzas_locales.json').read_text());b=json.loads((direct/'results'/f'{case}_fuerzas_locales.json').read_text())
        if a.keys()!=b.keys():raise ValueError('Different member tags')
        for tag in a:np.testing.assert_allclose(a[tag],b[tag],rtol=1e-5,atol=1e-5)
    resource='visualization/unity/UnityVisualization/Assets/Resources/semana4_resultados.json'
    a=json.loads((source/resource).read_text(encoding='utf-8'));b=json.loads((direct/resource).read_text(encoding='utf-8'))
    for ca,cb in zip(a['cases'],b['cases']):
        if ca['name']!=cb['name']:raise ValueError('Case ordering mismatch')
        for ba,bb in zip(ca['bars'],cb['bars']):
            if ba['id']!=bb['id']:raise ValueError('Member ordering mismatch')
            for field in ('s','n','vy','vz','t','my','mz'):np.testing.assert_allclose(ba[field],bb[field],rtol=1e-5,atol=1e-5)
    report=dict(status='OK',backendJob=str(job),directRun=str(direct),comparisons=comparisons,relativeTolerance=1e-5,
                absoluteTolerances=dict(translation_m=1e-8,rotation_rad=1e-9,force_kN=1e-5,moment_kNm=1e-5))
    (job/'direct_comparison.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    return report


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--job',required=True,type=Path);a=p.parse_args();print(json.dumps(run(a.job),indent=2))
