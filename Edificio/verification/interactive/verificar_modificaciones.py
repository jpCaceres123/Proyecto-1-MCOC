"""Reproducible intensity and beam-section variants, with base restoration."""
import csv, hashlib, json, shutil, subprocess, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
PROJECT=ROOT.parent
PY=sys.executable
EVID=ROOT/'documentation'/'semana05_evidencias'; EVID.mkdir(parents=True,exist_ok=True)
GEOM=ROOT/'data'/'geometry'/'geometria_manual.json'
PARAM=ROOT/'data'/'parameters'/'parametros.json'
BUILDER=ROOT/'model'/'builders'/'generar_modelo_manual.py'
RUNNER=ROOT/'analysis'/'load_cases'/'ejecutar.py'
RES=ROOT/'visualization'/'unity'/'UnityVisualization'/'Assets'/'Resources'
def run(args):
    print('+',' '.join(map(str,args)),flush=True);subprocess.run([PY,*map(str,args)],cwd=PROJECT,check=True)
def result():
    rows=list(csv.DictReader((RES/'semana3_desplazamientos.csv').open(encoding='utf-8-sig',newline='')))
    chosen=[r for r in rows if r['caso']=='R']
    peak=max(chosen,key=lambda r:abs(float(r['uz_m'])))
    forces=list(csv.DictReader((RES/'semana3_esfuerzos_locales.csv').open(encoding='utf-8-sig',newline='')))
    beam=next(r for r in forces if r['caso']=='R' and r['elemento']=='207')
    return {'case':'R','node':peak['nodo'],'max_abs_uz_m':abs(float(peak['uz_m'])),'uz_at_peak_m':float(peak['uz_m']),
            'element_id':207,'member_action':{'Vzi_kN':float(beam['Vzi_kN']),'Myi_kNm':float(beam['Myi_kNm'])}}
def resource_hashes():
    return {p.relative_to(RES).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(RES.rglob('*')) if p.is_file()}
base_geom=json.loads(GEOM.read_text(encoding='utf-8')); base_cfg=json.loads(PARAM.read_text(encoding='utf-8'))
section_path=EVID/'mod_seccion_viga_207_geometria.json'
variant=dict(base_geom); variant['element_section_overrides']={'207':{'b_m':.75,'h_m':.45}}
section_path.write_text(json.dumps(variant,indent=2),encoding='utf-8')
q_cfg=dict(base_cfg);q_cfg['q_Q_kN_m2']=4.0
q_path=EVID/'mod_Q_parametros.json';q_path.write_text(json.dumps(q_cfg,indent=2),encoding='utf-8')
report={'baseline_geometry':'Edificio/data/geometry/geometria_manual.json','baseline_parameters':'Edificio/data/parameters/parametros.json','variants':[]}
try:
    run([BUILDER]);run([RUNNER]);baseline=result();initial_hashes=resource_hashes()
    run([RUNNER,'--parametros',q_path]);report['variants'].append({'category':'intensidad de carga','source':'q_Q_kN_m2','base':base_cfg['q_Q_kN_m2'],'variant':4.0,'response_before':baseline,'response_after':result(),'input':str(q_path.relative_to(PROJECT))})
    run([BUILDER,'--geometria',section_path]);run([RUNNER]);after=result()
    model=json.loads((ROOT/'results'/'modelo_3d_manual.json').read_text(encoding='utf-8'))
    beam=next(e for e in model['elements'] if e['id']==207)
    report['variants'].append({'category':'sección de viga','element_id':207,'type':beam['type'],'node_i':beam['i'],'node_j':beam['j'],'source':'geometria_manual.json → element_section_overrides[207]','base_dimensions_m':({'BEAM_SMALL':{'b_m':.30,'h_m':.45},'BEAM_VARIABLE':{'b_m':.60,'h_m':.35},'BEAM_40x60':{'b_m':.40,'h_m':.60}}.get(beam['type'],{'b_m':.80,'h_m':.60})),'variant_dimensions_m':{'b_m':.75,'h_m':.45},'computed_section':beam.get('section_override'),'response_before':baseline,'response_after':after,'input':str(section_path.relative_to(PROJECT))})
finally:
    # Restore builder source and regenerate base model, analyses and Unity resources even after failure.
    run([BUILDER]);run([RUNNER])
report['restored_base_response']=result();report['resources_sha256_after_restore']=resource_hashes();report['restored_exactly']=report['resources_sha256_after_restore']==initial_hashes
if not report['restored_exactly'] or report['restored_base_response']!=baseline: raise AssertionError('La restauración no coincide con la base inicial.')
(EVID/'modificaciones_ejecucion.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
