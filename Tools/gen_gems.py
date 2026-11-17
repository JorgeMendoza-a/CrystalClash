# Herramienta (fuera de Assets): regenera los PNG de Assets/Resources/Minerals.
# Uso: python3 Tools/gen_gems.py Assets/Resources/Minerals   (requiere: pip install pillow)
"""Genera sprites provisionales de minerales para CrystalClash.

Cada mineral tiene una FORMA distinta (no solo color) para que se distingan
bien, también para jugadores con daltonismo.
Uso: python3 gen_gems.py <carpeta_salida>
"""
import math
import random
import sys
from PIL import Image, ImageDraw, ImageFilter

S = 512          # se dibuja grande y se reduce (antialias)
OUT = 256
C = S / 2
LIGHT = math.radians(-135)  # luz desde arriba a la izquierda


def shade(rgb, k):
    """k>1 aclara, k<1 oscurece."""
    if k >= 1:
        t = min(1.0, k - 1)
        return tuple(int(c + (255 - c) * t) for c in rgb)
    return tuple(int(c * k) for c in rgb)


def poly(n, r, rot=0.0, sx=1.0, sy=1.0, cx=C, cy=C):
    return [(cx + math.cos(rot + 2 * math.pi * i / n) * r * sx,
             cy + math.sin(rot + 2 * math.pi * i / n) * r * sy) for i in range(n)]


def scale_pts(pts, k, cx=C, cy=C):
    return [(cx + (x - cx) * k, cy + (y - cy) * k) for x, y in pts]


def facet_brightness(p1, p2, cx=C, cy=C):
    mx, my = (p1[0] + p2[0]) / 2, (p1[1] + p2[1]) / 2
    ang = math.atan2(my - cy, mx - cx)
    return 0.62 + 0.5 * math.cos(ang - LIGHT)


def faceted_gem(draw, outer, base, table_k=0.55, outline=True):
    """Gema facetada: anillo de facetas + mesa central + contorno."""
    inner = scale_pts(outer, table_k)
    n = len(outer)
    for i in range(n):
        a, b = outer[i], outer[(i + 1) % n]
        ia, ib = inner[i], inner[(i + 1) % n]
        k = facet_brightness(a, b)
        draw.polygon([a, b, ib, ia], fill=shade(base, k))
        # sub-faceta (triángulo) para dar más brillo
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        draw.polygon([mid, ib, ia], fill=shade(base, k * 1.08))
    draw.polygon(inner, fill=shade(base, 1.18))
    # línea de brillo dentro de la mesa
    if outline:
        draw.polygon(outer, outline=shade(base, 0.35), width=10)
        draw.polygon(inner, outline=shade(base, 1.35), width=3)


def highlight(img, cx, cy, rx, ry, alpha=170):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=(255, 255, 255, alpha))
    layer = layer.filter(ImageFilter.GaussianBlur(10))
    img.alpha_composite(layer)


def sparkle(img, cx, cy, r):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.polygon([(cx, cy - r), (cx + r * 0.18, cy - r * 0.18), (cx + r, cy), (cx + r * 0.18, cy + r * 0.18),
               (cx, cy + r), (cx - r * 0.18, cy + r * 0.18), (cx - r, cy), (cx - r * 0.18, cy - r * 0.18)],
              fill=(255, 255, 255, 235))
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(1.5)))


def lumpy(n, r, jitter, seed, sx=1.0, sy=1.0):
    rnd = random.Random(seed)
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        rr = r * (1 - jitter / 2 + rnd.random() * jitter)
        pts.append((C + math.cos(a) * rr * sx, C + math.sin(a) * rr * sy))
    return pts


def new():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def finish(img, path):
    img.resize((OUT, OUT), Image.LANCZOS).save(path)


# ----------------------------------------------------------------------

def sapphire(path):   # hexágono, azul
    img, d = new()
    faceted_gem(d, poly(6, 215, rot=math.pi / 6, sy=1.05), (30, 90, 230))
    highlight(img, C - 70, C - 90, 60, 30)
    sparkle(img, C - 95, C - 105, 38)
    finish(img, path)


def emerald(path):    # talla esmeralda (rectángulo con esquinas cortadas), verde
    img, d = new()
    w, h, cut = 175, 215, 62
    outer = [(C - w + cut, C - h), (C + w - cut, C - h), (C + w, C - h + cut), (C + w, C + h - cut),
             (C + w - cut, C + h), (C - w + cut, C + h), (C - w, C + h - cut), (C - w, C - h + cut)]
    faceted_gem(d, outer, (25, 175, 70), table_k=0.6)
    # escalones de la talla
    d.polygon(scale_pts(outer, 0.78), outline=(170, 255, 190), width=3)
    highlight(img, C - 60, C - 110, 55, 25)
    sparkle(img, C + 85, C - 120, 32)
    finish(img, path)


def amethyst(path):   # rombo alto (cristal), morado
    img, d = new()
    outer = [(C, C - 230), (C + 120, C - 120), (C + 175, C), (C + 120, C + 130), (C, C + 230),
             (C - 120, C + 130), (C - 175, C), (C - 120, C - 120)]
    faceted_gem(d, outer, (150, 50, 220), table_k=0.5)
    highlight(img, C - 50, C - 100, 40, 28)
    sparkle(img, C - 70, C - 120, 34)
    finish(img, path)


def ruby(path):       # redondo brillante, rojo
    img, d = new()
    outer = poly(12, 215, rot=math.pi / 12)
    faceted_gem(d, outer, (215, 25, 45), table_k=0.5)
    # estrella de facetas en la mesa
    inner = scale_pts(outer, 0.5)
    for i in range(0, 12, 2):
        d.line([inner[i], (C, C)], fill=(255, 140, 150), width=3)
    d.ellipse([C - 215, C - 215, C + 215, C + 215], outline=(90, 5, 15), width=10)
    highlight(img, C - 75, C - 85, 55, 32)
    sparkle(img, C - 90, C - 100, 38)
    finish(img, path)


def gold(path):       # pepita irregular, dorada
    img, d = new()
    outer = lumpy(11, 205, 0.32, seed=7, sx=1.08, sy=0.92)
    base = (235, 175, 25)
    faceted_gem(d, outer, base, table_k=0.45, outline=False)
    # bultos
    rnd = random.Random(3)
    for _ in range(9):
        x = C + rnd.uniform(-120, 120)
        y = C + rnd.uniform(-100, 100)
        r = rnd.uniform(22, 42)
        d.ellipse([x - r, y - r, x + r, y + r], fill=shade(base, 0.8))
        d.ellipse([x - r * 0.7 - 6, y - r * 0.7 - 6, x + r * 0.5 - 6, y + r * 0.5 - 6], fill=shade(base, 1.25))
    d.polygon(outer, outline=(110, 70, 5), width=10)
    highlight(img, C - 70, C - 70, 60, 30, alpha=150)
    sparkle(img, C + 80, C - 80, 34)
    finish(img, path)


def carbon(path):     # roca angulosa, gris oscuro (más clara que el fondo)
    img, d = new()
    outer = lumpy(9, 210, 0.28, seed=21, sx=1.05, sy=0.95)
    faceted_gem(d, outer, (95, 98, 110), table_k=0.5)
    rnd = random.Random(5)
    for _ in range(6):   # vetas
        x = C + rnd.uniform(-100, 100)
        y = C + rnd.uniform(-90, 90)
        d.line([(x, y), (x + rnd.uniform(-60, 60), y + rnd.uniform(-40, 40))], fill=(55, 57, 66), width=6)
    highlight(img, C - 60, C - 80, 50, 22, alpha=110)
    finish(img, path)


def copper(path):     # cuadrado redondeado, cobrizo
    img, d = new()
    faceted_gem(d, poly(4, 230, rot=math.pi / 4), (200, 100, 45), table_k=0.55)
    highlight(img, C - 70, C - 80, 55, 28)
    finish(img, path)


def silver(path):     # pentágono, plateado
    img, d = new()
    faceted_gem(d, poly(5, 220, rot=-math.pi / 2), (190, 195, 210), table_k=0.55)
    highlight(img, C - 60, C - 80, 55, 28)
    sparkle(img, C + 70, C - 100, 30)
    finish(img, path)


def diamond(path):    # talla diamante clásica, celeste
    img, d = new()
    outer = [(C - 130, C - 150), (C + 130, C - 150), (C + 215, C - 60), (C, C + 220), (C - 215, C - 60)]
    faceted_gem(d, outer, (90, 220, 245), table_k=0.5)
    highlight(img, C - 60, C - 110, 60, 22)
    sparkle(img, C - 120, C - 120, 40)
    sparkle(img, C + 100, C - 20, 26)
    finish(img, path)


def prism(path):      # estrella de 6 puntas casi blanca (se tiñe de arcoíris en el juego)
    img, d = new()
    pts = []
    for i in range(12):
        r = 225 if i % 2 == 0 else 120
        a = -math.pi / 2 + i * math.pi / 6
        pts.append((C + math.cos(a) * r, C + math.sin(a) * r))
    faceted_gem(d, pts, (225, 225, 240), table_k=0.45)
    highlight(img, C - 40, C - 70, 50, 26)
    sparkle(img, C, C, 60)
    finish(img, path)


# --- marcas de especiales (se dibujan encima del mineral) --------------

def drill(path, horizontal):
    img, d = new()
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    if horizontal:
        g.rounded_rectangle([20, C - 48, S - 20, C + 48], 40, fill=(255, 255, 255, 140))
    else:
        g.rounded_rectangle([C - 48, 20, C + 48, S - 20], 40, fill=(255, 255, 255, 140))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(14)))
    # flechas a ambos lados
    w = 30
    if horizontal:
        d.rectangle([90, C - 14, S - 90, C + 14], fill=(255, 255, 255, 255))
        d.polygon([(30, C), (100, C - w * 2), (100, C + w * 2)], fill=(255, 255, 255, 255))
        d.polygon([(S - 30, C), (S - 100, C - w * 2), (S - 100, C + w * 2)], fill=(255, 255, 255, 255))
    else:
        d.rectangle([C - 14, 90, C + 14, S - 90], fill=(255, 255, 255, 255))
        d.polygon([(C, 30), (C - w * 2, 100), (C + w * 2, 100)], fill=(255, 255, 255, 255))
        d.polygon([(C, S - 30), (C - w * 2, S - 100), (C + w * 2, S - 100)], fill=(255, 255, 255, 255))
    finish(img, path)


def bomb(path):
    img, d = new()
    # anillo oscuro + mecha con chispa: "dinamita"
    d.ellipse([C - 120, C - 120, C + 120, C + 120], fill=(25, 25, 30, 235), outline=(255, 210, 80, 255), width=12)
    d.line([(C + 60, C - 100), (C + 120, C - 175)], fill=(200, 160, 90, 255), width=14)
    sparkle(img, C + 128, C - 185, 55)
    d.ellipse([C - 70, C - 70, C - 20, C - 20], fill=(255, 255, 255, 120))
    finish(img, path)


def main(out):
    gems = [("sapphire", sapphire), ("emerald", emerald), ("amethyst", amethyst), ("ruby", ruby),
            ("gold", gold), ("carbon", carbon), ("copper", copper), ("silver", silver),
            ("diamond", diamond), ("prism", prism)]
    for name, fn in gems:
        fn(f"{out}/{name}.png")
    drill(f"{out}/drill_h.png", True)
    drill(f"{out}/drill_v.png", False)
    bomb(f"{out}/bomb.png")


if __name__ == "__main__":
    main(sys.argv[1])
