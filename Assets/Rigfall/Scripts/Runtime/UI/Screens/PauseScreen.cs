using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class PauseScreen : UiScreen
    {
        protected override void Build(RectTransform root)
        {
            var run = Run.Current;
            var db = Game.Db;
            var card = Card(root, 1100f, 760f);
            var title = UIKit.Title(card, "PAUSED", 56, Palette.Accent, TextAnchor.UpperLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -20f), new Vector2(600f, 70f));

            var col = UIKit.VStack(card, 12, 0, "Buttons");
            UIKit.Place((RectTransform)col.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -110f), new Vector2(460f, 560f));
            FirstSelected = UIKit.Button(col.transform, "Resume", Resume, UIKit.Style.Primary, 28);
            UIKit.Button(col.transform, "Settings", () => Game.UI.Open(new SettingsScreen()));
            UIKit.Button(col.transform, "Field Manual", () => Game.UI.Open(new ExtrasScreen()));
            if (run != null && !run.IsTutorial)
            {
                UIKit.Button(col.transform, "Restart from last dawn", () => Game.UI.Open(new ConfirmDialog("Restart from last dawn?",
                    "You go back to the start of Day " + run.Night + " with the buildings and barrels you had that morning.", "Restart", () => Game.I.RetryFromDawn())));
                UIKit.Button(col.transform, "Restart run from Day 1", () => Game.UI.Open(new ConfirmDialog("Restart the run?",
                    "Same field and loadout, from Day 1. Your permanent unlocks are kept.", "Restart", () => Game.I.RestartRun(), true)));
            }
            UIKit.Button(col.transform, run != null && !run.IsTutorial ? "Save & quit to menu" : "Quit to menu", () => Game.UI.Open(new ConfirmDialog("Quit to the main menu?",
                run != null && !run.IsTutorial ? "Your run is saved at the start of Day " + run.Night + ". Choose Continue on the main menu to carry on." : "The tutorial will end.",
                "Quit", () => Game.I.QuitToMenu())), UIKit.Style.Danger);

            if (run == null) return;
            var info = UIKit.Panel(card, new Color(0f, 0f, 0f, 0.25f));
            UIKit.Place(info.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -110f), new Vector2(520f, 610f));
            var sb = new System.Text.StringBuilder();
            sb.Append(UIKit.Colored(run.Map.name + " · " + run.Difficulty.name, Palette.Hazard)).Append('\n');
            sb.Append("Day ").Append(run.Night).Append(" · Score ").Append(run.Score.ToString("N0")).Append('\n');
            sb.Append(run.Class.name).Append(" · ").Append(UIKit.Colored(run.Weapon.name, Palette.Rarity(run.Weapon.rarity))).Append("\n\n");
            sb.Append(UIKit.Colored("Perks", Palette.Hazard)).Append('\n');
            if (run.Perks.Count == 0) sb.Append(UIKit.Colored("none\n", Palette.UiDim));
            foreach (var p in run.Perks) sb.Append("• ").Append(p.name).Append(UIKit.Colored(" · " + p.description, Palette.UiDim)).Append('\n');
            sb.Append('\n').Append(UIKit.Colored("Mutators (" + run.Mutators.Count + ")", Palette.Hazard)).Append('\n');
            int shown = 0;
            foreach (var m in run.Mutators) if (shown++ < 7) sb.Append("• ").Append(m.name).Append('\n');
            if (run.Mutators.Count > 7) sb.Append("… and ").Append(run.Mutators.Count - 7).Append(" more\n");
            sb.Append('\n').Append(UIKit.Colored("Controls", Palette.Hazard)).Append('\n')
              .Append("WASD move · LMB/J attack · RMB/K ability · Space dash\nE hold to build · Q/Tab next option · F hold to command · N hold for night");
            var t = UIKit.Label(info.transform, sb.ToString(), 19, Palette.Ui, TextAnchor.UpperLeft);
            UIKit.Stretch(t.rectTransform, 16f);
        }

        void Resume()
        {
            Game.UI.Close(this);
            Game.I.Resume();
        }

        public override bool OnBack()
        {
            Game.I.Resume();
            return true;
        }
    }
}
