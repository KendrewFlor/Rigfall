// Static shortcuts to the UIManager so gameplay code can say Ui.Toast("...") without
// holding references. Every call is safe before the UI exists (e.g. in tests or at boot).
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rigfall.Runtime
{
    public static class Ui
    {
        static UIManager M => Game.UI;

        public static bool PointerOverUi => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public static void Toast(string text) { if (M != null) M.Toast(text); }
        public static void Banner(string text, Color color) { if (M != null) M.Banner(text, color); }
        public static void Floating(Vector3 world, string text, Color color, float scale = 1f) { if (M != null) M.Floating(world, text, color, scale); }
        public static GameObject WorldLabel(Vector3 world, string text, Color color) => M != null ? M.WorldLabel(world, text, color) : new GameObject("label");
        public static void ShowPrompt(BuildingSlot owner, string title, string subtitle, List<UpgradeChoice> choices, int selected, float hold)
        { if (M != null) M.ShowPrompt(owner, title, subtitle, choices, selected, hold); }
        public static void HidePrompt(BuildingSlot owner) { if (M != null) M.HidePrompt(owner); }
        public static void ShowBossBar(Enemy e) { if (M != null) M.ShowBossBar(e); }
        public static void HideBossBar(Enemy e) { if (M != null) M.HideBossBar(e); }
        public static void HurtVignette() { if (M != null) M.HurtVignette(); }
        public static void ShowCoach(TutorialStepDef step) { if (M != null) M.ShowCoach(step); }
        public static void Achievement(AchievementDef a) { if (M != null) M.Achievement(a); }
    }
}
