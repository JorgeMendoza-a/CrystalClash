"""Genera una frase con Kokoro TTS (voces en español: ef_dora, em_alex, em_santa).
Uso: python tts_kokoro.py <carpeta_modelo> <voz> <velocidad> <salida.wav>  (el texto va por stdin)
La carpeta debe tener kokoro-v1.0.onnx y voices-v1.0.bin (github.com/thewh1teagle/kokoro-onnx, releases)."""
import sys
import soundfile as sf
from kokoro_onnx import Kokoro

model_dir, voice, speed, out = sys.argv[1], sys.argv[2], float(sys.argv[3]), sys.argv[4]
text = sys.stdin.read().strip()
k = Kokoro(model_dir + "/kokoro-v1.0.onnx", model_dir + "/voices-v1.0.bin")
samples, sr = k.create(text, voice=voice, speed=speed, lang="es")
sf.write(out, samples, sr, subtype="PCM_16")
