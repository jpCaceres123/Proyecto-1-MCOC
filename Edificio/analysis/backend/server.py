"""Authenticated LAN-only worker. One isolated OpenSees process at a time."""
from contextlib import asynccontextmanager
import hashlib
import ipaddress
import json
import os
from pathlib import Path
import queue
import secrets
import subprocess
import sys
import threading
import time
from typing import Literal
from uuid import UUID

from fastapi import Depends, FastAPI, Header, HTTPException, Request
from fastapi.responses import FileResponse
from pydantic import BaseModel, ConfigDict, Field, StrictBool, StrictInt, model_validator

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'analysis'))
from contracts import digest, publish
sys.path.insert(0,str(ROOT/'model/builders'))
from variantes_interactivas import apply


class Edit(BaseModel):
    model_config=ConfigDict(extra='forbid',allow_inf_nan=False)
    kind:Literal['Viga','Columna','Muro','Losa']
    id:StrictInt=Field(gt=0)
    changeLoad:StrictBool=False
    changeSection:StrictBool=False
    q:float=Field(default=0,ge=0,le=100)
    b:float=Field(default=0,ge=0,le=3)
    h:float=Field(default=0,ge=0,le=3)
    @model_validator(mode='after')
    def dimensions(self):
        if self.changeSection and (self.h<.02 or (self.kind in ('Viga','Columna') and self.b<.05)):
            raise ValueError('Section must have positive permitted dimensions')
        return self


class JobRequest(BaseModel):
    model_config=ConfigDict(extra='forbid',allow_inf_nan=False)
    contractVersion:Literal[1]
    requestId:UUID
    modelHash:str=Field(pattern=r'^[0-9a-f]{64}$')
    operation:Literal['analysis','capacity']='analysis'
    changes:list[Edit]=Field(default_factory=list,max_length=64)
    elementTag:StrictInt|None=None
    barsPerFace:StrictInt=Field(default=5,ge=2,le=12)
    diameter_m:float=Field(default=.028,ge=.008,le=.04)
    @model_validator(mode='after')
    def operation_fields(self):
        if self.operation=='capacity' and (self.changes or self.elementTag is None):
            raise ValueError('Capacity requires one column and no global model changes')
        return self


class Manager:
    def __init__(self,root,storage,timeout=600,runner=None):
        self.root=Path(root);self.storage=Path(storage);self.storage.mkdir(parents=True,exist_ok=True)
        self.timeout=timeout;self.jobs={};self.lock=threading.RLock();self.queue=queue.Queue();self.closed=False;self.process=None
        self.runner=runner or self.run_analysis
        # Never automatically resume an interrupted numerical job after restart.
        for directory in self.storage.iterdir():
            if not directory.is_dir():continue
            try:
                UUID(directory.name)
                request=json.loads((directory/'request.json').read_text(encoding='utf-8'))
                JobRequest.model_validate(request)
                status=json.loads((directory/'status.json').read_text(encoding='utf-8'))
                state=status['status'];error=status.get('error')
                if state in ('pending','running'):state='failed';error='Server restarted; submit a new request'
                if state not in ('completed','failed','cancelled'):continue
                self.jobs[directory.name]=dict(id=directory.name,status=state,request=request,error=error)
                self.save(directory.name)
            except (ValueError,KeyError,OSError):continue
        self.worker=threading.Thread(target=self.loop,daemon=True);self.worker.start()
    @property
    def model_hash(self):return digest(self.root/'results/modelo_3d_manual.json')
    def submit(self,request):
        if request.modelHash!=self.model_hash:raise HTTPException(409,'Base model mismatch; reload base')
        data=json.loads((self.root/'results/modelo_3d_manual.json').read_text(encoding='utf-8'))
        if request.operation=='analysis':
            try:apply(data,[e.model_dump() for e in request.changes])
            except (ValueError,KeyError) as e:raise HTTPException(422,str(e)) from e
        else:
            member=next((e for e in data['elements'] if e['id']==request.elementTag),None)
            if not member or member['type']!='COLUMN':raise HTTPException(422,'Capacity supports reinforced concrete columns only')
        key=str(request.requestId);payload=request.model_dump(mode='json')
        with self.lock:
            if key in self.jobs:
                if self.jobs[key]['request']!=payload:raise HTTPException(409,'Request ID reused for different input')
                return self.public(key)
            if sum(j['status'] in ('pending','running') for j in self.jobs.values())>=8:raise HTTPException(429,'Queue full')
            directory=self.storage/key
            if directory.exists():raise HTTPException(409,'Request ID already exists on disk; use a new ID')
            directory.mkdir();(directory/'request.json').write_text(json.dumps(payload,indent=2),encoding='utf-8')
            self.jobs[key]=dict(id=key,status='pending',request=payload,error=None)
            self.save(key);self.queue.put(key);return self.public(key)
    def save(self,key):
        (self.storage/key/'status.json').write_text(json.dumps(self.public(key),indent=2),encoding='utf-8')
    def public(self,key):
        j=self.jobs[key];return dict(id=key,status=j['status'],error=j.get('error'),operation=j['request']['operation'])
    def stop_process(self):
        p=self.process
        if p is None or p.poll() is not None:return
        if os.name=='nt':subprocess.run(['taskkill','/PID',str(p.pid),'/T','/F'],capture_output=True,timeout=10)
        else:
            import signal
            os.killpg(p.pid,signal.SIGTERM)
        try:p.wait(timeout=10)
        except subprocess.TimeoutExpired:p.kill();p.wait()
    def cancel(self,key):
        with self.lock:
            j=self.jobs[key]
            if j['status'] in ('pending','running'):
                running=j['status']=='running';j['status']='cancelled';self.save(key)
                if running:self.stop_process()
            return self.public(key)
    def loop(self):
        while True:
            key=self.queue.get()
            if key is None:return
            with self.lock:
                if self.jobs[key]['status']!='pending':continue
                self.jobs[key]['status']='running';self.save(key)
            try:
                self.runner(key)
                with self.lock:
                    if self.jobs[key]['status']=='running':self.jobs[key]['status']='completed'
            except Exception as e:
                with self.lock:
                    if self.jobs[key]['status']=='running':self.jobs[key]['status']='failed';self.jobs[key]['error']=str(e)
            finally:
                with self.lock:self.process=None;self.save(key)
    def run_analysis(self,key):
        job=self.storage/key;request=self.jobs[key]['request']
        if digest(self.root/'results/modelo_3d_manual.json')!=request['modelHash']:
            raise RuntimeError('Base model changed after submission; no result published')
        script=self.root/'analysis/load_cases/unity_reanalizar.py'
        command=[sys.executable,'-u',str(script),'--request',str(job/'request.json')]
        if request['operation']=='capacity':
            script=Path(__file__).with_name('capacity_job.py')
            command=[sys.executable,'-u',str(script),'--root',str(self.root),'--request',str(job/'request.json')]
        started=time.monotonic()
        with (job/'analysis.log').open('w',encoding='utf-8') as log:
            with self.lock:
                if self.jobs[key]['status']=='cancelled':return
                self.process=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT,cwd=job,start_new_session=os.name!='nt')
            while self.process.poll() is None:
                if time.monotonic()-started>self.timeout:self.stop_process();raise TimeoutError('Analysis timeout (600 s by default)')
                time.sleep(.1)
            code=self.process.returncode
        if self.jobs[key]['status']=='cancelled':return
        if code:raise RuntimeError('Worker failed; no new results published (code '+str(code)+')')
        if request['operation']=='analysis':
            completion=json.loads((job/'complete.json').read_text(encoding='utf-8'))
            if completion.get('status')!='OK':raise RuntimeError('Incomplete worker verification')
            base=json.loads((self.root/'results/modelo_3d_manual.json').read_text(encoding='utf-8'))
            variant=json.loads((job/'Edificio/results/modelo_3d_manual.json').read_text(encoding='utf-8'))
            if {e['id'] for e in base['elements']}!={e['id'] for e in variant['elements']}:raise RuntimeError('Element tags changed')
            publish(job/'Edificio',job/'delivery',self.root/'visualization/android-ar/tools')
        else:
            folder=job/'delivery';folder.mkdir();source=job/'capacity.json';source.replace(folder/'capacity.json')
            manifest=dict(contractVersion=1,modelHash=request['modelHash'],revision=digest(folder/'capacity.json'),
                          files=[dict(name='capacity.json',sha256=digest(folder/'capacity.json'))])
            (folder/'delivery.json').write_text(json.dumps(manifest),encoding='utf-8')
    def close(self):
        with self.lock:
            for key in self.jobs:self.cancel(key)
            self.closed=True;self.queue.put(None)
        self.worker.join(timeout=15)


def create_app(root=ROOT,storage=None,token=None,runner=None):
    token=token or os.environ.get('MCOC_API_TOKEN')
    if not token or len(token)<24:raise ValueError('MCOC_API_TOKEN requires at least 24 characters')
    storage=storage or Path(os.environ.get('LOCALAPPDATA',str(Path.home())))/'MCOCHonors/Jobs'
    manager=Manager(root,storage,runner=runner)
    @asynccontextmanager
    async def lifespan(app):
        yield
        manager.close()
    app=FastAPI(title='MCOC LAN OpenSees',lifespan=lifespan,docs_url=None,redoc_url=None,openapi_url=None)
    app.state.manager=manager
    def authorize(request:Request,authorization:str=Header(default='')):
        host=request.client.host if request.client else ''
        if host!='testclient':
            try:
                ip=ipaddress.ip_address(host)
                if not (ip.is_private or ip.is_loopback) or ip.is_unspecified:raise ValueError()
            except ValueError:raise HTTPException(403,'LAN clients only')
        if not secrets.compare_digest(authorization,'Bearer '+token):raise HTTPException(401,'Invalid token')
    def known(key):
        if key not in manager.jobs:raise HTTPException(404,'Job not found')
    @app.get('/health',dependencies=[Depends(authorize)])
    def health():return dict(contractVersion=1,modelHash=manager.model_hash,status='ready',timeoutSeconds=manager.timeout)
    @app.post('/jobs',dependencies=[Depends(authorize)],status_code=202)
    def submit(request:JobRequest):return manager.submit(request)
    @app.get('/jobs/{key}',dependencies=[Depends(authorize)])
    def status(key:str):
        known(key)
        with manager.lock:return manager.public(key)
    @app.delete('/jobs/{key}',dependencies=[Depends(authorize)])
    def cancel(key:str):known(key);return manager.cancel(key)
    def delivery(key):
        known(key)
        if manager.jobs[key]['status']!='completed':raise HTTPException(409,'No validated completed result')
        folder=manager.storage/key/'delivery'
        return folder,json.loads((folder/'delivery.json').read_text(encoding='utf-8'))
    @app.get('/jobs/{key}/delivery',dependencies=[Depends(authorize)])
    def manifest(key:str):return delivery(key)[1]
    @app.get('/jobs/{key}/files/{name}',dependencies=[Depends(authorize)])
    def file(key:str,name:str):
        folder,manifest=delivery(key);entry=next((f for f in manifest['files'] if f['name']==name),None)
        if entry is None or Path(name).name!=name:raise HTTPException(404,'File not in manifest')
        if digest(folder/name)!=entry['sha256']:raise HTTPException(409,'Result file hash mismatch')
        return FileResponse(folder/name,filename=name)
    return app


if __name__=='__main__':
    import argparse
    import uvicorn
    p=argparse.ArgumentParser();p.add_argument('--host',default='127.0.0.1');p.add_argument('--port',type=int,default=8765);a=p.parse_args()
    ip=ipaddress.ip_address(a.host)
    if not ip.is_private or ip.is_unspecified:raise ValueError('Bind to a specific private LAN address, never 0.0.0.0')
    uvicorn.run(create_app(),host=a.host,port=a.port)
