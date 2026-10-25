// The in-run HUD: barrels, day/night counter, wave progress, Main Rig integrity, score,
// hero health, ability cooldown, the current contract, hints and the night-horn hold bar.
using Rigfall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Rigfall.Runtime
{
    public sealed class Hud : MonoBehaviour
    {
        RectTransform root;
        Text barrels, barrelsSub, phase, phaseSub, rigLabel, score, heroLabel, ability, quest, hint, mods;
        Image rigFill, heroFill, abilityFill, waveFill, nightHold;
        RectTransform barrelBox, nightHoldBox, waveBox, questBox;
        Run run;
        int shownBarrels = -1;
        float holdAmount;

        public void Build(RectTransform layer)
        {
            root = UIKit.Rect("HudRoot", layer);
            UIKit.Stretch(root);

            // Barrels (top left)
            var bb = UIKit.Panel(root, new Color(0f, 0f, 0f, 0.55f));
            barrelBox = bb.rectTransform;
            UIKit.Place(barrelBox, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(330f, 96f));
            var icon = UIKit.Panel(barrelBox, Palette.Barrel);
            UIKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 4f), new Vector2(46f, 58f));
            var band = UIKit.Panel(icon.transform, new Color(0.35f, 0.2f, 0f), false);
            UIKit.Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 6f));
            barrels = UIKit.Title(barrelBox, "0", 46, Palette.Barrel, TextAnchor.UpperLeft);
            UIKit.Place(barrels.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(80f, -6f), new Vector2(240f, 56f));
            barrelsSub = UIKit.Label(barrelBox, "", 18, Palette.UiDim, TextAnchor.LowerLeft);
            UIKit.Place(barrelsSub.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(80f, 8f), new Vector2(250f, 30f));

            // Phase (top centre)
            var pb = UIKit.Panel(root, new Color(0f, 0f, 0f, 0.55f));
            UIKit.Place(pb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(520f, 76f));
            phase = UIKit.Title(pb.transform, "DAY 1", 40, Palette.Hazard, TextAnchor.UpperCenter);
            UIKit.Stretch(phase.rectTransform, 4f);
            phaseSub = UIKit.Label(pb.transform, "", 19, Palette.Ui, TextAnchor.LowerCenter);
            UIKit.Stretch(phaseSub.rectTransform, 6f);
            var wb = UIKit.Panel(root, new Color(0f, 0f, 0f, 0.5f), false);
            waveBox = wb.rectTransform;
            UIKit.Place(waveBox, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(520f, 8f));
            waveFill = UIKit.Panel(waveBox, Palette.Danger, false);
            Fill(waveFill.rectTransform, 1f);

            // Rig integrity + score (top right)
            var rb = UIKit.Panel(root, new Color(0f, 0f, 0f, 0.55f));
            UIKit.Place(rb.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(380f, 96f));
            rigLabel = UIKit.Label(rb.transform, "MAIN RIG", 20, Palette.Ui, TextAnchor.UpperLeft, UIKit.BoldFont);
            UIKit.Stretch(rigLabel.rectTransform, 12f);
            var rigBg = UIKit.Panel(rb.transform, new Color(0.25f, 0.05f, 0.05f), false);
            UIKit.Place(rigBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(350f, 16f));
            rigFill = UIKit.Panel(rigBg.transform, Palette.Heal, false);
            Fill(rigFill.rectTransform, 1f);
            score = UIKit.Label(rb.transform, "", 19, Palette.UiDim, TextAnchor.LowerLeft);
            UIKit.Stretch(score.rectTransform, 12f);

            // Hero (bottom left)
            var hb = UIKit.Panel(root, new Color(0f, 0f, 0f, 0.55f));
            UIKit.Place(hb.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(430f, 120f));
            heroLabel = UIKit.Label(hb.transform, "", 20, Palette.Ui, TextAnchor.UpperLeft, UIKit.BoldFont);
            UIKit.Stretch(heroLabel.rectTransform, 12f);
            var heroBg = UIKit.Panel(hb.transform, new Color(0.25f, 0.05f, 0.05f), false);
            UIKit.Place(heroBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, -2f), new Vector2(300f, 16f));
            heroFill = UIKit.Panel(heroBg.transform, Palette.Friendly, false);
            Fill(heroFill.rectTransform, 1f);
            var ab = UIKit.Panel(hb.transform, Palette.PanelLight);
            UIKit.Place(ab.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(92f, 92f));
            abilityFill = UIKit.Panel(ab.transform, Palette.Accent);
            UIKit.Stretch(abilityFill.rectTransform, 4f);
            abilityFill.type = Image.Type.Filled;
            abilityFill.fillMethod = Image.FillMethod.Vertical;
            abilityFill.sprite = UIKit.RoundedSmall; // Filled images need a sprite to fill
            ability = UIKit.Label(ab.transform, "", 16, Color.white, TextAnchor.MiddleCenter, UIKit.BoldFont);
            UIKit.Stretch(ability.rectTransform, 4f);
            mods = UIKit.Label(hb.transform, "", 16, Palette.UiDim, TextAnchor.LowerLeft);
            UIKit.Stretch(mods.rectTransform, 12f);
            mods.rectTransform.offsetMax = new Vector2(-110f, -12f);

            // Contract (right)
            var qb = UIKit.Panel(root, new Color(0f, 0f, 0f, 0.55f));
            questBox = qb.rectTransform;
            UIKit.Place(questBox, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 120f), new Vector2(380f, 120f));
            quest = UIKit.Label(questBox, "", 19, Palette.Ui, TextAnchor.UpperLeft);
            UIKit.Stretch(quest.rectTransform, 12f);

            // Hint + night horn (bottom centre)
            hint = UIKit.Label(root, "", 22, Palette.Ui, TextAnchor.LowerCenter, UIKit.BoldFont);
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1100f, 60f));
            hint.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            var nb = UIKit.Panel(root, new Color(0f, 0f, 0f, 0.6f), false);
            nightHoldBox = nb.rectTransform;
            UIKit.Place(nightHoldBox, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(460f, 12f));
            nightHold = UIKit.Panel(nightHoldBox, Palette.Danger, false);
            Fill(nightHold.rectTransform, 0f);

            root.gameObject.SetActive(false);
        }

        static void Fill(RectTransform r, float amount)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        public void Bind(Run r)
        {
            run = r;
            shownBarrels = -1;
            root.gameObject.SetActive(r != null);
        }

        public void Hide() { run = null; root.gameObject.SetActive(false); }

        public void PunchBarrels() { if (barrelBox != null) Tweener.Punch(barrelBox, 0.12f, 0.25f); }

        public void SetNightHold(float amount) { holdAmount = amount; }

        void Update()
        {
            if (run == null || run.Rig == null || run.Hero == null) { if (root.gameObject.activeSelf && run == null) root.gameObject.SetActive(false); return; }
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
            var db = Game.Db;

            if (shownBarrels != run.Barrels)
            {
                shownBarrels = run.Barrels;
                barrels.text = shownBarrels.ToString();
            }
            barrelsSub.text = run.FreeUpgrades > 0 ? UIKit.Colored(run.FreeUpgrades + " free work order" + (run.FreeUpgrades > 1 ? "s" : ""), Palette.Heal) : "barrels";

            bool night = run.IsNight;
            phase.text = (night ? "NIGHT " : "DAY ") + run.Night;
            phase.color = night ? Palette.Hurt : Palette.Hazard;
            if (night)
            {
                int alive = run.EnemiesAlive + run.Spawner.Pending;
                phaseSub.text = alive + " enemies left" + (string.IsNullOrEmpty(run.ChaosMutatorTonight) ? "" : " · " + db.Mutator(run.ChaosMutatorTonight).name);
                float total = Mathf.Max(1, run.Spawner.Total);
                Fill(waveFill.rectTransform, 1f - (run.Spawner.Total - run.Spawner.Pending - run.EnemiesAlive + 0f) / total);
                waveBox.gameObject.SetActive(true);
            }
            else
            {
                var plan = run.Plan;
                string boss = plan != null && !string.IsNullOrEmpty(plan.bossId) ? " · BOSS: " + db.Enemy(plan.bossId).name : "";
                string fresh = plan != null && !string.IsNullOrEmpty(plan.newEnemyId) ? " · New: " + db.Enemy(plan.newEnemyId).name : "";
                phaseSub.text = "Build and prepare" + boss + fresh;
                waveBox.gameObject.SetActive(false);
            }

            Fill(rigFill.rectTransform, run.Rig.Health.Fraction);
            rigFill.color = Color.Lerp(Palette.Hurt, Palette.Heal, run.Rig.Health.Fraction);
            rigLabel.text = "MAIN RIG  T" + run.RigTier + "   " + Mathf.CeilToInt(run.Rig.Health.current) + " / " + Mathf.CeilToInt(run.Rig.Health.max);
            score.text = "Score " + run.Score.ToString("N0") + "   ×" + run.ScoreMultiplier.ToString("0.##") + "   " + run.Map.name;

            var hero = run.Hero;
            Fill(heroFill.rectTransform, hero.Health.Fraction);
            heroLabel.text = hero.Downed
                ? UIKit.Colored(hero.WaitingForDawn ? "DOWN until dawn" : "DOWN · back in " + Mathf.CeilToInt(hero.RespawnIn) + " s", Palette.Hurt)
                : run.Class.name + " · " + UIKit.Colored(run.Weapon.name, Palette.Rarity(run.Weapon.rarity));
            abilityFill.fillAmount = hero.AbilityReady;
            ability.text = hero.AbilityReady >= 1f ? run.Class.abilityName.ToUpperInvariant().Replace(" ", "\n") + "\nREADY" : Mathf.CeilToInt((1f - hero.AbilityReady) * hero.AbilityCooldown) + "s";
            mods.text = run.Perks.Count + " perks · " + run.Mutators.Count + " mutators" + (run.Blessings.Count > 0 ? " · " + run.Blessings.Count + " blessings" : "");

            var q = run.Quest;
            var qdef = db.Quest(q.questId);
            questBox.gameObject.SetActive(qdef != null);
            if (qdef != null)
                quest.text = UIKit.Colored("CONTRACT · " + qdef.name, Palette.Hazard) + "\n" + Quests.Describe(q, db) + "\n" +
                             UIKit.Colored(Quests.ProgressText(q, db), q.failed ? Palette.Hurt : Palette.Heal) + "   " +
                             UIKit.Colored("+" + qdef.rewardBarrels + " bbl", Palette.Barrel);

            if (night) hint.text = hero.Downed ? "" : "Hold F to command units · Space to dash · Right click / K for " + run.Class.abilityName;
            else hint.text = Tutorial.Active ? "" : "Hold N to start Night " + run.Night + "   ·   Walk onto a slot and hold E to build";
            nightHoldBox.gameObject.SetActive(!night && holdAmount > 0f);
            Fill(nightHold.rectTransform, holdAmount);
        }
    }
}
