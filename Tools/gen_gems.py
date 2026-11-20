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


if __name__ == "__main__" and sys.argv[1] not in ("--board", "--ui", "--ui2", "--ui3", "--ui4", "--ui5"):
    main(sys.argv[1])


# ======================================================================
# Marco y casillas del tablero (Assets/Resources/Board)
# Uso: python3 Tools/gen_gems.py --board Assets/Resources/Board
# ======================================================================

FRAME_SIZE = 192
FRAME_BORDER = 48   # píxeles de borde para el 9-slice (debe coincidir con BoardManager)


def board_frame(path):
    k = 4  # dibujar 4x más grande y reducir
    n = FRAME_SIZE * k
    b = FRAME_BORDER * k
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # cuerpo de metal oscuro con degradado vertical
    for i in range(n):
        t = i / n
        col = (int(70 - 30 * t), int(52 - 22 * t), int(40 - 18 * t), 255)
        d.line([(0, i), (n, i)], fill=col)
    mask = Image.new("L", (n, n), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, n - 1, n - 1], radius=b * 0.7, fill=255)
    body = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    body.paste(img, (0, 0), mask)
    img = body
    d = ImageDraw.Draw(img)
    # borde exterior oscuro y aro dorado
    d.rounded_rectangle([2, 2, n - 3, n - 3], radius=b * 0.7, outline=(20, 12, 8, 255), width=6 * k // 2)
    d.rounded_rectangle([b * 0.28, b * 0.28, n - b * 0.28, n - b * 0.28], radius=b * 0.45,
                        outline=(214, 166, 72, 255), width=3 * k)
    d.rounded_rectangle([b * 0.28 + 3 * k, b * 0.28 + 3 * k, n - b * 0.28 - 3 * k, n - b * 0.28 - 3 * k],
                        radius=b * 0.4, outline=(120, 85, 30, 255), width=k)
    # interior: panel azul muy oscuro (semi-transparente) con sombra interior
    inner = [b * 0.62, b * 0.62, n - b * 0.62, n - b * 0.62]
    d.rounded_rectangle(inner, radius=b * 0.25, fill=(10, 16, 30, 235), outline=(5, 8, 15, 255), width=2 * k)
    # remaches dorados en las esquinas
    for cx, cy in [(b * 0.45, b * 0.45), (n - b * 0.45, b * 0.45), (b * 0.45, n - b * 0.45), (n - b * 0.45, n - b * 0.45)]:
        r = b * 0.17
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(196, 150, 60, 255), outline=(90, 60, 20, 255), width=k)
        d.ellipse([cx - r * 0.5, cy - r * 0.6, cx, cy - r * 0.1], fill=(255, 230, 160, 255))
    img.resize((FRAME_SIZE, FRAME_SIZE), Image.LANCZOS).save(path)


def board_cell(path, base):
    k = 4
    n = 128 * k
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pad = 4 * k
    d.rounded_rectangle([pad, pad, n - pad, n - pad], radius=18 * k, fill=base + (215,))
    # bisel: luz arriba-izquierda, sombra abajo-derecha
    d.rounded_rectangle([pad, pad, n - pad, n - pad], radius=18 * k, outline=(255, 255, 255, 28), width=3 * k)
    d.line([(pad + 18 * k, n - pad - 2 * k), (n - pad - 18 * k, n - pad - 2 * k)], fill=(0, 0, 0, 90), width=4 * k)
    img.resize((128, 128), Image.LANCZOS).save(path)


if __name__ == "__main__" and len(sys.argv) > 2 and sys.argv[1] == "--board":
    out = sys.argv[2]
    board_frame(f"{out}/board_frame.png")
    board_cell(f"{out}/board_cell.png", (24, 32, 52))
    board_cell(f"{out}/board_cell_alt.png", (32, 42, 66))


# ======================================================================
# Íconos de la UI (Assets/Resources/UI)
# Uso: python3 Tools/gen_gems.py --ui Assets/Resources/UI
# ======================================================================

def star_icon(path, filled):
    img, d = new()
    pts = []
    for i in range(10):
        r = 230 if i % 2 == 0 else 100
        a = -math.pi / 2 + i * math.pi / 5
        pts.append((C + math.cos(a) * r, C + 15 + math.sin(a) * r))
    if filled:
        faceted_gem(d, pts, (245, 185, 30), table_k=0.5, outline=False)
        d.polygon(pts, outline=(120, 70, 0), width=14)
        highlight(img, C - 40, C - 50, 45, 22)
    else:
        d.polygon(pts, fill=(40, 44, 58, 255), outline=(110, 115, 130, 255), width=14)
    finish(img, path)


def hammer_icon(path):
    img, d = new()
    # mango de madera en diagonal
    d.line([(C - 150, C + 170), (C + 60, C - 40)], fill=(90, 50, 20, 255), width=58)
    d.line([(C - 150, C + 170), (C + 60, C - 40)], fill=(170, 105, 50, 255), width=40)
    # cabeza de metal (rotada 45°)
    head = [(C - 10, C - 190), (C + 190, C + 10), (C + 120, C + 80), (C - 80, C - 120)]
    head = [(x + 10, y - 20) for x, y in head]
    d.polygon(head, fill=(120, 130, 150, 255), outline=(40, 45, 55, 255), width=10)
    d.polygon([head[0], head[1], ((head[1][0] + head[2][0]) / 2, (head[1][1] + head[2][1]) / 2),
               ((head[0][0] + head[3][0]) / 2, (head[0][1] + head[3][1]) / 2)], fill=(185, 195, 215, 255))
    highlight(img, C + 30, C - 120, 40, 18)
    finish(img, path)


def bomb_icon(path):
    img, d = new()
    d.ellipse([C - 170, C - 120, C + 150, C + 200], fill=(30, 30, 36, 255), outline=(10, 10, 12, 255), width=10)
    d.rectangle([C + 40, C - 150, C + 110, C - 90], fill=(90, 90, 100, 255))
    d.line([(C + 75, C - 150), (C + 120, C - 205), (C + 170, C - 190)], fill=(200, 160, 90, 255), width=14)
    highlight(img, C - 70, C - 30, 55, 35, alpha=120)
    sparkle(img, C + 175, C - 195, 60)
    finish(img, path)


def lightning_icon(path):
    img, d = new()
    bolt = [(C + 40, C - 230), (C - 130, C + 30), (C - 10, C + 30), (C - 60, C + 230), (C + 140, C - 50),
            (C + 15, C - 50), (C + 80, C - 230)]
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).polygon(bolt, fill=(255, 220, 60, 160))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(18)))
    d.polygon(bolt, fill=(255, 210, 40, 255), outline=(150, 90, 0, 255), width=10)
    d.polygon([bolt[0], bolt[1], bolt[2], (C + 10, C - 40)], fill=(255, 240, 150, 255))
    finish(img, path)


def shuffle_icon(path):
    img, d = new()
    r = 165
    box = [C - r, C - r, C + r, C + r]
    for start, end, col, tip_ang in [(200, 340, (60, 200, 90), 340), (20, 160, (240, 120, 40), 160)]:
        d.arc(box, start, end, fill=col + (255,), width=52)
        a = math.radians(tip_ang)
        tx, ty = C + math.cos(a) * r, C + math.sin(a) * r
        # punta de flecha tangente al arco
        t = a + math.pi / 2
        p1 = (tx + math.cos(t) * 70, ty + math.sin(t) * 70)
        p2 = (tx + math.cos(a) * 60, ty + math.sin(a) * 60)
        p3 = (tx - math.cos(a) * 60, ty - math.sin(a) * 60)
        d.polygon([p1, p2, p3], fill=col + (255,))
    finish(img, path)


def badge_icon(path):
    img, d = new()
    d.ellipse([40, 40, S - 40, S - 40], fill=(20, 110, 40, 255), outline=(230, 190, 80, 255), width=28)
    d.ellipse([80, 80, S - 80, S - 80], fill=(40, 160, 60, 255))
    highlight(img, C - 60, C - 90, 90, 40, alpha=90)
    finish(img, path)


LEAGUES = [  # nombre de archivo, color del metal, color de la gema
    ("bronce", (184, 115, 51), (230, 140, 70)),
    ("plata", (175, 180, 195), (220, 225, 240)),
    ("oro", (225, 175, 35), (255, 215, 80)),
    ("platino", (60, 190, 150), (90, 235, 200)),
    ("diamante", (55, 140, 230), (110, 200, 255)),
    ("maestro", (210, 50, 55), (255, 90, 90)),
    ("campeon", (150, 70, 230), (210, 140, 255)),
]


def rank_emblem(path, metal, gem):
    img, d = new()
    # alas laterales
    for side in (-1, 1):
        wing = [(C + side * 120, C - 120), (C + side * 235, C - 160), (C + side * 210, C - 40),
                (C + side * 240, C + 10), (C + side * 180, C + 80), (C + side * 120, C + 60)]
        d.polygon(wing, fill=shade(metal, 0.75), outline=shade(metal, 0.35))
    # escudo
    shield = [(C - 150, C - 190), (C + 150, C - 190), (C + 165, C - 20), (C + 90, C + 130), (C, C + 220),
              (C - 90, C + 130), (C - 165, C - 20)]
    faceted_gem(d, shield, metal, table_k=0.78)
    # gema central
    faceted_gem(d, poly(6, 85, rot=math.pi / 6, cy=C - 15), gem, table_k=0.5)
    highlight(img, C - 50, C - 110, 50, 22)
    sparkle(img, C - 40, C - 50, 26)
    finish(img, path)


if __name__ == "__main__" and len(sys.argv) > 2 and sys.argv[1] == "--ui":
    out = sys.argv[2]
    star_icon(f"{out}/star_full.png", True)
    star_icon(f"{out}/star_empty.png", False)
    hammer_icon(f"{out}/booster_hammer.png")
    bomb_icon(f"{out}/booster_bomb.png")
    lightning_icon(f"{out}/booster_lightning.png")
    shuffle_icon(f"{out}/booster_shuffle.png")
    badge_icon(f"{out}/badge.png")
    for name, metal, gem in LEAGUES:
        rank_emblem(f"{out}/rank_{name}.png", metal, gem)


# ======================================================================
# Piezas de interfaz con relieve (Assets/Resources/UI)
# Uso: python3 Tools/gen_gems.py --ui2 Assets/Resources/UI
# Todas son 9-slice: el borde indicado no se estira (ver UiKit.cs).
# ======================================================================

def _rounded_mask(n, radius, inset=0):
    m = Image.new("L", (n, n), 0)
    ImageDraw.Draw(m).rounded_rectangle([inset, inset, n - 1 - inset, n - 1 - inset], radius=radius, fill=255)
    return m


def _vgradient(n, top, bottom):
    g = Image.new("RGBA", (n, n))
    d = ImageDraw.Draw(g)
    for i in range(n):
        t = i / (n - 1)
        d.line([(0, i), (n, i)], fill=tuple(int(top[k] + (bottom[k] - top[k]) * t) for k in range(3)) + (255,))
    return g


def ui_button(path, top, bottom, size=160):
    """Botón 9-slice (borde 56 px): degradado, brillo arriba, aro dorado y sombra abajo."""
    k = 4
    n = size * k
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    # sombra inferior
    shadow = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle([6 * k, 12 * k, n - 6 * k, n - 2 * k], radius=34 * k, fill=(0, 0, 0, 150))
    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(4 * k)))
    # aro dorado
    rim = _vgradient(n, (255, 222, 140), (150, 95, 25))
    img.paste(rim, (0, 0), _rounded_mask(n, 36 * k, inset=4 * k))
    # cuerpo
    body = _vgradient(n, top, bottom)
    img.paste(body, (0, 0), _rounded_mask(n, 30 * k, inset=12 * k))
    d = ImageDraw.Draw(img)
    # brillo en la mitad superior
    gloss = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    ImageDraw.Draw(gloss).rounded_rectangle([20 * k, 18 * k, n - 20 * k, n // 2], radius=24 * k, fill=(255, 255, 255, 70))
    img.alpha_composite(gloss)
    # línea oscura de base
    d.rounded_rectangle([12 * k, 12 * k, n - 12 * k, n - 12 * k], radius=30 * k, outline=(0, 0, 0, 120), width=2 * k)
    img.resize((size, size), Image.LANCZOS).save(path)


def ui_panel(path, size=256):
    """Panel 9-slice (borde 80 px): piedra azul oscura, doble aro dorado y adornos en las esquinas."""
    k = 3
    n = size * k
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    outer = _vgradient(n, (92, 66, 34), (40, 26, 14))
    img.paste(outer, (0, 0), _rounded_mask(n, 40 * k, inset=2 * k))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([2 * k, 2 * k, n - 2 * k, n - 2 * k], radius=40 * k, outline=(20, 12, 6, 255), width=3 * k)
    # aro dorado exterior e interior
    d.rounded_rectangle([12 * k, 12 * k, n - 12 * k, n - 12 * k], radius=32 * k, outline=(232, 186, 90, 255), width=4 * k)
    d.rounded_rectangle([18 * k, 18 * k, n - 18 * k, n - 18 * k], radius=28 * k, outline=(120, 80, 28, 255), width=2 * k)
    # interior piedra oscura con degradado
    inner = _vgradient(n, (22, 30, 52), (9, 13, 26))
    img.paste(inner, (0, 0), _rounded_mask(n, 24 * k, inset=24 * k))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([24 * k, 24 * k, n - 24 * k, n - 24 * k], radius=24 * k, outline=(0, 0, 0, 200), width=3 * k)
    d.line([(40 * k, 27 * k), (n - 40 * k, 27 * k)], fill=(255, 255, 255, 30), width=2 * k)
    # adornos de esquina: rombo dorado con gema azul
    for cx, cy in [(22 * k, 22 * k), (n - 22 * k, 22 * k), (22 * k, n - 22 * k), (n - 22 * k, n - 22 * k)]:
        r = 17 * k
        d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=(226, 178, 80, 255), outline=(90, 55, 15, 255))
        r2 = 8 * k
        d.polygon([(cx, cy - r2), (cx + r2, cy), (cx, cy + r2), (cx - r2, cy)], fill=(80, 170, 255, 255))
        d.ellipse([cx - 3 * k, cy - 5 * k, cx + 1 * k, cy - 1 * k], fill=(230, 245, 255, 255))
    img.resize((size, size), Image.LANCZOS).save(path)


def ui_bar(path, top, bottom, size=96):
    """Barra 9-slice (borde 40 px)."""
    k = 4
    n = size * k
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    img.paste(_vgradient(n, top, bottom), (0, 0), _rounded_mask(n, 40 * k, inset=4 * k))
    gloss = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    ImageDraw.Draw(gloss).rounded_rectangle([14 * k, 10 * k, n - 14 * k, n * 0.42], radius=20 * k, fill=(255, 255, 255, 60))
    img.alpha_composite(gloss)
    ImageDraw.Draw(img).rounded_rectangle([4 * k, 4 * k, n - 4 * k, n - 4 * k], radius=40 * k, outline=(0, 0, 0, 160), width=3 * k)
    img.resize((size, size), Image.LANCZOS).save(path)


def ui_row(path, size=96):
    """Fondo de fila de tabla 9-slice (borde 24 px)."""
    k = 4
    n = size * k
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    img.paste(_vgradient(n, (34, 44, 70), (20, 27, 46)), (0, 0), _rounded_mask(n, 18 * k, inset=2 * k))
    ImageDraw.Draw(img).rounded_rectangle([2 * k, 2 * k, n - 2 * k, n - 2 * k], radius=18 * k, outline=(90, 110, 150, 120), width=2 * k)
    img.resize((size, size), Image.LANCZOS).save(path)


def coin_icon(path):
    img, d = new()
    d.ellipse([C - 210, C - 210, C + 210, C + 210], fill=(150, 95, 10, 255))
    d.ellipse([C - 195, C - 205, C + 195, C + 185], fill=(250, 196, 40, 255))
    d.ellipse([C - 150, C - 160, C + 150, C + 140], fill=(230, 165, 25, 255), outline=(160, 105, 10, 255), width=10)
    crown = [(C - 90, C + 50), (C - 100, C - 60), (C - 45, C - 10), (C, C - 85), (C + 45, C - 10), (C + 100, C - 60), (C + 90, C + 50)]
    d.polygon(crown, fill=(255, 225, 110, 255), outline=(150, 95, 10, 255))
    highlight(img, C - 70, C - 120, 70, 30, alpha=140)
    finish(img, path)


if __name__ == "__main__" and len(sys.argv) > 2 and sys.argv[1] == "--ui2":
    out = sys.argv[2]
    ui_button(f"{out}/btn_green.png", (110, 205, 70), (35, 110, 25))
    ui_button(f"{out}/btn_blue.png", (80, 150, 240), (25, 70, 160))
    ui_button(f"{out}/btn_gray.png", (110, 115, 130), (50, 54, 66))
    ui_button(f"{out}/btn_red.png", (235, 95, 80), (140, 30, 25))
    ui_button(f"{out}/btn_purple.png", (170, 90, 240), (80, 25, 150))
    ui_panel(f"{out}/panel.png")
    ui_bar(f"{out}/bar_bg.png", (12, 16, 28), (26, 32, 50))
    ui_bar(f"{out}/bar_fill.png", (90, 180, 255), (25, 90, 210))
    ui_row(f"{out}/row.png")
    coin_icon(f"{out}/coin.png")


# ======================================================================
# Mapa, barra de navegación e íconos (Assets/Resources/UI)
# Uso: python3 Tools/gen_gems.py --ui3 Assets/Resources/UI
# ======================================================================

def level_node(path, top, bottom):
    img, d = new()
    # sombra
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse([C - 205, C - 180, C + 215, C + 235], fill=(0, 0, 0, 150))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(12)))
    # aro dorado
    rim = _vgradient(S, (255, 225, 140), (140, 90, 20))
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).ellipse([C - 215, C - 215, C + 215, C + 215], fill=255)
    img.paste(rim, (0, 0), m)
    # cuerpo
    body = _vgradient(S, top, bottom)
    m2 = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m2).ellipse([C - 185, C - 185, C + 185, C + 185], fill=255)
    img.paste(body, (0, 0), m2)
    d = ImageDraw.Draw(img)
    d.ellipse([C - 185, C - 185, C + 185, C + 185], outline=(0, 0, 0, 140), width=6)
    gloss = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(gloss).ellipse([C - 140, C - 170, C + 140, C - 10], fill=(255, 255, 255, 70))
    img.alpha_composite(gloss.filter(ImageFilter.GaussianBlur(4)))
    finish(img, path)


def rail_segment(path, w=256, h=96):
    """Tramo de riel horizontal (se estira a lo largo; 9-slice de 24 px a los lados)."""
    k = 3
    W, H = w * k, h * k
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 10 * k, W - 1, H - 10 * k], radius=24 * k, fill=(70, 45, 25, 255), outline=(30, 18, 8, 255), width=3 * k)
    # durmientes de madera
    for x in range(16 * k, W - 10 * k, 28 * k):
        d.rectangle([x, 18 * k, x + 14 * k, H - 18 * k], fill=(160, 110, 55, 255), outline=(80, 50, 20, 255), width=k)
    # dos rieles metálicos
    for y in (32 * k, H - 32 * k):
        d.rectangle([6 * k, y - 4 * k, W - 6 * k, y + 4 * k], fill=(205, 210, 220, 255), outline=(70, 75, 85, 255), width=k)
    img.resize((w, h), Image.LANCZOS).save(path)


def heart_icon(path):
    img, d = new()
    pts = []
    for i in range(200):
        t = 2 * math.pi * i / 200
        x = 16 * math.sin(t) ** 3
        y = -(13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t))
        pts.append((C + x * 13, C + y * 13 + 10))
    d.polygon(pts, fill=(220, 30, 50, 255), outline=(110, 0, 15, 255))
    d.line(pts + [pts[0]], fill=(110, 0, 15, 255), width=12)
    highlight(img, C - 80, C - 90, 55, 35, alpha=170)
    finish(img, path)


def gear_icon(path):
    img, d = new()
    teeth = []
    for i in range(16):
        r = 210 if i % 2 == 0 else 165
        a0 = 2 * math.pi * i / 16
        for da in (-0.17, 0.17):
            teeth.append((C + math.cos(a0 + da) * r, C + math.sin(a0 + da) * r))
    d.polygon(teeth, fill=(210, 165, 70, 255), outline=(90, 60, 15, 255))
    d.ellipse([C - 70, C - 70, C + 70, C + 70], fill=(40, 30, 20, 255), outline=(90, 60, 15, 255), width=10)
    highlight(img, C - 60, C - 90, 50, 25, alpha=110)
    finish(img, path)


def nav_map_icon(path):
    img, d = new()
    d.polygon([(C - 200, C - 140), (C - 70, C - 190), (C + 70, C - 140), (C + 200, C - 190), (C + 200, C + 150),
               (C + 70, C + 200), (C - 70, C + 150), (C - 200, C + 200)], fill=(225, 200, 140, 255), outline=(110, 80, 35, 255))
    d.line([(C - 70, C - 190), (C - 70, C + 150)], fill=(170, 140, 90, 255), width=8)
    d.line([(C + 70, C - 140), (C + 70, C + 200)], fill=(170, 140, 90, 255), width=8)
    d.line([(C - 150, C + 100), (C - 40, C - 20), (C + 60, C + 40), (C + 130, C - 90)], fill=(190, 60, 40, 255), width=12)
    d.line([(C + 100, C - 120), (C + 160, C - 60)], fill=(190, 40, 30, 255), width=16)
    d.line([(C + 160, C - 120), (C + 100, C - 60)], fill=(190, 40, 30, 255), width=16)
    finish(img, path)


def nav_home_icon(path):
    img, d = new()
    d.rectangle([C - 140, C - 20, C + 140, C + 190], fill=(200, 150, 90, 255), outline=(90, 55, 20, 255), width=10)
    d.polygon([(C - 200, C), (C, C - 200), (C + 200, C)], fill=(60, 110, 220, 255), outline=(20, 40, 100, 255))
    d.rectangle([C - 40, C + 70, C + 40, C + 190], fill=(110, 70, 30, 255))
    d.rectangle([C + 60, C + 20, C + 120, C + 80], fill=(255, 220, 120, 255), outline=(90, 55, 20, 255), width=6)
    finish(img, path)


def nav_shop_icon(path):
    img, d = new()
    d.rectangle([C - 170, C - 10, C + 170, C + 190], fill=(140, 90, 50, 255), outline=(70, 40, 15, 255), width=10)
    for i in range(6):
        x0 = C - 200 + i * 67
        d.polygon([(x0, C - 140), (x0 + 67, C - 140), (x0 + 67, C - 10), (x0, C - 10)],
                  fill=(170, 60, 210, 255) if i % 2 == 0 else (235, 225, 245, 255))
    d.rectangle([C - 200, C - 160, C + 200, C - 130], fill=(120, 40, 160, 255))
    d.rectangle([C - 120, C + 40, C + 120, C + 140], fill=(255, 210, 110, 255), outline=(70, 40, 15, 255), width=6)
    finish(img, path)


def nav_trophy_icon(path):
    img, d = new()
    gold = (245, 190, 40, 255)
    dark = (130, 80, 0, 255)
    d.arc([C - 210, C - 160, C - 60, C + 20], 90, 270, fill=gold, width=24)
    d.arc([C + 60, C - 160, C + 210, C + 20], 270, 90, fill=gold, width=24)
    d.polygon([(C - 140, C - 190), (C + 140, C - 190), (C + 110, C + 20), (C, C + 70), (C - 110, C + 20)], fill=gold, outline=dark)
    d.rectangle([C - 30, C + 60, C + 30, C + 140], fill=gold, outline=dark, width=6)
    d.rectangle([C - 110, C + 140, C + 110, C + 200], fill=(150, 90, 40, 255), outline=(70, 40, 15, 255), width=8)
    highlight(img, C - 60, C - 120, 40, 50, alpha=120)
    finish(img, path)


def nav_profile_icon(path):
    img, d = new()
    d.ellipse([C - 90, C - 190, C + 90, C - 10], fill=(240, 200, 150, 255), outline=(110, 70, 30, 255), width=8)
    d.pieslice([C - 200, C + 10, C + 200, C + 400], 180, 360, fill=(60, 110, 200, 255), outline=(20, 40, 100, 255))
    # casco de minero
    d.chord([C - 110, C - 230, C + 110, C - 30], 180, 360, fill=(230, 180, 50, 255), outline=(110, 70, 10, 255))
    d.ellipse([C - 25, C - 200, C + 25, C - 150], fill=(255, 250, 200, 255))
    finish(img, path)


if __name__ == "__main__" and len(sys.argv) > 2 and sys.argv[1] == "--ui3":
    out = sys.argv[2]
    level_node(f"{out}/node_green.png", (110, 205, 70), (30, 110, 25))
    level_node(f"{out}/node_blue.png", (80, 150, 240), (20, 60, 150))
    level_node(f"{out}/node_gray.png", (80, 82, 92), (30, 32, 40))
    rail_segment(f"{out}/rail.png")
    heart_icon(f"{out}/heart.png")
    gear_icon(f"{out}/gear.png")
    nav_map_icon(f"{out}/nav_map.png")
    nav_home_icon(f"{out}/nav_home.png")
    nav_shop_icon(f"{out}/nav_shop.png")
    nav_trophy_icon(f"{out}/nav_trophy.png")
    nav_profile_icon(f"{out}/nav_profile.png")


# ======================================================================
# Perfil: íconos y escudos de logros (Assets/Resources/UI)
# Uso: python3 Tools/gen_gems.py --ui4 Assets/Resources/UI
# ======================================================================

def _sword(d, x0, y0, x1, y1):
    d.line([(x0, y0), (x1, y1)], fill=(60, 65, 80, 255), width=46)
    d.line([(x0, y0), (x1, y1)], fill=(215, 222, 235, 255), width=30)
    # guarda y mango
    mx, my = x0 + (x1 - x0) * 0.78, y0 + (y1 - y0) * 0.78
    nx, ny = -(y1 - y0), (x1 - x0)
    ln = math.hypot(nx, ny)
    nx, ny = nx / ln * 70, ny / ln * 70
    d.line([(mx - nx, my - ny), (mx + nx, my + ny)], fill=(200, 150, 50, 255), width=26)
    d.line([(mx, my), (x1 + (x1 - x0) * 0.12, y1 + (y1 - y0) * 0.12)], fill=(120, 70, 30, 255), width=30)


def swords_icon(path):
    img, d = new()
    _sword(d, C - 170, C - 170, C + 120, C + 120)
    _sword(d, C + 170, C - 170, C - 120, C + 120)
    finish(img, path)


def target_icon(path):
    img, d = new()
    for r, col in [(210, (40, 90, 200)), (165, (235, 240, 255)), (120, (40, 90, 200)), (75, (235, 240, 255)), (35, (230, 60, 60))]:
        d.ellipse([C - r, C - r, C + r, C + r], fill=col + (255,), outline=(15, 30, 80, 255), width=6)
    d.line([(C + 10, C - 10), (C + 220, C - 220)], fill=(120, 80, 40, 255), width=18)
    d.polygon([(C + 200, C - 240), (C + 250, C - 250), (C + 240, C - 200)], fill=(230, 60, 60, 255))
    finish(img, path)


def pencil_icon(path):
    img, d = new()
    d.line([(C - 150, C + 150), (C + 140, C - 140)], fill=(40, 25, 10, 255), width=110)
    d.line([(C - 150, C + 150), (C + 140, C - 140)], fill=(250, 200, 70, 255), width=80)
    d.polygon([(C - 205, C + 205), (C - 185, C + 105), (C - 105, C + 185)], fill=(240, 220, 180, 255), outline=(40, 25, 10, 255))
    d.polygon([(C - 205, C + 205), (C - 195, C + 165), (C - 165, C + 195)], fill=(40, 40, 50, 255))
    d.line([(C + 100, C - 180), (C + 180, C - 100)], fill=(230, 120, 140, 255), width=70)
    finish(img, path)


def copy_icon(path):
    img, d = new()
    d.rounded_rectangle([C - 150, C - 190, C + 90, C + 90], radius=30, fill=(150, 160, 185, 255), outline=(40, 45, 60, 255), width=14)
    d.rounded_rectangle([C - 90, C - 110, C + 150, C + 190], radius=30, fill=(225, 230, 245, 255), outline=(40, 45, 60, 255), width=14)
    finish(img, path)


def achievement_shield(path, metal, inner, emblem):
    img, d = new()
    for side in (-1, 1):
        wing = [(C + side * 130, C - 110), (C + side * 240, C - 170), (C + side * 215, C - 40),
                (C + side * 245, C + 20), (C + side * 175, C + 90), (C + side * 125, C + 70)]
        d.polygon(wing, fill=shade(metal, 0.8), outline=shade(metal, 0.35))
    shield = [(C - 160, C - 200), (C + 160, C - 200), (C + 175, C - 20), (C + 95, C + 140), (C, C + 225),
              (C - 95, C + 140), (C - 175, C - 20)]
    faceted_gem(d, shield, metal, table_k=0.8)
    inner_pts = scale_pts(shield, 0.72)
    d.polygon(inner_pts, fill=inner + (255,), outline=shade(metal, 0.4))
    emblem(img, d)
    highlight(img, C - 60, C - 120, 55, 22)
    finish(img, path)


def _emb_star(img, d):
    pts = []
    for i in range(10):
        r = 120 if i % 2 == 0 else 52
        a = -math.pi / 2 + i * math.pi / 5
        pts.append((C + math.cos(a) * r, C - 10 + math.sin(a) * r))
    faceted_gem(d, pts, (170, 80, 240), table_k=0.5)


def _emb_swords(img, d):
    _sword(d, C - 100, C - 120, C + 80, C + 80)
    _sword(d, C + 100, C - 120, C - 80, C + 80)


def _emb_trophy(img, d):
    gold = (245, 190, 40, 255)
    d.arc([C - 125, C - 105, C - 35, C + 5], 90, 270, fill=gold, width=16)
    d.arc([C + 35, C - 105, C + 125, C + 5], 270, 90, fill=gold, width=16)
    d.polygon([(C - 85, C - 120), (C + 85, C - 120), (C + 65, C + 10), (C, C + 45), (C - 65, C + 10)], fill=gold, outline=(120, 70, 0, 255))
    d.rectangle([C - 18, C + 40, C + 18, C + 90], fill=gold)
    d.rectangle([C - 65, C + 88, C + 65, C + 120], fill=(150, 90, 40, 255))


def _emb_crown(img, d):
    crown = [(C - 120, C + 70), (C - 135, C - 70), (C - 60, C - 5), (C, C - 110), (C + 60, C - 5), (C + 135, C - 70), (C + 120, C + 70)]
    d.polygon(crown, fill=(250, 200, 50, 255), outline=(130, 80, 0, 255))
    for x in (C - 135, C, C + 135):
        y = C - 70 if x != C else C - 110
        d.ellipse([x - 16, y - 16, x + 16, y + 16], fill=(250, 220, 120, 255))
    d.rectangle([C - 120, C + 60, C + 120, C + 95], fill=(220, 160, 30, 255), outline=(130, 80, 0, 255))


if __name__ == "__main__" and len(sys.argv) > 2 and sys.argv[1] == "--ui4":
    out = sys.argv[2]
    swords_icon(f"{out}/icon_swords.png")
    target_icon(f"{out}/icon_target.png")
    pencil_icon(f"{out}/icon_pencil.png")
    copy_icon(f"{out}/icon_copy.png")
    achievement_shield(f"{out}/ach_combos.png", (225, 175, 50), (60, 30, 90), _emb_star)
    achievement_shield(f"{out}/ach_battles.png", (210, 60, 55), (70, 15, 15), _emb_swords)
    achievement_shield(f"{out}/ach_stars.png", (60, 130, 230), (15, 30, 80), _emb_trophy)
    achievement_shield(f"{out}/ach_champion.png", (150, 70, 230), (45, 15, 80), _emb_crown)


# ======================================================================
# Insignias de temporada: brillo y cinta (se tiñen por código según la temporada)
# Uso: python3 Tools/gen_gems.py --ui5 Assets/Resources/UI
# ======================================================================

def season_glow(path):
    n = 512
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    px = img.load()
    for y in range(n):
        for x in range(n):
            dx, dy = (x - n / 2) / (n / 2), (y - n / 2) / (n / 2)
            r = math.hypot(dx, dy)
            # halo suave con rayos
            a = max(0.0, 1 - r) ** 1.6
            ang = math.atan2(dy, dx)
            rays = 0.55 + 0.45 * max(0.0, math.cos(ang * 8)) ** 6
            px[x, y] = (255, 255, 255, int(255 * min(1.0, a * rays * 1.25)))
    img.save(path)


def season_ribbon(path):
    """Cinta 9-slice (borde 60 px) blanca/gris: el color lo pone el juego."""
    k = 3
    w, h = 320 * k, 120 * k
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    tail = 40 * k
    # colas
    for side in (0, 1):
        x0 = 0 if side == 0 else w - tail * 2
        pts = [(x0, 30 * k), (x0 + tail * 2, 30 * k), (x0 + tail * 2, h - 10 * k), (x0, h - 10 * k), (x0 + tail, h / 2 + 10 * k)] if side == 0 \
            else [(x0, 30 * k), (x0 + tail * 2, 30 * k), (x0 + tail, h / 2 + 10 * k), (x0 + tail * 2, h - 10 * k), (x0, h - 10 * k)]
        d.polygon(pts, fill=(150, 150, 150, 255), outline=(40, 40, 40, 255))
    d.rounded_rectangle([tail, 10 * k, w - tail, h - 30 * k], radius=14 * k, fill=(235, 235, 235, 255), outline=(40, 40, 40, 255), width=4 * k)
    d.rectangle([tail + 8 * k, 16 * k, w - tail - 8 * k, 40 * k], fill=(255, 255, 255, 255))
    img.resize((w // k, h // k), Image.LANCZOS).save(path)


if __name__ == "__main__" and len(sys.argv) > 2 and sys.argv[1] == "--ui5":
    out = sys.argv[2]
    season_glow(f"{out}/season_glow.png")
    season_ribbon(f"{out}/season_ribbon.png")
