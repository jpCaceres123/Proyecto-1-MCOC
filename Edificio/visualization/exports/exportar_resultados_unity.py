"""Exporta ejes, malla, seis GDL y diagramas de barra para Semana 4.

No resuelve ni modifica el edificio: construye su topología y lee resultados
existentes. Los diagramas por estaciones ya incluyen las cargas distribuidas.
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


def element_metadata(data):
    sections = {
        'COLUMN': 'section_columns',
        'STEEL_COLUMN_SHS300x20': 'section_steel_columns',
        'BEAM_SMALL': 'section_small_beams',
        'BEAM_VARIABLE': 'section_variable_beams',
        'BEAM_40x60': 'section_40x60_beams',
    }
    nodes = {node['id']: node for node in data['nodes']}
    result = []
    for element in data['elements']:
        if element['type'] == 'WALL':
            continue
        section_key = sections.get(element['type'], 'section_beams')
        material_key = 'material_steel' if element['type'] == 'STEEL_COLUMN_SHS300x20' else 'material'
        section = element.get('section_override', data[section_key])
        material = data[material_key]
        result.append({
            'id': element['id'],
            'type': element['type'],
            'i': element['i'],
            'j': element['j'],
            'section': section_key,
            'sectionData': section,
            'material': material_key,
            'materialData': material,
            'restraints': {
                'i': bool(nodes[element['i']].get('restraint', False)),
                'j': bool(nodes[element['j']].get('restraint', False)),
            },
        })
    return result


def wall_demands(data, out, shells):
    """Return P-M demands at the bottom cut of every wall-floor segment."""
    coords = {node: np.asarray(ops.nodeCoord(node), dtype=float) for node in ops.getNodeTags()}
    by_wall = {}
    for shell in shells:
        by_wall.setdefault(shell['wall'], []).append(shell)
    demands = []
    for wall_id, wall_shells in sorted(by_wall.items()):
        wall = next(w for w in data['walls'] if w['id'] == wall_id)
        start = np.array([wall['x_i_m'], wall['y_i_m'], wall['z_i_m']], dtype=float)
        end = np.array([wall['x_j_m'], wall['y_j_m'], wall['z_j_m']], dtype=float)
        longitudinal = end - start
        longitudinal[2] = 0.0
        longitudinal /= np.linalg.norm(longitudinal)
        transverse = np.cross(np.array([0.0, 0.0, 1.0]), longitudinal)
        center = (start + end) / 2.0
        bottom_levels = sorted({min(coords[node][2] for node in shell['nodes']) for shell in wall_shells})
        top_level = max(max(coords[node][2] for node in shell['nodes']) for shell in wall_shells)
        segment_demands = []
        for bottom_z in bottom_levels:
            bottom = [shell for shell in wall_shells
                      if abs(min(coords[node][2] for node in shell['nodes']) - bottom_z) <= 1e-8]
            center[2] = bottom_z
            case_values = []
            for case in CASES[:5]:
                forces = json.loads((out / f'{case}_fuerzas.json').read_text(encoding='utf-8'))
                resultant = np.zeros(3)
                moment = 0.0
                for shell in bottom:
                    values = forces[str(shell['id'])]
                    for index, node in enumerate(shell['nodes']):
                        if abs(coords[node][2] - bottom_z) > 1e-8:
                            continue
                        force = np.asarray(values[index * 6:index * 6 + 3], dtype=float)
                        nodal_moment = np.asarray(values[index * 6 + 3:index * 6 + 6], dtype=float)
                        resultant += force
                        moment += np.dot(np.cross(coords[node] - center, force) + nodal_moment, transverse)
                case_values.append(dict(name=case, p=float(resultant[2]), m=float(abs(moment))))
            top_z = min((z for z in bottom_levels if z > bottom_z), default=top_level)
            segment_demands.append(dict(z_min=bottom_z, z_max=top_z, demands=case_values))
        demands.append(dict(id=wall_id, demands=segment_demands[0]['demands'], segments=segment_demands))
    return demands


def capacity_material():
    parameters = json.loads((ROOT / 'data' / 'parameters' / 'parametros.json').read_text(encoding='utf-8'))
    column = parameters['columna']
    return {key: column[key] for key in ('fc_MPa', 'fy_MPa', 'Es_MPa')}


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
    wall_nodes = {w['id']: set(w['node_ids']) for w in data['wall_mesh']['walls']}
    shells = []
    bar_tags = {b['id'] for b in bars}
    for tag in sorted(ops.getEleTags()):
        if tag in bar_tags:
            continue
        connectivity = list(ops.eleNodes(tag))
        owners = [wall for wall, ids in wall_nodes.items() if set(connectivity) <= ids]
        if len(connectivity) != 4 or len(owners) != 1:
            raise ValueError(f'Shell {tag} sin identificación única de muro')
        shells.append(dict(id=tag, wall=owners[0], nodes=connectivity))
    responses = []
    for case in CASES:
        with np.load(out / f'{case}.npz') as result:
            tags = result['node_tags'].astype(int).tolist()
            if tags != nodes or result['u'].shape != (len(nodes), 6):
                raise ValueError(f'{case}: topología distinta; regenerar análisis antes de exportar')
            if not np.isfinite(result['u']).all():
                raise ValueError(f'{case}: desplazamientos no finitos')
            diagrams = json.loads((out / f'{case}_diagramas_barras.json').read_text(encoding='utf-8'))
            if set(map(int, diagrams)) != bar_tags:
                raise ValueError(f'{case}: diagramas de barras incompletos')
            responses.append(dict(name=case,
                nodes=[dict(id=tag, u=u[:3].tolist(), r=u[3:].tolist()) for tag, u in zip(tags, result['u'])],
                bars=[dict(id=int(tag), **stations) for tag, stations in diagrams.items()]))
    payload = dict(schema=2, axes='OpenSees global XYZ, right handed',
                   units='m, rad, kN, kN*m', memberLoads='distributed_with_station_results',
                   nodes=[dict(id=n, xyz=list(ops.nodeCoord(n))) for n in nodes],
                   elementMetadata=element_metadata(data),
                   capacityMaterial=capacity_material(),
                   wallDemands=wall_demands(data, out, shells),
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
