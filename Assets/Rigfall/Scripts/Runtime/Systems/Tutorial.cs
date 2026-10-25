// The optional interactive tutorial (Main Menu -> Tutorial). Steps come from
// Resources/Data/tutorial.json; gameplay code calls Tutorial.Notify("event") and the
// current step advances when its event has happened enough times.
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public static class Tutorial
    {
        public static bool Active { get; private set; }
        public static int StepIndex { get; private set; }
        static int count;

        public static TutorialStepDef Current =>
            Active && Game.Db != null && StepIndex < Game.Db.Tutorial.Count ? Game.Db.Tutorial[StepIndex] : null;

        public static void Begin()
        {
            Active = true;
            StepIndex = 0;
            count = 0;
            Ui.ShowCoach(Current);
        }

        public static void Stop()
        {
            Active = false;
            Ui.ShowCoach(null);
        }

        public static void Notify(string evt)
        {
            var step = Current;
            if (step == null || step.advanceOn != evt) return;
            count++;
            if (count < Mathf.Max(1, step.count)) return;
            count = 0;
            StepIndex++;
            AudioService.Ui("sfx_quest_done", 1.1f, 0.7f);
            if (StepIndex >= Game.Db.Tutorial.Count)
            {
                Active = false;
                Ui.ShowCoach(null);
                Game.Profile.tutorialDone = true;
                Game.CheckAchievements();
                Game.SaveProfile();
                Ui.Banner("Tutorial complete!", Palette.Heal);
                return;
            }
            Ui.ShowCoach(Current);
        }
    }
}
