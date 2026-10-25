"""rigkit - a tiny low-poly modelling kit shared by the game and by Blender.

Model recipes (rigfall_models.py) are written with these primitives. The same recipe can
then be:
  * triangulated here into the JSON mesh format the Unity game loads
    (python3 blender/build_models.py), or
  * built as real Blender objects with vertex colours, ready to edit, render or export
    to FBX/GLB (blender --background --python blender/rigfall_blender.py).

Coordinates use Unity's convention: metres, +Y up, +Z forward (the model faces +Z),
+X right. Rotations are Euler degrees applied like Unity (Z, then X, then Y).
Every primitive is flat-shaded: each face gets its own vertices and one normal.
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field

Vec = tuple[float, float, float]


# ---------------------------------------------------------------------------- colours

def hex_color(h: str) -> Vec:
    h = h.lstrip("#")
    return (int(h[0:2], 16) / 255.0, int(h[2:4], 16) / 255.0, int(h[4:6], 16) / 255.0)


def shade(c: Vec | str, k: float) -> Vec:
    if isinstance(c, str):
        c = hex_color(c)
    return (min(1.0, c[0] * k), min(1.0, c[1] * k), min(1.0, c[2] * k))


def mix(a: Vec | str, b: Vec | str, t: float) -> Vec:
    a = hex_color(a) if isinstance(a, str) else a
    b = hex_color(b) if isinstance(b, str) else b
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


# ---------------------------------------------------------------------------- vector maths

def add(a: Vec, b: Vec) -> Vec:
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def sub(a: Vec, b: Vec) -> Vec:
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def cross(a: Vec, b: Vec) -> Vec:
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def dot(a: Vec, b: Vec) -> float:
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def norm(a: Vec) -> Vec:
    l = math.sqrt(dot(a, a))
    return (0.0, 1.0, 0.0) if l < 1e-12 else (a[0] / l, a[1] / l, a[2] / l)


def rotate(p: Vec, rot: Vec) -> Vec:
    """Unity Euler order: Z, then X, then Y."""
    x, y, z = p
    rx, ry, rz = (math.radians(a) for a in rot)
    if rz:
        c, s = math.cos(rz), math.sin(rz)
        x, y = x * c - y * s, x * s + y * c
    if rx:
        c, s = math.cos(rx), math.sin(rx)
        y, z = y * c - z * s, y * s + z * c
    if ry:
        c, s = math.cos(ry), math.sin(ry)
        x, z = x * c + z * s, -x * s + z * c
    return (x, y, z)


# ---------------------------------------------------------------------------- model data

@dataclass
class Face:
    pts: list[Vec]
    color: Vec


@dataclass
class Part:
    name: str
    pivot: Vec = (0.0, 0.0, 0.0)
    faces: list[Face] = field(default_factory=list)


@dataclass
class Model:
    name: str
    parts: list[Part] = field(default_factory=list)
    anchors: dict[str, Vec] = field(default_factory=dict)
    _current: Part | None = None

    def __post_init__(self) -> None:
        self.part("body")

    # -- structure --------------------------------------------------------------------
    def part(self, name: str, pivot: Vec = (0.0, 0.0, 0.0)) -> "Model":
        """Start (or switch to) a named part. Geometry added afterwards is relative to the
        model origin; the exporter makes it relative to the part's pivot."""
        for p in self.parts:
            if p.name == name:
                self._current = p
                return self
        p = Part(name, pivot)
        self.parts.append(p)
        self._current = p
        return self

    def anchor(self, name: str, pos: Vec) -> "Model":
        self.anchors[name] = pos
        return self

    def _add(self, faces: list[list[Vec]], color: Vec | str, center: Vec, pos: Vec, rot: Vec, scale: Vec) -> "Model":
        color = hex_color(color) if isinstance(color, str) else color
        flip = (scale[0] * scale[1] * scale[2]) < 0
        for pts in faces:
            # Orient each face outward (away from the primitive's centre) before transforming.
            n = cross(sub(pts[1], pts[0]), sub(pts[2], pts[0]))
            mid = tuple(sum(p[i] for p in pts) / len(pts) for i in range(3))
            if dot(n, sub(mid, center)) < 0:
                pts = list(reversed(pts))
            if flip:
                pts = list(reversed(pts))
            world = [add(rotate((p[0] * scale[0], p[1] * scale[1], p[2] * scale[2]), rot), pos) for p in pts]
            self._current.faces.append(Face(world, color))
        return self

    # -- primitives ---------------------------------------------------------------------
    def box(self, size: Vec, pos: Vec = (0, 0, 0), color: Vec | str = "#888888", rot: Vec = (0, 0, 0), bevel_top: float = 0.0) -> "Model":
        """Axis-aligned box centred on pos (before rotation). bevel_top shrinks the top face."""
        sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
        t = 1.0 - bevel_top
        b = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, -sy, sz), (-sx, -sy, sz)]
        u = [(-sx * t, sy, -sz * t), (sx * t, sy, -sz * t), (sx * t, sy, sz * t), (-sx * t, sy, sz * t)]
        faces = [b[::-1], u, [b[0], b[1], u[1], u[0]], [b[1], b[2], u[2], u[1]], [b[2], b[3], u[3], u[2]], [b[3], b[0], u[0], u[3]]]
        return self._add(faces, color, (0, 0, 0), pos, rot, (1, 1, 1))

    def cyl(self, radius: float, height: float, pos: Vec = (0, 0, 0), color: Vec | str = "#888888", rot: Vec = (0, 0, 0),
            sides: int = 8, top_radius: float | None = None, caps: bool = True) -> "Model":
        """Cylinder (or tapered cylinder/cone) standing on pos, along +Y before rotation."""
        r2 = radius if top_radius is None else top_radius
        bottom = [(radius * math.cos(2 * math.pi * i / sides), 0.0, radius * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
        top = [(r2 * math.cos(2 * math.pi * i / sides), height, r2 * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
        faces = []
        for i in range(sides):
            j = (i + 1) % sides
            if r2 > 1e-6:
                faces.append([bottom[i], bottom[j], top[j], top[i]])
            else:
                faces.append([bottom[i], bottom[j], top[i]])
        if caps:
            faces.append(bottom[::-1])
            if r2 > 1e-6:
                faces.append(top)
        return self._add(faces, color, (0, height / 2, 0), pos, rot, (1, 1, 1))

    def cone(self, radius: float, height: float, pos: Vec = (0, 0, 0), color: Vec | str = "#888888", rot: Vec = (0, 0, 0), sides: int = 8) -> "Model":
        return self.cyl(radius, height, pos, color, rot, sides, top_radius=0.0)

    def sphere(self, radius: float, pos: Vec = (0, 0, 0), color: Vec | str = "#888888", rings: int = 4, segs: int = 8,
               scale: Vec = (1, 1, 1), rot: Vec = (0, 0, 0)) -> "Model":
        """Low-poly UV sphere (rings x segs), optionally squashed by scale."""
        pts = []
        for r in range(rings + 1):
            phi = math.pi * r / rings
            row = []
            for s in range(segs):
                th = 2 * math.pi * s / segs
                row.append((radius * math.sin(phi) * math.cos(th), radius * math.cos(phi), radius * math.sin(phi) * math.sin(th)))
            pts.append(row)
        faces = []
        for r in range(rings):
            for s in range(segs):
                t = (s + 1) % segs
                a, b, c, d = pts[r][s], pts[r][t], pts[r + 1][t], pts[r + 1][s]
                if r == 0:
                    faces.append([a, c, d])
                elif r == rings - 1:
                    faces.append([a, b, c])
                else:
                    faces.append([a, b, c, d])
        return self._add(faces, color, (0, 0, 0), pos, rot, scale)

    def wedge(self, size: Vec, pos: Vec = (0, 0, 0), color: Vec | str = "#888888", rot: Vec = (0, 0, 0)) -> "Model":
        """Triangular prism: a ramp rising toward +Z (roofs, blades, teeth)."""
        sx, sy, sz = size[0] / 2, size[1], size[2] / 2
        a, b, c, d = (-sx, 0, -sz), (sx, 0, -sz), (sx, 0, sz), (-sx, 0, sz)
        e, f = (-sx, sy, sz), (sx, sy, sz)
        faces = [[a, b, c, d], [a, d, e], [b, c, f], [d, c, f, e], [a, b, f, e]]
        return self._add(faces, color, (0, sy / 3, sz / 3), pos, rot, (1, 1, 1))

    def roof(self, size: Vec, pos: Vec = (0, 0, 0), color: Vec | str = "#888888", rot: Vec = (0, 0, 0)) -> "Model":
        """Gable roof: ridge along X, sitting on pos."""
        sx, sy, sz = size[0] / 2, size[1], size[2] / 2
        a, b, c, d = (-sx, 0, -sz), (sx, 0, -sz), (sx, 0, sz), (-sx, 0, sz)
        e, f = (-sx, sy, 0), (sx, sy, 0)
        faces = [[a, b, c, d], [a, b, f, e], [d, c, f, e], [a, d, e], [b, c, f]]
        return self._add(faces, color, (0, sy / 3, 0), pos, rot, (1, 1, 1))

    def beam(self, a: Vec, b: Vec, thickness: float, color: Vec | str = "#888888") -> "Model":
        """Square-section strut from point a to point b (derrick lattices, pipes, legs)."""
        d = sub(b, a)
        length = math.sqrt(dot(d, d))
        if length < 1e-6:
            return self
        mid = (a[0] + d[0] / 2, a[1] + d[1] / 2, a[2] + d[2] / 2)
        dn = norm(d)
        ry = math.degrees(math.atan2(dn[0], dn[2]))
        rx = -math.degrees(math.asin(max(-1.0, min(1.0, dn[1]))))
        return self.box((thickness, thickness, length), mid, color, (rx, ry, 0))

    def pipe(self, a: Vec, b: Vec, radius: float, color: Vec | str = "#888888", sides: int = 6) -> "Model":
        """Round pipe from a to b."""
        d = sub(b, a)
        length = math.sqrt(dot(d, d))
        if length < 1e-6:
            return self
        dn = norm(d)
        # Rotate +Y onto the direction: first tilt around X, then yaw around Y.
        rx = math.degrees(math.acos(max(-1.0, min(1.0, dn[1]))))
        ry = math.degrees(math.atan2(dn[0], dn[2]))
        return self.cyl(radius, length, a, color, (rx, ry, 0), sides)

    def merge(self, other: "Model", offset: Vec = (0, 0, 0), rot: Vec = (0, 0, 0), scale: float = 1.0, into: str | None = None) -> "Model":
        """Copy another model's geometry into the current part (or a named one)."""
        if into:
            self.part(into)
        for p in other.parts:
            for f in p.faces:
                pts = [add(rotate((q[0] * scale, q[1] * scale, q[2] * scale), rot), offset) for q in f.pts]
                self._current.faces.append(Face(pts, f.color))
        return self

    # -- export ---------------------------------------------------------------------------
    def triangles(self, part: Part):
        """Yield (a, b, c, normal, color) for every triangle of a part, relative to its pivot."""
        for f in part.faces:
            pts = [sub(p, part.pivot) for p in f.pts]
            n = norm(cross(sub(pts[1], pts[0]), sub(pts[2], pts[0])))
            for i in range(1, len(pts) - 1):
                yield pts[0], pts[i], pts[i + 1], n, f.color

    def bounds(self) -> tuple[Vec, Vec]:
        lo = [1e9, 1e9, 1e9]
        hi = [-1e9, -1e9, -1e9]
        for p in self.parts:
            for f in p.faces:
                for q in f.pts:
                    for i in range(3):
                        lo[i] = min(lo[i], q[i])
                        hi[i] = max(hi[i], q[i])
        if lo[0] > hi[0]:
            return (0, 0, 0), (0, 0, 0)
        return tuple(lo), tuple(hi)

    def to_json(self) -> dict:
        """The format Unity's Models.cs reads (see ModelData / ModelPart)."""
        lo, hi = self.bounds()
        radius = max(abs(lo[0]), abs(hi[0]), abs(lo[2]), abs(hi[2]))
        parts = []
        for p in self.parts:
            if not p.faces:
                continue
            v, n, c, t = [], [], [], []
            for a, b, cc, nn, col in self.triangles(p):
                for q in (a, b, cc):
                    t.append(len(v) // 3)
                    v.extend(round(x, 3) for x in q)
                    n.extend(round(x, 3) for x in nn)
                    c.extend(round(x, 3) for x in col)
            parts.append({"name": p.name, "px": round(p.pivot[0], 3), "py": round(p.pivot[1], 3), "pz": round(p.pivot[2], 3),
                          "v": v, "n": n, "c": c, "t": t})
        return {
            "name": self.name,
            "radius": round(radius, 3),
            "height": round(hi[1], 3),
            "anchors": [{"name": k, "x": round(v[0], 3), "y": round(v[1], 3), "z": round(v[2], 3)} for k, v in sorted(self.anchors.items())],
            "parts": parts,
        }

    def triangle_count(self) -> int:
        return sum(len(f.pts) - 2 for p in self.parts for f in p.faces)
