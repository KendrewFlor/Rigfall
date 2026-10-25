// A shooting module used by derrick towers, the Main Rig's gunners, fire monitors and
// Fortified Pumpjacks. Targets are found by a lightweight coroutine every 0.2 seconds
// (not every frame); the Update loop only counts down the cooldown and fires.
using System.Collections;
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class Turret : MonoBehaviour
    {
        public float damage = 8f, range = 10f, rate = 1f, splash, burn, slow;
        public bool canHitAir = true, lob;
        public float projectileSpeed = 26f;
        public KillSource source = KillSource.Tower;
        public Color shotColor = new Color(1f, 0.85f, 0.4f);
        public Vector3 muzzle = new Vector3(0f, 3f, 0f);
        public string sound = "sfx_shoot";
        public Health owner;
        public Transform aimPart;        // optional part that turns toward the target

        static readonly WaitForSeconds scanWait = new WaitForSeconds(0.2f);
        Health target;
        float cooldown;

        void OnEnable() { StartCoroutine(Scan()); }

        IEnumerator Scan()
        {
            while (true)
            {
                if (owner == null || owner.Alive)
                {
                    if (target == null || !target.Alive || target.DistanceTo(transform.position) > range)
                        target = Health.Nearest(transform.position, range, Team.Enemy, canHitAir, false);
                }
                else target = null;
                yield return scanWait;
            }
        }

        void Update()
        {
            cooldown -= Time.deltaTime;
            if (target == null || !target.Alive || (owner != null && !owner.Alive)) return;
            if (aimPart != null)
            {
                Vector3 d = MapMath.Flat(target.transform.position - aimPart.position);
                if (d.sqrMagnitude > 0.01f) aimPart.rotation = Quaternion.Slerp(aimPart.rotation, Quaternion.LookRotation(d), Time.deltaTime * 10f);
            }
            if (cooldown > 0f) return;
            cooldown = 1f / Mathf.Max(0.05f, rate);
            Fire();
        }

        void Fire()
        {
            Vector3 from = transform.TransformPoint(muzzle);
            var d = DamageInfo.Simple(damage, source, gameObject);
            d.burnDps = burn; d.burnTime = burn > 0f ? 3f : 0f;
            d.slow = slow; d.slowTime = slow > 0f ? 2f : 0f;
            d.knockback = slow > 0f ? 1.5f : 0.3f;
            d.projectile = true;
            Projectile.Launch(new ProjectileSpec
            {
                from = from,
                target = target,
                targetPoint = target.transform.position,
                speed = projectileSpeed,
                maxRange = range,
                lob = lob || splash > 0f,
                arcHeight = 2.5f,
                splash = splash,
                canHitAir = canHitAir,
                targetTeam = Team.Enemy,
                damage = d,
                color = shotColor,
                size = splash > 0f ? 0.4f : 0.22f,
                impactFx = burn > 0f ? FxKind.Fire : slow > 0f ? FxKind.Mud : FxKind.Spark,
            });
            Fx.Emit(FxKind.Spark, from, 3);
            AudioService.Sfx(sound, from, Random.Range(0.95f, 1.1f), 0.7f);
            if (aimPart != null) Tweener.Punch(aimPart, 0.12f, 0.15f);
        }
    }
}
