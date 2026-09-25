"""Windows Unity bridge: immutable job directory; never overwrite source results."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--request', required=True, type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    job = args.request.resolve().parent
    request = json.loads(args.request.read_text(encoding='utf-8-sig'))
    work = job/'Edificio'
    if work.exists():
        raise ValueError('Use una carpeta nueva para cada calculo')
    for folder in ('analysis', 'model', 'data', 'verification'):
        shutil.copytree(root/folder, work/folder, ignore=shutil.ignore_patterns('__pycache__'))
    for folder in ('exports', 'plots'):
        shutil.copytree(root/'visualization'/folder, work/'visualization'/folder,
                        ignore=shutil.ignore_patterns('__pycache__'))
    resources = work/'visualization/unity/UnityVisualization/Assets/Resources'
    resources.mkdir(parents=True)
    (work/'documentation').mkdir()
    geometry = json.loads((root/'data/geometry/geometria_manual.json').read_text(encoding='utf-8'))
    geometry['interactive_changes'] = request['changes']
    source = work/'data/geometry/geometria_manual.json'
    source.write_text(json.dumps(geometry, indent=2), encoding='utf-8')
    for script in ('model/builders/generar_modelo_manual.py', 'analysis/load_cases/ejecutar.py'):
        subprocess.run([sys.executable, str(work/script)], cwd=job, check=True)
    required = ('model_3d.csv', 'semana4_resultados.json', 'semana3_desplazamientos.csv',
                'semana3_esfuerzos_locales.csv', 'semana5_modelo_hash.txt')
    for name in required:
        if not (resources/name).is_file():
            raise RuntimeError('Exportacion incompleta: '+name)
    (job/'complete.json').write_text(json.dumps({'resources': str(resources),
        'changes': request['changes'], 'status': 'OK'}), encoding='utf-8')
    print('UNITY_REANALYSIS_OK', flush=True)


if __name__ == '__main__':
    main()
