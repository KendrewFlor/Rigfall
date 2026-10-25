"""Tests for the Python side of Rigfall: data validation, generated maps and models, the
modelling kit's conventions, the audio files and the security scanner.

    python3 -m unittest discover -s tests/python -v

Standard library only (no numpy needed), so CI runs it without installing anything.
"""
from __future__ import annotations

import io
import json
import sys
import unittest
import wave
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
sys.path.insert(0, str(ROOT / "blender"))

import build_models  # noqa: E402
import make_maps  # noqa: E402
import rigkit  # noqa: E402
import security_scan  # noqa: E402
import validate_data  # noqa: E402

DATA = ROOT / "Assets" / "Rigfall" / "Resources" / "Data"
AUDIO = ROOT / "Assets" / "Rigfall" / "Resources" / "Audio"


def quiet(fn, *args, **kwargs):
    out, err = io.StringIO(), io.StringIO()
    with redirect_stdout(out), redirect_stderr(err):
        code = fn(*args, **kwargs)
    return code, out.getvalue() + err.getvalue()


def load(name: str) -> list[dict]:
    return json.loads((DATA / f"{name}.json").read_text(encoding="utf-8"))["items"]


class DataTests(unittest.TestCase):
    def test_game_data_is_valid(self):
        code, log = quiet(validate_data.main, True)
        self.assertEqual(code, 0, log)

    def test_maps_are_generated_from_the_script(self):
        old = sys.argv
        sys.argv = ["make_maps.py", "--check"]
        try:
            code, log = quiet(make_maps.main)
        finally:
            sys.argv = old
        self.assertEqual(code, 0, log)

    def test_design_counts(self):
        perks = load("perks")
        self.assertEqual(len(perks), 40)
        self.assertEqual(sum(p["source"] == "starter" for p in perks), 12, "new players start with 12 perk choices")
        self.assertEqual(sum(p["source"] == "achievement" for p in perks), 15, "15 perks come from achievements")
        self.assertEqual(len(load("mutators")), 19)
        self.assertEqual(len(load("classes")), 5)
        self.assertEqual([d["id"] for d in load("difficulties")], ["easy", "normal", "hard", "extreme", "blowout"])
        self.assertTrue(load("difficulties")[-1]["allMutators"])
        self.assertEqual([r["name"] for r in load("rarities")][-1], "Blowout Brilliant")

    def test_every_class_has_one_weapon_per_rarity(self):
        weapons = load("weapons")
        rarities = [r["id"] for r in load("rarities")]
        for c in load("classes"):
            mine = sorted(w["rarity"] for w in weapons if w["classId"] == c["id"])
            self.assertEqual(mine, sorted(rarities), c["id"])

    def test_unit_buildings_match_the_design(self):
        units = load("units")
        by_building: dict[str, list[str]] = {}
        for u in units:
            by_building.setdefault(u["building"], []).append(u["id"])
        self.assertEqual({k: len(v) for k, v in by_building.items()},
                         {"company-office": 4, "perforation-range": 4, "crew-camp": 4})

    def test_bosses_and_enemy_rotation(self):
        enemies = load("enemies")
        self.assertEqual(sum(e["boss"] for e in enemies), 4)
        for m in load("maps"):
            self.assertGreaterEqual(len(m["enemyOrder"]), 8, m["id"])


class ModelTests(unittest.TestCase):
    def test_committed_meshes_match_the_recipes(self):
        code, log = quiet(build_models.main, ["--check"])
        self.assertEqual(code, 0, log)

    def test_rotation_follows_unity_euler_conventions(self):
        def close(a, b):
            return all(abs(x - y) < 1e-9 for x, y in zip(a, b))
        self.assertTrue(close(rigkit.rotate((0, 1, 0), (90, 0, 0)), (0, 0, 1)))   # Euler(90,0,0) * up = forward
        self.assertTrue(close(rigkit.rotate((0, 0, 1), (0, 90, 0)), (1, 0, 0)))   # Euler(0,90,0) * forward = right
        self.assertTrue(close(rigkit.rotate((1, 0, 0), (0, 0, 90)), (0, 1, 0)))   # Euler(0,0,90) * right = up

    def test_faces_point_outward(self):
        m = rigkit.Model("t")
        m.box((1, 2, 3), (5, 1, -2), "#ffffff", rot=(10, 20, 30))
        m.cyl(0.5, 2, (0, 0, 0), "#ffffff", rot=(90, 0, 0))
        m.sphere(1, (2, 2, 2), "#ffffff")
        centres = [(5, 1, -2), (0, 0, 1), (2, 2, 2)]
        for f in m.parts[0].faces:
            n = rigkit.cross(rigkit.sub(f.pts[1], f.pts[0]), rigkit.sub(f.pts[2], f.pts[0]))
            mid = tuple(sum(p[i] for p in f.pts) / len(f.pts) for i in range(3))
            c = min(centres, key=lambda q: sum((mid[i] - q[i]) ** 2 for i in range(3)))
            self.assertGreater(rigkit.dot(n, rigkit.sub(mid, c)), 0)

    def test_beam_and_pipe_reach_their_end_points(self):
        for kind in ("beam", "pipe"):
            m = rigkit.Model("t")
            getattr(m, kind)((0, 0, 0), (1, 2, 3), 0.05, "#ffffff")
            lo, hi = m.bounds()
            self.assertAlmostEqual(hi[2], 3, delta=0.12)
            self.assertAlmostEqual(lo[1], 0, delta=0.12)

    def test_json_is_well_formed(self):
        data = json.loads((ROOT / "Assets/Rigfall/Resources/Models/building-pumpjack.json").read_text(encoding="utf-8"))
        names = [p["name"] for p in data["parts"]]
        self.assertIn("beam", names, "House.cs rocks the pumpjack's 'beam' part")
        for p in data["parts"]:
            self.assertEqual(len(p["v"]), len(p["n"]))
            self.assertEqual(len(p["v"]), len(p["c"]))
            self.assertEqual(len(p["t"]) % 3, 0)
            self.assertLess(max(p["t"]), len(p["v"]) // 3)

    def test_anchor_conventions(self):
        for name in ("hero-driller", "hero-toolpusher", "hero-perforator", "hero-mud-engineer", "hero-geophysicist"):
            data = json.loads((ROOT / f"Assets/Rigfall/Resources/Models/{name}.json").read_text(encoding="utf-8"))
            self.assertIn("hand", [a["name"] for a in data["anchors"]], name)
        tower = json.loads((ROOT / "Assets/Rigfall/Resources/Models/building-derrick-tower.json").read_text(encoding="utf-8"))
        self.assertIn("top", [p["name"] for p in tower["parts"]])
        self.assertIn("muzzle", [a["name"] for a in tower["anchors"]])
        import rigfall_models
        lo, hi = rigfall_models.build("building-blast-wall").bounds()
        self.assertAlmostEqual(hi[0] - lo[0], 3.2, delta=0.05, msg="Wall.cs assumes a 3.2 m segment")


class AudioTests(unittest.TestCase):
    def test_every_sound_the_game_uses_exists_and_is_valid(self):
        names = {t["id"] for t in load("music")} | {"amb_desert", "amb_swamp", "amb_arctic", "amb_sea"}
        for cs in (ROOT / "Assets/Rigfall/Scripts").rglob("*.cs"):
            import re
            names |= set(re.findall(r'"(sfx_[a-z0-9_]+)"', cs.read_text(encoding="utf-8")))
        self.assertGreater(len(names), 40)
        for n in sorted(names):
            path = AUDIO / f"{n}.wav"
            self.assertTrue(path.exists(), n)
            with wave.open(str(path), "rb") as w:
                self.assertEqual((w.getnchannels(), w.getsampwidth(), w.getframerate()), (1, 2, 22050), n)
                frames = w.readframes(w.getnframes())
            peak = max(abs(int.from_bytes(frames[i:i + 2], "little", signed=True)) for i in range(0, len(frames), 2 * 97))
            self.assertGreater(peak, 1000, f"{n} is silent")

    def test_music_tracks_are_long_enough_to_loop(self):
        for t in load("music"):
            with wave.open(str(AUDIO / f"{t['id']}.wav"), "rb") as w:
                self.assertGreaterEqual(w.getnframes() / w.getframerate(), 15, t["id"])


class SecurityTests(unittest.TestCase):
    def test_repository_passes_the_scan(self):
        security_scan.findings.clear()
        code, log = quiet(security_scan.main)
        self.assertEqual(code, 0, log)

    def test_scanner_catches_bad_content(self):
        security_scan.findings.clear()
        security_scan.scan_text("Assets/Rigfall/Scripts/Runtime/X.cs", 'var r = UnityWebRequest.Get("https://evil.example");')
        security_scan.scan_text("Assets/Rigfall/Scripts/Core/Y.cs", "using UnityEngine;\nclass Y {}")
        security_scan.scan_text("tools/z.py", "import socket\n")
        security_scan.scan_text("README.md", "hello <!-- hidden --> world")
        security_scan.scan_text("docs/a.md", "zero" + chr(0x200B) + "width")
        rules = {f[2] for f in security_scan.findings}
        self.assertTrue({"unsafe-source", "architecture", "hidden-markdown-comment", "hidden-unicode"} <= rules, rules)
        security_scan.findings.clear()


if __name__ == "__main__":
    unittest.main()
