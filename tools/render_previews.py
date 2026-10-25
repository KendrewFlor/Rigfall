"""Render preview images of the Rigfall models without Blender or Unity.

    python3 tools/render_previews.py                     # contact sheets into docs/images/
    python3 tools/render_previews.py --only hero- enemy-  # just some models
    python3 tools/render_previews.py --out /tmp/previews

A small numpy z-buffer rasteriser: flat shading with the same half-Lambert light the game's
Rigfall/Lit shader uses, a three-quarter camera like the in-game one, and 2x supersampling.
It exists so the models can be reviewed (and the README illustrated) on any machine.
Needs numpy and Pillow (pip install numpy pillow).
"""
from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "blender"))

import rigfall_models  # noqa: E402

LIGHT = np.array([-0.45, 0.8, 0.4])
LIGHT = LIGHT / np.linalg.norm(LIGHT)
BG_TOP = np.array([0.16, 0.19, 0.22])
BG_BOTTOM = np.array([0.09, 0.10, 0.12])


def triangles(model) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    pts, nrm, col = [], [], []
    for part in model.parts:
        for a, b, c, n, colour in model.triangles(part):
            pv = part.pivot
            pts.append([[a[i] + pv[i] for i in range(3)], [b[i] + pv[i] for i in range(3)], [c[i] + pv[i] for i in range(3)]])
            nrm.append(n)
            col.append(colour)
    return np.array(pts, dtype=float), np.array(nrm, dtype=float), np.array(col, dtype=float)


def render(model, size: int = 256, yaw: float = 35.0, pitch: float = 28.0) -> Image.Image:
    ss = 2
    w = h = size * ss
    tris, normals, colours = triangles(model)
    img = np.zeros((h, w, 3))
    t = np.linspace(0, 1, h)[:, None]
    img[:] = (BG_TOP * (1 - t) + BG_BOTTOM * t)[:, None, :]
    if len(tris) == 0:
        return Image.fromarray((img * 255).astype(np.uint8)).resize((size, size))

    # Camera: look at the model's centre from the front-left, slightly above.
    lo, hi = tris.reshape(-1, 3).min(0), tris.reshape(-1, 3).max(0)
    centre = (lo + hi) / 2
    ya, pa = math.radians(yaw), math.radians(pitch)
    fwd = -np.array([math.sin(ya) * math.cos(pa), math.sin(pa), math.cos(ya) * math.cos(pa)])
    right = np.cross(np.array([0.0, 1.0, 0.0]), fwd)
    right /= np.linalg.norm(right)
    up = np.cross(fwd, right)
    rel = tris - centre
    cx, cy, cz = rel @ right, rel @ up, rel @ fwd
    extent = max(np.abs(cx).max(), np.abs(cy).max()) * 1.15 + 1e-6
    sx = (cx / extent * 0.5 + 0.5) * w
    sy = (0.5 - cy / extent * 0.5) * h

    # Lighting: half-Lambert squared plus ambient, like Rigfall/Lit.
    ndl = normals @ LIGHT * 0.5 + 0.5
    shade = np.clip(ndl * ndl * 0.85 + 0.28, 0, 1.25)[:, None]
    lit = np.clip(colours * shade, 0, 1)
    # Back faces (normal pointing away from the camera) are culled, as in the game.
    facing = normals @ (-fwd) > -1e-4

    zbuf = np.full((h, w), np.inf)
    for i in np.argsort(-cz.mean(1)):
        if not facing[i]:
            continue
        x0, y0, x1, y1, x2, y2 = sx[i, 0], sy[i, 0], sx[i, 1], sy[i, 1], sx[i, 2], sy[i, 2]
        minx, maxx = int(max(0, math.floor(min(x0, x1, x2)))), int(min(w - 1, math.ceil(max(x0, x1, x2))))
        miny, maxy = int(max(0, math.floor(min(y0, y1, y2)))), int(min(h - 1, math.ceil(max(y0, y1, y2))))
        if minx > maxx or miny > maxy:
            continue
        area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
        if abs(area) < 1e-9:
            continue
        xs, ys = np.meshgrid(np.arange(minx, maxx + 1) + 0.5, np.arange(miny, maxy + 1) + 0.5)
        w0 = ((x1 - xs) * (y2 - ys) - (x2 - xs) * (y1 - ys)) / area
        w1 = ((x2 - xs) * (y0 - ys) - (x0 - xs) * (y2 - ys)) / area
        w2 = 1 - w0 - w1
        inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        if not inside.any():
            continue
        z = w0 * cz[i, 0] + w1 * cz[i, 1] + w2 * cz[i, 2]
        region = zbuf[miny:maxy + 1, minx:maxx + 1]
        closer = inside & (z < region)
        region[closer] = z[closer]
        img[miny:maxy + 1, minx:maxx + 1][closer] = lit[i]
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).resize((size, size), Image.LANCZOS)


def sheet(names: list[str], cols: int = 6, cell: int = 220) -> Image.Image:
    rows = math.ceil(len(names) / cols)
    out = Image.new("RGB", (cols * cell, rows * (cell + 22)), (14, 16, 18))
    draw = ImageDraw.Draw(out)
    font = ImageFont.load_default()
    for k, name in enumerate(names):
        img = render(rigfall_models.build(name), cell)
        x, y = (k % cols) * cell, (k // cols) * (cell + 22)
        out.paste(img, (x, y))
        draw.text((x + 6, y + cell + 4), name, fill=(220, 226, 230), font=font)
    return out


GROUPS = {
    "heroes": ("hero-",),
    "weapons": ("weapon-",),
    "enemies": ("enemy-", "boss-"),
    "units": ("unit-",),
    "buildings": ("building-",),
    "world": ("decor-", "prop-"),
}


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--only", nargs="*", default=None, help="model name prefixes to render")
    ap.add_argument("--out", default=str(ROOT / "docs" / "images"))
    args = ap.parse_args(argv)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    names = sorted(rigfall_models.MODELS)
    for group, prefixes in GROUPS.items():
        chosen = [n for n in names if n.startswith(prefixes)]
        if args.only is not None:
            chosen = [n for n in chosen if n.startswith(tuple(args.only))]
        if not chosen:
            continue
        path = out / f"models-{group}.png"
        sheet(chosen).save(path, optimize=True)
        print("wrote", path.relative_to(ROOT) if path.is_relative_to(ROOT) else path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
