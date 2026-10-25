// Loads the low-poly models produced by the Blender pipeline (blender/build_models.py)
// from Resources/Models/<name>.json and turns them into Unity meshes. Each model is a list
// of named parts with their own pivot, so code can animate pieces (the pumpjack's walking
// beam, a boss's arms) without an animation system.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rigfall.Runtime
{
    [Serializable]
    public class ModelAnchor
    {
        public string name = "";
        public float x, y, z;
    }

    [Serializable]
    public class ModelPart
    {
        public string name = "";
        public float px, py, pz;          // pivot, relative to the model origin
        public float[] v = new float[0];  // x,y,z per vertex (relative to the pivot)
        public float[] n = new float[0];  // normals
        public float[] c = new float[0];  // r,g,b per vertex (0..1)
        public int[] t = new int[0];      // triangles
    }

    [Serializable]
    public class ModelData
    {
        public string name = "";
        public float radius = 1f, height = 1f;
        public ModelAnchor[] anchors = new ModelAnchor[0];
        public ModelPart[] parts = new ModelPart[0];
    }

    public static class Models
    {
        sealed class Loaded
        {
            public ModelData data;
            public Mesh[] meshes;
        }

        static readonly Dictionary<string, Loaded> cache = new Dictionary<string, Loaded>();

        static Loaded Get(string name)
        {
            Loaded l;
            if (cache.TryGetValue(name, out l)) return l;
            l = new Loaded();
            var asset = Resources.Load<TextAsset>("Models/" + name);
            if (asset != null)
            {
                try { l.data = JsonUtility.FromJson<ModelData>(asset.text); }
                catch (Exception e) { Debug.LogWarning("Rigfall: bad model " + name + ": " + e.Message); }
            }
            if (l.data == null || l.data.parts == null || l.data.parts.Length == 0)
            {
                Debug.LogWarning("Rigfall: model '" + name + "' missing, using a placeholder.");
                l.data = Placeholder(name);
            }
            l.meshes = new Mesh[l.data.parts.Length];
            for (int i = 0; i < l.data.parts.Length; i++) l.meshes[i] = BuildMesh(name + "/" + l.data.parts[i].name, l.data.parts[i]);
            cache[name] = l;
            return l;
        }

        static ModelData Placeholder(string name)
        {
            // A simple coloured box so the game still works if an asset is missing.
            var part = new ModelPart { name = "body" };
            var verts = new List<float>();
            var norms = new List<float>();
            var cols = new List<float>();
            var tris = new List<int>();
            Vector3[] dirs = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            foreach (var nrm in dirs)
            {
                Vector3 a = nrm == Vector3.up || nrm == Vector3.down ? Vector3.right : Vector3.up;
                Vector3 b = Vector3.Cross(nrm, a);
                int start = verts.Count / 3;
                for (int k = 0; k < 4; k++)
                {
                    float sa = (k == 0 || k == 3) ? -0.5f : 0.5f;
                    float sb = (k < 2) ? -0.5f : 0.5f;
                    Vector3 p = nrm * 0.5f + a * sa + b * sb + Vector3.up * 0.5f;
                    verts.Add(p.x); verts.Add(p.y); verts.Add(p.z);
                    norms.Add(nrm.x); norms.Add(nrm.y); norms.Add(nrm.z);
                    cols.Add(0.8f); cols.Add(0.3f); cols.Add(0.8f);
                }
                tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
                tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
            }
            part.v = verts.ToArray(); part.n = norms.ToArray(); part.c = cols.ToArray(); part.t = tris.ToArray();
            return new ModelData { name = name, radius = 0.7f, height = 1f, parts = new[] { part } };
        }

        static Mesh BuildMesh(string meshName, ModelPart p)
        {
            int count = p.v.Length / 3;
            var verts = new List<Vector3>(count);
            var normals = new List<Vector3>(count);
            var colors = new List<Color>(count);
            for (int i = 0; i < count; i++)
            {
                verts.Add(new Vector3(p.v[i * 3], p.v[i * 3 + 1], p.v[i * 3 + 2]));
                if (p.n.Length >= (i + 1) * 3) normals.Add(new Vector3(p.n[i * 3], p.n[i * 3 + 1], p.n[i * 3 + 2]));
                if (p.c.Length >= (i + 1) * 3) colors.Add(new Color(p.c[i * 3], p.c[i * 3 + 1], p.c[i * 3 + 2], 1f));
                else colors.Add(Color.white);
            }
            var mesh = new Mesh { name = meshName };
            if (count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            var tris = new List<int>(p.t.Length);
            foreach (int i in p.t) if (i >= 0 && i < count) tris.Add(i);
            while (tris.Count % 3 != 0) tris.RemoveAt(tris.Count - 1);
            mesh.SetTriangles(tris, 0);
            if (normals.Count == count) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        public static ModelData Data(string name) => Get(name).data;

        public static bool Exists(string name) => Resources.Load<TextAsset>("Models/" + name) != null;

        /// <summary>
        /// Instantiate a model under 'parent'. Each part becomes a child GameObject named after
        /// the part, positioned at its pivot. Pass a material to override the shared tinted one.
        /// </summary>
        public static GameObject Spawn(string name, Transform parent, Material material = null, bool castShadows = true)
        {
            var l = Get(name);
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            var mat = material != null ? material : Materials.Lit(Color.white);
            for (int i = 0; i < l.data.parts.Length; i++)
            {
                var p = l.data.parts[i];
                var go = new GameObject(string.IsNullOrEmpty(p.name) ? "part" + i : p.name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(p.px, p.py, p.pz);
                go.AddComponent<MeshFilter>().sharedMesh = l.meshes[i];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = castShadows && Game.Settings.graphicsQuality > 0 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
            return root;
        }

        public static Vector3 Anchor(string model, string anchor, Vector3 fallback)
        {
            var d = Get(model).data;
            if (d.anchors != null)
                foreach (var a in d.anchors) if (a.name == anchor) return new Vector3(a.x, a.y, a.z);
            return fallback;
        }

        /// <summary>Swap the material on every renderer under a model (hit flash, ghost previews).</summary>
        public static void SetMaterial(GameObject model, Material m)
        {
            if (model == null) return;
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = m;
        }
    }
}
