"""Runs real HTTP/Unity/OpenSees integration on loopback, closes its own server."""
import argparse
import json
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import tempfile
import threading
import time
import importlib.util
import uvicorn

ROOT=Path(__file__).resolve().parents[2]


def main():
    p=argparse.ArgumentParser();p.add_argument('--unity',required=True,type=Path);a=p.parse_args()
    spec=importlib.util.spec_from_file_location('honors_server',ROOT/'analysis/backend/server.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    evidence=Path(tempfile.mkdtemp(prefix='MCOC-unity-http-'));token=secrets.token_urlsafe(32)
    with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
    config=evidence/'backend.json';config.write_text(json.dumps(dict(server=f'http://127.0.0.1:{port}',token=token)),encoding='utf-8')
    server=uvicorn.Server(uvicorn.Config(module.create_app(storage=evidence/'Jobs',token=token),host='127.0.0.1',port=port,log_level='warning'))
    thread=threading.Thread(target=server.run,daemon=True);thread.start()
    try:
        deadline=time.monotonic()+30
        while not server.started:
            if not thread.is_alive() or time.monotonic()>deadline:raise RuntimeError('Server did not start')
            time.sleep(.1)
        log=evidence/'unity.log'
        code=subprocess.call([str(a.unity),'-batchmode','-projectPath',str(ROOT/'visualization/unity/CampusCardboard'),
                              '-executeMethod','CampusCardboardValidation.Run','-campus-vr-check','-campus-vr-network-check',
                              '--backend-config='+str(config),'-logFile',str(log)],
                             creationflags=subprocess.CREATE_NO_WINDOW if sys.platform=='win32' else 0)
        text=log.read_text(encoding='utf-8',errors='replace')
        success=code==0 and 'http_opensees_new_verified_revision=True' in text and 'http_reinforcement_regeneration=True' in text
        print(json.dumps(dict(status='OK' if success else 'REVISAR',evidence=str(evidence),unityExitCode=code),indent=2),flush=True)
        if not success:raise RuntimeError('Unity HTTP test did not pass; inspect evidence log')
    finally:
        server.should_exit=True;thread.join(timeout=20)
        # Configuration contains a short-lived token, retained only with local test evidence.


if __name__=='__main__':main()
