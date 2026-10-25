// Builds a field (map) at runtime from its MapDef: ground or offshore deck, water,
// walkways, decoration, lighting, the fixed building slots and the spawn vectors. Then it
// bakes a NavMesh over the ground with NavMeshSurface so enemies, units and the hero can
// path-find. Nothing here needs a hand-made scene.
using System.Collections.Generic;
using Rigfall.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Rigfall.Runtime
{
    public static class MapMath
    {
        /// <summary>Data angles are maths angles (0 = +X, counter-clockwise). Unity yaw that points +Z that way.</summary>
        public static float Yaw(float mathDegrees) => 90f - mathDegrees;

        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>Closest point on the NavMesh, or the input if none is near.</summary>
        public static Vector3 OnNavMesh(Vector3 p, float maxDistance = 4f)
        {
            NavMeshHit hit;
            return NavMesh.SamplePosition(p, out hit, maxDistance, NavMesh.AllAreas) ? hit.position : p;
        }
    }
    public static class MapBuilder
    {
        static readonly HashSet<string> SolidDecor = new HashSet<string>
        {
            "decor-rock", "decor-old-tank", "decor-container", "decor-crane", "decor-storage-tank", "decor-pine",
            "decor-mangrove", "decor-ice", "decor-pipe-rack", "decor-lifeboat", "decor-pipeline",
        };

        /// <summary>Build a field. menuMode = scenery only (no slots, no NavMesh) for the main menu backdrop.</summary>
        public static Field Build(MapDef def, Transform parent, bool menuMode)
        {
            var root = new GameObject("Field " + def.id);
            root.transform.SetParent(parent, false);
            var field = root.AddComponent<Field>();
            field.Def = def;
            field.GroundRoot = Child(root.transform, "Ground");
            field.BuildingRoot = Child(root.transform, "Buildings");
            field.SlotRoot = Child(root.transform, "Slots");
            field.UnitRoot = Child(root.transform, "Units");
            field.EnemyRoot = Child(root.transform, "Enemies");
            field.PreviewRoot = Child(root.transform, "Preview");

            var rng = new Rng(def.id + ":scenery");
            if (def.shape == "deck") BuildDeck(def, field, rng); else BuildDisc(def, field, rng);
            BuildDecor(def, field);
            BuildLights(def, field, root.transform);

            field.Spawns = new Vector3[def.spawns.Length];
            for (int i = 0; i < def.spawns.Length; i++) field.Spawns[i] = new Vector3(def.spawns[i].x, 0f, def.spawns[i].z);

            if (!menuMode)
            {
                // Bake the NavMesh over the ground and decor only. Slots, buildings, units and
                // enemies live under other roots, so they don't end up in the bake; buildings
                // carve holes later with NavMeshObstacle.
                field.Surface = field.GroundRoot.gameObject.AddComponent<NavMeshSurface>();
                field.Surface.collectObjects = CollectObjects.Children;
                field.Surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                field.Surface.BuildNavMesh();

                foreach (var s in def.slots)
                {
                    var bdef = Game.Db.Building(s.building);
                    if (bdef == null) continue;
                    var go = new GameObject("Slot " + s.id + " (" + s.building + ")");
                    go.transform.SetParent(field.SlotRoot, false);
                    go.transform.position = new Vector3(s.x, 0f, s.z);
                    go.transform.rotation = Quaternion.Euler(0f, MapMath.Yaw(s.rot), 0f);
                    var slot = go.AddComponent<BuildingSlot>();
                    slot.Setup(s, bdef, field);
                    field.Slots.Add(slot);
                }
                var rigGo = new GameObject("Slot rig");
                rigGo.transform.SetParent(field.SlotRoot, false);
                field.RigSlot = rigGo.AddComponent<BuildingSlot>();
                field.RigSlot.Setup(new SlotDef { id = "rig", building = "main-rig" }, Game.Db.Building("main-rig"), field);
                field.HeroSpawn = MapMath.OnNavMesh(new Vector3(0f, 0f, -5f));
            }
            field.SetDay(true, true);
            return field;
        }

        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        // ---- onshore: a low-poly disc with darker patches --------------------------------

        static void BuildDisc(MapDef def, Field field, Rng rng)
        {
            float r = def.radius;
            var ground = Palette.Hex(def.ground);
            var alt = Palette.Hex(def.groundAlt);
            var accent = Palette.Hex(def.accent);
            int rings = 14, segments = 48;
            float outer = r + 22f;
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            // Flat-shaded triangles: each gets its own vertices and one colour.
            System.Func<float, float, Vector3> P = (rad, ang) =>
            {
                float a = ang * Mathf.Deg2Rad;
                float y = 0f;
                if (rad > r + 2f) y = (rad - r - 2f) * 0.18f + Mathf.PerlinNoise(rad * 0.15f, ang * 0.05f) * 1.5f * Mathf.Clamp01((rad - r - 2f) / 6f);
                return new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad);
            };
            for (int i = 0; i < rings; i++)
            {
                float r0 = outer * i / rings, r1 = outer * (i + 1) / rings;
                for (int j = 0; j < segments; j++)
                {
                    float a0 = 360f * j / segments, a1 = 360f * (j + 1) / segments;
                    var p00 = P(r0, a0); var p01 = P(r0, a1); var p10 = P(r1, a0); var p11 = P(r1, a1);
                    float n = Mathf.PerlinNoise(p10.x * 0.08f + 10f, p10.z * 0.08f + 3f);
                    var c = Color.Lerp(ground, alt, Mathf.Clamp01(n * 1.4f - 0.2f));
                    if (r0 > r + 4f) c = Color.Lerp(c, accent, 0.25f);
                    c *= rng.Range(0.96f, 1.04f);
                    c.a = 1f;
                    if (i == 0) Tri(verts, cols, tris, Vector3.zero, p11, p10, c);
                    else { Tri(verts, cols, tris, p00, p01, p11, c); Tri(verts, cols, tris, p00, p11, p10, c * 0.98f); }
                }
            }
            var go = MeshObject("Ground", field.GroundRoot, verts, cols, tris);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.25f, 0f);
            box.size = new Vector3(r * 2f + 6f, 0.5f, r * 2f + 6f);

            if (!string.IsNullOrEmpty(def.water))
            {
                // Swamp pools: shallow water patches outside the play area.
                for (int k = 0; k < 7; k++)
                {
                    float a = rng.Range(0f, 360f) * Mathf.Deg2Rad;
                    float rad = rng.Range(r * 0.8f, r + 10f);
                    var pool = Models.Spawn("decor-pool", field.GroundRoot, Materials.Lit(Palette.Hex(def.water)), false);
                    pool.transform.position = new Vector3(Mathf.Cos(a) * rad, 0.03f, Mathf.Sin(a) * rad);
                    pool.transform.localScale = Vector3.one * rng.Range(1.5f, 3f);
                }
            }
            Bounds(field, r + 3f);
        }

        // ---- offshore: a steel deck over water with walkways to the spawn points -------------

        static void BuildDeck(MapDef def, Field field, Rng rng)
        {
            float r = def.radius;
            var plate = Palette.Hex(def.ground);
            var plateAlt = Palette.Hex(def.groundAlt);
            var accent = Palette.Hex(def.accent);
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            float cell = 2.5f;
            int n = Mathf.CeilToInt(r * 2f / cell);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float x0 = -r + i * cell, z0 = -r + j * cell;
                    float x1 = Mathf.Min(r, x0 + cell), z1 = Mathf.Min(r, z0 + cell);
                    var c = ((i + j) % 2 == 0 ? plate : plateAlt) * rng.Range(0.97f, 1.03f);
                    bool edge = i == 0 || j == 0 || i == n - 1 || j == n - 1;
                    if (edge) c = Color.Lerp(c, accent, 0.65f);
                    c.a = 1f;
                    Quad(verts, cols, tris, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1), c);
                }
            // Deck sides, so it reads as a slab above the water.
            float h = 1.4f;
            var side = plateAlt * 0.7f; side.a = 1f;
            Quad(verts, cols, tris, new Vector3(-r, -h, -r), new Vector3(r, -h, -r), new Vector3(r, 0, -r), new Vector3(-r, 0, -r), side);
            Quad(verts, cols, tris, new Vector3(r, -h, r), new Vector3(-r, -h, r), new Vector3(-r, 0, r), new Vector3(r, 0, r), side);
            Quad(verts, cols, tris, new Vector3(-r, -h, r), new Vector3(-r, -h, -r), new Vector3(-r, 0, -r), new Vector3(-r, 0, r), side);
            Quad(verts, cols, tris, new Vector3(r, -h, -r), new Vector3(r, -h, r), new Vector3(r, 0, r), new Vector3(r, 0, -r), side);
            var deck = MeshObject("Deck", field.GroundRoot, verts, cols, tris);
            var box = deck.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.25f, 0f);
            box.size = new Vector3(r * 2f, 0.5f, r * 2f);

            foreach (var w in def.walkways)
            {
                var walk = Models.Spawn("prop-walkway", field.GroundRoot, null, true);
                walk.transform.position = new Vector3(w.x, 0f, w.z);
                walk.transform.rotation = Quaternion.Euler(0f, MapMath.Yaw(w.rot), 0f);
                walk.transform.localScale = new Vector3(w.width / 4f, 1f, w.length / 8f);
                var wb = walk.AddComponent<BoxCollider>();
                wb.center = new Vector3(0f, -0.25f, 0f);
                wb.size = new Vector3(4f, 0.5f, 8f);
            }
            // Jacket legs at the corners, and the sea.
            foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
            {
                var leg = Models.Spawn("prop-jacket-leg", field.GroundRoot, null, true);
                leg.transform.position = new Vector3(corner.x * (r - 1.2f), 0f, corner.y * (r - 1.2f));
            }
            var sea = Models.Spawn("prop-sea", field.GroundRoot, Materials.Lit(Palette.Hex(def.water)), false);
            sea.transform.position = new Vector3(0f, -2.2f, 0f);
            sea.transform.localScale = new Vector3(14f, 1f, 14f);
            sea.AddComponent<Bobber>().Setup(0.12f, 0.6f);
            Bounds(field, r + 14f);
        }

        /// <summary>Invisible walls so nothing wanders off the edge of the world.</summary>
        static void Bounds(Field field, float half)
        {
            var root = Child(field.transform, "Bounds");
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Bound" + i);
                go.transform.SetParent(root, false);
                var b = go.AddComponent<BoxCollider>();
                float a = i * 90f * Mathf.Deg2Rad;
                go.transform.position = new Vector3(Mathf.Cos(a) * (half + 0.5f), 1f, Mathf.Sin(a) * (half + 0.5f));
                b.size = (i % 2 == 0) ? new Vector3(1f, 4f, half * 2f + 2f) : new Vector3(half * 2f + 2f, 4f, 1f);
            }
        }

        static void BuildDecor(MapDef def, Field field)
        {
            int budget = Game.Settings.graphicsQuality == 0 ? def.decor.Length / 2 : def.decor.Length;
            for (int i = 0; i < budget; i++)
            {
                var d = def.decor[i];
                var go = Models.Spawn(d.model, field.GroundRoot, null, Game.Settings.graphicsQuality > 1);
                go.transform.position = new Vector3(d.x, 0f, d.z);
                go.transform.rotation = Quaternion.Euler(0f, d.rot, 0f);
                go.transform.localScale = Vector3.one * d.scale;
                if (d.model == "decor-tumbleweed") go.AddComponent<Tumbleweed>();
                if (SolidDecor.Contains(d.model))
                {
                    var col = go.AddComponent<CapsuleCollider>();
                    var data = Models.Data(d.model);
                    col.radius = Mathf.Max(0.3f, data.radius * 0.7f);
                    col.height = Mathf.Max(1f, data.height);
                    col.center = new Vector3(0f, col.height * 0.5f, 0f);
                }
            }
        }

        static void BuildLights(MapDef def, Field field, Transform root)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = Game.Settings.graphicsQuality == 0 ? LightShadows.None : Game.Settings.graphicsQuality == 1 ? LightShadows.Hard : LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            field.Sun = sun;
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 38f;
            RenderSettings.fogEndDistance = 95f;

            if (Game.Settings.graphicsQuality >= 1)
            {
                var lampGo = new GameObject("Rig floodlight");
                lampGo.transform.SetParent(root, false);
                lampGo.transform.position = new Vector3(0f, 9f, 0f);
                var lamp = lampGo.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.range = 26f;
                lamp.color = new Color(1f, 0.85f, 0.6f);
                lamp.intensity = 0f;
                lamp.shadows = LightShadows.None;
                field.RigLamp = lamp;
            }
        }

        // ---- mesh helpers -----------------------------------------------------------------

        static void Tri(List<Vector3> v, List<Color> c, List<int> t, Vector3 a, Vector3 b, Vector3 d, Color col)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(d);
            c.Add(col); c.Add(col); c.Add(col);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        static void Quad(List<Vector3> v, List<Color> c, List<int> t, Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color col)
        {
            Tri(v, c, t, a, e, d, col);
            Tri(v, c, t, a, d, b, col);
        }

        static GameObject MeshObject(string name, Transform parent, List<Vector3> v, List<Color> c, List<int> t)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Materials.Lit(Color.white);
            r.receiveShadows = true;
            return go;
        }
    }
}
