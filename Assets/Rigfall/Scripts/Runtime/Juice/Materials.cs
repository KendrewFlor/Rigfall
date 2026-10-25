// Materials and colours. Every mesh in Rigfall uses the vertex-coloured low-poly shader
// in Resources/Shaders, so there are no texture assets at all. Materials are cached by
// colour so hundreds of objects share a handful of materials (fewer draw-call batches).
using System.Collections.Generic;
using UnityEngine;

namespace Rigfall.Runtime
{
    public static class Palette
    {
        public static readonly Color Barrel = Hex("#FFB000");
        public static readonly Color Crit = Hex("#FFD23F");
        public static readonly Color Hurt = Hex("#FF5A5A");
        public static readonly Color Heal = Hex("#4FD18B");
        public static readonly Color Ui = Hex("#E8EEF0");
        public static readonly Color UiDim = Hex("#9DB4BB");
        public static readonly Color Panel = Hex("#122229");
        public static readonly Color PanelLight = Hex("#1D3A43");
        public static readonly Color Accent = Hex("#FF6A13");
        public static readonly Color Hazard = Hex("#F2C200");
        public static readonly Color Danger = Hex("#C2283A");
        public static readonly Color Friendly = Hex("#3FA7FF");

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
        }

        public static Color Rarity(string rarityId)
        {
            var db = Game.Db;
            var r = db != null ? db.Rarity(rarityId) : null;
            return r != null ? Hex(r.color) : Color.white;
        }
    }

    public static class Materials
    {
        static Shader lit, glow, particle;
        static readonly Dictionary<int, Material> litCache = new Dictionary<int, Material>();
        static readonly Dictionary<int, Material> glowCache = new Dictionary<int, Material>();
        static Material particleAlpha, particleAdd;
        static Texture2D softDot;

        static Shader Load(string name, string fallback)
        {
            var s = Resources.Load<Shader>("Shaders/" + name);
            if (s == null) s = Shader.Find(fallback);
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        static int Key(Color c) => ((int)(c.r * 255) << 24) ^ ((int)(c.g * 255) << 16) ^ ((int)(c.b * 255) << 8) ^ (int)(c.a * 255);

        /// <summary>Lit, vertex-coloured material tinted by a colour (white = model colours as authored).</summary>
        public static Material Lit(Color tint)
        {
            if (lit == null) lit = Load("RigfallLit", "Standard");
            int k = Key(tint);
            Material m;
            if (!litCache.TryGetValue(k, out m) || m == null)
            {
                m = new Material(lit) { name = "Lit " + ColorUtility.ToHtmlStringRGB(tint), color = tint };
                m.enableInstancing = true;
                litCache[k] = m;
            }
            return m;
        }

        /// <summary>A per-renderer copy for things that flash when hit. Destroy it with the object.</summary>
        public static Material LitInstance(Color tint)
        {
            var m = new Material(Lit(tint));
            m.name += " (instance)";
            return m;
        }

        /// <summary>Unlit, emissive colour for projectiles, flames and range rings.</summary>
        public static Material Glow(Color c)
        {
            if (glow == null) glow = Load("RigfallGlow", "Unlit/Color");
            int k = Key(c);
            Material m;
            if (!glowCache.TryGetValue(k, out m) || m == null)
            {
                m = new Material(glow) { name = "Glow", color = c };
                glowCache[k] = m;
            }
            return m;
        }

        public static Material Particle(bool additive)
        {
            if (particle == null) particle = Load("RigfallParticle", "Sprites/Default");
            if (softDot == null) softDot = MakeSoftDot(32);
            if (additive)
            {
                if (particleAdd == null)
                {
                    particleAdd = new Material(particle) { name = "Particle Add", mainTexture = softDot };
                    particleAdd.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    particleAdd.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                }
                return particleAdd;
            }
            if (particleAlpha == null)
            {
                particleAlpha = new Material(particle) { name = "Particle Alpha", mainTexture = softDot };
                particleAlpha.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                particleAlpha.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            return particleAlpha;
        }

        static Texture2D MakeSoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                    byte a = (byte)(Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
