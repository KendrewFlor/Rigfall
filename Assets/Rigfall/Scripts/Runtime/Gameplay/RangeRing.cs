using System.Collections;
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    /// <summary>A circle drawn on the ground showing a tower's range (during the day).</summary>
    public sealed class RangeRing : MonoBehaviour
    {
        LineRenderer line;
        float radius = -1f;

        public static RangeRing Create(Transform parent, Color color)
        {
            var go = new GameObject("Range ring");
            go.transform.SetParent(parent, false);
            var ring = go.AddComponent<RangeRing>();
            ring.line = go.AddComponent<LineRenderer>();
            ring.line.useWorldSpace = false;
            ring.line.loop = true;
            ring.line.widthMultiplier = 0.12f;
            ring.line.sharedMaterial = Materials.Glow(color);
            ring.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.line.receiveShadows = false;
            go.SetActive(false);
            return ring;
        }

        public void SetRadius(float r)
        {
            if (Mathf.Approximately(r, radius)) return;
            radius = r;
            const int n = 64;
            line.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0.08f, Mathf.Sin(a) * r));
            }
        }

        public void Show(bool on) { if (gameObject.activeSelf != on) gameObject.SetActive(on); }
    }
}
