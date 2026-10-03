"""Dibuja un protocolo de medición AR; no representa datos observados."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


HERE = Path(__file__).resolve().parent
OUT = HERE / "medicion_error_ar.png"
im = Image.new("RGB", (1400, 700), "#f2f6fa")
d = ImageDraw.Draw(im)


def face(size, bold=False):
    for name in (["C:/Windows/Fonts/arialbd.ttf", "DejaVuSans-Bold.ttf"] if bold else
                 ["C:/Windows/Fonts/arial.ttf", "DejaVuSans.ttf"]):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


title, header, label, note = face(34, True), face(25, True), face(21), face(18)
navy, teal, orange, blue, gray = "#102438", "#008c83", "#bd6428", "#316ac1", "#586b7d"


def t(x, y, value, color=navy, font=label):
    d.text((x, y), value, fill=color, font=font)


def arrow(a, b, color):
    import math
    d.line((a, b), fill=color, width=5)
    angle = math.atan2(b[1]-a[1], b[0]-a[0]); length = 13
    d.polygon([b, (b[0]-length*math.cos(angle-0.5), b[1]-length*math.sin(angle-0.5)),
               (b[0]-length*math.cos(angle+0.5), b[1]-length*math.sin(angle+0.5))], fill=color)


t(35, 27, "¿Cómo estimar el error espacial de alineamiento?", navy, title)
t(37, 78, "Comparar puntos sobre la misma cara, a escala real 1:1; repetir la medición tras reanclar.", gray)
d.rounded_rectangle((35, 135, 900, 583), radius=20, fill="white", outline="#cbd9e4", width=2)
t(60, 153, "Cara del elemento físico", navy, header)
d.rounded_rectangle((82, 220, 805, 488), radius=10, fill="#e7ebee", outline="#9eaebb", width=3)
# The virtual outlines are deliberately displaced; no measured displacement is implied.
for a, b in [((119, 277), (706, 277)), ((119, 413), (706, 413))]:
    d.line((a, b), fill=gray, width=9)
for a, b in [((156, 238), (743, 238)), ((156, 374), (743, 374))]:
    d.line((a, b), fill=teal, width=6)
for x, y in [(119, 277), (706, 413)]:
    d.ellipse((x-10, y-10, x+10, y+10), fill=navy)
for x, y in [(156, 238), (743, 374)]:
    d.ellipse((x-10, y-10, x+10, y+10), fill=teal)
arrow((119, 304), (156, 304), orange)
arrow((180, 277), (180, 238), blue)
t(113, 316, "δh", orange, header)
t(191, 247, "δv", blue, header)
t(217, 202, "Virtual", teal, label)
t(415, 416, "Real", navy, label)
d.rounded_rectangle((574, 300, 647, 373), radius=4, outline=orange, width=3)
t(573, 380, "marcador", orange, note)
d.ellipse((62, 516, 75, 529), fill=navy)
t(83, 509, "punto real", navy, note)
d.ellipse((223, 516, 236, 529), fill=teal)
t(244, 509, "punto virtual", navy, note)
t(418, 509, "δh / δv: separar y medir en cm", navy, note)
t(61, 544, "La separación dibujada NO está a escala y NO es una lectura del teléfono.", orange, note)

d.rounded_rectangle((925, 135, 1365, 583), radius=20, fill="white", outline="#cbd9e4", width=2)
t(950, 154, "Protocolo en el Redmi", navy, header)
for y, line in [(214, "1. Marcador medido: 20 × 20 cm"), (254, "2. Escala real: 1:1"),
                (294, "3. Dos puntos de la misma cara"), (334, "4. Anotar δh y δv en cm"),
                (374, "5. Repetir al menos 3 anclajes")]:
    t(950, y, line)
d.line((947, 422, 1339, 422), fill="#cad8e4", width=2)
t(950, 440, "e_plano ≈ √(δh² + δv²)", blue, header)
t(950, 480, "e_medio = Σe / n", navy)
t(950, 515, "e_máx = max(e)", navy)

d.rounded_rectangle((35, 608, 1365, 667), radius=13, fill="#e0ebfa")
t(56, 622, "Estado actual: detección y resultados verificados por el equipo; error de posición NO MEDIDO.", navy, label)
im.save(OUT, optimize=True)
print(OUT)
