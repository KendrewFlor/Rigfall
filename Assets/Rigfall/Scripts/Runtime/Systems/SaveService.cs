// Saving and loading. Uses PlayerPrefs, which Unity stores in the browser's IndexedDB on
// WebGL and in the registry/plist on desktop. Two separate saves:
//   * the Profile (permanent unlocks, records, settings), never lost when a run fails;
//   * the RunSnapshot taken at every dawn, used for "Continue" and "Retry from last dawn".
// Loaded data is validated by Progress.Sanitize before use.
using System;
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public static class SaveService
    {
        const string ProfileKey = "rigfall.profile.v1";
        const string RunKey = "rigfall.run.v1";
        const string RunStartKey = "rigfall.runstart.v1";
        const int MaxBytes = 512 * 1024;

        public static Profile LoadProfile(GameDatabase db)
        {
            Profile p = null;
            try
            {
                string json = PlayerPrefs.GetString(ProfileKey, "");
                if (!string.IsNullOrEmpty(json) && json.Length < MaxBytes) p = JsonUtility.FromJson<Profile>(json);
            }
            catch (Exception e) { Debug.LogWarning("Rigfall: profile could not be read, starting fresh. " + e.Message); }
            if (p == null) p = new Profile();
            Progress.Sanitize(p, db);
            return p;
        }

        public static void SaveProfile(Profile p)
        {
            try
            {
                PlayerPrefs.SetString(ProfileKey, JsonUtility.ToJson(p));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("Rigfall: could not save profile. " + e.Message); }
        }

        public static void SaveRun(RunSnapshot s, bool isRunStart)
        {
            try
            {
                string json = JsonUtility.ToJson(s);
                PlayerPrefs.SetString(RunKey, json);
                if (isRunStart) PlayerPrefs.SetString(RunStartKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("Rigfall: could not save run. " + e.Message); }
        }

        public static RunSnapshot LoadRun(GameDatabase db) => Read(RunKey, db);
        public static RunSnapshot LoadRunStart(GameDatabase db) => Read(RunStartKey, db);

        static RunSnapshot Read(string key, GameDatabase db)
        {
            try
            {
                string json = PlayerPrefs.GetString(key, "");
                if (string.IsNullOrEmpty(json) || json.Length > MaxBytes) return null;
                var s = JsonUtility.FromJson<RunSnapshot>(json);
                return IsUsable(s, db) ? s : null;
            }
            catch (Exception) { return null; }
        }

        /// <summary>Reject snapshots that reference content that no longer exists or has impossible values.</summary>
        static bool IsUsable(RunSnapshot s, GameDatabase db)
        {
            if (s == null || !s.valid) return false;
            if (db.Map(s.mapId) == null || db.Difficulty(s.difficultyId) == null || db.Class(s.classId) == null) return false;
            var w = db.Weapon(s.weaponId);
            if (w == null || w.classId != s.classId) return false;
            if (s.night < 1 || s.night > 100000 || s.barrels < 0 || s.barrels > 1000000) return false;
            if (s.perks == null) s.perks = new System.Collections.Generic.List<string>();
            if (s.mutators == null) s.mutators = new System.Collections.Generic.List<string>();
            if (s.blessings == null) s.blessings = new System.Collections.Generic.List<string>();
            if (s.buildings == null) s.buildings = new System.Collections.Generic.List<BuildingState>();
            if (s.quest == null) s.quest = new QuestState();
            if (s.stats == null) s.stats = new RunStats();
            if (s.stats.bossesKilled == null) s.stats.bossesKilled = new System.Collections.Generic.List<string>();
            if (s.stats.wheelRarities == null) s.stats.wheelRarities = new System.Collections.Generic.List<string>();
            if (s.stats.unitBuildingKinds == null) s.stats.unitBuildingKinds = new System.Collections.Generic.List<string>();
            s.perks.RemoveAll(id => db.Perk(id) == null);
            while (s.perks.Count > Progress.MaxPerks) s.perks.RemoveAt(s.perks.Count - 1);
            s.mutators.RemoveAll(id => db.Mutator(id) == null);
            s.blessings.RemoveAll(id => !db.Wheel.Exists(x => x.id == id));
            var map = db.Map(s.mapId);
            s.buildings.RemoveAll(b => b == null || b.path == null || (b.slotId != "rig" && Array.Find(map.slots, x => x.id == b.slotId) == null)
                || !BuildingRules.IsValidPath(db.Building(b.buildingId), b.path));
            s.freeUpgrades = Mathf.Clamp(s.freeUpgrades, 0, 20);
            return true;
        }

        public static bool HasRun(GameDatabase db) => LoadRun(db) != null;

        public static void ClearRun()
        {
            PlayerPrefs.DeleteKey(RunKey);
            PlayerPrefs.DeleteKey(RunStartKey);
            PlayerPrefs.Save();
        }

        public static void WipeEverything()
        {
            PlayerPrefs.DeleteKey(ProfileKey);
            ClearRun();
        }
    }
}
