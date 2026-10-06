"""Versioned, immutable delivery of ONE structural run. Never edits source results."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
import importlib.util
import csv

VERSION = 1
FIELDS = ('n', 'vy', 'vz', 't', 'my', 'mz')
ROOT = Path(__file__).resolve().parents[1]


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def validate(data):
    nodes = {n['id']: n for n in data['nodes']}
    bars = {b['id']: b for b in data['bars']}
    if len(nodes) != len(data['nodes']) or len(bars) != len(data['bars']):
        raise ValueError('Duplicate analytical ID')
    for b in bars.values():
        if b['i'] not in nodes or b['j'] not in nodes or b['i'] == b['j']:
            raise ValueError('Invalid connectivity')
    if not {'G', 'Q', 'EX', 'EY', 'R'} <= {c['name'] for c in data['cases']}:
        raise ValueError('Incomplete load cases')
    for case in data['cases']:
        if {b['id'] for b in case['bars']} != bars.keys() or len(case['bars']) != len(bars):
            raise ValueError('Case with missing members')
        if {n['id'] for n in case['nodes']} != nodes.keys() or len(case['nodes']) != len(nodes):
            raise ValueError('Case with missing or duplicate nodes')
        for b in case['bars']:
            s = b['s']
            if len(s) < 2 or not all(math.isfinite(x) for x in s) or s[0] != 0 or s[-1] != 1 or any(a >= z for a, z in zip(s, s[1:])):
                raise ValueError('Invalid x/L stations')
            for field in FIELDS:
                if len(b[field]) != len(s) or not all(math.isfinite(v) for v in b[field]):
                    raise ValueError('Invalid station results')
        for n in case['nodes']:
            if len(n['u']) != 3 or len(n['r']) != 3 or not all(math.isfinite(x) for x in n['u'] + n['r']):
                raise ValueError('Invalid nodal response')
    return len(bars)


def campus(data):
    """Adapter only; preserves all saved stations, signs and global nodal DOFs."""
    entries = []
    for bar in data['bars']:
        cases = []
        for c in data['cases']:
            if c['name'] not in ('G', 'Q', 'EX', 'EY', 'R'):
                continue
            b = next(x for x in c['bars'] if x['id'] == bar['id'])
            nodes = {n['id']: n for n in c['nodes']}
            cases.append(dict(name=c['name'], text='OpenSees · '+c['name'], detail='Ejes locales; movimientos globales XYZ',
                              endI=[b[f][0]*(1 if k==0 else -1) for k,f in enumerate(FIELDS)],
                              endJ=[b[f][-1]*(-1 if k==0 else 1) for k,f in enumerate(FIELDS)],
                              moveI=nodes[bar['i']]['u']+nodes[bar['i']]['r'],
                              moveJ=nodes[bar['j']]['u']+nodes[bar['j']]['r'],
                              graphs=[dict(label=f.upper() if f in ('n', 't') else f[0].upper()+f[1:],
                                           unit='kN' if k < 3 else 'kN·m', values=b[f], stations=b['s'])
                                      for k, f in enumerate(FIELDS)]))
        entries.append(dict(key='E:'+str(bar['id']), title='Elemento '+str(bar['id']), summary='Resultados OpenSees',
                            category='Barra', geometry=['i '+str(bar['i'])+' → j '+str(bar['j'])], cases=cases))
    return dict(source='Instantánea de una corrida OpenSees; sin recalcular en el teléfono', entries=entries)


def publish(building, destination, ar_tools=None):
    building, destination = Path(building), Path(destination)
    if destination.exists():
        raise ValueError('Delivery must use a new directory')
    resources = building/'visualization/unity/UnityVisualization/Assets/Resources'
    data = json.loads((resources/'semana4_resultados.json').read_text(encoding='utf-8'))
    validate(data)
    destination.mkdir(parents=True)
    names = ('model_3d.csv', 'semana4_resultados.json', 'semana3_desplazamientos.csv',
             'semana3_esfuerzos_locales.csv', 'semana5_modelo_hash.txt')
    for name in names:
        if not (resources/name).is_file():raise ValueError('Incomplete export: '+name)
    for source in resources.iterdir():
        if source.is_file() and source.suffix in ('.csv','.json','.txt'):
            shutil.copyfile(source,destination/source.name)
    shutil.copyfile(resources/'model_3d.csv', destination/'estructura_principal.csv')
    (destination/'inspeccion_estructural.json').write_text(json.dumps(campus(data), ensure_ascii=False, allow_nan=False), encoding='utf-8')
    model_hash = digest(building/'results/modelo_3d_manual.json')
    if (destination/'semana5_modelo_hash.txt').read_text().strip() != model_hash:
        raise ValueError('Model/export hash mismatch')
    checks_path=building/'results/verificaciones_globales.csv'
    with checks_path.open(encoding='utf-8-sig',newline='') as source:
        checks=list(csv.DictReader(source))
    if not checks or any(row['estado']!='OK' for row in checks):
        raise ValueError('Global verification contains REVISAR or no checks')
    shutil.copyfile(checks_path,destination/'analysis_verifications.csv')
    shutil.copyfile(building/'results/manifest.json',destination/'analysis_manifest.json')
    if ar_tools is not None:
        ar_tools=Path(ar_tools)
        for module_name,filename in (('ar_export','export_ar_data.py'),('overlay_export','export_overlay_geometry.py')):
            spec=importlib.util.spec_from_file_location(module_name,ar_tools/filename);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
            if module_name=='ar_export':module.main(building=building,assets=destination,markers=destination/'unused_markers',regenerate_markers=False,marker_source=ar_tools.parent/'app/src/main/assets')
            else:module.export(building,destination/'overlay_geometry.json')
    files = [dict(name=p.name, sha256=digest(p)) for p in sorted(destination.iterdir()) if p.is_file()]
    manifest = dict(contractVersion=VERSION, modelHash=model_hash, resultsHash=digest(resources/'semana4_resultados.json'),
                    units=dict(length='m', force='kN', moment='kN*m', rotation='rad', stress='kN/m2'),
                    axes=dict(opensees='XYZ right handed', unity='XZY', ar='XYZ right handed'),
                    elementTags=[b['id'] for b in data['bars']], files=files)
    manifest['revision'] = hashlib.sha256(json.dumps(manifest, sort_keys=True).encode()).hexdigest()
    (destination/'delivery.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--destination', required=True, type=Path)
    args = parser.parse_args()
    print(json.dumps(publish(ROOT, args.destination), indent=2))
