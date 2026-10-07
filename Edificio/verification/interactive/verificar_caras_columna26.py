"""Re-solve G/Q and evaluate the proposed 0.40 m end exclusion separately.

Original analytical files and structural input are not overwritten. Reading
sections at beam faces is not equivalent to introducing rigid joint offsets.
"""
import csv
import json
from pathlib import Path
import sys
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
FIELDS=('n','vy','vz','t','my','mz')
sys.path.insert(0,str(ROOT/'analysis/load_cases'))
import casos


def main():
    model=json.loads((ROOT/'results/modelo_3d_manual.json').read_text())
    config=json.loads((ROOT/'data/parameters/parametros.json').read_text())
    nodes={n['id']:n for n in model['nodes']}
    element=next(e for e in model['elements'] if e['id']==26)
    xyz=lambda tag:np.array([nodes[tag][f'{a}_m'] for a in 'xyz'])
    length=float(np.linalg.norm(xyz(element['j'])-xyz(element['i'])))
    offsets=(.40,.40)
    clear=length-sum(offsets)
    with (ROOT/'data/references/columna26_C9_foto.csv').open(newline='',encoding='utf-8') as file:
        reference=list(csv.DictReader(file))
    records=[];checks=[]
    for name in ('G','Q'):
        print(f'Resolving {name}',flush=True)
        fresh=casos.solve({name:1},config)
        stored=json.loads((ROOT/f'results/{name}_fuerzas_locales.json').read_text())
        max_delta=max(float(np.max(np.abs(np.asarray(value)-stored[tag]))) for tag,value in fresh['local_forces'].items())
        force_error=float(np.linalg.norm(fresh['support_sum'][:3]+fresh['applied_sum'][:3]))
        scale=max(1.,float(np.linalg.norm(fresh['applied_sum'][:3])))
        check=dict(case=name,max_end_action_difference=max_delta,
                   equilibrium_force_relative_error=force_error/scale,
                   equilibrium_force_pass=force_error/scale<=1e-4,
                   stored_reproduction_pass=max_delta<1e-5,
                   moment_equilibrium_not_certified='Inherited equalDOF constraints at non-coincident wall/frame nodes')
        checks.append(check)
        b=fresh['bar_diagrams']['26']
        for row in (r for r in reference if r['caso_modelo']==name):
            station=int(row['estacion_mm'])/1000
            x=offsets[0]+station
            if x>length-offsets[1]+1e-9:raise ValueError('Reference station outside proposed free span')
            for field,ref_column in zip(FIELDS,
                                       ('P_N','V2_N','V3_N','T_Nmm','M2_Nmm','M3_Nmm')):
                value=float(np.interp(x/length,b['s'],b[field]))
                reference_value=int(row[ref_column])/(1000 if field in ('n','vy','vz') else 1e6)
                records.append(dict(case=name,reference_station_m=station,model_cut_x_m=x,
                    field=field,unit='kN' if field in ('n','vy','vz') else 'kN*m',
                    reference=reference_value,model_section=value,
                    magnitude_difference_pct=(abs(value)/abs(reference_value)-1)*100,
                    axes_verified=False))
    # Inspect ALL coincident nodes at each endpoint for incident beams.
    incident={}
    for end in ('i','j'):
        coordinate=xyz(element[end])
        tags={tag for tag in nodes if np.linalg.norm(xyz(tag)-coordinate)<1e-8}
        beams=[dict(id=e['id'],type=e['type']) for e in model['elements']
               if e['type'].startswith('BEAM') and (e['i'] in tags or e['j'] in tags)]
        incident[end]=beams
    weight=model['section_columns']['A_m2']*config['peso_especifico_HA_kN_m3']*clear
    axial=[r['model_section'] for r in records if r['case']=='G' and r['field']=='n']
    report=dict(offset_hypothesis_m=offsets,clear_length_m=clear,
        treatment='Section readout only; analytical rigid offsets NOT introduced',
        incident_beams=incident,fresh_solver_checks=checks,face_comparisons=records,
        clear_span_self_weight_kN=weight,model_face_axial_drop_kN=axial[0]-axial[1],
        reference_axial_drop_kN=37.962,
        note='Lower node has no incident beam in current topology; cannot substantiate two rigid joint zones from that topology.')
    output=ROOT/'documentation/comparacion_columna26'
    (output/'verificacion_caras.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
    with (output/'comparacion_caras.csv').open('w',newline='',encoding='utf-8-sig') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(records[0]));writer.writeheader();writer.writerows(records)
    print(json.dumps(report,ensure_ascii=False,indent=2))
    if not all(c['stored_reproduction_pass'] and c['equilibrium_force_pass'] for c in checks):
        raise RuntimeError('Reproduction or force equilibrium check failed; see report')


if __name__=='__main__':main()
