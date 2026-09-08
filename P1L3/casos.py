"""Casos elasticos independientes. Unidades: kN, m, s, radianes."""
from pathlib import Path
import sys
import json
import csv
from collections import defaultdict
import numpy as np
import openseespy.opensees as ops

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT.parent / 'P1L2' / 'scripts'))
import modelo_opensees_3d as base
import verificar_modelo as verification


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
            live = q * row['tributary_area_m2']
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
    floors = []
    assigned = set()
    for d in data['diaphragms']:
        ids = d['node_ids']
        weights = np.array([-loads['G'][n][2] - cfg['fraccion_Q_masa'] * loads['Q'][n][2] for n in ids])
        W = float(weights.sum())
        if W <= 0:
            raise ValueError('Piso sin peso sismico positivo')
        xy = np.array([ops.nodeCoord(n)[:2] for n in ids])
        cm = weights @ xy / W
        master = d['master_node']
        xm, ym, _ = ops.nodeCoord(master)
        F = cfg['aceleracion_fraccion_g'] * W
        loads['EX'][master] += [F, 0, 0, 0, 0, -F * (cm[1] - ym)]
        loads['EY'][master] += [0, F, 0, 0, 0, F * (cm[0] - xm)]
        floors.append(dict(bloque=d['subbuilding'], z_m=d['z_m'], master=master,
                           G_kN=float(sum(-loads['G'][n][2] for n in ids)),
                           Q_kN=float(sum(-loads['Q'][n][2] for n in ids)),
                           peso_sismico_kN=W, masa_t=W/cfg['g_m_s2'],
                           CM_x_m=float(cm[0]), CM_y_m=float(cm[1]), F_kN=F,
                           Mz_EX_master_kNm=-F*(cm[1]-ym),
                           Mz_EY_master_kNm=F*(cm[0]-xm)))
        assigned.update(ids)
    # Masas consistentes (1 t = 1 kN s2/m); reemplaza las masas heredadas.
    for n in ops.getNodeTags():
        m = (-loads['G'][n][2] - cfg['fraccion_Q_masa'] * loads['Q'][n][2]) / cfg['g_m_s2']
        ops.mass(n, m, m, m, 0, 0, 0)
    base_weight = sum(-loads['G'][n][2] - cfg['fraccion_Q_masa']*loads['Q'][n][2]
                      for n in ops.getNodeTags() if n not in assigned)
    return loads, floors, transfers, base_weight


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
    support_sum = support_r[:, :3].sum(axis=0)
    applied_sum = sum((v[:3] for v in total.values()), np.zeros(3))
    floor_response = []
    compatibility = 0.0
    for d, floor in zip(data['diaphragms'], floors):
        master = d['master_node']
        um = np.array(ops.nodeDisp(master))
        xm, ym, _ = ops.nodeCoord(master)
        ux = um[0] - um[5]*(floor['CM_y_m']-ym)
        uy = um[1] + um[5]*(floor['CM_x_m']-xm)
        floor_response.append(dict(bloque=floor['bloque'], z_m=floor['z_m'],
                                   ux_CM_m=ux, uy_CM_m=uy, giro_z_rad=um[5]))
        for n in d['node_ids']:
            x,y,_ = ops.nodeCoord(n)
            err = max(abs(ops.nodeDisp(n,1)-(um[0]-um[5]*(y-ym))),
                      abs(ops.nodeDisp(n,2)-(um[1]+um[5]*(x-xm))))
            compatibility = max(compatibility, err)
    # Reacciones de todos los nodos no son equivalentes a reacciones de apoyo:
    # los nodos MPC contienen fuerzas internas de restriccion.
    return dict(node_tags=tags, element_tags=element_tags, u=u, r=r, forces=forces,
                support_sum=support_sum, applied_sum=applied_sum,
                floor_response=floor_response, compatibility=compatibility,
                floors=floors, transfers=transfers, base_weight=base_weight,
                support_r=support_r, support_tags=sorted(supports),
                support_coordinates=[ops.nodeCoord(n) for n in sorted(supports)],
                support_heights=sorted({ops.nodeCoord(n,3) for n in supports}))


def run(cfg, out):
    if cfg['patron_sismico'] != 'uniforme_aceleracion':
        raise ValueError('Patron no implementado: use uniforme_aceleracion')
    if cfg['q_Q_kN_m2'] is not None and cfg['q_Q_kN_m2'] < 0:
        raise ValueError('q_Q debe ser no negativa')
    results = {}
    checks = []
    def check(name, error, tol):
        checks.append(dict(control=name, error=float(error), tolerancia=float(tol),
                           estado='OK' if np.isfinite(error) and error <= tol else 'REVISAR'))
    for case in ('G', 'Q', 'EX', 'EY', 'R'):
        print(f'Analizando {case}...', flush=True)
        result = solve(cfg['combinacion'] if case == 'R' else {case: 1}, cfg)
        results[case] = result
        np.savez_compressed(out/f'{case}.npz', node_tags=result['node_tags'],
                            u=result['u'], nodal_residual=result['r'],
                            support_tags=result['support_tags'], reaction=result['support_r'])
        (out/f'{case}_fuerzas.json').write_text(json.dumps(result['forces']), encoding='utf-8')
        dump_csv(out/f'{case}_pisos.csv', result['floor_response'])
        scale = max(1, np.linalg.norm(result['applied_sum']))
        check(f'{case}: equilibrio apoyos / carga', np.linalg.norm(result['support_sum']+result['applied_sum'])/scale, 1e-4)
        check(f'{case}: compatibilidad diafragmas [m]', result['compatibility'], 1e-5)
        if case in ('EX', 'EY'):
            direction = 0 if case == 'EX' else 1
            force = sum(f['F_kN'] for f in result['floors'])
            check(f'{case}: carga lateral total [kN]', abs(result['applied_sum'][direction]-force), 1e-7)
            check(f'{case}: corte basal relativo', abs(-result['support_sum'][direction]-force)/force, 1e-4)
            component = 'ux_CM_m' if case == 'EX' else 'uy_CM_m'
            check(f'{case}: pisos con desplazamiento contrario', sum(f[component] <= 0 for f in result['floor_response']), 0)
            # No hay excentricidad accidental: fuerza + par en master debe
            # ser estaticamente equivalente a fuerza aplicada en el CM.
            torsion_error=0.0
            for f in result['floors']:
                xm,ym,_=ops.nodeCoord(f['master'])
                arm=(f['CM_y_m']-ym) if case=='EX' else -(f['CM_x_m']-xm)
                torsion_error=max(torsion_error,abs(f[f'Mz_{case}_master_kNm']+f['F_kN']*arm))
            check(f'{case}: momento aplicado respecto al CM [kNm]',torsion_error,1e-7)
    ref = results['G']
    dump_csv(out/'apoyos_heredados.csv',[
        dict(nodo=n,x_m=xyz[0],y_m=xyz[1],z_m=xyz[2])
        for n,xyz in zip(ref['support_tags'],ref['support_coordinates'])])
    dump_csv(out/'masas_y_sismo.csv', ref['floors'])
    dump_csv(out/'transferencia_Q.csv', ref['transfers'])
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
