// Contracts: one side quest per day/night, offered at dawn and paid at the next dawn.
using System;
using System.Collections.Generic;

namespace Rigfall.Core
{
    public enum KillSource { Hero, Tower, MeleeUnit, RangedUnit, Building, Other }

    public static class Quests
    {
        /// <summary>Pick tonight's contract. killable = enemy ids that will appear tonight.</summary>
        public static QuestState Pick(GameDatabase db, int night, bool bossNight, IList<string> killable, Rng rng)
        {
            var options = new List<QuestDef>();
            foreach (var q in db.Quests)
            {
                if (night < q.minNight) continue;
                if (q.type == "bossHero" && !bossNight) continue;
                if (q.type == "killType" && (killable == null || killable.Count == 0)) continue;
                options.Add(q);
            }
            if (options.Count == 0) return new QuestState();
            // Boss nights strongly favour the boss contract.
            var def = bossNight && options.Exists(o => o.type == "bossHero") && rng.Chance(0.6f)
                ? options.Find(o => o.type == "bossHero")
                : rng.Pick(options);
            var state = new QuestState
            {
                questId = def.id,
                target = Math.Max(1, (int)Math.Round(def.baseCount + def.perNight * Math.Max(0, night - 1))),
            };
            if (def.type == "killType")
            {
                state.key = killable[rng.Range(0, killable.Count)];
                state.target = Math.Max(3, state.target);
            }
            return state;
        }

        public static string Describe(QuestState q, GameDatabase db)
        {
            var def = db.Quest(q.questId);
            if (def == null) return "";
            string enemy = "";
            if (!string.IsNullOrEmpty(q.key)) { var e = db.Enemy(q.key); enemy = e == null ? q.key : e.name + "s"; }
            return def.description.Replace("{n}", q.target.ToString()).Replace("{enemy}", enemy);
        }

        public static string Type(QuestState q, GameDatabase db)
        {
            var def = db.Quest(q.questId);
            return def == null ? "" : def.type;
        }

        public static void OnKill(QuestState q, GameDatabase db, EnemyDef enemy, KillSource source)
        {
            if (q == null || q.failed) return;
            switch (Type(q, db))
            {
                case "heroKills": if (source == KillSource.Hero) q.progress++; break;
                case "towerKills": if (source == KillSource.Tower) q.progress++; break;
                case "unitKills": if (source == KillSource.MeleeUnit || source == KillSource.RangedUnit) q.progress++; break;
                case "killType": if (enemy != null && enemy.id == q.key) q.progress++; break;
                case "bossHero": if (enemy != null && enemy.boss && source == KillSource.Hero) q.progress++; break;
            }
        }

        public static void OnBuildAction(QuestState q, GameDatabase db)
        {
            if (q != null && Type(q, db) == "buildActions") q.progress++;
        }

        public static void OnBuildingLost(QuestState q, GameDatabase db, string buildingKind)
        {
            if (q != null && buildingKind == "wall" && Type(q, db) == "noWallLost") q.failed = true;
        }

        public static void OnRigDamaged(QuestState q, GameDatabase db)
        {
            if (q != null && Type(q, db) == "rigUntouched") q.failed = true;
        }

        public static void OnUnitLost(QuestState q, GameDatabase db)
        {
            if (q != null && Type(q, db) == "noUnitLost") q.failed = true;
        }

        public static void OnNightStart(QuestState q, GameDatabase db, int barrels)
        {
            if (q == null) return;
            string t = Type(q, db);
            if (t == "holdBarrels") { if (barrels >= q.target) q.progress = q.target; else q.failed = true; }
            if (t == "buildActions" && q.progress < q.target) q.failed = true; // the build window is the day
        }

        /// <summary>Is the contract fulfilled at dawn?</summary>
        public static bool IsComplete(QuestState q, GameDatabase db)
        {
            if (q == null || string.IsNullOrEmpty(q.questId) || q.failed) return false;
            switch (Type(q, db))
            {
                case "noWallLost":
                case "rigUntouched":
                case "noUnitLost":
                    return true; // survived the night without failing
                default:
                    return q.progress >= q.target;
            }
        }

        public static string ProgressText(QuestState q, GameDatabase db)
        {
            if (q == null || string.IsNullOrEmpty(q.questId)) return "";
            if (q.failed) return "Failed";
            switch (Type(q, db))
            {
                case "noWallLost":
                case "rigUntouched":
                case "noUnitLost":
                    return "Holding";
                default:
                    return Math.Min(q.progress, q.target) + " / " + q.target;
            }
        }
    }
}
