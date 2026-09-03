"""Modelo OpenSeesPy 3D de vigas y columnas generado desde modelo_3d.json."""

from pathlib import Path
import json

import openseespy.opensees as ops


ROOT = Path(__file__).resolve().parents[1]
MODEL = ROOT / "outputs" / "modelo_3d_manual.json"


def build_model():
    data = json.loads(MODEL.read_text(encoding="utf-8"))
    ops.wipe()
    ops.model("basic", "-ndm", 3, "-ndf", 6)
    ops.geomTransf("Linear", 1, 0, 0, 1)
    ops.geomTransf("Linear", 2, 1, 0, 0)
    ops.geomTransf("Linear", 3, 0, 1, 0)

    structural_node_ids = {
        node_id
        for element in data["elements"]
        for node_id in (element["i"], element["j"])
    }
    data["structural_node_ids"] = sorted(structural_node_ids)
    for node in data["nodes"]:
        if node["id"] not in structural_node_ids:
            continue
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
        elif element["type"] == "WALL":
            thickness = element["thickness_m"]
            wall_width = 1.0
            section = {
                "A_m2": thickness * wall_width,
                "J_m4": thickness * wall_width ** 3 / 12.0,
                "Iy_m4": thickness * wall_width ** 3 / 12.0,
                "Iz_m4": wall_width * thickness ** 3 / 12.0,
            }
        else:
            section = data["section_beams"]
        direction = element.get("direction", element["type"])
        if element["type"] in ("COLUMN", "WALL"):
            transform = 3
        elif direction == "X":
            transform = 1
        else:
            transform = 2
        ops.element("elasticBeamColumn", element["id"], element["i"], element["j"],
                    section["A_m2"], E, G, section["J_m4"], section["Iy_m4"],
                    section["Iz_m4"], transform)
    if data.get("beam_load_cases") or data.get("beam_slab_loads"):
        apply_slab_loads_to_beams(data)
    data["coincident_nodes"] = merge_coincident_nodes(data)
    data["diaphragms"] = apply_rigid_diaphragms(data)
    return data


def apply_slab_loads_to_beams(data):
    """Apply tributary dead and live load resultants to beam end nodes.

    The tributary distribution shape is retained in the generated contract. The
    OpenSees model receives its exact resultant through two separate patterns,
    split equally between the beam end nodes.
    """
    nodes_by_element = {element["id"]: (element["i"], element["j"])
                        for element in data["elements"]}
    cases = data.get("beam_load_cases")
    if not cases:
        cases = [{**load, "dead_load_kN": load["total_load_kN"], "live_load_kN": 0.0}
                 for load in data.get("beam_slab_loads", [])]

    dead_loads = {}
    live_loads = {}
    for load in cases:
        beam_nodes = nodes_by_element.get(load["beam_id"])
        if not beam_nodes:
            continue
        for node_id in beam_nodes:
            dead_loads[node_id] = dead_loads.get(node_id, 0.0) - load["dead_load_kN"] / 2.0
            live_loads[node_id] = live_loads.get(node_id, 0.0) - load["live_load_kN"] / 2.0

    ops.timeSeries("Linear", 1)
    ops.pattern("Plain", 1, 1)
    for node_id, pz in dead_loads.items():
        ops.load(node_id, 0.0, 0.0, pz, 0.0, 0.0, 0.0)

    ops.timeSeries("Linear", 2)
    ops.pattern("Plain", 2, 2)
    for node_id, pz in live_loads.items():
        ops.load(node_id, 0.0, 0.0, pz, 0.0, 0.0, 0.0)


def analyze_gravity(data):
    """Run one linear static step with dead and live load patterns active."""
    ops.wipeAnalysis()
    ops.constraints("Transformation")
    ops.numberer("RCM")
    ops.system("BandGeneral")
    ops.test("NormDispIncr", 1.0e-8, 50, 0)
    ops.algorithm("Linear")
    ops.integrator("LoadControl", 1.0)
    ops.analysis("Static")

    result = ops.analyze(1)
    if result != 0:
        print(f"Analisis gravitacional fallo con codigo {result}")
        return False

    ops.reactions()
    structural_nodes = data["structural_node_ids"]
    reaction_z = sum(ops.nodeReaction(node_id, 3) for node_id in structural_nodes)
    support_reaction_z = sum(
        ops.nodeReaction(node["id"], 3)
        for node in data["nodes"]
        if node.get("restraint") and node["id"] in structural_nodes
    )
    dead_total = sum(load["dead_load_kN"] for load in data.get("beam_load_cases", []))
    live_total = sum(load["live_load_kN"] for load in data.get("beam_load_cases", []))
    applied_total = dead_total + live_total
    residual = reaction_z - applied_total
    print(f"Carga muerta aplicada: {dead_total:.6f} kN")
    print(f"Sobrecarga aplicada: {live_total:.6f} kN")
    print(f"Reaccion nodal vertical global: {reaction_z:.6f} kN")
    print(f"Reaccion en apoyos directos: {support_reaction_z:.6f} kN")
    print(f"Residual de equilibrio: {residual:.9f} kN")
    return abs(residual) < 1.0e-5


def apply_rigid_diaphragms(data):
    """Constrain each elevated level as a rigid horizontal diaphragm."""
    by_level = {}
    for node in data["nodes"]:
        if node["id"] not in data["structural_node_ids"]:
            continue
        z = round(node["z_m"], 6)
        if z > 0.0:
            by_level.setdefault(z, []).append(node["id"])

    diaphragms = []
    for z, node_ids in sorted(by_level.items()):
        master = min(node_ids)
        slaves = [node_id for node_id in node_ids if node_id != master]
        if slaves:
            ops.rigidDiaphragm(3, master, *slaves)
        diaphragms.append({"z_m": z, "master_node": master, "slave_count": len(slaves)})
    return diaphragms


def merge_coincident_nodes(data):
    """Tie duplicate structural nodes that occupy the same coordinates."""
    by_coordinate = {}
    for node in data["nodes"]:
        if node["id"] not in data["structural_node_ids"]:
            continue
        coordinate = tuple(round(node[key], 6) for key in ("x_m", "y_m", "z_m"))
        by_coordinate.setdefault(coordinate, []).append(node["id"])

    ties = []
    for coordinate, node_ids in sorted(by_coordinate.items()):
        master = min(node_ids)
        for slave in node_ids:
            if slave == master:
                continue
            ops.equalDOF(master, slave, 1, 2, 3, 4, 5, 6)
            ties.append({"master_node": master, "slave_node": slave, "coordinates_m": coordinate})
    return ties


if __name__ == "__main__":
    model = build_model()
    print(f"Modelo 3D creado: {len(model['nodes'])} nodos, {len(model['elements'])} elementos y {len(model.get('slabs', []))} losas")
    print(f"Fuente de geometria: {model['source']}")
    analyze_gravity(model)
    print("Las losas se usan solo para calcular areas tributarias; las cargas se aplican a las vigas.")
