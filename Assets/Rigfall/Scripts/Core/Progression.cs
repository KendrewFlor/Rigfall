// Permanent progress (Profile) and the in-run checkpoint (RunSnapshot).
//
// Losing a run never touches the Profile: unlocks, weapon levels, field stages and
// achievements are kept. Only the run itself (barrels, buildings, the current night) is
// lost, and even that can be resumed from the last dawn because a RunSnapshot is saved at
// the start of every day.
using System;
using System.Collections.Generic;

namespace Rigfall.Core
{
    [Serializable]
    public class WeaponLevel
    {
        public string id = "";
        public int level;
    }

    [Serializable]
    public class MapRecord
    {
        public string id = "";
        public int stage;            // 0 = not cleared, 1 = night 7 boss beaten, 2 = night 14, 3 = night 21
        public int bestNight;
        public int bestScore;
        public string bestDifficulty = "";
    }

    [Serializable]
    public class LifetimeStats
    {
        public int kills, heroKills, towerKills, unitKills, rangedUnitKills, flyingKills, bossKills;
        public int nights, runs, defeats, wheelSpins, buildingsBuilt, barrelsEarned, bestNight;

        public int Get(string key)
        {
            switch (key)
            {
                case "kills": return kills;
                case "heroKills": return heroKills;
                case "towerKills": return towerKills;
                case "unitKills": return unitKills;
                case "rangedUnitKills": return rangedUnitKills;
                case "flyingKills": return flyingKills;
                case "bossKills": return bossKills;
                case "nights": return nights;
                default: return 0;
            }
        }
    }

    [Serializable]
    public class Loadout
    {
        public string mapId = "permian-pad";
        public string difficultyId = "normal";
        public string classId = "driller";
        public string weaponId = "tricone-bit";
        public List<string> perks = new List<string>();
        public List<string> mutators = new List<string>();
    }

    [Serializable]
    public class GameSettings
    {
        // Graphics: 0 = Low, 1 = Medium, 2 = High
        public int graphicsQuality = 1;
        public bool showDamageNumbers = true;
        public float screenShake = 0.7f;
        public float cameraZoom = 1f;
        public int fpsCap = 60;
        public float uiScale = 1f;
        public bool highContrastPrompts;
        // Sound
        public float masterVolume = 0.8f, musicVolume = 0.55f, sfxVolume = 0.8f, ambienceVolume = 0.5f, uiVolume = 0.7f;
        public string musicTrack = "";        // "" = automatic (per field and phase)
        public bool dynamicMusic = true;      // music intensifies with the wave
        public bool muteWhenUnfocused = true;
        public bool spatialAudio = true;
        // Gameplay
        public bool autoAttack;
        public bool holdToBuild = true;
        public bool showRanges = true;
        public bool tutorialHints = true;
    }

    [Serializable]
    public class ScoreEntry
    {
        public string mapId = "", difficultyId = "", classId = "";
        public int night, score;
    }

    [Serializable]
    public class Profile
    {
        public int version = 1;
        public int coreSamples;
        public List<string> perks = new List<string>();
        public List<string> mutators = new List<string>();
        public List<string> weapons = new List<string>();
        public List<string> achievements = new List<string>();
        public List<WeaponLevel> weaponLevels = new List<WeaponLevel>();
        public List<MapRecord> maps = new List<MapRecord>();
        public LifetimeStats stats = new LifetimeStats();
        public List<string> classesNight3 = new List<string>();
        public bool tutorialDone;
        public bool seenIntro;
        public Loadout loadout = new Loadout();
        public GameSettings settings = new GameSettings();
        public List<ScoreEntry> scores = new List<ScoreEntry>();
    }

    [Serializable]
    public class BuildingState
    {
        public string slotId = "";
        public string buildingId = "";
        public List<string> path = new List<string>(); // option id chosen at each tier
    }

    [Serializable]
    public class RunStats
    {
        public int nightsSurvived, kills, heroKills, towerKills, unitKills, rangedUnitKills, flyingKills, bossKills;
        public int maxBarrels, maxUnitsAlive, buildingsBuilt, bestCleanNight, barrelsEarned;
        public List<string> bossesKilled = new List<string>();
        public List<string> wheelRarities = new List<string>();
        public List<string> unitBuildingKinds = new List<string>();
    }

    [Serializable]
    public class QuestState
    {
        public string questId = "";
        public string key = "";
        public int target;
        public int progress;
        public bool failed;
    }

    [Serializable]
    public class RunSnapshot
    {
        public int version = 1;
        public bool valid;
        public string mapId = "", difficultyId = "", classId = "", weaponId = "";
        public List<string> perks = new List<string>();
        public List<string> mutators = new List<string>();
        public List<string> blessings = new List<string>();   // wheel reward ids with run-long effects
        public int night = 1;                                 // the night this day leads into
        public int barrels;
        public int score;
        public int freeUpgrades;
        public bool reliefUsed;
        public string seed = "";
        public List<BuildingState> buildings = new List<BuildingState>();
        public QuestState quest = new QuestState();
        public RunStats stats = new RunStats();
    }

    /// <summary>Rules for reading and changing the Profile.</summary>
    public static class Progress
    {
        public const int MaxPerks = 4;
        public const int MaxScores = 20;

        public static Profile NewProfile(GameDatabase db)
        {
            var p = new Profile();
            Sanitize(p, db);
            return p;
        }

        /// <summary>
        /// Make a loaded profile safe to use: unknown ids dropped, numbers clamped, nulls
        /// replaced. Save files are treated as untrusted input.
        /// </summary>
        public static void Sanitize(Profile p, GameDatabase db)
        {
            if (p.perks == null) p.perks = new List<string>();
            if (p.mutators == null) p.mutators = new List<string>();
            if (p.weapons == null) p.weapons = new List<string>();
            if (p.achievements == null) p.achievements = new List<string>();
            if (p.weaponLevels == null) p.weaponLevels = new List<WeaponLevel>();
            if (p.maps == null) p.maps = new List<MapRecord>();
            if (p.stats == null) p.stats = new LifetimeStats();
            if (p.classesNight3 == null) p.classesNight3 = new List<string>();
            if (p.loadout == null) p.loadout = new Loadout();
            if (p.settings == null) p.settings = new GameSettings();
            if (p.scores == null) p.scores = new List<ScoreEntry>();

            Filter(p.perks, id => db.Perk(id) != null);
            Filter(p.mutators, id => db.Mutator(id) != null);
            Filter(p.weapons, id => db.Weapon(id) != null);
            Filter(p.achievements, id => db.Achievement(id) != null);
            Filter(p.classesNight3, id => db.Class(id) != null);
            p.weaponLevels.RemoveAll(w => w == null || db.Weapon(w.id) == null);
            foreach (var w in p.weaponLevels) w.level = Clamp(w.level, 0, WeaponUpgrades.MaxLevel);
            p.maps.RemoveAll(m => m == null || db.Map(m.id) == null);
            foreach (var m in p.maps)
            {
                m.stage = Clamp(m.stage, 0, 3);
                m.bestNight = Clamp(m.bestNight, 0, 100000);
                m.bestScore = Clamp(m.bestScore, 0, int.MaxValue);
                if (db.Difficulty(m.bestDifficulty) == null) m.bestDifficulty = "";
            }
            p.scores.RemoveAll(e => e == null || db.Map(e.mapId) == null);
            p.coreSamples = Clamp(p.coreSamples, 0, 10000000);

            var st = p.settings;
            st.graphicsQuality = Clamp(st.graphicsQuality, 0, 2);
            st.masterVolume = Clamp01(st.masterVolume); st.musicVolume = Clamp01(st.musicVolume);
            st.sfxVolume = Clamp01(st.sfxVolume); st.ambienceVolume = Clamp01(st.ambienceVolume); st.uiVolume = Clamp01(st.uiVolume);
            st.screenShake = Clamp01(st.screenShake);
            st.cameraZoom = Math.Max(0.7f, Math.Min(1.4f, st.cameraZoom));
            st.uiScale = Math.Max(0.8f, Math.Min(1.3f, st.uiScale));
            st.fpsCap = st.fpsCap == 30 || st.fpsCap == 60 || st.fpsCap == 120 ? st.fpsCap : 60;
            string track = st.musicTrack;
            if (track == null || (track.Length > 0 && !db.Music.Exists(t => t.id == track))) st.musicTrack = "";

            FixLoadout(p, db);
        }

        static void Filter(List<string> list, Func<string, bool> keep)
        {
            var seen = new HashSet<string>();
            list.RemoveAll(id => string.IsNullOrEmpty(id) || !keep(id) || !seen.Add(id));
        }

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
        static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0 ? 0 : v > 1 ? 1 : v;

        // ---- unlock rules -----------------------------------------------------------------

        public static bool IsPerkUnlocked(Profile p, PerkDef perk) =>
            perk != null && (perk.source == "starter" || p.perks.Contains(perk.id));

        public static bool IsMutatorUnlocked(Profile p, MutatorDef m) =>
            m != null && (m.source == "starter" || p.mutators.Contains(m.id));

        public static bool IsWeaponUnlocked(Profile p, WeaponDef w) =>
            w != null && (w.unlock == "start" || p.weapons.Contains(w.id));

        public static bool IsMapUnlocked(Profile p, MapDef m) =>
            m != null && (string.IsNullOrEmpty(m.unlockAfter) || Record(p, m.unlockAfter).stage >= 1);

        public static MapRecord Record(Profile p, string mapId)
        {
            var r = p.maps.Find(m => m.id == mapId);
            if (r == null) { r = new MapRecord { id = mapId }; p.maps.Add(r); }
            return r;
        }

        public static int WeaponLevelOf(Profile p, string weaponId)
        {
            var w = p.weaponLevels.Find(x => x.id == weaponId);
            return w == null ? 0 : w.level;
        }

        public static bool TryUpgradeWeapon(Profile p, GameDatabase db, string weaponId)
        {
            var def = db.Weapon(weaponId);
            if (def == null || !IsWeaponUnlocked(p, def)) return false;
            int level = WeaponLevelOf(p, weaponId);
            int cost = WeaponUpgrades.CostToUpgrade(level, db.RarityOrder(def.rarity));
            if (cost < 0 || p.coreSamples < cost) return false;
            p.coreSamples -= cost;
            var w = p.weaponLevels.Find(x => x.id == weaponId);
            if (w == null) { w = new WeaponLevel { id = weaponId }; p.weaponLevels.Add(w); }
            w.level = level + 1;
            return true;
        }

        public static int UnlockedPerkCount(Profile p, GameDatabase db)
        {
            int n = 0;
            foreach (var perk in db.Perks) if (IsPerkUnlocked(p, perk)) n++;
            return n;
        }

        /// <summary>Keep the saved loadout valid against what the profile actually owns.</summary>
        public static void FixLoadout(Profile p, GameDatabase db)
        {
            var l = p.loadout;
            if (l.perks == null) l.perks = new List<string>();
            if (l.mutators == null) l.mutators = new List<string>();
            var map = db.Map(l.mapId);
            if (map == null || !IsMapUnlocked(p, map)) l.mapId = db.Maps.Count > 0 ? db.Maps[0].id : "";
            if (db.Difficulty(l.difficultyId) == null) l.difficultyId = "normal";
            var cls = db.Class(l.classId);
            if (cls == null) { cls = db.Classes.Count > 0 ? db.Classes[0] : null; l.classId = cls == null ? "" : cls.id; }
            var weapon = db.Weapon(l.weaponId);
            if (cls != null && (weapon == null || weapon.classId != cls.id || !IsWeaponUnlocked(p, weapon))) l.weaponId = cls.startingWeapon;
            Filter(l.perks, id => IsPerkUnlocked(p, db.Perk(id)));
            while (l.perks.Count > MaxPerks) l.perks.RemoveAt(l.perks.Count - 1);
            Filter(l.mutators, id => IsMutatorUnlocked(p, db.Mutator(id)));
        }

        /// <summary>Mutators actually in effect: the chosen ones, or all 19 on Blowout difficulty.</summary>
        public static List<MutatorDef> ActiveMutators(GameDatabase db, DifficultyDef diff, IList<string> chosen)
        {
            var list = new List<MutatorDef>();
            if (diff != null && diff.allMutators) { list.AddRange(db.Mutators); return list; }
            if (chosen == null) return list;
            foreach (var id in chosen)
            {
                var m = db.Mutator(id);
                if (m != null && !list.Contains(m)) list.Add(m);
            }
            return list;
        }

        /// <summary>
        /// Called each time a night is survived. Updates the field record and clears stages
        /// on nights 7, 14 and 21 (unlocking that field's reward weapons). Returns newly
        /// unlocked weapon ids.
        /// </summary>
        public static List<string> RecordNight(Profile p, GameDatabase db, string mapId, string difficultyId, int night, int score)
        {
            var unlocked = new List<string>();
            var rec = Record(p, mapId);
            if (night > rec.bestNight) { rec.bestNight = night; rec.bestDifficulty = difficultyId; }
            if (score > rec.bestScore) rec.bestScore = score;
            if (night > p.stats.bestNight) p.stats.bestNight = night;
            p.stats.nights++;

            int stage = Math.Min(3, night / 7);
            if (stage > rec.stage)
            {
                for (int s = rec.stage + 1; s <= stage; s++)
                {
                    string tag = "map:" + mapId + ":" + s;
                    foreach (var w in db.Weapons)
                        if (w.unlock == tag && !p.weapons.Contains(w.id)) { p.weapons.Add(w.id); unlocked.Add(w.id); }
                }
                rec.stage = stage;
            }
            return unlocked;
        }

        public static void RecordScore(Profile p, ScoreEntry entry)
        {
            p.scores.Add(entry);
            p.scores.Sort((a, b) => b.score.CompareTo(a.score));
            while (p.scores.Count > MaxScores) p.scores.RemoveAt(p.scores.Count - 1);
        }

        /// <summary>
        /// Unlock something random that the player doesn't own yet, up to a maximum rarity.
        /// Tries the exact rarity first, then lower ones, then anything. Returns the id or "".
        /// kind: "perk" | "mutator" | "weapon".
        /// </summary>
        public static string UnlockRandom(Profile p, GameDatabase db, string kind, string maxRarity, Rng rng)
        {
            int max = db.RarityOrder(maxRarity);
            for (int pass = 0; pass < 3; pass++)
            {
                var candidates = new List<string>();
                if (kind == "perk")
                {
                    foreach (var x in db.Perks)
                        if (x.source == "wheel" && !p.perks.Contains(x.id) && Fits(db.RarityOrder(x.rarity), max, pass)) candidates.Add(x.id);
                }
                else if (kind == "mutator")
                {
                    foreach (var x in db.Mutators)
                        if (x.source == "wheel" && !p.mutators.Contains(x.id) && Fits(db.RarityOrder(x.rarity), max, pass)) candidates.Add(x.id);
                }
                else if (kind == "weapon")
                {
                    foreach (var x in db.Weapons)
                        if ((x.unlock == "wheel" || x.unlock.StartsWith("achievement:")) && !p.weapons.Contains(x.id) && Fits(db.RarityOrder(x.rarity), max, pass)) candidates.Add(x.id);
                }
                if (candidates.Count == 0) continue;
                string id = rng.Pick(candidates);
                if (kind == "perk") p.perks.Add(id);
                else if (kind == "mutator") p.mutators.Add(id);
                else p.weapons.Add(id);
                return id;
            }
            return "";
        }

        static bool Fits(int order, int max, int pass) => pass == 0 ? order == max : pass == 1 ? order <= max : true;
    }
}
