"""Verifica demandas P-M de muros y columnas contra sus envolventes.

Unidades: kN y kN m. La comprobacion de columna usa la envolvente nominal
exportada para la seccion de hormigon y toma el peor momento de los dos ejes.
"""
from pathlib import Path
import csv
import json
import math


ROOT = Path(__file__).resolve().parents[1]
RESULTS = ROOT / "results"


def inside_polygon(p, m, points):
    """Return whether (M, P) lies inside a closed P-M polygon."""
    if not points:
        return False
    x, y = float(m), float(p)
    polygon = [(float(item["M_kNm"]), float(item["P_kN"])) for item in points]
    inside = False
    for i, (xi, yi) in enumerate(polygon):
        xj, yj = polygon[i - 1]
        crosses = (yi > y) != (yj > y)
        if crosses and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            inside = not inside
    return inside


def wall_polygon(rows):
    positive = sorted((row for row in rows if row["ramal"] == "+principal"),
                      key=lambda row: int(row["indice"]))
    negative = sorted((row for row in rows if row["ramal"] == "-principal"),
                      key=lambda row: int(row["indice"]), reverse=True)
    return positive + negative


def read_wall_checks():
    envelopes = {}
    with (RESULTS / "PM_muros_envolvente.csv").open(encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            key = (int(row["wall_id"]), int(row["segmento"]))
            envelopes.setdefault(key, []).append(row)

    checks = []
    with (RESULTS / "demanda_muros.csv").open(encoding="utf-8-sig", newline="") as stream:
        for demand in csv.DictReader(stream):
            if demand["caso"] not in ("G", "Q", "EX", "EY", "R"):
                continue
            key = (int(demand["panel_id"]), 1)
            polygon = wall_polygon(envelopes.get(key, []))
            p = float(demand["P_compresion_kN"])
            m = float(demand["M_principal_kNm"])
            checks.append({
                "tipo": "MURO",
                "elemento": int(demand["panel_id"]),
                "muro_origen": int(demand["source_wall_id"]),
                "piso": int(demand["floor"]),
                "caso": demand["caso"],
                "P_kN": p,
                "M_kNm": m,
                "cumple": inside_polygon(p, m, polygon),
            })
    return checks


def read_column_checks():
    summary = json.loads((RESULTS / "resumen_capacidad.json").read_text(encoding="utf-8"))
    points = summary["puntos"]
    model = json.loads((RESULTS / "modelo_3d_manual.json").read_text(encoding="utf-8"))
    column_ids = {str(element["id"]) for element in model["elements"] if element["type"] == "COLUMN"}
    checks = []
    for case in ("G", "Q", "EX", "EY", "R"):
        forces = json.loads((RESULTS / f"{case}_fuerzas_locales.json").read_text(encoding="utf-8"))
        for element_id in sorted(column_ids, key=int):
            values = forces[element_id]
            for end, offset in (("i", 0), ("j", 6)):
                # OpenSees reports the axial action with opposite signs at
                # the two ends of a member; compression is positive here.
                p = float(values[offset]) if end == "i" else -float(values[offset])
                m = max(abs(float(values[offset + 4])), abs(float(values[offset + 5])))
                checks.append({
                    "tipo": "COLUMNA",
                    "elemento": int(element_id),
                    "extremo": end,
                    "caso": case,
                    "P_kN": p,
                    "M_kNm": m,
                    "cumple": inside_polygon(p, m, points),
                })
    return checks


def main():
    checks = read_wall_checks() + read_column_checks()
    with (RESULTS / "verificacion_demanda_capacidad.csv").open("w", newline="", encoding="utf-8") as stream:
        fields = sorted({key for row in checks for key in row})
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(checks)
    summary = {
        "estado": "OK" if all(row["cumple"] for row in checks) else "REVISAR",
        "total": len(checks),
        "cumplen": sum(row["cumple"] for row in checks),
        "fuera_capacidad": sum(not row["cumple"] for row in checks),
        "muros": sum(row["tipo"] == "MURO" for row in checks),
        "columnas": sum(row["tipo"] == "COLUMNA" for row in checks),
    }
    (RESULTS / "verificacion_demanda_capacidad.json").write_text(
        json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(summary, indent=2, ensure_ascii=False))
    for row in checks:
        if not row["cumple"]:
            print(row)


if __name__ == "__main__":
    main()
