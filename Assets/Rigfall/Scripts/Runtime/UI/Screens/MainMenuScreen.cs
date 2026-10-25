using Rigfall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Rigfall.Runtime
{
    public sealed class MainMenuScreen : UiScreen
    {
        public override float DimAlpha => 0.15f;

        protected override void Build(RectTransform root)
        {
            var db = Game.Db;
            var p = Game.Profile;

            var title = UIKit.Title(root, "RIGFALL", 150, Palette.Accent, TextAnchor.MiddleLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -90f), new Vector2(1000f, 170f));
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(6f, -6f);
            var sub = UIKit.Label(root, "Build by day. Defend the rig by night.", 32, Palette.Ui, TextAnchor.MiddleLeft, UIKit.BoldFont);
            UIKit.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(118f, -260f), new Vector2(1000f, 50f));
            sub.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);

            var col = UIKit.VStack(root, 14, 0, "Menu");
            UIKit.Place((RectTransform)col.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, -110f), new Vector2(520f, 620f));

            var saved = SaveService.LoadRun(db);
            if (saved != null)
            {
                var map = db.Map(saved.mapId);
                var cont = UIKit.Button(col.transform, "Continue · " + map.name + " · Day " + saved.night, () => Game.I.ContinueSavedRun(), UIKit.Style.Primary, 26);
                FirstSelected = cont;
            }
            var play = UIKit.Button(col.transform, saved != null ? "New hitch" : "Play", () => Game.UI.Open(new LoadoutScreen()), saved != null ? UIKit.Style.Normal : UIKit.Style.Primary, 30);
            if (FirstSelected == null) FirstSelected = play;
            UIKit.Button(col.transform, p.tutorialDone ? "Tutorial" : "Tutorial (recommended)", () => Game.I.StartTutorial());
            UIKit.Button(col.transform, "Achievements  " + p.achievements.Count + "/" + db.Achievements.Count, () => Game.UI.Open(new AchievementsScreen()));
            UIKit.Button(col.transform, "Field Manual & Extras", () => Game.UI.Open(new ExtrasScreen()));
            UIKit.Button(col.transform, "Settings", () => Game.UI.Open(new SettingsScreen()));
            if (Application.platform != RuntimePlatform.WebGLPlayer)
                UIKit.Button(col.transform, "Quit", () => Application.Quit(), UIKit.Style.Ghost);

            var info = UIKit.Panel(root, new Color(0f, 0f, 0f, 0.55f));
            UIKit.Place(info.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(560f, 170f));
            int fields = 0;
            foreach (var m in db.Maps) if (Progress.IsMapUnlocked(p, m)) fields++;
            var t = UIKit.Label(info.transform,
                UIKit.Colored("ENGINEER RECORD", Palette.Hazard) + "\n" +
                "Core samples: " + UIKit.Colored(p.coreSamples.ToString(), Palette.Barrel) + "\n" +
                "Best night: " + p.stats.bestNight + "   ·   Nights survived: " + p.stats.nights + "\n" +
                "Fields open: " + fields + "/" + db.Maps.Count + "   ·   Perks: " + Progress.UnlockedPerkCount(p, db) + "/" + db.Perks.Count,
                22, Palette.Ui, TextAnchor.UpperLeft);
            UIKit.Stretch(t.rectTransform, 16f);

            var ver = UIKit.Label(root, "v" + Application.version + "  ·  A petroleum engineering tower-defence game", 16, Palette.UiDim, TextAnchor.LowerLeft);
            UIKit.Place(ver.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 16f), new Vector2(900f, 30f));
        }

        public override bool OnBack() => false; // the main menu stays open
    }
}
