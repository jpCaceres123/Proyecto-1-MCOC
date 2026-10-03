"""Ilustra la identidad numérica de una barra en OpenSees, Unity y el APK."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
AR = ROOT / "Edificio/visualization/android-ar/app/src/main/assets/structural_data.json"
UNITY = ROOT / "Edificio/visualization/unity/UnityVisualization/Assets/Resources/semana4_resultados.json"
ANALYSIS = ROOT / "Edificio/results/R_diagramas_barras.json"
ar = json.loads(AR.read_text(encoding="utf-8"))
unity = json.loads(UNITY.read_text(encoding="utf-8"))
solved = json.loads(ANALYSIS.read_text(encoding="utf-8"))
member = next(m for m in ar["members"] if m["id"] == 246)
unity_r = next(c for c in unity["cases"] if c["name"] == "R")
unity_bar = next(b for b in unity_r["bars"] if b["id"] == 246)
ar_bar = member["cases"]["R"]
raw = solved["246"]
for field in ("s", "n", "vy", "vz", "t", "my", "mz"):
    assert ar_bar[field] == unity_bar[field] == raw[field], f"Barra 246: {field} distinto"
assert (member["i"], member["j"]) == (next(b for b in unity["bars"] if b["id"] == 246)["i"],
                                      next(b for b in unity["bars"] if b["id"] == 246)["j"])
station = ar_bar["s"].index(0.5)
moment = ar_bar["my"][station]
shear = ar_bar["vz"][station]
area = member["load_info"]["tributary_area_m2"]


def font(size, bold=False):
    names = (["C:/Windows/Fonts/arialbd.ttf", "DejaVuSans-Bold.ttf"] if bold else
             ["C:/Windows/Fonts/arial.ttf", "DejaVuSans.ttf"])
    for name in names:
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


im = Image.new("RGB", (1560, 715), "#f2f6fa")
d = ImageDraw.Draw(im)
title, big, text, small = font(35, True), font(25, True), font(22), font(19)
navy, teal, blue, orange, gray = "#102438", "#008c83", "#316ac1", "#bd6428", "#53697c"


def write(x, y, value, color=navy, face=text):
    d.text((x, y), value, font=face, fill=color)


write(42, 27, "Un mismo ID y un mismo resultado en tres contratos", navy, title)
write(45, 80, "Viga 246 · caso R · estación x/L = 0,50 · valores firmados de ejes locales OpenSees", gray)

cards = [(38, 145, 497, 505, "1 · Cálculo OpenSees", "Edificio/results/R_diagramas_barras.json"),
         (548, 145, 1007, 505, "2 · Contrato del visor Unity", "Assets/Resources/semana4_resultados.json"),
         (1058, 145, 1517, 505, "3 · Instantánea del APK", "assets/structural_data.json")]
for x0, y0, x1, y1, heading, path in cards:
    d.rounded_rectangle((x0, y0, x1, y1), radius=19, fill="white", outline="#cad8e5", width=2)
    write(x0+23, y0+23, heading, navy, big)
    write(x0+23, y0+68, "elementTag 246 · Viga", gray, small)
    write(x0+23, y0+105, "My = {:+.3f} kN·m".format(moment), blue, big)
    write(x0+23, y0+150, "Vz = {:+.3f} kN".format(shear), teal, big)
    write(x0+23, y0+195, "Misma estación s = 0,50", navy, text)
    d.line((x0+22, y0+237, x1-22, y0+237), fill="#cbd9e4", width=2)
    write(x0+23, y0+250, "Estaciones y acciones coinciden", gray, small)
    write(x0+23, y0+278, "exactamente en los archivos.", gray, small)
    # Paths are split to remain legible in each card.
    if len(path) > 35:
        write(x0+23, y0+310, path[:path.rfind("/")+1], gray, font(15))
        write(x0+23, y0+333, path[path.rfind("/")+1:], gray, font(15))
    else:
        write(x0+23, y0+321, path, gray, font(15))

for x in (505, 1015):
    d.line((x, 320, x+34, 320), fill=orange, width=5)
    d.polygon(((x+34, 320), (x+22, 312), (x+22, 328)), fill=orange)

d.rounded_rectangle((38, 520, 1517, 671), radius=18, fill="#e4eef8")
write(65, 540, "Prueba en teléfono comunicada por el equipo", navy, big)
write(65, 579, "ID 246: área de losas asignada = {:.4f} m². Se probaron los ID 1, 241 y 246 (y otros).".format(area), navy)
write(65, 617, "Valores idénticos en los archivos; el equipo informa igualdad en el teléfono. NO es una captura AR.", orange, small)

out = HERE / "trazabilidad_resultados.png"
im.save(out, optimize=True)
print("ID 246 R, x/L=0.50:", "My", moment, "Vz", shear, "area_m2", area)
print(out)
