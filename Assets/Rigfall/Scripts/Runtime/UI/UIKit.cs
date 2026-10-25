// Helpers that build uGUI elements from code: panels with rounded corners (a procedurally
// generated 9-sliced sprite), text in the game's fonts, buttons with juice, stacks,
// scroll lists and "steppers" (◀ value ▶) used for settings and loadout choices.
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rigfall.Runtime
{
    public static class UIKit
    {
        static Sprite rounded, roundedSmall, circle;
        static Font title, body, bold;

        public static Font TitleFont => title != null ? title : (title = LoadFont("Fonts/SairaStencilOne-Regular"));
        public static Font BodyFont => body != null ? body : (body = LoadFont("Fonts/ChakraPetch-Regular"));
        public static Font BoldFont => bold != null ? bold : (bold = LoadFont("Fonts/ChakraPetch-Bold"));

        static Font LoadFont(string path)
        {
            var f = Resources.Load<Font>(path);
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }

        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(64, 16));
        public static Sprite RoundedSmall => roundedSmall != null ? roundedSmall : (roundedSmall = MakeRounded(32, 7));
        public static Sprite Circle => circle != null ? circle : (circle = MakeRounded(64, 32));

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = Mathf.Min(radius + 1, size / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        // ---- rect helpers -----------------------------------------------------------------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
            return r;
        }

        /// <summary>Anchor at a normalised point with a fixed size and offset.</summary>
        public static RectTransform Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        public static LayoutElement Size(Component c, float width = -1f, float height = -1f, float flexW = -1f, float flexH = -1f)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) { le.preferredWidth = width; le.minWidth = width; }
            if (height >= 0) { le.preferredHeight = height; le.minHeight = height; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        // ---- building blocks --------------------------------------------------------------

        public static Image Panel(Transform parent, Color color, bool roundedCorners = true, string name = "Panel")
        {
            var r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            if (roundedCorners) { img.sprite = Rounded; img.type = Image.Type.Sliced; }
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, Font font = null, string name = "Label")
        {
            var r = Rect(name, parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = font != null ? font : BodyFont;
            t.text = text;
            t.fontSize = Mathf.RoundToInt(size * Game.Settings.uiScale);
            t.color = color;
            t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Text Title(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
            => Label(parent, text, size, color, align, TitleFont, "Title");

        public enum Style { Primary, Normal, Danger, Ghost }

        public static Button Button(Transform parent, string text, UnityAction onClick, Style style = Style.Normal, int fontSize = 26)
        {
            Color bg, fg;
            switch (style)
            {
                case Style.Primary: bg = Palette.Accent; fg = new Color(0.1f, 0.04f, 0.01f); break;
                case Style.Danger: bg = Palette.Danger; fg = Color.white; break;
                case Style.Ghost: bg = new Color(1f, 1f, 1f, 0.06f); fg = Palette.Ui; break;
                default: bg = Palette.PanelLight; fg = Palette.Ui; break;
            }
            var img = Panel(parent, bg, true, "Button " + text);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.selectedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            colors.colorMultiplier = 1.3f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            var label = Label(img.transform, text, fontSize, fg, TextAnchor.MiddleCenter, BoldFont);
            Stretch(label.rectTransform, 6f);
            if (onClick != null) btn.onClick.AddListener(onClick);
            img.gameObject.AddComponent<UiButtonFx>();
            Size(img, -1, 56);
            return btn;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static VerticalLayoutGroup VStack(Transform parent, float spacing, int padding, string name = "VStack")
        {
            var r = Rect(name, parent);
            var v = r.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(Transform parent, float spacing, int padding, string name = "HStack")
        {
            var r = Rect(name, parent);
            var h = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padding, padding, padding, padding);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;
            return h;
        }

        public static GridLayoutGroup Grid(Transform parent, Vector2 cell, Vector2 spacing, int padding = 0)
        {
            var r = Rect("Grid", parent);
            var g = r.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = spacing;
            g.padding = new RectOffset(padding, padding, padding, padding);
            var fit = r.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return g;
        }

        /// <summary>A vertical scroll view. Returns the content transform to fill.</summary>
        public static RectTransform ScrollList(Transform parent, float spacing = 10f, string name = "Scroll")
        {
            var view = Rect(name, parent);
            view.gameObject.AddComponent<RectMask2D>();
            var bg = view.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.001f); // receives scroll-wheel events
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            var content = Rect("Content", view);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(4, 16, 4, 4);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = view;
            return content;
        }

        /// <summary>A labelled ◀ value ▶ control. getText is called to refresh the value text.</summary>
        public static Text Stepper(Transform parent, string label, Func<string> getText, Action prev, Action next, string help = null)
        {
            var row = HStack(parent, 12, 6, "Stepper " + label);
            Size(row, -1, 62);
            var name = Label(row.transform, label, 24, Palette.Ui);
            Size(name, 330, -1, 1);
            Text value = null;
            var left = Button(row.transform, "◀", () => { prev(); value.text = getText(); AudioService.Ui("sfx_ui_click"); }, Style.Ghost, 24);
            Size(left, 60, 50);
            var box = Panel(row.transform, new Color(0f, 0f, 0f, 0.25f));
            Size(box, 320, 50);
            value = Label(box.transform, getText(), 24, Palette.Hazard, TextAnchor.MiddleCenter, BoldFont);
            Stretch(value.rectTransform, 4f);
            var right = Button(row.transform, "▶", () => { next(); value.text = getText(); AudioService.Ui("sfx_ui_click"); }, Style.Ghost, 24);
            Size(right, 60, 50);
            if (!string.IsNullOrEmpty(help))
            {
                var h = Label(parent, help, 18, Palette.UiDim);
                Size(h, -1, 26);
            }
            return value;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
        public static string Colored(string text, Color c) => "<color=" + Hex(c) + ">" + text + "</color>";
        public static string RarityName(string rarityId)
        {
            var r = Game.Db.Rarity(rarityId);
            return Colored(r != null ? r.name : rarityId, Palette.Rarity(rarityId));
        }
    }
}
