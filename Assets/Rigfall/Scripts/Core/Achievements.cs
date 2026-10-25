// Achievement conditions. The runtime fills an AchievementContext with what is true right
// now (current run, current buildings) and asks which locked achievements are now earned.
using System;
using System.Collections.Generic;

namespace Rigfall.Core
{
    public sealed class AchievementContext
    {
        public Profile profile;
        public RunStats run;                        // null when not in a run
        public string classId = "", setting = "";
        public int difficultyOrder;
        public int mutatorCount;
        public int perkCount;
        public int barrels;
        public int unitsAlive;
        public Dictionary<string, int> buildingCounts = new Dictionary<string, int>();
        public GameDatabase db;
    }

    public static class Achievements
    {
        static readonly string[] UnitBuildings = { "crew-camp", "perforation-range", "company-office" };

        public static bool IsMet(AchievementDef a, AchievementContext c)
        {
            var run = c.run;
            switch (a.type)
            {
                case "tutorial":
                    return c.profile.tutorialDone;
                case "lifetime":
                    return c.profile.stats.Get(a.key) >= a.value;
                case "stageAllMaps":
                    foreach (var m in c.db.Maps)
                        if (Progress.Record(c.profile, m.id).stage < a.value) return false;
                    return true;
                case "classesNight":
                    return c.profile.classesNight3.Count >= c.db.Classes.Count;
            }

            if (run == null || !Filters(a, c)) return false;
            switch (a.type)
            {
                case "nightsRun": return run.nightsSurvived >= a.value;
                case "bossKilled":
                    if (string.IsNullOrEmpty(a.key)) return run.bossesKilled.Count >= a.value;
                    return run.bossesKilled.Contains(a.key);
                case "barrelsHeld": return Math.Max(run.maxBarrels, c.barrels) >= a.value;
                case "buildingCount":
                    int n;
                    return c.buildingCounts.TryGetValue(a.key, out n) && n >= a.value;
                case "noBuildingLost": return run.bestCleanNight >= a.value;
                case "unitsAlive": return Math.Max(run.maxUnitsAlive, c.unitsAlive) >= a.value;
                case "unitBuildingsAll":
                    foreach (var b in UnitBuildings) if (!run.unitBuildingKinds.Contains(b)) return false;
                    return true;
                case "wheelRarity": return run.wheelRarities.Contains(a.key);
                case "loadoutPerks": return c.perkCount >= a.value;
                default: return false;
            }
        }

        static bool Filters(AchievementDef a, AchievementContext c)
        {
            if (a.difficulty >= 0 && c.difficultyOrder < a.difficulty) return false;
            if (!string.IsNullOrEmpty(a.classId) && a.classId != c.classId) return false;
            if (!string.IsNullOrEmpty(a.setting) && a.setting != c.setting) return false;
            if (a.mutators > 0 && c.mutatorCount < a.mutators) return false;
            return true;
        }

        /// <summary>
        /// Unlock every achievement whose condition is now met, apply its rewards to the
        /// profile and return the newly unlocked ones (for the toast).
        /// </summary>
        public static List<AchievementDef> Check(AchievementContext c)
        {
            var unlocked = new List<AchievementDef>();
            foreach (var a in c.db.Achievements)
            {
                if (c.profile.achievements.Contains(a.id)) continue;
                if (!IsMet(a, c)) continue;
                c.profile.achievements.Add(a.id);
                Grant(a, c.profile);
                unlocked.Add(a);
            }
            return unlocked;
        }

        public static void Grant(AchievementDef a, Profile p)
        {
            if (!string.IsNullOrEmpty(a.rewardPerk) && !p.perks.Contains(a.rewardPerk)) p.perks.Add(a.rewardPerk);
            if (!string.IsNullOrEmpty(a.rewardWeapon) && !p.weapons.Contains(a.rewardWeapon)) p.weapons.Add(a.rewardWeapon);
            if (!string.IsNullOrEmpty(a.rewardMutator) && !p.mutators.Contains(a.rewardMutator)) p.mutators.Add(a.rewardMutator);
            p.coreSamples += Math.Max(0, a.rewardCore);
        }

        public static string RewardText(AchievementDef a, GameDatabase db)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(a.rewardPerk)) { var x = db.Perk(a.rewardPerk); if (x != null) parts.Add("Perk: " + x.name); }
            if (!string.IsNullOrEmpty(a.rewardWeapon)) { var x = db.Weapon(a.rewardWeapon); if (x != null) parts.Add("Weapon: " + x.name); }
            if (!string.IsNullOrEmpty(a.rewardMutator)) { var x = db.Mutator(a.rewardMutator); if (x != null) parts.Add("Mutator: " + x.name); }
            if (a.rewardCore > 0) parts.Add("+" + a.rewardCore + " core samples");
            return string.Join(" · ", parts.ToArray());
        }
    }
}
