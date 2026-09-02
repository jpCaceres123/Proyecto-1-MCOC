"""Genera nodos, elementos, Excel y CSV desde geometria_manual.json."""

from pathlib import Path
import json

from openpyxl import Workbook


ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "data"
OUTPUTS = ROOT / "outputs"
MODEL = OUTPUTS / "modelo_3d_manual.json"
EXCEL = OUTPUTS / "nodos_modelo_manual.xlsx"
CSV = ROOT / "UnityVisualization" / "Assets" / "Resources" / "model_3d.csv"


def main():
    config = json.loads((DATA / "geometria_manual.json").read_text(encoding="utf-8"))
    nodes = []
    by_level = {z: [] for z in config["levels_m"]}
    for k, z in enumerate(config["levels_m"]):
        for y_index, y in enumerate(config["y_axes_m"]):
            for axis in config["column_axes"]:
                if z < axis["z_inicio_m"]:
                    continue
                tag = 100000 * k + 1000 * y_index + int(axis["x_m"] * 10) + 1
                node = {"id": tag, "level": k, "axis": axis["eje"],
                        "x_m": axis["x_m"], "y_m": y, "z_m": z,
                        "restraint": k == 0, "status": "MANUAL"}
                nodes.append(node)
                by_level[z].append(node)

        # Some columns occupy only selected intersections of the extended grid.
        # They are declared explicitly instead of being created at every Y row.
        for point_index, point in enumerate(config.get("column_points", [])):
            if z < point.get("z_inicio_m", 0.0):
                continue
            tag = 700000 + 1000 * k + point_index + 1
            node = {"id": tag, "level": k, "axis": point["id"],
                    "x_m": point["x_m"], "y_m": point["y_m"], "z_m": z,
                    "restraint": k == 0, "status": "MANUAL"}
            nodes.append(node)
            by_level[z].append(node)

    manual_beams = list(config.get("manual_beams", []))
    for repeat in config.get("repeat_manual_beams", []):
        def allowed(beam):
            if not repeat.get("exclude_negative_y", False):
                return True
            return all(beam.get(key, 0.0) >= 0.0 for key in ("y_m", "y_i_m", "y_j_m") if key in beam)

        manual_beams.extend(
            [dict(beam, z_m=repeat["to_z_m"])
             for beam in manual_beams
             if round(beam["z_m"], 3) == round(repeat["from_z_m"], 3)
             and (not repeat.get("source_group") or beam.get("group") == repeat["source_group"])
             and beam.get("group") != repeat.get("exclude_group")
             and allowed(beam)]
        )

    elements = []
    eid = 1
    levels = config["levels_m"]
    for z0, z1 in zip(levels, levels[1:]):
        low = {(n["axis"], round(n["y_m"], 3)): n for n in by_level[z0]}
        high = {(n["axis"], round(n["y_m"], 3)): n for n in by_level[z1]}
        for key, top in high.items():
            if key in low:
                elements.append({"id": eid, "type": "COLUMN", "i": low[key]["id"], "j": top["id"], "status": "MANUAL"})
                eid += 1

    manual_z = {round(b["z_m"], 3) for b in manual_beams}
    node_at = {(n["axis"], round(n["y_m"], 3), round(n["z_m"], 3)): n for n in nodes}
    node_by_xyz = {(round(n["x_m"], 3), round(n["y_m"], 3), round(n["z_m"], 3)): n for n in nodes}
    next_manual_node = 900000

    def ensure_beam_node(x, y, z):
        nonlocal next_manual_node
        key = (round(x, 3), round(y, 3), round(z, 3))
        if key not in node_by_xyz:
            node = {"id": next_manual_node, "level": levels.index(z), "axis": "BEAM_MANUAL",
                    "x_m": x, "y_m": y, "z_m": z, "restraint": False, "status": "MANUAL_BEAM_NODE"}
            next_manual_node += 1
            nodes.append(node)
            node_by_xyz[key] = node
        return node_by_xyz[key]

    for beam in manual_beams:
        z = round(beam["z_m"], 3)
        if "x_i_m" in beam:
            a = ensure_beam_node(beam["x_i_m"], beam["y_i_m"], z)
            b = ensure_beam_node(beam["x_j_m"], beam["y_j_m"], z)
        elif beam["direction"] == "X":
            a = node_at[(beam["axis_i"], round(beam["y_m"], 3), z)]
            b = node_at[(beam["axis_j"], round(beam["y_m"], 3), z)]
        else:
            x = next(axis["x_m"] for axis in config["column_axes"] if axis["eje"] == beam["axis"])
            a = node_at.get((beam["axis"], round(beam["y_i_m"], 3), z),
                            ensure_beam_node(x, beam["y_i_m"], z))
            b = node_at.get((beam["axis"], round(beam["y_j_m"], 3), z),
                            ensure_beam_node(x, beam["y_j_m"], z))
        elements.append({"id": eid, "type": beam.get("member_type", "BEAM_" + beam["direction"]), "direction": beam["direction"], "i": a["id"], "j": b["id"], "status": "MANUAL"})
        eid += 1

    # Provisional beams: connect adjacent listed column axes and Y rows where no manual layout exists.
    for z in levels[1:]:
        if round(z, 3) in manual_z:
            continue
        current = by_level[z]
        for y in config["y_axes_m"]:
            row = sorted((n for n in current if abs(n["y_m"] - y) < 1e-9), key=lambda n: n["x_m"])
            for a, b in zip(row, row[1:]):
                elements.append({"id": eid, "type": "BEAM_X", "direction": "X", "i": a["id"], "j": b["id"], "status": "PROVISIONAL_BEAM"})
                eid += 1
        for axis in config["column_axes"]:
            col = sorted((n for n in current if n["axis"] == axis["eje"]), key=lambda n: n["y_m"])
            for a, b in zip(col, col[1:]):
                elements.append({"id": eid, "type": "BEAM_Y", "direction": "Y", "i": a["id"], "j": b["id"], "status": "PROVISIONAL_BEAM"})
                eid += 1

    def covered(intervals, start, end):
        current = start
        for a, b in sorted((min(a, b), max(a, b)) for a, b in intervals):
            if b <= current + 1e-6:
                continue
            if a > current + 1e-6:
                return False
            current = max(current, b)
            if current >= end - 1e-6:
                return True
        return current >= end - 1e-6

    def covered_with_ids(intervals, start, end):
        current = start
        ids = []
        for a, b, element_id in sorted((min(a, b), max(a, b), element_id) for a, b, element_id in intervals):
            if b <= current + 1e-6:
                continue
            if a > current + 1e-6:
                return []
            ids.append(element_id)
            current = max(current, b)
            if current >= end - 1e-6:
                return ids
        return ids if current >= end - 1e-6 else []

    def tributary_loads_for_rectangle(lx, ly, edges):
        area = lx * ly
        ratio = max(lx, ly) / min(lx, ly)
        loads = []

        def add(edge, tributary_area, distribution, w_start, w_max, w_end):
            loads.append({
                "edge": edge,
                "beam_ids": edges[edge],
                "tributary_area_m2": round(tributary_area, 6),
                "distribution": distribution,
                "w_start_kN_m": round(w_start, 6),
                "w_max_kN_m": round(w_max, 6),
                "w_end_kN_m": round(w_end, 6),
                "total_load_kN": round(3.68 * tributary_area, 6),
            })

        if ratio >= 2.0:
            if lx >= ly:
                w = 3.68 * ly / 2.0
                add("bottom", area / 2.0, "uniform", w, w, w)
                add("top", area / 2.0, "uniform", w, w, w)
            else:
                w = 3.68 * lx / 2.0
                add("left", area / 2.0, "uniform", w, w, w)
                add("right", area / 2.0, "uniform", w, w, w)
        elif lx >= ly:
            short = ly
            long = lx
            short_edge_area = short * short / 4.0
            long_edge_area = short * (2.0 * long - short) / 4.0
            w_max = 3.68 * short / 2.0
            add("left", short_edge_area, "triangular", 0.0, w_max, 0.0)
            add("right", short_edge_area, "triangular", 0.0, w_max, 0.0)
            add("bottom", long_edge_area, "trapezoidal", 0.0, w_max, 0.0)
            add("top", long_edge_area, "trapezoidal", 0.0, w_max, 0.0)
        else:
            short = lx
            long = ly
            short_edge_area = short * short / 4.0
            long_edge_area = short * (2.0 * long - short) / 4.0
            w_max = 3.68 * short / 2.0
            add("bottom", short_edge_area, "triangular", 0.0, w_max, 0.0)
            add("top", short_edge_area, "triangular", 0.0, w_max, 0.0)
            add("left", long_edge_area, "trapezoidal", 0.0, w_max, 0.0)
            add("right", long_edge_area, "trapezoidal", 0.0, w_max, 0.0)

        return loads

    # Detect every elementary rectangular face bounded by four coplanar beams.
    slab_faces = []
    for z in levels[1:]:
        horizontal = {}
        vertical = {}
        x_breaks = set()
        y_breaks = set()
        for element in elements:
            if not element["type"].startswith("BEAM"):
                continue
            a = next(n for n in nodes if n["id"] == element["i"])
            b = next(n for n in nodes if n["id"] == element["j"])
            if abs(a["z_m"] - z) > 1e-6 or abs(b["z_m"] - z) > 1e-6:
                continue
            x_breaks.update((round(a["x_m"], 3), round(b["x_m"], 3)))
            y_breaks.update((round(a["y_m"], 3), round(b["y_m"], 3)))
            if abs(a["y_m"] - b["y_m"]) < 1e-6:
                horizontal.setdefault(round(a["y_m"], 6), []).append((a["x_m"], b["x_m"], element["id"]))
            elif abs(a["x_m"] - b["x_m"]) < 1e-6:
                vertical.setdefault(round(a["x_m"], 6), []).append((a["y_m"], b["y_m"], element["id"]))
        xs = sorted(x_breaks)
        ys = sorted(y_breaks)
        for i, x0 in enumerate(xs):
            for x1 in xs[i + 1:]:
                if x1 - x0 < 0.05:
                    continue
                for j, y0 in enumerate(ys):
                    for y1 in ys[j + 1:]:
                        if y1 - y0 < 0.05:
                            continue
                        edges = {
                            "bottom": covered_with_ids(horizontal.get(round(y0, 6), []), x0, x1),
                            "top": covered_with_ids(horizontal.get(round(y1, 6), []), x0, x1),
                            "left": covered_with_ids(vertical.get(round(x0, 6), []), y0, y1),
                            "right": covered_with_ids(vertical.get(round(x1, 6), []), y0, y1),
                        }
                        if not all(edges.values()):
                            continue
                        has_internal_horizontal = any(
                            y0 + 1e-6 < y < y1 - 1e-6
                            and covered_with_ids(intervals, x0, x1)
                            for y, intervals in horizontal.items()
                        )
                        has_internal_vertical = any(
                            x0 + 1e-6 < x < x1 - 1e-6
                            and covered_with_ids(intervals, y0, y1)
                            for x, intervals in vertical.items()
                        )
                        if has_internal_horizontal or has_internal_vertical:
                            continue
                        slab_faces.append({
                            "z_m": z,
                            "vertices": [(x0, y0), (x1, y0), (x1, y1), (x0, y1)],
                            "edge_beam_ids": edges,
                        })

    slabs = []
    next_slab_node = 800000
    next_slab_element = 1000000
    for face_data in slab_faces:
        z = face_data["z_m"]
        face = face_data["vertices"]
        slab_node_ids = []
        for x, y in face:
            key = (round(x, 3), round(y, 3), round(z, 3))
            if key not in node_by_xyz:
                node = {"id": next_slab_node, "level": levels.index(z), "axis": "SLAB",
                        "x_m": x, "y_m": y, "z_m": z, "restraint": False,
                        "status": "SLAB_NODE"}
                next_slab_node += 1
                nodes.append(node)
                node_by_xyz[key] = node
            slab_node_ids.append(node_by_xyz[key]["id"])
        lx = abs(face[1][0] - face[0][0])
        ly = abs(face[2][1] - face[1][1])
        tributary_loads = tributary_loads_for_rectangle(lx, ly, face_data["edge_beam_ids"])
        area = lx * ly
        assigned_area = sum(load["tributary_area_m2"] for load in tributary_loads)
        slabs.append({"id": next_slab_element, "node_ids": slab_node_ids,
                      "coordinates": [{"x_m": x, "y_m": y, "z_m": z} for x, y in face],
                      "edge_beam_ids": face_data["edge_beam_ids"],
                      "dimensions_m": {"lx": round(lx, 6), "ly": round(ly, 6)},
                      "area_m2": round(area, 6),
                      "thickness_m": config.get("slab_thickness_m", 0.15),
                      "density_kg_m3": 2500.0,
                      "self_weight_kN_m2": 3.68,
                      "tributary_loads": tributary_loads,
                      "area_check_m2": round(assigned_area - area, 6),
                      "z_m": z, "status": "AUTO_CLOSED_BY_BEAMS"})
        next_slab_element += 1

    beam_slab_loads = []
    for slab in slabs:
        for load in slab["tributary_loads"]:
            for beam_id in load["beam_ids"]:
                beam_slab_loads.append({
                    "slab_id": slab["id"],
                    "beam_id": beam_id,
                    "z_m": slab["z_m"],
                    "edge": load["edge"],
                    "tributary_area_m2": load["tributary_area_m2"],
                    "distribution": load["distribution"],
                    "w_start_kN_m": load["w_start_kN_m"],
                    "w_max_kN_m": load["w_max_kN_m"],
                    "w_end_kN_m": load["w_end_kN_m"],
                    "total_load_kN": load["total_load_kN"],
                })

    data = {"source": "geometria_manual.json", "status": "MANUAL_REVIEW",
            "units": "kN-m-s", "nodes": nodes, "elements": elements,
            "walls": config.get("walls", []),
            "slabs": slabs,
            "beam_slab_loads": beam_slab_loads,
            "section_columns": {"A_m2": 0.49, "Iy_m4": 0.020004, "Iz_m4": 0.020004, "J_m4": 0.040008},
            "section_beams": {"A_m2": 0.48, "Iy_m4": 0.0256, "Iz_m4": 0.0144, "J_m4": 0.002},
            "section_small_beams": {"A_m2": 0.135, "Iy_m4": 0.002278, "Iz_m4": 0.000506, "J_m4": 0.0002},
            "section_variable_beams": {"A_m2": 0.21, "Iy_m4": 0.005, "Iz_m4": 0.0015, "J_m4": 0.0004},
            "section_40x60_beams": {"A_m2": 0.24, "Iy_m4": 0.0072, "Iz_m4": 0.0032, "J_m4": 0.0008},
            "material": config["material"], "mass_per_node_t": config["mass_per_node_t"]}
    OUTPUTS.mkdir(parents=True, exist_ok=True)
    MODEL.write_text(json.dumps(data, indent=2), encoding="utf-8")
    CSV.parent.mkdir(parents=True, exist_ok=True)
    lines = ["kind,id,type,i,j,x_m,y_m,z_m,level,status"]
    lines.extend(f'N,{n["id"]},NODE,,,,{n["x_m"]},{n["y_m"]},{n["z_m"]},{n["level"]},{n["status"]}' for n in nodes)
    lines.extend(f'E,{e["id"]},{e["type"]},{e["i"]},{e["j"]},,,,,,{e["status"]}' for e in elements)
    for wall in config.get("walls", []):
        lines.append(
            f'W,{wall["id"]},WALL,{wall["x_i_m"]},{wall["y_i_m"]},{wall["z_i_m"]},'
            f'{wall["x_j_m"]},{wall["y_j_m"]},{wall["z_j_m"]},{wall["thickness_m"]},{wall.get("status", "MANUAL")}'
        )
    for slab in slabs:
        n1, n2, n3, n4 = slab["node_ids"]
        lines.append(f'S,{slab["id"]},{n1},{n2},{n3},{n4},{slab["z_m"]},{slab["thickness_m"]},{slab["status"]}')
    CSV.write_text("\n".join(lines) + "\n", encoding="utf-8")

    workbook = Workbook()
    sheet = workbook.active
    sheet.title = "Nodos_Manual"
    sheet.append(["id", "nivel", "eje", "x_m", "y_m", "z_m", "restriccion", "estado"])
    for n in nodes:
        sheet.append([n["id"], n["level"], n["axis"], n["x_m"], n["y_m"], n["z_m"], "Empotrado" if n["restraint"] else "Libre", n["status"]])
    el_sheet = workbook.create_sheet("Elementos")
    el_sheet.append(["id", "tipo", "nodo_i", "nodo_j", "estado"])
    for e in elements:
        el_sheet.append([e["id"], e["type"], e["i"], e["j"], e["status"]])
    slab_sheet = workbook.create_sheet("Losas")
    slab_sheet.append(["id", "nodo_1", "nodo_2", "nodo_3", "nodo_4", "z_m", "lx_m", "ly_m", "area_m2", "espesor_m", "densidad_kg_m3", "q_kN_m2", "control_area_m2", "estado"])
    for slab in slabs:
        slab_sheet.append([slab["id"], *slab["node_ids"], slab["z_m"],
                           slab["dimensions_m"]["lx"], slab["dimensions_m"]["ly"], slab["area_m2"],
                           slab["thickness_m"], slab["density_kg_m3"], slab["self_weight_kN_m2"],
                           slab["area_check_m2"], slab["status"]])
    load_sheet = workbook.create_sheet("Cargas_Losa")
    load_sheet.append(["losa_id", "viga_id", "z_m", "borde", "area_tributaria_m2", "tipo", "w_inicio_kN_m", "w_max_kN_m", "w_final_kN_m", "carga_total_kN"])
    for load in beam_slab_loads:
        load_sheet.append([load["slab_id"], load["beam_id"], load["z_m"], load["edge"],
                           load["tributary_area_m2"], load["distribution"],
                           load["w_start_kN_m"], load["w_max_kN_m"], load["w_end_kN_m"],
                           load["total_load_kN"]])
    notes = workbook.create_sheet("Supuestos")
    notes.append(["campo", "valor"])
    notes.append(["niveles", "0, 3.96, 7.92, 11.88, 15.84, 19.80 m"])
    notes.append(["ejes Y", "Eje 3 = 0; Eje 2 = 7.25; Eje 1 = 16.15 m"])
    notes.append(["columnas", "Todas 0.70 x 0.70 m; datos ingresados manualmente"])
    notes.append(["vigas", "Cielo 1 subterraneo: 7; Cielos Piso 1 y Piso 2: base repetida; Piso 2 agrega voladizos; Piso 3 conserva la base y agrega su configuracion especial"])
    notes.append(["modelo", "Vigas, columnas, muros y losas cerradas por vigas; sin fundaciones"])
    workbook.save(EXCEL)
    print(f"Modelo manual: {len(nodes)} nodos, {len(elements)} elementos; Excel: {EXCEL.name}")


if __name__ == "__main__":
    main()
