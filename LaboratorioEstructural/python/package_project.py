"""Portable delivery archive; excludes Unity caches and intermediate work."""
from pathlib import Path
import zipfile
from model import ROOT

def package():
    parent=ROOT.parent;target=parent/'Entrega_LaboratorioEstructural.zip'
    excluded={'Library','Temp','Logs','obj','UserSettings','__pycache__','work','.git','.venv'}
    with zipfile.ZipFile(target,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
        for p in sorted(ROOT.rglob('*')):
            if not p.is_file():continue
            relative=p.relative_to(ROOT)
            if any(part in excluded for part in relative.parts):continue
            if p.suffix in {'.tmp','.pyc'}:continue
            archive.write(p,p.relative_to(parent))
        for name in ['modelo_estructural_completo.xlsx','INICIAR_LABORATORIO.cmd']:
            archive.write(parent/name,name)
    with zipfile.ZipFile(target) as archive:
        bad=archive.testzip()
        if bad:raise RuntimeError('Archive integrity failed: '+bad)
        print(f'Archive verified: {len(archive.namelist())} entries; {target.stat().st_size/1024**2:.1f} MiB')
    return target

if __name__=='__main__':print(package())
