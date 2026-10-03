"""Genera el esquema explicativo OpenSees -> marcador -> ARCore del informe P1A6."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


HERE = Path(__file__).resolve().parent
OUT = HERE / "transformacion_coordenadas.png"
W, H = 1600, 940
BG = "#f2f6fa"
NAVY = "#102438"
MUTED = "#485d70"
TEAL = "#008c83"
ORANGE = "#cb6a2a"
BLUE = "#316ac1"
LINE = "#cbd9e4"


def font(size, bold=False):
    names = (["C:/Windows/Fonts/arialbd.ttf", "DejaVuSans-Bold.ttf"] if bold else
             ["C:/Windows/Fonts/arial.ttf", "DejaVuSans.ttf"])
    for name in names:
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


image = Image.new("RGB", (W, H), BG)
draw = ImageDraw.Draw(image)
title, subtitle, heading, body, detail, equation = (
    font(37, True), font(20), font(25, True), font(19), font(17), font(25, True))


def text(x, y, value, color=NAVY, face=body):
    draw.text((x, y), value, font=face, fill=color)


def card(box):
    draw.rounded_rectangle(box, radius=20, fill="white", outline=LINE, width=2)


def arrow(start, end, color, width=5, head=15):
    import math
    draw.line([start, end], fill=color, width=width)
    angle = math.atan2(end[1] - start[1], end[0] - start[0])
    left = (end[0] - head * math.cos(angle - 0.54), end[1] - head * math.sin(angle - 0.54))
    right = (end[0] - head * math.cos(angle + 0.54), end[1] - head * math.sin(angle + 0.54))
    draw.polygon([end, left, right], fill=color)


text(42, 25, "DEL MODELO ESTRUCTURAL A LA REALIDAD AUMENTADA", NAVY, title)
text(45, 80, "Esquema de coordenadas: OpenSees → barra local → marcador → anchor ARCore", MUTED, subtitle)

# 1. Global OpenSees and local bar axes.
card((35, 133, 516, 632))
text(57, 157, "1 · OpenSees", NAVY, heading)
text(58, 198, "Coordenadas globales (X, Y, Z) [m]", MUTED, body)
text(58, 225, "Z es vertical · extremos i y j", MUTED, detail)
arrow((128, 403), (416, 315), TEAL, 14, 24)
draw.ellipse((114, 389, 142, 417), fill=NAVY)
draw.ellipse((401, 301, 430, 330), fill=NAVY)
draw.ellipse((257, 350, 276, 369), fill=ORANGE)
text(109, 419, "i", NAVY, heading)
text(415, 332, "j", NAVY, heading)
text(272, 376, "c", ORANGE, heading)
text(185, 288, "x_local: i → j", TEAL, detail)
draw.line((59, 480, 493, 480), fill=LINE, width=2)
text(61, 500, "c = (p_i + p_j) / 2", NAVY, body)
text(61, 536, "B = [x_local  y_local  z_local]", NAVY, body)
text(61, 573, "p_local = Bᵀ · (p − c)", BLUE, body)

# 2. Marker plane: X to the right, -Z up and Y normal.
card((551, 133, 1047, 632))
text(575, 157, "2 · Imagen / marcador", NAVY, heading)
text(576, 197, "Origen: centro de la imagen física", MUTED, body)
draw.rounded_rectangle((696, 256, 897, 457), radius=6, fill="#edf6f5", outline=TEAL, width=4)
draw.rectangle((733, 273, 859, 303), fill="#d9e8e6")
text(760, 277, "ARRIBA", NAVY, detail)
arrow((743, 391), (851, 391), TEAL, 9, 17)
draw.ellipse((782, 380, 802, 400), fill=ORANGE)
text(748, 352, "i", NAVY, detail)
text(853, 375, "j", NAVY, detail)
arrow((793, 338), (793, 309), ORANGE, 4, 12)
text(898, 282, "−Z", ORANGE, body)
arrow((897, 469), (962, 469), BLUE, 4, 12)
text(897, 489, "+X", BLUE, body)
draw.ellipse((620, 371, 643, 394), outline=ORANGE, width=3)
draw.ellipse((628, 379, 636, 387), fill=ORANGE)
text(581, 404, "+Y: normal", ORANGE, detail)
text(597, 476, "20 × 20 cm reales", MUTED, body)
draw.line((573, 517, 1024, 517), fill=LINE, width=2)
text(576, 529, "Viga: x_local → +X marcador", TEAL, detail)
text(576, 558, "Columna: x_local → −Z marcador", ORANGE, detail)
text(576, 589, "d: distancia normal cara ↔ eje", MUTED, detail)

# 3. Tracked AR frame and camera projection.
card((1081, 133, 1565, 632))
text(1106, 157, "3 · ARCore / teléfono", NAVY, heading)
text(1107, 197, "Pose de la imagen → anchor", MUTED, body)
for y in (338, 377, 416, 455):
    draw.line((1114, y, 1526, y), fill="#e6eef4", width=2)
for x in (1161, 1225, 1289, 1353, 1417, 1481):
    draw.line((x, 322, x, 474), fill="#e6eef4", width=2)
draw.rounded_rectangle((1150, 338, 1259, 448), radius=8, fill="#def1ee", outline=TEAL, width=3)
draw.ellipse((1193, 382, 1217, 406), fill=ORANGE)
text(1128, 457, "anchor", ORANGE, detail)
arrow((1205, 392), (1399, 336), TEAL, 9, 17)
draw.rounded_rectangle((1429, 269, 1501, 400), radius=13, fill=NAVY)
draw.rectangle((1440, 288, 1490, 366), fill="#cdece9")
draw.ellipse((1462, 377, 1468, 383), fill="white")
arrow((1434, 335), (1404, 334), BLUE, 4, 13)
text(1322, 248, "cámara", MUTED, detail)
draw.line((1104, 514, 1542, 514), fill=LINE, width=2)
text(1108, 531, "T_mundo←anchor: rotación + posición", NAVY, detail)
text(1108, 561, "Proyección: vista de la cámara", MUTED, detail)
text(1108, 591, "Origen AR: propio de la sesión", MUTED, detail)

card((35, 663, 1565, 820))
text(61, 683, "COMPOSICIÓN DEL PUNTO", NAVY, heading)
text(63, 724, "p_marker = s · C · Bᵀ · (p_OpenSees − c) + (0, d, 0)", NAVY, equation)
text(63, 766, "p_AR = T_mundo←anchor · [p_marker, 1]", BLUE, equation)

draw.rounded_rectangle((35, 841, 1565, 918), radius=14, fill="#e3ecfa")
text(59, 856, "Unity: (X, Y, Z)_visor = (X, Z, Y)_OpenSees. Es una representación independiente; no es un paso de la APK.", NAVY, body)
text(59, 887, "s = 0,10 o 1,00; d se calibra en terreno. Esquema explicativo, no medición de alineamiento físico.", MUTED, detail)

image.save(OUT, optimize=True)
print(OUT)
