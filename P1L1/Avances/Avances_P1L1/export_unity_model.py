"""Export the LT1 benchmark geometry as a Unity-independent JSON contract."""

import json
from pathlib import Path

from benchmark_3d import (
    BEAM_B_M,
    BEAM_H_M,
    COL_B_M,
    COL_H_M,
    E_KPA,
    FC_KPA,
    G_KPA,
    NU,
    X_COORDS_M,
    Y_COORDS_M,
    Z_LEVELS_M,
    node_tag,
)


OUT_PATH = Path(__file__).resolve().parent / "results" / "unity_model.json"


def unity_position(x, y, z):
    return {"x_m": x, "y_m": z, "z_m": y}


def local_axes(direction):
    if direction == "column":
        local_x = (0.0, 0.0, 1.0)
        local_y = (0.0, -1.0, 0.0)
        local_z = (1.0, 0.0, 0.0)
    elif direction == "beam_x":
        local_x = (1.0, 0.0, 0.0)
        local_y = (0.0, 1.0, 0.0)
        local_z = (0.0, 0.0, 1.0)
    else:
        local_x = (0.0, 1.0, 0.0)
        local_y = (-1.0, 0.0, 0.0)
        local_z = (0.0, 0.0, 1.0)
    return {
        "x_os": list(local_x),
        "y_os": list(local_y),
        "z_os": list(local_z),
    }


def main():
    nodes = []
    for level, z in enumerate(Z_LEVELS_M):
        for ix, x in enumerate(X_COORDS_M):
            for iy, y in enumerate(Y_COORDS_M):
                tag = node_tag(level, ix, iy)
                nodes.append({
                    "nodeTag": tag,
                    "levelIndex": level,
                    "position_os_m": {"x_m": x, "y_m": y, "z_m": z},
                    "position_unity_m": unity_position(x, y, z),
                    "isSupport": level == 0,
                })

    elements = []
    element_tag = 1
    for level in range(len(Z_LEVELS_M) - 1):
        for ix in range(3):
            for iy in range(3):
                elements.append({
                    "elementTag": element_tag,
                    "kind": "column",
                    "nodeI": node_tag(level, ix, iy),
                    "nodeJ": node_tag(level + 1, ix, iy),
                    "sectionId": "C70x70",
                    "transformationId": 1,
                    "localAxes_os": local_axes("column"),
                })
                element_tag += 1

    for level in range(1, len(Z_LEVELS_M)):
        for ix in range(2):
            for iy in range(3):
                elements.append({
                    "elementTag": element_tag,
                    "kind": "beam_x",
                    "nodeI": node_tag(level, ix, iy),
                    "nodeJ": node_tag(level, ix + 1, iy),
                    "sectionId": "B60x80",
                    "transformationId": 2,
                    "localAxes_os": local_axes("beam_x"),
                })
                element_tag += 1
        for ix in range(3):
            for iy in range(2):
                elements.append({
                    "elementTag": element_tag,
                    "kind": "beam_y",
                    "nodeI": node_tag(level, ix, iy),
                    "nodeJ": node_tag(level, ix, iy + 1),
                    "sectionId": "B60x80",
                    "transformationId": 2,
                    "localAxes_os": local_axes("beam_y"),
                })
                element_tag += 1

    diaphragms = []
    for level in range(1, len(Z_LEVELS_M)):
        master = node_tag(level, 1, 1)
        slaves = [node_tag(level, ix, iy) for ix in range(3) for iy in range(3)
                  if (ix, iy) != (1, 1)]
        diaphragms.append({
            "levelIndex": level,
            "masterNodeTag": master,
            "slaveNodeTags": slaves,
            "normalAxis_os": "Z",
        })

    model = {
        "schema": "structural_model_unity_v1",
        "units": {"length": "m", "force": "kN", "stress": "kPa", "moment": "kN*m"},
        "source": {
            "model": "LT1 3D OpenSeesPy benchmark",
            "sourcePlans": ["2017_67-102.pdf", "2017_67-103.pdf", "2017_67-300.pdf", "2017_67-304.pdf"],
        },
        "coordinateMapping": {
            "openSeesAxes": "X horizontal, Y horizontal, Z vertical",
            "unityAxes": "X right, Y up, Z forward",
            "openSeesToUnity": "(x_os, y_os, z_os) -> (x_unity, y_unity, z_unity) = (x_os, z_os, y_os)",
        },
        "geometry": {
            "axisLabels": {"x": ["E", "F", "G"], "y": ["1", "2", "3"]},
            "xCoordinates_m": list(X_COORDS_M),
            "yCoordinates_m": list(Y_COORDS_M),
            "zLevels_m": list(Z_LEVELS_M),
            "rigidOffsets_m": {"E_prime_to_E": 0.25, "F_to_F_prime": 0.25},
        },
        "sections": [
            {"sectionId": "C70x70", "kind": "column", "width_m": COL_B_M,
             "height_m": COL_H_M, "area_m2": COL_B_M * COL_H_M,
             "material": {"fc_kPa": FC_KPA, "E_kPa": E_KPA, "G_kPa": G_KPA, "nu": NU}},
            {"sectionId": "B60x80", "kind": "beam", "width_m": BEAM_B_M,
             "height_m": BEAM_H_M, "area_m2": BEAM_B_M * BEAM_H_M,
             "material": {"fc_kPa": FC_KPA, "E_kPa": E_KPA, "G_kPa": G_KPA, "nu": NU}},
        ],
        "nodes": nodes,
        "elements": elements,
        "supports": [{"nodeTag": node_tag(0, ix, iy),
                       "restrainedDofs": ["Ux", "Uy", "Uz", "Rx", "Ry", "Rz"]}
                      for ix in range(3) for iy in range(3)],
        "diaphragms": diaphragms,
        "counts": {"nodes": len(nodes), "elements": len(elements),
                   "supports": 9, "diaphragms": len(diaphragms)},
    }
    OUT_PATH.write_text(json.dumps(model, indent=2), encoding="utf-8")
    print(OUT_PATH)


if __name__ == "__main__":
    main()
