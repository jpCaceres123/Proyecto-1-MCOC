"""Exporta ejes OpenSees, malla y seis GDL para el postproceso de Semana 4.

No resuelve ni modifica el edificio: construye su topología y lee resultados
existentes. Las barras actuales sólo reciben cargas nodales, de modo que sus
esfuerzos interiores se interpolan desde las acciones de extremo.
"""
import argparse
import json
from pathlib import Path
import sys

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'model' / 'opensees'))
import modelo_opensees_3d as model
import openseespy.opensees as ops

CASES = ('G', 'Q', 'EX', 'EY', 'R', 'EXG', 'EXQ', 'EYG', 'EYQ')


def export_results(out=None, destination=None):
    out = Path(out or ROOT / 'results')
    destination = Path(destination or ROOT / 'visualization' / 'unity' /
                       'UnityVisualization' / 'Assets' / 'Resources' / 'semana4_resultados.json')
    data = model.build_model()
    nodes = sorted(ops.getNodeTags())
    bars = []
    for e in data['elements']:
        if e['type'] == 'WALL':
            continue
        axes = [ops.eleResponse(e['id'], name) for name in ('xlocal', 'ylocal', 'zlocal')]
        basis = np.asarray(axes, dtype=float)
        if basis.shape != (3, 3) or not np.allclose(basis @ basis.T, np.eye(3), atol=1e-10):
            raise ValueError(f"Ejes inválidos en barra {e['id']}")
        bars.append(dict(id=e['id'], i=e['i'], j=e['j'], x=axes[0], y=axes[1], z=axes[2]))
    shell_owner = {int(tag): int(w['id']) for w in data['wall_mesh']['walls']
                   for tag in w.get('shell_element_ids', [])}
    shells = []
    bar_tags = {b['id'] for b in bars}
    for tag in sorted(ops.getEleTags()):
        if tag in bar_tags:
            continue
        connectivity = list(ops.eleNodes(tag))
        owner = shell_owner.get(int(tag))
        if len(connectivity) != 4 or owner is None:
            raise ValueError(f'Shell {tag} sin identificación única de muro')
        shells.append(dict(id=tag, wall=owner, nodes=connectivity))
    responses = []
    for case in CASES:
        with np.load(out / f'{case}.npz') as result:
            tags = result['node_tags'].astype(int).tolist()
            if tags != nodes or result['u'].shape != (len(nodes), 6):
                raise ValueError(f'{case}: topología distinta; regenerar análisis antes de exportar')
            if not np.isfinite(result['u']).all():
                raise ValueError(f'{case}: desplazamientos no finitos')
            responses.append(dict(name=case, nodes=[dict(id=tag, u=u[:3].tolist(), r=u[3:].tolist())
                                                   for tag, u in zip(tags, result['u'])]))
    payload = dict(schema=1, axes='OpenSees global XYZ, right handed',
                   units='m, rad, kN, kN*m', memberLoads='nodal_only',
                   nodes=[dict(id=n, xyz=list(ops.nodeCoord(n))) for n in nodes],
                   bars=bars, shells=shells, cases=responses)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(payload, ensure_ascii=False, allow_nan=False), encoding='utf-8')
    return dict(nodes=len(nodes), bars=len(bars), shells=len(shells), cases=len(responses))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path)
    parser.add_argument('--destination', type=Path)
    args = parser.parse_args()
    print(json.dumps(export_results(args.out, args.destination)))
