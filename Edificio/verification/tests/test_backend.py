import importlib.util
import tempfile
import threading
import time
import unittest
import uuid
import json
from pathlib import Path
from fastapi.testclient import TestClient
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('backend_server',ROOT/'analysis/backend/server.py')
server=importlib.util.module_from_spec(spec);spec.loader.exec_module(server)
TOKEN='test-only-token-never-use-in-production'


class BackendTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.release=threading.Event()
        self.app=server.create_app(storage=self.tmp.name,token=TOKEN,runner=lambda key:self.release.wait(3))
        self.client=TestClient(self.app);self.headers={'Authorization':'Bearer '+TOKEN}
    def tearDown(self):self.release.set();self.app.state.manager.close();self.tmp.cleanup()
    def request(self):return dict(contractVersion=1,requestId=str(uuid.uuid4()),modelHash=self.app.state.manager.model_hash,changes=[dict(kind='Viga',id=207,changeLoad=True,q=2)])
    def test_auth_and_unknown_schema(self):
        self.assertEqual(self.client.get('/health').status_code,401)
        self.assertEqual(self.client.get('/health',headers=self.headers).status_code,200)
        r=self.request();r['command']='anything';self.assertEqual(self.client.post('/jobs',json=r,headers=self.headers).status_code,422)
    def test_hash_and_id_validation(self):
        r=self.request();r['modelHash']='0'*64;self.assertEqual(self.client.post('/jobs',json=r,headers=self.headers).status_code,409)
        r=self.request();r['changes'][0]['id']=99999999;self.assertEqual(self.client.post('/jobs',json=r,headers=self.headers).status_code,422)
    def test_idempotency_conflict_and_cancel(self):
        r=self.request();a=self.client.post('/jobs',json=r,headers=self.headers);self.assertEqual(a.status_code,202)
        key=a.json()['id'];self.assertEqual(self.client.post('/jobs',json=r,headers=self.headers).status_code,202)
        r['changes'][0]['q']=3;self.assertEqual(self.client.post('/jobs',json=r,headers=self.headers).status_code,409)
        self.assertEqual(self.client.get('/jobs/'+key+'/delivery',headers=self.headers).status_code,409)
        self.assertEqual(self.client.delete('/jobs/'+key,headers=self.headers).json()['status'],'cancelled')
    def test_capacity_rejects_beam(self):
        r=self.request();r.update(operation='capacity',changes=[],elementTag=207)
        self.assertEqual(self.client.post('/jobs',json=r,headers=self.headers).status_code,422)

    def test_failed_job_never_publishes_results(self):
        def fail(key):raise RuntimeError('controlled numerical failure')
        self.app.state.manager.runner=fail
        key=self.client.post('/jobs',json=self.request(),headers=self.headers).json()['id']
        deadline=time.monotonic()+2
        while time.monotonic()<deadline:
            state=self.client.get('/jobs/'+key,headers=self.headers).json()
            if state['status']=='failed':break
            time.sleep(.01)
        self.assertEqual(state['status'],'failed')
        self.assertEqual(self.client.get('/jobs/'+key+'/delivery',headers=self.headers).status_code,409)

    def test_whitelist_and_file_hash(self):
        key=self.client.post('/jobs',json=self.request(),headers=self.headers).json()['id']
        self.client.delete('/jobs/'+key,headers=self.headers)
        folder=Path(self.tmp.name)/key/'delivery';folder.mkdir()
        (folder/'capacity.json').write_text('{}')
        manifest=dict(files=[dict(name='capacity.json',sha256=server.digest(folder/'capacity.json'))])
        (folder/'delivery.json').write_text(json.dumps(manifest))
        with self.app.state.manager.lock:self.app.state.manager.jobs[key]['status']='completed'
        self.assertEqual(self.client.get('/jobs/'+key+'/files/capacity.json',headers=self.headers).status_code,200)
        self.assertEqual(self.client.get('/jobs/'+key+'/files/request.json',headers=self.headers).status_code,404)
        (folder/'capacity.json').write_text('{"changed":true}')
        self.assertEqual(self.client.get('/jobs/'+key+'/files/capacity.json',headers=self.headers).status_code,409)


if __name__=='__main__':unittest.main()
