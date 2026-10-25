// Legendary and Blowout Brilliant weapons shimmer: small sparkles around the model, and
// Blowout Brilliant ones cycle through an oil-slick rainbow.
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class RarityGlow : MonoBehaviour
    {
        Color color;
        float next;
        bool rainbow;

        public void Setup(Color c)
        {
            color = c;
            rainbow = c.r > 0.9f && c.b > 0.7f; // the Blowout Brilliant magenta
        }

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 0.15f;
            Color c = rainbow ? Color.HSVToRGB(Mathf.Repeat(Time.time * 0.5f, 1f), 0.8f, 1f) : color;
            Fx.Emit(rainbow ? FxKind.Brilliant : FxKind.Coin, transform.position, 1, c);
        }
    }
}
