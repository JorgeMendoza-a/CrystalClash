"""
Gráficos para Google Play: ícono 512x512 (y 1024 para Unity) y gráfico destacado 1024x500.
Uso: python3 Tools/Promo/make_store_graphics.py <carpeta_salida>
"""
import math, os, random, sys
from PIL import Image, ImageDraw, ImageFilter

sys.argv = [sys.argv[0], sys.argv[1] if len(sys.argv) > 1 else "store_out"]
OUT = sys.argv[1]
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_promo as m   # reutiliza fuentes, sprites, brillos y textos del video (sin --narrado no genera voz)

os.makedirs(OUT, exist_ok=True)
random.seed(11)

def radial(size, inner, outer, center=None, radius=None):
    w, h = size
    cx, cy = center or (w / 2, h / 2)
    radius = radius or math.hypot(w, h) / 2
    im = Image.new("RGBA", size)
    d = ImageDraw.Draw(im)
    for r in range(int(radius), 0, -4):
        k = r / radius
        col = tuple(int(inner[i] * (1 - k) + outer[i] * k) for i in range(3)) + (255,)
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col)
    return im

def rays(size, center, color, count=14, alpha=40):
    w, h = size
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    cx, cy = center
    L = math.hypot(w, h)
    for i in range(count):
        a = 2 * math.pi * i / count
        w2 = math.pi / count * 0.5
        d.polygon([(cx, cy), (cx + L * math.cos(a - w2), cy + L * math.sin(a - w2)),
                   (cx + L * math.cos(a + w2), cy + L * math.sin(a + w2))], fill=color + (alpha,))
    return layer.filter(ImageFilter.GaussianBlur(6))

def sparkle(im, x, y, s, alpha=1.0):
    m.paste_center(im, m.glow(int(s * 3), (255, 255, 255), 0.9), x, y, 1.0, 0.6 * alpha)
    layer = Image.new("RGBA", (int(s * 2), int(s * 2)), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    c = s
    d.polygon([(c, 0), (c + s * 0.18, c - s * 0.18), (2 * c, c), (c + s * 0.18, c + s * 0.18),
               (c, 2 * c), (c - s * 0.18, c + s * 0.18), (0, c), (c - s * 0.18, c - s * 0.18)], fill=(255, 255, 255, int(255 * alpha)))
    m.paste_center(im, layer, x, y)

# ------------------------------------------------------------------
# Ícono: prisma de cristal gigante sobre la mina morada, con rayos y brillos.
# Google recorta las esquinas, así que lo importante va en el centro.
# ------------------------------------------------------------------
def make_icon(size):
    S = 1024
    icon = radial((S, S), (150, 90, 255), (28, 12, 70), center=(S / 2, S * 0.46), radius=S * 0.8)
    icon.alpha_composite(rays((S, S), (S / 2, S * 0.46), (255, 220, 140), 16, 38))
    m.paste_center(icon, m.glow(900, (200, 150, 255), 1.0), S / 2, S * 0.47, 1.0, 1.0)
    # Cristales pequeños alrededor
    for rel, x, y, sz, ang in [("Minerals/sapphire.png", 0.2, 0.74, 230, -18), ("Minerals/emerald.png", 0.8, 0.74, 230, 18),
                               ("Minerals/ruby.png", 0.23, 0.25, 190, 15), ("Minerals/gold.png", 0.78, 0.25, 190, -12)]:
        m.paste_center(icon, m.scaled(rel, sz), S * x, S * y, 1.0, 1.0, ang)
    # Prisma principal
    m.paste_center(icon, m.glow(700, (255, 255, 255), 0.6), S / 2, S * 0.47, 1.0, 0.8)
    m.paste_center(icon, m.scaled("Minerals/prism.png", 640), S / 2, S * 0.48)
    for x, y, s in [(0.66, 0.3, 46), (0.35, 0.62, 30), (0.62, 0.66, 24), (0.4, 0.33, 22)]:
        sparkle(icon, S * x, S * y, s)
    # Viñeta suave en los bordes
    vig = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(vig).rectangle([0, 0, S, S], outline=(10, 4, 30, 160), width=60)
    icon.alpha_composite(vig.filter(ImageFilter.GaussianBlur(50)))
    return icon.resize((size, size), Image.LANCZOS)

# ------------------------------------------------------------------
# Gráfico destacado 1024x500: la mina de cristales, el logo y el minero.
# ------------------------------------------------------------------
def make_feature():
    W, H = 2048, 1000   # se dibuja al doble y se reduce
    banner = m.img("UI/Shop/Banners/banner_mina_cristales.png")
    s = max(W / banner.width, H / banner.height)
    bg = banner.resize((int(banner.width * s), int(banner.height * s)), Image.LANCZOS)
    x0 = (bg.width - W) // 2; y0 = (bg.height - H) // 2
    feat = bg.crop((x0, y0, x0 + W, y0 + H)).filter(ImageFilter.GaussianBlur(3))
    feat = Image.alpha_composite(feat, Image.new("RGBA", (W, H), (12, 6, 34, 120)))
    feat.alpha_composite(rays((W, H), (W * 0.42, H * 0.45), (255, 220, 150), 18, 26))
    m.paste_center(feat, m.glow(1300, (160, 110, 255), 0.9), W * 0.42, H * 0.47, 1.0, 1.0)

    # Cristales sobre el logo
    for rel, dx, dy, sz, ang in [("Minerals/sapphire.png", -330, -300, 210, -20), ("Minerals/amethyst.png", 330, -300, 210, 15),
                                 ("Minerals/emerald.png", -170, -360, 180, 10), ("Minerals/ruby.png", 170, -360, 180, -12),
                                 ("Minerals/prism.png", 0, -330, 270, 0)]:
        m.paste_center(feat, m.scaled(rel, sz), W * 0.42 + dx, H * 0.5 + dy, 1.0, 1.0, ang)
    m.text(feat, "CRYSTAL", 250, W * 0.42, H * 0.47, 1.0, 1.0, gradient=((150, 220, 255), (30, 100, 240)), stroke=14)
    m.text(feat, "CLASH", 280, W * 0.42, H * 0.72, 1.0, 1.0, gradient=(m.GOLD_TOP, m.GOLD_BOTTOM), stroke=14)
    m.text(feat, "Combina cristales · Reta a jugadores en línea", 64, W * 0.42, H * 0.91, 1.0, 1.0)

    # El minero a la derecha, saludando con cristales
    m.paste_center(feat, m.glow(800, (255, 220, 140), 0.5), W * 0.86, H * 0.68, 1.0, 0.8)
    miner = m.draw_miner(0.7, False)
    m.paste_center(feat, miner, W * 0.86, H - miner.height * 1.55 / 2 + 60, 1.55)
    m.paste_center(feat, m.scaled("Minerals/diamond.png", 170), W * 0.74, H * 0.3, 1.0, 1.0, -15)
    m.paste_center(feat, m.scaled("Minerals/gold.png", 150), W * 0.95, H * 0.22, 1.0, 1.0, 12)
    for x, y, sz in [(0.12, 0.2, 30), (0.7, 0.15, 24), (0.2, 0.8, 22), (0.62, 0.86, 26)]:
        sparkle(feat, W * x, H * y, sz)
    return feat.resize((1024, 500), Image.LANCZOS)

icon512 = make_icon(512)
icon512.convert("RGB").save(os.path.join(OUT, "icono_512.png"))          # Play Store: PNG 512x512
make_icon(1024).convert("RGB").save(os.path.join(OUT, "icono_1024.png")) # Unity (Player Settings → Icon)
make_feature().convert("RGB").save(os.path.join(OUT, "grafico_destacado_1024x500.png"))

# Vista previa: el ícono con las esquinas redondeadas como lo muestra Play Store
prev = Image.new("RGB", (512 * 2 + 60, 512 + 40), (240, 240, 245))
mask = Image.new("L", (512, 512), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, 511, 511], radius=104, fill=255)
prev.paste(icon512.convert("RGB"), (20, 20), mask)
small = icon512.convert("RGB").resize((144, 144), Image.LANCZOS)
mask_s = Image.new("L", (144, 144), 0); ImageDraw.Draw(mask_s).rounded_rectangle([0, 0, 143, 143], radius=30, fill=255)
prev.paste(small, (512 + 60, 20), mask_s)
prev.save(os.path.join(OUT, "vista_icono.jpg"))
print("listo")
