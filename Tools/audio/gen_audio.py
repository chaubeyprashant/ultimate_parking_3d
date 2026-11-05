#!/usr/bin/env python3
"""Synthesises every sound in the game from scratch (sines, noise, envelopes) — nothing sampled or downloaded.
Writes 16-bit mono WAVs into Assets/_Project/Audio. Deterministic (fixed seed). Usage: gen_audio.py <outDir>"""
import math, os, random, struct, sys, wave

SR = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else "."
rng = random.Random(1234)

def write(name, samples, peak=0.89):
    m = max(1e-9, max(abs(s) for s in samples))
    k = peak / m
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * k)) * 32767)) for s in samples))
    print(f"{name}.wav {len(samples)/SR:.2f}s")

def env(t, a, d, total):
    if t < a: return t / a
    return max(0.0, 1.0 - (t - a) / max(1e-6, total - a)) ** d

def lowpass(x, cutoff):
    rc = 1.0 / (2 * math.pi * cutoff); dt = 1.0 / SR; al = dt / (rc + dt); y = 0.0; out = []
    for v in x:
        y += al * (v - y); out.append(y)
    return out

def highpass(x, cutoff):
    lp = lowpass(x, cutoff); return [a - b for a, b in zip(x, lp)]

# --- engine loop: 1 s, integer cycles of 50 Hz so the loop is seamless; pitched up at runtime with speed
def engine():
    n = SR; f = 50.0; out = []
    noise = lowpass([rng.uniform(-1, 1) for _ in range(n)], 400)
    for i in range(n):
        t = i / SR; ph = 2 * math.pi * f * t
        s = (0.9 * math.sin(ph) + 0.55 * math.sin(2 * ph + 0.3) + 0.35 * math.sin(3 * ph + 1.1)
             + 0.22 * math.sin(4 * ph) + 0.12 * math.sin(6 * ph + 0.7))
        s *= 0.85 + 0.15 * math.sin(2 * math.pi * 4 * t)          # cylinder pulse, 4 Hz (integer → seamless)
        out.append(s + 0.35 * noise[i])
    return lowpass(out, 1400)

# --- brake squeal: short tonal screech with noise, falling slightly
def brake():
    n = int(SR * 0.7); out = []
    noise = highpass([rng.uniform(-1, 1) for _ in range(n)], 1500)
    for i in range(n):
        t = i / SR; f = 2300 - 400 * t
        s = 0.5 * math.sin(2 * math.pi * f * t + 3 * math.sin(2 * math.pi * 9 * t)) + 0.35 * noise[i]
        out.append(s * env(t, 0.03, 1.6, 0.7))
    return out

# --- collision: low body thud + noise crunch + short metallic ring
def collision():
    n = int(SR * 0.55); out = []
    noise = lowpass([rng.uniform(-1, 1) for _ in range(n)], 2500)
    for i in range(n):
        t = i / SR; f = 95 * math.exp(-t * 6) + 38
        thud = math.sin(2 * math.pi * f * t) * math.exp(-t * 9)
        crunch = noise[i] * math.exp(-t * 22)
        ring = (math.sin(2 * math.pi * 610 * t) + 0.6 * math.sin(2 * math.pi * 1530 * t)) * math.exp(-t * 14) * 0.25
        out.append(1.0 * thud + 0.7 * crunch + ring)
    return out

# --- UI click: crisp, short
def click():
    n = int(SR * 0.05)
    return [math.sin(2 * math.pi * 2400 * i / SR) * math.exp(-i / SR * 120) + 0.3 * rng.uniform(-1, 1) * math.exp(-i / SR * 300) for i in range(n)]

def bell(freq, dur, decay=5.0):
    n = int(SR * dur)
    return [(math.sin(2 * math.pi * freq * i / SR) + 0.35 * math.sin(2 * math.pi * freq * 2.76 * i / SR) * math.exp(-i / SR * 6)
             + 0.15 * math.sin(2 * math.pi * freq * 5.4 * i / SR) * math.exp(-i / SR * 12)) * env(i / SR, 0.004, 1.0, dur) * math.exp(-i / SR * decay)
            for i in range(n)]

def mix(*parts):
    n = max(off + len(p) for off, p in parts); out = [0.0] * n
    for off, p in parts:
        for i, v in enumerate(p): out[off + i] += v
    return out

def note(m): return 440.0 * 2 ** ((m - 69) / 12)

# --- reward: one star chime (played three times, pitched up); coin blip; success fanfare; fail sting
def star(): return bell(note(84), 0.6, 4.0)
def coin(): return mix((0, bell(note(91), 0.12, 25)), (int(SR * 0.07), bell(note(96), 0.25, 14)))
def success():
    chord = [note(m) for m in (72, 76, 79, 84)]
    return mix(*[(int(SR * 0.08 * k), bell(f, 1.4, 2.5)) for k, f in enumerate(chord)])
def fail():
    def tone(f, dur):
        n = int(SR * dur)
        return [(math.sin(2 * math.pi * f * i / SR) + 0.3 * math.sin(2 * math.pi * f * 2 * i / SR)) * env(i / SR, 0.01, 1.2, dur) for i in range(n)]
    return lowpass(mix((0, tone(note(62), 0.35)), (int(SR * 0.28), tone(note(57), 0.7))), 2500)
def collision_warn(): return bell(note(76), 0.18, 18)

# --- music: 16 s calm, seamless loop. Am - F - C - G pads (detuned sines) + soft pulse bass on eighths
def music():
    dur = 16.0; n = int(SR * dur); out = [0.0] * n
    chords = [(57, 60, 64, 69), (53, 57, 60, 65), (48, 55, 60, 64), (55, 59, 62, 67)]
    roots = [45, 41, 48, 43]
    for c, notes in enumerate(chords):
        start = int(SR * 4 * c)
        for i in range(int(SR * 4)):
            t = i / SR
            e = min(1.0, t / 0.8) * min(1.0, (4.0 - t) / 0.8)          # crossfading pads, zero at the seams
            s = 0.0
            for m in notes:
                f = note(m)
                s += math.sin(2 * math.pi * f * t) + 0.5 * math.sin(2 * math.pi * f * 1.003 * t + 1.0)
            out[start + i] += 0.08 * s * e
            beat = (t * 4) % 1.0                                         # eighth notes at 120 bpm
            out[start + i] += 0.22 * math.sin(2 * math.pi * note(roots[c]) * t) * math.exp(-beat * 6) * min(1.0, beat * 80)
    hat = highpass([rng.uniform(-1, 1) for _ in range(n)], 7000)
    for i in range(n):
        beat = ((i / SR) * 4 + 0.5) % 1.0
        out[i] += 0.035 * hat[i] * math.exp(-beat * 30)
    return lowpass(out, 5000)

os.makedirs(OUT, exist_ok=True)
for name, fn in [("engine_loop", engine), ("brake_squeal", brake), ("collision", collision), ("ui_click", click),
                 ("reward_star", star), ("reward_coin", coin), ("level_success", success), ("level_fail", fail),
                 ("collision_warn", collision_warn), ("music_loop", music)]:
    write(name, fn(), peak=0.7 if name == "music_loop" else 0.89)
