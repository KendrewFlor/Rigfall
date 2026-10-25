// Juice for every button: hover/select sound, a small scale pop, and a click sound.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rigfall.Runtime
{
    public sealed class UiButtonFx : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerClickHandler, ISubmitHandler
    {
        Button button;

        void Awake() { button = GetComponent<Button>(); }

        public void OnPointerEnter(PointerEventData e) => Hover();
        public void OnSelect(BaseEventData e) => Hover();

        void Hover()
        {
            if (button != null && !button.interactable) return;
            AudioService.Ui("sfx_ui_hover", 1f, 0.5f);
            Tweener.Punch(transform, 0.05f, 0.18f);
        }

        public void OnPointerClick(PointerEventData e) => Click();
        public void OnSubmit(BaseEventData e) => Click();

        void Click()
        {
            if (button != null && !button.interactable) { AudioService.Ui("sfx_buy_fail", 1f, 0.6f); return; }
            AudioService.Ui("sfx_ui_click");
            Tweener.Punch(transform, 0.1f, 0.22f);
        }
    }
}
