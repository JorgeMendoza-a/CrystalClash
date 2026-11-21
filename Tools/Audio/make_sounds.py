import numpy as np, wave, sys, os
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)

def save(name, x, sr):
    x = np.asarray(x, dtype=np.float64)
    peak = np.max(np.abs(x)) or 1.0
    x = x / peak * 0.89
    x = np.tanh(x * 1.2) / np.tanh(1.2)
    if not name.startswith("music"):
        fade = min(len(x), int(0.008 * sr))
        x[-fade:] *= np.linspace(1, 0, fade)
    data = (x * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr); w.writeframes(data.tobytes())

def t_axis(dur, sr): return np.arange(int(dur * sr)) / sr
def note_hz(n): return 440.0 * 2 ** ((n - 69) / 12.0)   # MIDI -> Hz

def env(t, a=0.005, d=0.3, s=0.0, r=0.0, dur=None):
    e = np.where(t < a, t / a, np.exp(-(t - a) / max(d, 1e-4)) * (1 - s) + s)
    if dur is not None and r > 0:
        e = e * np.clip((dur - t) / r, 0, 1)
    return e

def bell(freq, dur, sr, decay=0.6, bright=1.0):
    t = t_axis(dur, sr)
    partials = [(1, 1.0), (2.0, 0.5 * bright), (2.76, 0.35 * bright), (4.07, 0.2 * bright), (5.43, 0.1 * bright)]
    x = np.zeros_like(t)
    for ratio, amp in partials:
        x += amp * np.sin(2 * np.pi * freq * ratio * t) * np.exp(-t / (decay / ratio ** 0.6))
    return x * env(t, a=0.002, d=10)

def pluck(freq, dur, sr, damp=0.996):
    n = int(dur * sr); period = max(2, int(sr / freq))
    buf = rng.uniform(-1, 1, period); out = np.zeros(n)
    for i in range(n):
        out[i] = buf[i % period]
        buf[i % period] = damp * 0.5 * (buf[i % period] + buf[(i + 1) % period])
    return out

def mix(total, sr, parts):
    out = np.zeros(int(total * sr))
    for start, sig in parts:
        i = int(start * sr); j = min(len(out), i + len(sig))
        out[i:j] += sig[: j - i]
    return out

def lowpass(x, alpha):
    y = np.zeros_like(x); acc = 0.0
    for i, v in enumerate(x):
        acc += alpha * (v - acc); y[i] = acc
    return y

SR = 44100

# Botón: blip suave
t = t_axis(0.07, SR)
save("click", np.sin(2*np.pi*(900 + 500*np.exp(-t*60))*t) * env(t, 0.002, 0.025), SR)

# Intercambio: whoosh corto (ruido filtrado con barrido)
t = t_axis(0.16, SR)
n = rng.uniform(-1, 1, len(t))
sweep = lowpass(n, 0.08) * np.sin(np.pi * t / 0.16) ** 2
save("swap", sweep + 0.25*np.sin(2*np.pi*(300+900*t/0.16)*t)*np.sin(np.pi*t/0.16), SR)

# Movimiento inválido: dos golpes graves
def bonk(f, d=0.12):
    t = t_axis(d, SR); return np.sin(2*np.pi*f*t) * env(t, 0.002, 0.05) + 0.3*np.sin(2*np.pi*f*2*t)*env(t,0.002,0.02)
save("invalid", mix(0.3, SR, [(0, bonk(220)), (0.12, bonk(180))]), SR)

# Combinación: campanita de cristal (acorde brillante)
save("match", mix(0.6, SR, [(0, bell(note_hz(84), 0.6, SR, 0.25)), (0.02, 0.6*bell(note_hz(91), 0.55, SR, 0.2))]), SR)

# Crear especial: arpegio ascendente con brillo
parts = [(i*0.055, 0.8*bell(note_hz(n), 0.5, SR, 0.25)) for i, n in enumerate([79, 83, 86, 91, 95])]
save("special_create", mix(0.8, SR, parts), SR)

# Taladro (línea): zap láser con barrido
t = t_axis(0.45, SR)
f = 1800*np.exp(-t*6) + 150
phase = 2*np.pi*np.cumsum(f)/SR
zap = (np.sign(np.sin(phase))*0.4 + np.sin(phase)) * env(t, 0.003, 0.18)
save("line", zap + 0.3*lowpass(rng.uniform(-1,1,len(t)),0.3)*env(t,0.002,0.08), SR)

# Dinamita: explosión
t = t_axis(0.9, SR)
noise = lowpass(rng.uniform(-1, 1, len(t)), 0.12) * env(t, 0.002, 0.25)
thump = np.sin(2*np.pi*(120*np.exp(-t*8)+40)*t) * env(t, 0.002, 0.18)
save("bomb", noise*1.2 + thump*1.4, SR)

# Prisma: brillo mágico (barrido de campanitas)
parts = [(i*0.03, 0.5*bell(note_hz(n), 0.7, SR, 0.3)) for i, n in enumerate([72, 76, 79, 84, 88, 91, 96, 100])]
t = t_axis(1.0, SR)
shimmer = np.sin(2*np.pi*(2000+1500*np.sin(2*np.pi*7*t))*t) * env(t, 0.05, 0.3) * 0.15
save("prism", mix(1.0, SR, parts) + shimmer, SR)

# Potenciador: golpe + destello
t = t_axis(0.5, SR)
hit = np.sin(2*np.pi*(200*np.exp(-t*10)+60)*t) * env(t, 0.002, 0.12)
save("booster", mix(0.5, SR, [(0, hit*1.2), (0.03, 0.6*bell(note_hz(88), 0.45, SR, 0.2))]), SR)

# Estrella / premio: ding
save("star", mix(0.7, SR, [(0, bell(note_hz(96), 0.7, SR, 0.35)), (0, 0.4*bell(note_hz(103), 0.6, SR, 0.25))]), SR)

# Moneda / compra: ching ching
save("coin", mix(0.45, SR, [(0, 0.8*bell(note_hz(98), 0.3, SR, 0.12)), (0.08, bell(note_hz(103), 0.37, SR, 0.15))]), SR)

# Victoria: fanfarria (arpegio mayor + acorde final)
notes = [(0.00, 72), (0.12, 76), (0.24, 79), (0.36, 84)]
parts = [(s, bell(note_hz(n), 0.6, SR, 0.3)) for s, n in notes]
parts += [(0.52, 0.9*bell(note_hz(n), 1.4, SR, 0.7)) for n in (72, 76, 79, 84, 88)]
parts += [(0.52 + i*0.0, 0.25*pluck(note_hz(n), 1.2, SR)) for i, n in enumerate((48, 55, 60))]
save("win", mix(2.1, SR, parts), SR)

# Derrota: notas que bajan
parts = [(i*0.28, bell(note_hz(n), 0.7, SR, 0.4, bright=0.5)) for i, n in enumerate([67, 64, 60, 55])]
save("lose", mix(1.7, SR, parts), SR)

# Rival encontrado: golpe dramático + subida
t = t_axis(1.1, SR)
rise = np.sin(2*np.pi*np.cumsum(200 + 600*t/1.1)/SR) * np.clip(t/0.9, 0, 1) * env(t, 0.5, 10) * 0.25
hitn = lowpass(rng.uniform(-1,1,len(t)), 0.2) * env(t, 0.002, 0.15)
boom = np.sin(2*np.pi*(90*np.exp(-t*5)+45)*t) * env(t, 0.002, 0.35)
save("match_found", hitn + boom*1.3 + rise + mix(1.1, SR, [(0.75, 0.6*bell(note_hz(84), 0.35, SR, 0.2))]), SR)

# ------------------------------------------------------------------
# Música (loops sin corte): 22050 Hz para que pese poco
# ------------------------------------------------------------------
def music(name, bpm, chords, melody, bass_oct=36, sr=22050, bars_per_chord=1):
    beat = 60.0 / bpm
    bar = beat * 4
    length = bar * bars_per_chord * len(chords)
    tail = 2.0
    out = np.zeros(int((length + tail) * sr))

    def add(start, sig):
        i = int(start * sr); j = min(len(out), i + len(sig)); out[i:j] += sig[: j - i]

    for ci, chord in enumerate(chords):
        cstart = ci * bar * bars_per_chord
        cdur = bar * bars_per_chord
        # Colchón suave (pad)
        t = t_axis(cdur + 0.5, sr)
        pad = np.zeros_like(t)
        for n in chord:
            f = note_hz(n)
            pad += np.sin(2*np.pi*f*t) + 0.3*np.sin(2*np.pi*f*2.003*t)
        pad *= np.clip(t/0.4, 0, 1) * np.clip((cdur + 0.5 - t)/0.5, 0, 1) * 0.06
        add(cstart, pad)
        # Bajo pulsado
        for b in range(4 * bars_per_chord):
            add(cstart + b*beat, 0.35*pluck(note_hz(chord[0] - 12 if chord[0] > bass_oct + 12 else chord[0]), beat*0.95, sr, 0.993))
        # Arpegio de cristal en corcheas
        arp = sorted(chord) + [chord[0] + 12]
        for k in range(8 * bars_per_chord):
            n = arp[k % len(arp)] + 12
            add(cstart + k*beat/2, 0.10*bell(note_hz(n), 0.6, sr, 0.25, bright=0.6))
    # Melodía
    for start_beats, n, dur_beats in melody:
        add(start_beats*beat, 0.22*bell(note_hz(n), dur_beats*beat + 0.6, sr, 0.5, bright=0.8))
    # Percusión suave (tic en los tiempos)
    for b in range(int(length / beat)):
        t = t_axis(0.05, sr)
        tick = lowpass(rng.uniform(-1, 1, len(t)), 0.6) * env(t, 0.001, 0.012) * (0.05 if b % 2 else 0.08)
        add(b*beat, tick)

    # Cerrar el loop: la cola vuelve al principio
    n = int(length * sr)
    loop = out[:n].copy()
    loop[: len(out) - n] += out[n:]
    save(name, loop, sr)

# Menú: tranquila, Do mayor (C - Am - F - G)
C, Am, F, G = [60, 64, 67], [57, 60, 64], [53, 57, 60], [55, 59, 62]
mel = [(0,76,1),(1,79,1),(2,84,2),(4,81,1),(5,79,1),(6,76,2),(8,77,1),(9,81,1),(10,84,2),(12,83,1),(13,79,1),(14,74,2),
       (16,76,1),(17,79,1),(18,84,1.5),(19.5,86,0.5),(20,88,2),(22,84,2),(24,81,1),(25,77,1),(26,81,2),(28,79,2),(30,83,2)]
music("music_menu", 96, [C, Am, F, G, C, Am, F, G], mel)

# Partida: más movida, La menor (Am - F - C - G)
mel2 = [(0,81,0.5),(0.5,84,0.5),(1,88,1),(2,86,0.5),(2.5,84,0.5),(3,81,1),(4,77,0.5),(4.5,81,0.5),(5,84,1),(6,86,1),(7,84,1),
        (8,79,0.5),(8.5,84,0.5),(9,88,1),(10,91,1),(11,88,1),(12,86,0.5),(12.5,83,0.5),(13,79,1),(14,83,1),(15,86,1),
        (16,81,0.5),(16.5,84,0.5),(17,88,1),(18,89,1),(19,88,1),(20,86,1),(21,84,1),(22,81,2),(24,84,1),(25,88,1),(26,91,1),(27,89,1),(28,88,2),(30,86,2)]
music("music_game", 120, [Am, F, C, G, Am, F, C, G], mel2)
print("ok")
