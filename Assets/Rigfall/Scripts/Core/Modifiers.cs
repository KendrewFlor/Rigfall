// Stat modifiers: perks, mutators and wheel blessings all change the game through the
// same small vocabulary of stat keys ("hero.damage", "income.house", ...). Gameplay code
// asks the ModifierSet for the final value instead of knowing about individual perks.
using System;
using System.Collections.Generic;

namespace Rigfall.Core
{
    /// <summary>Every stat key the game understands. tools/validate_data.py keeps the JSON in sync with this list.</summary>
    public static class Stat
    {
        // Economy
        public const string IncomeRig = "income.rig", IncomeHouse = "income.house", IncomeMill = "income.mill";
        public const string IncomeRigTier = "income.rigTier", IncomeAll = "income.all";
        public const string InterestRate = "interest.rate", InterestCap = "interest.cap";
        public const string BountyChance = "bounty.chance", WheelLuck = "wheel.luck", CorePerNight = "core.perNight";
        public const string CostBuild = "cost.build", CostTower = "cost.tower", CostWall = "cost.wall";
        public const string CostUnitBuilding = "cost.unitBuilding", CostUpgrade = "cost.upgrade", CostRig = "cost.rig";
        public const string CostFirstDaily = "cost.firstDaily";
        // Buildings
        public const string HpWall = "hp.wall", HpHouse = "hp.house", HpTower = "hp.tower", HpRig = "hp.rig", HpBuilding = "hp.building";
        public const string RegenBuilding = "regen.building", DamageTakenBuilding = "damageTaken.building", DamageTakenAir = "damageTaken.air";
        public const string TowerRange = "tower.range", TowerDamage = "tower.damage", TowerRate = "tower.rate", TowerBurn = "tower.burn";
        public const string TowerIgnoreRigTier = "tower.ignoreRigTier";
        public const string WallThorns = "wall.thorns", WallExplode = "wall.explode";
        public const string HouseRespawnIncome = "house.respawnIncome", HouseTurret = "house.turret";
        // Units
        public const string UnitDamage = "unit.damage", UnitHp = "unit.hp", UnitSpeed = "unit.speed", UnitRange = "unit.range";
        public const string UnitCount = "unit.count", UnitRespawn = "unit.respawn", UnitArmor = "unit.armor", UnitNoDawnHeal = "unit.noDawnHeal";
        // Hero
        public const string HeroDamage = "hero.damage", HeroRate = "hero.rate", HeroSpeed = "hero.speed", HeroHp = "hero.hp";
        public const string HeroRange = "hero.range", HeroKnockback = "hero.knockback", HeroRespawn = "hero.respawn";
        public const string HeroCrit = "hero.crit", HeroBurn = "hero.burn", HeroSlow = "hero.slow";
        public const string HeroAbilityCooldown = "hero.abilityCooldown", HeroArmor = "hero.armor", HeroNoRespawn = "hero.noRespawn";
        public const string CommandRadius = "command.radius";
        // Enemies and the night
        public const string EnemyHp = "enemy.hp", EnemyDamage = "enemy.damage", EnemySpeed = "enemy.speed", EnemyCount = "enemy.count";
        public const string EnemyEliteChance = "enemy.eliteChance", EnemyFlyingShare = "enemy.flyingShare", EnemyRevive = "enemy.revive";
        public const string EnemyRange = "enemy.range", EnemyBuildingDamage = "enemy.buildingDamage", EnemyArmor = "enemy.armor";
        public const string VisionRadius = "vision.radius", BossInterval = "boss.interval", SpawnExtraLanes = "spawn.extraLanes";
    }

    /// <summary>
    /// Holds two layers of modifiers: permanent ones for the whole run (perks, mutators,
    /// blessings) and a night layer that is cleared every dawn (Uncontrolled Flow).
    /// Value = set ?? (base + sum(add)) * product(mul).
    /// </summary>
    public sealed class ModifierSet
    {
        sealed class Acc { public float add; public float mul = 1f; public bool hasSet; public float set; }

        readonly List<StatMod> run = new List<StatMod>();
        readonly List<StatMod> night = new List<StatMod>();
        readonly HashSet<string> specials = new HashSet<string>();
        readonly HashSet<string> nightSpecials = new HashSet<string>();
        readonly Dictionary<string, Acc> cache = new Dictionary<string, Acc>();
        bool dirty = true;

        public int Version { get; private set; }

        public void Clear()
        {
            run.Clear(); night.Clear(); specials.Clear(); nightSpecials.Clear();
            Touch();
        }

        public void Add(IEnumerable<StatMod> mods)
        {
            if (mods == null) return;
            foreach (var m in mods) if (m != null && !string.IsNullOrEmpty(m.stat)) run.Add(m);
            Touch();
        }

        public void Add(StatMod mod) { if (mod != null) { run.Add(mod); Touch(); } }

        public void AddSpecial(string special)
        {
            if (!string.IsNullOrEmpty(special)) { specials.Add(special); Touch(); }
        }

        public void SetNightLayer(IEnumerable<StatMod> mods, string special)
        {
            night.Clear();
            nightSpecials.Clear();
            if (mods != null) foreach (var m in mods) if (m != null) night.Add(m);
            if (!string.IsNullOrEmpty(special)) nightSpecials.Add(special);
            Touch();
        }

        public void ClearNightLayer() => SetNightLayer(null, null);

        public bool HasSpecial(string special) => specials.Contains(special) || nightSpecials.Contains(special);

        void Touch() { dirty = true; Version++; }

        void Rebuild()
        {
            cache.Clear();
            Fold(run);
            Fold(night);
            dirty = false;
        }

        void Fold(List<StatMod> mods)
        {
            foreach (var m in mods)
            {
                Acc a;
                if (!cache.TryGetValue(m.stat, out a)) { a = new Acc(); cache[m.stat] = a; }
                switch (m.op)
                {
                    case "mul": a.mul *= m.value; break;
                    case "set": a.hasSet = true; a.set = m.value; break;
                    default: a.add += m.value; break;
                }
            }
        }

        /// <summary>Final value of a stat, starting from the given base value.</summary>
        public float Get(string stat, float baseValue = 0f)
        {
            if (dirty) Rebuild();
            Acc a;
            if (!cache.TryGetValue(stat, out a)) return baseValue;
            if (a.hasSet) return a.set;
            return (baseValue + a.add) * a.mul;
        }

        /// <summary>Multiplier stats start at 1.</summary>
        public float Mul(string stat) => Get(stat, 1f);

        /// <summary>Flag stats (set to 1 to switch on).</summary>
        public bool Flag(string stat) => Get(stat, 0f) > 0.5f;
    }
}
