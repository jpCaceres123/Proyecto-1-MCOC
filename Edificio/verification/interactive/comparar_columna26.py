"""Compare the supplied C9/ID26 table and refresh stale campus BAR results.

No solver, geometry or saved analytical results are modified. SAP local axes,
end offsets and case definitions are unknown; magnitude differences are not
a validation of the original building. Run with --actualizar-visores to export.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
FIELDS = ('n', 'vy', 'vz', 't', 'my', 'mz')
CASES = ('G', 'Q', 'EX', 'EY', 'R', 'EXG', 'EXQ', 'EYG', 'EYQ')


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main(update=False):
    out = ROOT/'results'
    model = read(out/'modelo_3d_manual.json')
    elements = {e['id']: e for e in model['elements'] if e['type'] != 'WALL'}
    nodes = {n['id']: n for n in model['nodes']}
    element = elements[26]
    xyz = lambda tag: np.array([nodes[tag][f'{a}_m'] for a in 'xyz'])
    length = float(np.linalg.norm(xyz(element['j'])-xyz(element['i'])))
    saved = {}
    inputs = {'model': sha(out/'modelo_3d_manual.json')}
    # Audit all delivered bar stations against stored nodal end actions.
    for case in CASES:
        forces = read(out/f'{case}_fuerzas_locales.json')
        diagrams = read(out/f'{case}_diagramas_barras.json')
        if set(map(int, forces)) != set(elements) or set(map(int, diagrams)) != set(elements):
            raise ValueError(f'{case}: incomplete bar IDs')
        for tag, f in forces.items():
            b = diagrams[tag]
            start = [b[k][0] for k in FIELDS]
            end = [b[k][-1] for k in FIELDS]
            if not np.allclose(start, [f[0], *(-np.asarray(f[1:6]))], atol=1e-6, rtol=1e-7):
                raise ValueError(f'{case}/{tag}: inconsistent i section')
            if not np.allclose(end, [-f[6], *f[7:12]], atol=1e-6, rtol=1e-7):
                raise ValueError(f'{case}/{tag}: inconsistent j section')
        with np.load(out/f'{case}.npz') as response:
            displacements = {int(tag): list(map(float, u)) for tag, u in zip(response['node_tags'], response['u'])}
            reactions = ({int(tag): list(map(float, r)) for tag, r in zip(response['support_tags'], response['reaction'])}
                         if {'support_tags','reaction'} <= set(response.files) else {})
        saved[case] = (forces, diagrams, displacements, reactions)
        for suffix in ('fuerzas_locales.json', 'diagramas_barras.json'):
            inputs[f'{case}_{suffix}'] = sha(out/f'{case}_{suffix}')
        inputs[f'{case}.npz'] = sha(out/f'{case}.npz')
    reference_path = ROOT/'data/references/columna26_C9_foto.csv'
    with reference_path.open(encoding='utf-8-sig', newline='') as stream:
        reference = list(csv.DictReader(stream))
    comparisons = []
    for row in reference:
        case = row['caso_modelo']
        end = 0 if int(row['estacion_mm']) == 0 else -1
        b = saved[case][1]['26']
        for field, column in zip(FIELDS, ('P_N', 'V2_N', 'V3_N', 'T_Nmm', 'M2_Nmm', 'M3_Nmm')):
            original = int(row[column]) / (1000 if field in FIELDS[:3] else 1_000_000)
            value = b[field][end]
            comparisons.append(dict(caso=case,extremo='i' if end==0 else 'j',
                estacion_referencia_m=int(row['estacion_mm'])/1000,
                estacion_modelo_m=0 if end==0 else length,componente=field,
                unidad='kN' if field in FIELDS[:3] else 'kN*m',
                referencia=original,modelo_seccion=value,
                diferencia_magnitud_pct=(abs(value)/abs(original)-1)*100,
                ejes_originales_confirmados=False))
    stale = []
    unavailable = []
    for project in ('CampusPlayable', 'CampusCardboard'):
        destination = ROOT/f'visualization/unity/{project}/Assets/Resources/inspeccion_estructural.json'
        database = read(destination)
        changed = 0
        for entry in database['entries']:
            if not entry['key'].startswith('E:'):
                continue
            tag = entry['key'][2:]
            if int(tag) not in elements:
                unavailable.append(dict(project=project,key=entry['key']))
                # Retain visual IDs, but do not present results from a different topology.
                entry['summary']='Sin resultados en la corrida actual'
                entry['cases']=[dict(name=c['name'],text='No disponible',detail='ID ausente de los resultados actuales') for c in entry['cases']]
                continue
            bar = elements[int(tag)]
            for case in entry['cases']:
                name = case['name']
                if name not in saved:
                    raise ValueError(f'Unknown case {name}')
                forces, diagrams, movements, reactions = saved[name]
                f, b = forces[tag], diagrams[tag]
                stale_case = not np.allclose(case['endI'],f[:6]) or not np.allclose(case['endJ'],f[6:])
                stale_case |= any(not np.allclose(g['values'],b[field]) for field,g in zip(FIELDS,case['graphs']))
                changed += bool(stale_case)
                if int(tag)==26 and name in ('G','Q'):
                    stale.append(dict(project=project,case=name,previous_axial_i_kN=case['endI'][0],current_axial_i_kN=f[0]))
                case.update(endI=f[:6],endJ=f[6:],moveI=movements[bar['i']],moveJ=movements[bar['j']],
                    reactionI=reactions.get(bar['i']),reactionJ=reactions.get(bar['j']))
                case['graphs']=[dict(label=label,unit='kN' if k<3 else 'kN·m',values=b[field],stations=b['s'])
                    for k,(field,label) in enumerate(zip(FIELDS,('N','Vy','Vz','T','My','Mz')))]
            # Metadata and tributary loads are retained; only saved bar responses refreshed.
        if update:
            database['barResultsProvenance']={'inputs_sha256':inputs,'reference_axes_verified':False}
            database['source']='Barras actualizadas desde Edificio/results; kN, m, kN*m. N de sección positivo en compresión. Sin equivalencia SAP confirmada.'
            destination.write_text(json.dumps(database,ensure_ascii=False,allow_nan=False),encoding='utf-8')
        print(f'{project}: {changed} stale bar/case records; updated={update}')
    report = dict(element_id=26,reference_label='C9',mapping_source='User statement; unverified without original model',
        model_length_m=length,reference_length_m=3.160,
        original_CM_axial_drop_kN=(3556504-3518542)/1000,
        model_G_axial_drop_kN=saved['G'][1]['26']['n'][0]-saved['G'][1]['26']['n'][-1],
        internal_station_equilibrium_checked_cases=len(CASES),bars_per_case=len(elements),
        stale_inspector=stale,visual_ids_without_current_results=unavailable,comparisons=comparisons,inputs_sha256=inputs,
        conclusions=['Inspector bar results were stale.',
            'Nodal end actions differ in sign from section diagrams; use section diagrams for internal-force cards.',
            'Reference length differs by 0.800 m; end offsets/clear length are a hypothesis, not confirmed.',
            'CM=G and CV=Q are tentative mappings; original axes and loading unavailable.',
            'No analytical inputs adjusted to fit the photograph.'])
    folder=ROOT/'documentation/comparacion_columna26';folder.mkdir(parents=True,exist_ok=True)
    (folder/'comparacion.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
    with (folder/'comparacion.csv').open('w',encoding='utf-8-sig',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(comparisons[0]));writer.writeheader();writer.writerows(comparisons)
    return report


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--actualizar-visores',action='store_true')
    args=parser.parse_args()
    result=main(args.actualizar_visores)
    print(json.dumps({k:result[k] for k in ('model_length_m','reference_length_m','original_CM_axial_drop_kN','model_G_axial_drop_kN')},indent=2))
