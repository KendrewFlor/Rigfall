// Base class for full-screen menus and dialogs. Screens are plain C# objects that build
// their uGUI hierarchy under a root the UIManager gives them, and are kept on a stack so
// Back / Escape returns to the previous one.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rigfall.Runtime
{
    public abstract class UiScreen
    {
        public RectTransform Root { get; private set; }
        protected Selectable FirstSelected;

        /// <summary>Darken what's behind the screen.</summary>
        public virtual float DimAlpha => 0.72f;
        /// <summary>Screens that block hero input (all of them, except the HUD-like ones).</summary>
        public virtual bool BlocksInput => true;

        public void Create(RectTransform layer)
        {
            Root = UIKit.Rect(GetType().Name, layer);
            UIKit.Stretch(Root);
            if (DimAlpha > 0f)
            {
                var dim = Root.gameObject.AddComponent<Image>();
                dim.color = new Color(0.02f, 0.05f, 0.07f, DimAlpha);
                dim.raycastTarget = true;
            }
            Build(Root);
            Tweener.ScaleIn(Root, 0.25f, true);
        }

        protected abstract void Build(RectTransform root);

        public virtual void OnShow() { Select(); }
        public virtual void OnHide() { }

        /// <summary>Back/Escape pressed. Return true to close this screen.</summary>
        public virtual bool OnBack() => true;

        public void Select()
        {
            if (FirstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(FirstSelected.gameObject);
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
        }

        /// <summary>A centred card of the given size, the usual frame for a screen.</summary>
        protected static RectTransform Card(RectTransform root, float width, float height, Color? border = null)
        {
            var frame = UIKit.Panel(root, border ?? Palette.Accent);
            UIKit.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width + 6, height + 6));
            var card = UIKit.Panel(frame.transform, Palette.Panel);
            UIKit.Stretch(card.rectTransform, 3f);
            return card.rectTransform;
        }
    }
}
