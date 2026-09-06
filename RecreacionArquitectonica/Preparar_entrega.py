"""Empaqueta el proyecto editable y el visor, sin cachés de Unity."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parent
target = root.parent / 'Entrega_RecreacionArquitectonica.zip'
excluded = {'Library', 'Temp', 'Logs', 'obj', 'UserSettings', 'work', '__pycache__'}
with ZipFile(target, 'w', compression=ZIP_DEFLATED, compresslevel=6) as archive:
    for path in sorted(root.rglob('*')):
        if path.is_file() and not any(p in excluded for p in path.relative_to(root).parts):
            archive.write(path, path.relative_to(root.parent))
    launcher = root.parent / 'ABRIR_RECREACION_ARQUITECTONICA.cmd'
    archive.write(launcher, launcher.name)
with ZipFile(target) as archive:
    assert archive.testzip() is None
    print(f'ZIP verificado: {len(archive.namelist())} archivos; {target.stat().st_size / 1024**2:.1f} MiB')
print(target)
