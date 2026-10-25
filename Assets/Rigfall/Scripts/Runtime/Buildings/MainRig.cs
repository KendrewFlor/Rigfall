// The Main Rig: the "castle centre". If its integrity reaches 0 the night is lost.
// Tiers raise its integrity and income and unlock higher upgrades elsewhere. From tier 1
// a crew on the rig floor shoots at nearby enemies.
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class MainRig : BuildingBase
    {
        public BuildingSlot Interaction { get; set; }
        float bopUntil;

        protected override void OnSetup()
        {
            Health.radius = 2.6f;
        }

        protected override void OnOptionChanged()
        {
            var o = Option;
            var t = EnsureTurret(o != null && o.damage > 0f);
            if (t != null)
            {
                t.damage = o.damage;
                t.range = o.range;
                t.rate = o.rate;
                t.splash = o.splash;
                t.canHitAir = true;
                t.source = KillSource.Building;
                t.muzzle = Models.Anchor(ModelName(), "muzzle", new Vector3(0f, 6f, 0f));
                t.shotColor = new Color(1f, 0.75f, 0.3f);
            }
        }

        protected override void OnDamaged(Health h, DamageInfo d)
        {
            base.OnDamaged(h, d);
            if (run == null) return;
            run.OnRigDamaged();
            Fx.Shake(0.08f);
            // Blowout Preventer perk: once per night the rams close below half integrity.
            if (run.Mods.HasSpecial("bop") && !run.BopUsedTonight && Health.Fraction < 0.5f && Health.Alive)
            {
                run.BopUsedTonight = true;
                bopUntil = Time.time + 5f;
                Health.invulnerable = true;
                Ui.Banner("BOP closed! The rig is sealed for 5 seconds", Palette.Friendly);
                AudioService.Sfx("sfx_ability", transform.position);
                Fx.Emit(FxKind.Shockwave, transform.position + Vector3.up, 30);
            }
        }

        protected override void OnDied(Health h, DamageInfo d)
        {
            // Relief Well perk: survive once per run with 30% integrity.
            if (run != null && run.Mods.HasSpecial("reliefWell") && !run.ReliefUsed)
            {
                run.ReliefUsed = true;
                Health.Revive(0.3f);
                Ui.Banner("Relief Well! The rig is back at 30%", Palette.Heal);
                Fx.Emit(FxKind.Heal, transform.position + Vector3.up * 2f, 40);
                AudioService.Sfx("sfx_heal", transform.position);
                return;
            }
            // Otherwise stay at 0: the night state machine sees it and moves to GameOver.
            Fx.Emit(FxKind.Explosion, transform.position + Vector3.up * 3f, 40);
            Fx.Emit(FxKind.Fire, transform.position + Vector3.up * 4f, 40);
            Fx.Shake(1.2f);
        }

        protected override void Update()
        {
            base.Update();
            if (Health != null && Health.invulnerable && Time.time > bopUntil) Health.invulnerable = false;
        }

        public override void OnDawn()
        {
            Health.invulnerable = false;
            if (!Health.Alive) Health.Revive(1f);
            else Health.HealFull();
        }
    }
}
