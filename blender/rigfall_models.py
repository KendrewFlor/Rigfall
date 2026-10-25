"""Every model in Rigfall, written as a recipe with the rigkit primitives.

Each recipe returns a rigkit.Model. MODELS maps the model name (the name the game data
uses, e.g. "building-pumpjack" or "building-derrick-tower@flare-stack") to its recipe.
Run blender/build_models.py to turn them into the JSON meshes the game loads, or open
them in Blender with blender/rigfall_blender.py.

Scale: 1 unit = 1 metre. Characters are about 1.8 m tall and face +Z.
"""
from __future__ import annotations

import math
import random

from rigkit import Model, hex_color, mix, shade

# ---------------------------------------------------------------------------- palette

STEEL = "#8A9BA1"
STEEL_DARK = "#4E5B60"
IRON = "#2E3538"
HIVIS = "#FF6A13"
SAFETY = "#F2C200"
WHITE = "#E9EEF0"
SKIN = "#C98E62"
SKIN_DARK = "#8C5A3C"
RUST = "#9A4E25"
RED = "#C2283A"
BLUE = "#2F6FB5"
GREEN = "#2FA36B"
BROWN = "#6B4A2E"
SAND = "#C9A26B"
BLACK = "#16181A"
OIL = "#1A1410"
GLOW_ORANGE = "#FF8A3D"
GLOW_YELLOW = "#FFD23F"
ICE = "#CFEAFA"
ICE_DEEP = "#7FB8DA"


# ---------------------------------------------------------------------------- people

def humanoid(name: str, suit: str, hat: str, *, stripes: bool = True, hat_kind: str = "hard", skin: str = SKIN,
             backpack: str | None = None, scale: float = 1.0) -> Model:
    """A chunky low-poly worker in coveralls and a hard hat, facing +Z."""
    m = Model(name)
    s = scale
    dark = shade(suit, 0.72)
    # legs and boots
    for x in (-0.13, 0.13):
        m.box((0.22 * s, 0.72 * s, 0.24 * s), (x * s, 0.44 * s, 0), dark)
        m.box((0.24 * s, 0.14 * s, 0.32 * s), (x * s, 0.07 * s, 0.04 * s), BLACK)
    # torso, belt, reflective stripes
    m.box((0.62 * s, 0.72 * s, 0.36 * s), (0, 1.16 * s, 0), suit, bevel_top=0.08)
    m.box((0.64 * s, 0.08 * s, 0.38 * s), (0, 0.84 * s, 0), BLACK)
    if stripes:
        for y in (1.02, 1.3):
            m.box((0.64 * s, 0.05 * s, 0.38 * s), (0, y * s, 0), "#E8F06A")
    # arms
    for x in (-0.42, 0.42):
        m.box((0.18 * s, 0.6 * s, 0.2 * s), (x * s, 1.18 * s, 0.02 * s), suit)
        m.box((0.16 * s, 0.14 * s, 0.18 * s), (x * s, 0.84 * s, 0.04 * s), "#D9C49A")  # gloves
    # head
    m.box((0.32 * s, 0.32 * s, 0.3 * s), (0, 1.69 * s, 0), skin)
    m.box((0.2 * s, 0.05 * s, 0.02 * s), (0, 1.66 * s, 0.155 * s), BLACK)  # eyes band
    if hat_kind == "hard":
        m.sphere(0.24 * s, (0, 1.85 * s, 0), hat, rings=3, segs=8, scale=(1, 0.62, 1.05))
        m.cyl(0.27 * s, 0.03 * s, (0, 1.82 * s, 0.02 * s), hat, sides=8)
    elif hat_kind == "cowboy":
        m.cyl(0.36 * s, 0.03 * s, (0, 1.84 * s, 0), hat, sides=10)
        m.cyl(0.17 * s, 0.18 * s, (0, 1.85 * s, 0), hat, sides=8, top_radius=0.15 * s)
    elif hat_kind == "cap":
        m.sphere(0.2 * s, (0, 1.85 * s, 0), hat, rings=3, segs=8, scale=(1, 0.55, 1))
        m.box((0.24 * s, 0.03 * s, 0.16 * s), (0, 1.82 * s, 0.2 * s), hat)
    if backpack:
        m.box((0.42 * s, 0.5 * s, 0.2 * s), (0, 1.16 * s, -0.28 * s), backpack)
    m.anchor("hand", (0.45 * s, 0.86 * s, 0.18 * s))
    m.anchor("head", (0, 1.95 * s, 0))
    return m


def hero_driller() -> Model:
    m = humanoid("hero-driller", HIVIS, WHITE)
    m.box((0.5, 0.12, 0.04), (0, 1.5, 0.19), WHITE)  # name tag / chest badge
    return m


def hero_toolpusher() -> Model:
    m = humanoid("hero-toolpusher", "#D9C49A", WHITE, stripes=False)
    m.box((0.66, 0.2, 0.4), (0, 1.42, 0), "#B0A27E")  # vest yoke
    m.box((0.08, 0.3, 0.06), (-0.22, 1.25, 0.2), BLACK)  # radio
    m.cyl(0.01, 0.2, (-0.22, 1.4, 0.2), BLACK, sides=4)
    return m


def hero_perforator() -> Model:
    m = humanoid("hero-perforator", BLUE, "#FF6A13")
    m.box((0.3, 0.07, 0.04), (0, 1.73, 0.16), "#7FD0FF")  # safety glasses
    m.box((0.5, 0.26, 0.18), (0, 0.98, 0.22), STEEL_DARK)  # charge pouch
    return m


def hero_mud_engineer() -> Model:
    m = humanoid("hero-mud-engineer", "#8B6B47", SAFETY, backpack="#5C4A3A")
    m.cyl(0.12, 0.5, (0, 1.05, -0.42), "#B8B0A0", sides=8)  # mud tank
    m.pipe((0.0, 1.4, -0.42), (0.3, 1.3, -0.1), 0.04, BLACK)
    return m


def hero_geophysicist() -> Model:
    m = humanoid("hero-geophysicist", GREEN, "#E9EEF0", hat_kind="cap", backpack="#3A5A40")
    m.cyl(0.015, 0.7, (0.12, 1.4, -0.3), BLACK, sides=4)  # antenna
    m.sphere(0.04, (0.12, 2.12, -0.3), RED, rings=2, segs=6)
    return m


# ---------------------------------------------------------------------------- weapons
# Weapons are held at the "hand" anchor; the grip is at the origin and they point +Z.

def bit(name: str, head: str, accent: str, kind: str) -> Model:
    m = Model(name)
    m.pipe((0, 0, -0.1), (0, 0, 0.62), 0.05, STEEL, sides=6)
    m.cyl(0.09, 0.12, (0, 0, 0.6), STEEL_DARK, rot=(90, 0, 0), sides=6)
    z = 0.78
    if kind == "tricone":
        for i in range(3):
            a = math.radians(120 * i)
            m.cone(0.09, 0.2, (math.cos(a) * 0.07, math.sin(a) * 0.07, z), head, rot=(90, 0, 0), sides=7)
    elif kind == "pdc":
        m.cyl(0.13, 0.16, (0, 0, z - 0.04), head, rot=(90, 0, 0), sides=8)
        for i in range(6):
            a = math.radians(60 * i)
            m.box((0.04, 0.05, 0.08), (math.cos(a) * 0.1, math.sin(a) * 0.1, z + 0.12), accent)
    elif kind == "hybrid":
        m.cyl(0.13, 0.12, (0, 0, z - 0.04), head, rot=(90, 0, 0), sides=8)
        for i in range(2):
            a = math.radians(180 * i)
            m.cone(0.07, 0.16, (math.cos(a) * 0.06, math.sin(a) * 0.06, z + 0.06), accent, rot=(90, 0, 0), sides=6)
        m.box((0.04, 0.22, 0.08), (0, 0, z + 0.12), accent)
    elif kind == "diamond":
        m.sphere(0.14, (0, 0, z + 0.06), head, rings=3, segs=8, scale=(1, 1, 1.2))
        for i in range(8):
            a = math.radians(45 * i)
            m.box((0.04, 0.04, 0.04), (math.cos(a) * 0.12, math.sin(a) * 0.12, z + 0.1), accent)
    else:  # black diamond core bit
        m.cyl(0.15, 0.18, (0, 0, z - 0.04), head, rot=(90, 0, 0), sides=10)
        for i in range(10):
            a = math.radians(36 * i)
            m.box((0.04, 0.04, 0.06), (math.cos(a) * 0.13, math.sin(a) * 0.13, z + 0.16), accent)
        m.cyl(0.06, 0.05, (0, 0, z + 0.14), BLACK, rot=(90, 0, 0), sides=8)
    return m


def pipe_wrench() -> Model:
    m = Model("weapon-pipe-wrench")
    m.box((0.06, 0.06, 0.8), (0, 0, 0.32), RED)
    m.box((0.1, 0.22, 0.12), (0, 0.06, 0.74), STEEL_DARK)
    m.box((0.1, 0.06, 0.18), (0, 0.16, 0.82), STEEL_DARK)
    return m


def chain_tongs() -> Model:
    m = Model("weapon-chain-tongs")
    m.box((0.06, 0.06, 0.85), (0, 0, 0.34), STEEL)
    for i in range(6):
        a = math.radians(60 * i)
        m.box((0.05, 0.05, 0.05), (math.cos(a) * 0.12, math.sin(a) * 0.12, 0.82), STEEL_DARK)
    return m


def power_tongs() -> Model:
    m = Model("weapon-power-tongs")
    m.box((0.07, 0.07, 0.6), (0, 0, 0.22), STEEL_DARK)
    m.box((0.36, 0.26, 0.34), (0, 0, 0.66), BLUE)
    m.cyl(0.1, 0.2, (0, 0.13, 0.66), STEEL, sides=8)
    return m


def collar_maul() -> Model:
    m = Model("weapon-collar-maul")
    m.box((0.06, 0.06, 0.8), (0, 0, 0.3), BROWN)
    m.cyl(0.16, 0.5, (0.25, 0, 0.78), STEEL_DARK, rot=(0, 0, 90), sides=8)
    m.cyl(0.17, 0.04, (0.25, 0, 0.78), SAFETY, rot=(0, 0, 90), sides=8)
    return m


def kelly_bar() -> Model:
    m = Model("weapon-kelly-bar")
    m.cyl(0.07, 1.1, (0, 0, -0.1), "#FFD23F", rot=(90, 0, 0), sides=6)
    m.cyl(0.13, 0.2, (0, 0, 0.9), "#E8B000", rot=(90, 0, 0), sides=6)
    m.sphere(0.07, (0, 0, 1.12), "#FF2BD6", rings=2, segs=6)
    return m


def gun(name: str, body: str, barrel: str, *, barrels: int = 1, length: float = 0.45, radius: float = 0.04, glow: str | None = None) -> Model:
    m = Model(name)
    m.box((0.08, 0.18, 0.1), (0, -0.08, 0.0), BLACK)  # grip
    m.box((0.12, 0.12, length * 0.6), (0, 0.04, length * 0.25), body)
    for i in range(barrels):
        x = (i - (barrels - 1) / 2) * 0.07
        m.cyl(radius, length, (x, 0.05, 0.05), barrel, rot=(90, 0, 0), sides=6)
    if glow:
        for k in range(3):
            m.cyl(radius * 1.8, 0.03, (0, 0.05, 0.15 + k * length * 0.25), glow, rot=(90, 0, 0), sides=8)
    m.anchor("muzzle", (0, 0.05, 0.05 + length))
    return m


def mud_gun(name: str, tank: str, nozzle_len: float, tank_r: float) -> Model:
    m = Model(name)
    m.cyl(tank_r, 0.4, (0, -0.1, -0.1), tank, sides=8)
    m.cyl(0.035, nozzle_len, (0, 0.1, 0.0), STEEL_DARK, rot=(90, 0, 0), sides=6)
    m.cone(0.06, 0.12, (0, 0.1, nozzle_len), BLACK, rot=(90, 0, 0), sides=6)
    m.anchor("muzzle", (0, 0.1, nozzle_len + 0.1))
    return m


def staff(name: str, pole: str, head: str, kind: str) -> Model:
    m = Model(name)
    m.cyl(0.025, 1.3, (0, -0.4, 0.05), pole, sides=6)
    top = (0, 0.9, 0.05)
    if kind == "thumper":
        m.box((0.2, 0.2, 0.2), top, head)
    elif kind == "vibro":
        m.box((0.32, 0.06, 0.32), top, head)
        m.box((0.1, 0.16, 0.1), (0, 0.8, 0.05), STEEL_DARK)
    elif kind == "airgun":
        for i in range(3):
            a = math.radians(120 * i)
            m.cyl(0.05, 0.22, (math.cos(a) * 0.08, 0.85, 0.05 + math.sin(a) * 0.08), head, sides=6)
    elif kind == "geophone":
        m.sphere(0.12, top, head, rings=3, segs=6)
        for i in range(4):
            a = math.radians(90 * i + 45)
            m.cone(0.025, 0.2, (math.cos(a) * 0.1, 0.92, 0.05 + math.sin(a) * 0.1), SAFETY, sides=4)
    else:  # FWI scepter
        m.sphere(0.15, (0, 0.98, 0.05), head, rings=3, segs=8)
        m.cyl(0.2, 0.03, (0, 0.98, 0.05), "#3FA7FF", rot=(90, 0, 0), sides=10)
        m.cyl(0.2, 0.03, (0, 0.98, 0.05), "#FFD23F", sides=10)
    return m


WEAPONS = {
    "weapon-tricone-bit": lambda: bit("weapon-tricone-bit", "#C9CED2", STEEL_DARK, "tricone"),
    "weapon-pdc-bit": lambda: bit("weapon-pdc-bit", STEEL_DARK, "#7FD0FF", "pdc"),
    "weapon-hybrid-bit": lambda: bit("weapon-hybrid-bit", STEEL_DARK, GLOW_ORANGE, "hybrid"),
    "weapon-diamond-bit": lambda: bit("weapon-diamond-bit", "#E8F7FF", "#FFFFFF", "diamond"),
    "weapon-black-diamond-bit": lambda: bit("weapon-black-diamond-bit", "#2B2B33", "#FF2BD6", "black"),
    "weapon-pipe-wrench": pipe_wrench,
    "weapon-chain-tongs": chain_tongs,
    "weapon-power-tongs": power_tongs,
    "weapon-collar-maul": collar_maul,
    "weapon-kelly-bar": kelly_bar,
    "weapon-wireline-pistol": lambda: gun("weapon-wireline-pistol", BLUE, STEEL, length=0.32),
    "weapon-tt-perf-gun": lambda: gun("weapon-tt-perf-gun", BLUE, STEEL, barrels=3, length=0.42, radius=0.03),
    "weapon-charge-carbine": lambda: gun("weapon-charge-carbine", "#4A3A8A", STEEL_DARK, length=0.7),
    "weapon-tcp-cannon": lambda: gun("weapon-tcp-cannon", "#B07800", STEEL_DARK, length=0.75, radius=0.08),
    "weapon-plasma-perforator": lambda: gun("weapon-plasma-perforator", "#3A1A4A", "#C0C8FF", length=0.7, radius=0.05, glow="#FF2BD6"),
    "weapon-mud-hose": lambda: mud_gun("weapon-mud-hose", "#8B6B47", 0.4, 0.1),
    "weapon-jet-gun": lambda: mud_gun("weapon-jet-gun", "#4FA3B5", 0.7, 0.1),
    "weapon-barite-blaster": lambda: mud_gun("weapon-barite-blaster", "#B8B0A0", 0.55, 0.15),
    "weapon-kill-mud-cannon": lambda: mud_gun("weapon-kill-mud-cannon", "#6B3F1F", 0.8, 0.18),
    "weapon-kill-pill": lambda: mud_gun("weapon-kill-pill", "#E6D3A3", 0.7, 0.16),
    "weapon-thumper-staff": lambda: staff("weapon-thumper-staff", BROWN, GREEN, "thumper"),
    "weapon-vibroseis-rod": lambda: staff("weapon-vibroseis-rod", STEEL_DARK, "#5FD18B", "vibro"),
    "weapon-airgun-array": lambda: staff("weapon-airgun-array", STEEL_DARK, "#3FA7FF", "airgun"),
    "weapon-geophone-spread": lambda: staff("weapon-geophone-spread", BLACK, "#FFB000", "geophone"),
    "weapon-fwi-scepter": lambda: staff("weapon-fwi-scepter", "#2B2B33", "#FF2BD6", "fwi"),
}


# ---------------------------------------------------------------------------- enemies

def eyes(m: Model, y: float, z: float, spread: float = 0.12, size: float = 0.08, color: str = BLACK) -> None:
    for x in (-spread, spread):
        m.box((size, size * 1.3, size * 0.5), (x, y, z), color)


def gas_kick() -> Model:
    m = Model("enemy-gas-kick")
    m.sphere(0.42, (0, 0.5, 0), "#BFE6F2", rings=4, segs=8)
    m.sphere(0.2, (0.3, 0.75, -0.1), "#E2F6FB", rings=3, segs=6)
    m.sphere(0.15, (-0.28, 0.28, 0.12), "#E2F6FB", rings=3, segs=6)
    eyes(m, 0.6, 0.38)
    return m


def sand_crawler() -> Model:
    m = Model("enemy-sand-crawler")
    for i, z in enumerate((-0.45, 0.0, 0.45)):
        m.sphere(0.36 - 0.04 * i, (0, 0.33, z), mix(SAND, BROWN, 0.15 * i), rings=3, segs=7, scale=(1.1, 0.75, 1))
        for x in (-0.38, 0.38):
            m.box((0.08, 0.25, 0.08), (x, 0.12, z), BROWN)
    eyes(m, 0.45, 0.78, 0.1, 0.07)
    return m


def flash_fire() -> Model:
    m = Model("enemy-flash-fire")
    m.sphere(0.4, (0, 0.55, 0), "#FF6A13", rings=4, segs=8)
    m.sphere(0.26, (0, 0.62, 0.12), GLOW_YELLOW, rings=3, segs=6)
    m.part("flame", (0, 0.8, 0))
    for i in range(5):
        a = math.radians(72 * i)
        m.cone(0.13, 0.5, (math.cos(a) * 0.2, 0.85, math.sin(a) * 0.2), "#FF8A3D", sides=5)
    m.cone(0.16, 0.7, (0, 0.9, 0), GLOW_YELLOW, sides=6)
    m.part("body")
    eyes(m, 0.55, 0.38, 0.12, 0.08, "#3A0A00")
    return m


def h2s_wraith() -> Model:
    m = Model("enemy-h2s-wraith")
    c = "#C6D42A"
    m.sphere(0.45, (0, 0.0, 0), c, rings=4, segs=8)
    for i in range(4):
        a = math.radians(90 * i + 45)
        m.sphere(0.25, (math.cos(a) * 0.35, -0.1, math.sin(a) * 0.35), mix(c, "#7D8A1A", 0.4), rings=3, segs=6)
    m.cone(0.25, 0.7, (0, -0.25, -0.25), "#7D8A1A", rot=(160, 0, 0), sides=6)
    eyes(m, 0.08, 0.42, 0.13, 0.09, "#2A3000")
    return m


def corrosion_beetle() -> Model:
    m = Model("enemy-corrosion-beetle")
    m.sphere(0.5, (0, 0.42, -0.05), RUST, rings=3, segs=8, scale=(1, 0.7, 1.25))
    m.box((0.06, 0.1, 1.1), (0, 0.72, -0.05), shade(RUST, 0.7))
    m.sphere(0.24, (0, 0.38, 0.6), "#5A2A10", rings=3, segs=6)
    for side in (-1, 1):
        m.wedge((0.06, 0.08, 0.3), (side * 0.12, 0.33, 0.85), "#2A1408", rot=(0, side * -20, 0))
        for z in (-0.3, 0.0, 0.3):
            m.beam((side * 0.4, 0.35, z), (side * 0.7, 0.0, z + 0.1), 0.06, "#2A1408")
    eyes(m, 0.45, 0.82, 0.1, 0.06, GLOW_ORANGE)
    return m


def shale_slinger() -> Model:
    m = humanoid("enemy-shale-slinger", "#4E5458", "#33383B", stripes=False, hat_kind="none", skin="#5D6468")
    for y in (0.95, 1.2, 1.45):
        m.box((0.7, 0.06, 0.4), (0, y, 0), "#2B2F31", rot=(0, 0, 8))
    m.box((0.3, 0.04, 0.22), (0.5, 1.55, 0.25), "#6E767A", rot=(30, 20, 0))
    eyes(m, 1.72, 0.16, 0.07, 0.05, GLOW_ORANGE)
    return m


def hydrate_golem() -> Model:
    m = Model("enemy-hydrate-golem")
    for x in (-0.22, 0.22):
        m.box((0.3, 0.7, 0.32), (x, 0.35, 0), ICE_DEEP)
    m.box((0.95, 0.85, 0.6), (0, 1.1, 0), ICE, rot=(0, 5, 0))
    m.box((0.5, 0.4, 0.45), (0, 1.72, 0.05), ICE)
    for x in (-0.65, 0.65):
        m.box((0.32, 0.85, 0.34), (x, 1.05, 0.05), ICE_DEEP, rot=(0, 0, -6 if x > 0 else 6))
        m.box((0.4, 0.34, 0.4), (x * 1.05, 0.6, 0.1), "#E8F7FF")
    for i in range(5):
        random.seed(i)
        m.cone(0.08, 0.35, (random.uniform(-0.35, 0.35), 1.5, random.uniform(-0.25, 0.1)), "#FFFFFF", sides=4)
    eyes(m, 1.75, 0.28, 0.1, 0.07, "#3FA7FF")
    return m


def bitumen_ooze() -> Model:
    m = Model("enemy-bitumen-ooze")
    m.sphere(0.55, (0, 0.45, 0), "#1C1712", rings=4, segs=9, scale=(1.1, 0.8, 1.1))
    m.sphere(0.25, (0.35, 0.7, -0.2), "#2E241A", rings=3, segs=6)
    m.sphere(0.18, (-0.4, 0.25, 0.3), "#2E241A", rings=3, segs=6)
    m.box((0.18, 0.04, 0.02), (-0.15, 0.82, 0.52), "#8A7A60")  # glossy highlight
    eyes(m, 0.6, 0.55, 0.14, 0.09, "#FFD76A")
    return m


def sour_bat() -> Model:
    m = Model("enemy-sour-bat")
    m.sphere(0.24, (0, 0, 0), "#7D8A1A", rings=3, segs=6)
    m.part("wings", (0, 0, 0))
    for side in (-1, 1):
        m.wedge((0.7, 0.05, 0.5), (side * 0.45, 0.0, -0.05), "#4A5200", rot=(0, side * 90, side * 10))
    m.part("body")
    for x in (-0.1, 0.1):
        m.cone(0.05, 0.16, (x, 0.18, 0.05), "#4A5200", sides=4)
    eyes(m, 0.04, 0.22, 0.07, 0.05, "#FFD23F")
    return m


def scale_brute() -> Model:
    m = Model("enemy-scale-brute")
    for x in (-0.25, 0.25):
        m.box((0.34, 0.7, 0.36), (x, 0.35, 0), "#A9A39A")
    m.box((1.05, 0.9, 0.7), (0, 1.15, 0), "#D9D4CB", bevel_top=0.1)
    for y in (0.85, 1.1, 1.35):
        m.box((1.12, 0.08, 0.76), (0, y, 0), "#B5AFA5")
    m.box((0.45, 0.38, 0.42), (0, 1.75, 0.08), "#C8C2B8")
    for x in (-0.72, 0.72):
        m.box((0.36, 0.9, 0.4), (x, 1.05, 0.05), "#B5AFA5")
        m.sphere(0.25, (x, 0.55, 0.1), "#E8E4DC", rings=3, segs=6)
    eyes(m, 1.78, 0.3, 0.1, 0.06, RED)
    return m


def riser_eel() -> Model:
    m = Model("enemy-riser-eel")
    for i in range(6):
        t = i / 5
        m.sphere(0.22 - 0.02 * i, (math.sin(t * 2.5) * 0.18, 0.2 + t * 1.3, -0.3 + t * 0.35), mix("#1A7D8A", "#0F4A52", t), rings=3, segs=7)
    m.box((0.3, 0.2, 0.35), (0, 1.55, 0.2), "#1A7D8A")
    m.wedge((0.28, 0.1, 0.3), (0, 1.42, 0.32), "#0F4A52", rot=(180, 0, 0))
    eyes(m, 1.62, 0.36, 0.09, 0.06, "#E8F06A")
    return m


def paraffin_crawler() -> Model:
    m = Model("enemy-paraffin-crawler")
    for i in range(5):
        z = -0.6 + i * 0.3
        m.sphere(0.26, (0, 0.28 + (0.05 if i % 2 else 0), z), mix("#F0E2A8", "#D9C47A", i / 4), rings=3, segs=7)
        for x in (-0.24, 0.24):
            m.box((0.06, 0.14, 0.06), (x, 0.07, z), "#B8A46A")
    eyes(m, 0.36, 0.55, 0.09, 0.06)
    return m


def boss_blowout() -> Model:
    m = Model("boss-the-blowout")
    # A burning BOP stack with arms of fire.
    m.box((1.2, 0.5, 1.2), (0, 0.25, 0), IRON)
    m.cyl(0.45, 0.9, (0, 0.5, 0), RED, sides=8)
    m.box((1.1, 0.3, 0.5), (0, 0.9, 0), "#8A1A24")
    m.cyl(0.35, 0.5, (0, 1.4, 0), STEEL_DARK, sides=8)
    m.part("flame", (0, 1.9, 0))
    m.cone(0.75, 2.2, (0, 1.9, 0), "#FF6A13", sides=7)
    m.cone(0.5, 2.6, (0, 1.9, 0), GLOW_YELLOW, sides=6)
    for side in (-1, 1):
        m.cone(0.3, 1.3, (side * 0.6, 2.2, 0), "#FF8A3D", rot=(0, 0, side * -55), sides=6)
        m.sphere(0.3, (side * 1.35, 2.9, 0), "#FF4A1A", rings=3, segs=6)
    m.part("body")
    eyes(m, 2.6, 0.45, 0.18, 0.12, "#3A0A00")
    return m


def boss_colossus() -> Model:
    m = Model("boss-salt-dome-colossus")
    pink = "#E8C8C0"
    for x in (-0.3, 0.3):
        m.box((0.4, 0.8, 0.45), (x, 0.4, 0), "#C9A8A0")
    m.box((1.3, 1.1, 0.85), (0, 1.35, 0), pink, bevel_top=0.15)
    m.sphere(0.6, (0, 2.1, 0.05), "#F2E0DA", rings=3, segs=8, scale=(1, 0.8, 1))  # the dome head
    for x in (-0.9, 0.9):
        m.box((0.45, 1.1, 0.5), (x, 1.25, 0.05), "#D8B8B0")
        m.box((0.6, 0.5, 0.6), (x * 1.05, 0.6, 0.1), "#F2E0DA")
    for i in range(6):
        random.seed(100 + i)
        m.box((0.18, 0.18, 0.18), (random.uniform(-0.5, 0.5), random.uniform(0.9, 1.8), 0.43), "#FFFFFF", rot=(45, 45, 0))
    eyes(m, 2.05, 0.6, 0.18, 0.1, "#7A2A2A")
    return m


def boss_kraken() -> Model:
    m = Model("boss-riser-kraken")
    purple = "#4A2A6B"
    m.sphere(0.85, (0, 1.6, 0), purple, rings=4, segs=9, scale=(1, 1.2, 1))
    m.part("tentacles", (0, 0.8, 0))
    for i in range(8):
        a = math.radians(45 * i)
        x, z = math.cos(a), math.sin(a)
        prev = (x * 0.4, 0.9, z * 0.4)
        for k in range(1, 4):
            nxt = (x * (0.4 + 0.35 * k), 0.9 - 0.28 * k + (0.15 if k == 3 else 0), z * (0.4 + 0.35 * k))
            m.pipe(prev, nxt, 0.16 - 0.03 * k, mix(purple, "#8A5AB0", k / 3), sides=6)
            prev = nxt
    m.part("body")
    m.cyl(0.18, 2.6, (0.9, 0, -0.6), STEEL_DARK, sides=8)  # the riser it climbs
    eyes(m, 1.8, 0.82, 0.25, 0.16, "#FFD23F")
    return m


def boss_leviathan() -> Model:
    m = Model("boss-hydrate-leviathan")
    for i in range(7):
        t = i / 6
        m.sphere(0.6 - 0.05 * i, (0, 0.6 + t * 0.8, -1.4 + t * 1.6), mix(ICE_DEEP, ICE, t), rings=3, segs=8)
    m.box((0.9, 0.6, 1.0), (0, 1.6, 0.6), ICE)
    m.wedge((0.8, 0.25, 0.8), (0, 1.25, 0.7), ICE_DEEP, rot=(180, 0, 0))
    for side in (-1, 1):
        m.cone(0.12, 0.6, (side * 0.3, 1.9, 0.4), "#FFFFFF", rot=(-30, 0, side * 20), sides=4)
        m.wedge((1.0, 0.08, 0.7), (side * 0.8, 1.2, -0.4), "#E8F7FF", rot=(0, side * 80, side * 20))
    for i in range(6):
        m.cone(0.1, 0.4, (0, 1.2 + i * 0.1, -1.2 + i * 0.35), "#FFFFFF", sides=4)
    eyes(m, 1.75, 1.1, 0.25, 0.1, "#3FA7FF")
    return m


# ---------------------------------------------------------------------------- units

def unit_roughneck() -> Model:
    m = humanoid("unit-roughneck", HIVIS, SAFETY)
    m.box((0.05, 0.05, 0.6), (0.45, 0.9, 0.35), BROWN)
    m.box((0.14, 0.14, 0.22), (0.45, 0.9, 0.68), STEEL_DARK)  # sledgehammer
    return m


def unit_tong_hand() -> Model:
    m = humanoid("unit-tong-hand", "#E67E22", WHITE)
    m.box((0.06, 0.06, 1.2), (0.45, 0.95, 0.6), STEEL)
    m.box((0.16, 0.16, 0.12), (0.45, 0.95, 1.2), STEEL_DARK)
    return m


def unit_chain_spinner() -> Model:
    m = humanoid("unit-chain-spinner", "#D35400", SAFETY)
    for i in range(10):
        a = math.radians(36 * i)
        m.box((0.07, 0.07, 0.07), (math.cos(a) * 0.55, 1.0, math.sin(a) * 0.55), STEEL_DARK)
    return m


def unit_wildcatter() -> Model:
    m = humanoid("unit-wildcatter", "#7A4A2A", "#3A2A1C", stripes=False, hat_kind="cowboy")
    m.box((0.04, 0.04, 0.7), (0.45, 0.9, 0.3), BROWN)
    m.wedge((0.06, 0.12, 0.35), (0.45, 0.9, 0.7), STEEL, rot=(90, 0, 0))  # pickaxe
    return m


def unit_sniper() -> Model:
    m = humanoid("unit-wireline-sniper", BLUE, "#FF6A13")
    m.cyl(0.03, 1.3, (0.42, 1.05, -0.1), BLACK, rot=(90, 0, 0), sides=5)
    m.box((0.08, 0.1, 0.12), (0.42, 1.12, 0.5), STEEL_DARK)
    return m


def unit_rivet() -> Model:
    m = humanoid("unit-rivet-gunner", "#6E767A", WHITE)
    m.box((0.6, 0.7, 0.05), (0, 1.0, 0.2), BROWN)  # welding apron
    m.box((0.14, 0.18, 0.4), (0.45, 0.95, 0.3), "#B07800")
    return m


def unit_hunter() -> Model:
    m = humanoid("unit-kick-hunter", GREEN, HIVIS)
    m.box((0.66, 0.9, 0.04), (0, 1.1, -0.22), "#3A5A40")  # cape
    m.cyl(0.03, 1.0, (0.42, 1.05, -0.05), BROWN, rot=(90, 0, 0), sides=5)
    return m


def unit_flare_gunner() -> Model:
    m = humanoid("unit-flare-gunner", RED, SAFETY)
    m.cyl(0.07, 0.5, (0.42, 1.05, 0.05), "#FF8A3D", rot=(90, 0, 0), sides=6)
    return m


def unit_iron_roughneck() -> Model:
    m = Model("unit-iron-roughneck")
    for x in (-0.28, 0.28):
        m.box((0.3, 0.6, 0.4), (x, 0.3, 0), STEEL_DARK)
    m.box((1.0, 0.85, 0.7), (0, 1.0, 0), SAFETY, bevel_top=0.1)
    m.box((1.04, 0.12, 0.74), (0, 0.72, 0), BLACK)
    m.box((0.5, 0.3, 0.45), (0, 1.58, 0.05), STEEL)
    m.box((0.36, 0.08, 0.04), (0, 1.6, 0.28), GLOW_ORANGE)
    for x in (-0.68, 0.68):
        m.box((0.3, 0.3, 0.7), (x, 1.05, 0.25), STEEL_DARK)
        m.box((0.36, 0.36, 0.2), (x, 1.05, 0.65), IRON)  # tong jaws
    return m


def unit_medic() -> Model:
    m = humanoid("unit-hse-medic", WHITE, WHITE)
    m.box((0.2, 0.06, 0.02), (0, 1.25, 0.19), GREEN)
    m.box((0.06, 0.2, 0.02), (0, 1.25, 0.19), GREEN)
    m.box((0.06, 0.9, 0.7), (-0.5, 1.0, 0.25), "#7FD0FF")  # blast shield
    return m


def unit_drone() -> Model:
    m = Model("unit-flare-drone")
    m.box((0.5, 0.18, 0.5), (0, 0, 0), IRON)
    m.part("rotors", (0, 0.15, 0))
    for x in (-0.45, 0.45):
        for z in (-0.45, 0.45):
            m.beam((0, 0.05, 0), (x, 0.1, z), 0.05, STEEL_DARK)
            m.cyl(0.22, 0.02, (x, 0.12, z), "#C0C8D0", sides=8)
    m.part("body")
    m.cyl(0.07, 0.4, (0, -0.15, 0.05), "#FF8A3D", rot=(90, 0, 0), sides=6)
    m.box((0.12, 0.06, 0.04), (0, 0.02, 0.26), RED)
    return m


def unit_buggy() -> Model:
    m = Model("unit-swamp-buggy")
    m.box((1.0, 0.35, 1.5), (0, 0.55, 0), "#3A5A40", bevel_top=0.1)
    for x in (-0.55, 0.55):
        for z in (-0.5, 0.5):
            m.cyl(0.32, 0.25, (x - (0.12 if x < 0 else -0.12) + (0.0), 0.32, z), BLACK, rot=(0, 0, 90), sides=8)
    m.pipe((-0.35, 0.75, 0.55), (0.35, 0.75, 0.55), 0.03, STEEL)
    rider = humanoid("rider", "#5C6B3A", SAFETY, scale=0.8)
    m.merge(rider, (0, 0.55, -0.15))
    return m


# ---------------------------------------------------------------------------- buildings

def lattice_tower(m: Model, base: float, top: float, height: float, color: str, y0: float = 0.0, levels: int = 5, thick: float = 0.1) -> None:
    """Four tapered legs with horizontal and diagonal bracing: a derrick."""
    def corner(level: float, sx: int, sz: int):
        w = base + (top - base) * level
        return (sx * w / 2, y0 + height * level, sz * w / 2)
    corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    for sx, sz in corners:
        m.beam(corner(0, sx, sz), corner(1, sx, sz), thick * 1.3, color)
    for k in range(levels):
        a, b = k / levels, (k + 1) / levels
        for i in range(4):
            c1, c2 = corners[i], corners[(i + 1) % 4]
            m.beam(corner(b, *c1), corner(b, *c2), thick, color)
            m.beam(corner(a, *c1), corner(b, *c2), thick * 0.7, color)


def main_rig(tier: int) -> Model:
    name = "building-main-rig" if tier == 0 else f"building-main-rig@rig-t{tier}"
    m = Model(name)
    # substructure and rig floor
    m.box((6.4, 0.3, 6.4), (0, 0.15, 0), STEEL_DARK)
    for x in (-2.6, 2.6):
        for z in (-2.6, 2.6):
            m.box((0.6, 2.5, 0.6), (x, 1.4, z), "#B8B0A0")
    m.box((6.0, 0.4, 6.0), (0, 2.8, 0), SAFETY if tier >= 2 else "#D9C49A")
    m.box((6.1, 0.08, 6.1), (0, 3.04, 0), IRON)
    # derrick
    height = 9.0 + tier * 1.0
    lattice_tower(m, 4.4, 1.2, height, "#E9EEF0" if tier < 3 else "#FFB000", y0=3.0, levels=6, thick=0.14)
    m.box((1.8, 0.6, 1.8), (0, 3.0 + height + 0.3, 0), RED)  # crown block
    # drawworks, doghouse, mud tanks
    m.box((2.4, 1.2, 1.4), (0, 3.6, -2.2), BLUE)
    m.box((2.4, 2.0, 2.0), (-2.2, 4.0, 2.2), WHITE)
    m.roof((2.5, 0.5, 2.1), (-2.2, 5.0, 2.2), RED)
    m.box((1.8, 1.2, 4.0), (4.2, 0.6, 0), "#5D6B70")
    m.box((1.8, 0.1, 4.0), (4.2, 1.25, 0), SAFETY)
    # BOP and wellhead under the floor
    m.cyl(0.55, 1.6, (0, 0.3, 0), RED, sides=8)
    m.box((1.5, 0.4, 0.6), (0, 1.2, 0), "#8A1A24")
    # stairs
    for i in range(6):
        m.box((1.0, 0.12, 0.4), (-3.6, 0.3 + i * 0.45, -2.4 + i * 0.4), SAFETY)
    if tier >= 1:
        # rig floor gunners: rails and a gun
        for x in (-2.9, 2.9):
            m.box((0.08, 0.9, 5.8), (x, 3.45, 0), SAFETY)
        m.part("gun", (2.2, 3.4, 2.2))
        m.box((0.6, 0.6, 0.6), (2.2, 3.4, 2.2), STEEL_DARK)
        m.cyl(0.08, 0.9, (2.2, 3.5, 2.4), BLACK, rot=(90, 0, 0), sides=6)
        m.part("body")
        m.anchor("muzzle", (2.2, 3.6, 3.3))
    if tier >= 2:
        m.box((1.0, 1.6, 1.0), (0, 3.0 + height * 0.55, 0), "#FFB000")  # top drive
        m.pipe((0, 3.0, 0), (0, 3.0 + height * 0.5, 0), 0.08, STEEL)
    if tier >= 3:
        m.cyl(0.25, 1.4, (0.7, 3.0 + height + 0.6, 0), BLACK, rot=(0, 0, -25), sides=6)  # crown flare cannon
        m.anchor("muzzle", (1.2, 3.0 + height + 1.8, 0))
        for x in (-0.9, 0.9):
            m.box((0.3, 0.3, 0.3), (x, 3.0 + height + 0.8, 0.9), GLOW_YELLOW)
    return m


def pumpjack(tier: int) -> Model:
    names = {0: "building-pumpjack", 1: "building-pumpjack@pumpjack-t1", 2: "building-pumpjack@pumpjack-t2"}
    m = Model(names[tier])
    k = 1.0 + 0.12 * tier
    body = ["#4E5B60", "#2F6FB5", "#2FA36B"][tier]
    m.box((1.4 * k, 0.25, 3.4 * k), (0, 0.12, 0), IRON)
    # samson post (A-frame)
    for x in (-0.45, 0.45):
        m.beam((x * k, 0.25, -0.5 * k), (0, 2.3 * k, 0), 0.14, body)
        m.beam((x * k, 0.25, 0.5 * k), (0, 2.3 * k, 0), 0.14, body)
    # gearbox and crank with counterweights
    m.box((0.7, 0.7, 0.7), (0, 0.6, -1.25 * k), IRON)
    for x in (-0.45, 0.45):
        m.box((0.12, 0.9, 0.5), (x, 0.65, -1.25 * k), RED)
    # wellhead with a little Christmas tree
    m.cyl(0.14, 0.6, (0, 0.25, 1.55 * k), STEEL, sides=6)
    m.box((0.4, 0.12, 0.12), (0, 0.6, 1.55 * k), RED)
    # walking beam with horse head: the "beam" part rocks around the saddle
    m.part("beam", (0, 2.35 * k, 0))
    m.box((0.22, 0.32, 3.2 * k), (0, 2.45 * k, 0.1), body)
    m.box((0.3, 1.0 * k, 0.35), (0, 2.1 * k, 1.75 * k), body)  # horse head
    m.wedge((0.3, 0.3, 0.35), (0, 2.6 * k, 1.75 * k), body)
    m.cyl(0.03, 1.6 * k, (0, 0.6, 1.9 * k), BLACK, sides=4)  # bridle
    for x in (-0.18, 0.18):
        m.beam((x, 2.4 * k, -1.4 * k), (x, 0.9, -1.25 * k), 0.08, STEEL_DARK)  # pitman arms
    m.part("body")
    if tier >= 2:
        m.box((0.5, 0.7, 0.3), (0.7, 0.6, 1.0), WHITE)  # rod pump controller
        m.box((0.9, 0.04, 0.6), (0.7, 1.2, 1.0), "#1F3A6B", rot=(30, 0, 0))  # solar panel
    return m


def tank(m: Model, x: float, z: float, r: float, h: float, color: str) -> None:
    m.cyl(r, h, (x, 0, z), color, sides=10)
    m.cyl(r * 1.02, 0.12, (x, h, z), shade(color, 0.85), sides=10, top_radius=r * 0.7)
    m.box((0.12, h, 0.12), (x + r * 0.8, h / 2, z + r * 0.6), SAFETY)  # ladder


def tank_battery(variant: str) -> Model:
    name = "building-tank-battery" + ("" if variant == "" else "@" + variant)
    m = Model(name)
    m.box((4.2, 0.2, 3.2), (0, 0.1, 0), "#8A7A60")
    n = 4 if variant == "tank-farm" else 2
    for i in range(n):
        x = -1.3 + i * (2.6 / max(1, n - 1)) if n > 1 else 0
        tank(m, x, -0.6 if n == 2 else (-0.6 if i % 2 == 0 else 0.7), 0.75, 2.4, "#D9D4CB" if i % 2 == 0 else "#C8C2B8")
    m.cyl(0.35, 1.4, (1.4, 0.7, 1.0), STEEL, rot=(0, 0, 90), sides=8)  # horizontal separator
    m.pipe((-1.3, 1.5, -0.6), (1.4, 1.5, 1.0), 0.06, STEEL_DARK)
    if variant in ("vapor-recovery", "tank-farm"):
        m.box((1.2, 0.9, 0.8), (-1.2, 0.55, 1.1), GREEN)
        m.cyl(0.08, 1.2, (-1.5, 1.0, 1.1), STEEL, sides=6)
    if variant in ("fire-monitor", "deluge-skid"):
        m.cyl(0.18, 1.1, (1.8, 0.2, 1.3), RED, sides=8)
        m.part("top", (1.8, 1.4, 1.3))
        m.box((0.3, 0.3, 0.3), (1.8, 1.4, 1.3), RED)
        m.cyl(0.08, 0.8, (1.8, 1.45, 1.3), RED, rot=(70, 0, 0), sides=6)
        m.part("body")
        m.anchor("muzzle", (1.8, 1.75, 2.05))
    if variant == "deluge-skid":
        m.box((1.0, 1.0, 0.8), (-1.4, 0.6, 1.2), RED)
    return m


def derrick_tower(variant: str) -> Model:
    name = "building-derrick-tower" + ("" if variant == "" else "@" + variant)
    m = Model(name)
    height = {"": 4.6, "crown-sniper": 6.0, "flare-stack": 4.6, "monkeyboard-overwatch": 7.2, "thermal-oxidizer": 5.0}[variant]
    m.box((2.2, 0.3, 2.2), (0, 0.15, 0), STEEL_DARK)
    if variant in ("flare-stack", "thermal-oxidizer"):
        r = 0.35 if variant == "flare-stack" else 0.6
        m.cyl(r, height, (0, 0.3, 0), "#5D6B70", sides=8, top_radius=r * 0.8)
        for y in (1.5, 3.0):
            m.cyl(r * 1.2, 0.12, (0, y, 0), SAFETY, sides=8)
        m.part("top", (0, height + 0.3, 0))
        m.cyl(r * 0.9, 0.4, (0, height + 0.3, 0), BLACK, sides=8)
        m.cone(r * 1.2, 1.4 if variant == "flare-stack" else 1.8, (0, height + 0.7, 0), "#FF8A3D", sides=6)
        m.cone(r * 0.7, 1.8 if variant == "flare-stack" else 2.3, (0, height + 0.7, 0), GLOW_YELLOW, sides=5)
        m.part("body")
        m.anchor("muzzle", (0, height + 1.2, 0))
        return m
    color = {"": "#E9EEF0", "crown-sniper": "#BFD8E8", "monkeyboard-overwatch": "#FFB000"}[variant]
    lattice_tower(m, 1.8, 0.7, height, color, y0=0.3, levels=4, thick=0.1)
    m.box((1.4, 0.15, 1.4), (0, height * 0.75, 0), SAFETY)  # monkeyboard
    m.part("top", (0, height + 0.3, 0))
    m.box((0.9, 0.5, 0.9), (0, height + 0.5, 0), STEEL_DARK)
    gun_len = 1.6 if variant != "" else 0.9
    m.cyl(0.08, gun_len, (0, height + 0.6, 0.2), BLACK, rot=(90, 0, 0), sides=6)
    gunner = humanoid("gunner", HIVIS, SAFETY, scale=0.55)
    m.merge(gunner, (0, height + 0.6, -0.2))
    if variant == "monkeyboard-overwatch":
        m.box((0.4, 0.4, 0.3), (0.5, height + 0.9, 0.2), GLOW_YELLOW)  # searchlight
    m.part("body")
    m.anchor("muzzle", (0, height + 0.6, 0.3 + gun_len))
    return m


def blast_wall(variant: str) -> Model:
    name = "building-blast-wall" + ("" if variant == "" else "@" + variant)
    m = Model(name)
    w = 3.2
    for x in (-w / 2 + 0.12, 0, w / 2 - 0.12):
        m.box((0.22, 2.1, 0.3), (x, 1.05, 0), IRON)
    m.box((w, 1.7, 0.18), (0, 0.95, 0), "#7A8A90")
    for i in range(6):
        m.box((w / 6 * 0.95, 0.18, 0.2), (-w / 2 + w / 12 + i * w / 6, 1.85, 0), SAFETY if i % 2 == 0 else BLACK)
    if variant == "reinforced-berm":
        m.wedge((w, 1.3, 1.6), (0, 0, -0.85), "#8B6B47", rot=(0, 0, 0))
        m.box((w, 0.4, 0.4), (0, 0.2, 0.3), "#6B5A3A")
    if variant == "barbed-casing":
        for i in range(5):
            x = -w / 2 + 0.3 + i * (w - 0.6) / 4
            m.pipe((x, 0.2, 0.25), (x, 1.2, 0.7), 0.08, RUST)
            m.cone(0.06, 0.3, (x, 1.25, 0.72), STEEL, rot=(40, 0, 0), sides=4)
    return m


def trailer(m: Model, pos, size, color, roof_color) -> None:
    x, y, z = pos
    m.box(size, (x, y + size[1] / 2 + 0.3, z), color)
    m.box((size[0] * 0.95, 0.3, size[2] * 0.6), (x, 0.15, z), IRON)
    m.box((size[0] + 0.1, 0.1, size[2] + 0.1), (x, y + size[1] + 0.35, z), roof_color)
    m.box((0.7, 1.4, 0.06), (x - size[0] * 0.25, y + 1.0, z + size[2] / 2 + 0.03), shade(color, 0.6))  # door
    m.box((0.9, 0.5, 0.06), (x + size[0] * 0.2, y + 1.5, z + size[2] / 2 + 0.03), "#7FB8DA")  # window


def crew_camp() -> Model:
    m = Model("building-crew-camp")
    trailer(m, (-0.9, 0, 0), (2.4, 2.0, 1.6), WHITE, RED)
    trailer(m, (1.1, 0, -0.6), (1.6, 1.8, 1.4), "#D9C49A", BLUE)
    m.box((0.6, 0.4, 0.4), (-0.9, 2.65, -0.4), STEEL)  # AC unit
    m.cyl(0.04, 3.2, (2.1, 0, 1.0), STEEL, sides=4)
    m.box((0.8, 0.5, 0.04), (2.5, 2.8, 1.0), HIVIS)  # flag
    return m


def perforation_range() -> Model:
    m = Model("building-perforation-range")
    m.box((3.2, 0.2, 2.4), (0, 0.1, 0), "#8A7A60")
    for x in (-1.4, 1.4):
        for z in (-1.0, 1.0):
            m.box((0.15, 2.2, 0.15), (x, 1.2, z), STEEL_DARK)
    m.roof((3.4, 0.6, 2.6), (0, 2.3, 0), BLUE)
    m.box((2.4, 0.9, 0.5), (0, 0.65, -0.6), STEEL_DARK)  # gun rack
    for i in range(4):
        m.cyl(0.06, 1.0, (-0.9 + i * 0.6, 0.8, -0.3), STEEL, rot=(70, 0, 0), sides=6)
    for x in (-0.9, 0.9):
        m.box((0.8, 0.8, 0.08), (x, 0.9, 2.0), WHITE)  # targets
        m.box((0.4, 0.4, 0.1), (x, 0.9, 2.0), RED)
    return m


def company_office() -> Model:
    m = Model("building-company-office")
    trailer(m, (0, 0, 0), (3.2, 2.2, 2.0), "#E9EEF0", "#5D6B70")
    m.box((3.0, 0.4, 0.06), (0, 2.6, 1.04), BLUE)  # sign band
    m.cyl(0.05, 3.0, (1.3, 2.6, -0.6), STEEL, sides=4)  # antenna mast
    m.sphere(0.4, (-1.0, 3.1, -0.4), "#C8C2B8", rings=2, segs=8, scale=(1, 0.4, 1), rot=(-30, 0, 0))  # sat dish
    m.box((0.8, 0.12, 1.2), (0, 0.42, 1.6), SAFETY)  # steps
    return m


# ---------------------------------------------------------------------------- decor & props

def decor_rock() -> Model:
    m = Model("decor-rock")
    m.sphere(0.7, (0, 0.4, 0), "#8B7B66", rings=3, segs=6, scale=(1.2, 0.8, 1))
    m.sphere(0.4, (0.6, 0.25, 0.3), "#7A6A55", rings=2, segs=5)
    return m


def decor_cactus() -> Model:
    m = Model("decor-cactus")
    g = "#4F7A3A"
    m.cyl(0.22, 2.2, (0, 0, 0), g, sides=7)
    m.cyl(0.14, 0.8, (0.35, 0.9, 0), g, sides=6)
    m.cyl(0.14, 0.7, (-0.32, 1.2, 0), g, sides=6)
    m.box((0.5, 0.16, 0.16), (0.2, 0.95, 0), g)
    m.box((0.45, 0.16, 0.16), (-0.18, 1.25, 0), g)
    return m


def decor_shrub() -> Model:
    m = Model("decor-shrub")
    for i, (x, z, r) in enumerate(((0, 0, 0.5), (0.4, 0.2, 0.35), (-0.35, -0.2, 0.38))):
        m.sphere(r, (x, r * 0.7, z), mix("#6B7A3A", "#4E5A2A", i / 2), rings=3, segs=6)
    return m


def decor_old_tank() -> Model:
    m = Model("decor-old-tank")
    m.cyl(1.0, 1.6, (0, 0, 0), RUST, sides=9)
    m.cyl(1.02, 0.1, (0, 1.6, 0), "#6A3A1A", sides=9, top_radius=0.5)
    return m


def decor_tumbleweed() -> Model:
    m = Model("decor-tumbleweed")
    m.sphere(0.45, (0, 0.45, 0), "#B89A6A", rings=3, segs=6)
    m.sphere(0.3, (0.15, 0.55, 0.1), "#A88A5A", rings=2, segs=5)
    return m


def decor_mangrove() -> Model:
    m = Model("decor-mangrove")
    for i in range(5):
        a = math.radians(72 * i)
        m.beam((math.cos(a) * 0.8, 0, math.sin(a) * 0.8), (0, 1.2, 0), 0.12, "#5C4A3A")
    m.cyl(0.18, 1.8, (0, 1.1, 0), "#5C4A3A", sides=6)
    m.sphere(1.3, (0, 3.2, 0), "#3E6B2E", rings=3, segs=7, scale=(1.2, 0.7, 1.2))
    return m


def decor_reeds() -> Model:
    m = Model("decor-reeds")
    random.seed(7)
    for i in range(7):
        x, z = random.uniform(-0.4, 0.4), random.uniform(-0.4, 0.4)
        m.cyl(0.04, random.uniform(0.8, 1.4), (x, 0, z), "#7A8A3A", sides=4)
    return m


def decor_pool() -> Model:
    m = Model("decor-pool")
    m.cyl(1.0, 0.05, (0, 0, 0), "#2E4A3A", sides=10)
    return m


def decor_pine() -> Model:
    m = Model("decor-pine")
    m.cyl(0.18, 1.0, (0, 0, 0), "#5C4A3A", sides=6)
    for i, (y, r) in enumerate(((0.8, 1.2), (1.6, 0.95), (2.3, 0.7))):
        m.cone(r, 1.3, (0, y, 0), mix("#2E5A40", "#3E7A52", i / 2), sides=7)
        m.cone(r * 0.9, 0.35, (0, y + 0.9, 0), WHITE, sides=7)
    return m


def decor_ice() -> Model:
    m = Model("decor-ice")
    m.box((1.2, 1.0, 1.0), (0, 0.5, 0), ICE, rot=(0, 20, 8))
    m.box((0.6, 1.4, 0.6), (0.6, 0.7, 0.3), ICE_DEEP, rot=(0, 40, -10))
    return m


def decor_snowdrift() -> Model:
    m = Model("decor-snowdrift")
    m.sphere(1.2, (0, 0, 0), "#F4FAFF", rings=3, segs=8, scale=(1.4, 0.45, 1))
    return m


def decor_pipeline() -> Model:
    m = Model("decor-pipeline")
    m.cyl(0.35, 6.0, (3.0, 1.0, 0), STEEL, rot=(0, 0, 90), sides=8)  # +Y rotates onto -X
    for x in (-2.4, 0, 2.4):
        m.box((0.2, 1.0, 0.6), (x, 0.5, 0), "#5C4A3A")
    return m


def decor_container() -> Model:
    m = Model("decor-container")
    random.seed(3)
    c = random.choice([BLUE, RED, "#2FA36B", HIVIS])
    m.box((2.4, 2.4, 6.0), (0, 1.2, 0), c)
    for z in range(-2, 3):
        m.box((2.45, 2.2, 0.06), (0, 1.2, z * 1.2), shade(c, 0.8))
    return m


def decor_crane() -> Model:
    m = Model("decor-crane")
    m.cyl(0.8, 3.0, (0, 0, 0), SAFETY, sides=8)
    m.box((1.6, 1.4, 2.0), (0, 3.6, 0), SAFETY)
    lattice_tower(m, 0.8, 0.4, 8.0, SAFETY, y0=3.5, levels=4, thick=0.08)
    return m


def decor_lifeboat() -> Model:
    m = Model("decor-lifeboat")
    m.sphere(1.0, (0, 1.2, 0), HIVIS, rings=3, segs=8, scale=(1, 0.8, 2.5))
    m.box((1.6, 0.2, 4.0), (0, 0.4, 0), STEEL_DARK)
    return m


def decor_pipe_rack() -> Model:
    m = Model("decor-pipe-rack")
    for x in (-1.5, 1.5):
        m.box((0.2, 1.2, 1.4), (x, 0.6, 0), SAFETY)
    for i in range(3):
        for j in range(4):
            m.cyl(0.12, 4.0, (2.0, 1.25 + i * 0.25, -0.45 + j * 0.3), STEEL, rot=(0, 0, 90), sides=6)
    return m


def decor_barrels() -> Model:
    m = Model("decor-barrels")
    for i, (x, z) in enumerate(((0, 0), (0.6, 0.1), (0.3, 0.55), (-0.5, 0.4))):
        m.cyl(0.28, 0.85, (x, 0, z), [BLUE, RED, "#2FA36B", SAFETY][i], sides=8)
    return m


def decor_storage_tank() -> Model:
    m = Model("decor-storage-tank")
    tank(m, 0, 0, 2.2, 4.0, "#C8C2B8")
    return m


def prop_barrel() -> Model:
    m = Model("prop-barrel")
    m.cyl(0.3, 0.9, (0, 0, 0), "#FFB000", sides=8)
    for y in (0.25, 0.65):
        m.cyl(0.31, 0.06, (0, y, 0), "#6B4400", sides=8)
    return m


def prop_chevron() -> Model:
    m = Model("prop-chevron")
    for side in (-1, 1):
        m.box((0.2, 0.05, 1.2), (side * 0.4, 0, 0), (1, 1, 1), rot=(0, side * -45, 0))
    return m


def prop_spawn_marker() -> Model:
    m = Model("prop-spawn-marker")
    for i in range(8):
        a = math.radians(45 * i)
        m.box((0.7, 0.05, 0.25), (math.cos(a) * 1.6, 0, math.sin(a) * 1.6), (1, 1, 1), rot=(0, -math.degrees(a), 0))
    return m


def prop_slot_pad() -> Model:
    m = Model("prop-slot-pad")
    m.cyl(1.2, 0.06, (0, 0, 0), (1, 1, 1), sides=12)
    for i in range(4):
        a = math.radians(90 * i + 45)
        m.box((0.4, 0.08, 0.12), (math.cos(a) * 1.0, 0.04, math.sin(a) * 1.0), "#F2C200", rot=(0, -math.degrees(a), 0))
    return m


def prop_rubble() -> Model:
    m = Model("prop-rubble")
    random.seed(11)
    for i in range(8):
        m.box((random.uniform(0.2, 0.6), random.uniform(0.1, 0.35), random.uniform(0.2, 0.5)),
              (random.uniform(-0.8, 0.8), 0.1, random.uniform(-0.8, 0.8)), random.choice([STEEL_DARK, IRON, RUST, "#5D6B70"]),
              rot=(random.uniform(-20, 20), random.uniform(0, 180), random.uniform(-20, 20)))
    return m


def prop_walkway() -> Model:
    m = Model("prop-walkway")
    m.box((4.0, 0.2, 8.0), (0, -0.1, 0), "#6E7A80")
    for x in (-1.9, 1.9):
        m.box((0.08, 0.9, 8.0), (x, 0.45, 0), SAFETY)
    for z in range(-3, 4):
        m.box((4.1, 0.05, 0.12), (0, 0.01, z * 1.1), "#59656B")
    return m


def prop_jacket_leg() -> Model:
    m = Model("prop-jacket-leg")
    m.cyl(0.8, 14.0, (0, -14.0, 0), "#F2C200", sides=8)
    m.cyl(0.85, 0.6, (0, -2.6, 0), BLACK, sides=8)
    return m


def prop_sea() -> Model:
    m = Model("prop-sea")
    m.box((20.0, 0.1, 20.0), (0, 0, 0), (1, 1, 1))
    return m


# ---------------------------------------------------------------------------- registry

MODELS = {
    "hero-driller": hero_driller,
    "hero-toolpusher": hero_toolpusher,
    "hero-perforator": hero_perforator,
    "hero-mud-engineer": hero_mud_engineer,
    "hero-geophysicist": hero_geophysicist,
    **WEAPONS,
    "enemy-gas-kick": gas_kick,
    "enemy-sand-crawler": sand_crawler,
    "enemy-flash-fire": flash_fire,
    "enemy-h2s-wraith": h2s_wraith,
    "enemy-corrosion-beetle": corrosion_beetle,
    "enemy-shale-slinger": shale_slinger,
    "enemy-hydrate-golem": hydrate_golem,
    "enemy-bitumen-ooze": bitumen_ooze,
    "enemy-sour-bat": sour_bat,
    "enemy-scale-brute": scale_brute,
    "enemy-riser-eel": riser_eel,
    "enemy-paraffin-crawler": paraffin_crawler,
    "boss-the-blowout": boss_blowout,
    "boss-salt-dome-colossus": boss_colossus,
    "boss-riser-kraken": boss_kraken,
    "boss-hydrate-leviathan": boss_leviathan,
    "unit-roughneck": unit_roughneck,
    "unit-tong-hand": unit_tong_hand,
    "unit-chain-spinner": unit_chain_spinner,
    "unit-wildcatter": unit_wildcatter,
    "unit-wireline-sniper": unit_sniper,
    "unit-rivet-gunner": unit_rivet,
    "unit-kick-hunter": unit_hunter,
    "unit-flare-gunner": unit_flare_gunner,
    "unit-iron-roughneck": unit_iron_roughneck,
    "unit-hse-medic": unit_medic,
    "unit-flare-drone": unit_drone,
    "unit-swamp-buggy": unit_buggy,
    "building-main-rig": lambda: main_rig(0),
    "building-main-rig@rig-t1": lambda: main_rig(1),
    "building-main-rig@rig-t2": lambda: main_rig(2),
    "building-main-rig@rig-t3": lambda: main_rig(3),
    "building-pumpjack": lambda: pumpjack(0),
    "building-pumpjack@pumpjack-t1": lambda: pumpjack(1),
    "building-pumpjack@pumpjack-t2": lambda: pumpjack(2),
    "building-tank-battery": lambda: tank_battery(""),
    "building-tank-battery@vapor-recovery": lambda: tank_battery("vapor-recovery"),
    "building-tank-battery@fire-monitor": lambda: tank_battery("fire-monitor"),
    "building-tank-battery@tank-farm": lambda: tank_battery("tank-farm"),
    "building-tank-battery@deluge-skid": lambda: tank_battery("deluge-skid"),
    "building-derrick-tower": lambda: derrick_tower(""),
    "building-derrick-tower@crown-sniper": lambda: derrick_tower("crown-sniper"),
    "building-derrick-tower@flare-stack": lambda: derrick_tower("flare-stack"),
    "building-derrick-tower@monkeyboard-overwatch": lambda: derrick_tower("monkeyboard-overwatch"),
    "building-derrick-tower@thermal-oxidizer": lambda: derrick_tower("thermal-oxidizer"),
    "building-blast-wall": lambda: blast_wall(""),
    "building-blast-wall@reinforced-berm": lambda: blast_wall("reinforced-berm"),
    "building-blast-wall@barbed-casing": lambda: blast_wall("barbed-casing"),
    "building-crew-camp": crew_camp,
    "building-perforation-range": perforation_range,
    "building-company-office": company_office,
    "decor-rock": decor_rock,
    "decor-cactus": decor_cactus,
    "decor-shrub": decor_shrub,
    "decor-old-tank": decor_old_tank,
    "decor-tumbleweed": decor_tumbleweed,
    "decor-mangrove": decor_mangrove,
    "decor-reeds": decor_reeds,
    "decor-pool": decor_pool,
    "decor-pine": decor_pine,
    "decor-ice": decor_ice,
    "decor-snowdrift": decor_snowdrift,
    "decor-pipeline": decor_pipeline,
    "decor-container": decor_container,
    "decor-crane": decor_crane,
    "decor-lifeboat": decor_lifeboat,
    "decor-pipe-rack": decor_pipe_rack,
    "decor-barrels": decor_barrels,
    "decor-storage-tank": decor_storage_tank,
    "prop-barrel": prop_barrel,
    "prop-chevron": prop_chevron,
    "prop-spawn-marker": prop_spawn_marker,
    "prop-slot-pad": prop_slot_pad,
    "prop-rubble": prop_rubble,
    "prop-walkway": prop_walkway,
    "prop-jacket-leg": prop_jacket_leg,
    "prop-sea": prop_sea,
}


def build(name: str) -> Model:
    m = MODELS[name]()
    m.name = name
    return m
