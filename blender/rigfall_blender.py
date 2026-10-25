"""Rigfall models in Blender: build them, edit them, export them.

The model recipes live in rigfall_models.py (pure Python). This script turns them into real
Blender objects with vertex colours, one object per animated part, so you can look at them,
tweak them by hand, render them or export them for other tools. It can also read your
edited objects back into the JSON format the Unity game loads.

Run it with Blender 4.x (it does nothing outside Blender):

  # Build every model into a .blend file (one collection per model):
  blender --background --python blender/rigfall_blender.py -- --build --save out/rigfall_models.blend

  # Build a few models and export each one to FBX and glTF (GLB):
  blender --background --python blender/rigfall_blender.py -- --build hero-driller building-pumpjack --fbx out/fbx --glb out/glb

  # After editing in Blender: write the edited models back to the game's JSON meshes.
  blender out/rigfall_models.blend --background --python blender/rigfall_blender.py -- --to-json

Axes: the game uses Unity's convention (+Y up, +Z forward, left-handed). Blender is +Z up and
right-handed, so a game point (x, y, z) becomes Blender (x, z, y), and face winding is
reversed to keep normals pointing out. The FBX export uses Unity-friendly axis settings.

Nothing here touches the network. Only standard Blender modules are used.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

try:
    import bpy  # type: ignore
    import bmesh  # type: ignore
except ImportError:  # pragma: no cover - only meaningful inside Blender
    bpy = None
    bmesh = None

import rigfall_models  # noqa: E402

ROOT = HERE.parent
MODELS_DIR = ROOT / "Assets" / "Rigfall" / "Resources" / "Models"
COLOR_ATTR = "Col"


def to_blender(p):
    return (p[0], p[2], p[1])


def to_game(p):
    return (p[0], p[2], p[1])


# ---------------------------------------------------------------------------- build

def _set_corner_color(attr, index, rgb):
    value = attr.data[index]
    rgba = (rgb[0], rgb[1], rgb[2], 1.0)
    if hasattr(value, "color_srgb"):
        value.color_srgb = rgba  # the game's colours are authored in sRGB
    else:
        value.color = rgba


def _material():
    mat = bpy.data.materials.get("Rigfall Vertex Colour")
    if mat is not None:
        return mat
    mat = bpy.data.materials.new("Rigfall Vertex Colour")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    attr = nodes.new("ShaderNodeVertexColor")
    attr.layer_name = COLOR_ATTR
    attr.location = (-300, 200)
    if bsdf is not None:
        mat.node_tree.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.85
    return mat


def build_model(name: str, collection=None):
    """Create one model: an empty named after it, with one mesh object per part."""
    model = rigfall_models.build(name)
    coll = bpy.data.collections.new(name)
    (collection or bpy.context.scene.collection).children.link(coll)
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "PLAIN_AXES"
    coll.objects.link(root)
    for anchor, pos in model.anchors.items():
        a = bpy.data.objects.new(f"{name}:anchor:{anchor}", None)
        a.empty_display_type = "SPHERE"
        a.empty_display_size = 0.08
        a.location = to_blender(pos)
        a["rigfall_anchor"] = anchor
        a.parent = root
        coll.objects.link(a)
    mat = _material()
    for part in model.parts:
        if not part.faces:
            continue
        verts, faces, colours = [], [], []
        for f in part.faces:
            idx = []
            for p in reversed(f.pts):  # mirrored axes flip the winding
                rel = (p[0] - part.pivot[0], p[1] - part.pivot[1], p[2] - part.pivot[2])
                idx.append(len(verts))
                verts.append(to_blender(rel))
            faces.append(idx)
            colours.append(f.color)
        mesh = bpy.data.meshes.new(f"{name}:{part.name}")
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        attr = mesh.color_attributes.new(COLOR_ATTR, "BYTE_COLOR", "CORNER")
        for poly, rgb in zip(mesh.polygons, colours):
            for li in poly.loop_indices:
                _set_corner_color(attr, li, rgb)
        mesh.materials.append(mat)
        obj = bpy.data.objects.new(f"{name}:{part.name}", mesh)  # Blender names are global, so prefix them
        obj["rigfall_part"] = part.name
        obj.location = to_blender(part.pivot)
        obj.parent = root
        coll.objects.link(obj)
    root["rigfall_model"] = name
    return root


# ---------------------------------------------------------------------------- read back

def _corner_rgb(mesh, loop_index):
    attr = mesh.color_attributes.get(COLOR_ATTR) or (mesh.color_attributes[0] if len(mesh.color_attributes) else None)
    if attr is None:
        return (0.8, 0.8, 0.8)
    value = attr.data[loop_index] if attr.domain == "CORNER" else attr.data[mesh.loops[loop_index].vertex_index]
    c = value.color_srgb if hasattr(value, "color_srgb") else value.color
    return (c[0], c[1], c[2])


def model_to_json(root) -> dict:
    """Read a model built (and maybe edited) in Blender back into the game's JSON format."""
    name = root["rigfall_model"]
    parts, anchors = [], []
    lo = [1e9, 1e9, 1e9]
    hi = [-1e9, -1e9, -1e9]
    for child in root.children:
        if "rigfall_anchor" in child:
            p = to_game(child.location)
            anchors.append({"name": child["rigfall_anchor"], "x": round(p[0], 3), "y": round(p[1], 3), "z": round(p[2], 3)})
            continue
        if child.type != "MESH":
            continue
        depsgraph = bpy.context.evaluated_depsgraph_get()
        evaluated = child.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        pivot = to_game(child.location)
        # Apply the part's own rotation and scale (location is the pivot).
        basis = child.matrix_basis.to_3x3()
        v, n, c, t = [], [], [], []
        for poly in mesh.polygons:
            loops = list(poly.loop_indices)
            pts = [to_game(basis @ mesh.vertices[mesh.loops[li].vertex_index].co) for li in loops]
            pts.reverse()  # mirrored axes: flip the winding back
            loops.reverse()
            ax, ay, az = pts[0]
            bx, by, bz = pts[1]
            cx, cy, cz = pts[2]
            ux, uy, uz = bx - ax, by - ay, bz - az
            wx, wy, wz = cx - ax, cy - ay, cz - az
            nx, ny, nz = uy * wz - uz * wy, uz * wx - ux * wz, ux * wy - uy * wx
            length = (nx * nx + ny * ny + nz * nz) ** 0.5 or 1.0
            for p, li in zip(pts, loops):
                t.append(len(v) // 3)
                v.extend(round(x, 3) for x in p)
                n.extend(round(x / length, 3) for x in (nx, ny, nz))
                c.extend(round(x, 3) for x in _corner_rgb(mesh, li))
                for i in range(3):
                    world = p[i] + pivot[i]
                    lo[i] = min(lo[i], world)
                    hi[i] = max(hi[i], world)
        evaluated.to_mesh_clear()
        parts.append({"name": child.get("rigfall_part", child.name), "px": round(pivot[0], 3), "py": round(pivot[1], 3),
                      "pz": round(pivot[2], 3), "v": v, "n": n, "c": c, "t": t})
    radius = max(abs(lo[0]), abs(hi[0]), abs(lo[2]), abs(hi[2])) if parts else 1.0
    return {"name": name, "radius": round(radius, 3), "height": round(hi[1], 3) if parts else 1.0,
            "anchors": sorted(anchors, key=lambda a: a["name"]), "parts": parts}


# ---------------------------------------------------------------------------- export

def _select_only(root):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root


def export_fbx(root, folder: Path):
    folder.mkdir(parents=True, exist_ok=True)
    _select_only(root)
    bpy.ops.export_scene.fbx(filepath=str(folder / f"{root.name}.fbx"), use_selection=True, object_types={"EMPTY", "MESH"},
                             axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_UNITS",
                             bake_space_transform=True, colors_type="SRGB", mesh_smooth_type="FACE")


def export_glb(root, folder: Path):
    folder.mkdir(parents=True, exist_ok=True)
    _select_only(root)
    bpy.ops.export_scene.gltf(filepath=str(folder / f"{root.name}.glb"), export_format="GLB", use_selection=True,
                              export_yup=True)


# ---------------------------------------------------------------------------- CLI

def _args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser(prog="rigfall_blender.py")
    ap.add_argument("--build", nargs="*", default=None, metavar="MODEL",
                    help="build these models (no names = every model)")
    ap.add_argument("--clear", action="store_true", help="delete everything in the scene first")
    ap.add_argument("--save", type=Path, help="save the .blend file here")
    ap.add_argument("--fbx", type=Path, help="export each built model as FBX into this folder")
    ap.add_argument("--glb", type=Path, help="export each built model as GLB into this folder")
    ap.add_argument("--to-json", nargs="*", default=None, metavar="MODEL",
                    help="write models in the open .blend back to Resources/Models (no names = all)")
    return ap.parse_args(argv)


def main():
    if bpy is None:
        print("Run this inside Blender: blender --background --python blender/rigfall_blender.py -- --build")
        return 2
    args = _args()
    if args.clear or (args.build is not None and args.save):
        bpy.ops.wm.read_factory_settings(use_empty=True)

    roots = []
    if args.build is not None:
        names = args.build or sorted(rigfall_models.MODELS)
        unknown = [n for n in names if n not in rigfall_models.MODELS]
        if unknown:
            print("Unknown models:", ", ".join(unknown))
            return 1
        top = bpy.data.collections.new("Rigfall")
        bpy.context.scene.collection.children.link(top)
        for i, name in enumerate(names):
            root = build_model(name, top)
            root.location = ((i % 10) * 8.0, (i // 10) * 8.0, 0.0)  # lay them out on a grid
            roots.append(root)
        print(f"Built {len(roots)} models.")

    for root in roots:
        saved = root.location.copy()
        root.location = (0.0, 0.0, 0.0)
        if args.fbx:
            export_fbx(root, args.fbx)
        if args.glb:
            export_glb(root, args.glb)
        root.location = saved

    if args.to_json is not None:
        wanted = set(args.to_json)
        count = 0
        for obj in bpy.data.objects:
            if "rigfall_model" not in obj:
                continue
            if wanted and obj["rigfall_model"] not in wanted:
                continue
            data = model_to_json(obj)
            MODELS_DIR.mkdir(parents=True, exist_ok=True)
            (MODELS_DIR / f"{data['name']}.json").write_text(json.dumps(data, separators=(",", ":")) + "\n", encoding="utf-8")
            count += 1
        print(f"Wrote {count} model(s) to {MODELS_DIR}.")
        print("Note: build_models.py --check will now report these as edited; move your changes into the recipe")
        print("or drop the check for hand-edited models.")

    if args.save:
        args.save.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(args.save.resolve()))
        print("Saved", args.save)
    return 0


if __name__ == "__main__":
    code = main()
    if bpy is not None and bpy.app.background:
        sys.exit(code)
