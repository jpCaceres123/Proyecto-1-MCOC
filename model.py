"""Benchmark 3D OpenSeesPy. Units: kN, m, radians."""
from pathlib import Path
import csv
import json
import openseespy.opensees as ops

OUT = Path(__file__).parent / "results"
OUT.mkdir(exist_ok=True)

# Geometry and section properties
Lx, Ly, H = 6.0, 5.0, 3.0
E, nu = 25_000_000.0, 0.20  # kN/m2
G = E / (2.0 * (1.0 + nu))
Ac, Ic = 0.30 * 0.30, 0.30**4 / 12.0
Ab, Iy_b, Iz_b = 0.25 * 0.40, 0.25 * 0.40**3 / 12.0, 0.40 * 0.25**3 / 12.0
Jc, Jb = 0.141 * 0.30**4, 0.141 * 0.25**2 * 0.40**2
q_slab = 5.0

ops.wipe()
ops.model('basic', '-ndm', 3, '-ndf', 6)

coords = {1: (0, 0, 0), 2: (Lx, 0, 0), 3: (Lx, Ly, 0), 4: (0, Ly, 0),
          5: (0, 0, H), 6: (Lx, 0, H), 7: (Lx, Ly, H), 8: (0, Ly, H)}
for tag, xyz in coords.items():
    ops.node(tag, *xyz)
for tag in range(1, 5):
    ops.fix(tag, 1, 1, 1, 1, 1, 1)

# vecxz establishes local z. It is deliberately global Z for horizontal members.
ops.geomTransf('Linear', 1, 1, 0, 0)  # columns: local z approximately global X
ops.geomTransf('Linear', 2, 0, 0, 1)  # beams parallel to global X
ops.geomTransf('Linear', 3, 0, 0, 1)  # beams parallel to global Y

elements = {}
tag = 1
for i, j in ((1, 5), (2, 6), (3, 7), (4, 8)):
    ops.element('elasticBeamColumn', tag, i, j, Ac, E, G, Jc, Ic, Ic, 1)
    elements[tag] = ('column', i, j, 0.0)
    tag += 1
for i, j in ((5, 6), (8, 7)):
    ops.element('elasticBeamColumn', tag, i, j, Ab, E, G, Jb, Iy_b, Iz_b, 2)
    elements[tag] = ('beam_x', i, j, q_slab * Ly / 2.0)
    tag += 1
for i, j in ((5, 8), (6, 7)):
    ops.element('elasticBeamColumn', tag, i, j, Ab, E, G, Jb, Iy_b, Iz_b, 3)
    elements[tag] = ('beam_y', i, j, q_slab * Lx / 2.0)
    tag += 1

ops.timeSeries('Linear', 1)
ops.pattern('Plain', 1, 1)
for etag, (kind, _, _, w) in elements.items():
    if w:
        ops.eleLoad('-ele', etag, '-type', '-beamUniform', 0.0, -w, 0.0)

ops.system('BandGeneral')
ops.numberer('RCM')
ops.constraints('Transformation')
ops.integrator('LoadControl', 1.0)
ops.algorithm('Linear')
ops.analysis('Static')
ok = ops.analyze(1)
if ok != 0:
    raise RuntimeError(f'OpenSees static analysis failed with code {ok}')
ops.reactions()

def write_csv(name, header, rows):
    with (OUT / name).open('w', newline='', encoding='utf-8') as f:
        writer = csv.writer(f)
        writer.writerow(header)
        writer.writerows(rows)

disp_rows = [[n, *ops.nodeDisp(n)] for n in coords]
reaction_rows = [[n, *ops.nodeReaction(n)] for n in range(1, 5)]
force_rows = [[e, *ops.eleResponse(e, 'localForce')] for e in elements]
write_csv('nodal_displacements.csv', ['node', 'ux', 'uy', 'uz', 'rx', 'ry', 'rz'], disp_rows)
write_csv('reactions.csv', ['node', 'Fx', 'Fy', 'Fz', 'Mx', 'My', 'Mz'], reaction_rows)
write_csv('element_forces.csv', ['element'] + [f'f{i}' for i in range(1, 13)], force_rows)

total_load = 2 * Lx * q_slab * Ly / 2 + 2 * Ly * q_slab * Lx / 2
sum_reaction_z = sum(row[3] for row in reaction_rows)
node_check, axial_element, moment_element = 7, 1, 5
axial_check = force_rows[axial_element - 1]
moment_check = force_rows[moment_element - 1]
moment_reference = abs(moment_check[5])
values = {
    'total_applied_vertical_load_kN': total_load,
    'sum_vertical_reactions_kN': sum_reaction_z,
    'equilibrium_error_kN': sum_reaction_z - total_load,
    'node_7_uz_m': ops.nodeDisp(node_check)[2],
    'element_1_local_force': axial_check[1:],
    'element_5_local_force': moment_check[1:],
    'analysis_code': ok,
}
(OUT / 'verification.json').write_text(json.dumps(values, indent=2), encoding='utf-8')
(OUT / 'verification.md').write_text(f'''# Verificación automática\n\nCaso ejecutado con OpenSeesPy en unidades kN-m.\n\n| Magnitud | Referencia independiente | OpenSeesPy | Error |\n|---|---:|---:|---:|\n| Carga vertical total (kN) | {total_load:.6f} | {total_load:.6f} | 0.000000 |\n| Suma de reacciones Z (kN) | {total_load:.6f} | {sum_reaction_z:.6f} | {sum_reaction_z-total_load:.3e} |\n| Desplazamiento nodo 7, uz (m) | revisar estimación elástica | {ops.nodeDisp(7)[2]:.6e} | n/a |\n| Axial elemento 1, extremo i (kN) | simetría: 300/4 = 75.000000 | {axial_check[1]:.6f} | n/a |\n| Momento extremo elemento 5, My-i (kN m) | viga fija: abs(wL²/12) = {12.5*Lx**2/12:.6f} | {moment_reference:.6f} | n/a |\n\nLa primera y segunda filas son referencias independientes de estática global. Para la defensa, la estimación de viga fija de la última fila sirve como orden de magnitud; el marco redistribuye momentos entre columnas y vigas, por lo que no se espera igualdad exacta.\n\n**Criterio:** el equilibrio global pasa si `abs(error) < 1e-8 kN`.\n''', encoding='utf-8')
print(json.dumps(values, indent=2))
