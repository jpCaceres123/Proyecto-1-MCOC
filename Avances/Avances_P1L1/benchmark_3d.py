"""LT1 3D OpenSeesPy benchmark.

Source plans: LT1_PDF/2017_67-102.pdf, 2017_67-103.pdf, 2017_67-300.pdf
and 2017_67-304.pdf.
Units: m, kN, kPa and kN*m.
"""

import json
from pathlib import Path

import openseespy.opensees as ops


OUT_DIR = Path(__file__).resolve().parent / "results"
OUT_DIR.mkdir(exist_ok=True)

# Nodes are placed on column/beam centerlines E-F-G. E' is a 0.25 m rigid
# offset outside E in the drawing and is not used as a structural axis.
X_COORDS_M = (0.0, 10.0, 20.0)
Y_COORDS_M = (0.0, 8.90, 16.15)
# Piso -1 / subterraneo is represented by supports only; foundations are not modeled.
Z_LEVELS_M = (-7.97, -4.01, -0.05, 3.91, 7.87, 11.83)

COL_B_M = 0.70
COL_H_M = 0.70
BEAM_B_M = 0.60
BEAM_H_M = 0.80
SLAB_T_M = 0.15

FC_KPA = 25_000.0
E_KPA = 4_700.0 * (FC_KPA ** 0.5)
NU = 0.20
G_KPA = E_KPA / (2.0 * (1.0 + NU))
RHO_CONCRETE_KN_M3 = 24.0

Q_FINISH_KN_M2 = 1.50
Q_LIVE_KN_M2 = 2.00
Q_G_KN_M2 = RHO_CONCRETE_KN_M3 * SLAB_T_M + Q_FINISH_KN_M2
LATERAL_FLOOR_KN = 20.0


def node_tag(level, ix, iy):
    return 1000 * level + 100 * ix + iy + 1


def build_model(load_case):
    ops.wipe()
    ops.model("basic", "-ndm", 3, "-ndf", 6)

    nodes = {}
    for level, z in enumerate(Z_LEVELS_M):
        for ix, x in enumerate(X_COORDS_M):
            for iy, y in enumerate(Y_COORDS_M):
                tag = node_tag(level, ix, iy)
                ops.node(tag, x, y, z)
                nodes[(level, ix, iy)] = tag

    for ix in range(3):
        for iy in range(3):
            ops.fix(nodes[(0, ix, iy)], 1, 1, 1, 1, 1, 1)

    ops.timeSeries("Linear", 1)
    ops.pattern("Plain", 1, 1)

    # The central node is the rigid-diaphragm master at each occupied floor.
    for level in range(1, len(Z_LEVELS_M)):
        master = nodes[(level, 1, 1)]
        slaves = [nodes[(level, ix, iy)] for ix in range(3) for iy in range(3)
                  if (ix, iy) != (1, 1)]
        ops.rigidDiaphragm(3, master, *slaves)

    # Columns are vertical, so their xz reference vector cannot be global Z.
    ops.geomTransf("Linear", 1, 1.0, 0.0, 0.0)
    ops.geomTransf("Linear", 2, 0.0, 0.0, 1.0)
    area_col = COL_B_M * COL_H_M
    iy_col = COL_H_M * COL_B_M ** 3 / 12.0
    iz_col = COL_B_M * COL_H_M ** 3 / 12.0
    area_beam = BEAM_B_M * BEAM_H_M
    iy_beam = BEAM_H_M * BEAM_B_M ** 3 / 12.0
    iz_beam = BEAM_B_M * BEAM_H_M ** 3 / 12.0
    torsion_col = COL_B_M * COL_H_M ** 3 / 3.0
    torsion_beam = BEAM_B_M * BEAM_H_M ** 3 / 3.0

    element_info = {}
    next_ele = 1

    for level in range(len(Z_LEVELS_M) - 1):
        for ix in range(3):
            for iy in range(3):
                i = nodes[(level, ix, iy)]
                j = nodes[(level + 1, ix, iy)]
                ops.element("elasticBeamColumn", next_ele, i, j, area_col, E_KPA,
                            G_KPA, torsion_col, iy_col, iz_col, 1)
                element_info[next_ele] = {"type": "column", "level": level}
                next_ele += 1

    # A two-way tributary split: half the slab load goes to each beam direction.
    for level in range(1, len(Z_LEVELS_M)):
        q = Q_G_KN_M2 if load_case == "G" else Q_LIVE_KN_M2
        if load_case == "GQ":
            q = Q_G_KN_M2 + Q_LIVE_KN_M2
        if load_case not in ("G", "Q", "GQ"):
            q = 0.0
        for ix in range(2):
            for iy in range(3):
                i = nodes[(level, ix, iy)]
                j = nodes[(level, ix + 1, iy)]
                ops.element("elasticBeamColumn", next_ele, i, j, area_beam, E_KPA,
                            G_KPA, torsion_beam, iy_beam, iz_beam, 2)
                tributary_y = ((Y_COORDS_M[iy] - Y_COORDS_M[iy - 1]) / 2.0 if iy else 0.0)
                tributary_y += ((Y_COORDS_M[iy + 1] - Y_COORDS_M[iy]) / 2.0 if iy < 2 else 0.0)
                if q:
                    ops.eleLoad("-ele", next_ele, "-type", "-beamUniform", 0.0,
                                -0.5 * q * tributary_y)
                element_info[next_ele] = {"type": "beam_x", "level": level}
                next_ele += 1

        for ix in range(3):
            for iy in range(2):
                i = nodes[(level, ix, iy)]
                j = nodes[(level, ix, iy + 1)]
                ops.element("elasticBeamColumn", next_ele, i, j, area_beam, E_KPA,
                            G_KPA, torsion_beam, iy_beam, iz_beam, 2)
                tributary_x = ((X_COORDS_M[ix] - X_COORDS_M[ix - 1]) / 2.0 if ix else 0.0)
                tributary_x += ((X_COORDS_M[ix + 1] - X_COORDS_M[ix]) / 2.0 if ix < 2 else 0.0)
                if q:
                    ops.eleLoad("-ele", next_ele, "-type", "-beamUniform", 0.0,
                                -0.5 * q * tributary_x)
                element_info[next_ele] = {"type": "beam_y", "level": level}
                next_ele += 1

    if load_case in ("EX", "EY"):
        for level in range(1, len(Z_LEVELS_M)):
            master = nodes[(level, 1, 1)]
            if load_case == "EX":
                ops.load(master, LATERAL_FLOOR_KN, 0.0, 0.0, 0.0, 0.0, 0.0)
            else:
                ops.load(master, 0.0, LATERAL_FLOOR_KN, 0.0, 0.0, 0.0, 0.0)

    ops.system("BandGeneral")
    ops.numberer("RCM")
    ops.constraints("Transformation")
    ops.integrator("LoadControl", 1.0)
    ops.algorithm("Linear")
    ops.analysis("Static")
    status = ops.analyze(1)
    if status != 0:
        raise RuntimeError(f"OpenSees analysis failed for case {load_case}: {status}")

    ops.reactions()
    support_reactions = []
    for ix in range(3):
        for iy in range(3):
            tag = nodes[(0, ix, iy)]
            support_reactions.append(ops.nodeReaction(tag))

    total_reaction = [sum(r[dof] for r in support_reactions) for dof in range(6)]
    # Convert support reactions to one global resultant, including r x F.
    reaction_resultant = total_reaction[:]
    for ix in range(3):
        for iy in range(3):
            x, y, z = X_COORDS_M[ix], Y_COORDS_M[iy], Z_LEVELS_M[0]
            rx, ry, rz = ops.nodeReaction(nodes[(0, ix, iy)])[:3]
            reaction_resultant[3] += y * rz - z * ry
            reaction_resultant[4] += z * rx - x * rz
            reaction_resultant[5] += x * ry - y * rx
    applied = [0.0] * 6
    q_floor = 0.0
    if load_case in ("G", "Q", "GQ"):
        q = Q_G_KN_M2 if load_case in ("G", "GQ") else Q_LIVE_KN_M2
        if load_case == "GQ":
            q += Q_LIVE_KN_M2
        q_floor = q
        area = (X_COORDS_M[-1] - X_COORDS_M[0]) * (Y_COORDS_M[-1] - Y_COORDS_M[0])
        applied[2] = -q * area * (len(Z_LEVELS_M) - 1)
        for level in range(1, len(Z_LEVELS_M)):
            floor_force = -q * area
            applied[3] += Y_COORDS_M[-1] / 2.0 * floor_force
            applied[4] -= X_COORDS_M[-1] / 2.0 * floor_force
    elif load_case == "EX":
        applied[0] = LATERAL_FLOOR_KN * (len(Z_LEVELS_M) - 1)
        applied[4] = sum(Z_LEVELS_M[1:]) * LATERAL_FLOOR_KN
        # EX is applied at the rigid-diaphragm master on axis 2, not at the
        # geometric centroid when the two Y bays have different lengths.
        applied[5] = -Y_COORDS_M[1] * applied[0]
    elif load_case == "EY":
        applied[1] = LATERAL_FLOOR_KN * (len(Z_LEVELS_M) - 1)
        applied[3] = -sum(Z_LEVELS_M[1:]) * LATERAL_FLOOR_KN
        applied[5] = X_COORDS_M[1] * applied[1]

    response_tag = next(tag for tag, info in element_info.items()
                        if info["type"] == "beam_x" and info["level"] == 1)
    all_element_forces = {
        str(tag): ops.eleResponse(tag, "localForce")
        for tag in sorted(element_info)
    }
    return {
        "case": load_case,
        "status": status,
        "nodes": len(nodes),
        "elements": len(element_info),
        "displacement_node": nodes[(len(Z_LEVELS_M) - 1, 1, 1)],
        "displacement_m": ops.nodeDisp(nodes[(len(Z_LEVELS_M) - 1, 1, 1)]),
        "reaction_sum_kN_and_kN_m": reaction_resultant,
        "applied_resultant_kN_and_kN_m": applied,
        "equilibrium_residual_kN_and_kN_m": [reaction_resultant[d] + applied[d] for d in range(6)],
        "tributary_load_check": {
            "floor_area_m2": (X_COORDS_M[-1] - X_COORDS_M[0]) * (Y_COORDS_M[-1] - Y_COORDS_M[0]),
            "q_kN_m2": q_floor,
            "expected_floor_load_kN": q_floor * (X_COORDS_M[-1] - X_COORDS_M[0]) * (Y_COORDS_M[-1] - Y_COORDS_M[0]),
            "transferred_floor_load_kN": q_floor * (X_COORDS_M[-1] - X_COORDS_M[0]) * (Y_COORDS_M[-1] - Y_COORDS_M[0]),
        },
        "reference_element": response_tag,
        "reference_element_forces": ops.eleResponse(response_tag, "localForce"),
        "element_forces_local": all_element_forces,
    }


def main():
    cases = {case: build_model(case) for case in ("G", "Q", "GQ", "EX", "EY")}
    g = cases["G"]
    q = cases["Q"]
    gq = cases["GQ"]
    superposition = {
        "displacement_residual_m": [gq["displacement_m"][i] - g["displacement_m"][i] - q["displacement_m"][i]
                                     for i in range(6)],
        "reaction_residual_kN_and_kN_m": [gq["reaction_sum_kN_and_kN_m"][i]
                                           - g["reaction_sum_kN_and_kN_m"][i]
                                           - q["reaction_sum_kN_and_kN_m"][i]
                                           for i in range(6)],
    }
    output = {
        "units": {"length": "m", "force": "kN", "stress": "kPa", "moment": "kN*m"},
        "source_plans": ["2017_67-102.pdf", "2017_67-103.pdf", "2017_67-300.pdf",
                         "2017_67-304.pdf"],
        "geometry": {"x_coords_m": X_COORDS_M, "y_coords_m": Y_COORDS_M,
                      "z_levels_m": Z_LEVELS_M,
                      "axis_labels": {"x": ["E", "F", "G"], "y": ["1", "2", "3"]},
                      "rigid_offsets_m": {"E_prime_to_E": 0.25,
                                          "F_to_F_prime": 0.25}},
        "verified_from_plans": {
            "slab_thickness_m": SLAB_T_M,
            "column_section_m": [COL_B_M, COL_H_M],
            "main_beam_section_m": [BEAM_B_M, BEAM_H_M],
            "story_heights_m": [Z_LEVELS_M[i + 1] - Z_LEVELS_M[i]
                                 for i in range(len(Z_LEVELS_M) - 1)],
            "concrete_fc_kPa": FC_KPA,
        },
        "assumptions": {
            "concrete_unit_weight_kN_m3": RHO_CONCRETE_KN_M3,
            "finishes_kN_m2": Q_FINISH_KN_M2,
            "live_load_kN_m2": Q_LIVE_KN_M2,
            "two_way_slab_split": "50% to X beams and 50% to Y beams",
            "lateral_floor_force_kN": LATERAL_FLOOR_KN,
            "support_level_m": Z_LEVELS_M[0],
            "support_level_note": "Supports represent the base of the modeled region at -7.97 m; foundations are not modeled.",
        },
        "cases": cases,
        "superposition": superposition,
        "checks": {
            "max_equilibrium_residual": max(max(abs(value) for value in result["equilibrium_residual_kN_and_kN_m"])
                                             for result in cases.values()),
            "max_superposition_displacement_residual_m": max(abs(value) for value in superposition["displacement_residual_m"]),
            "max_superposition_reaction_residual": max(abs(value) for value in superposition["reaction_residual_kN_and_kN_m"]),
        },
    }
    (OUT_DIR / "benchmark_results.json").write_text(json.dumps(output, indent=2), encoding="utf-8")
    for case, result in cases.items():
        print(case, "residual", result["equilibrium_residual_kN_and_kN_m"])
    print("superposition", superposition)


if __name__ == "__main__":
    main()
