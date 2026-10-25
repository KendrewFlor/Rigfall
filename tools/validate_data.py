#!/usr/bin/env python3
"""Validate every game-data JSON file in Assets/Rigfall/Resources/Data.

Checks the design rules the game depends on (40 perks, 15 of them from
achievements, 12 available to a new player, 19 mutators, 5 classes, 5 rarities)
and every cross-reference between files (weapons -> classes, achievements ->
perks/weapons/mutators, maps -> enemies, buildings -> units, and so on). It also
checks that every model and audio file the data mentions exists.

Exit code 0 when everything is consistent, 1 otherwise. Used by CI and tests.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "Assets/Rigfall/Resources/Data"
MODELS = ROOT / "Assets/Rigfall/Resources/Models"
AUDIO = ROOT / "Assets/Rigfall/Resources/Audio"

RARITIES = ["common", "rare", "epic", "legendary", "brilliant"]
STATS = {
    # economy
    "income.rig", "income.house", "income.mill", "income.rigTier", "income.all", "interest.rate", "interest.cap",
    "bounty.chance", "wheel.luck", "core.perNight", "cost.build", "cost.tower", "cost.wall", "cost.unitBuilding",
    "cost.upgrade", "cost.rig", "cost.firstDaily",
    # buildings
    "hp.wall", "hp.house", "hp.tower", "hp.rig", "hp.building", "regen.building", "damageTaken.building",
    "damageTaken.air", "tower.range", "tower.damage", "tower.rate", "tower.burn", "tower.ignoreRigTier",
    "wall.thorns", "wall.explode", "house.respawnIncome", "house.turret",
    # units
    "unit.damage", "unit.hp", "unit.speed", "unit.range", "unit.count", "unit.respawn", "unit.armor", "unit.noDawnHeal",
    # hero
    "hero.damage", "hero.rate", "hero.speed", "hero.hp", "hero.range", "hero.knockback", "hero.respawn", "hero.crit",
    "hero.burn", "hero.slow", "hero.abilityCooldown", "hero.armor", "hero.noRespawn", "command.radius",
    # enemies and the night
    "enemy.hp", "enemy.damage", "enemy.speed", "enemy.count", "enemy.eliteChance", "enemy.flyingShare", "enemy.revive",
    "enemy.range", "enemy.buildingDamage", "enemy.armor", "vision.radius", "boss.interval", "spawn.extraLanes",
}
OPS = {"add", "mul", "set"}
PERK_SPECIALS = {"", "bop", "kickDetection", "reliefWell"}
MUTATOR_SPECIALS = {"", "chaos", "earlyFlyers", "fog"}
ENEMY_SPECIALS = {"", "kamikaze", "poison", "split", "slowOnHit", "summon", "shockwave", "breath"}
UNIT_SPECIALS = {"", "taunt", "heal", "antiAir", "charge", "monsterHunter", "burn", "control", "berserk"}
WEAPON_ATTACKS = {"melee", "slam", "ranged", "spray", "lob"}
WEAPON_EFFECTS = {"", "armorPierce", "burn", "crit", "slow", "stun"}
BUILDING_KINDS = {"rig", "house", "mill", "tower", "wall", "barracks"}
ACH_TYPES = {"nightsRun", "tutorial", "bossKilled", "barrelsHeld", "buildingCount", "noBuildingLost", "lifetime",
             "unitsAlive", "unitBuildingsAll", "stageAllMaps", "wheelRarity", "loadoutPerks", "classesNight"}
LIFETIME_KEYS = {"kills", "heroKills", "towerKills", "rangedUnitKills", "flyingKills", "bossKills", "nights"}
QUEST_TYPES = {"heroKills", "towerKills", "unitKills", "noWallLost", "rigUntouched", "buildActions", "killType",
               "bossHero", "noUnitLost", "holdBarrels"}
WHEEL_KINDS = {"barrels", "core", "rigHeal", "blessing", "freeUpgrade", "unlockPerk", "unlockMutator", "unlockWeapon"}
TUTORIAL_EVENTS = re.compile(r"^(move|attack|night|dawn|wheelDone|command|build:[a-z-]+)$")
COLOR = re.compile(r"^#[0-9A-Fa-f]{6}$")
ID = re.compile(r"^[a-z0-9]+([-_][a-z0-9]+)*$")

errors: list[str] = []


def err(msg: str) -> None:
    errors.append(msg)


def load(name: str) -> list[dict]:
    path = DATA / f"{name}.json"
    try:
        doc = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as e:
        err(f"{name}.json: cannot read ({e})")
        return []
    items = doc.get("items")
    if not isinstance(items, list):
        err(f"{name}.json: top level must be {{\"items\": [...]}} (Unity's JsonUtility can't read a bare array)")
        return []
    return items


def unique_ids(name: str, items: list[dict], key: str = "id") -> dict[str, dict]:
    out: dict[str, dict] = {}
    for it in items:
        i = it.get(key)
        if not isinstance(i, str) or (key == "id" and not ID.match(i)):
            err(f"{name}: bad {key} {i!r}")
            continue
        if i in out:
            err(f"{name}: duplicate {key} {i}")
        out[i] = it
    return out


def check_mods(where: str, mods: list) -> None:
    if not isinstance(mods, list):
        err(f"{where}: mods must be a list")
        return
    for m in mods:
        if m.get("stat") not in STATS:
            err(f"{where}: unknown stat {m.get('stat')!r}")
        if m.get("op") not in OPS:
            err(f"{where}: unknown op {m.get('op')!r}")
        if not isinstance(m.get("value"), (int, float)):
            err(f"{where}: value must be a number")


def model_exists(name: str) -> bool:
    return (MODELS / f"{name}.json").exists()


def main(check_assets: bool = True) -> int:
    rar = unique_ids("rarities", load("rarities"))
    if list(rar) != RARITIES:
        err(f"rarities must be exactly {RARITIES} in order")
    if abs(sum(r["wheelWeight"] for r in rar.values()) - 100) > 1e-6:
        err("rarity wheel weights must add up to 100")

    diffs = unique_ids("difficulties", load("difficulties"))
    if list(diffs) != ["easy", "normal", "hard", "extreme", "blowout"]:
        err("difficulties must be easy, normal, hard, extreme, blowout")
    if diffs and not diffs.get("blowout", {}).get("allMutators"):
        err("blowout difficulty must switch on all mutators")

    classes = unique_ids("classes", load("classes"))
    if len(classes) != 5:
        err(f"there must be 5 classes, found {len(classes)}")

    weapons = unique_ids("weapons", load("weapons"))
    for w in weapons.values():
        if w["classId"] not in classes:
            err(f"weapon {w['id']}: unknown class {w['classId']}")
        if w["rarity"] not in rar:
            err(f"weapon {w['id']}: unknown rarity")
        if w["attack"] not in WEAPON_ATTACKS or w["effect"] not in WEAPON_EFFECTS:
            err(f"weapon {w['id']}: bad attack/effect")
        if w["damage"] <= 0 or w["rate"] <= 0 or w["range"] <= 0:
            err(f"weapon {w['id']}: damage, rate and range must be positive")
        if not COLOR.match(w["tint"]):
            err(f"weapon {w['id']}: bad tint")
    for c in classes.values():
        if c["startingWeapon"] not in weapons or weapons[c["startingWeapon"]]["classId"] != c["id"]:
            err(f"class {c['id']}: starting weapon must be one of its own weapons")
        own = [w for w in weapons.values() if w["classId"] == c["id"]]
        if sorted(w["rarity"] for w in own) != sorted(RARITIES):
            err(f"class {c['id']}: needs exactly one weapon of each rarity")
        if not COLOR.match(c["color"]):
            err(f"class {c['id']}: bad color")

    perks = unique_ids("perks", load("perks"))
    if len(perks) != 40:
        err(f"there must be 40 perks, found {len(perks)}")
    by_source = {s: [p for p in perks.values() if p["source"] == s] for s in ("starter", "achievement", "wheel")}
    if len(by_source["starter"]) != 12:
        err(f"12 perks must be available to a new player, found {len(by_source['starter'])}")
    if len(by_source["achievement"]) != 15:
        err(f"15 perks must come from achievements, found {len(by_source['achievement'])}")
    if sum(len(v) for v in by_source.values()) != len(perks):
        err("every perk needs a source of starter, achievement or wheel")
    for p in perks.values():
        if p["rarity"] not in rar:
            err(f"perk {p['id']}: unknown rarity")
        if p["special"] not in PERK_SPECIALS:
            err(f"perk {p['id']}: unknown special {p['special']}")
        if not p["mods"] and not p["special"]:
            err(f"perk {p['id']}: does nothing")
        check_mods(f"perk {p['id']}", p["mods"])

    muts = unique_ids("mutators", load("mutators"))
    if len(muts) != 19:
        err(f"there must be 19 mutators, found {len(muts)}")
    for m in muts.values():
        if m["rarity"] not in rar or m["source"] not in ("starter", "achievement", "wheel"):
            err(f"mutator {m['id']}: bad rarity/source")
        if m["special"] not in MUTATOR_SPECIALS:
            err(f"mutator {m['id']}: unknown special {m['special']}")
        if not m["mods"] and not m["special"]:
            err(f"mutator {m['id']}: does nothing")
        if not (0 < m["scoreMult"] <= 3):
            err(f"mutator {m['id']}: score multiplier out of range")
        check_mods(f"mutator {m['id']}", m["mods"])

    enemies = unique_ids("enemies", load("enemies"))
    bosses = [e for e in enemies.values() if e["boss"]]
    if len(bosses) < 3:
        err("need at least three bosses (nights 7, 14, 21)")
    for e in enemies.values():
        if e["special"] not in ENEMY_SPECIALS:
            err(f"enemy {e['id']}: unknown special {e['special']}")
        if e["spawns"] and e["spawns"] not in enemies:
            err(f"enemy {e['id']}: spawns unknown enemy {e['spawns']}")
        if e["special"] in ("split", "summon") and not e["spawns"]:
            err(f"enemy {e['id']}: {e['special']} needs a 'spawns' enemy")
        if e["hp"] <= 0 or e["speed"] <= 0:
            err(f"enemy {e['id']}: hp and speed must be positive")
        if e["ranged"] and e["projectileSpeed"] <= 0:
            err(f"enemy {e['id']}: ranged enemies need a projectile speed")

    units = unique_ids("units", load("units"))
    for u in units.values():
        if u["special"] not in UNIT_SPECIALS:
            err(f"unit {u['id']}: unknown special {u['special']}")
        if u["ranged"] and u["projectileSpeed"] <= 0:
            err(f"unit {u['id']}: ranged units need a projectile speed")

    buildings = unique_ids("buildings", load("buildings"))
    for kind in ("rig", "house", "mill", "tower", "wall", "barracks"):
        if not any(b["kind"] == kind for b in buildings.values()):
            err(f"no building of kind {kind}")
    unit_homes: dict[str, set[str]] = {}
    for b in buildings.values():
        if b["kind"] not in BUILDING_KINDS:
            err(f"building {b['id']}: bad kind")
        if not b["tiers"]:
            err(f"building {b['id']}: needs at least one tier")
        prev_ids: set[str] = set()
        for t_i, tier in enumerate(b["tiers"]):
            if not tier["options"]:
                err(f"building {b['id']} tier {t_i}: no options")
            ids = set()
            for o in tier["options"]:
                ids.add(o["id"])
                if o["requires"] and o["requires"] not in prev_ids:
                    err(f"building {b['id']} option {o['id']}: requires {o['requires']} which isn't in the previous tier")
                if o["unit"]:
                    if o["unit"] not in units:
                        err(f"building {b['id']}: unknown unit {o['unit']}")
                    unit_homes.setdefault(o["unit"], set()).add(b["id"])
                if b["kind"] == "barracks" and t_i == 0 and not o["unit"]:
                    err(f"building {b['id']}: first tier options must choose a unit")
                if o["cost"] < 0 or o["hp"] <= 0:
                    err(f"building {b['id']} option {o['id']}: bad cost/hp")
            prev_ids = ids
    for u in units.values():
        if u["building"] not in buildings:
            err(f"unit {u['id']}: unknown building {u['building']}")
        elif u["building"] not in unit_homes.get(u["id"], set()):
            err(f"unit {u['id']}: its building never offers it")
    for home in ("crew-camp", "perforation-range", "company-office"):
        if sum(1 for u in units.values() if u["building"] == home) != 4:
            err(f"{home} must offer exactly 4 units")

    maps = unique_ids("maps", load("maps"))
    if len(maps) < 5:
        err("need at least 5 fields")
    regular = {e["id"] for e in enemies.values() if not e["boss"] and e["threat"] > 0}
    for m in maps.values():
        for e in m["enemyOrder"]:
            if e not in enemies or enemies[e]["boss"]:
                err(f"map {m['id']}: enemyOrder has unknown/boss enemy {e}")
        if set(m["enemyOrder"]) != regular:
            err(f"map {m['id']}: enemyOrder must contain every regular enemy exactly once")
        if m["unlockAfter"] and m["unlockAfter"] not in maps:
            err(f"map {m['id']}: unlockAfter unknown map")
        if not any(s["fromNight"] == 1 for s in m["spawns"]):
            err(f"map {m['id']}: needs a lane open on night 1")
        for s in m["slots"]:
            if s["building"] not in buildings or buildings[s["building"]]["kind"] == "rig":
                err(f"map {m['id']} slot {s['id']}: bad building {s['building']}")
        for key in ("ground", "groundAlt", "accent", "fog", "skyDay", "skyNight", "sun"):
            if not COLOR.match(m[key]):
                err(f"map {m['id']}: bad color {key}")
        if m["shape"] not in ("disc", "deck"):
            err(f"map {m['id']}: shape must be disc or deck")
    rewarded = {w["unlock"].split(":")[1] for w in weapons.values() if w["unlock"].startswith("map:")}
    for m in maps.values():
        if m["id"] not in rewarded:
            err(f"map {m['id']}: should reward at least one weapon (different areas, different weapons)")

    achs = unique_ids("achievements", load("achievements"))
    ach_perks = []
    for a in achs.values():
        if a["type"] not in ACH_TYPES:
            err(f"achievement {a['id']}: unknown type {a['type']}")
        if a["type"] == "lifetime" and a["key"] not in LIFETIME_KEYS:
            err(f"achievement {a['id']}: unknown lifetime stat {a['key']}")
        if a["type"] == "bossKilled" and a["key"] and a["key"] not in enemies:
            err(f"achievement {a['id']}: unknown boss")
        if a["classId"] and a["classId"] not in classes:
            err(f"achievement {a['id']}: unknown class")
        if a["rewardPerk"]:
            ach_perks.append(a["rewardPerk"])
            if a["rewardPerk"] not in perks or perks[a["rewardPerk"]]["source"] != "achievement":
                err(f"achievement {a['id']}: reward perk must be an achievement perk")
        if a["rewardWeapon"] and a["rewardWeapon"] not in weapons:
            err(f"achievement {a['id']}: unknown weapon")
        if a["rewardMutator"] and a["rewardMutator"] not in muts:
            err(f"achievement {a['id']}: unknown mutator")
    if sorted(ach_perks) != sorted(p["id"] for p in by_source["achievement"]):
        err("every achievement perk must be granted by exactly one achievement")
    for w in weapons.values():
        if w["unlock"].startswith("achievement:"):
            a = w["unlock"].split(":", 1)[1]
            if a not in achs or achs[a]["rewardWeapon"] != w["id"]:
                err(f"weapon {w['id']}: its achievement doesn't grant it")
        elif w["unlock"].startswith("map:"):
            parts = w["unlock"].split(":")
            if len(parts) != 3 or parts[1] not in maps or parts[2] not in ("1", "2", "3"):
                err(f"weapon {w['id']}: bad map unlock {w['unlock']}")
        elif w["unlock"] not in ("start", "wheel"):
            err(f"weapon {w['id']}: bad unlock {w['unlock']}")
    for m in muts.values():
        if m["source"] == "achievement" and not any(a["rewardMutator"] == m["id"] for a in achs.values()):
            err(f"mutator {m['id']}: no achievement grants it")

    quests = unique_ids("quests", load("quests"))
    for q in quests.values():
        if q["type"] not in QUEST_TYPES:
            err(f"quest {q['id']}: unknown type")

    wheel = unique_ids("wheel", load("wheel"))
    for w in wheel.values():
        if w["rarity"] not in rar or w["kind"] not in WHEEL_KINDS:
            err(f"wheel {w['id']}: bad rarity/kind")
        check_mods(f"wheel {w['id']}", w["mods"])
    for r in RARITIES:
        if not any(w["rarity"] == r for w in wheel.values()):
            err(f"wheel has no {r} segment")

    for t in load("tutorial"):
        if not TUTORIAL_EVENTS.match(t.get("advanceOn", "")):
            err(f"tutorial {t.get('id')}: bad advanceOn")
    tracks = unique_ids("music", load("music"))

    if check_assets:
        needed = {c["model"] for c in classes.values()} | {w["model"] for w in weapons.values()} | \
                 {e["model"] for e in enemies.values()} | {u["model"] for u in units.values()} | \
                 {b["model"] for b in buildings.values()}
        for m in maps.values():
            needed |= set(m["decorModels"])
        for name in sorted(needed):
            if not model_exists(name):
                err(f"missing model Resources/Models/{name}.json (run blender/build_models.py)")
        for m in maps.values():
            for t in (m["bgmDay"], m["bgmNight"]):
                if t not in tracks:
                    err(f"map {m['id']}: unknown music track {t}")
        for t in tracks:
            if not (AUDIO / f"{t}.wav").exists():
                err(f"missing audio Resources/Audio/{t}.wav (run tools/synth_audio.py)")
        # Every non-ASCII character shown in the UI must exist in the bundled body font: a WebGL
        # build has no system fonts to fall back on, so a missing glyph renders as nothing.
        try:
            from fontTools.ttLib import TTFont  # optional: pip install fonttools
        except ImportError:
            TTFont = None
        if TTFont is not None:
            cmap = TTFont(str(ROOT / "Assets/Rigfall/Resources/Fonts/ChakraPetch-Regular.ttf")).getBestCmap()
            texts = [p for p in (ROOT / "Assets/Rigfall/Resources/Data").glob("*.json")]
            texts += list((ROOT / "Assets/Rigfall/Scripts/Runtime").rglob("*.cs"))
            for path in texts:
                body = path.read_text(encoding="utf-8")
                if path.suffix == ".cs":
                    body = re.sub(r"//[^\n]*|/\*.*?\*/", "", body, flags=re.S)  # comments are never shown
                for ch in sorted({c for c in body if ord(c) > 126}):
                    if ord(ch) not in cmap:
                        err(f"{path.relative_to(ROOT)}: '{ch}' (U+{ord(ch):04X}) is not in ChakraPetch-Regular.ttf")
        # Sounds and props the C# code names directly (AudioService.Sfx("sfx_coin"), Models.Spawn("prop-barrel")).
        scripts = ROOT / "Assets/Rigfall/Scripts"
        for cs in sorted(scripts.rglob("*.cs")):
            text = cs.read_text(encoding="utf-8")
            for sound in sorted(set(re.findall(r'"((?:sfx|amb|bgm)_[a-z0-9_]+)"', text))):
                if not (AUDIO / f"{sound}.wav").exists():
                    err(f"{cs.relative_to(ROOT)}: sound '{sound}' has no Resources/Audio/{sound}.wav")
            for prop in sorted(set(re.findall(r'"((?:prop|decor)-[a-z0-9-]+)"', text))):
                if not model_exists(prop):
                    err(f"{cs.relative_to(ROOT)}: model '{prop}' has no Resources/Models/{prop}.json")

    if errors:
        print(f"Game data has {len(errors)} problem(s):", file=sys.stderr)
        for e in errors:
            print(f"  x {e}", file=sys.stderr)
        return 1
    print(f"Game data OK: {len(classes)} classes, {len(weapons)} weapons, {len(perks)} perks "
          f"({len(by_source['starter'])} starter, {len(by_source['achievement'])} achievement, {len(by_source['wheel'])} wheel), "
          f"{len(muts)} mutators, {len(enemies)} enemies ({len(bosses)} bosses), {len(units)} units, "
          f"{len(buildings)} buildings, {len(maps)} fields, {len(achs)} achievements, {len(quests)} quests.")
    return 0


if __name__ == "__main__":
    sys.exit(main(check_assets="--no-assets" not in sys.argv))
