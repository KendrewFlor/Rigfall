// Extras: the Field Manual (enemies, units, buildings, perks, mutators, weapons), a glossary
// of real petroleum engineering terms, a jukebox, records and credits.
using Rigfall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Rigfall.Runtime
{
    public sealed class ExtrasScreen : UiScreen
    {
        enum Tab { Enemies, Crews, Buildings, Weapons, Perks, Mutators, Glossary, Jukebox, Records, Credits }
        Tab tab = Tab.Enemies;
        RectTransform tabs, content;

        protected override void Build(RectTransform root)
        {
            var card = Card(root, 1760f, 960f, Palette.Friendly);
            var title = UIKit.Title(card, "FIELD MANUAL & EXTRAS", 44, Palette.Friendly, TextAnchor.UpperLeft);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(1000f, 60f));
            var back = UIKit.Button(card, "Back", () => Game.UI.Close(this), UIKit.Style.Ghost, 22);
            UIKit.Place((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -22f), new Vector2(160f, 50f));
            FirstSelected = back;

            var tabRow = UIKit.HStack(card, 8, 0, "Tabs");
            tabs = (RectTransform)tabRow.transform;
            UIKit.Place(tabs, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -90f), new Vector2(1704f, 52f));
            tabRow.childForceExpandWidth = true;

            var area = UIKit.Rect("Area", card);
            UIKit.Stretch(area, 28f);
            area.offsetMax = new Vector2(-28f, -156f);
            content = UIKit.ScrollList(area, 8f);
            UIKit.Stretch((RectTransform)content.parent);
            Refresh();
        }

        void Refresh()
        {
            foreach (Transform c in tabs) Object.Destroy(c.gameObject);
            foreach (Tab t in System.Enum.GetValues(typeof(Tab)))
            {
                var tt = t;
                var b = UIKit.Button(tabs, t.ToString(), () => { tab = tt; Refresh(); }, tab == t ? UIKit.Style.Primary : UIKit.Style.Normal, 20);
                UIKit.Size(b, -1, 48, 1);
            }
            foreach (Transform c in content) Object.Destroy(c.gameObject);
            var db = Game.Db;
            switch (tab)
            {
                case Tab.Enemies:
                    foreach (var e in db.Enemies)
                    {
                        if (e.threat == 0 && !e.boss) continue;
                        string tags = (e.boss ? "BOSS · " : "") + (e.flying ? "Flying · " : "") + (e.ranged ? "Ranged · " : "") + "HP " + e.hp + " · Dmg " + e.damage + " · Speed " + e.speed + (e.armor > 0 ? " · Armour " + e.armor : "");
                        Entry(UIKit.Colored(e.name, e.boss ? Palette.Danger : Palette.Hazard), e.description + "\n" + UIKit.Colored(tags, Palette.UiDim), 110);
                    }
                    Entry(UIKit.Colored("How the night grows", Palette.Friendly),
                        "A new enemy type joins every 3 nights (in each field's own order). Bosses arrive every 7th night, rotating through the four. Every night enemies gain a little health and damage and the wave gets a little bigger, without end.", 110);
                    break;
                case Tab.Crews:
                    foreach (var u in db.Units)
                    {
                        var home = db.Building(u.building);
                        string tags = home.name + " · " + u.role + " · HP " + u.hp + " · Dmg " + u.damage + " · Range " + u.attackRange + (u.canHitAir ? " · hits air" : "");
                        Entry(UIKit.Colored(u.name, Palette.Heal), u.description + "\n" + UIKit.Colored(tags, Palette.UiDim), 100);
                    }
                    break;
                case Tab.Buildings:
                    foreach (var b in db.Buildings)
                    {
                        var sb = new System.Text.StringBuilder(b.description).Append('\n');
                        for (int t = 0; t < b.tiers.Length; t++)
                        {
                            sb.Append(UIKit.Colored("Tier " + t + ": ", Palette.UiDim));
                            for (int i = 0; i < b.tiers[t].options.Length; i++)
                            {
                                var o = b.tiers[t].options[i];
                                if (i > 0) sb.Append(UIKit.Colored(" or ", Palette.UiDim));
                                sb.Append(o.name).Append(UIKit.Colored(" (" + o.cost + " bbl)", Palette.Barrel));
                            }
                            sb.Append('\n');
                        }
                        Entry(UIKit.Colored(b.name, Palette.Hazard), sb.ToString(), 60 + 30 * b.tiers.Length + 30);
                    }
                    break;
                case Tab.Weapons:
                    foreach (var w in db.Weapons)
                    {
                        bool open = Progress.IsWeaponUnlocked(Game.Profile, w);
                        Entry(UIKit.Colored(w.name, Palette.Rarity(w.rarity)) + "   " + UIKit.RarityName(w.rarity) + "   " + UIKit.Colored(db.Class(w.classId).name, Palette.UiDim),
                            w.description + "\n" + UIKit.Colored(open ? "Unlocked" : "Unlock: " + LoadoutScreen.UnlockText(w), open ? Palette.Heal : Palette.UiDim), 100);
                    }
                    break;
                case Tab.Perks:
                    foreach (var p in db.Perks)
                        Entry(UIKit.Colored(p.name, Palette.Rarity(p.rarity)) + "   " + UIKit.RarityName(p.rarity) + "   " + UIKit.Colored(p.category, Palette.UiDim),
                            p.description + "\n" + UIKit.Colored(Progress.IsPerkUnlocked(Game.Profile, p) ? p.flavor : "Unlock: " + LoadoutScreen.PerkUnlockText(p), Palette.UiDim), 100);
                    break;
                case Tab.Mutators:
                    foreach (var m in db.Mutators)
                        Entry(UIKit.Colored(m.name, Palette.Rarity(m.rarity)) + "   " + UIKit.Colored("score ×" + m.scoreMult.ToString("0.0#"), Palette.Barrel),
                            m.description + "\n" + UIKit.Colored(Progress.IsMutatorUnlocked(Game.Profile, m) ? "Unlocked" : "Unlock: " + LoadoutScreen.MutatorUnlockText(m), Palette.UiDim), 96);
                    break;
                case Tab.Glossary:
                    Entry(UIKit.Colored("Real oilfield terms behind the game", Palette.Friendly), "Every name in Rigfall comes from real petroleum engineering. Here's what they mean.", 80);
                    foreach (var g in db.Glossary) Entry(UIKit.Colored(g.term, Palette.Hazard), g.text, 96);
                    break;
                case Tab.Jukebox:
                    Entry(UIKit.Colored("Jukebox", Palette.Friendly), "All music is synthesised for Rigfall. Click a track to listen. To keep one playing everywhere, choose it under Settings > Audio > Music track.", 80);
                    foreach (var m in db.Music)
                    {
                        var id = m.id;
                        var b = UIKit.Button(content, (AudioService.CurrentTrack == m.id ? "▶  " : "") + m.title + "  ·  " + m.mood, () => { AudioService.Preview(id); Refresh(); },
                            AudioService.CurrentTrack == m.id ? UIKit.Style.Primary : UIKit.Style.Normal, 22);
                        UIKit.Size(b, -1, 58);
                    }
                    break;
                case Tab.Records:
                {
                    var p = Game.Profile;
                    var s = p.stats;
                    Entry(UIKit.Colored("Lifetime", Palette.Hazard),
                        "Runs " + s.runs + " · Nights survived " + s.nights + " · Best night " + s.bestNight + " · Defeats " + s.defeats + "\n" +
                        "Enemies defeated " + s.kills + " (hero " + s.heroKills + ", towers " + s.towerKills + ", units " + s.unitKills + ", flying " + s.flyingKills + ", bosses " + s.bossKills + ")\n" +
                        "Buildings built " + s.buildingsBuilt + " · Barrels earned " + s.barrelsEarned + " · Wheel spins " + s.wheelSpins + " · Core samples " + p.coreSamples, 130);
                    foreach (var m in db.Maps)
                    {
                        var r = Progress.Record(p, m.id);
                        Entry(UIKit.Colored(m.name, Palette.Hazard), "Stage " + r.stage + "/3 · Best night " + r.bestNight + (string.IsNullOrEmpty(r.bestDifficulty) ? "" : " (" + db.Difficulty(r.bestDifficulty).name + ")") + " · Best score " + r.bestScore.ToString("N0"), 76);
                    }
                    if (p.scores.Count > 0)
                    {
                        var sb = new System.Text.StringBuilder();
                        int rank = 1;
                        foreach (var e in p.scores)
                        {
                            if (rank > 10) break;
                            sb.Append(rank++).Append(". ").Append(e.score.ToString("N0")).Append("  ·  ").Append(db.Map(e.mapId).name).Append(" · night ").Append(e.night)
                              .Append(" · ").Append(db.Difficulty(e.difficultyId) != null ? db.Difficulty(e.difficultyId).name : e.difficultyId)
                              .Append(" · ").Append(db.Class(e.classId) != null ? db.Class(e.classId).name : e.classId).Append('\n');
                        }
                        Entry(UIKit.Colored("Top runs", Palette.Hazard), sb.ToString(), 60 + 28 * Mathf.Min(10, p.scores.Count));
                    }
                    break;
                }
                case Tab.Credits:
                    Entry(UIKit.Colored("Rigfall", Palette.Accent), "Design, code and content by Kendrew Flor S. Arrabe. A petroleum engineering take on the build-by-day, defend-by-night formula.", 90);
                    Entry(UIKit.Colored("Made with", Palette.Hazard), "Unity 6 (C#) for the game, Blender (Python) for the low-poly models, Python for the synthesised music and sound effects.", 90);
                    Entry(UIKit.Colored("Fonts", Palette.Hazard), "Saira Stencil One and Chakra Petch, both under the SIL Open Font License 1.1.", 80);
                    Entry(UIKit.Colored("Inspiration", Palette.Hazard), "Inspired by minimalist strategy games such as Thronefall. Rigfall is an independent fan project and is not affiliated with them.", 90);
                    break;
            }
        }

        void Entry(string head, string body, float height)
        {
            var bg = UIKit.Panel(content, new Color(1f, 1f, 1f, 0.04f));
            UIKit.Size(bg, -1, height);
            var h = UIKit.Label(bg.transform, head, 24, Palette.Ui, TextAnchor.UpperLeft, UIKit.BoldFont);
            UIKit.Stretch(h.rectTransform, 12f);
            var b = UIKit.Label(bg.transform, body, 19, Palette.Ui, TextAnchor.UpperLeft);
            UIKit.Stretch(b.rectTransform, 12f);
            b.rectTransform.offsetMax = new Vector2(-12f, -42f);
        }
    }
}
