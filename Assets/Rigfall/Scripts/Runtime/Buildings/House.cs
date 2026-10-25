// Economy buildings: pumpjacks (kind "house") and tank batteries (kind "mill"). They pay
// barrels at dawn (see Run.EndNight). A pumpjack nods while it works; with the Fortified
// Pumpjacks perk, or a tank battery's Fire Monitor upgrade, they also shoot.
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class House : BuildingBase
    {
        Transform beam, head;
        float phase;

        /// <summary>Barrels this building's option pays at dawn (before perks and multipliers).</summary>
        public float Income { get { var o = Option; return o != null ? o.income : 0f; } }

        protected override void OnSetup() { phase = Random.value * 6f; }

        protected override void OnOptionChanged()
        {
            beam = visual != null ? visual.transform.Find("beam") : null;
            head = visual != null ? visual.transform.Find("head") : null;
            var o = Option;
            float turretDamage = 0f;
            float range = 9f, rate = 1.5f, slow = 0f, splash = 0f;
            if (o != null && o.damage > 0f) { turretDamage = o.damage; range = o.range; rate = o.rate; slow = o.slow; splash = o.splash; }
            if (Def.kind == "house" && run != null)
            {
                float fortified = run.Mods.Get(Stat.HouseTurret, 0f);
                if (fortified > 0f) { turretDamage = Mathf.Max(turretDamage, fortified); range = 9f; rate = 1.4f; }
            }
            var t = EnsureTurret(turretDamage > 0f);
            if (t != null)
            {
                t.damage = turretDamage;
                t.range = range;
                t.rate = rate;
                t.slow = slow;
                t.splash = splash;
                t.canHitAir = slow <= 0f;
                t.source = KillSource.Building;
                t.muzzle = new Vector3(0f, 2.2f, 0f);
                t.shotColor = slow > 0f ? new Color(0.5f, 0.75f, 1f) : new Color(1f, 0.9f, 0.5f);
                t.sound = slow > 0f ? "sfx_spray" : "sfx_shoot";
            }
        }

        protected override void Update()
        {
            base.Update();
            if (Destroyed) return;
            if (beam != null)
            {
                float a = Mathf.Sin(Time.time * 1.6f + phase) * 14f;
                beam.localRotation = Quaternion.Euler(a, 0f, 0f);
            }
        }
    }
}
