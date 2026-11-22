"""
Video promocional de Crystal Clash (vertical 1080x1920, 30 fps, ~32 s).
Usa las imágenes reales del juego (Assets/Resources) y la música/sonidos generados.

Uso:  python3 Tools/Promo/make_promo.py <carpeta_salida>
Necesita: Pillow, numpy y ffmpeg.
"""
import math, os, random, subprocess, sys, wave
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RES = os.path.join(ROOT, "Assets", "Resources")
OUT = sys.argv[1] if len(sys.argv) > 1 else "promo_out"
os.makedirs(OUT, exist_ok=True)
FRAMES = os.path.join(OUT, "frames")
os.makedirs(FRAMES, exist_ok=True)

W, H, FPS = 1080, 1920, 30
DURATION = 32.0
FONT_PATH = os.path.join(RES, "Fonts", "LilitaOne-Regular.ttf")
GOLD_TOP, GOLD_BOTTOM = (255, 236, 140), (230, 140, 20)
random.seed(4)

_font_cache = {}
def font(size):
    if size not in _font_cache:
        _font_cache[size] = ImageFont.truetype(FONT_PATH, size)
    return _font_cache[size]

_img_cache = {}
def img(rel):
    if rel not in _img_cache:
        _img_cache[rel] = Image.open(os.path.join(RES, rel)).convert("RGBA")
    return _img_cache[rel]

_scaled = {}
def scaled(rel, size):
    key = (rel, int(size))
    if key not in _scaled:
        im = img(rel)
        s = max(1, int(size))
        ratio = s / max(im.size)
        _scaled[key] = im.resize((max(1, int(im.width * ratio)), max(1, int(im.height * ratio))), Image.LANCZOS)
    return _scaled[key]

# ------------------------------------------------------------------
# Ayudas de animación
# ------------------------------------------------------------------
def clamp(x, a=0.0, b=1.0): return max(a, min(b, x))
def ease_out(t): t = clamp(t); return 1 - (1 - t) ** 3
def ease_in_out(t): t = clamp(t); return t * t * (3 - 2 * t)
def back_out(t):
    t = clamp(t); c1 = 1.70158; c3 = c1 + 1
    return 1 + c3 * (t - 1) ** 3 + c1 * (t - 1) ** 2
def prog(t, start, dur): return clamp((t - start) / dur)

def paste_center(frame, im, cx, cy, scale=1.0, alpha=1.0, angle=0.0):
    if scale <= 0.01 or alpha <= 0.01: return
    w, h = max(1, int(im.width * scale)), max(1, int(im.height * scale))
    sprite = im.resize((w, h), Image.LANCZOS) if (w, h) != im.size else im
    if angle: sprite = sprite.rotate(angle, resample=Image.BICUBIC, expand=True)
    if alpha < 1.0:
        a = sprite.getchannel("A").point(lambda v: int(v * alpha))
        sprite = sprite.copy(); sprite.putalpha(a)
    frame.alpha_composite(sprite, (int(cx - sprite.width / 2), int(cy - sprite.height / 2)))

def text_image(text, size, fill=(255, 255, 255), stroke=8, gradient=None, shadow=True):
    f = font(size)
    bbox = f.getbbox(text, stroke_width=stroke)
    w, h = bbox[2] - bbox[0] + 20, bbox[3] - bbox[1] + 30
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    ox, oy = 10 - bbox[0], 10 - bbox[1]
    if shadow:
        d.text((ox + 4, oy + 8), text, font=f, fill=(0, 0, 0, 150), stroke_width=stroke, stroke_fill=(0, 0, 0, 150))
    d.text((ox, oy), text, font=f, fill=(30, 18, 5, 255), stroke_width=stroke, stroke_fill=(30, 18, 5, 255))
    if gradient:
        mask = Image.new("L", (w, h), 0)
        ImageDraw.Draw(mask).text((ox, oy), text, font=f, fill=255)
        top, bottom = gradient
        grad = Image.new("RGBA", (w, h))
        gd = ImageDraw.Draw(grad)
        for y in range(h):
            k = y / max(1, h - 1)
            gd.line([(0, y), (w, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * k) for i in range(3)) + (255,))
        im.paste(grad, (0, 0), mask)
    else:
        d.text((ox, oy), text, font=f, fill=fill)
    return im

_text_cache = {}
def text(frame, s, size, cx, cy, scale=1.0, alpha=1.0, fill=(255, 255, 255), gradient=None, stroke=8):
    key = (s, size, fill, gradient, stroke)
    if key not in _text_cache: _text_cache[key] = text_image(s, size, fill, stroke, gradient)
    paste_center(frame, _text_cache[key], cx, cy, scale, alpha)

def glow(size, color, strength=1.0):
    key = ("glow", size, color, strength)
    if key not in _scaled:
        im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        for r in range(size // 2, 0, -4):
            a = int(255 * strength * (1 - r / (size / 2)) ** 2)
            d.ellipse([size / 2 - r, size / 2 - r, size / 2 + r, size / 2 + r], fill=color + (a,))
        _scaled[key] = im
    return _scaled[key]

# ------------------------------------------------------------------
# Fondo: la mina de cristales (banner ampliado, oscurecido y desenfocado)
# ------------------------------------------------------------------
def make_background():
    banner = img("UI/Shop/Banners/banner_mina_cristales.png")
    s = H * 1.15 / banner.height
    big = banner.resize((int(banner.width * s), int(banner.height * s)), Image.LANCZOS).filter(ImageFilter.GaussianBlur(10))
    dark = Image.new("RGBA", big.size, (8, 4, 24, 150))
    big = Image.alpha_composite(big, dark)
    return big

BG = make_background()
PARTICLES = [(random.uniform(0, W), random.uniform(0, H), random.uniform(0.4, 1.0), random.uniform(10, 40)) for _ in range(40)]

def background(t):
    # Paneo lento de izquierda a derecha
    max_x = BG.width - W
    x = int((math.sin(t * 0.08) * 0.5 + 0.5) * max_x)
    y = int((BG.height - H) / 2)
    frame = BG.crop((x, y, x + W, y + H)).copy()
    # Brillitos que suben
    for px, py, sp, sz in PARTICLES:
        yy = (py - t * 60 * sp) % H
        a = 0.35 + 0.35 * math.sin(t * 3 + px)
        paste_center(frame, glow(int(sz * 2), (190, 150, 255), 0.9), px, yy, 1.0, a)
    # Viñeta
    return frame

VIGNETTE = Image.new("RGBA", (W, H), (0, 0, 0, 0))
_vd = ImageDraw.Draw(VIGNETTE)
for i in range(60):
    a = int(3.2 * i)
    _vd.rectangle([i * 6, i * 9, W - i * 6, H - i * 9], outline=(0, 0, 0, max(0, 180 - a)), width=10)
VIGNETTE = VIGNETTE.filter(ImageFilter.GaussianBlur(30))

# ------------------------------------------------------------------
# Tablero simulado (para la escena de juego)
# ------------------------------------------------------------------
TYPES = ["Minerals/ruby.png", "Minerals/sapphire.png", "Minerals/emerald.png",
         "Minerals/amethyst.png", "Minerals/gold.png", "Minerals/diamond.png"]
BOMB = "Minerals/bomb.png"
N = 8
CELL = 118
BOARD_X = (W - N * CELL) // 2
BOARD_Y = 640

def cell_center(x, y): return BOARD_X + x * CELL + CELL / 2, BOARD_Y + y * CELL + CELL / 2

def random_grid():
    g = [[0] * N for _ in range(N)]
    for y in range(N):
        for x in range(N):
            while True:
                v = random.randrange(len(TYPES))
                if x >= 2 and g[y][x - 1] == v and g[y][x - 2] == v: continue
                if y >= 2 and g[y - 1][x] == v and g[y - 2][x] == v: continue
                break
            g[y][x] = v
    return g

def find_matches(g):
    hit = set()
    for y in range(N):
        for x in range(N - 2):
            v = g[y][x]
            if v is not None and v >= 0 and g[y][x + 1] == v and g[y][x + 2] == v:
                hit |= {(x, y), (x + 1, y), (x + 2, y)}
    for x in range(N):
        for y in range(N - 2):
            v = g[y][x]
            if v is not None and v >= 0 and g[y + 1][x] == v and g[y + 2][x] == v:
                hit |= {(x, y), (x, y + 1), (x, y + 2)}
    return hit

class Board:
    """Guarda una línea de tiempo de 'estados' y animaciones simples para dibujar."""
    def __init__(self):
        self.grid = random_grid()
        # Preparamos jugadas seguras: fila 5 -> ruby ruby _ ruby con el que cambia abajo
        g = self.grid
        g[5][1] = g[5][2] = 0; g[5][3] = 3; g[6][3] = 0      # jugada 1: (3,6) sube a (3,5)
        g[2][4] = g[3][4] = 1; g[4][5] = 1; g[4][4] = 4      # jugada 2: (5,4) a (4,4)
        g[1][6] = -1                                          # dinamita
        self.events = []   # (start, kind, data)

    def snapshot(self):
        return [row[:] for row in self.grid]

board = Board()

def plan_board_scene(start):
    """Calcula las jugadas y devuelve una lista de 'pasos' con tiempos."""
    steps = []
    t = start
    g = board.grid

    def do_swap(a, b):
        nonlocal t
        before = [row[:] for row in g]
        (ax, ay), (bx, by) = a, b
        g[ay][ax], g[by][bx] = g[by][bx], g[ay][ax]
        steps.append(("swap", t, 0.25, before, a, b)); t += 0.25
        cascade()

    def clear_and_fall(hit, kind="clear"):
        nonlocal t
        before = [row[:] for row in g]
        steps.append((kind, t, 0.3, before, hit)); t += 0.3
        for (x, y) in hit: g[y][x] = None
        # Gravedad
        falls = {}
        for x in range(N):
            col = [g[y][x] for y in range(N) if g[y][x] is not None]
            missing = N - len(col)
            new = [random.randrange(len(TYPES)) for _ in range(missing)]
            newcol = new + col
            # distancia que cae cada pieza (en celdas)
            src_rows = [y for y in range(N) if g[y][x] is not None]
            for i, y in enumerate(src_rows):
                falls[(x, missing + i)] = missing + i - y
            for i in range(missing):
                falls[(x, i)] = missing  # vienen de arriba
            for y in range(N): g[y][x] = newcol[y]
        steps.append(("fall", t, 0.35, [row[:] for row in g], falls)); t += 0.35

    def cascade():
        level = 1
        while True:
            hit = find_matches(g)
            if not hit: break
            steps.append(("score", t, 0, len(hit) * 30 * level)); 
            clear_and_fall(hit)
            level += 1

    t += 0.6
    do_swap((3, 6), (3, 5))
    t += 0.5
    do_swap((5, 4), (4, 4))
    t += 0.5
    # Dinamita: explota 3x3 alrededor de (6,1)
    hit = {(x, y) for x in range(5, 8) for y in range(0, 3)}
    steps.append(("boom", t, 0, (6, 1)))
    steps.append(("score", t, 0, 600))
    clear_and_fall(hit, kind="explode")
    cascade()
    return steps, t

BOARD_START = 3.0
BOARD_STEPS, BOARD_END = plan_board_scene(BOARD_START)

def draw_board(frame, t):
    # Marco
    pad = 22
    frame_box = Image.new("RGBA", (N * CELL + pad * 2, N * CELL + pad * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(frame_box)
    d.rounded_rectangle([0, 0, frame_box.width - 1, frame_box.height - 1], radius=36, fill=(20, 14, 45, 215), outline=(230, 180, 70, 255), width=8)
    for y in range(N):
        for x in range(N):
            if (x + y) % 2 == 0:
                d.rectangle([pad + x * CELL, pad + y * CELL, pad + (x + 1) * CELL - 1, pad + (y + 1) * CELL - 1], fill=(255, 255, 255, 14))
    frame.alpha_composite(frame_box, (BOARD_X - pad, BOARD_Y - pad))

    # Estado que corresponde al tiempo t
    grid = None; anim = None
    for st in BOARD_STEPS:
        kind, start = st[0], st[1]
        if kind in ("score", "boom"): continue
        dur = st[2]
        if t < start:
            if grid is None: grid = st[3]
            break
        if start <= t < start + dur:
            anim = st; grid = st[3]; break
        grid = st[3] if kind == "fall" else None
    if grid is None:
        # después del último paso: el último 'fall'
        falls = [s for s in BOARD_STEPS if s[0] == "fall" and s[1] <= t]
        grid = falls[-1][3] if falls else BOARD_STEPS[0][3]

    size = CELL * 0.88
    target = frame
    frame = Image.new("RGBA", (W, H), (0, 0, 0, 0))   # las piezas se dibujan aparte para recortarlas al tablero
    for y in range(N):
        for x in range(N):
            v = grid[y][x]
            if v is None: continue
            rel = BOMB if v == -1 else TYPES[v]
            cx, cy = cell_center(x, y)
            scale, alpha = 1.0, 1.0
            if anim:
                kind, start, dur = anim[0], anim[1], anim[2]
                k = (t - start) / dur
                if kind == "swap":
                    a, b = anim[4], anim[5]
                    if (x, y) == a: tx, ty = cell_center(*b); cx += (tx - cx) * ease_in_out(k); cy += (ty - cy) * ease_in_out(k)
                    elif (x, y) == b: tx, ty = cell_center(*a); cx += (tx - cx) * ease_in_out(k); cy += (ty - cy) * ease_in_out(k)
                elif kind in ("clear", "explode") and (x, y) in anim[4]:
                    scale = 1.0 + 0.25 * math.sin(k * math.pi) - k
                    alpha = 1 - k * 0.7
                elif kind == "fall":
                    dist = anim[4].get((x, y), 0)
                    cy -= dist * CELL * (1 - ease_out(k))
            paste_center(frame, scaled(rel, size), cx, cy, max(0.0, scale))
            # Destellos al romper
            if anim and anim[0] in ("clear", "explode") and (x, y) in anim[4]:
                k = (t - anim[1]) / anim[2]
                paste_center(frame, glow(160, (255, 230, 150), 1.0), cx, cy, 0.5 + k, 1 - k)

    box = (BOARD_X, BOARD_Y, BOARD_X + N * CELL, BOARD_Y + N * CELL)
    target.alpha_composite(frame.crop(box), (BOARD_X, BOARD_Y))
    frame = target

    # Explosión de la dinamita
    for st in BOARD_STEPS:
        if st[0] == "boom" and st[1] <= t < st[1] + 0.6:
            k = (t - st[1]) / 0.6
            cx, cy = cell_center(*st[3])
            # Destello blanco sobre el tablero y bola de fuego
            flash = Image.new("RGBA", (N * CELL, N * CELL), (255, 245, 220, int(170 * (1 - k) ** 2)))
            frame.alpha_composite(flash, (BOARD_X, BOARD_Y))
            paste_center(frame, glow(900, (255, 150, 40), 1.0), cx, cy, 0.5 + k * 1.3, 1 - k)
            paste_center(frame, glow(500, (255, 250, 210), 1.0), cx, cy, 0.4 + k * 0.8, (1 - k) ** 1.5)
            ring = Image.new("RGBA", (800, 800), (0, 0, 0, 0))
            ImageDraw.Draw(ring).ellipse([40, 40, 760, 760], outline=(255, 240, 200, 255), width=18)
            paste_center(frame, ring, cx, cy, 0.2 + k * 0.9, 1 - k)

def board_score(t):
    return sum(st[3] for st in BOARD_STEPS if st[0] == "score" and st[1] <= t)

# ------------------------------------------------------------------
# Escenas
# ------------------------------------------------------------------
def banner_card(frame, rel_banner, rel_avatar, label, cx, cy, width, alpha=1.0, color=(255, 255, 255)):
    b = scaled(rel_banner, width)
    paste_center(frame, b, cx, cy, 1.0, alpha)
    a = scaled(rel_avatar, b.height * 0.82)
    paste_center(frame, a, cx - b.width * 0.30, cy, 1.0, alpha)
    text(frame, label, 70, cx + b.width * 0.12, cy, 1.0, alpha, fill=color)

def scene_intro(frame, t):
    # Cristales que entran volando y el logo que rebota
    pieces = [("Minerals/sapphire.png", -300, 560, -20), ("Minerals/amethyst.png", 300, 520, 15),
              ("Minerals/emerald.png", -180, 380, 10), ("Minerals/ruby.png", 200, 360, -12), ("Minerals/prism.png", 0, 450, 0)]
    for i, (rel, dx, y, ang) in enumerate(pieces):
        k = back_out(prog(t, 0.1 + i * 0.12, 0.6))
        size = 300 if "prism" in rel else 220
        paste_center(frame, glow(420, (170, 120, 255), 0.6), W / 2 + dx, y + 40, k, k * 0.7)
        paste_center(frame, scaled(rel, size), W / 2 + dx, y + math.sin(t * 2 + i) * 10, k, 1.0, ang)
    k = back_out(prog(t, 0.6, 0.7))
    text(frame, "CRYSTAL", 210, W / 2, 860, k, 1.0, gradient=((150, 220, 255), (30, 100, 240)), stroke=12)
    text(frame, "CLASH", 230, W / 2, 1080, k, 1.0, gradient=(GOLD_TOP, GOLD_BOTTOM), stroke=12)
    k2 = prog(t, 1.6, 0.6)
    text(frame, "¡El juego de cristales más épico!", 64, W / 2, 1290, 1.0, k2)
    # Brillo que cruza el logo
    sx = -400 + 1900 * prog(t, 1.2, 1.0)
    paste_center(frame, glow(500, (255, 255, 255), 0.5), sx, 960, 1.0, 0.6 * (1 - abs(prog(t, 1.2, 1.0) - 0.5) * 2))

def scene_board(frame, t):
    k = ease_out(prog(t, BOARD_START, 0.4))
    text(frame, "¡COMBINA CRISTALES!", 92, W / 2, 330 - (1 - k) * 80, 1.0, k, gradient=(GOLD_TOP, GOLD_BOTTOM), stroke=10)
    text(frame, f"PUNTOS  {board_score(t)}", 70, W / 2, 500, 1.0, k)
    draw_board(frame, t)
    k3 = prog(t, BOARD_START + 1.5, 0.5)
    text(frame, "100 niveles  ·  4 mundos", 66, W / 2, 1680, 1.0, k3)

def scene_specials(frame, t, start):
    k = ease_out(prog(t, start, 0.4))
    text(frame, "CREA ESPECIALES", 112, W / 2, 380, 1.0, k, gradient=(GOLD_TOP, GOLD_BOTTOM), stroke=10)
    text(frame, "EXPLOSIVOS", 112, W / 2, 520, 1.0, k, gradient=((255, 170, 120), (220, 50, 30)), stroke=10)
    items = [("Minerals/drill_h.png", "Taladro", "4 en línea"), ("Minerals/bomb.png", "Dinamita", "en L o T"), ("Minerals/prism.png", "Prisma", "5 en línea")]
    for i, (rel, name, how) in enumerate(items):
        y = 830 + i * 330
        kk = back_out(prog(t, start + 0.4 + i * 0.35, 0.5))
        pulse = 1 + 0.06 * math.sin((t - start) * 6 + i)
        paste_center(frame, glow(420, (255, 200, 90) if i != 2 else (200, 140, 255), 0.8), 300, y, kk * pulse, kk)
        paste_center(frame, scaled(rel, 250), 300, y, kk * pulse)
        text(frame, name, 96, 680, y - 40, kk, 1.0)
        text(frame, how, 60, 680, y + 55, kk, 1.0, fill=(200, 190, 255))

def scene_versus(frame, t, start):
    k = ease_out(prog(t, start, 0.4))
    text(frame, "RETA A JUGADORES", 104, W / 2, 330, 1.0, k, gradient=(GOLD_TOP, GOLD_BOTTOM), stroke=10)
    text(frame, "EN LÍNEA  ·  1 VS 1", 90, W / 2, 470, 1.0, k)
    k1 = ease_out(prog(t, start + 0.3, 0.5))
    banner_card(frame, "UI/Shop/Banners/banner_volcan_ardiente.png", "UI/Shop/Avatars/avatar_dragon_fuego.png", "TÚ",
                W / 2 - (1 - k1) * 1200, 820, 1000)
    banner_card(frame, "UI/Shop/Banners/banner_cueva_hielo.png", "UI/Shop/Avatars/avatar_yeti_hielo.png", "RIVAL",
                W / 2 + (1 - k1) * 1200, 1330, 1000)
    kv = back_out(prog(t, start + 0.9, 0.35))
    slam = prog(t, start + 0.9, 0.5)
    paste_center(frame, glow(900, (255, 200, 80), 1.0), W / 2, 1075, 0.3 + slam, (1 - slam) * 0.9)
    text(frame, "VS", 260, W / 2, 1075, kv * (1 + 0.04 * math.sin(t * 8)), 1.0, gradient=(GOLD_TOP, GOLD_BOTTOM), stroke=14)
    k4 = prog(t, start + 1.8, 0.5)
    text(frame, "Mismo tablero para los dos", 56, W / 2, 1620, 1.0, k4)
    text(frame, "¡Gana quien haga más puntos!", 56, W / 2, 1700, 1.0, k4)

RANKS = ["UI/Ranks/rango_bronce_1.png", "UI/Ranks/rango_plata_1.png", "UI/Ranks/rango_oro_1.png",
         "UI/Ranks/rango_platino_1.png", "UI/Ranks/rango_diamante_1.png", "UI/Ranks/rango_maestro_1.png", "UI/Ranks/rango_campeon.png"]
RANK_NAMES = ["BRONCE", "PLATA", "ORO", "PLATINO", "DIAMANTE", "MAESTRO", "CAMPEÓN"]

def scene_ranks(frame, t, start):
    k = ease_out(prog(t, start, 0.4))
    text(frame, "SUBE DE RANGO", 116, W / 2, 330, 1.0, k, gradient=(GOLD_TOP, GOLD_BOTTOM), stroke=10)
    text(frame, "Temporadas con premios exclusivos", 56, W / 2, 460, 1.0, k)
    for i, rel in enumerate(RANKS[:-1]):
        kk = back_out(prog(t, start + 0.3 + i * 0.22, 0.4))
        col, row = i % 3, i // 3
        x = 220 + col * 320
        y = 690 + row * 360
        paste_center(frame, scaled(rel, 250), x, y, kk)
        text(frame, RANK_NAMES[i], 46, x, y + 150, kk, 1.0)
    kc = back_out(prog(t, start + 1.8, 0.6))
    pulse = 1 + 0.05 * math.sin((t - start) * 5)
    paste_center(frame, glow(800, (210, 140, 255), 1.0), W / 2, 1520, kc * pulse, kc)
    paste_center(frame, scaled(RANKS[-1], 360), W / 2, 1480, kc * pulse)
    text(frame, "CAMPEÓN", 90, W / 2, 1700, kc, 1.0, gradient=((240, 200, 255), (160, 80, 240)), stroke=10)

AVATARS = ["avatar_rey_lich", "avatar_golem_lava", "avatar_hechicero_cristal", "avatar_demonio_mina",
           "avatar_ninja_minero", "avatar_golem_oro", "avatar_caballero_obsidiana", "avatar_zombie_minero", "avatar_dragon_fuego"]

def scene_profile(frame, t, start):
    k = ease_out(prog(t, start, 0.4))
    text(frame, "PERSONALIZA", 120, W / 2, 330, 1.0, k, gradient=(GOLD_TOP, GOLD_BOTTOM), stroke=10)
    text(frame, "TU PERFIL", 120, W / 2, 470, 1.0, k, gradient=(GOLD_TOP, GOLD_BOTTOM), stroke=10)
    for i, name in enumerate(AVATARS):
        kk = back_out(prog(t, start + 0.25 + i * 0.1, 0.4))
        col, row = i % 3, i // 3
        paste_center(frame, scaled("UI/Shop/Avatars/" + name + ".png", 280), 220 + col * 320, 720 + row * 300, kk)
    kb = ease_out(prog(t, start + 1.3, 0.6))
    paste_center(frame, scaled("UI/Shop/Banners/banner_tesoro_del_rey.png", 980), W / 2 + (1 - kb) * 1200, 1620, 1.0)
    text(frame, "Avatares épicos y banners legendarios", 52, W / 2, 1820, 1.0, kb)

def scene_outro(frame, t, start):
    k = back_out(prog(t, start, 0.7))
    paste_center(frame, glow(900, (150, 110, 255), 0.7), W / 2, 700, k, k)
    paste_center(frame, scaled("Minerals/prism.png", 300), W / 2, 470, k)
    text(frame, "CRYSTAL", 200, W / 2, 760, k, 1.0, gradient=((150, 220, 255), (30, 100, 240)), stroke=12)
    text(frame, "CLASH", 220, W / 2, 970, k, 1.0, gradient=(GOLD_TOP, GOLD_BOTTOM), stroke=12)
    feats = ["100 niveles", "Clasificatoria 1 vs 1", "Amigos y torneo semanal", "¡Gratis!"]
    for i, f in enumerate(feats):
        kk = ease_out(prog(t, start + 0.6 + i * 0.2, 0.4))
        text(frame, f, 64, W / 2, 1180 + i * 95, 1.0, kk)
    kp = back_out(prog(t, start + 1.6, 0.5))
    pill = Image.new("RGBA", (860, 150), (0, 0, 0, 0))
    ImageDraw.Draw(pill).rounded_rectangle([0, 0, 859, 149], radius=75, fill=(60, 160, 60, 255), outline=(255, 255, 255, 255), width=6)
    paste_center(frame, pill, W / 2, 1640, kp * (1 + 0.03 * math.sin(t * 6)))
    text(frame, "MUY PRONTO EN GOOGLE PLAY", 58, W / 2, 1640, kp * (1 + 0.03 * math.sin(t * 6)), 1.0)

SCENES = [
    (0.0, BOARD_START, lambda f, t: scene_intro(f, t)),
    (BOARD_START, BOARD_END + 0.6, lambda f, t: scene_board(f, t)),
]
s = BOARD_END + 0.6
SPECIALS = s; s += 4.0
VERSUS = s; s += 4.5
RANKS_T = s; s += 4.2
PROFILE = s; s += 4.0
OUTRO = s
DURATION = OUTRO + 4.5
SCENES += [
    (SPECIALS, VERSUS, lambda f, t: scene_specials(f, t, SPECIALS)),
    (VERSUS, RANKS_T, lambda f, t: scene_versus(f, t, VERSUS)),
    (RANKS_T, PROFILE, lambda f, t: scene_ranks(f, t, RANKS_T)),
    (PROFILE, OUTRO, lambda f, t: scene_profile(f, t, PROFILE)),
    (OUTRO, DURATION, lambda f, t: scene_outro(f, t, OUTRO)),
]

def render():
    total = int(DURATION * FPS)
    for i in range(total):
        t = i / FPS
        frame = background(t)
        for start, end, fn in SCENES:
            if start <= t < end:
                layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
                fn(layer, t)
                # Fundido entre escenas
                fade = min(clamp((t - start) / 0.25), clamp((end - t) / 0.25)) if start > 0 else clamp((end - t) / 0.25)
                if fade < 1:
                    a = layer.getchannel("A").point(lambda v: int(v * fade)); layer.putalpha(a)
                frame.alpha_composite(layer)
        frame.alpha_composite(VIGNETTE)
        if t < 0.4:  # fundido desde negro
            frame = Image.blend(Image.new("RGBA", (W, H), (0, 0, 0, 255)), frame, t / 0.4)
        if t > DURATION - 0.6:
            frame = Image.blend(frame, Image.new("RGBA", (W, H), (0, 0, 0, 255)), (t - (DURATION - 0.6)) / 0.6)
        frame.convert("RGB").save(os.path.join(FRAMES, f"f{i:05d}.jpg"), quality=92)
    return total

# ------------------------------------------------------------------
# Audio: música + efectos en los momentos clave
# ------------------------------------------------------------------
def read_wav(rel):
    with wave.open(os.path.join(RES, "Audio", rel)) as w:
        sr = w.getframerate()
        x = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float32) / 32768
    if sr != 44100:
        idx = np.arange(0, len(x), sr / 44100.0)
        x = np.interp(idx, np.arange(len(x)), x)
    return x

def make_audio(path):
    sr = 44100
    n = int(DURATION * sr)
    out = np.zeros(n, np.float32)
    music = read_wav("music_game.wav")
    reps = int(np.ceil(n / len(music)))
    m = np.tile(music, reps)[:n] * 0.45
    fade = int(1.5 * sr)
    m[-fade:] *= np.linspace(1, 0, fade)
    m[: int(0.5 * sr)] *= np.linspace(0, 1, int(0.5 * sr))
    out += m

    def add(rel, t, vol=0.8, pitch=1.0):
        x = read_wav(rel)
        if pitch != 1.0:
            idx = np.arange(0, len(x), pitch); x = np.interp(idx, np.arange(len(x)), x)
        i = int(t * sr); j = min(n, i + len(x))
        if i < n: out[i:j] += x[: j - i] * vol

    add("match_found.wav", 0.6, 0.6)
    level = 0
    for st in BOARD_STEPS:
        if st[0] == "swap": add("swap.wav", st[1], 0.6); level = 0
        elif st[0] == "clear": add("match.wav", st[1], 0.8, 1 + 0.1 * level); level += 1
        elif st[0] == "boom": add("bomb.wav", st[1], 0.9)
    for i in range(3): add("special_create.wav", SPECIALS + 0.4 + i * 0.35, 0.6)
    add("match_found.wav", VERSUS + 0.9, 0.9)
    for i in range(6): add("star.wav", RANKS_T + 0.3 + i * 0.22, 0.45, 1 + 0.05 * i)
    add("win.wav", RANKS_T + 1.8, 0.8)
    for i in range(0, 9, 2): add("coin.wav", PROFILE + 0.25 + i * 0.1, 0.4)
    add("win.wav", OUTRO + 0.1, 0.8)

    peak = np.max(np.abs(out)) or 1
    out = np.tanh(out / peak * 1.3) / np.tanh(1.3) * 0.9
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((out * 32767).astype(np.int16).tobytes())

if __name__ == "__main__":
    total = render()
    audio = os.path.join(OUT, "audio.wav")
    make_audio(audio)
    video = os.path.join(OUT, "crystal_clash_promo.mp4")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", os.path.join(FRAMES, "f%05d.jpg"),
                    "-i", audio, "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p",
                    "-c:a", "aac", "-b:a", "160k", "-shortest", "-movflags", "+faststart", video], check=True)
    print(f"{total} cuadros, {DURATION:.1f} s -> {video}")
