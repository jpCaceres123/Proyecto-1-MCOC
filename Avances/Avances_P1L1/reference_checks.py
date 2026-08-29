"""Independent hand-checks for the benchmark output.

The lateral displacement and beam force checks are intentionally simplified
screening estimates, not replacements for the OpenSees solution.
"""

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parent
RESULTS = ROOT / "results" / "benchmark_results.json"
OUT = ROOT / "results" / "reference_checks.json"


def main():
    data = json.loads(RESULTS.read_text(encoding="utf-8"))
    x = data["geometry"]["x_coords_m"]
    y = data["geometry"]["y_coords_m"]
    z = data["geometry"]["z_levels_m"]
    area = (x[-1] - x[0]) * (y[-1] - y[0])
    floors = len(z) - 1
    qg = data["cases"]["G"]["tributary_load_check"]["q_kN_m2"]
    qq = data["cases"]["Q"]["tributary_load_check"]["q_kN_m2"]
    lateral = data["assumptions"]["lateral_floor_force_kN"]
    beam_length = x[1] - x[0]
    beam_q = 0.5 * qg * (y[1] - y[0])
    beam_simple_moment = beam_q * beam_length**2 / 12.0
    e = 4_700.0 * (data["verified_from_plans"]["concrete_fc_kPa"] ** 0.5)
    inertia = 0.70 * 0.70**3 / 12.0
    height = z[-1] - z[0]
    lateral_displacement = (lateral * floors) * height**3 / (3.0 * e * inertia * 9)

    checks = {
        "units": {"force": "kN", "length": "m", "moment": "kN*m"},
        "exact_resultant_references": {
            "G_total_vertical_kN": -qg * area * floors,
            "Q_total_vertical_kN": -qq * area * floors,
            "EX_base_shear_kN": lateral * floors,
            "EY_base_shear_kN": lateral * floors,
        },
        "expected_support_reactions": {
            "G_vertical_kN": qg * area * floors,
            "Q_vertical_kN": qq * area * floors,
            "EX_horizontal_kN": -lateral * floors,
            "EY_horizontal_kN": -lateral * floors,
        },
        "simplified_screening_references": {
            "top_node_EX_displacement_m": lateral_displacement,
            "reference_beam_simple_support_end_moment_kN_m": beam_simple_moment,
            "reference_beam_simple_support_end_shear_kN": beam_q * beam_length / 2.0,
        },
        "compared_open_sees_values": {
            "G_total_vertical_kN": data["cases"]["G"]["reaction_sum_kN_and_kN_m"][2],
            "Q_total_vertical_kN": data["cases"]["Q"]["reaction_sum_kN_and_kN_m"][2],
            "EX_base_shear_kN": data["cases"]["EX"]["reaction_sum_kN_and_kN_m"][0],
            "EY_base_shear_kN": data["cases"]["EY"]["reaction_sum_kN_and_kN_m"][1],
            "EX_top_node_displacement_m": data["cases"]["EX"]["displacement_m"][0],
            "G_reference_beam_local_force": data["cases"]["G"]["reference_element_forces"],
        },
    }
    OUT.write_text(json.dumps(checks, indent=2), encoding="utf-8")
    print(OUT)


if __name__ == "__main__":
    main()
