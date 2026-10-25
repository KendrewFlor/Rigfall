// Unit buildings: Crew Camp (barracks), Perforation Range (archery range) and the Company
// Man's Office (hero's quarter). Tier 0 chooses which unit moves in; later tiers add a
// unit and then raise their stats. Fallen units are replaced over time at night and all
// return at dawn.
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class Barracks : BuildingBase
    {
        readonly List<FriendlyUnit> units = new List<FriendlyUnit>();
        readonly Dictionary<FriendlyUnit, float> respawnAt = new Dictionary<FriendlyUnit, float>();
        public const float BaseRespawn = 20f;

        public IReadOnlyList<FriendlyUnit> Units => units;

        public UnitDef UnitDef => Game.Db.Unit(BuildingRules.UnitId(Def, Path));

        public Vector3 Post(int index, int count)
        {
            Vector3 fwd = transform.forward;
            Vector3 right = transform.right;
            float lateral = (index - (count - 1) * 0.5f) * 1.7f;
            return MapMath.OnNavMesh(transform.position + fwd * (Def.footprint + 2.2f) + right * lateral);
        }

        protected override void OnOptionChanged() { SyncUnits(); }

        void SyncUnits()
        {
            if (run == null) return;
            var def = UnitDef;
            if (def == null) return;
            int want = BuildingRules.UnitCount(Def, Path, run.Mods);
            var o = Option;
            float bonus = o != null ? o.unitBonus : 1f;
            units.RemoveAll(u => u == null);
            while (units.Count < want)
            {
                var u = FriendlyUnit.Spawn(def, this, Post(units.Count, want), bonus);
                units.Add(u);
                Tweener.ScaleIn(u.transform, 0.35f);
            }
            for (int i = 0; i < units.Count; i++)
            {
                units[i].SetBonus(bonus);
                units[i].SetPost(Post(i, units.Count));
            }
        }

        public void OnUnitDied(FriendlyUnit u)
        {
            float t = BaseRespawn * (run != null ? run.Mods.Mul(Stat.UnitRespawn) : 1f);
            respawnAt[u] = Time.time + t;
        }

        protected override void Update()
        {
            base.Update();
            if (respawnAt.Count == 0) return;
            FriendlyUnit ready = null;
            bool stale = false;
            foreach (var kv in respawnAt)
            {
                // A destroyed unit compares equal to null in Unity but is still a valid dictionary key.
                if (kv.Key == null) { ready = kv.Key; stale = true; break; }
                if (Time.time >= kv.Value) { ready = kv.Key; break; }
            }
            if (stale) { respawnAt.Remove(ready); return; }
            if (!ReferenceEquals(ready, null) && !Destroyed)
            {
                respawnAt.Remove(ready);
                int i = units.IndexOf(ready);
                ready.Respawn(Post(Mathf.Max(0, i), units.Count));
            }
        }

        public override void OnDawn()
        {
            base.OnDawn();
            respawnAt.Clear();
            SyncUnits();
        }
    }
}
