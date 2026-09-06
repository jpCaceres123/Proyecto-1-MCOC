"""Convierte Cargas_losas.txt a un esquema JSON legible por los generadores."""

from pathlib import Path
import json
import re


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Cargas_losas.txt"
OUTPUT = ROOT / "data" / "cargas_losas.json"
COORDINATE = re.compile(r"\(\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)(?:\s*,\s*(-?\d+(?:\.\d+)?))?\s*\)")
LOAD = re.compile(r"PM\.?\s*ADIC\.?\s*=\s*(-?\d+(?:\.\d+)?)\s*Kg/m\^2.*SC\s*=\s*(-?\d+(?:\.\d+)?)\s*Kg/m\^2", re.I)


def coordinates(line):
    return [[float(x), float(y)] for x, y, _ in COORDINATE.findall(line)]


def parse_load(line):
    match = LOAD.search(line.replace("\t", " "))
    if match:
        return {"pm_adic_kg_m2": float(match.group(1)), "sc_kg_m2": float(match.group(2))}
    pm = re.search(r"PM\.?\s*ADIC\.?\s*=\s*(-?\d+(?:\.\d+)?)", line, re.I)
    sc = re.search(r"SC\s*=\s*(-?\d+(?:\.\d+)?)", line, re.I)
    return {"pm_adic_kg_m2": float(pm.group(1)) if pm else None, "sc_kg_m2": float(sc.group(1)) if sc else None}


def main():
    lines = SOURCE.read_text(encoding="utf-8").splitlines()
    levels = []
    current_level = None
    current_zone = None
    current_block = None
    in_lt2 = False
    lt2_zone = {"id": "LT2", "geometries": []}

    def add_block():
        nonlocal current_block
        if current_block and current_block["points"]:
            target = lt2_zone if in_lt2 else (current_zone if current_zone is not None else current_level["zones"][-1])
            target["geometries"].append(current_block)
        current_block = None

    for line in lines:
        level_match = re.search(r"Nivel:\s*z\s*=\s*(-?\d+(?:\.\d+)?)", line, re.I)
        if level_match:
            add_block()
            current_level = {"z_m": float(level_match.group(1)), "zones": []}
            levels.append(current_level)
            current_zone = None
            in_lt2 = False
            continue
        if "LT2" in line.upper():
            add_block()
            in_lt2 = True
            current_zone = lt2_zone
            continue
        zone_match = re.match(r"\s*Zona\s+(\d+):", line, re.I)
        if zone_match and current_level:
            add_block()
            current_zone = {"id": f"Zona {zone_match.group(1)}", "geometries": []}
            current_level["zones"].append(current_zone)
            continue
        if "Pasada" in line:
            add_block()
            current_block = {"type": "void", "id": line.strip(), "points": []}
            continue
        if "PM" in line.upper() or re.search(r"\bSC\s*=", line, re.I):
            load = parse_load(line)
            target = current_zone if current_zone is not None else (current_level["zones"][-1] if current_level else lt2_zone)
            if load["pm_adic_kg_m2"] is not None:
                target["pm_adic_kg_m2"] = load["pm_adic_kg_m2"]
            if load["sc_kg_m2"] is not None:
                target["sc_kg_m2"] = load["sc_kg_m2"]
            continue
        label = line.strip().lower()
        if label.startswith("polígono") or label.startswith("poligono"):
            add_block()
            current_block = {"type": "polygon", "id": line.strip(), "points": []}
            continue
        if label.startswith("vacío") or label.startswith("vacio") or label.startswith("con un espacio"):
            add_block()
            current_block = {"type": "void", "id": line.strip(), "points": []}
            continue
        points = coordinates(line)
        if points and current_block is not None:
            current_block["points"].extend(points)

    add_block()
    for level in levels:
        for zone in level["zones"]:
            for key in ("pm_adic", "sc"):
                value = zone.get(f"{key}_kg_m2")
                zone[f"{key}_kn_m2"] = round(value * 9.80665 / 1000.0, 6) if value is not None else None
    for key in ("pm_adic", "sc"):
        value = lt2_zone.get(f"{key}_kg_m2")
        lt2_zone[f"{key}_kn_m2"] = round(value * 9.80665 / 1000.0, 6) if value is not None else None
    result = {
        "source": "Cargas_losas.txt",
        "units": {"length": "m", "surface_load": "kg/m2", "model_load": "kN/m2"},
        "gravity_conversion": 9.80665 / 1000.0,
        "slab": {"thickness_m": 0.15, "density_kg_m3": 2500.0},
        "levels": levels,
        "lt2": lt2_zone,
        "lt2_load_cases": [
            {"geometry_ids": ["LT2 A", "LT2 B", "LT2 C"], "pm_adic_kg_m2": 260.0, "sc_kg_m2": 500.0,
             "levels_z_m": [3.96, 7.92, 11.88, 15.84]},
            {"geometry_ids": ["LT2 D"], "pm_adic_kg_m2": 260.0, "sc_kg_m2": 300.0,
             "levels_z_m": [3.96, 7.92, 11.88, 15.84]},
            {"geometry_ids": ["LT2 E"], "pm_adic_kg_m2": 260.0, "sc_kg_m2": 200.0,
             "levels_z_m": [3.96, 7.92, 11.88, 15.84]},
            {"geometry_ids": ["Piso 4 LT2"], "pm_adic_kg_m2": 200.0, "sc_kg_m2": 200.0,
             "levels_z_m": [19.8]},
        ],
        "lt2_levels": [3.96, 7.92, 11.88, 15.84],
    }
    OUTPUT.write_text(json.dumps(result, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    print(f"JSON creado: {OUTPUT}")


if __name__ == "__main__":
    main()
