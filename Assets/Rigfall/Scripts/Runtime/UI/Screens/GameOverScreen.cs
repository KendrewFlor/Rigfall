// The Main Rig fell. The run's barrels and buildings are lost, but nothing permanent is:
// retry from the last dawn, restart the run, change the loadout or go back to the menu.
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class GameOverScreen : UiScreen
    {
        readonly Run run;
        public GameOverScreen(Run run) { this.run = run; }
        public override float DimAlpha => 0.6f;

        protected override void Build(RectTransform root)
        {
            var card = Card(root, 1200f, 760f, Palette.Danger);
            var title = UIKit.Title(card, "THE RIG HAS FALLEN", 64, Palette.Hurt, TextAnchor.UpperLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -24f), new Vector2(1100f, 80f));

            var s = run.Stats;
            var sb = new System.Text.StringBuilder();
            sb.Append(run.Map.name).Append(" · ").Append(run.Difficulty.name).Append(" · ").Append(run.Class.name).Append("\n\n");
            sb.Append("Fell on night ").Append(UIKit.Colored(run.Night.ToString(), Palette.Hazard)).Append(" · nights survived ").Append(s.nightsSurvived).Append('\n');
            sb.Append("Score ").Append(UIKit.Colored(run.Score.ToString("N0"), Palette.Barrel)).Append(" · enemies defeated ").Append(s.kills)
              .Append(" (by you ").Append(s.heroKills).Append(") · bosses ").Append(s.bossKills).Append('\n');
            sb.Append("Buildings built ").Append(s.buildingsBuilt).Append(" · barrels earned ").Append(s.barrelsEarned).Append("\n\n");
            sb.Append(UIKit.Colored("You keep everything permanent: unlocks, weapon levels, achievements, core samples and field stages. Only this run's barrels and buildings are lost, and you can pick it up again from the last dawn.", Palette.Heal));
            var info = UIKit.Label(card, sb.ToString(), 24, Palette.Ui, TextAnchor.UpperLeft);
            UIKit.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -130f), new Vector2(1120f, 360f));

            var grid = UIKit.Grid(card, new Vector2(550f, 64f), new Vector2(16f, 14f));
            UIKit.Place((RectTransform)grid.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 30f), new Vector2(1120f, 160f));
            if (!run.IsTutorial)
            {
                FirstSelected = UIKit.Button(grid.transform, "Retry from last dawn (Day " + run.Night + ")", () => Game.I.RetryFromDawn(), UIKit.Style.Primary, 24);
                UIKit.Button(grid.transform, "Restart run from Day 1", () => Game.I.RestartRun(), UIKit.Style.Normal, 24);
                UIKit.Button(grid.transform, "Change loadout", () => { Game.I.QuitToMenu(); Game.UI.Open(new LoadoutScreen()); }, UIKit.Style.Normal, 24);
            }
            var menu = UIKit.Button(grid.transform, "Main menu", () => Game.I.QuitToMenu(), UIKit.Style.Ghost, 24);
            if (FirstSelected == null) FirstSelected = menu;
        }

        public override bool OnBack() => false;
    }
}
