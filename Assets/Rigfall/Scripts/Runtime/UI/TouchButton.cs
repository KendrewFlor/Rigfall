// A press-and-hold on-screen button. Reports pressed/released through onChange.
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rigfall.Runtime
{
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public System.Action<bool> onChange;

        public void OnPointerDown(PointerEventData e)
        {
            onChange?.Invoke(true);
            Tweener.Punch(transform, 0.12f, 0.15f);
        }

        public void OnPointerUp(PointerEventData e) => onChange?.Invoke(false);
        public void OnPointerExit(PointerEventData e) => onChange?.Invoke(false);
        void OnDisable() => onChange?.Invoke(false);
    }
}
