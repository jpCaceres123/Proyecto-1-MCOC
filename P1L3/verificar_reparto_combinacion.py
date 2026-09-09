"""Verifica los datos del visor sin OpenSees: reparto y superposición de R."""
import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
RES = ROOT.parent/'P1L2/UnityVisualization/Assets/Resources'


def read(name):
    with (RES/name).open(encoding='utf-8-sig', newline='') as stream:
        return list(csv.DictReader(stream))


weights = {r['losa_id']: r for r in read('semana3_pesos_losas.csv')}
totals = {key: [0., 0.] for key in weights}
keys = set()
for row in read('semana3_reparto_losas.csv'):
    key = (row['losa_id'], row['receptor'], row['id'])
    assert key not in keys, f'Receptor duplicado: {key}'
    keys.add(key)
    for i, field in enumerate(('G_kN', 'Q_kN')):
        totals[row['losa_id']][i] += float(row[field])
for key, weight in weights.items():
    for i, field in enumerate(('G_losa_kN', 'Q_losa_kN')):
        assert abs(totals[key][i]-float(weight[field])) < 1e-7, (key, field)
print(f'Reparto: {len(weights)} losas conservan G/Q; receptores sin duplicados.')

cfg = json.loads((ROOT/'parametros.json').read_text(encoding='utf-8'))
cases = ('G', 'Q', 'EX', 'EY')
for filename, idfields, fields in (
    ('semana3_desplazamientos.csv', ('nodo',), ('ux_m','uy_m','uz_m')),
    ('semana3_pisos.csv', ('bloque','z_m'), ('ux_cm_m','uy_cm_m','rz_rad')),
    ('semana3_esfuerzos_locales.csv', ('elemento',),
     ('Ni_kN','Vyi_kN','Vzi_kN','Ti_kNm','Myi_kNm','Mzi_kNm',
      'Nj_kN','Vyj_kN','Vzj_kN','Tj_kNm','Myj_kNm','Mzj_kNm')),
):
    records = {(r['caso'], tuple(r[k] for k in idfields)): r for r in read(filename)}
    delta = scale = 0.
    for (case, key), reference in records.items():
        if case != 'R': continue
        for field in fields:
            base = [float(records[(c,key)][field]) for c in cases]
            value = sum(cfg['combinacion'][c]*v for c,v in zip(cases,base))
            expected = float(reference[field])
            delta = max(delta,abs(value-expected))
            scale = max(scale,abs(expected),abs(value))
    relative = delta/max(1e-12,scale)
    assert relative < 1e-5, (filename,relative)
    print(f'{filename}: R reproduce análisis (error relativo {relative:.3e}).')
