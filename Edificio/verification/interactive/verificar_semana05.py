"""Contrasta los datos exportados al visor con tres soluciones OpenSees nuevas."""
from pathlib import Path
import csv
import json
import sys
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'analysis/load_cases'))
import casos

STATES = {'S1': [1, 1, 0, 0], 'S2': [1.2, 1.4, .8, -.3], 'S3': [1, .5, -1, .7]}

def main():
    cfg = json.loads((ROOT / 'data/parameters/parametros.json').read_text(encoding='utf-8'))
    resources = ROOT / 'visualization/unity/UnityVisualization/Assets/Resources'
    names = ['G', 'Q', 'EX', 'EY']
    def read(filename, key, fields):
        data = {c: {} for c in names + ['EXG', 'EXQ', 'EYG', 'EYQ']}
        with (resources / filename).open(encoding='utf-8-sig') as stream:
            for row in csv.DictReader(stream):
                if row['caso'] in data:
                    data[row['caso']][int(row[key])] = [float(row[f]) for f in fields]
        return data
    disps = read('semana3_desplazamientos.csv', 'nodo', ['ux_m', 'uy_m', 'uz_m'])
    local = read('semana3_esfuerzos_locales.csv', 'elemento',
                 ['Ni_kN','Vyi_kN','Vzi_kN','Ti_kNm','Myi_kNm','Mzi_kNm',
                  'Nj_kN','Vyj_kN','Vzj_kN','Tj_kNm','Myj_kNm','Mzj_kNm'])
    # ApplyMass/TryForces reconstruyen EX/EY incluso con los factores iniciales.
    for exported, dtype in [(disps, np.float32), (local, np.float64)]:
        for direction in ('EX', 'EY'):
            gbase, qbase = exported[direction+'G'], exported[direction+'Q']
            assert set(gbase) == set(qbase) == set(exported[direction])
            exported[direction] = {
                i: dtype(cfg['ponderador_G_masa'])*np.array(gbase[i],dtype=dtype)
                 + dtype(cfg['fraccion_Q_masa'])*np.array(qbase[i],dtype=dtype)
                for i in gbase}
    reactions = {c: np.load(ROOT / f'results/{c}.npz') for c in names}
    records = []
    for state, factors in STATES.items():
        explicit = casos.solve(dict(zip(names, factors)), cfg)
        for field, exported, ids, source, width in [
            ('desplazamiento_m', disps, sorted(disps['G']),
             dict(zip(map(int, explicit['node_tags']), explicit['u'][:, :3])), 3),
            ('fuerza_local_kN_kNm', local, sorted(local['G']),
             {int(k): v for k,v in explicit['local_forces'].items()}, 12),
            ('reaccion_kN_kNm', None, list(map(int, explicit['support_tags'])),
             dict(zip(map(int, explicit['support_tags']), explicit['support_r'])), 6)]:
            actual = np.array([source[i] for i in ids])
            # Unity acumula Vector3 en float32; fuerzas locales en double.
            dtype = np.float32 if field == 'desplazamiento_m' else np.float64
            combined = np.zeros(actual.shape, dtype=dtype)
            for case, factor in zip(names, factors):
                if exported is None:
                    mapping = dict(zip(map(int,reactions[case]['support_tags']),reactions[case]['reaction']))
                else:
                    mapping = exported[case]
                assert set(ids) <= set(mapping), (case, field, 'IDs faltantes')
                combined += dtype(factor) * np.array([mapping[i] for i in ids], dtype=dtype)
            error = float(np.max(np.abs(combined-actual)))
            relative = error / max(1e-12, float(np.max(np.abs(actual))))
            row, col = np.unravel_index(np.argmax(np.abs(actual)), actual.shape)
            records.append(dict(estado=state, factores=factors, respuesta=field,
                                id=ids[row], componente=int(col+1),
                                superpuesta=float(combined[row,col]), explicita=float(actual[row,col]),
                                error_max=error, error_relativo=relative, tolerancia=1e-5, ok=relative <= 1e-5))
    out = ROOT.parent / 'reports/semana05_evidencias'
    out.mkdir(parents=True, exist_ok=True)
    (out/'superposicion.json').write_text(json.dumps(records, indent=2), encoding='utf-8')
    with (out/'superposicion.csv').open('w',newline='',encoding='utf-8') as f:
        writer=csv.DictWriter(f,fieldnames=list(records[0])); writer.writeheader(); writer.writerows(records)
    print(json.dumps(records, indent=2))
    return 0 if all(r['ok'] for r in records) else 1

if __name__ == '__main__':
    raise SystemExit(main())
