"""Exporta repartos existentes sin volver a resolver el modelo (solo biblioteca estándar)."""
import csv
import json
from pathlib import Path


def export_repartition(model, cfg, target):
    groups = {slab['id']: {} for slab in model['slabs']}
    for kind in ('beam', 'wall'):
        for row in model[f'{kind}_load_cases']:
            values = groups[row['slab_id']].setdefault((kind, row[f'{kind}_id']), [0., 0., 0.])
            values[0] += row['tributary_area_m2']
            values[1] += row['dead_load_kN']
            q = row['q_SC_kN_m2'] if cfg['q_Q_kN_m2'] is None else cfg['q_Q_kN_m2']
            values[2] += row['tributary_area_m2'] * q
    rows = []
    for slab in model['slabs']:
        for (kind, tag), (area, dead, live) in sorted(groups[slab['id']].items()):
            rows.append(dict(losa_id=slab['id'], receptor=kind, id=tag, area_m2=area,
                peso_propio_kN=area*slab['thickness_m']*slab['density_kg_m3']*9.80665/1000,
                G_kN=dead, Q_kN=live))
    with target.open('w', newline='', encoding='utf-8-sig') as stream:
        writer = csv.DictWriter(stream, fieldnames=['losa_id','receptor','id','area_m2','peso_propio_kN','G_kN','Q_kN'])
        writer.writeheader()
        writer.writerows(rows)
    return rows


if __name__ == '__main__':
    root = Path(__file__).resolve().parent
    model = json.loads((root.parent/'P1L2/outputs/modelo_3d_manual.json').read_text(encoding='utf-8'))
    cfg = json.loads((root/'parametros.json').read_text(encoding='utf-8'))
    rows = export_repartition(model, cfg, root.parent/'P1L2/UnityVisualization/Assets/Resources/semana3_reparto_losas.csv')
    print(f'Exportados {len(rows)} receptores de {len(model["slabs"])} losas.')
