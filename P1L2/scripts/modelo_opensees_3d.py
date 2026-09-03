"""Modelo OpenSeesPy 3D: vigas, columnas, muros (ShellMITC4) y losas tributarias."""

from pathlib import Path
import json

import openseespy.opensees as ops


ROOT = Path(__file__).resolve().parents[1]
MODEL = ROOT / "outputs" / "modelo_3d_manual.json"

WALL_MESH_NODE_BASE = 3_000_000
WALL_ELEMENT_BASE = 500_000
WALL_SECTION_BASE = 200
# The source geometry contains wall centerlines offset from frame grid lines.
# This tolerance is only used at wall boundary nodes, not for interior nodes.
WALL_CONNECT_THRESHOLD_M = 2.0
WALL_TARGET_H_ELEMENT_M = 2.0


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
        if element["type"] != "WALL"
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
        # WALL records remain in the data contract for visualization. Their
        # analytical representation is generated below with ShellMITC4.
        if element["type"] == "WALL":
            continue
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

    wall_mesh = create_wall_mesh(data, structural_node_ids)
    data["wall_mesh"] = wall_mesh

    wall_mesh_ids = set()
    for wi in wall_mesh.get("walls", []):
        for nid in wi.get("node_ids", []):
            wall_mesh_ids.add(nid)
    data["structural_node_ids"] = sorted(set(data["structural_node_ids"]) | wall_mesh_ids)

    if data.get("beam_load_cases") or data.get("beam_slab_loads"):
        apply_slab_loads_to_beams(data)
    apply_wall_self_weight(wall_mesh)
    data["coincident_nodes"] = merge_coincident_nodes(data)
    data["diaphragms"] = apply_rigid_diaphragms(data)
    return data


# ---------------------------------------------------------------------------
# Wall ShellMITC4 mesh
# ---------------------------------------------------------------------------

def create_wall_sections(data):
    """Create ElasticMembranePlateSection for each unique wall thickness."""
    E = data["material"]["E_kPa"] * 1000.0
    nu = data["material"]["nu"]
    # Convert kg/m3 to consistent kN-s2/m4 units.
    rho = data.get("wall_density_kg_m3", 2500.0) * 9.80665 / 1000.0
    thicknesses = sorted({w["thickness_m"] for w in data.get("walls", [])})
    sections = {}
    for idx, t in enumerate(thicknesses):
        tag = WALL_SECTION_BASE + idx
        ops.section("ElasticMembranePlateSection", tag, E, nu, t, rho)
        sections[t] = tag
    return sections


def create_wall_mesh(data, structural_node_ids):
    """Discretise every wall as ShellMITC4 elements.

    Returns a dict describing the mesh for downstream bookkeeping.
    """
    walls = data.get("walls", [])
    if not walls:
        return {"wall_count": 0, "shell_count": 0, "mesh_node_count": 0}

    levels = sorted({round(n["z_m"], 6) for n in data["nodes"]
                     if n["id"] in structural_node_ids})
    frame_nodes = [n for n in data["nodes"]
                   if n["id"] in structural_node_ids
                   and n.get("status") != "MANUAL_WALL_NODE"]
    wall_sections = create_wall_sections(data)

    next_mesh_node = WALL_MESH_NODE_BASE
    next_shell = WALL_ELEMENT_BASE

    existing_xyz = {
        (round(n["x_m"], 6), round(n["y_m"], 6), round(n["z_m"], 6)): n["id"]
        for n in data["nodes"]
        if n["id"] in structural_node_ids
    }

    mesh_info = {
        "wall_count": len(walls), "shell_count": 0, "mesh_node_count": 0,
        "sections": {str(k): v for k, v in wall_sections.items()},
        "walls": [],
        "self_weight_kN": 0.0,
        "node_self_weight_kN": {},
    }

    for wall in walls:
        z0, z1 = wall["z_i_m"], wall["z_j_m"]
        x0, y0 = wall["x_i_m"], wall["y_i_m"]
        x1, y1 = wall["x_j_m"], wall["y_j_m"]

        wall_levels = [z for z in levels if z0 - 1e-6 <= z <= z1 + 1e-6]
        if len(wall_levels) < 2:
            continue

        dx = x1 - x0
        dy = y1 - y0
        wall_len = (dx ** 2 + dy ** 2) ** 0.5
        if wall_len < 1e-6:
            continue

        nh = max(1, round(wall_len / WALL_TARGET_H_ELEMENT_M))
        nv = len(wall_levels) - 1
        section_tag = wall_sections[wall["thickness_m"]]

        wall_nodes = []
        for j, z in enumerate(wall_levels):
            row = []
            for i in range(nh + 1):
                t = i / nh
                x = x0 + t * dx
                y = y0 + t * dy
                key = (round(x, 6), round(y, 6), round(z, 6))
                if key in existing_xyz:
                    row.append(existing_xyz[key])
                else:
                    tag = next_mesh_node
                    next_mesh_node += 1
                    ops.node(tag, x, y, z)
                    if abs(z) < 1e-6:
                        ops.fix(tag, 1, 1, 1, 1, 1, 1)
                    data["nodes"].append({
                        "id": tag,
                        "level": min(range(len(levels)),
                                      key=lambda index: abs(levels[index] - z)),
                        "axis": "WALL_SHELL", "x_m": x, "y_m": y, "z_m": z,
                        "restraint": abs(z) < 1e-6,
                        "status": "WALL_SHELL_NODE", "wall_id": wall["id"],
                    })
                    existing_xyz[key] = tag
                    row.append(tag)
            wall_nodes.append(row)

        created_shells = 0
        for j in range(nv):
            for i in range(nh):
                n1 = wall_nodes[j][i]
                n2 = wall_nodes[j][i + 1]
                n3 = wall_nodes[j + 1][i + 1]
                n4 = wall_nodes[j + 1][i]
                if n1 == n2 or n2 == n3 or n3 == n4 or n4 == n1:
                    continue
                ops.element("ShellMITC4", next_shell, n1, n2, n3, n4, section_tag)
                next_shell += 1
                created_shells += 1

                shell_area = (wall_len / nh) * (wall_levels[j + 1] - wall_levels[j])
                shell_weight = (
                    data.get("wall_density_kg_m3", 2500.0)
                    * 9.80665 / 1000.0
                    * wall["thickness_m"] * shell_area
                )
                mesh_info["self_weight_kN"] += shell_weight
                for node_id in (n1, n2, n3, n4):
                    mesh_info["node_self_weight_kN"][node_id] = (
                        mesh_info["node_self_weight_kN"].get(node_id, 0.0)
                        + shell_weight / 4.0
                    )

        mesh_info["shell_count"] += created_shells

        edge_node_ids = set()
        for row in wall_nodes:
            edge_node_ids.add(row[0])
            edge_node_ids.add(row[-1])

        connected = 0
        for mesh_nid in edge_node_ids:
            mx = ops.nodeCoord(mesh_nid, 1)
            my = ops.nodeCoord(mesh_nid, 2)
            mz = ops.nodeCoord(mesh_nid, 3)
            best_dist = WALL_CONNECT_THRESHOLD_M
            best_frame = None
            for fn in frame_nodes:
                if abs(fn["z_m"] - mz) > 1e-6:
                    continue
                d = ((fn["x_m"] - mx) ** 2 + (fn["y_m"] - my) ** 2) ** 0.5
                if d < best_dist:
                    best_dist = d
                    best_frame = fn["id"]
            if best_frame is not None and best_frame != mesh_nid:
                try:
                    ops.equalDOF(best_frame, mesh_nid, 1, 2, 3, 4, 5, 6)
                    connected += 1
                except Exception:
                    pass

        base_diaphragm_connection = False
        if z0 > 1e-6 and connected == 0:
            floor_nodes = [node for node in frame_nodes
                           if abs(node["z_m"] - wall_levels[0]) < 1e-6]
            if floor_nodes:
                master = min(floor_nodes, key=lambda node: node["id"])["id"]
                for node_id in wall_nodes[0]:
                    if node_id == master:
                        continue
                    try:
                        ops.equalDOF(master, node_id, 1, 2, 3, 4, 5, 6)
                        connected += 1
                    except Exception:
                        pass
                base_diaphragm_connection = connected > 0

        mesh_info["walls"].append({
            "id": wall["id"], "thickness_m": wall["thickness_m"],
            "nh": nh, "nv": nv, "shell_elements": created_shells,
            "edge_nodes_connected": connected,
            "base_diaphragm_connection": base_diaphragm_connection,
            "node_ids": [nid for row in wall_nodes for nid in row],
        })

    mesh_info["mesh_node_count"] = next_mesh_node - WALL_MESH_NODE_BASE
    return mesh_info


def apply_wall_self_weight(wall_mesh):
    """Apply the wall self-weight as vertical nodal loads."""
    node_weights = wall_mesh.get("node_self_weight_kN", {})
    if not node_weights:
        return
    ops.timeSeries("Linear", 3)
    ops.pattern("Plain", 3, 3)
    for node_id, weight in node_weights.items():
        ops.load(node_id, 0.0, 0.0, -weight, 0.0, 0.0, 0.0)


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
    wall_total = data.get("wall_mesh", {}).get("self_weight_kN", 0.0)
    applied_total = dead_total + live_total + wall_total
    residual = reaction_z - applied_total
    print(f"Carga muerta aplicada: {dead_total:.6f} kN")
    print(f"Peso propio de muros: {wall_total:.6f} kN")
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
    wm = model.get("wall_mesh", {})
    print(f"Modelo 3D creado: {len(model['nodes'])} nodos, {len(model['elements'])} elementos y {len(model.get('slabs', []))} losas")
    print(f"Muros malla: {wm.get('wall_count', 0)} muros, {wm.get('shell_count', 0)} ShellMITC4, {wm.get('mesh_node_count', 0)} nodos de malla")
    print(f"Fuente de geometria: {model['source']}")
    analyze_gravity(model)
    print("Las losas se usan solo para calcular areas tributarias; las cargas se aplican a las vigas.")
