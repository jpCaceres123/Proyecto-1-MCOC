"""Edits keyed by analytical ID. Units: kN, m; global gravity is -Z."""
import math


def number(value, positive=False):
    value = float(value)
    if not math.isfinite(value) or value < 0 or (positive and value == 0):
        raise ValueError('Valor no finito, negativo o dimension nula')
    return value


def section(b, h, steel=False):
    b, h = number(b, True), number(h, True)
    if steel:
        # b = outer square dimension; h = wall thickness. Preserve SHS family.
        if 2*h >= b:
            raise ValueError('SHS: espesor debe ser menor que b/2')
        inner = b-2*h
        return dict(b_m=b, h_m=b, outer_width_m=b, wall_thickness_m=h,
                    A_m2=b*b-inner*inner, Iy_m4=(b**4-inner**4)/12,
                    Iz_m4=(b**4-inner**4)/12, J_m4=(b-h)**3*h)
    a, c = min(b, h), max(b, h)
    return dict(b_m=b, h_m=h, A_m2=b*h, Iy_m4=h*b**3/12,
                Iz_m4=b*h**3/12, J_m4=c*a**3*(1/3-.21*a/c*(1-(a/c)**4/12)))


def apply(data, changes):
    bars = {e['id']: e for e in data['elements'] if e['type'] != 'WALL'}
    walls = {e['id']: e for e in data['walls']}
    slabs = {e['id']: e for e in data['slabs']}
    seen = set()
    for edit in changes:
        kind, tag = edit['kind'], int(edit['id'])
        if (kind, tag) in seen:
            raise ValueError('ID duplicado en la variante')
        seen.add((kind, tag))
        group = slabs if kind == 'Losa' else walls if kind == 'Muro' else bars
        if kind not in ('Losa', 'Muro', 'Viga', 'Columna') or tag not in group:
            raise ValueError(f'Elemento no encontrado: {kind} {tag}')
        target = group[tag]
        if kind in ('Viga', 'Columna'):
            column = 'COLUMN' in target['type']
            if column != (kind == 'Columna'):
                raise ValueError('Tipo de elemento incompatible')
        if edit.get('changeSection'):
            if kind in ('Viga', 'Columna'):
                target['section_override'] = section(edit['b'], edit['h'], target['type'].startswith('STEEL'))
            else:
                old = target['thickness_m']
                target['thickness_m'] = number(edit['h'], True)
                if kind == 'Losa':
                    delta = (target['thickness_m']-old)*target['density_kg_m3']*9.80665/1000
                    target['self_weight_kN_m2'] += delta
                    for rows in (data['beam_load_cases'], data['wall_load_cases']):
                        for row in rows:
                            if row['slab_id'] == tag:
                                before = row['dead_load_kN']
                                row['dead_load_kN'] += delta*row['tributary_area_m2']
                                for key in ('w_G_start_kN_m', 'w_G_max_kN_m', 'w_G_end_kN_m'):
                                    if key in row and before:
                                        row[key] *= row['dead_load_kN']/before
        if edit.get('changeLoad'):
            q = number(edit['q'])
            if kind == 'Losa':
                target['interactive_original_Q_kN'] = sum(
                    r['tributary_area_m2']*r['q_SC_kN_m2']
                    for rows in (data['beam_load_cases'], data['wall_load_cases'])
                    for r in rows if r['slab_id'] == tag)
                target['interactive_q_kN_m2'] = q
                for rows in (data['beam_load_cases'], data['wall_load_cases']):
                    for row in rows:
                        if row['slab_id'] == tag:
                            row['interactive_q_kN_m2'] = q
            else:
                target['interactive_Q_kN_m'] = q
    data['interactive_changes'] = changes
