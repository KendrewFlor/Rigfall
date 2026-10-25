// Derrick tower: a gunner on the monkeyboard. Uses a Turret for targeting (0.2 s scans).
// Upgrades branch into the Crown Block Sniper line (range) or the Flare Stack line (burning splash).
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class Tower : BuildingBase
    {
        RangeRing ring;

        protected override void OnSetup()
        {
            ring = RangeRing.Create(transform, new Color(1f, 0.85f, 0.4f, 0.6f));
            OnOptionChanged(); // now that the ring exists, size it
        }

        protected override void OnOptionChanged()
        {
            var o = Option;
            var t = EnsureTurret(o != null);
            if (t == null || run == null) return;
            var mods = run.Mods;
            t.damage = o.damage * mods.Mul(Stat.TowerDamage);
            t.range = o.range * mods.Mul(Stat.TowerRange);
            t.rate = o.rate * mods.Mul(Stat.TowerRate);
            t.splash = o.splash;
            t.burn = o.burn + mods.Get(Stat.TowerBurn, 0f);
            t.canHitAir = true;
            t.source = KillSource.Tower;
            t.muzzle = Models.Anchor(ModelName(), "muzzle", new Vector3(0f, 4.5f, 0f));
            t.shotColor = t.burn > 0f ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.9f, 0.5f);
            t.sound = o.splash > 0f ? "sfx_shoot_heavy" : "sfx_shoot";
            var top = visual != null ? visual.transform.Find("top") : null;
            t.aimPart = top;
            if (ring != null) ring.SetRadius(t.range);
        }

        protected override void Update()
        {
            base.Update();
            if (ring == null || run == null || run.Hero == null) return;
            bool show = !run.IsNight && !Destroyed && Game.Settings.showRanges &&
                        Vector3.Distance(run.Hero.transform.position, transform.position) < 6f;
            ring.Show(show);
        }
    }
}
