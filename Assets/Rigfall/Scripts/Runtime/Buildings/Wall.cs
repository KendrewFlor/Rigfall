// Blast wall segment. Blocks the lane (its NavMeshObstacle carves the NavMesh, so enemies
// have to break it or go around). Barbed Casing hurts attackers; with the Shaped-Charge
// Walls perk a breached wall detonates.
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class Wall : BuildingBase
    {
        protected override void OnDamaged(Health h, DamageInfo d)
        {
            base.OnDamaged(h, d);
            if (run == null || d.attacker == null || d.projectile) return;
            var o = Option;
            float thorns = (o != null ? o.thorns : 0f) + run.Mods.Get(Stat.WallThorns, 0f);
            if (thorns <= 0f) return;
            var attacker = d.attacker.GetComponent<Health>();
            if (attacker != null && attacker.Alive && attacker.team == Team.Enemy)
                Combat.Hit(attacker, DamageInfo.Simple(thorns, KillSource.Building, gameObject), FxKind.Spark, "");
        }

        protected override void OnKnockedOut()
        {
            if (run == null) return;
            float blast = run.Mods.Get(Stat.WallExplode, 0f);
            if (blast <= 0f) return;
            var d = DamageInfo.Simple(blast, KillSource.Building, gameObject);
            d.knockback = 3f;
            Combat.Splash(transform.position + Vector3.up * 0.5f, 3.6f, Team.Enemy, false, d, FxKind.Explosion);
            Fx.Shake(0.5f);
        }
    }
}
