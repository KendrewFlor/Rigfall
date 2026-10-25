"""Synthesise every Rigfall sound: sound effects, ambience loops and the nine music tracks.

    python3 tools/synth_audio.py            # write Assets/Rigfall/Resources/Audio/*.wav
    python3 tools/synth_audio.py --only sfx_coin bgm_night
    python3 tools/synth_audio.py --check    # every expected file exists and is a valid WAV

No samples, no downloads: everything is built from oscillators, filtered noise and a small
Karplus-Strong string, with numpy. Music is composed procedurally from a fixed seed per
track (scale, chord progression, drum pattern, a motif that repeats with variations), and
each track is rendered as a seamless loop. Output is 16-bit mono WAV at 22.05 kHz; Unity
compresses it on import.
"""
from __future__ import annotations

import argparse
import sys
import wave
from dataclasses import dataclass
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "Rigfall" / "Resources" / "Audio"
SR = 22050


# ============================================================================ building blocks

def n_of(seconds: float) -> int:
    return max(1, int(round(seconds * SR)))


def times(seconds: float) -> np.ndarray:
    return np.arange(n_of(seconds)) / SR


def midi_hz(m: float) -> float:
    return 440.0 * 2 ** ((m - 69) / 12)


def phase_of(freq) -> np.ndarray:
    return 2 * np.pi * np.cumsum(np.asarray(freq, dtype=float)) / SR


def sine(freq, seconds: float | None = None) -> np.ndarray:
    if np.isscalar(freq):
        freq = np.full(n_of(seconds), float(freq))
    return np.sin(phase_of(freq))


def additive(freq, seconds: float | None, partials) -> np.ndarray:
    """Band-limited waveform from (harmonic, amplitude) pairs, skipping anything above Nyquist."""
    if np.isscalar(freq):
        freq = np.full(n_of(seconds), float(freq))
    ph = phase_of(freq)
    top = float(np.max(freq))
    out = np.zeros_like(ph)
    for h, a in partials:
        if h * top < SR / 2 * 0.95:
            out += a * np.sin(ph * h)
    return out


def saw(freq, seconds=None, harmonics: int = 24) -> np.ndarray:
    return additive(freq, seconds, [(h, 1.0 / h) for h in range(1, harmonics + 1)]) * 0.55


def square(freq, seconds=None, harmonics: int = 15) -> np.ndarray:
    return additive(freq, seconds, [(h, 1.0 / h) for h in range(1, 2 * harmonics, 2)]) * 0.75


def triangle(freq, seconds=None) -> np.ndarray:
    return additive(freq, seconds, [(h, ((-1) ** ((h - 1) // 2)) / (h * h)) for h in range(1, 16, 2)]) * 0.8


def noise(seconds: float, rng) -> np.ndarray:
    return rng.uniform(-1, 1, n_of(seconds))


def _shape(x: np.ndarray, gain) -> np.ndarray:
    spec = np.fft.rfft(x)
    spec *= gain(np.fft.rfftfreq(len(x), 1 / SR))
    return np.fft.irfft(spec, len(x))


def lowpass(x: np.ndarray, cutoff: float, order: int = 2) -> np.ndarray:
    return _shape(x, lambda f: 1 / np.sqrt(1 + (f / cutoff) ** (2 * order)))


def highpass(x: np.ndarray, cutoff: float, order: int = 2) -> np.ndarray:
    return _shape(x, lambda f: 1 / np.sqrt(1 + (cutoff / np.maximum(f, 1e-3)) ** (2 * order)))


def bandpass(x: np.ndarray, centre: float, q: float = 2.0) -> np.ndarray:
    return _shape(x, lambda f: 1 / np.sqrt(1 + (q * (f / centre - centre / np.maximum(f, 1e-3))) ** 2))


def sweep_filter(x: np.ndarray, start: float, end: float, chunks: int = 24, kind: str = "low") -> np.ndarray:
    """A filter whose cutoff glides from start to end (processed in overlapping chunks)."""
    out = np.zeros_like(x)
    edges = np.linspace(0, len(x), chunks + 1).astype(int)
    for i in range(chunks):
        a, b = edges[i], edges[i + 1]
        pad = min(512, a)
        seg = x[a - pad:b]
        fc = start * (end / start) ** (i / max(1, chunks - 1))
        y = lowpass(seg, fc) if kind == "low" else highpass(seg, fc)
        out[a:b] = y[pad:]
    return out


def exp_env(seconds: float, decay: float, attack: float = 0.002) -> np.ndarray:
    t = times(seconds)
    e = np.exp(-t / max(decay, 1e-4))
    if attack > 0:
        e *= np.clip(t / attack, 0, 1)
    return e


def adsr(seconds: float, a: float, d: float, s: float, r: float) -> np.ndarray:
    n = n_of(seconds)
    t = np.arange(n) / SR
    e = np.where(t < a, t / max(a, 1e-4), s + (1 - s) * np.exp(-(t - a) / max(d, 1e-4)))
    rel = n_of(r)
    if rel < n:
        e[-rel:] *= np.linspace(1, 0, rel) ** 1.5
    return e


def fit(x: np.ndarray, n: int) -> np.ndarray:
    if len(x) >= n:
        return x[:n]
    return np.concatenate([x, np.zeros(n - len(x))])


def mix(*parts: tuple[float, np.ndarray]) -> np.ndarray:
    n = max(len(p) for _, p in parts)
    out = np.zeros(n)
    for gain, p in parts:
        out[:len(p)] += gain * p
    return out


def place(buf: np.ndarray, sound: np.ndarray, at_seconds: float, gain: float = 1.0, wrap: bool = False) -> None:
    i = int(round(at_seconds * SR))
    if wrap:
        idx = (np.arange(len(sound)) + i) % len(buf)
        np.add.at(buf, idx, sound * gain)
        return
    if i >= len(buf):
        return
    j = min(len(buf), i + len(sound))
    buf[i:j] += sound[:j - i] * gain


def reverb(x: np.ndarray, seconds: float, mix_level: float, rng, wrap: bool = False) -> np.ndarray:
    """Convolution with decaying noise: a cheap, smooth hall. wrap=True keeps loops seamless."""
    ir = noise(seconds, rng) * np.exp(-times(seconds) / (seconds / 5))
    ir = lowpass(ir, 4500)
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    if wrap:
        n = len(x)
        wet = np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(fit(ir, n)), n)
    else:
        n = len(x) + len(ir)
        wet = np.fft.irfft(np.fft.rfft(fit(x, n)) * np.fft.rfft(fit(ir, n)), n)
        x = fit(x, n)
    return x * (1 - mix_level * 0.5) + wet * mix_level


def soft_clip(x: np.ndarray, drive: float = 1.0) -> np.ndarray:
    return np.tanh(x * drive) / np.tanh(drive)


def normalize(x: np.ndarray, peak: float = 0.89) -> np.ndarray:
    m = float(np.max(np.abs(x))) or 1.0
    return x * (peak / m)


def fade(x: np.ndarray, fade_in: float = 0.002, fade_out: float = 0.02) -> np.ndarray:
    x = x.copy()
    a, b = min(len(x), n_of(fade_in)), min(len(x), n_of(fade_out))
    x[:a] *= np.linspace(0, 1, a)
    x[len(x) - b:] *= np.linspace(1, 0, b)
    return x


# ============================================================================ instruments

def ks_pluck(freq: float, seconds: float, rng, bright: float = 0.6, decay: float = 0.996) -> np.ndarray:
    """Karplus-Strong string, vectorised one period at a time."""
    period = max(2, int(round(SR / freq)))
    n = n_of(seconds)
    y = np.zeros(n + period + 1)
    burst = rng.uniform(-1, 1, period + 1)
    burst -= burst.mean()  # a DC offset in the burst would never decay
    y[:period + 1] = lowpass(burst, 1500 + 8000 * bright) if period > 16 else burst
    k = period + 1
    while k < len(y):
        end = min(len(y), k + period)
        m = end - k
        y[k:end] = decay * 0.5 * (y[k - period:k - period + m] + y[k - period - 1:k - period - 1 + m])
        k = end
    return y[period + 1:period + 1 + n] * 0.9


def fm_bell(freq: float, seconds: float, ratio: float = 3.5, index: float = 4.0, decay: float = 0.8) -> np.ndarray:
    t = times(seconds)
    mod = np.sin(2 * np.pi * freq * ratio * t) * index * np.exp(-t / (decay * 0.5))
    return np.sin(2 * np.pi * freq * t + mod) * exp_env(seconds, decay, 0.003)


def pad(freqs, seconds: float, rng, cutoff: float = 1800, attack: float = 0.4, release: float = 0.6) -> np.ndarray:
    out = np.zeros(n_of(seconds))
    for f in freqs:
        for det in (-0.08, 0.0, 0.07):
            out += saw(midi_hz(f + det), seconds, harmonics=12)
    out = lowpass(out / (len(freqs) * 3), cutoff)
    return out * adsr(seconds, attack, 0.6, 0.85, release)


def organ(freqs, seconds: float) -> np.ndarray:
    out = np.zeros(n_of(seconds))
    for f in freqs:
        hz = midi_hz(f)
        out += additive(hz, seconds, [(1, 1.0), (2, 0.5), (3, 0.25), (4, 0.18), (6, 0.08)])
    return out / max(1, len(freqs)) * adsr(seconds, 0.03, 0.3, 0.8, 0.1)


def bass_note(midi: int, seconds: float, kind: str) -> np.ndarray:
    hz = midi_hz(midi)
    if kind == "sub":
        x = sine(hz, seconds) + 0.3 * triangle(hz * 2, seconds)
        return x * adsr(seconds, 0.005, 0.2, 0.7, 0.05)
    if kind == "drive":
        x = soft_clip(lowpass(saw(hz, seconds) + 0.5 * square(hz * 0.5, seconds), 900), 2.5)
        return x * adsr(seconds, 0.004, 0.15, 0.75, 0.04)
    if kind == "upright":
        return lowpass(triangle(hz, seconds) + 0.25 * saw(hz, seconds, 6), 700) * exp_env(seconds, 0.45, 0.004)
    x = sweep_filter(saw(hz, seconds, 16), 1100, 600, chunks=4)  # a little filter 'pluck'
    return x * adsr(seconds, 0.004, 0.18, 0.6, 0.04)


def lead_note(midi: int, seconds: float, kind: str, rng) -> np.ndarray:
    hz = midi_hz(midi)
    t = times(seconds)
    vib = 1 + 0.006 * np.sin(2 * np.pi * 5.5 * t) * np.clip((t - 0.12) / 0.2, 0, 1)
    if kind == "pluck":
        return ks_pluck(hz, seconds, rng, bright=0.55)
    if kind == "twang":
        x = ks_pluck(hz, seconds, rng, bright=0.8, decay=0.997)
        return x + 0.15 * ks_pluck(hz * 2.003, seconds, rng, bright=0.9)
    if kind == "bell":
        return fm_bell(hz, seconds, decay=min(1.4, seconds * 1.4))
    if kind == "harmonica":
        x = lowpass(square(hz * vib, None, 9) * 0.6 + saw(hz * vib, None, 10) * 0.4, 2400)
        return x * adsr(seconds, 0.04, 0.2, 0.75, 0.06)
    if kind == "alarm":
        x = lowpass(square(hz * vib, None, 12), 3000)
        return x * adsr(seconds, 0.01, 0.12, 0.7, 0.04)
    if kind == "brass":
        bright = 900 + 2600 * np.clip(t / 0.08, 0, 1)
        x = saw(hz * vib, None, 18)
        x = sweep_filter(x, 900, 3200, chunks=6)
        return x * adsr(seconds, 0.05, 0.25, 0.8, 0.08) * (bright / bright.max())
    # flute-ish default
    x = sine(hz * vib) + 0.18 * sine(2 * hz * vib) + 0.05 * lowpass(rng.uniform(-1, 1, len(t)), 3000)
    return x * adsr(seconds, 0.06, 0.2, 0.85, 0.08)


# ---- drums

def kick(rng, punch: float = 1.0) -> np.ndarray:
    d = 0.42
    t = times(d)
    f = 45 + 110 * punch * np.exp(-t / 0.045)
    body = sine(f) * exp_env(d, 0.16)
    click = lowpass(noise(0.01, rng), 3000) * 0.4
    return mix((1.0, body), (1.0, click))


def snare(rng, tone: float = 190) -> np.ndarray:
    d = 0.26
    rattle = highpass(noise(d, rng), 1400) * exp_env(d, 0.07)
    body = sine(tone, d) * exp_env(d, 0.05)
    return rattle * 0.8 + body * 0.6


def hat(rng, open_: bool = False) -> np.ndarray:
    d = 0.25 if open_ else 0.06
    return highpass(noise(d, rng), 7000, 3) * exp_env(d, 0.09 if open_ else 0.018) * 0.38


def clank(rng, f: float = 520.0, d: float = 0.5) -> np.ndarray:
    """Struck steel: inharmonic partials with fast decays (pipe, anvil, rig floor)."""
    out = np.zeros(n_of(d))
    for ratio, amp, dec in ((1.0, 1.0, 0.3), (2.76, 0.6, 0.18), (5.4, 0.4, 0.09), (8.93, 0.25, 0.05)):
        out += amp * sine(f * ratio, d) * exp_env(d, dec * d / 0.5)
    out += fit(highpass(noise(0.015, rng), 2000), len(out)) * 0.5  # the strike
    return out * 0.45


def tom(rng, f: float) -> np.ndarray:
    d = 0.5
    t = times(d)
    return sine(f * (1 + 0.5 * np.exp(-t / 0.03))) * exp_env(d, 0.18) + lowpass(noise(d, rng), 1200) * exp_env(d, 0.03) * 0.3


def shaker(rng) -> np.ndarray:
    d = 0.09
    return bandpass(noise(d, rng), 6000, 1.5) * adsr(d, 0.02, 0.03, 0.2, 0.03) * 0.28


DRUMS = {"k": kick, "s": snare, "h": hat, "o": lambda r: hat(r, True), "c": lambda r: clank(r, 700, 0.35),
         "t": lambda r: tom(r, 110), "T": lambda r: tom(r, 75), "x": shaker, "K": lambda r: kick(r, 1.6)}


# ============================================================================ composer

SCALES = {
    "major": [0, 2, 4, 5, 7, 9, 11],
    "minor": [0, 2, 3, 5, 7, 8, 10],
    "dorian": [0, 2, 3, 5, 7, 9, 10],
    "mixolydian": [0, 2, 4, 5, 7, 9, 10],
    "phrygian": [0, 1, 3, 5, 7, 8, 10],
    "harmonic": [0, 2, 3, 5, 7, 8, 11],
}


@dataclass
class Track:
    name: str
    bpm: float
    root: int                      # MIDI note of the key
    scale: str
    progression: list[int]         # scale degree (0-based) per bar, 8 bars
    drums: dict[str, str]          # instrument -> 16-step pattern per bar ('x' hit, '.' rest, 'X' accent)
    bass: str                      # 16-step pattern: digit = chord tone index (0 root, 1 third, 2 fifth, 3 octave), '.' rest
    bass_kind: str
    lead_kind: str
    pad_kind: str = "pad"          # pad | organ | none
    bars: int = 16
    swing: float = 0.0
    lead_octave: int = 1
    lead_density: float = 0.55
    lead_gain: float = 0.32
    pad_gain: float = 0.22
    bass_gain: float = 0.42
    drum_gain: float = 0.5
    reverb: float = 0.25
    seed: int = 1
    intro_bars: int = 0            # bars where the drums sit out (for loops that breathe)


def chord(track: Track, degree: int) -> list[int]:
    sc = SCALES[track.scale]
    notes = []
    for step in (0, 2, 4):
        idx = degree + step
        notes.append(track.root + sc[idx % 7] + 12 * (idx // 7))
    return notes


def scale_note(track: Track, index: int) -> int:
    sc = SCALES[track.scale]
    return track.root + sc[index % 7] + 12 * (index // 7)


RHYTHMS = [
    [(0, 4), (4, 2), (6, 2), (8, 4), (12, 4)],
    [(0, 2), (2, 2), (4, 4), (8, 2), (10, 2), (12, 4)],
    [(0, 6), (6, 2), (8, 6), (14, 2)],
    [(0, 3), (3, 3), (6, 2), (8, 3), (11, 3), (14, 2)],
    [(0, 8), (8, 4), (12, 4)],
    [(0, 2), (2, 1), (3, 1), (4, 4), (8, 2), (10, 2), (12, 2), (14, 2)],
]


def compose_motif(track: Track, rng, bars: int = 2) -> list[tuple[int, int, int]]:
    """Notes as (step, length, scale index relative to the chord root), steps of a 16th."""
    notes = []
    pos = 7 * track.lead_octave + 2
    for bar in range(bars):
        rhythm = RHYTHMS[int(rng.integers(len(RHYTHMS)))]
        for start, length in rhythm:
            if rng.random() > track.lead_density + (0.25 if start in (0, 8) else 0):
                continue
            if start in (0, 8):
                pos = 7 * track.lead_octave + int(rng.choice([0, 2, 4]))  # chord tones on strong beats
            else:
                pos += int(rng.choice([-2, -1, -1, 1, 1, 2, 3]))
                pos = int(np.clip(pos, 7 * track.lead_octave - 2, 7 * track.lead_octave + 9))
            notes.append((bar * 16 + start, length, pos))
    return notes


def vary(motif, rng, amount: float):
    out = []
    for step, length, pos in motif:
        if rng.random() < amount:
            pos += int(rng.choice([-2, -1, 1, 2]))
        out.append((step, length, pos))
    return out


def render_track(track: Track) -> np.ndarray:
    rng = np.random.default_rng(track.seed)
    step_s = 60.0 / track.bpm / 4
    bar_s = step_s * 16
    total = n_of(bar_s * track.bars)
    drums = np.zeros(total)
    bass = np.zeros(total)
    lead = np.zeros(total)
    pads = np.zeros(total)
    drum_cache = {k: [fn(np.random.default_rng(track.seed * 31 + i)) for i in range(3)] for k, fn in DRUMS.items()}

    def at(bar: int, step: int) -> float:
        swing = track.swing * step_s if step % 2 == 1 else 0.0
        return bar * bar_s + step * step_s + swing

    # Motif structure: A A' B A'' over each 8 bars, so the tune is memorable but not static.
    a = compose_motif(track, rng)
    b = compose_motif(track, rng)
    phrases = [a, vary(a, rng, 0.25), b, vary(a, rng, 0.4)]

    prog = track.progression
    for bar in range(track.bars):
        degree = prog[bar % len(prog)]
        ch = chord(track, degree)
        section_b = (bar // 8) % 2 == 1
        # drums
        if bar >= track.intro_bars:
            for inst, pattern in track.drums.items():
                for step, ch_ in enumerate(pattern):
                    if ch_ == ".":
                        continue
                    if section_b is False and inst in ("o", "x") and bar % 2 == 0 and step > 12:
                        continue
                    hit = drum_cache[inst][(bar + step) % 3]
                    gain = 1.0 if ch_ == "X" else 0.7
                    place(drums, hit, at(bar, step), gain * (1.08 if section_b else 1.0), wrap=True)
            if bar % 8 == 7 and "s" in track.drums:  # fill into the next phrase
                for step in (12, 13, 14, 15):
                    place(drums, drum_cache["s"][step % 3], at(bar, step), 0.45 + 0.1 * (step - 12), wrap=True)
        # bass
        for step, c in enumerate(track.bass):
            if c == ".":
                continue
            length = 1
            while step + length < 16 and track.bass[step + length] == "-":
                length += 1
            if c == "-":
                continue
            idx = int(c)
            note = (ch + [ch[0] + 12])[idx] - 24
            place(bass, bass_note(note, step_s * length * 0.95, track.bass_kind), at(bar, step), wrap=True)
        # pads / organ
        if track.pad_kind != "none":
            voicing = [n - 12 if n - track.root > 9 else n for n in ch]
            body = pad(voicing, bar_s * 1.05, rng) if track.pad_kind == "pad" else organ(voicing, bar_s * 0.98)
            place(pads, body, at(bar, 0), wrap=True)
        # lead
        phrase = phrases[(bar // 2) % 4]
        for step, length, pos in phrase:
            if step // 16 != bar % 2:
                continue
            s = step % 16
            note = scale_note(track, degree + pos)
            place(lead, lead_note(note, step_s * length * 1.1, track.lead_kind, rng), at(bar, s), wrap=True)
        if section_b:
            # counter-melody: slow chord-tone line a sixth below in the second half
            place(lead, lead_note(ch[2] + 12 * (track.lead_octave - 1), bar_s * 0.9, "flute", rng), at(bar, 0), 0.35, wrap=True)

    music = (drums * track.drum_gain + bass * track.bass_gain + lead * track.lead_gain + pads * track.pad_gain)
    music = reverb(music, 1.8, track.reverb, rng, wrap=True)
    music = highpass(music, 30)  # remove the DC offset that drive and soft clipping leave behind
    music = soft_clip(normalize(music, 0.95), 1.2)
    return normalize(music, 0.85)


TRACKS = [
    Track("bgm_menu", 96, 45, "dorian", [0, 0, 5, 6, 0, 0, 3, 4],
          {"k": "X.......x.x.....", "s": "....X.......X...", "h": "x.x.x.x.x.x.x.xx", "c": "..........x....."},
          "0..0..2.0..0.3..", "pluck", "brass", seed=11, swing=0.12, lead_density=0.5, reverb=0.3),
    Track("bgm_day_desert", 108, 50, "mixolydian", [0, 0, 6, 6, 3, 3, 4, 4],
          {"k": "X.....x.X.......", "s": "....X.......X...", "h": "x.xxx.xxx.xxx.xx", "x": "..x...x...x...x."},
          "0.2.0.2.0.2.3.2.", "upright", "twang", pad_kind="none", seed=21, swing=0.18, lead_density=0.6),
    Track("bgm_day_swamp", 84, 43, "dorian", [0, 0, 3, 0, 4, 3, 0, 4],
          {"k": "X.....x...x.....", "s": "....X.......X..x", "h": "x..x..x..x..x..x", "x": "..x..x..x..x..x."},
          "0..2..3.0..2..1.", "upright", "harmonica", pad_kind="organ", seed=31, swing=0.33, pad_gain=0.16),
    Track("bgm_day_arctic", 72, 52, "major", [0, 4, 5, 3, 0, 4, 3, 4],
          {"h": "x.......x.......", "t": "........x.......", "k": "X..............."},
          "0-------2-------", "sub", "bell", seed=41, lead_density=0.45, reverb=0.5, drum_gain=0.35, pad_gain=0.3),
    Track("bgm_day_offshore", 100, 48, "major", [0, 4, 5, 3, 0, 4, 1, 4],
          {"k": "X.......X.......", "s": "....x.......x...", "h": "..x...x...x...x.", "x": "xxxxxxxxxxxxxxxx"},
          "0.0.2.0.0.0.3.2.", "pluck", "pluck", seed=51, lead_density=0.65, reverb=0.35),
    Track("bgm_night", 140, 45, "minor", [0, 0, 5, 5, 3, 3, 4, 4],
          {"k": "X...x...X...x...", "s": "....X.......X...", "h": "x.x.x.x.x.x.x.x.", "o": "..x...x...x...x.", "c": "x..............."},
          "0.000.000.000.30", "drive", "alarm", seed=61, lead_density=0.6, drum_gain=0.6, reverb=0.18, pad_gain=0.15),
    Track("bgm_boss", 160, 40, "phrygian", [0, 0, 1, 0, 0, 0, 1, 6],
          {"K": "X..x..X...X..x..", "s": "....X.......X...", "h": "xxxxxxxxxxxxxxxx", "T": "............xxxx", "c": "X.......X......."},
          "000.000.000.0101", "drive", "brass", seed=71, lead_density=0.55, drum_gain=0.65, bass_gain=0.5, reverb=0.15),
    Track("bgm_dawn", 90, 50, "major", [0, 3, 4, 0, 5, 3, 4, 4],
          {"k": "X.......x.......", "h": "..x...x...x...x.", "x": "x.x.x.x.x.x.x.x."},
          "0.....2.0.....2.", "sub", "bell", seed=81, lead_density=0.55, reverb=0.45, pad_gain=0.28, bars=8),
    Track("bgm_defeat", 60, 45, "harmonic", [0, 5, 3, 4],
          {"T": "X...............", "h": "........x......."},
          "0---------------", "sub", "flute", seed=91, bars=8, lead_density=0.35, reverb=0.55, drum_gain=0.3, pad_gain=0.3),
]


# ============================================================================ sound effects

def sfx_bank(rng) -> dict[str, np.ndarray]:
    s: dict[str, np.ndarray] = {}

    def whoosh(d, f0, f1, gain=1.0):
        x = noise(d, rng)
        out = np.zeros(len(x))
        edges = np.linspace(0, len(x), 13).astype(int)
        for i in range(12):
            a, b = edges[i], edges[i + 1]
            out[a:b] = bandpass(x, f0 * (f1 / f0) ** (i / 11), 2.5)[a:b]
        return out * np.sin(np.linspace(0, np.pi, len(x))) ** 1.5 * gain

    s["sfx_swing"] = whoosh(0.22, 600, 2600)
    thump = sine(110 * (1 + np.exp(-times(0.14) / 0.02))) * exp_env(0.14, 0.05)
    s["sfx_hit"] = mix((1.0, thump), (0.6, highpass(noise(0.05, rng), 1800) * exp_env(0.05, 0.012)))
    s["sfx_hit_heavy"] = mix((1.0, kick(rng, 1.3)), (0.6, clank(rng, 300, 0.35)), (0.5, lowpass(noise(0.2, rng), 1500) * exp_env(0.2, 0.05)))
    pop = square(np.full(n_of(0.14), 1.0) * (300 * np.exp(-times(0.14) / 0.03) + 80)) * exp_env(0.14, 0.04)
    s["sfx_shoot"] = mix((0.7, highpass(noise(0.14, rng), 900) * exp_env(0.14, 0.025)), (0.5, pop))
    s["sfx_shoot_heavy"] = mix((1.0, kick(rng, 1.8)), (0.8, lowpass(noise(0.45, rng), 2200) * exp_env(0.45, 0.09)),
                               (0.4, highpass(noise(0.08, rng), 2500) * exp_env(0.08, 0.02)))
    hiss = bandpass(noise(0.35, rng), 2200, 0.8) * (0.7 + 0.3 * np.sin(2 * np.pi * 22 * times(0.35)))
    s["sfx_spray"] = hiss * adsr(0.35, 0.02, 0.1, 0.8, 0.12)
    whistle = sine(500 + 900 * np.linspace(0, 1, n_of(0.3)) ** 0.7) * adsr(0.3, 0.02, 0.1, 0.6, 0.1)
    s["sfx_lob"] = np.concatenate([whistle * 0.45, (sine(90, 0.15) * exp_env(0.15, 0.05)) * 0.8])
    boom = sweep_filter(noise(1.3, rng), 3000, 120) * exp_env(1.3, 0.35, 0.003)
    s["sfx_explosion"] = mix((1.2, boom), (1.0, kick(rng, 1.5)), (0.3, clank(rng, 240, 0.6)))
    s["sfx_coin"] = np.concatenate([fm_bell(midi_hz(83), 0.09, 2.0, 1.5, 0.08), fm_bell(midi_hz(88), 0.3, 2.0, 1.5, 0.18)]) * 0.8
    build = np.zeros(n_of(0.6))
    for i, f in enumerate((640, 600, 680)):
        place(build, clank(rng, f, 0.25), i * 0.15)
    s["sfx_build"] = build
    up = np.zeros(n_of(0.75))
    for i, m in enumerate((72, 76, 79, 84)):
        place(up, fm_bell(midi_hz(m), 0.4, 2.0, 2.0, 0.25) * 0.6 + ks_pluck(midi_hz(m), 0.4, rng) * 0.4, i * 0.09)
    s["sfx_upgrade"] = up
    s["sfx_buy_fail"] = np.concatenate([lowpass(square(150, 0.12), 1200), np.zeros(n_of(0.03)), lowpass(square(110, 0.18), 1200)]) * 0.5 \
        * fit(adsr(0.33, 0.005, 0.1, 0.8, 0.05), n_of(0.12) + n_of(0.03) + n_of(0.18))
    s["sfx_ui_click"] = mix((0.6, sine(1800, 0.04) * exp_env(0.04, 0.008)), (0.4, highpass(noise(0.02, rng), 3000) * exp_env(0.02, 0.004)))
    s["sfx_ui_hover"] = sine(1200, 0.05) * exp_env(0.05, 0.012) * 0.35
    s["sfx_ui_back"] = sine(np.linspace(1200, 650, n_of(0.1))) * exp_env(0.1, 0.03) * 0.5
    s["sfx_wheel_tick"] = mix((0.5, sine(2400, 0.03) * exp_env(0.03, 0.005)), (0.4, highpass(noise(0.015, rng), 4000) * exp_env(0.015, 0.003)))
    win = np.zeros(n_of(1.3))
    for i, m in enumerate((67, 72, 76, 79)):
        place(win, lead_note(m, 0.5 if i < 3 else 0.9, "brass", rng) * 0.6, i * 0.11)
    s["sfx_wheel_win"] = win
    shimmer = np.zeros(n_of(2.0))
    for i in range(14):
        m = int(rng.choice([84, 86, 88, 91, 93, 96]))
        place(shimmer, fm_bell(midi_hz(m), 0.9, 3.0, 2.5, 0.5), i * 0.08 + rng.uniform(0, 0.03), 0.35)
    shimmer += fit(pad([72, 76, 79, 84], 1.8, rng, 3000, 0.05, 1.0), len(shimmer)) * 0.5
    s["sfx_rarity_brilliant"] = shimmer
    horn = np.zeros(n_of(2.0))
    for start, m in ((0.0, 58), (0.85, 55)):
        tone = lowpass(saw(midi_hz(m), 0.8, 20) + saw(midi_hz(m + 4), 0.8, 20) * 0.7, 2000) * adsr(0.8, 0.06, 0.2, 0.85, 0.15)
        place(horn, soft_clip(tone, 1.6), start)
    s["sfx_horn_night"] = horn
    dawn = np.zeros(n_of(1.8))
    for i, m in enumerate((72, 76, 79)):
        place(dawn, fm_bell(midi_hz(m), 1.5, 3.5, 1.5, 1.0), i * 0.06, 0.5)
    s["sfx_dawn"] = dawn
    s["sfx_enemy_die"] = mix((0.7, sine(np.linspace(420, 90, n_of(0.3))) * exp_env(0.3, 0.1)),
                             (0.6, lowpass(noise(0.3, rng), 900) * exp_env(0.3, 0.08)))
    s["sfx_enemy_spawn"] = sweep_filter(noise(0.6, rng), 300, 3500) * adsr(0.6, 0.15, 0.2, 0.6, 0.2) * 0.8
    growl_t = times(1.7)
    growl = np.sin(2 * np.pi * 70 * growl_t + 3 * np.sin(2 * np.pi * 37 * growl_t)) * adsr(1.7, 0.15, 0.4, 0.7, 0.6)
    s["sfx_boss_roar"] = soft_clip(mix((1.0, growl), (0.5, lowpass(noise(1.7, rng), 600) * adsr(1.7, 0.1, 0.4, 0.6, 0.6))), 2.0)
    s["sfx_building_hit"] = mix((1.0, clank(rng, 420, 0.35)), (0.5, lowpass(noise(0.1, rng), 1500) * exp_env(0.1, 0.03)))
    crash = mix((1.0, sweep_filter(noise(1.3, rng), 4000, 200) * exp_env(1.3, 0.4)), (0.8, kick(rng, 1.2)))
    for i in range(5):
        place(crash, clank(rng, float(rng.uniform(250, 700)), 0.4), 0.05 + i * 0.12, 0.5)
    s["sfx_building_destroyed"] = crash
    s["sfx_hero_hurt"] = lowpass(square(np.linspace(240, 140, n_of(0.18))), 1400) * exp_env(0.18, 0.06) * 0.6
    down = np.zeros(n_of(1.0))
    for i, m in enumerate((64, 60, 57, 52)):
        place(down, lead_note(m, 0.3, "flute", rng), i * 0.18, 0.6)
    s["sfx_hero_down"] = down
    ability = np.zeros(n_of(0.75))
    place(ability, whoosh(0.45, 400, 4000), 0.0, 0.8)
    place(ability, fm_bell(midi_hz(81), 0.5, 3.0, 2.0, 0.3), 0.2, 0.5)
    s["sfx_ability"] = ability
    heal = np.zeros(n_of(0.8))
    for i, m in enumerate((76, 81, 88)):
        place(heal, sine(midi_hz(m), 0.5) * adsr(0.5, 0.05, 0.2, 0.5, 0.25), i * 0.1, 0.4)
    s["sfx_heal"] = heal
    s["sfx_command"] = np.concatenate([sine(2100, 0.12) * adsr(0.12, 0.01, 0.05, 0.8, 0.02),
                                       sine(np.linspace(2100, 2900, n_of(0.22))) * adsr(0.22, 0.01, 0.05, 0.8, 0.08)]) * 0.45
    ach = np.zeros(n_of(1.4))
    for i, m in enumerate((72, 76, 79, 84)):
        place(ach, fm_bell(midi_hz(m), 1.0, 3.5, 2.0, 0.6), i * 0.1, 0.5)
    s["sfx_achievement"] = ach
    lose = np.zeros(n_of(2.4))
    for i, m in enumerate((57, 55, 52)):
        place(lose, lead_note(m, 0.9 if i < 2 else 1.4, "brass", rng), i * 0.55, 0.6)
    s["sfx_defeat"] = lose
    fan = np.zeros(n_of(2.2))
    for i, (t0, m, d) in enumerate(((0, 67, 0.2), (0.2, 67, 0.2), (0.4, 72, 0.35), (0.8, 76, 0.3), (1.1, 79, 1.0))):
        place(fan, lead_note(m, d, "brass", rng), t0, 0.6)
        place(fan, lead_note(m - 12, d, "brass", rng), t0, 0.3)
    s["sfx_victory_stage"] = fan
    q = np.zeros(n_of(0.9))
    for i, m in enumerate((79, 83, 86)):
        place(q, fm_bell(midi_hz(m), 0.6, 2.0, 1.5, 0.35), i * 0.12, 0.5)
    s["sfx_quest_done"] = q
    return s


def ambience_bank(rng) -> dict[str, np.ndarray]:
    d = 12.0
    t = times(d)
    a: dict[str, np.ndarray] = {}

    def loopify(x: np.ndarray, xfade: float = 1.0) -> np.ndarray:
        n = n_of(d)
        k = n_of(xfade)
        x = fit(x, n + k)
        out = x[:n].copy()
        ramp = np.linspace(0, 1, k)
        out[:k] = x[:k] * ramp + x[n:n + k] * (1 - ramp)
        return out

    def slow_lfo(rate, phase=0.0):
        # whole cycles per loop so the swell is seamless
        cycles = max(1, round(rate * d))
        return 0.5 + 0.5 * np.sin(2 * np.pi * cycles * np.arange(n_of(d + 1.0)) / n_of(d) + phase)

    wind = lowpass(noise(d + 1.0, rng), 500) * (0.4 + 0.6 * slow_lfo(0.17))
    gust = bandpass(noise(d + 1.0, rng), 900, 3) * slow_lfo(0.25, 1.3) ** 3
    a["amb_desert"] = loopify(wind + gust * 0.6)

    swamp = lowpass(noise(d + 1.0, rng), 350) * 0.3
    # insects: buzzing high band with an amplitude flutter
    insects = bandpass(noise(d + 1.0, rng), 4800, 8) * (0.5 + 0.5 * np.sin(2 * np.pi * 31 * times(d + 1.0)))
    swamp = swamp + insects * 0.25 * slow_lfo(0.33)
    for i in range(10):  # frogs
        at = float(rng.uniform(0, d))
        f = float(rng.uniform(120, 220))
        croak = sine(f * (1 + 0.2 * np.sin(2 * np.pi * 30 * times(0.25))), 0.25) * adsr(0.25, 0.02, 0.1, 0.6, 0.08)
        place(swamp, lowpass(croak, 800) * 0.5, at, wrap=False)
    a["amb_swamp"] = loopify(swamp)

    howl = np.zeros(n_of(d + 1.0))
    for f, ph in ((420, 0.0), (610, 2.0), (330, 4.1)):
        howl += bandpass(noise(d + 1.0, rng), f, 12) * slow_lfo(0.12, ph) ** 2
    a["amb_arctic"] = loopify(howl * 1.6 + lowpass(noise(d + 1.0, rng), 300) * 0.4)

    sea = np.zeros(n_of(d + 1.0))
    for k in range(4):  # waves every 3 s
        wave_ = lowpass(noise(3.4, rng), 900) * adsr(3.4, 1.2, 0.8, 0.5, 1.4)
        place(sea, wave_, k * 3.0 + 0.2)
    sea += lowpass(noise(d + 1.0, rng), 200) * 0.5
    for _ in range(3):  # distant steel creaks from the jacket
        place(sea, clank(rng, float(rng.uniform(180, 260)), 0.8) * 0.15, float(rng.uniform(0.5, d - 1)))
    a["amb_sea"] = loopify(sea)
    return a


# ============================================================================ output

def write_wav(path: Path, data: np.ndarray) -> None:
    pcm = (np.clip(data, -1, 1) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


SFX_NAMES = [
    "sfx_swing", "sfx_hit", "sfx_hit_heavy", "sfx_shoot", "sfx_shoot_heavy", "sfx_spray", "sfx_lob", "sfx_explosion",
    "sfx_coin", "sfx_build", "sfx_upgrade", "sfx_buy_fail", "sfx_ui_click", "sfx_ui_hover", "sfx_ui_back", "sfx_wheel_tick",
    "sfx_wheel_win", "sfx_rarity_brilliant", "sfx_horn_night", "sfx_dawn", "sfx_enemy_die", "sfx_enemy_spawn",
    "sfx_boss_roar", "sfx_building_hit", "sfx_building_destroyed", "sfx_hero_hurt", "sfx_hero_down", "sfx_ability",
    "sfx_heal", "sfx_command", "sfx_achievement", "sfx_defeat", "sfx_victory_stage", "sfx_quest_done",
]
AMBIENCE_NAMES = ["amb_desert", "amb_swamp", "amb_arctic", "amb_sea"]
EXPECTED = SFX_NAMES + AMBIENCE_NAMES + [t.name for t in TRACKS]


def check() -> int:
    problems = []
    for name in EXPECTED:
        path = OUT / f"{name}.wav"
        if not path.exists():
            problems.append(f"missing {path.relative_to(ROOT)}")
            continue
        try:
            with wave.open(str(path), "rb") as w:
                if w.getframerate() != SR or w.getsampwidth() != 2 or w.getnframes() == 0:
                    problems.append(f"{path.name}: unexpected format")
        except wave.Error as e:
            problems.append(f"{path.name}: {e}")
    for p in problems:
        print("ERROR:", p, file=sys.stderr)
    if not problems:
        print(f"checked {len(EXPECTED)} sounds")
    return 1 if problems else 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--only", nargs="*", help="names to (re)build")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--out", type=Path, default=OUT)
    args = ap.parse_args(argv)
    if args.check:
        return check()
    want = set(args.only or EXPECTED)
    args.out.mkdir(parents=True, exist_ok=True)
    written = 0
    if want & set(SFX_NAMES):
        bank = sfx_bank(np.random.default_rng(1234))
        missing = set(SFX_NAMES) - set(bank)
        if missing:
            raise SystemExit(f"sfx not synthesised: {sorted(missing)}")
        for name in SFX_NAMES:
            if name in want:
                write_wav(args.out / f"{name}.wav", normalize(fade(bank[name]), 0.8))
                written += 1
    if want & set(AMBIENCE_NAMES):
        amb = ambience_bank(np.random.default_rng(99))
        for name in AMBIENCE_NAMES:
            if name in want:
                write_wav(args.out / f"{name}.wav", normalize(amb[name], 0.6))
                written += 1
    for track in TRACKS:
        if track.name in want:
            write_wav(args.out / f"{track.name}.wav", render_track(track))
            written += 1
    print(f"wrote {written} sound(s) to {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
