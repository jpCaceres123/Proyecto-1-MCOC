"""Bidirectional slab tributary-load distributor.

The method uses 45-degree lines from the panel corners. For each edge it
returns a piecewise-linear line load in kN/m and checks load conservation.
"""

import json
import sys
from pathlib import Path


TOLERANCE = 1.0e-10


def _breakpoints(length, other_length):
    points = {0.0, length, length / 2.0}
    if other_length / 2.0 < length:
        points.add(other_length / 2.0)
        points.add(length - other_length / 2.0)
    return sorted(points)


def _edge_load_points(length, other_length, q_kN_m2):
    return [
        {"position_m": position, "load_kN_m": q_kN_m2 * min(
            position, length - position, other_length / 2.0
        )}
        for position in _breakpoints(length, other_length)
    ]


def _integral(points):
    return sum(
        (points[index]["load_kN_m"] + points[index + 1]["load_kN_m"])
        * (points[index + 1]["position_m"] - points[index]["position_m"])
        / 2.0
        for index in range(len(points) - 1)
    )


def distribute_panel(panel):
    length_x = float(panel["length_x_m"])
    length_y = float(panel["length_y_m"])
    q = float(panel["q_kN_m2"])
    if length_x <= 0 or length_y <= 0 or q < 0:
        raise ValueError("Panel lengths must be positive and q cannot be negative")

    edges = {
        "y_min_beam_x": _edge_load_points(length_x, length_y, q),
        "y_max_beam_x": _edge_load_points(length_x, length_y, q),
        "x_min_beam_y": _edge_load_points(length_y, length_x, q),
        "x_max_beam_y": _edge_load_points(length_y, length_x, q),
    }
    loads = {edge: _integral(points) for edge, points in edges.items()}
    area_load = q * length_x * length_y
    distributed_load = sum(loads.values())
    residual = distributed_load - area_load
    if abs(residual) > TOLERANCE * max(1.0, area_load):
        raise RuntimeError(f"Load conservation failed: {residual}")

    return {
        "panel_id": panel.get("panel_id", "panel"),
        "units": {"length": "m", "surface_load": "kN/m2", "line_load": "kN/m"},
        "dimensions_m": {"x": length_x, "y": length_y},
        "q_kN_m2": q,
        "edges": edges,
        "edge_loads_kN": loads,
        "total_panel_load_kN": area_load,
        "total_distributed_load_kN": distributed_load,
        "conservation_residual_kN": residual,
        "method": "45-degree tributary lines to the four panel edges",
    }


def main():
    if len(sys.argv) != 2:
        raise SystemExit("Uso: python slab_bidirectional.py input.json")
    input_path = Path(sys.argv[1])
    data = json.loads(input_path.read_text(encoding="utf-8"))
    panels = data.get("panels", [data]) if isinstance(data, dict) else data
    result = {
        "units": {"length": "m", "surface_load": "kN/m2", "line_load": "kN/m"},
        "method": "Distribucion bidireccional por lineas a 45 grados",
        "panels": [distribute_panel(panel) for panel in panels],
    }
    output_path = input_path.with_name(input_path.stem + "_distribution.json")
    output_path.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(output_path)


if __name__ == "__main__":
    main()
