#!/usr/bin/env python3
"""Generate Assets/Rigfall/Resources/Data/maps.json from compact field layouts.

Every field (map) is described by its lanes (the directions enemies come from and
the night each lane opens), its palette and its enemy introduction order. This
script turns that into fixed building slots (Thronefall-style), spawn vectors and
decoration, so all five fields follow the same readable pattern:

  rig at the centre -> pumpjacks close in -> unit buildings and tank batteries
  between the lanes -> derrick towers flanking each lane -> blast walls across it.

Run:  python3 tools/make_maps.py            (writes the JSON)
      python3 tools/make_maps.py --check    (fails if the JSON is out of date)
"""
from __future__ import annotations

import json
import math
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/Rigfall/Resources/Data/maps.json"

FIELDS = [
    {
        "id": "permian-pad", "name": "Permian Pad", "setting": "onshore", "biome": "desert", "order": 0,
        "description": "A dusty multi-well pad in West Texas shale country. Wide open lanes and plenty of room for pumpjacks. The place to learn the job.",
        "unlockAfter": "", "shape": "disc", "radius": 34.0,
        "lanes": [(90, 1), (215, 3), (325, 6)],
        "palette": {"ground": "#C9A26B", "groundAlt": "#B88A55", "accent": "#8B5A2B", "water": "", "fog": "#E8C38A",
                     "skyDay": "#F4C98E", "skyNight": "#1C1B33", "sun": "#FFE2B0"},
        "decor": ["decor-rock", "decor-cactus", "decor-shrub", "decor-old-tank", "decor-tumbleweed"],
        "bgmDay": "bgm_day_desert", "bgmNight": "bgm_night",
        "enemyOrder": ["gas-kick", "sand-crawler", "flash-fire", "corrosion-beetle", "h2s-wraith", "shale-slinger",
                       "hydrate-golem", "bitumen-ooze", "sour-bat", "scale-brute", "paraffin-crawler", "riser-eel"],
    },
    {
        "id": "delta-swamp", "name": "Delta Swamp", "setting": "onshore", "biome": "swamp", "order": 1,
        "description": "Wells on board-roads through mangrove swamp. Waxy crawlers come out of the cold flowlines and bats nest in the flare.",
        "unlockAfter": "permian-pad", "shape": "disc", "radius": 33.0,
        "lanes": [(60, 1), (180, 3), (300, 5), (0, 9)],
        "palette": {"ground": "#5E7A3A", "groundAlt": "#4D6B33", "accent": "#3A2A1C", "water": "#2E4A3A", "fog": "#7D9A6A",
                     "skyDay": "#BFD8A8", "skyNight": "#101C1A", "sun": "#F2F2C8"},
        "decor": ["decor-mangrove", "decor-reeds", "decor-pool", "decor-shrub", "decor-rock"],
        "bgmDay": "bgm_day_swamp", "bgmNight": "bgm_night",
        "enemyOrder": ["gas-kick", "paraffin-crawler", "sour-bat", "bitumen-ooze", "h2s-wraith", "sand-crawler",
                       "flash-fire", "corrosion-beetle", "shale-slinger", "hydrate-golem", "scale-brute", "riser-eel"],
    },
    {
        "id": "arctic-slope", "name": "Arctic Slope", "setting": "onshore", "biome": "arctic", "order": 2,
        "description": "An ice pad on the North Slope. Hydrates form in every line, and the golems they make hit like trucks.",
        "unlockAfter": "delta-swamp", "shape": "disc", "radius": 35.0,
        "lanes": [(30, 1), (150, 2), (270, 4)],
        "palette": {"ground": "#E6EDF2", "groundAlt": "#CFDAE2", "accent": "#5D7A8C", "water": "", "fog": "#DDE8F0",
                     "skyDay": "#CFE3F2", "skyNight": "#0E1630", "sun": "#F4FAFF"},
        "decor": ["decor-pine", "decor-ice", "decor-snowdrift", "decor-rock", "decor-pipeline"],
        "bgmDay": "bgm_day_arctic", "bgmNight": "bgm_night",
        "enemyOrder": ["gas-kick", "sand-crawler", "paraffin-crawler", "shale-slinger", "hydrate-golem", "h2s-wraith",
                       "flash-fire", "corrosion-beetle", "bitumen-ooze", "sour-bat", "scale-brute", "riser-eel"],
    },
    {
        "id": "north-sea-jacket", "name": "North Sea Jacket", "setting": "offshore", "biome": "sea", "order": 3,
        "description": "A fixed steel jacket platform in a grey sea. Enemies climb up the risers onto three walkways. No room to waste.",
        "unlockAfter": "arctic-slope", "shape": "deck", "radius": 24.0,
        "lanes": [(0, 1), (120, 2), (240, 4)],
        "palette": {"ground": "#6E7A80", "groundAlt": "#5D696F", "accent": "#F2C200", "water": "#1F4E66", "fog": "#8FA3AE",
                     "skyDay": "#AFC3CF", "skyNight": "#0B1420", "sun": "#E8F0F4"},
        "decor": ["decor-container", "decor-crane", "decor-lifeboat", "decor-pipe-rack", "decor-barrels"],
        "bgmDay": "bgm_day_offshore", "bgmNight": "bgm_night",
        "enemyOrder": ["gas-kick", "riser-eel", "sour-bat", "corrosion-beetle", "h2s-wraith", "shale-slinger",
                       "flash-fire", "sand-crawler", "paraffin-crawler", "bitumen-ooze", "hydrate-golem", "scale-brute"],
    },
    {
        "id": "deepwater-fpso", "name": "Deepwater FPSO", "setting": "offshore", "biome": "deepsea", "order": 4,
        "description": "A floating production ship 200 km from shore, moored over 2,000 m of water. Four walkways, rusty decks and the hardest nights.",
        "unlockAfter": "north-sea-jacket", "shape": "deck", "radius": 25.0,
        "lanes": [(45, 1), (135, 2), (225, 3), (315, 5)],
        "palette": {"ground": "#7A6E64", "groundAlt": "#695E55", "accent": "#C2283A", "water": "#0F2F4A", "fog": "#56708A",
                     "skyDay": "#9DB4C8", "skyNight": "#070D18", "sun": "#FFF0D8"},
        "decor": ["decor-container", "decor-storage-tank", "decor-pipe-rack", "decor-barrels", "decor-crane"],
        "bgmDay": "bgm_day_offshore", "bgmNight": "bgm_night",
        "enemyOrder": ["gas-kick", "riser-eel", "shale-slinger", "corrosion-beetle", "h2s-wraith", "flash-fire",
                       "sour-bat", "bitumen-ooze", "paraffin-crawler", "hydrate-golem", "scale-brute", "sand-crawler"],
    },
]


def polar(r: float, deg: float) -> tuple[float, float]:
    a = math.radians(deg)
    return (round(r * math.cos(a), 2), round(r * math.sin(a), 2))


def ang_dist(a: float, b: float) -> float:
    d = abs((a - b) % 360)
    return min(d, 360 - d)


def build(field: dict) -> dict:
    rnd = random.Random(field["id"])
    lanes = field["lanes"]
    radius = field["radius"]
    deck = field["shape"] == "deck"
    slots: list[dict] = []
    taken: list[tuple[float, float, float]] = [(0.0, 0.0, 5.0)]  # x, z, keep-out radius (rig)

    def free(x: float, z: float, r: float) -> bool:
        return all(math.hypot(x - tx, z - tz) >= r + tr for tx, tz, tr in taken)

    def add(building: str, x: float, z: float, rot: float, r: float, length: float = 0.0) -> None:
        slots.append({"id": f"s{len(slots):02d}", "building": building, "x": x, "z": z,
                      "rot": round(rot % 360, 1), "length": length})
        taken.append((x, z, r))

    wall_r = radius * (0.56 if deck else 0.52)
    lane_angles = [a for a, _ in lanes]

    # Lane defences: three blast-wall segments across the lane, two towers flanking it.
    for a, _ in lanes:
        cx, cz = polar(wall_r, a)
        tx, tz = -math.sin(math.radians(a)), math.cos(math.radians(a))  # tangent
        for k in (-1, 0, 1):
            x, z = round(cx + tx * 3.3 * k, 2), round(cz + tz * 3.3 * k, 2)
            add("blast-wall", x, z, a, 1.2, 3.2)
        for side in (-1, 1):
            x, z = polar(wall_r - 3.6, a + side * math.degrees(math.asin(6.2 / (wall_r - 3.6))))
            add("derrick-tower", x, z, a, 1.6)

    # Between lanes: unit buildings and tank batteries.
    gaps = []
    sorted_angles = sorted(lane_angles)
    for i, a in enumerate(sorted_angles):
        b = sorted_angles[(i + 1) % len(sorted_angles)]
        span = (b - a) % 360 or 360
        gaps.append((a + span / 2, span))
    gaps.sort(key=lambda g: -g[1])

    def place_between(building: str, keep: float, radii: tuple[float, ...], fractions: tuple[float, ...]) -> bool:
        # Try every gap (widest first), every radius and every angular offset until one is free.
        for mid, span in gaps:
            for rr in radii:
                for f in fractions:
                    for side in ((1,) if f == 0 else (-1, 1)):
                        x, z = polar(rr, mid + side * f * span)
                        if deck and (abs(x) > radius - 3 or abs(z) > radius - 3):
                            continue
                        if free(x, z, keep):
                            add(building, x, z, mid + 180, keep)
                            return True
        return False

    unit_buildings = ["company-office", "perforation-range", "crew-camp"]
    if len(lanes) >= 4:
        unit_buildings.append("crew-camp")
    for b in unit_buildings:
        if not place_between(b, 2.4, (11.0, 12.5, 9.5, 14.0), (0.0, 0.12, 0.22)):
            raise SystemExit(f"{field['id']}: no room for {b}")
    for _ in range(3 if len(lanes) >= 4 else 2):
        if not place_between("tank-battery", 2.1, (13.0, 15.0, 10.5, 16.5), (0.25, 0.15, 0.33, 0.0)):
            raise SystemExit(f"{field['id']}: no room for a tank battery")

    # Pumpjacks: inner ring, avoiding the lanes; a few riskier outer ones.
    placed = 0
    for ring, count in ((7.5, 6), (9.5, 5), (16.5, 4)):
        for k in range(48):
            if count <= 0:
                break
            ang = k * 7.5 + rnd.uniform(-2, 2)
            if min(ang_dist(ang, la) for la in lane_angles) < (24 if ring > 12 else 16):
                continue
            x, z = polar(ring, ang)
            if deck and (abs(x) > radius - 3 or abs(z) > radius - 3):
                continue
            if free(x, z, 1.7):
                add("pumpjack", x, z, rnd.uniform(0, 360), 1.7)
                count -= 1
                placed += 1

    # Spawn vectors at the edge of each lane.
    spawns = []
    for i, (a, night) in enumerate(lanes):
        edge = radius + (9.0 if deck else -2.0)
        x, z = polar(edge, a)
        spawns.append({"id": f"lane{i}", "x": x, "z": z, "fromNight": night})

    walkways = []
    if deck:
        for a, _ in lanes:
            cx, cz = polar(radius + 4.5, a)
            walkways.append({"x": cx, "z": cz, "width": 7.0, "length": 12.0, "rot": round(a % 360, 1)})

    decor = []
    tries = 0
    while len(decor) < (38 if not deck else 24) and tries < 4000:
        tries += 1
        if deck:
            x = rnd.uniform(-radius + 1.5, radius - 1.5)
            z = rnd.uniform(-radius + 1.5, radius - 1.5)
            if max(abs(x), abs(z)) < radius - 6:
                continue
        else:
            r = rnd.uniform(radius * 0.62, radius + 14)
            ang = rnd.uniform(0, 360)
            if min(ang_dist(ang, la) for la in lane_angles) < 14:
                continue
            x, z = polar(r, ang)
        if not free(x, z, 1.0):
            continue
        decor.append({"model": rnd.choice(field["decor"]), "x": round(x, 2), "z": round(z, 2),
                      "rot": round(rnd.uniform(0, 360), 1), "scale": round(rnd.uniform(0.8, 1.35), 2)})
        taken.append((x, z, 1.0))

    out = {k: field[k] for k in ("id", "name", "setting", "biome", "order", "description", "unlockAfter",
                                 "shape", "radius", "decor", "bgmDay", "bgmNight", "enemyOrder")}
    out["decorModels"] = out.pop("decor")
    out.update(field["palette"])
    out["spawns"] = spawns
    out["slots"] = slots
    out["decor"] = decor
    out["walkways"] = walkways
    out["startBarrels"] = 6
    return out


def main() -> int:
    data = {"items": [build(f) for f in FIELDS]}
    text = json.dumps(data, indent=1) + "\n"
    if "--check" in sys.argv:
        if not OUT.exists() or OUT.read_text() != text:
            print("maps.json is out of date: run python3 tools/make_maps.py", file=sys.stderr)
            return 1
        print("maps.json is up to date")
        return 0
    OUT.write_text(text)
    for m in data["items"]:
        kinds: dict[str, int] = {}
        for s in m["slots"]:
            kinds[s["building"]] = kinds.get(s["building"], 0) + 1
        print(f'{m["id"]:<18} slots={len(m["slots"]):<3} {kinds}')
    return 0


if __name__ == "__main__":
    sys.exit(main())
