// Rigfall.Core — plain C# (no UnityEngine). Data classes that mirror the JSON files in
// Assets/Rigfall/Resources/Data. Field names match the JSON keys exactly, because Unity's
// JsonUtility (and System.Text.Json in the tests) map by field name.
//
// Every class is [Serializable] with public fields only: JsonUtility ignores properties,
// dictionaries and top-level arrays, so each file is wrapped as { "items": [ ... ] }.
using System;
using System.Collections.Generic;

namespace Rigfall.Core
{
    [Serializable]
    public class StatMod
    {
        public string stat = "";
        public string op = "add"; // add | mul | set
        public float value;

        public StatMod() { }
        public StatMod(string stat, string op, float value) { this.stat = stat; this.op = op; this.value = value; }
    }

    [Serializable]
    public class RarityDef
    {
        public string id = "";
        public string name = "";
        public string color = "#FFFFFF";
        public float wheelWeight;
        public int order;
    }

    [Serializable]
    public class DifficultyDef
    {
        public string id = "";
        public string name = "";
        public int order;
        public string description = "";
        public float enemyHp = 1, enemyDamage = 1, enemyCount = 1, enemySpeed = 1, eliteChance;
        public float income = 1;
        public int startBarrels;
        public float scaling = 1, scoreMult = 1, heroRespawn = 1;
        public bool allMutators;
    }

    [Serializable]
    public class ClassDef
    {
        public string id = "", name = "", role = "", weaponType = "", description = "", flavor = "";
        public float maxHp = 100, moveSpeed = 6, armor, commandRadius = 6;
        public string startingWeapon = "", model = "", color = "#FFFFFF";
        public string ability = "", abilityName = "", abilityDescription = "";
        public float abilityCooldown = 10;
    }

    [Serializable]
    public class WeaponDef
    {
        public string id = "", name = "", classId = "", rarity = "common", attack = "melee", description = "";
        public float damage, rate = 1, range = 2, arc, radius, knockback;
        public float projectileSpeed;
        public int projectiles = 1;
        public float spread;
        public int pierce;
        public string effect = "";
        public float effectValue, effectDuration;
        public string unlock = "start", model = "", tint = "#FFFFFF";
    }

    [Serializable]
    public class PerkDef
    {
        public string id = "", name = "", rarity = "common", category = "", source = "starter";
        public string description = "", flavor = "";
        public StatMod[] mods = new StatMod[0];
        public string special = "";
    }

    [Serializable]
    public class MutatorDef
    {
        public string id = "", name = "", rarity = "common", source = "starter", group = "", description = "";
        public float scoreMult = 1;
        public StatMod[] mods = new StatMod[0];
        public string special = "";
    }

    [Serializable]
    public class EnemyDef
    {
        public string id = "", name = "", description = "";
        public bool boss;
        public float hp = 10, damage = 1, attackRate = 1, attackRange = 1.2f, speed = 3, armor;
        public bool flying, ranged;
        public float projectileSpeed, buildingDamage = 1;
        public int threat = 1, score = 1;
        public string model = "";
        public float scale = 1;
        public string special = "";
        public float specialValue;
        public string spawns = "";
    }

    [Serializable]
    public class UnitDef
    {
        public string id = "", name = "", building = "", role = "", description = "";
        public float hp = 100, damage = 10, attackRate = 1, attackRange = 1.5f, speed = 3.5f, armor;
        public bool ranged, canHitAir, flying;
        public float projectileSpeed, splash;
        public string special = "";
        public float specialValue;
        public string model = "";
        public float scale = 1;
    }

    [Serializable]
    public class BuildingOption
    {
        public string id = "", name = "", description = "";
        public int cost;
        public int requiresRigTier;
        public string requires = "";
        public float hp = 100, income, damage, range, rate, splash, burn, slow, thorns;
        public string unit = "";
        public int unitCount;
        public float unitBonus = 1;
        public bool canHitAir;
    }

    [Serializable]
    public class BuildingTier
    {
        public BuildingOption[] options = new BuildingOption[0];
    }

    [Serializable]
    public class BuildingDef
    {
        public string id = "", name = "", kind = "", description = "", model = "";
        public float footprint = 1.5f;
        public BuildingTier[] tiers = new BuildingTier[0];

        public bool IsUnitBuilding => kind == "barracks";
        public int MaxTier => tiers.Length - 1;
    }

    [Serializable]
    public class SpawnDef
    {
        public string id = "";
        public float x, z;
        public int fromNight = 1;
    }

    [Serializable]
    public class SlotDef
    {
        public string id = "", building = "";
        public float x, z, rot, length;
    }

    [Serializable]
    public class DecorDef
    {
        public string model = "";
        public float x, z, rot, scale = 1;
    }

    [Serializable]
    public class WalkwayDef
    {
        public float x, z, width, length, rot;
    }

    [Serializable]
    public class MapDef
    {
        public string id = "", name = "", setting = "onshore", biome = "", description = "", unlockAfter = "";
        public int order;
        public string shape = "disc";
        public float radius = 30;
        public string[] decorModels = new string[0];
        public string bgmDay = "", bgmNight = "";
        public string[] enemyOrder = new string[0];
        public string ground = "#888888", groundAlt = "#777777", accent = "#444444", water = "", fog = "#AAAAAA";
        public string skyDay = "#99BBDD", skyNight = "#101020", sun = "#FFFFFF";
        public SpawnDef[] spawns = new SpawnDef[0];
        public SlotDef[] slots = new SlotDef[0];
        public DecorDef[] decor = new DecorDef[0];
        public WalkwayDef[] walkways = new WalkwayDef[0];
        public int startBarrels = 6;

        public bool Offshore => setting == "offshore";
    }

    [Serializable]
    public class AchievementDef
    {
        public string id = "", name = "", description = "", type = "", key = "";
        public int value;
        public int difficulty = -1;
        public string classId = "", setting = "";
        public int mutators;
        public string rewardPerk = "", rewardWeapon = "", rewardMutator = "";
        public int rewardCore;
    }

    [Serializable]
    public class QuestDef
    {
        public string id = "", name = "", description = "", type = "", key = "";
        public int baseCount = 1;
        public float perNight;
        public int minNight = 1;
        public int rewardBarrels, rewardCore;
    }

    [Serializable]
    public class WheelRewardDef
    {
        public string id = "", rarity = "common", kind = "", name = "", description = "";
        public float amount, perNight;
        public string target = "";
        public StatMod[] mods = new StatMod[0];
    }

    [Serializable]
    public class TutorialStepDef
    {
        public string id = "", title = "", text = "", advanceOn = "";
        public int count = 1;
    }

    [Serializable]
    public class GlossaryEntry
    {
        public string term = "", text = "";
    }

    [Serializable]
    public class MusicTrackDef
    {
        public string id = "", title = "", mood = "";
    }

    // ---- JsonUtility wrappers: one per file -------------------------------------------------
    [Serializable] public class RarityList { public RarityDef[] items = new RarityDef[0]; }
    [Serializable] public class DifficultyList { public DifficultyDef[] items = new DifficultyDef[0]; }
    [Serializable] public class ClassList { public ClassDef[] items = new ClassDef[0]; }
    [Serializable] public class WeaponList { public WeaponDef[] items = new WeaponDef[0]; }
    [Serializable] public class PerkList { public PerkDef[] items = new PerkDef[0]; }
    [Serializable] public class MutatorList { public MutatorDef[] items = new MutatorDef[0]; }
    [Serializable] public class EnemyList { public EnemyDef[] items = new EnemyDef[0]; }
    [Serializable] public class UnitList { public UnitDef[] items = new UnitDef[0]; }
    [Serializable] public class BuildingList { public BuildingDef[] items = new BuildingDef[0]; }
    [Serializable] public class MapList { public MapDef[] items = new MapDef[0]; }
    [Serializable] public class AchievementList { public AchievementDef[] items = new AchievementDef[0]; }
    [Serializable] public class QuestList { public QuestDef[] items = new QuestDef[0]; }
    [Serializable] public class WheelList { public WheelRewardDef[] items = new WheelRewardDef[0]; }
    [Serializable] public class TutorialList { public TutorialStepDef[] items = new TutorialStepDef[0]; }
    [Serializable] public class GlossaryList { public GlossaryEntry[] items = new GlossaryEntry[0]; }
    [Serializable] public class MusicList { public MusicTrackDef[] items = new MusicTrackDef[0]; }

    /// <summary>
    /// All game data, indexed by id. Built once at startup from the JSON wrappers above.
    /// Lists keep the file order (which the UI uses); dictionaries give fast lookups.
    /// </summary>
    public sealed class GameDatabase
    {
        public readonly List<RarityDef> Rarities;
        public readonly List<DifficultyDef> Difficulties;
        public readonly List<ClassDef> Classes;
        public readonly List<WeaponDef> Weapons;
        public readonly List<PerkDef> Perks;
        public readonly List<MutatorDef> Mutators;
        public readonly List<EnemyDef> Enemies;
        public readonly List<UnitDef> Units;
        public readonly List<BuildingDef> Buildings;
        public readonly List<MapDef> Maps;
        public readonly List<AchievementDef> Achievements;
        public readonly List<QuestDef> Quests;
        public readonly List<WheelRewardDef> Wheel;
        public readonly List<TutorialStepDef> Tutorial;
        public readonly List<GlossaryEntry> Glossary;
        public readonly List<MusicTrackDef> Music;

        readonly Dictionary<string, RarityDef> rarity = new Dictionary<string, RarityDef>();
        readonly Dictionary<string, DifficultyDef> difficulty = new Dictionary<string, DifficultyDef>();
        readonly Dictionary<string, ClassDef> classes = new Dictionary<string, ClassDef>();
        readonly Dictionary<string, WeaponDef> weapons = new Dictionary<string, WeaponDef>();
        readonly Dictionary<string, PerkDef> perks = new Dictionary<string, PerkDef>();
        readonly Dictionary<string, MutatorDef> mutators = new Dictionary<string, MutatorDef>();
        readonly Dictionary<string, EnemyDef> enemies = new Dictionary<string, EnemyDef>();
        readonly Dictionary<string, UnitDef> units = new Dictionary<string, UnitDef>();
        readonly Dictionary<string, BuildingDef> buildings = new Dictionary<string, BuildingDef>();
        readonly Dictionary<string, MapDef> maps = new Dictionary<string, MapDef>();
        readonly Dictionary<string, AchievementDef> achievements = new Dictionary<string, AchievementDef>();
        readonly Dictionary<string, QuestDef> quests = new Dictionary<string, QuestDef>();

        public GameDatabase(RarityList r, DifficultyList d, ClassList c, WeaponList w, PerkList p, MutatorList m,
            EnemyList e, UnitList u, BuildingList b, MapList maps, AchievementList a, QuestList q, WheelList wheel,
            TutorialList tutorial, GlossaryList glossary, MusicList music)
        {
            Rarities = Index(r.items, rarity, x => x.id);
            Difficulties = Index(d.items, difficulty, x => x.id);
            Classes = Index(c.items, classes, x => x.id);
            Weapons = Index(w.items, weapons, x => x.id);
            Perks = Index(p.items, perks, x => x.id);
            Mutators = Index(m.items, mutators, x => x.id);
            Enemies = Index(e.items, enemies, x => x.id);
            Units = Index(u.items, units, x => x.id);
            Buildings = Index(b.items, buildings, x => x.id);
            Maps = Index(maps.items, this.maps, x => x.id);
            Achievements = Index(a.items, achievements, x => x.id);
            Quests = Index(q.items, quests, x => x.id);
            Wheel = new List<WheelRewardDef>(wheel.items ?? new WheelRewardDef[0]);
            Tutorial = new List<TutorialStepDef>(tutorial.items ?? new TutorialStepDef[0]);
            Glossary = new List<GlossaryEntry>(glossary.items ?? new GlossaryEntry[0]);
            Music = new List<MusicTrackDef>(music.items ?? new MusicTrackDef[0]);
            Maps.Sort((x, y) => x.order.CompareTo(y.order));
            Difficulties.Sort((x, y) => x.order.CompareTo(y.order));
            Rarities.Sort((x, y) => x.order.CompareTo(y.order));
        }

        static List<T> Index<T>(T[] items, Dictionary<string, T> into, Func<T, string> key)
        {
            var list = new List<T>();
            if (items == null) return list;
            foreach (var it in items)
            {
                if (it == null) continue;
                string k = key(it);
                if (string.IsNullOrEmpty(k) || into.ContainsKey(k)) continue; // skip broken or duplicate rows
                into[k] = it;
                list.Add(it);
            }
            return list;
        }

        static T Find<T>(Dictionary<string, T> d, string id) where T : class
        {
            if (id == null) return null;
            T v;
            return d.TryGetValue(id, out v) ? v : null;
        }

        public RarityDef Rarity(string id) => Find(rarity, id);
        public DifficultyDef Difficulty(string id) => Find(difficulty, id);
        public ClassDef Class(string id) => Find(classes, id);
        public WeaponDef Weapon(string id) => Find(weapons, id);
        public PerkDef Perk(string id) => Find(perks, id);
        public MutatorDef Mutator(string id) => Find(mutators, id);
        public EnemyDef Enemy(string id) => Find(enemies, id);
        public UnitDef Unit(string id) => Find(units, id);
        public BuildingDef Building(string id) => Find(buildings, id);
        public MapDef Map(string id) => Find(maps, id);
        public AchievementDef Achievement(string id) => Find(achievements, id);
        public QuestDef Quest(string id) => Find(quests, id);

        public int RarityOrder(string id)
        {
            var r = Rarity(id);
            return r == null ? 0 : r.order;
        }

        public List<WeaponDef> WeaponsFor(string classId)
        {
            var list = new List<WeaponDef>();
            foreach (var w in Weapons) if (w.classId == classId) list.Add(w);
            list.Sort((a, b) => RarityOrder(a.rarity).CompareTo(RarityOrder(b.rarity)));
            return list;
        }

        public List<EnemyDef> Bosses()
        {
            var list = new List<EnemyDef>();
            foreach (var e in Enemies) if (e.boss) list.Add(e);
            return list;
        }

        public BuildingOption Option(string buildingId, int tier, string optionId)
        {
            var b = Building(buildingId);
            if (b == null || tier < 0 || tier >= b.tiers.Length) return null;
            foreach (var o in b.tiers[tier].options) if (o.id == optionId) return o;
            return null;
        }
    }
}
