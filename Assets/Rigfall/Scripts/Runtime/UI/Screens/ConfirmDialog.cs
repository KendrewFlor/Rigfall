using System;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class ConfirmDialog : UiScreen
    {
        readonly string title, text, yes;
        readonly Action onYes;
        readonly bool danger;

        public ConfirmDialog(string title, string text, string yes, Action onYes, bool danger = false)
        {
            this.title = title; this.text = text; this.yes = yes; this.onYes = onYes; this.danger = danger;
        }

        protected override void Build(RectTransform root)
        {
            var card = Card(root, 760f, 320f, danger ? Palette.Danger : Palette.Accent);
            var t = UIKit.Title(card, title, 40, Palette.Ui, TextAnchor.UpperLeft);
            UIKit.Stretch(t.rectTransform, 28f);
            var body = UIKit.Label(card, text, 24, Palette.UiDim, TextAnchor.UpperLeft);
            UIKit.Stretch(body.rectTransform, 28f);
            body.rectTransform.offsetMax = new Vector2(-28f, -90f);
            var row = UIKit.HStack(card, 16, 0, "Buttons");
            UIKit.Place((RectTransform)row.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 24f), new Vector2(560f, 60f));
            row.childAlignment = TextAnchor.MiddleRight;
            row.childForceExpandWidth = true;
            var cancel = UIKit.Button(row.transform, "Cancel", () => Game.UI.Close(this));
            UIKit.Button(row.transform, yes, () => { Game.UI.Close(this); onYes?.Invoke(); }, danger ? UIKit.Style.Danger : UIKit.Style.Primary);
            FirstSelected = cancel;
        }
    }
}
