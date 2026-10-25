// Dawn after a cleared night: income breakdown, contract result, stage clears and new
// weapons, then the reward wheel ("core sample wheel") with its rarity segments.
using System.Collections;
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Rigfall.Runtime
{
    public sealed class DawnScreen : UiScreen
    {
        readonly Run run;
        readonly Run.DawnReport report;
        RectTransform wheel;
        Text result;
        Button spin, cont;
        bool spun;

        public override float DimAlpha => 0.55f;

        public DawnScreen(Run run, Run.DawnReport report) { this.run = run; this.report = report; }

        protected override void Build(RectTransform root)
        {
            var db = Game.Db;
            var card = Card(root, 1600f, 880f, Palette.Barrel);
            var title = UIKit.Title(card, "NIGHT " + run.Night + " CLEARED", 56, Palette.Barrel, TextAnchor.UpperLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -20f), new Vector2(900f, 70f));
            var sub = UIKit.Label(card, "The sun comes up over the crown block. Every building is repaired.", 22, Palette.UiDim, TextAnchor.UpperLeft);
            UIKit.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -92f), new Vector2(900f, 40f));

            // Left: report.
            var sb = new System.Text.StringBuilder();
            sb.Append(UIKit.Colored("DAWN INCOME  +" + report.income.total + " bbl", Palette.Barrel)).Append('\n');
            foreach (var line in report.income.lines) sb.Append("   ").Append(line).Append('\n');
            if (report.income.multiplier != 1f) sb.Append(UIKit.Colored("   (price and perk multiplier ×" + report.income.multiplier.ToString("0.00") + ")", Palette.UiDim)).Append('\n');
            sb.Append('\n');
            if (!string.IsNullOrEmpty(report.questName))
            {
                sb.Append(report.questDone
                    ? UIKit.Colored("CONTRACT COMPLETE · " + report.questName + "  +" + report.questBarrels + " bbl  +" + report.questCore + " core", Palette.Heal)
                    : UIKit.Colored("Contract failed · " + report.questName, Palette.Hurt)).Append("\n\n");
            }
            sb.Append(UIKit.Colored("+" + report.corePerNight + " core samples", Palette.Friendly)).Append(" for surviving the night\n");
            if (report.stageCleared) sb.Append('\n').Append(UIKit.Colored("STAGE " + new string('I', report.stage) + " CLEARED ON " + run.Map.name.ToUpperInvariant(), Palette.Crit)).Append('\n');
            foreach (var w in report.newWeapons)
            {
                var def = db.Weapon(w);
                if (def != null) sb.Append(UIKit.Colored("New weapon: " + def.name, Palette.Rarity(def.rarity))).Append('\n');
            }
            int next = run.Night + 1;
            sb.Append('\n').Append(UIKit.Colored("Next: Night " + next, Palette.Hazard));
            if (WaveDirector.IsBossNight(next, run.Mods)) sb.Append(UIKit.Colored("  ·  BOSS NIGHT", Palette.Danger));
            string fresh = WaveDirector.NewEnemyOn(run.Map, next);
            if (!string.IsNullOrEmpty(fresh)) sb.Append(UIKit.Colored("  ·  new enemy: " + db.Enemy(fresh).name, Palette.Hurt));
            var info = UIKit.Label(card, sb.ToString(), 22, Palette.Ui, TextAnchor.UpperLeft);
            UIKit.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -150f), new Vector2(780f, 600f));

            // Right: the wheel.
            var holder = UIKit.Rect("Wheel holder", card);
            UIKit.Place(holder, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-400f, -360f), new Vector2(560f, 560f));
            wheel = UIKit.Rect("Wheel", holder);
            UIKit.Stretch(wheel);
            var segs = RewardWheel.Segments(db, run.Mods.Get(Stat.WheelLuck, 0f));
            foreach (var s in segs)
            {
                var img = UIKit.Panel(wheel, Palette.Hex(s.rarity.color), false, "Seg " + s.rarity.id);
                UIKit.Stretch(img.rectTransform);
                img.sprite = UIKit.Circle;
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Top;
                img.fillClockwise = true;
                img.fillAmount = s.sweep / 360f;
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -s.startAngle);
                var label = UIKit.Label(wheel, s.rarity.name + "\n" + (s.probability * 100f).ToString(s.probability < 0.01f ? "0.0" : "0") + "%",
                    s.sweep < 20f ? 14 : 20, Color.black, TextAnchor.MiddleCenter, UIKit.BoldFont);
                float mid = (s.startAngle + s.sweep * 0.5f) * Mathf.Deg2Rad;
                UIKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Mathf.Sin(mid), Mathf.Cos(mid)) * 185f, new Vector2(160f, 60f));
                label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(s.startAngle + s.sweep * 0.5f));
            }
            var hub = UIKit.Panel(holder, Palette.Panel);
            hub.sprite = UIKit.Circle;
            UIKit.Place(hub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            var pointer = UIKit.Panel(holder, Color.white, false, "Pointer");
            UIKit.Place(pointer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, -24f), new Vector2(26f, 54f));

            result = UIKit.Label(card, "Clearing the night earned one spin of the core sample wheel.", 22, Palette.Ui, TextAnchor.UpperCenter);
            UIKit.Place(result.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-400f, 110f), new Vector2(720f, 110f));

            var row = UIKit.HStack(card, 16, 0, "Buttons");
            UIKit.Place((RectTransform)row.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 26f), new Vector2(760f, 66f));
            row.childForceExpandWidth = true;
            spin = UIKit.Button(row.transform, "SPIN THE WHEEL", Spin, UIKit.Style.Primary, 28);
            cont = UIKit.Button(row.transform, "Start Day " + next, Continue, UIKit.Style.Normal, 28);
            cont.interactable = false;
            FirstSelected = spin;
        }

        void Spin()
        {
            if (spun) return;
            spun = true;
            spin.interactable = false;
            var rng = new Rng(run.SeedHash ^ (ulong)(run.Night * 2654435761L));
            var r = RewardWheel.Spin(Game.Db, run.Mods.Get(Stat.WheelLuck, 0f), rng, run.Night);
            Game.I.StartCoroutine(SpinRoutine(r));
        }

        IEnumerator SpinRoutine(WheelResult r)
        {
            float target = 360f * 6f + r.landAngle;
            float duration = 3.6f, t = 0f, lastTick = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / duration);
                float angle = target * Tweener.EaseOutCubic(x);
                wheel.localRotation = Quaternion.Euler(0f, 0f, angle);
                if (angle - lastTick >= 22f) { lastTick = angle; AudioService.Ui("sfx_wheel_tick", 0.9f + 0.4f * (1f - x), 0.7f); }
                yield return null;
            }
            wheel.localRotation = Quaternion.Euler(0f, 0f, target);
            if (wheel == null) yield break;

            string extra = run.ApplyWheel(r);
            bool brilliant = r.rarity.id == "brilliant";
            AudioService.Ui(brilliant ? "sfx_rarity_brilliant" : "sfx_wheel_win");
            Tweener.Punch(wheel.parent, 0.15f, 0.4f);
            if (r.rarity.order >= 2) Fx.Shake(0.3f + 0.2f * r.rarity.order);
            if (brilliant && run.Hero != null) Fx.Emit(FxKind.Brilliant, run.Hero.transform.position + Vector3.up * 2f, 80);
            result.text = UIKit.Colored(r.rarity.name.ToUpperInvariant() + " · " + (r.reward != null ? r.reward.name : ""), Palette.Rarity(r.rarity.id)) + "\n" +
                          RewardWheel.Describe(r) + (string.IsNullOrEmpty(extra) ? "" : "\n" + UIKit.Colored(extra, Palette.Heal));
            Tweener.ScaleIn(result.transform, 0.3f, true);
            cont.interactable = true;
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(cont.gameObject);
        }

        void Continue()
        {
            if (!spun) return;
            Game.UI.Close(this);
            Game.I.ContinueToDay();
        }

        public override bool OnBack() => false; // finish the dawn first
    }
}
