"""Independent OpenSees checks for linear superposition (kN, m, rad)."""
import json, sys
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'analysis'/'load_cases'))
import casos
CFG=json.loads((ROOT/'data/parameters/parametros.json').read_text(encoding='utf-8'))
STATES={'S1':(1.,1.,0.,0.),'S2':(1.2,1.4,.8,-.3),'S3':(1.,.5,-1.,.7)}
COMPONENTS={'u_m':('ux','uy','uz'),'rotation_rad':('rx','ry','rz'),'local_force_kN':('N','Vy','Vz'),'local_moment_kNm':('T','My','Mz')}
def compare(label,cfg):
    names=('G','Q','EX','EY'); bases={n:casos.solve({n:1.0},cfg) for n in names}
    errors=[]
    for state,coeff in STATES.items():
        explicit=casos.solve(dict(zip(names,coeff)),cfg)
        for field,units in COMPONENTS.items():
            if field in ('u_m','rotation_rad'):
                offset=0 if field=='u_m' else 3
                rows=[(str(n),np.asarray(explicit['u'][i][offset:offset+3]),sum(coeff[k]*bases[names[k]]['u'][i][offset:offset+3] for k in range(4))) for i,n in enumerate(explicit['node_tags'])]
            elif field.startswith('local_'):
                rows=[]
                for eid,values in explicit['local_forces'].items():
                    summed=sum(coeff[k]*np.asarray(bases[names[k]]['local_forces'][eid]) for k in range(4))
                    rows.append((eid,np.asarray(values),summed))
            for component in range(len(units)):
                delta=max((abs(float(a[component]-b[component])) for _,a,b in rows),default=0.)
                scale=max((abs(float(a[component])) for _,a,_ in rows),default=0.)
                rel=delta/max(scale,1e-12)
                errors.append({'mass_case':label,'state':state,'quantity':field,'component':units[component],'unit':'m' if field=='u_m' else ('rad' if field=='rotation_rad' else ('kN' if units[component] in ('N','Vy','Vz') else 'kN·m')),'max_abs_error':delta,'relative_error':rel})
                if rel>1e-5: raise AssertionError(errors[-1])
        # Station diagrams are checked per component and physical unit.
        diagram_units={'n':'kN','vy':'kN','vz':'kN','t':'kN·m','my':'kN·m','mz':'kN·m'}
        for field,unit in diagram_units.items():
            delta=0.;scale=0.
            for eid,curves in explicit['bar_diagrams'].items():
                value=sum(coeff[k]*np.asarray(bases[names[k]]['bar_diagrams'][eid][field]) for k in range(4))
                actual=np.asarray(curves[field]);delta=max(delta,float(np.max(np.abs(actual-value))));scale=max(scale,float(np.max(np.abs(actual))))
            rel=delta/max(scale,1e-12);errors.append({'mass_case':label,'state':state,'quantity':'bar_diagram','component':field,'unit':unit,'max_abs_error':delta,'relative_error':rel})
            if rel>1e-5: raise AssertionError(errors[-1])
        # Wall demands are independently solved by cases.solve; retain max P and M error separately.
        wall_fields=(('P_compresion_kN','kN'),('M_principal_kNm','kN·m'))
        explicit_walls={r['panel_id']:r for r in explicit['wall_demands']}
        base_walls=[{r['panel_id']:r for r in bases[n]['wall_demands']} for n in names]
        for field,unit in wall_fields:
            delta=0.;scale=0.
            for panel,row in explicit_walls.items():
                value=sum(coeff[k]*base_walls[k][panel][field] for k in range(4))
                delta=max(delta,abs(float(row[field]-value)));scale=max(scale,abs(float(row[field])))
            rel=delta/max(scale,1e-12);errors.append({'mass_case':label,'state':state,'quantity':'wall_P' if unit=='kN' else 'wall_M','component':'combined wall demand','unit':unit,'max_abs_error':delta,'relative_error':rel})
            if rel>1e-5: raise AssertionError(errors[-1])
    return errors
if __name__=='__main__':
    all_errors=compare('alphaG=1,alphaQ=.5',CFG)
    changed=dict(CFG);changed['ponderador_G_masa']=.8;changed['fraccion_Q_masa']=.25
    all_errors += compare('alphaG=.8,alphaQ=.25',changed)
    out=ROOT/'documentation'/'semana05_evidencias';out.mkdir(parents=True,exist_ok=True)
    (out/'superposicion_verificacion.json').write_text(json.dumps(all_errors,indent=2),encoding='utf-8')
    print(f'{len(all_errors)} errores por estado/componente dentro de tolerancia 1e-5')
