// What can be built or upgraded on a slot, and at what price. A building's state is just
// its "path": the option id chosen at each tier (tier 0 = construction).
using System.Collections.Generic;

namespace Rigfall.Core
{
    public sealed class UpgradeChoice
    {
        public BuildingOption option;
        public int tier;
        public int price;
        public bool locked;
        public string reason = "";
    }

    public static class BuildingRules
    {
        public static int Tier(IList<string> path) => path == null ? -1 : path.Count - 1;

        public static BuildingOption Current(BuildingDef b, IList<string> path)
        {
            int t = Tier(path);
            if (b == null || t < 0 || t >= b.tiers.Length) return null;
            foreach (var o in b.tiers[t].options) if (o.id == path[t]) return o;
            return null;
        }

        /// <summary>The unit a barracks fields: always chosen at tier 0.</summary>
        public static string UnitId(BuildingDef b, IList<string> path)
        {
            if (b == null || path == null || path.Count == 0 || b.tiers.Length == 0) return "";
            foreach (var o in b.tiers[0].options) if (o.id == path[0]) return o.unit;
            return "";
        }

        /// <summary>Is the path a legal sequence of choices for this building?</summary>
        public static bool IsValidPath(BuildingDef b, IList<string> path)
        {
            if (b == null || path == null || path.Count > b.tiers.Length) return false;
            for (int t = 0; t < path.Count; t++)
            {
                BuildingOption found = null;
                foreach (var o in b.tiers[t].options) if (o.id == path[t]) found = o;
                if (found == null) return false;
                if (!string.IsNullOrEmpty(found.requires) && (t == 0 || path[t - 1] != found.requires)) return false;
            }
            return true;
        }

        /// <summary>
        /// The choices offered next on a slot. Options whose 'requires' doesn't match the
        /// previous choice are not offered; options above the Main Rig tier are offered but
        /// locked (unless Modular Rig-Up lifts the limit for towers).
        /// </summary>
        public static List<UpgradeChoice> NextChoices(BuildingDef b, IList<string> path, int rigTier, ModifierSet mods, bool firstToday)
        {
            var list = new List<UpgradeChoice>();
            if (b == null) return list;
            int next = path == null ? 0 : path.Count;
            if (next >= b.tiers.Length) return list;
            string prev = next > 0 ? path[next - 1] : "";
            bool ignoreTier = b.kind == "tower" && mods.Flag(Stat.TowerIgnoreRigTier);
            foreach (var o in b.tiers[next].options)
            {
                if (!string.IsNullOrEmpty(o.requires) && o.requires != prev) continue;
                var c = new UpgradeChoice { option = o, tier = next, price = Economy.Cost(b, next, o, mods, firstToday) };
                if (b.kind != "rig" && !ignoreTier && o.requiresRigTier > rigTier)
                {
                    c.locked = true;
                    c.reason = "Needs Main Rig tier " + o.requiresRigTier;
                }
                list.Add(c);
            }
            return list;
        }

        public static int UnitCount(BuildingDef b, IList<string> path, ModifierSet mods)
        {
            var o = Current(b, path);
            if (o == null || b.kind != "barracks") return 0;
            int n = o.unitCount + (int)System.Math.Round(mods.Get(Stat.UnitCount, 0));
            return n < 0 ? 0 : n;
        }
    }
}
