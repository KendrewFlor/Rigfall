// The on-screen movement stick. Writes a -1..1 vector to InputReader.TouchMove.
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rigfall.Runtime
{
    public sealed class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform knob;
        RectTransform rect;

        void Awake() { rect = (RectTransform)transform; }

        public void OnPointerDown(PointerEventData e) => OnDrag(e);

        public void OnDrag(PointerEventData e)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out local)) return;
            float radius = rect.sizeDelta.x * 0.5f;
            Vector2 v = Vector2.ClampMagnitude(local / radius, 1f);
            InputReader.TouchMove = v;
            if (knob != null) knob.anchoredPosition = v * radius * 0.6f;
        }

        public void OnPointerUp(PointerEventData e)
        {
            InputReader.TouchMove = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }

        void OnDisable()
        {
            InputReader.TouchMove = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }
    }
}
