"""Modelo OpenSeesPy 3D de vigas y columnas generado desde modelo_3d.json."""

from pathlib import Path
import json

import openseespy.opensees as ops


ROOT = Path(__file__).resolve().parent


def build_model():
    data = json.loads((ROOT / "modelo_3d_manual.json").read_text(encoding="utf-8"))
    ops.wipe()
    ops.model("basic", "-ndm", 3, "-ndf", 6)
    ops.geomTransf("Linear", 1, 0, 0, 1)
    ops.geomTransf("Linear", 2, 1, 0, 0)
    ops.geomTransf("Linear", 3, 0, 1, 0)

    for node in data["nodes"]:
        tag = node["id"]
        ops.node(tag, node["x_m"], node["y_m"], node["z_m"])
        if node.get("restraint", node["level"] == "SUBT_1"):
            ops.fix(tag, 1, 1, 1, 1, 1, 1)
        else:
            mass = data["mass_per_node_t"] * 9.80665
            ops.mass(tag, mass, mass, mass, 0.0, 0.0, 0.0)

    E = data["material"]["E_kPa"] * 1000.0
    G = E / (2.0 * (1.0 + data["material"]["nu"]))
    for element in data["elements"]:
        if element["type"] == "COLUMN":
            section = data["section_columns"]
        elif element["type"] == "BEAM_SMALL":
            section = data["section_small_beams"]
        elif element["type"] == "BEAM_VARIABLE":
            section = data["section_variable_beams"]
        elif element["type"] == "BEAM_40x60":
            section = data["section_40x60_beams"]
        else:
            section = data["section_beams"]
        direction = element.get("direction", element["type"])
        if element["type"] == "COLUMN":
            transform = 3
        elif direction == "X":
            transform = 1
        else:
            transform = 2
        ops.element("elasticBeamColumn", element["id"], element["i"], element["j"],
                    section["A_m2"], E, G, section["J_m4"], section["Iy_m4"],
                    section["Iz_m4"], transform)
    if data.get("slabs"):
        slab_section = 10
        thickness = data["slabs"][0]["thickness_m"]
        ops.section("ElasticMembranePlateSection", slab_section, E, data["material"]["nu"], thickness)
        for slab in data["slabs"]:
            ops.element("ShellMITC4", slab["id"], *slab["node_ids"], slab_section)
    return data


if __name__ == "__main__":
    model = build_model()
    print(f"Modelo 3D creado: {len(model['nodes'])} nodos, {len(model['elements'])} elementos y {len(model.get('slabs', []))} losas")
    print(f"Fuente de geometria: {model['source']}")
    print("Las losas se modelan con ShellMITC4; revisar conectividad y malla antes del analisis.")
