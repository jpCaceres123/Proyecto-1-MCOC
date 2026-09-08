"""Verificaciones independientes del modelo generado de P1L2."""

from pathlib import Path
import json
import sys
from collections import defaultdict


ROOT = Path(__file__).resolve().parents[1]
MODEL = ROOT / "outputs" / "modelo_3d_manual.json"
LOADS = ROOT / "data" / "cargas_losas.json"
GEOMETRY = ROOT / "data" / "geometria_manual.json"
KG_TO_KN = 9.80665 / 1000.0


def canonical_zone_id(zone_id):
    """Use source names when comparing generated load aliases."""
    if zone_id.startswith("LT1_superior"):
        return "Zona 5"
    if zone_id.startswith("LT2_E_superior"):
        return "LT2 E"
    if zone_id == "LT2 piso 4":
        return "Piso 4 LT2"
    return zone_id


def point_in_polygon(point, polygon):
    x, y = point
    inside = False
    for index, current in enumerate(polygon):
        previous = polygon[index - 1]
        if (current[1] > y) != (previous[1] > y):
            crossing_x = ((previous[0] - current[0]) * (y - current[1])
                          / (previous[1] - current[1]) + current[0])
            if x < crossing_x:
                inside = not inside
    return inside


def zone_contains(zone, point):
    outer = any(
        geometry.get("type") == "polygon"
        and point_in_polygon(point, geometry.get("points", []))
        for geometry in zone.get("geometries", [])
    )
    void = any(
        geometry.get("type") == "void"
        and point_in_polygon(point, geometry.get("points", []))
        for geometry in zone.get("geometries", [])
    )
    return outer and not void


def lt2_zone(geometries, z, point):
    geometries = geometries if abs(z - 19.8) < 1e-6 else geometries[:6]
    if any(
        geometry.get("type") == "void"
        and point_in_polygon(point, geometry.get("points", []))
        for geometry in geometries
    ):
        return None
    if abs(z - 19.8) < 1e-6:
        if len(geometries) < 7 or geometries[6].get("type") != "polygon":
            return None
        if point_in_polygon(point, geometries[6]["points"]):
            return {"id": "Piso 4 LT2", "pm_adic_kg_m2": 200.0, "sc_kg_m2": 200.0}
        return None
    if len(geometries) < 6:
        return None
    if point_in_polygon(point, geometries[4]["points"]):
        return {"id": "LT2 D", "pm_adic_kg_m2": 260.0, "sc_kg_m2": 300.0}
    for index, name in ((0, "LT2 A"), (1, "LT2 B"), (3, "LT2 C"), (5, "LT2 E")):
        geometry = geometries[index]
        if geometry.get("type") == "polygon" and point_in_polygon(point, geometry["points"]):
            return {"id": name, "pm_adic_kg_m2": 260.0,
                    "sc_kg_m2": 200.0 if name == "LT2 E" else 500.0}
    return None


def expected_floor_loads(model, loads, geometry, z):
    level = next((item for item in loads.get("levels", [])
                  if abs(item["z_m"] - z) < 1e-6), None)
    lt2 = loads.get("lt2", {}).get("geometries", []) if (
        abs(z - 19.8) < 1e-6 or z in loads.get("lt2_levels", [])
    ) else []
    manual_regions = []
    for region in geometry.get("cantilever_regions", []):
        if abs(region["z_m"] - z) < 1e-6:
            manual_regions.append({
                "id": region.get("source_zone", region["id"]),
                "pm_adic_kg_m2": region.get("pm_adic_kg_m2", 0.0),
                "sc_kg_m2": region.get("sc_kg_m2", 0.0),
                "geometries": [{"type": "polygon", "points": [
                    [region["x_min_m"], region["y_min_m"]],
                    [region["x_max_m"], region["y_min_m"]],
                    [region["x_max_m"], region["y_max_m"]],
                    [region["x_min_m"], region["y_max_m"]],
                ]}],
            })
    geometries = [geometry for zone in (level or {}).get("zones", [])
                  for geometry in zone.get("geometries", [])] + lt2
    geometries += [geometry_item for zone in manual_regions
                   for geometry_item in zone["geometries"]]
    for slab in model.get("slabs", []):
        if abs(slab["z_m"] - z) < 1e-6:
            geometries.extend({"type": "polygon", "points": [
                [point["x_m"], point["y_m"]] for point in slab["coordinates"]
            ]} for _ in [0])
    xs = sorted({round(point[0], 8) for geometry in geometries
                 for point in geometry.get("points", [])})
    ys = sorted({round(point[1], 8) for geometry in geometries
                 for point in geometry.get("points", [])})
    result = {"area_m2": 0.0, "dead_load_kN": 0.0, "live_load_kN": 0.0,
              "by_zone": defaultdict(lambda: {"area_m2": 0.0,
                                                "dead_load_kN": 0.0,
                                                "live_load_kN": 0.0})}
    slab_weight = model["slabs"][0]["self_weight_kN_m2"]
    for xa, xb in zip(xs, xs[1:]):
        for ya, yb in zip(ys, ys[1:]):
            point = ((xa + xb) / 2.0, (ya + yb) / 2.0)
            zone = None
            manual_matches = [item for item in manual_regions
                              if zone_contains(item, point)]
            if len(manual_matches) == 1:
                zone = manual_matches[0]
            if level:
                if zone is None:
                    matches = [item for item in level.get("zones", [])
                               if zone_contains(item, point)]
                    inside_level_polygon = any(
                        geometry.get("type") == "polygon"
                        and point_in_polygon(point, geometry.get("points", []))
                        for item in level.get("zones", [])
                        for geometry in item.get("geometries", [])
                    )
                    if len(matches) == 1:
                        zone = matches[0]
                    elif not inside_level_polygon:
                        zone = lt2_zone(lt2, z, point)
            else:
                zone = lt2_zone(lt2, z, point)
            if not zone:
                continue
            area = (xb - xa) * (yb - ya)
            q_g = slab_weight + zone.get("pm_adic_kg_m2", 0.0) * KG_TO_KN
            q_sc = zone.get("sc_kg_m2", 0.0) * KG_TO_KN
            result["area_m2"] += area
            result["dead_load_kN"] += area * q_g
            result["live_load_kN"] += area * q_sc
            zone_id = canonical_zone_id(zone["id"])
            result["by_zone"][zone_id]["area_m2"] += area
            result["by_zone"][zone_id]["dead_load_kN"] += area * q_g
            result["by_zone"][zone_id]["live_load_kN"] += area * q_sc
    return result


def main():
    model = json.loads(MODEL.read_text(encoding="utf-8"))
    loads = json.loads(LOADS.read_text(encoding="utf-8"))
    geometry = json.loads(GEOMETRY.read_text(encoding="utf-8"))
    levels = sorted({round(item["z_m"], 6) for item in model.get("slabs", [])})
    failures = []

    print("Empotramientos inferiores solicitados:")
    for axis in ("H", "I", "J"):
        axis_nodes = [node for node in model["nodes"] if node.get("axis") == axis]
        if not axis_nodes:
            failures.append(f"eje {axis} inexistente")
            continue
        z_min = min(node["z_m"] for node in axis_nodes)
        lower_nodes = [node for node in axis_nodes if abs(node["z_m"] - z_min) < 1e-6]
        fixed = sum(bool(node.get("restraint")) for node in lower_nodes)
        print(f"  Eje {axis}: {fixed}/{len(lower_nodes)} nodos inferiores empotrados en Z={z_min:.2f} m")
        if fixed != len(lower_nodes):
            failures.append(f"empotramientos del eje {axis}")

    print("Columnas tubulares de acero en puntas de voladizo:")
    node_by_id = {node["id"]: node for node in model["nodes"]}
    steel_elements = [element for element in model["elements"]
                      if element["type"] == "STEEL_COLUMN_SHS300x20"]
    expected_steel = geometry.get("steel_columns", [])
    if len(steel_elements) != len(expected_steel):
        failures.append("cantidad de columnas de acero")
    for expected in expected_steel:
        matches = []
        for element in steel_elements:
            a, b = node_by_id[element["i"]], node_by_id[element["j"]]
            if (abs(a["x_m"] - expected["x_m"]) < 1e-6
                    and abs(a["y_m"] - expected["y_m"]) < 1e-6
                    and abs(a["z_m"] - expected["z_i_m"]) < 1e-6
                    and abs(b["x_m"] - expected["x_m"]) < 1e-6
                    and abs(b["y_m"] - expected["y_m"]) < 1e-6
                    and abs(b["z_m"] - expected["z_j_m"]) < 1e-6):
                matches.append(element)
        print(f"  {expected['id']} eje {expected['axis']}: "
              f"{'OK' if len(matches) == 1 else 'REVISAR'}")
        if len(matches) != 1:
            failures.append(f"columna de acero {expected['id']}")
    section_steel = model.get("section_steel_columns", {})
    expected_area = 0.30 ** 2 - 0.26 ** 2
    if abs(section_steel.get("A_m2", 0.0) - expected_area) > 1e-12:
        failures.append("area SHS 300x300x20")

    max_area_error = max((abs(item.get("area_check_m2", 0.0))
                          for item in model.get("slabs", [])), default=0.0)
    max_partition_error = max((abs(item.get("global_partition", {})
                                     .get("loaded_area_check_m2", 0.0))
                               for item in model.get("slabs", [])
                               if item.get("global_partition")), default=0.0)
    # The LT2 joint/void intersection leaves a sub-centimetre-scale area
    # residual after polygon clipping; keep it below 0.01 m2.
    if max_area_error > 1.0e-2:
        failures.append("areas tributarias")
    if max_partition_error > 1.0e-2:
        failures.append("particiones de losa")
    if model.get("unassigned_load_slabs"):
        failures.append("losas sin zona de carga")

    print(f"Error maximo de area tributaria: {max_area_error:.3e} m2")
    print(f"Error maximo de particion cargada: {max_partition_error:.3e} m2")
    print(f"Losas sin zona de carga: {len(model.get('unassigned_load_slabs', []))}")
    print("Carga original vs carga transferida por nivel:")
    for z in levels:
        expected = expected_floor_loads(model, loads, geometry, z)
        all_load_cases = model.get("beam_load_cases", []) + model.get("wall_load_cases", [])
        applied = {
            "dead_load_kN": sum(item["dead_load_kN"] for item in all_load_cases
                                 if abs(item["level_z_m"] - z) < 1e-6),
            "live_load_kN": sum(item["live_load_kN"] for item in all_load_cases
                                if abs(item["level_z_m"] - z) < 1e-6),
        }
        dead_error = applied["dead_load_kN"] - expected["dead_load_kN"]
        live_error = applied["live_load_kN"] - expected["live_load_kN"]
        print(f"  z={z:.2f}: G origen={expected['dead_load_kN']:.3f} kN, "
              f"transferida={applied['dead_load_kN']:.3f} kN, "
              f"error={dead_error:.3f} kN; SC error={live_error:.3f} kN")
        applied_by_zone = defaultdict(lambda: {"dead_load_kN": 0.0, "live_load_kN": 0.0})
        for item in all_load_cases:
            if abs(item["level_z_m"] - z) < 1e-6:
                zone_id = canonical_zone_id(item["zone"])
                applied_by_zone[zone_id]["dead_load_kN"] += item["dead_load_kN"]
                applied_by_zone[zone_id]["live_load_kN"] += item["live_load_kN"]
        for zone in sorted(set(expected["by_zone"]) | set(applied_by_zone)):
            source = expected["by_zone"].get(zone, {})
            transferred = applied_by_zone.get(zone, {})
            print(f"    {zone}: G {source.get('dead_load_kN', 0.0) - transferred.get('dead_load_kN', 0.0):.3f} kN, "
                  f"SC {source.get('live_load_kN', 0.0) - transferred.get('live_load_kN', 0.0):.3f} kN")
        if abs(dead_error) > 1.0e-3 or abs(live_error) > 1.0e-3:
            failures.append(f"cargas del nivel {z}")

    print(f"Resultado: {'REVISAR' if failures else 'OK'}")
    if failures:
        print("Pendientes: " + ", ".join(dict.fromkeys(failures)))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
