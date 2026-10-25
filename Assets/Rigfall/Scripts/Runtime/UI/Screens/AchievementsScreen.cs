using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class AchievementsScreen : UiScreen
    {
        protected override void Build(RectTransform root)
        {
            var db = Game.Db;
            var p = Game.Profile;
            var card = Card(root, 1400f, 940f, Palette.Barrel);
            var title = UIKit.Title(card, "ACHIEVEMENTS  " + p.achievements.Count + "/" + db.Achievements.Count, 44, Palette.Barrel, TextAnchor.UpperLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(1000f, 60f));
            var back = UIKit.Button(card, "Back", () => Game.UI.Close(this), UIKit.Style.Ghost, 22);
            UIKit.Place((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -22f), new Vector2(160f, 50f));
            FirstSelected = back;

            var area = UIKit.Rect("Area", card);
            UIKit.Stretch(area, 28f);
            area.offsetMax = new Vector2(-28f, -96f);
            var list = UIKit.ScrollList(area, 8f);
            UIKit.Stretch((RectTransform)list.parent);

            foreach (var a in db.Achievements)
            {
                bool done = p.achievements.Contains(a.id);
                var bg = UIKit.Panel(list, done ? new Color(1f, 0.69f, 0f, 0.18f) : new Color(1f, 1f, 1f, 0.04f));
                UIKit.Size(bg, -1, 92);
                var t = UIKit.Label(bg.transform,
                    (done ? UIKit.Colored("◆ ", Palette.Barrel) : UIKit.Colored("◆ ", Palette.UiDim)) + UIKit.Colored(a.name, done ? Palette.Barrel : Palette.Ui) +
                    "\n" + UIKit.Colored(a.description, Palette.UiDim),
                    23, Palette.Ui, TextAnchor.MiddleLeft, UIKit.BoldFont);
                UIKit.Stretch(t.rectTransform, 14f);
                t.rectTransform.offsetMax = new Vector2(-520f, -8f);
                var r = UIKit.Label(bg.transform, Achievements.RewardText(a, db), 20, done ? Palette.Heal : Palette.Hazard, TextAnchor.MiddleRight);
                UIKit.Stretch(r.rectTransform, 14f);
                r.rectTransform.offsetMin = new Vector2(820f, 8f);
            }
        }
    }
}
