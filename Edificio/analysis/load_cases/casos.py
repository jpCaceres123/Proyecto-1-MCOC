"""Casos elasticos independientes. Unidades: kN, m, s, radianes."""
from pathlib import Path
import sys
import json
import csv
from collections import defaultdict
import numpy as np
import openseespy.opensees as ops

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'model' / 'opensees'))
sys.path.insert(0, str(ROOT / 'verification' / 'load_transfer'))
sys.path.insert(0, str(ROOT / 'analysis' / 'seismic'))
import modelo_opensees_3d as base
import verificar_modelo as verification
from sismo import calcular_pisos


def dump_csv(path, rows):
    if not rows:
        return
    with path.open('w', newline='', encoding='utf-8-sig') as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def vectors(data, cfg):
    loads = {key: defaultdict(lambda: np.zeros(6)) for key in ('G', 'Q', 'EX', 'EY')}
    elements = {e['id']: e for e in data['elements']}
    walls = {w['id']: w for w in data['wall_mesh']['walls']}
    transfers = []
    for kind in ('beam', 'wall'):
        for row in data[f'{kind}_load_cases']:
            if kind == 'beam':
                e = elements[row['beam_id']]
                ends = [e['i'], e['j']]
            else:
                ends = walls[row['wall_id']]['edge_node_ids_by_level'][str(round(row['level_z_m'], 6))]
            q = cfg['q_Q_kN_m2'] 
            q = row['q_SC_kN_m2'] if q is None else q
            live = q * row['tributary_area_m2'] #CALCULO CARGA VIVA
            for n in ends:
                loads['G'][n][2] -= row['dead_load_kN'] / len(ends)
                loads['Q'][n][2] -= live / len(ends)
            transfers.append(dict(z_m=row['level_z_m'], slab_id=row['slab_id'],
                                  receptor=kind, tag=row[f'{kind}_id'],
                                  area_m2=row['tributary_area_m2'], q_Q_kN_m2=q, Q_kN=live))
    # Peso propio de muros y barras; G de losas ya contiene terminaciones.
    for n, weight in data['wall_mesh']['node_self_weight_kN'].items():
        loads['G'][n][2] -= weight
    sections = {'COLUMN': 'section_columns', 'STEEL_COLUMN_SHS300x20': 'section_steel_columns', 'BEAM_SMALL': 'section_small_beams',
                'BEAM_VARIABLE': 'section_variable_beams', 'BEAM_40x60': 'section_40x60_beams'}
    for e in data['elements']:
        if e['type'] == 'WALL':
            continue
        area = data[sections.get(e['type'], 'section_beams')]['A_m2']
        length = np.linalg.norm(np.array(ops.nodeCoord(e['j'])) - ops.nodeCoord(e['i']))
        unit_weight = (data['material_steel']['density_kg_m3'] * 9.80665 / 1000.0
                       if e['type'] == 'STEEL_COLUMN_SHS300x20'
                       else cfg['peso_especifico_HA_kN_m3'])
        weight = area * length * unit_weight
        for n in (e['i'], e['j']):
            loads['G'][n][2] -= weight / 2
    coordinates={n:ops.nodeCoord(n) for n in ops.getNodeTags()}
    gravity={n:float(-loads['G'][n][2]) for n in coordinates}
    live={n:float(-loads['Q'][n][2]) for n in coordinates}
    floors, mass_audit, base_weight=calcular_pisos(data['diaphragms'],coordinates,gravity,live,cfg)
    for floor in floors:
        master= floor['master']; F=floor['F_kN']
        loads['EX'][master] += [F,0,0,0,0,floor['Mz_EX_master_kNm']]
        loads['EY'][master] += [0,F,0,0,0,floor['Mz_EY_master_kNm']]
    data['mass_audit']=mass_audit
    # Masas consistentes (1 t = 1 kN s2/m); reemplaza las masas heredadas.
    for n in ops.getNodeTags():
        m = (-cfg.get('ponderador_G_masa',1.)*loads['G'][n][2] - cfg['fraccion_Q_masa'] * loads['Q'][n][2]) / cfg['g_m_s2']
        ops.mass(n, m, m, m, 0, 0, 0)
    return loads, floors, transfers, base_weight


def wall_section_demands(data):
    """Resultantes en la hilera inferior de cada paño ShellMITC4."""
    geometry = {int(w['id']): w for w in data.get('walls', [])}
    rows = []
    for mesh in data.get('wall_mesh', {}).get('walls', []):
        wall = geometry[int(mesh['id'])]
        z_cut = float(wall['z_i_m'])
        dx = float(wall['x_j_m']) - float(wall['x_i_m'])
        dy = float(wall['y_j_m']) - float(wall['y_i_m'])
        length = float(np.hypot(dx, dy))
        tangent = np.array([dx / length, dy / length, 0.0])
        normal = np.array([-dy / length, dx / length, 0.0])
        center = np.array([(float(wall['x_i_m']) + float(wall['x_j_m'])) / 2,
                           (float(wall['y_i_m']) + float(wall['y_j_m'])) / 2, z_cut])
        force_sum = np.zeros(3)
        moment_sum = np.zeros(3)
        used_shells = 0
        used_terms = 0
        shell_ids = mesh.get('shell_element_ids', [])
        if not shell_ids:
            shell_ids = [shell_id for segment in mesh.get('segments', [])
                         for shell_id in segment.get('shell_ids', [])]
        for shell_id in shell_ids:
            nodes = list(ops.eleNodes(shell_id))
            values = np.asarray(ops.eleResponse(shell_id, 'forces'), dtype=float)
            if values.size != 6 * len(nodes):
                raise ValueError(f'Shell {shell_id}: respuesta forces incompleta')
            if not any(abs(ops.nodeCoord(n, 3) - z_cut) <= 1e-6 for n in nodes):
                continue
            used_shells += 1
            for index, node in enumerate(nodes):
                if abs(ops.nodeCoord(node, 3) - z_cut) > 1e-6:
                    continue
                action = values[6*index:6*index+6]
                force = action[:3]
                arm = np.asarray(ops.nodeCoord(node), dtype=float) - center
                force_sum += force
                moment_sum += action[3:] + np.cross(arm, force)
                used_terms += 1
        if not used_shells or not used_terms:
            raise ValueError(f'Muro {wall["id"]}: no se encontro la hilera Shell inferior')
        rows.append(dict(
            panel_id=int(wall['id']),
            source_wall_id=int(wall.get('source_wall_id', wall['id'])),
            floor=int(wall.get('floor', 0)), z_cut_m=z_cut,
            P_compresion_kN=float(force_sum[2]),
            M_principal_kNm=float(np.dot(moment_sum, normal)),
            V_en_plano_kN=float(np.dot(force_sum, tangent)),
            V_fuera_plano_kN=float(np.dot(force_sum, normal)),
            M_vertical_kNm=float(moment_sum[2]),
            shells_corte=used_shells, terminos_nodales=used_terms))
    return rows


def solve(coeff, cfg):
    data = base.build_model()
    # El constructor heredado aplica G+Q. Eliminarlos antes de definir el caso.
    for tag in (1, 2, 3, 6, 7):
        ops.remove('loadPattern', tag)
    loads, floors, transfers, base_weight = vectors(data, cfg)
    total = defaultdict(lambda: np.zeros(6))
    for case, scale in coeff.items():
        for n, force in loads[case].items():
            total[n] += scale * force
    ops.timeSeries('Linear', 100)
    ops.pattern('Plain', 100, 100)
    for n, force in total.items():
        if np.any(force):
            ops.load(n, *force.tolist())
    ops.constraints('Penalty', cfg['penalty'], cfg['penalty'])
    ops.numberer('RCM')
    ops.system('UmfPack')
    ops.algorithm('Linear')
    ops.integrator('LoadControl', 1.0)
    ops.analysis('Static')
    if ops.analyze(1):
        raise RuntimeError(f'OpenSees no convergio: {coeff}')
    ops.reactions()
    tags = sorted(ops.getNodeTags())
    element_tags = sorted(ops.getEleTags())
    u = np.array([ops.nodeDisp(n) for n in tags])
    r = np.array([ops.nodeReaction(n) for n in tags])
    supports = set(ops.getFixedNodes())
    # nodeReaction devuelve Ku-P sin incorporar fuerzas MPC. En nodos que
    # participan tambien en MPC no es la reaccion externa del apoyo.
    # Con Penalty y SP homogeneas: R_apoyo = -alpha_SP * u_restringido.
    support_r = np.zeros((len(supports), 6))
    for i,n in enumerate(sorted(supports)):
        for dof in ops.getFixedDOFs(n):
            support_r[i,dof-1] = -cfg['penalty']*ops.nodeDisp(n,dof)
    forces = {str(e): ops.eleForce(e) for e in element_tags}
    local_forces = {str(e['id']): ops.eleResponse(e['id'], 'localForce')
                    for e in data['elements'] if e['type'] != 'WALL'}
    wall_demands = wall_section_demands(data)
    support_sum = support_r[:, :3].sum(axis=0)
    applied_sum = sum((v[:3] for v in total.values()), np.zeros(3))
    applied_moment = sum((v[3:]+np.cross(ops.nodeCoord(n),v[:3]) for n,v in total.items()),np.zeros(3))
    floor_response = []
    compatibility = 0.0
    for d, floor in zip(data['diaphragms'], floors):
        master = d['master_node']
        um = np.array(ops.nodeDisp(master))
        xm, ym, _ = ops.nodeCoord(master)
        ux = um[0] - um[5]*(floor['CM_y_m']-ym)
        uy = um[1] + um[5]*(floor['CM_x_m']-xm)
        floor_response.append(dict(bloque=floor['bloque'], z_m=floor['z_m'],
                                   ux_master_m=um[0],uy_master_m=um[1],master_x_m=xm,master_y_m=ym,
                                   ux_CM_m=ux, uy_CM_m=uy, giro_z_rad=um[5]))
        for n in d['node_ids']:
            x,y,_ = ops.nodeCoord(n)
            err = max(abs(ops.nodeDisp(n,1)-(um[0]-um[5]*(y-ym))),
                      abs(ops.nodeDisp(n,2)-(um[1]+um[5]*(x-xm))))
            compatibility = max(compatibility, err)
    # Reacciones de todos los nodos no son equivalentes a reacciones de apoyo:
    # los nodos MPC contienen fuerzas internas de restriccion.
    return dict(node_tags=tags, element_tags=element_tags, u=u, r=r, forces=forces, local_forces=local_forces,
                wall_demands=wall_demands,
                support_sum=support_sum, applied_sum=applied_sum,
                applied_moment=applied_moment,
                floor_response=floor_response, compatibility=compatibility,
                floors=floors, transfers=transfers, base_weight=base_weight,
                mass_audit=data['mass_audit'],
                support_r=support_r, support_tags=sorted(supports),
                support_coordinates=[ops.nodeCoord(n) for n in sorted(supports)],
                support_heights=sorted({ops.nodeCoord(n,3) for n in supports}))


def run(cfg, out):
    if cfg['patron_sismico'] != 'uniforme_aceleracion':
        raise ValueError('Patron no implementado: use uniforme_aceleracion')
    if cfg['q_Q_kN_m2'] is not None and cfg['q_Q_kN_m2'] < 0:
        raise ValueError('q_Q debe ser no negativa')
    results = {}
    wall_demand_rows = []
    checks = []
    def check(name, error, tol):
        checks.append(dict(control=name, error=float(error), tolerancia=float(tol),
                           estado='OK' if np.isfinite(error) and error <= tol else 'REVISAR'))
    for case in ('G', 'Q', 'EX', 'EY', 'R'):
        print(f'Analizando {case}...', flush=True)
        result = solve(cfg['combinacion'] if case == 'R' else {case: 1}, cfg)
        results[case] = result
        wall_demand_rows.extend(dict(caso=case, **row) for row in result['wall_demands'])
        np.savez_compressed(out/f'{case}.npz', node_tags=result['node_tags'],
                            u=result['u'], nodal_residual=result['r'],
                            support_tags=result['support_tags'], reaction=result['support_r'])
        (out/f'{case}_fuerzas.json').write_text(json.dumps(result['forces']), encoding='utf-8')
        (out/f'{case}_fuerzas_locales.json').write_text(json.dumps(result['local_forces']), encoding='utf-8')
        dump_csv(out/f'{case}_pisos.csv', result['floor_response'])
        scale = max(1, np.linalg.norm(result['applied_sum']))
        check(f'{case}: equilibrio apoyos / carga', np.linalg.norm(result['support_sum']+result['applied_sum'])/scale, 1e-4)
        check(f'{case}: compatibilidad diafragmas [m]', result['compatibility'], 1e-5)
        if case in ('EX', 'EY'):
            direction = 0 if case == 'EX' else 1
            force = sum(f['F_kN'] for f in result['floors'])
            check(f'{case}: carga lateral total [kN]', abs(result['applied_sum'][direction]-force), 1e-7)
            target_moment=sum((np.cross([f['CM_x_m'],f['CM_y_m'],f['z_m']],
                [f['F_kN'],0,0] if case=='EX' else [0,f['F_kN'],0])
                for f in result['floors']),np.zeros(3))
            check(f'{case}: resultante aplicada equivalente a fuerzas en CM [kNm]',
                  np.linalg.norm(result['applied_moment']-target_moment),1e-6)
            check(f'{case}: corte basal relativo', abs(-result['support_sum'][direction]-force)/max(1,abs(force)), 1e-4)
            component = 'ux_CM_m' if case == 'EX' else 'uy_CM_m'
            # En un patrón uniforme de signo único se verifica el sentido global.
            # Con cargas de signos mixtos un desplazamiento local opuesto no es un error.
            accelerations=[f['a_g'] for f in result['floors']]
            if all(a>0 for a in accelerations) or all(a<0 for a in accelerations):
                sign=1 if accelerations[0]>0 else -1
                check(f'{case}: pisos con desplazamiento contrario', sum(sign*f[component] < -1e-10 for f in result['floor_response']), 0)
            for floor in result['floors']:
                check(f"{case}: F=m*a piso {floor['piso']} {floor['bloque']}",
                      abs(floor['F_kN']-floor['masa_t']*floor['a_m_s2']),1e-7)
            # No hay excentricidad accidental: fuerza + par en master debe
            # ser estaticamente equivalente a fuerza aplicada en el CM.
            torsion_error=0.0
            for f in result['floors']:
                xm,ym,_=ops.nodeCoord(f['master'])
                arm=(f['CM_y_m']-ym) if case=='EX' else -(f['CM_x_m']-xm)
                torsion_error=max(torsion_error,abs(f[f'Mz_{case}_master_kNm']+f['F_kN']*arm))
            check(f'{case}: momento aplicado respecto al CM [kNm]',torsion_error,1e-7)
    ref = results['G']
    # Bases de respuesta para cambiar los ponderadores de masa en Unity.
    # La rigidez no depende de la masa en este análisis estático lineal.
    seismic_bases={}
    for direction in ('EX','EY'):
        for suffix,ag,aq in (('G',1.,0.),('Q',0.,1.)):
            name=direction+suffix
            print(f'Analizando base de masa {name}...',flush=True)
            result=solve({direction:1.},{**cfg,'ponderador_G_masa':ag,'fraccion_Q_masa':aq,'_permitir_masa_nula_base':True})
            seismic_bases[name]=result
            wall_demand_rows.extend(dict(caso=name, **row) for row in result['wall_demands'])
            np.savez_compressed(out/f'{name}.npz',node_tags=result['node_tags'],u=result['u'])
            (out/f'{name}_fuerzas_locales.json').write_text(json.dumps(result['local_forces']),encoding='utf-8')
            dump_csv(out/f'{name}_pisos.csv',result['floor_response'])
            check(f'{name}: equilibrio apoyos / carga',np.linalg.norm(result['support_sum']+result['applied_sum'])/max(1,np.linalg.norm(result['applied_sum'])),1e-4)
        for field in ('u','support_r'):
            combined=cfg.get('ponderador_G_masa',1.)*seismic_bases[direction+'G'][field]+cfg['fraccion_Q_masa']*seismic_bases[direction+'Q'][field]
            check(f'{direction}: bases de masa reproducen {field}',np.max(np.abs(combined-results[direction][field]))/max(1e-12,np.max(np.abs(results[direction][field]))),1e-5)
        for component in ('P_compresion_kN', 'M_principal_kNm'):
            expected=np.array([row[component] for row in results[direction]['wall_demands']])
            combined=(cfg.get('ponderador_G_masa',1.)*np.array([row[component] for row in seismic_bases[direction+'G']['wall_demands']])
                      +cfg['fraccion_Q_masa']*np.array([row[component] for row in seismic_bases[direction+'Q']['wall_demands']]))
            check(f'{direction}: bases de masa reproducen demanda de muro {component}',
                  np.max(np.abs(combined-expected))/max(1e-12,np.max(np.abs(expected))),1e-5)
    # Contrastar ponderadores distintos con una nueva corrida explícita, también esfuerzos locales.
    for direction in ('EX','EY'):
        explicit=solve({direction:1.},{**cfg,'ponderador_G_masa':.8,'fraccion_Q_masa':.3})
        for field in ('u','support_r','local_forces'):
            def flatten(r):
                return np.array([v for key in sorted(r[field],key=int) for v in r[field][key]]) if field=='local_forces' else r[field]
            expected=flatten(explicit)
            combined=.8*flatten(seismic_bases[direction+'G'])+.3*flatten(seismic_bases[direction+'Q'])
            check(f'{direction}: masa 0.8G+0.3Q explícita {field}',np.max(np.abs(combined-expected))/max(1e-12,np.max(np.abs(expected))),1e-5)
    dump_csv(out/'apoyos_heredados.csv',[
        dict(nodo=n,x_m=xyz[0],y_m=xyz[1],z_m=xyz[2])
        for n,xyz in zip(ref['support_tags'],ref['support_coordinates'])])
    dump_csv(out/'masas_y_sismo.csv', ref['floors'])
    dump_csv(out/'auditoria_masas_piso.csv', ref['mass_audit'])
    floor_totals=[]
    for number in sorted({f['piso'] for f in ref['floors']}):
        group=[f for f in ref['floors'] if f['piso']==number]
        floor_totals.append(dict(piso=number,z_m=group[0]['z_m'],
            G_kN=sum(f['G_kN'] for f in group),Q_kN=sum(f['Q_kN'] for f in group),
            masa_t=sum(f['masa_t'] for f in group),a_g=group[0]['a_g'],a_m_s2=group[0]['a_m_s2'],
            Fx_EX_kN=sum(f['F_kN'] for f in group),Fy_EY_kN=sum(f['F_kN'] for f in group)))
    dump_csv(out/'sismo_por_piso.csv',floor_totals)
    dump_csv(out/'transferencia_Q.csv', ref['transfers'])
    dump_csv(out/'demanda_muros.csv', wall_demand_rows)
    # Control independiente: areas de zonas originales, no suma de receptores.
    model = json.loads(base.MODEL.read_text(encoding='utf-8'))
    source_loads = json.loads(verification.LOADS.read_text(encoding='utf-8'))
    geometry = json.loads(verification.GEOMETRY.read_text(encoding='utf-8'))
    qrows = []
    for z in sorted({r['z_m'] for r in ref['transfers']}):
        expected = verification.expected_floor_loads(model, source_loads, geometry, z)
        q0 = cfg['q_Q_kN_m2']
        target = expected['live_load_kN'] if q0 is None else q0 * expected['area_m2']
        actual = sum(r['Q_kN'] for r in ref['transfers'] if r['z_m']==z)
        qrows.append(dict(z_m=z, area_origen_m2=expected['area_m2'], Q_origen_kN=target,
                          Q_transferida_kN=actual, diferencia_kN=actual-target))
        check(f'Q: conservacion piso {z} [kN]', abs(actual-target), 0.002)
    dump_csv(out/'conservacion_Q.csv', qrows)
    comparison=[]
    for field, label in (('u','desplazamientos'), ('support_r','reacciones de apoyo'), ('forces','fuerzas internas')):
        def arr(res):
            return np.concatenate([res[field][str(e)] for e in res['element_tags']]) if field=='forces' else res[field].ravel()
        combined = sum((factor*arr(results[key]) for key,factor in cfg['combinacion'].items()), np.zeros_like(arr(ref)))
        explicit = arr(results['R'])
        error = np.max(np.abs(explicit-combined))
        relative = error / max(1e-12, np.max(np.abs(explicit)))
        check(f'Superposicion: {label}, error relativo maximo', relative, 1e-5)
        index=int(np.argmax(np.abs(explicit)))
        if field=='forces':
            offset=0
            for tag in ref['element_tags']:
                size=len(ref[field][str(tag)])
                if index < offset+size:
                    identifier=f'elemento {tag}, componente global {index-offset+1}'
                    break
                offset+=size
        else:
            tags=ref['node_tags'] if field=='u' else ref['support_tags']
            identifier=f'nodo {tags[index//6]}, DOF {index%6+1}'
        comparison.append(dict(respuesta=label,identificador=identifier,
                               superpuesta=combined[index],explicita=explicit[index],
                               error_absoluto_max=error,error_relativo_max=relative))
    for component in ('P_compresion_kN', 'M_principal_kNm', 'V_en_plano_kN', 'V_fuera_plano_kN'):
        explicit=np.array([row[component] for row in results['R']['wall_demands']])
        combined=sum((factor*np.array([row[component] for row in results[case]['wall_demands']])
                      for case,factor in cfg['combinacion'].items()),np.zeros_like(explicit))
        check(f'Superposicion: demanda de muros {component}, error relativo maximo',
              np.max(np.abs(explicit-combined))/max(1e-12,np.max(np.abs(explicit))),1e-5)
    dump_csv(out/'comparacion_superposicion.csv',comparison)
    # Control de sensibilidad del metodo Penalty en ambas direcciones.
    for case in ('EX','EY'):
        refined=solve({case:1},{**cfg,'penalty':cfg['penalty']*10})
        error=np.max(np.abs(refined['u']-results[case]['u']))/max(1e-12,np.max(np.abs(results[case]['u'])))
        check(f'{case}: sensibilidad penalty x10',error,0.01)
    dump_csv(out/'verificaciones_globales.csv', checks)
    summary = dict(checks=checks, base_weight_excluded_kN=ref['base_weight'],
                   support_heights_m=ref['support_heights'],
                   equilibrium={k:dict(applied=v['applied_sum'].tolist(), supports=v['support_sum'].tolist()) for k,v in results.items()},
                   floors=ref['floors'])
    (out/'resumen_global.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    return summary
