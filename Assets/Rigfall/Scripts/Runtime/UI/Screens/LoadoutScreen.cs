// "Plan your hitch": choose the field, difficulty, class, weapon (and upgrade it), up to
// 4 perks and any mutators, then start the run.
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Rigfall.Runtime
{
    public sealed class LoadoutScreen : UiScreen
    {
        enum Tab { Field, Engineer, Perks, Mutators }

        Tab tab = Tab.Field;
        Loadout l;
        RectTransform content, tabs;
        Text summary, startHint;
        Button start;

        static GameDatabase Db => Game.Db;
        static Profile P => Game.Profile;

        protected override void Build(RectTransform root)
        {
            Progress.FixLoadout(P, Db);
            var src = P.loadout;
            l = new Loadout { mapId = src.mapId, difficultyId = src.difficultyId, classId = src.classId, weaponId = src.weaponId,
                perks = new List<string>(src.perks), mutators = new List<string>(src.mutators) };

            var card = Card(root, 1760f, 960f);
            var title = UIKit.Title(card, "PLAN YOUR HITCH", 46, Palette.Accent, TextAnchor.UpperLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(900f, 60f));

            var tabRow = UIKit.HStack(card, 10, 0, "Tabs");
            tabs = (RectTransform)tabRow.transform;
            UIKit.Place(tabs, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -90f), new Vector2(1180f, 56f));
            tabRow.childForceExpandWidth = true;

            var area = UIKit.Panel(card, new Color(0f, 0f, 0f, 0.25f));
            UIKit.Place(area.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -160f), new Vector2(1180f, 770f));
            content = UIKit.ScrollList(area.transform, 10f);
            UIKit.Stretch((RectTransform)content.parent, 10f);

            var side = UIKit.Panel(card, Palette.PanelLight);
            UIKit.Place(side.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -90f), new Vector2(490f, 840f));
            summary = UIKit.Label(side.transform, "", 22, Palette.Ui, TextAnchor.UpperLeft);
            UIKit.Stretch(summary.rectTransform, 18f);
            summary.rectTransform.offsetMin = new Vector2(18f, 170f);
            startHint = UIKit.Label(side.transform, "", 18, Palette.UiDim, TextAnchor.LowerLeft);
            UIKit.Place(startHint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 100f), new Vector2(450f, 60f));
            start = UIKit.Button(side.transform, "START HITCH", StartRun, UIKit.Style.Primary, 32);
            UIKit.Place((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(450f, 72f));
            var back = UIKit.Button(card, "Back", () => Game.UI.Close(this), UIKit.Style.Ghost, 22);
            UIKit.Place((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -22f), new Vector2(160f, 50f));
            FirstSelected = start;
            Refresh();
        }

        void Refresh()
        {
            foreach (Transform c in tabs) Object.Destroy(c.gameObject);
            AddTab("Field & Difficulty", Tab.Field);
            AddTab("Engineer & Weapon", Tab.Engineer);
            AddTab("Perks " + l.perks.Count + "/" + Progress.MaxPerks, Tab.Perks);
            AddTab("Mutators " + ActiveMutators().Count, Tab.Mutators);

            foreach (Transform c in content) Object.Destroy(c.gameObject);
            switch (tab)
            {
                case Tab.Field: BuildFields(); break;
                case Tab.Engineer: BuildEngineer(); break;
                case Tab.Perks: BuildPerks(); break;
                case Tab.Mutators: BuildMutators(); break;
            }
            RefreshSummary();
        }

        void AddTab(string text, Tab t)
        {
            var b = UIKit.Button(tabs, text, () => { tab = t; Refresh(); }, tab == t ? UIKit.Style.Primary : UIKit.Style.Normal, 22);
            UIKit.Size(b, -1, 52, 1);
        }

        List<MutatorDef> ActiveMutators() => Progress.ActiveMutators(Db, Db.Difficulty(l.difficultyId), l.mutators);

        // ---- tabs -------------------------------------------------------------------------

        void BuildFields()
        {
            Section("FIELDS");
            foreach (var m in Db.Maps)
            {
                bool open = Progress.IsMapUnlocked(P, m);
                var rec = Progress.Record(P, m.id);
                // ◆ is in Chakra Petch (★ is not, and WebGL has no system font to fall back on).
                string stars = UIKit.Colored(new string('◆', rec.stage), Palette.Barrel) + UIKit.Colored(new string('◆', 3 - rec.stage), Palette.UiDim);
                string head = UIKit.Colored(m.name, open ? Palette.Hazard : Palette.UiDim) + "   " + UIKit.Colored(m.setting.ToUpperInvariant(), Palette.UiDim) +
                              "   " + stars + (rec.bestNight > 0 ? "   best night " + rec.bestNight : "");
                string body = open ? m.description : "Locked: clear Stage I (survive night 7) on " + Db.Map(m.unlockAfter).name + ".";
                var weapons = new List<string>();
                foreach (var w in Db.Weapons) if (w.unlock.StartsWith("map:" + m.id + ":")) weapons.Add(w.name);
                if (weapons.Count > 0) body += "\n" + UIKit.Colored("Stage rewards: " + string.Join(", ", weapons.ToArray()), Palette.UiDim);
                var id = m.id;
                Row(head, body, l.mapId == m.id, open, () => { l.mapId = id; Refresh(); }, 130f);
            }
            Section("DIFFICULTY");
            foreach (var d in Db.Difficulties)
            {
                var id = d.id;
                string head = UIKit.Colored(d.name, l.difficultyId == d.id ? Palette.Hazard : Palette.Ui) + "   " + UIKit.Colored("score ×" + d.scoreMult.ToString("0.0#"), Palette.Barrel);
                Row(head, d.description, l.difficultyId == d.id, true, () => { l.difficultyId = id; Refresh(); }, 96f);
            }
        }

        void BuildEngineer()
        {
            Section("CLASS");
            foreach (var c in Db.Classes)
            {
                var id = c.id;
                string head = UIKit.Colored(c.name, Palette.Hex(c.color)) + "   " + UIKit.Colored(c.role + " · " + c.weaponType, Palette.UiDim);
                string body = c.description + "\n" + UIKit.Colored("HP " + c.maxHp + " · Speed " + c.moveSpeed + " · Armour " + c.armor + " · Ability: " + c.abilityName, Palette.UiDim) +
                              "\n" + UIKit.Colored(c.abilityDescription, Palette.Ui);
                Row(head, body, l.classId == c.id, true, () =>
                {
                    l.classId = id;
                    var w = Db.Weapon(l.weaponId);
                    if (w == null || w.classId != id) l.weaponId = Db.Class(id).startingWeapon;
                    Refresh();
                }, 150f);
            }
            Section("WEAPON  ·  core samples: " + P.coreSamples);
            foreach (var w in Db.WeaponsFor(l.classId))
            {
                var weapon = w;
                bool open = Progress.IsWeaponUnlocked(P, w);
                int level = Progress.WeaponLevelOf(P, w.id);
                int cost = WeaponUpgrades.CostToUpgrade(level, Db.RarityOrder(w.rarity));
                string head = UIKit.Colored(w.name, open ? Palette.Rarity(w.rarity) : Palette.UiDim) + "   " + UIKit.RarityName(w.rarity) +
                              (open ? "   " + UIKit.Colored("Lv " + level + "/" + WeaponUpgrades.MaxLevel, Palette.Hazard) : "");
                string stats = w.attack.ToUpperInvariant() + " · dmg " + w.damage + " · " + w.rate + "/s · range " + w.range +
                               (w.projectiles > 1 ? " · ×" + w.projectiles : "") + (w.pierce > 0 ? " · pierce " + w.pierce : "") + (w.radius > 0 ? " · area " + w.radius + " m" : "");
                string body = w.description + "\n" + UIKit.Colored(stats, Palette.UiDim) + (open ? "" : "\n" + UIKit.Colored("Unlock: " + UnlockText(w), Palette.Hurt));
                var row = Row(head, body, l.weaponId == w.id, open, () => { l.weaponId = weapon.id; Refresh(); }, 140f);
                if (open && cost > 0)
                {
                    var up = UIKit.Button(row, "Upgrade · " + cost + " core", () =>
                    {
                        if (Progress.TryUpgradeWeapon(P, Db, weapon.id)) { AudioService.Ui("sfx_upgrade"); Game.SaveProfile(); Refresh(); }
                        else { AudioService.Ui("sfx_buy_fail"); Ui.Toast("Not enough core samples. Earn them by surviving nights, contracts and the wheel."); }
                    }, P.coreSamples >= cost ? UIKit.Style.Normal : UIKit.Style.Ghost, 20);
                    UIKit.Place((RectTransform)up.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(260f, 52f));
                }
            }
        }

        void BuildPerks()
        {
            Section("PERKS · choose up to " + Progress.MaxPerks + " · " + Progress.UnlockedPerkCount(P, Db) + " of " + Db.Perks.Count + " unlocked");
            foreach (var group in new[] { "starter", "wheel", "achievement" })
            {
                Section(group == "starter" ? "Starter perks" : group == "wheel" ? "Reward wheel perks" : "Achievement perks");
                foreach (var perk in Db.Perks)
                {
                    if (perk.source != group) continue;
                    var p = perk;
                    bool open = Progress.IsPerkUnlocked(P, perk);
                    bool on = l.perks.Contains(perk.id);
                    string head = (on ? UIKit.Colored("■ ", Palette.Heal) : "") + UIKit.Colored(perk.name, open ? Palette.Rarity(perk.rarity) : Palette.UiDim) + "   " +
                                  UIKit.RarityName(perk.rarity) + "   " + UIKit.Colored(perk.category, Palette.UiDim);
                    string body = perk.description + (open ? "\n" + UIKit.Colored(perk.flavor, Palette.UiDim) : "\n" + UIKit.Colored("Unlock: " + PerkUnlockText(perk), Palette.Hurt));
                    Row(head, body, on, open, () =>
                    {
                        if (l.perks.Contains(p.id)) l.perks.Remove(p.id);
                        else if (l.perks.Count >= Progress.MaxPerks) { Ui.Toast("You can only take 4 perks. Remove one first."); AudioService.Ui("sfx_buy_fail"); return; }
                        else l.perks.Add(p.id);
                        Refresh();
                    }, 110f);
                }
            }
        }

        void BuildMutators()
        {
            var diff = Db.Difficulty(l.difficultyId);
            Section(diff.allMutators ? "BLOWOUT DIFFICULTY: ALL 19 MUTATORS ARE ON" : "MUTATORS · make the run harder for a higher score");
            foreach (var m in Db.Mutators)
            {
                var mu = m;
                bool open = Progress.IsMutatorUnlocked(P, m);
                bool on = diff.allMutators || l.mutators.Contains(m.id);
                string head = (on ? UIKit.Colored("■ ", Palette.Heal) : "") + UIKit.Colored(m.name, open ? Palette.Rarity(m.rarity) : Palette.UiDim) + "   " +
                              UIKit.RarityName(m.rarity) + "   " + UIKit.Colored(m.group, Palette.UiDim) + "   " +
                              UIKit.Colored("score ×" + m.scoreMult.ToString("0.0#"), m.scoreMult >= 1f ? Palette.Barrel : Palette.Hurt);
                string body = m.description + (open ? "" : "\n" + UIKit.Colored("Unlock: " + MutatorUnlockText(m), Palette.Hurt));
                Row(head, body, on, open && !diff.allMutators, () =>
                {
                    if (l.mutators.Contains(mu.id)) l.mutators.Remove(mu.id); else l.mutators.Add(mu.id);
                    Refresh();
                }, 96f);
            }
        }

        // ---- helpers --------------------------------------------------------------------

        void Section(string text)
        {
            var t = UIKit.Label(content, text, 22, Palette.Hazard, TextAnchor.LowerLeft, UIKit.BoldFont);
            UIKit.Size(t, -1, 44);
        }

        RectTransform Row(string head, string body, bool selected, bool enabled, UnityEngine.Events.UnityAction onClick, float height)
        {
            var bg = UIKit.Panel(content, selected ? new Color(1f, 0.42f, 0.07f, 0.28f) : new Color(1f, 1f, 1f, enabled ? 0.05f : 0.02f));
            UIKit.Size(bg, -1, height);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.interactable = enabled;
            btn.onClick.AddListener(onClick);
            bg.gameObject.AddComponent<UiButtonFx>();
            var h = UIKit.Label(bg.transform, head, 24, Palette.Ui, TextAnchor.UpperLeft, UIKit.BoldFont);
            UIKit.Stretch(h.rectTransform, 12f);
            h.rectTransform.offsetMax = new Vector2(-290f, -10f);
            var b = UIKit.Label(bg.transform, body, 19, enabled ? Palette.Ui : Palette.UiDim, TextAnchor.UpperLeft);
            UIKit.Stretch(b.rectTransform, 12f);
            b.rectTransform.offsetMax = new Vector2(-290f, -44f);
            return bg.rectTransform;
        }

        public static string UnlockText(WeaponDef w)
        {
            if (w.unlock == "start") return "Starter weapon";
            if (w.unlock == "wheel") return "Blowout Brilliant or Legendary segments of the reward wheel";
            if (w.unlock.StartsWith("achievement:"))
            {
                var a = Game.Db.Achievement(w.unlock.Substring(12));
                return a != null ? "Achievement \"" + a.name + "\": " + a.description : w.unlock;
            }
            var parts = w.unlock.Split(':');
            if (parts.Length == 3)
            {
                var m = Game.Db.Map(parts[1]);
                int stage = 0;
                int.TryParse(parts[2], out stage);
                return "Clear Stage " + new string('I', stage) + " (survive night " + stage * 7 + ") on " + (m != null ? m.name : parts[1]);
            }
            return w.unlock;
        }

        public static string PerkUnlockText(PerkDef p)
        {
            if (p.source == "wheel") return "Win it on the reward wheel at dawn";
            foreach (var a in Game.Db.Achievements)
                if (a.rewardPerk == p.id) return "Achievement \"" + a.name + "\": " + a.description;
            return "Starter";
        }

        public static string MutatorUnlockText(MutatorDef m)
        {
            if (m.source == "wheel") return "Win it on the reward wheel at dawn";
            foreach (var a in Game.Db.Achievements)
                if (a.rewardMutator == m.id) return "Achievement \"" + a.name + "\": " + a.description;
            return "Starter";
        }

        void RefreshSummary()
        {
            var map = Db.Map(l.mapId);
            var diff = Db.Difficulty(l.difficultyId);
            var cls = Db.Class(l.classId);
            var w = Db.Weapon(l.weaponId);
            var muts = ActiveMutators();
            var sb = new System.Text.StringBuilder();
            sb.Append(UIKit.Colored("FIELD", Palette.Hazard)).Append('\n').Append(map.name).Append(" · ").Append(diff.name).Append("\n\n");
            sb.Append(UIKit.Colored("ENGINEER", Palette.Hazard)).Append('\n').Append(cls.name).Append(" · ").Append(UIKit.Colored(w.name, Palette.Rarity(w.rarity)))
              .Append(" Lv ").Append(Progress.WeaponLevelOf(P, w.id)).Append("\n\n");
            sb.Append(UIKit.Colored("PERKS " + l.perks.Count + "/" + Progress.MaxPerks, Palette.Hazard)).Append('\n');
            if (l.perks.Count == 0) sb.Append(UIKit.Colored("none (choose in the Perks tab)", Palette.UiDim)).Append('\n');
            foreach (var id in l.perks) sb.Append("• ").Append(UIKit.Colored(Db.Perk(id).name, Palette.Rarity(Db.Perk(id).rarity))).Append('\n');
            sb.Append('\n').Append(UIKit.Colored("MUTATORS " + muts.Count, Palette.Hazard)).Append('\n');
            int shown = 0;
            foreach (var m in muts) { if (shown++ < 6) sb.Append("• ").Append(m.name).Append('\n'); }
            if (muts.Count > 6) sb.Append("… and ").Append(muts.Count - 6).Append(" more\n");
            sb.Append('\n').Append(UIKit.Colored("SCORE MULTIPLIER ×" + Scoring.Multiplier(diff, muts).ToString("0.00"), Palette.Barrel));
            summary.text = sb.ToString();
            startHint.text = "Losing a run never costs unlocks, weapon levels or field progress. You can retry from the last dawn.";
        }

        void StartRun()
        {
            P.loadout = l;
            Progress.FixLoadout(P, Db);
            Game.SaveProfile();
            Game.I.StartRun(P.loadout, false);
        }
    }
}
