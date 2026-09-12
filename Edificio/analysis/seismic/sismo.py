"""Sismo pseudoestático del enunciado: masa de piso, CM y F=m*a.

Unidades: G/Q/F en kN, masa en t=kN*s²/m, a en m/s², coordenadas en m.
La aceleración es una entrada; no se obtiene de la altura ni de la deformada.
"""
from math import isfinite


def calcular_pisos(diaphragms, coordinates, gravity_loads, live_loads, cfg):
    g = float(cfg['g_m_s2'])
    fraction = float(cfg['fraccion_Q_masa'])
    factor_g = float(cfg.get('ponderador_G_masa', 1.0))
    default = float(cfg['aceleracion_fraccion_g'])
    if not all(isfinite(v) for v in (g, factor_g, fraction, default)) or g <= 0 or min(factor_g,fraction)<0:
        raise ValueError('g debe ser positiva y los ponderadores de masa deben ser finitos y no negativos')
    overrides = cfg.get('aceleracion_por_piso_g', {})
    levels = sorted({d['z_m'] for d in diaphragms})
    valid_keys = {str(i+1) for i in range(len(levels))}
    if set(overrides)-valid_keys:
        raise ValueError('aceleracion_por_piso_g contiene pisos inexistentes')
    assigned = set()
    result, audit = [], []
    for d in diaphragms:
        ids = d['node_ids']
        if len(ids) != len(set(ids)) or assigned.intersection(ids):
            raise ValueError('Masa duplicada entre diafragmas')
        assigned.update(ids)
        floor = levels.index(d['z_m'])+1
        alpha = float(overrides.get(str(floor), default))
        if not isfinite(alpha):
            raise ValueError('Aceleración no finita')
        G = sum(gravity_loads.get(n, 0.) for n in ids)
        Q = sum(live_loads.get(n, 0.) for n in ids)
        weights = {n: factor_g*gravity_loads.get(n, 0.)+fraction*live_loads.get(n, 0.) for n in ids}
        if any(w < -1e-9 or not isfinite(w) for w in weights.values()):
            raise ValueError('Peso sísmico nodal negativo o no finito')
        W = factor_g*G+fraction*Q #CALCULO MASA SISMICA 
        if W <= 0 and not cfg.get('_permitir_masa_nula_base',False):
            raise ValueError('Piso sin masa positiva')
        mass = W/g
        master = d['master_node']
        x = sum(weights[n]*coordinates[n][0] for n in ids)/W if W>0 else coordinates[master][0]
        y = sum(weights[n]*coordinates[n][1] for n in ids)/W if W>0 else coordinates[master][1]
        acceleration = alpha*g #CALCULO ACELERACION 
        force = mass*acceleration 
        master = d['master_node']
        xm, ym, _ = coordinates[master]
        result.append(dict(bloque=d['subbuilding'],z_m=d['z_m'],piso=floor,master=master,
            G_kN=G,Q_kN=Q,peso_sismico_kN=W,masa_t=mass,CM_x_m=x,CM_y_m=y,
            Gx_kNm=sum(gravity_loads.get(n,0.)*coordinates[n][0] for n in ids),
            Gy_kNm=sum(gravity_loads.get(n,0.)*coordinates[n][1] for n in ids),
            Qx_kNm=sum(live_loads.get(n,0.)*coordinates[n][0] for n in ids),
            Qy_kNm=sum(live_loads.get(n,0.)*coordinates[n][1] for n in ids),
            a_g=alpha,a_m_s2=acceleration,F_kN=force,
            Mz_EX_master_kNm=-force*(y-ym),Mz_EY_master_kNm=force*(x-xm)))
        for n in ids:
            audit.append(dict(piso=floor,bloque=d['subbuilding'],z_m=d['z_m'],nodo=n,
                x_m=coordinates[n][0],y_m=coordinates[n][1],G_kN=gravity_loads.get(n,0.),
                Q_kN=live_loads.get(n,0.),peso_sismico_kN=weights[n],masa_t=weights[n]/g))
    base = 0.
    for n, xyz in coordinates.items():
        if n in assigned: continue
        weight = factor_g*gravity_loads.get(n,0.)+fraction*live_loads.get(n,0.)
        if abs(weight)>1e-9 and abs(xyz[2])>1e-6:
            raise ValueError(f'Peso elevado sin piso asignado: nodo {n}, Z={xyz[2]}')
        base += weight
    expected = factor_g*sum(gravity_loads.values())+fraction*sum(live_loads.values())
    if abs(sum(f['peso_sismico_kN'] for f in result)+base-expected)>1e-7*max(1,abs(expected)):
        raise ValueError('No se conserva el peso sísmico entre pisos y base')
    return result, audit, base
