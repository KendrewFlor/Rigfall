using System.Collections;
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    /// <summary>Flashes a model white for a moment when it's hit.</summary>
    public sealed class HitFlash : MonoBehaviour
    {
        MeshRenderer[] renderers = new MeshRenderer[0];
        Material normal;
        float until;
        bool flashing;

        public void Bind(GameObject model, Material normalMaterial)
        {
            renderers = model != null ? model.GetComponentsInChildren<MeshRenderer>() : new MeshRenderer[0];
            normal = normalMaterial != null ? normalMaterial : Materials.Lit(Color.white);
        }

        public void Flash(float seconds = 0.08f)
        {
            until = Time.time + seconds;
            if (flashing) return;
            flashing = true;
            var white = Materials.Glow(new Color(1f, 0.95f, 0.9f));
            foreach (var r in renderers) if (r != null) r.sharedMaterial = white;
        }

        void Update()
        {
            if (!flashing || Time.time < until) return;
            flashing = false;
            foreach (var r in renderers) if (r != null) r.sharedMaterial = normal;
        }
    }
}
