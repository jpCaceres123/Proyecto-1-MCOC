"""Envolventes P-M nominales de muros en su dirección principal.

La sección resistente es longitud de muro x espesor. La deformación longitudinal
varía linealmente a lo largo del muro; la malla vertical de ambas caras y los
refuerzos de borde participan en P-M. La malla horizontal se conserva en los
datos, pero corresponde al diseño de corte y no se suma como acero longitudinal.

Unidades: geometría en m, materiales en MPa, fuerzas en kN y momentos en kN m.
Compresión P positiva.
"""
from pathlib import Path
import csv
import json
import math
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt


ROOT = Path(__file__).resolve().parents[2]
MODEL = ROOT / 'results' / 'modelo_3d_manual.json'
ASSIGNMENTS = ROOT / 'data' / 'reinforcement' / 'asignacion_armadura_muros.json'


def dump_csv(path, rows):
    if not rows:
        return
    with path.open('w', newline='', encoding='utf-8-sig') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def wall_length(wall):
    return math.hypot(wall['x_j_m']-wall['x_i_m'], wall['y_j_m']-wall['y_i_m'])


def vertical_steel_layers(length, profile, cover):
    """Capas de acero a lo largo del muro: (profundidad d, área m²).

    Cada posición de la malla contiene una barra en cada cara del muro. Las
    posiciones se reajustan uniformemente para que la separación real nunca
    exceda la separación especificada.
    """
    if length <= 2*cover:
        raise ValueError('Longitud de muro incompatible con recubrimiento')
    spacing = profile['vertical_spacing_mm']/1000.0
    diameter = profile['vertical_diameter_mm']/1000.0
    clear = length-2*cover
    intervals = max(1, math.ceil(clear/spacing))
    positions = np.linspace(cover, length-cover, intervals+1)
    area_pair = 2.0*math.pi*diameter**2/4.0  # doble malla
    layers = [[float(d), area_pair] for d in positions]
    n_boundary = int(profile.get('boundary_bars_each_end', 0))
    if n_boundary:
        db = profile['boundary_diameter_mm']/1000.0
        boundary_area = n_boundary*math.pi*db**2/4.0
        layers[0][1] += boundary_area
        layers[-1][1] += boundary_area
    return np.asarray(layers, dtype=float), float(clear/intervals)


def response_at_c(length, thickness, layers, neutral_axis, material):
    """Compatibilidad de deformaciones y bloque rectangular de Whitney."""
    fc, fy, Es = material['fc_MPa'], material['fy_MPa'], material['Es_MPa']
    eps_cu, beta1 = material['eps_cu'], material['beta1']
    c = float(neutral_axis)
    a = min(beta1*c, length)
    concrete_force = 0.85*fc*thickness*a*1000.0
    concrete_moment = concrete_force*(length/2.0-a/2.0)
    strains = eps_cu*(c-layers[:, 0])/c
    stresses = np.clip(Es*strains, -fy, fy)
    steel_forces = stresses*layers[:, 1]*1000.0
    axial = concrete_force+steel_forces.sum()
    moment = concrete_moment+np.sum(steel_forces*(length/2.0-layers[:, 0]))
    return float(axial), float(moment)


def root_for_axial(length, thickness, layers, target, material):
    grid = np.geomspace(max(1e-7, length*1e-6), length*20.0, 1200)
    values = [response_at_c(length, thickness, layers, c, material)[0]-target for c in grid]
    for lo, hi, flo, fhi in zip(grid[:-1], grid[1:], values[:-1], values[1:]):
        if flo == 0 or flo*fhi <= 0:
            for _ in range(80):
                mid = (lo+hi)/2.0
                fm = response_at_c(length, thickness, layers, mid, material)[0]-target
                if flo*fm <= 0:
                    hi, fhi = mid, fm
                else:
                    lo, flo = mid, fm
            return (lo+hi)/2.0
    raise RuntimeError(f'No se encontró P={target:.6g} kN en la sección de muro')


def key_points(length, thickness, layers, material):
    """Puntos A-G compatibles con la convención usada para las columnas."""
    fc, fy, Es = material['fc_MPa'], material['fy_MPa'], material['Es_MPa']
    eps_cu = material['eps_cu']
    As = float(layers[:, 1].sum())
    Ag = length*thickness
    pmax = material['factor_compresion_max']*(0.85*fc*(Ag-As)+fy*As)*1000.0
    points = [dict(punto='A', P_kN=pmax, M_kNm=0.0, c_m=None)]
    states = [('B', 0.0), ('C', fy/Es), ('D', 0.003), ('E', 0.005)]
    far_steel = float(layers[:, 0].max())
    for name, tension_strain in states:
        c = length if name == 'B' else far_steel*eps_cu/(eps_cu+tension_strain)
        p_value, moment = response_at_c(length, thickness, layers, c, material)
        points.append(dict(punto=name, P_kN=p_value, M_kNm=abs(moment), c_m=c))
    c_zero = root_for_axial(length, thickness, layers, 0.0, material)
    p_zero, m_zero = response_at_c(length, thickness, layers, c_zero, material)
    points.append(dict(punto='F', P_kN=0.0 if abs(p_zero)<1e-6 else p_zero,
                       M_kNm=abs(m_zero), c_m=c_zero))
    points.append(dict(punto='G', P_kN=-fy*As*1000.0, M_kNm=0.0, c_m=0.0))
    return points


def dense_envelope(length, thickness, layers, material, count=260):
    points = key_points(length, thickness, layers, material)
    pmax = points[0]['P_kN']
    candidates = [dict(P_kN=points[-1]['P_kN'], M_kNm=0.0, c_m=0.0)]
    for c in np.geomspace(max(1e-7, length*1e-6), length*20.0, count):
        p_value, moment = response_at_c(length, thickness, layers, c, material)
        if p_value <= pmax*(1+1e-9):
            candidates.append(dict(P_kN=p_value, M_kNm=abs(moment), c_m=float(c)))
    candidates.append(dict(P_kN=pmax, M_kNm=0.0, c_m=None))
    candidates.sort(key=lambda item:item['P_kN'])
    # Quitar puntos casi duplicados en P y mantener el de mayor M.
    clean=[]
    for item in candidates:
        if clean and abs(item['P_kN']-clean[-1]['P_kN']) < 1e-7*max(1.0,abs(pmax)):
            if item['M_kNm'] > clean[-1]['M_kNm']:
                clean[-1]=item
        else:
            clean.append(item)
    return clean, points


def active_segments(wall, assignment):
    z0, z1 = sorted((float(wall['z_i_m']), float(wall['z_j_m'])))
    segments=[]
    for profile in assignment['profiles']:
        lo=max(z0,float(profile['z_min_m']))
        hi=min(z1,float(profile['z_max_m']))
        if hi-lo > 1e-8:
            segments.append((lo,hi,profile))
    covered=sum(hi-lo for lo,hi,_ in segments)
    if abs(covered-(z1-z0)) > 1e-6:
        raise ValueError(f'Muro {wall["id"]}: perfiles no cubren toda su altura')
    return segments


def calculate(model_path=MODEL, assignments_path=ASSIGNMENTS):
    model=json.loads(Path(model_path).read_text(encoding='utf-8'))
    registry=json.loads(Path(assignments_path).read_text(encoding='utf-8'))
    walls={int(w['id']):w for w in model['walls']}
    assignments={int(a['wall_id']):a for a in registry['assignments']}
    model_sources={int(w.get('source_wall_id',w['id'])) for w in walls.values()}
    if model_sources != set(assignments):
        raise ValueError(f'IDs de muro origen sin correspondencia: modelo={sorted(model_sources-set(assignments))}, '
                         f'registro={sorted(set(assignments)-model_sources)}')
    material={key:registry[key] for key in
              ('fc_MPa','fy_MPa','Es_MPa','eps_cu','beta1','factor_compresion_max')}
    cover=float(registry['cover_center_m'])
    results=[]
    for wall_id in sorted(walls):
        wall=walls[wall_id]; source_wall_id=int(wall.get('source_wall_id',wall_id)); assignment=assignments[source_wall_id]
        length=wall_length(wall); thickness=float(wall['thickness_m'])
        for sequence,(z0,z1,profile) in enumerate(active_segments(wall,assignment),1):
            layers,actual_spacing=vertical_steel_layers(length,profile,cover)
            curve,points=dense_envelope(length,thickness,layers,material)
            results.append(dict(wall_id=wall_id,source_wall_id=source_wall_id,piso=int(wall.get('floor',0)),
                segmento=sequence,z_min_m=z0,z_max_m=z1,
                identificacion_plano=assignment['identificacion_plano'],confianza=assignment['confianza'],
                criterio_asignacion=assignment['criterio_asignacion'],length_m=length,thickness_m=thickness,
                vertical_diameter_mm=profile['vertical_diameter_mm'],vertical_spacing_mm=profile['vertical_spacing_mm'],
                actual_spacing_m=actual_spacing,horizontal_diameter_mm=profile['horizontal_diameter_mm'],
                horizontal_spacing_mm=profile['horizontal_spacing_mm'],boundary_bars_each_end=profile['boundary_bars_each_end'],
                boundary_diameter_mm=profile['boundary_diameter_mm'],layers=layers,As_m2=float(layers[:,1].sum()),
                curve=curve,points=points,note=profile.get('note','')))
    return results,registry


def run(out, model_path=MODEL, assignments_path=ASSIGNMENTS):
    out=Path(out); out.mkdir(parents=True,exist_ok=True)
    results,registry=calculate(model_path,assignments_path)
    curve_rows=[]; point_rows=[]; summary_rows=[]
    for section in results:
        common={key:section[key] for key in ('wall_id','source_wall_id','piso','segmento','z_min_m','z_max_m','identificacion_plano',
            'confianza','length_m','thickness_m','vertical_diameter_mm','vertical_spacing_mm',
            'horizontal_diameter_mm','horizontal_spacing_mm','boundary_bars_each_end','boundary_diameter_mm','As_m2')}
        for index,item in enumerate(section['curve']):
            for branch,sign in (('+principal',1),('-principal',-1)):
                curve_rows.append(dict(**common,ramal=branch,indice=index,P_kN=item['P_kN'],
                                       M_kNm=sign*item['M_kNm'],c_m=item['c_m']))
        for item in section['points']:
            for branch,sign in (('+principal',1),('-principal',-1)):
                point_rows.append(dict(**common,punto=item['punto'],ramal=branch,
                                       P_kN=item['P_kN'],M_kNm=sign*item['M_kNm'],c_m=item['c_m']))
        flexure=next(item for item in section['points'] if item['punto']=='F')
        summary_rows.append(dict(**common,separacion_real_vertical_mm=section['actual_spacing_m']*1000,
            rho_vertical=section['As_m2']/(section['length_m']*section['thickness_m']),
            P_compresion_max_kN=section['points'][0]['P_kN'],
            M_P0_kNm=flexure['M_kNm'],P_traccion_kN=section['points'][-1]['P_kN'],
            nota=section['note'],criterio_asignacion=section['criterio_asignacion']))
    dump_csv(out/'PM_muros_envolvente.csv',curve_rows)
    dump_csv(out/'PM_muros_puntos_clave.csv',point_rows)
    dump_csv(out/'PM_muros_resumen.csv',summary_rows)

    unity_walls=[]
    for wall_id in sorted({item['wall_id'] for item in results}):
        wall_sections=[]
        for section in (item for item in results if item['wall_id']==wall_id):
            wall_sections.append(dict(z_min=section['z_min_m'],z_max=section['z_max_m'],
                length=section['length_m'],thickness=section['thickness_m'],
                dv=float(section['vertical_diameter_mm']),sv=float(section['vertical_spacing_mm']),
                dh=float(section['horizontal_diameter_mm']),sh=float(section['horizontal_spacing_mm']),
                boundary_count=int(section['boundary_bars_each_end']),
                boundary_diameter=float(section['boundary_diameter_mm']),As=section['As_m2'],
                note=section['note'],
                points=[dict(name=p['punto'],p=p['P_kN'],m=p['M_kNm']) for p in section['points']],
                curve=[dict(p=p['P_kN'],m=p['M_kNm']) for p in section['curve']]))
        first=next(item for item in results if item['wall_id']==wall_id)
        unity_walls.append(dict(id=wall_id,source_id=first['source_wall_id'],floor=first['piso'],name=first['identificacion_plano'],
                                confidence=first['confianza'],segments=wall_sections))
    (out/'PM_muros_unity.json').write_text(json.dumps(dict(walls=unity_walls),ensure_ascii=False),encoding='utf-8')

    # Una subfigura por muro. Se grafica el segmento inferior disponible, que
    # normalmente es el más armado y deja la comparación entre los 24 IDs.
    fig,axes=plt.subplots(6,4,figsize=(15,22),layout='constrained')
    for source_wall_id,ax in zip(range(1,25),axes.ravel()):
        section=min((item for item in results if item['source_wall_id']==source_wall_id),key=lambda item:item['z_min_m'])
        p=[item['P_kN'] for item in section['curve']]
        m=[item['M_kNm'] for item in section['curve']]
        ax.plot(m,p,color='#2368a2',lw=1.4); ax.plot([-v for v in m],p,color='#2368a2',lw=1.4)
        kp=section['points']
        ax.scatter([item['M_kNm'] for item in kp],[item['P_kN'] for item in kp],s=10,color='#d24b3e')
        ax.set_title(f'Muro origen {source_wall_id} · L={section["length_m"]:.2f} m · e={section["thickness_m"]:.2f} m',fontsize=9)
        ax.grid(alpha=.2); ax.tick_params(labelsize=7)
        ax.set_xlabel('M principal [kN·m]',fontsize=8); ax.set_ylabel('P [kN]',fontsize=8)
    fig.suptitle('Envolventes P–M nominales de muros · segmento inferior disponible',fontsize=15)
    fig.savefig(out/'capacidad_PM_muros.png',dpi=160); plt.close(fig)

    counts={level:sum(1 for a in registry['assignments'] if a['confianza']==level)
            for level in ('alta','media','baja')}
    summary=dict(estado='OK',muros=len({r['source_wall_id'] for r in results}),
        panos_por_piso=len({r['wall_id'] for r in results}),secciones=len(results),
        puntos_envolvente=len(curve_rows),puntos_clave=len(point_rows),confianza=counts,
        direccion_principal=registry['direccion_principal'],doble_malla=registry['doble_malla'],
        advertencia=registry['advertencia'])
    (out/'resumen_capacidad_muros.json').write_text(json.dumps(summary,indent=2,ensure_ascii=False),encoding='utf-8')
    return summary


if __name__ == '__main__':
    result=run(ROOT/'results')
    print(json.dumps(result,indent=2,ensure_ascii=False))
