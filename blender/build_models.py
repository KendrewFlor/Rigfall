"""Build every Rigfall model into Assets/Rigfall/Resources/Models/<name>.json.

    python3 blender/build_models.py           # write the JSON meshes
    python3 blender/build_models.py --check   # fail if any JSON is missing or out of date
    python3 blender/build_models.py --stats   # print triangle counts

Pure Python (no Blender needed), so CI can check that the committed meshes match the
recipes in rigfall_models.py. Open the same recipes in Blender with rigfall_blender.py.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import rigfall_models  # noqa: E402

ROOT = HERE.parent
OUT = ROOT / "Assets" / "Rigfall" / "Resources" / "Models"

# Triangle budgets keep WebGL draw cost predictable; a recipe that blows through one is a bug.
BUDGET = {"boss-": 3000, "building-": 3500, "decor-": 1200, "prop-": 600}
DEFAULT_BUDGET = 1600


def budget_for(name: str) -> int:
    for prefix, limit in BUDGET.items():
        if name.startswith(prefix):
            return limit
    return DEFAULT_BUDGET


def file_for(name: str) -> Path:
    # '@' is fine in file names on every platform Unity supports.
    return OUT / f"{name}.json"


def render(name: str) -> str:
    return json.dumps(rigfall_models.build(name).to_json(), separators=(",", ":")) + "\n"


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--check", action="store_true", help="verify instead of writing")
    ap.add_argument("--stats", action="store_true", help="print triangle counts")
    args = ap.parse_args(argv)

    problems: list[str] = []
    total = 0
    for name in sorted(rigfall_models.MODELS):
        model = rigfall_models.build(name)
        tris = model.triangle_count()
        total += tris
        if tris == 0:
            problems.append(f"{name}: no geometry")
        if tris > budget_for(name):
            problems.append(f"{name}: {tris} triangles is over the {budget_for(name)} budget")
        if args.stats:
            lo, hi = model.bounds()
            print(f"{name:52s} {tris:5d} tris  height {hi[1]:5.2f}  parts {len([p for p in model.parts if p.faces])}")
        text = json.dumps(model.to_json(), separators=(",", ":")) + "\n"
        path = file_for(name)
        if args.check:
            if not path.exists():
                problems.append(f"{path.relative_to(ROOT)} is missing (run python3 blender/build_models.py)")
            elif path.read_text(encoding="utf-8") != text:
                problems.append(f"{path.relative_to(ROOT)} is out of date (run python3 blender/build_models.py)")
        else:
            OUT.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding="utf-8")

    if args.check and OUT.exists():
        known = {file_for(n).name for n in rigfall_models.MODELS}
        for f in OUT.glob("*.json"):
            if f.name not in known:
                problems.append(f"{f.relative_to(ROOT)} has no recipe (delete it or add one)")

    for p in problems:
        print("ERROR:", p, file=sys.stderr)
    if not problems:
        verb = "checked" if args.check else "wrote"
        print(f"{verb} {len(rigfall_models.MODELS)} models, {total} triangles")
    return 1 if problems else 0


if __name__ == "__main__":
    raise SystemExit(main())
