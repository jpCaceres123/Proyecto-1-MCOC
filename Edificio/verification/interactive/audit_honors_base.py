"""Fresh isolated baseline: checks source provenance and numerical compatibility."""
import importlib.util
import json
from pathlib import Path
import tempfile
import time
import uuid
import numpy as np

ROOT=Path(__file__).resolve().parents[2]


def main():
    spec=importlib.util.spec_from_file_location('honors_server',ROOT/'analysis/backend/server.py')
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    folder=Path(tempfile.mkdtemp(prefix='MCOC-base-audit-'));manager=module.Manager(ROOT,folder)
    try:
        request=module.JobRequest(contractVersion=1,requestId=uuid.uuid4(),modelHash=manager.model_hash,changes=[])
        key=manager.submit(request)['id'];deadline=time.monotonic()+660
        while manager.public(key)['status'] in ('pending','running'):
            if time.monotonic()>deadline:raise TimeoutError('Baseline audit timed out')
            time.sleep(.25)
        state=manager.public(key)
        if state['status']!='completed':raise RuntimeError(state)
        job=folder/key;work=job/'Edificio'
        comparisons=[];violations=[]
        for case in ('G','Q','EX','EY','R'):
            with np.load(ROOT/'results'/f'{case}.npz') as prior,np.load(work/'results'/f'{case}.npz') as fresh:
                np.testing.assert_array_equal(prior['node_tags'],fresh['node_tags'])
                if not np.allclose(prior['u'][:,:3],fresh['u'][:,:3],rtol=1e-5,atol=1e-8):violations.append(dict(case=case,quantity='translation'))
                if not np.allclose(prior['u'][:,3:],fresh['u'][:,3:],rtol=1e-5,atol=1e-9):violations.append(dict(case=case,quantity='rotation'))
                comparisons.append(dict(case=case,maxTranslationDifference_m=float(np.max(abs(prior['u'][:,:3]-fresh['u'][:,:3])))))
            old=json.loads((ROOT/'results'/f'{case}_fuerzas_locales.json').read_text());new=json.loads((work/'results'/f'{case}_fuerzas_locales.json').read_text())
            if old.keys()!=new.keys():raise ValueError('Changed tags in baseline')
            for tag in old:
                if not np.allclose(old[tag],new[tag],rtol=1e-5,atol=1e-5):violations.append(dict(case=case,quantity='local_actions',elementTag=tag,maxDifference=float(np.max(abs(np.array(old[tag])-np.array(new[tag]))))))
        provenance=json.loads((work/'results/manifest.json').read_text(encoding='utf-8'))
        for name,expected in provenance['inputs'].items():
            path=work.parent/Path(name)
            if module.digest(path)!=expected:raise ValueError('Provenance input hash mismatch: '+name)
        historical=json.loads((ROOT/'results/manifest.json').read_text(encoding='utf-8'))
        prior_model=json.loads((ROOT/'results/modelo_3d_manual.json').read_text(encoding='utf-8'))
        fresh_model=json.loads((work/'results/modelo_3d_manual.json').read_text(encoding='utf-8'))
        load_changes=[dict(index=k,field=f,previous=row.get(f),fresh=fresh_model['beam_load_cases'][k].get(f))
                      for k,row in enumerate(prior_model['beam_load_cases']) for f in row
                      if row.get(f)!=fresh_model['beam_load_cases'][k].get(f)]
        report=dict(status='REVISAR' if violations else 'OK',modelHash=manager.model_hash,freshInputHashes='verified',comparisons=comparisons,violations=violations,
                    historicalVersions=historical['versions'],freshVersions=provenance['versions'],regeneratedLoadDifferences=load_changes,
                    sourceOriginalResults='unchanged',job=str(job),historicalManifest='preserved; fresh manifest accompanies isolated rerun')
        (job/'baseline_audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
        print(json.dumps(report,indent=2),flush=True)
        if violations:return 1
        return 0
    finally:manager.close()


if __name__=='__main__':
    import sys
    sys.exit(main())
