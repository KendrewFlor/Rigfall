// Barrels (the in-run currency), dawn income, building costs, score and permanent weapon
// upgrades. All plain arithmetic so it can be tested and balanced outside Unity.
using System;
using System.Collections.Generic;

namespace Rigfall.Core
{
    /// <summary>One income-producing building, as seen at dawn.</summary>
    public sealed class Producer
    {
        public string kind;       // "house" (pumpjack) or "mill" (tank battery)
        public float income;      // from the building's current option
        public bool destroyed;    // knocked out during the night
        public Producer(string kind, float income, bool destroyed) { this.kind = kind; this.income = income; this.destroyed = destroyed; }
    }

    public sealed class IncomeBreakdown
    {
        public int rig, houses, mills, interest, total;
        public float multiplier = 1;
        public readonly List<string> lines = new List<string>();
    }

    public static class Economy
    {
        public static IncomeBreakdown Dawn(int rigTier, float rigIncome, IList<Producer> producers, int barrelsHeld,
            DifficultyDef diff, ModifierSet mods)
        {
            var b = new IncomeBreakdown();
            float rig = rigIncome + mods.Get(Stat.IncomeRig, 0) + mods.Get(Stat.IncomeRigTier, 0) * rigTier;
            float houses = 0, mills = 0;
            bool keepPaying = mods.Flag(Stat.HouseRespawnIncome);
            float houseBonus = mods.Get(Stat.IncomeHouse, 0);
            float millBonus = mods.Get(Stat.IncomeMill, 0);
            int lostHouses = 0;
            foreach (var p in producers)
            {
                if (p.kind == "house")
                {
                    if (p.destroyed && !keepPaying) { lostHouses++; continue; }
                    houses += p.income + houseBonus;
                }
                else if (p.kind == "mill")
                {
                    if (p.destroyed) continue;
                    mills += p.income + millBonus;
                }
            }
            b.multiplier = diff.income * mods.Mul(Stat.IncomeAll);
            b.rig = Round(rig * b.multiplier);
            b.houses = Round(houses * b.multiplier);
            b.mills = Round(mills * b.multiplier);
            float rate = mods.Get(Stat.InterestRate, 0);
            float cap = mods.Get(Stat.InterestCap, 0);
            b.interest = rate > 0 ? Math.Min((int)Math.Floor(barrelsHeld * rate), (int)cap) : 0;
            b.total = Math.Max(0, b.rig + b.houses + b.mills + b.interest);

            b.lines.Add("Main Rig  +" + b.rig);
            if (b.houses > 0) b.lines.Add("Pumpjacks  +" + b.houses);
            if (lostHouses > 0) b.lines.Add(lostHouses + " pumpjack" + (lostHouses == 1 ? "" : "s") + " knocked out  +0");
            if (b.mills > 0) b.lines.Add("Tank batteries  +" + b.mills);
            if (b.interest > 0) b.lines.Add("Royalty interest  +" + b.interest);
            return b;
        }

        static int Round(float v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Price of building (tier 0) or upgrading (tier 1+) with the given option.
        /// firstToday applies Spot Price Spike. Free work orders are handled by the caller.
        /// </summary>
        public static int Cost(BuildingDef building, int tier, BuildingOption option, ModifierSet mods, bool firstToday)
        {
            if (option == null || building == null) return 0;
            float c = option.cost;
            if (c <= 0) return 0;
            if (building.kind == "tower" && tier == 0) c += mods.Get(Stat.CostTower, 0);
            if (building.kind == "wall") c *= mods.Mul(Stat.CostWall);
            if (building.kind == "barracks") c *= mods.Mul(Stat.CostUnitBuilding);
            if (building.kind == "rig") c *= mods.Mul(Stat.CostRig);
            if (tier > 0) c *= mods.Mul(Stat.CostUpgrade);
            c *= mods.Mul(Stat.CostBuild);
            int price = Math.Max(1, (int)Math.Ceiling(c - 0.001f));
            if (firstToday) price += (int)Math.Round(mods.Get(Stat.CostFirstDaily, 0));
            return Math.Max(0, price);
        }

        /// <summary>Barrels you start a run with on a field.</summary>
        public static int StartingBarrels(MapDef map, DifficultyDef diff) => Math.Max(2, map.startBarrels + diff.startBarrels);
    }

    public static class Scoring
    {
        public static float Multiplier(DifficultyDef diff, IEnumerable<MutatorDef> mutators)
        {
            float m = diff.scoreMult;
            if (mutators != null) foreach (var mu in mutators) m *= mu.scoreMult;
            return m;
        }

        public static int ForNight(int night) => 100 * night;
    }

    /// <summary>Permanent weapon upgrades bought with core samples between runs.</summary>
    public static class WeaponUpgrades
    {
        public const int MaxLevel = 5;
        static readonly int[] baseCost = { 10, 20, 35, 55, 80 };
        static readonly float[] rarityFactor = { 1f, 1.3f, 1.6f, 2f, 2.5f };

        /// <summary>Core samples needed to go from level to level+1, or -1 at max level.</summary>
        public static int CostToUpgrade(int level, int rarityOrder)
        {
            if (level < 0) level = 0;
            if (level >= MaxLevel) return -1;
            int r = Math.Max(0, Math.Min(rarityFactor.Length - 1, rarityOrder));
            return (int)Math.Round(baseCost[level] * rarityFactor[r]);
        }

        public static float DamageMult(int level) => 1f + 0.08f * Clamp(level);
        public static float RateMult(int level) => 1f + 0.04f * Clamp(level);
        static int Clamp(int level) => Math.Max(0, Math.Min(MaxLevel, level));
    }
}
