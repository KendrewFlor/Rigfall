// An enemy. Ground enemies use a NavMeshAgent and path around (or break through) walls on
// their way to the Main Rig; flying enemies glide straight over everything. Every 0.2 s a
// coroutine picks a target: something of yours close by (units and the hero first,
// buildings second), otherwise the Main Rig.
using System.Collections;
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Rigfall.Runtime
{
    public sealed class Enemy : MonoBehaviour
    {
        public EnemyDef Def { get; private set; }
        public Health Health { get; private set; }
        public bool Elite { get; private set; }
        public bool Boss => Def != null && Def.boss;
        public bool Alive => (Health != null && Health.Alive) || reviving;  // a reforming enemy still counts

        NavMeshAgent agent;
        GameObject model;
        HitFlash flash;
        Health target;
        float damage, speed, range, cooldown, flyHeight = 2.6f, stuck;
        Vector3 knock;
        bool revived, dying, reviving, hidden;
        MeshRenderer[] renderers = new MeshRenderer[0];
        static readonly WaitForSeconds scanWait = new WaitForSeconds(0.2f);
        static readonly List<Health> scratch = new List<Health>();

        public Health Target => target;
        public float Damage => damage;

        public static Enemy Spawn(EnemyDef def, Vector3 at, bool elite, WavePlan plan, float extraHpMult = 1f)
        {
            var run = Run.Current;
            var go = new GameObject(def.name + (elite ? " (elite)" : ""));
            go.transform.SetParent(run.Field.EnemyRoot, false);
            go.transform.position = def.flying ? at : MapMath.OnNavMesh(at, 10f);
            var e = go.AddComponent<Enemy>();
            e.Setup(def, elite, plan, extraHpMult);
            return e;
        }

        void Setup(EnemyDef def, bool elite, WavePlan plan, float extraHpMult)
        {
            var run = Run.Current;
            Def = def;
            Elite = elite;
            float hpMult = plan != null ? plan.hpMult : 1f;
            float dmgMult = plan != null ? plan.damageMult : 1f;
            float spdMult = plan != null ? plan.speedMult : 1f;
            if (def.boss && plan != null) hpMult *= plan.bossHpMult;
            if (elite) { hpMult *= 2.5f; dmgMult *= 1.4f; }

            model = Models.Spawn(def.model, transform, elite ? Materials.Lit(new Color(1f, 0.82f, 0.55f)) : null);
            float scale = def.scale * (elite ? 1.3f : 1f);
            model.transform.localScale = Vector3.one * scale;
            if (def.flying) model.transform.localPosition = Vector3.up * flyHeight;
            renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            flash = gameObject.AddComponent<HitFlash>();
            flash.Bind(model, elite ? Materials.Lit(new Color(1f, 0.82f, 0.55f)) : Materials.Lit(Color.white));

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.45f * scale;
            col.height = 1.6f * scale;
            col.center = new Vector3(0f, def.flying ? flyHeight : 0.8f * scale, 0f);
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            Health = gameObject.AddComponent<Health>();
            Health.flying = def.flying;
            Health.elite = elite;
            Health.boss = def.boss;
            Health.radius = 0.45f * scale;
            Health.displayName = def.name;
            Health.Init(Team.Enemy, def.hp * hpMult, def.armor + run.Mods.Get(Stat.EnemyArmor, 0f));
            Health.RefreshColliders();
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;

            damage = def.damage * dmgMult;
            speed = def.speed * spdMult;
            range = def.attackRange * (def.ranged ? run.Mods.Mul(Stat.EnemyRange) : 1f);

            if (!def.flying)
            {
                agent = gameObject.AddComponent<NavMeshAgent>();
                agent.radius = Mathf.Clamp(0.4f * scale, 0.3f, 1.2f);
                agent.height = 1.6f * scale;
                agent.speed = speed;
                agent.angularSpeed = 540f;
                agent.acceleration = 20f;
                agent.autoBraking = false;
                agent.obstacleAvoidanceType = def.boss ? ObstacleAvoidanceType.NoObstacleAvoidance : ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.avoidancePriority = def.boss ? 10 : Random.Range(40, 90);
            }
            if (def.boss) gameObject.AddComponent<BossBrain>().Setup(this);
            run.Register(this);
            Tweener.ScaleIn(model.transform, 0.3f);
            StartCoroutine(Scan());
        }

        // ---- targeting -------------------------------------------------------------------

        IEnumerator Scan()
        {
            while (true)
            {
                if (Alive) { PickTarget(); UpdateVisibility(); }
                yield return scanWait;
            }
        }

        void PickTarget()
        {
            var run = Run.Current;
            if (run == null) return;
            bool kamikaze = Def.special == "kamikaze";
            float aggro = 5f + range;

            // Iron Roughnecks taunt: enemies near one must attack it.
            Health taunt = null;
            if (!Def.flying)
            {
                foreach (var u in run.Units)
                    if (u != null && u.Alive && u.Def.special == "taunt" && u.Health.DistanceTo(transform.position) < u.Def.specialValue)
                    { taunt = u.Health; break; }
            }
            if (taunt != null) { target = taunt; return; }

            // Keep the current target while it's alive and still close.
            if (target != null && target.Alive && !target.isRig && target.DistanceTo(transform.position) < aggro + 2f) return;

            var found = Health.Nearest(transform.position, aggro, Team.Player, Def.ranged || Def.flying, true,
                h => kamikaze ? (h.isBuilding ? 0f : 8f) : (h.isBuilding ? 2.5f : 0f));
            if (found != null) { target = found; return; }

            // Blocked by a wall? Attack the nearest building in reach.
            if (agent != null && agent.isOnNavMesh && !agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathPartial && agent.remainingDistance < 2f)
            {
                var wall = Health.Nearest(transform.position, 4f, Team.Player, false, true, h => h.isBuilding ? 0f : 5f);
                if (wall != null) { target = wall; return; }
            }
            target = run.Rig != null && run.Rig.Health.Alive ? run.Rig.Health : (run.Hero != null ? run.Hero.Health : null);
        }

        /// <summary>Tight Hole mutator: enemies are only visible near your hero and buildings.</summary>
        void UpdateVisibility()
        {
            var run = Run.Current;
            bool show = true;
            if (run != null && run.IsNight && !Boss && run.Mods.HasSpecial("fog"))
            {
                float r = run.Mods.Get(Stat.VisionRadius, 14f);
                Vector3 p = transform.position;
                show = run.Hero != null && MapMath.Flat(run.Hero.transform.position - p).sqrMagnitude < r * r;
                if (!show)
                    foreach (var b in run.Buildings)
                        if (b != null && !b.Destroyed && b.Health != null && b.Health.DistanceTo(p) < r) { show = true; break; }
            }
            if (show != hidden) return;
            hidden = !show;
            foreach (var mr in renderers) if (mr != null) mr.enabled = show;
        }

        // ---- behaviour -------------------------------------------------------------------

        void Update()
        {
            if (dying || reviving || !Alive) return;
            float dt = Time.deltaTime;
            cooldown -= dt;

            if (knock.sqrMagnitude > 0.001f)
            {
                Vector3 step = knock * dt * 8f;
                if (agent != null && agent.isOnNavMesh) agent.Move(step); else transform.position += step;
                knock = Vector3.Lerp(knock, Vector3.zero, dt * 8f);
            }

            if (Health.Stunned) { if (agent != null && agent.isOnNavMesh) agent.isStopped = true; return; }
            float slow = Health.SlowFactor * KickDetection.SlowFactor();
            if (agent != null)
            {
                if (!agent.isOnNavMesh) { RecoverNavMesh(); return; }
                agent.isStopped = false;
                agent.speed = speed * slow;
            }
            if (target == null || !target.Alive) { PickTarget(); if (target == null) return; }

            float dist = target.DistanceTo(transform.position);
            if (dist <= range)
            {
                if (agent != null && agent.hasPath) agent.ResetPath();
                Face(target.transform.position);
                if (cooldown <= 0f) Attack();
                return;
            }

            if (Def.flying)
            {
                Vector3 to = MapMath.Flat(target.transform.position - transform.position);
                transform.position += to.normalized * speed * slow * dt;
                Face(target.transform.position);
                if (model != null) model.transform.localPosition = Vector3.up * (flyHeight + Mathf.Sin(Time.time * 4f + GetInstanceID()) * 0.25f);
            }
            else
            {
                if ((agent.destination - target.transform.position).sqrMagnitude > 1f) agent.SetDestination(target.transform.position);
                if (model != null) model.transform.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * speed * 2.2f)) * 0.12f;
            }
        }

        void RecoverNavMesh()
        {
            stuck += Time.deltaTime;
            if (stuck < 0.5f) return;
            stuck = 0f;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 12f, NavMesh.AllAreas)) agent.Warp(hit.position);
        }

        void Face(Vector3 p)
        {
            Vector3 d = MapMath.Flat(p - transform.position);
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 10f);
        }

        void Attack()
        {
            var run = Run.Current;
            cooldown = 1f / Mathf.Max(0.1f, Def.attackRate);
            float dmg = damage;
            if (target.isBuilding) dmg *= Def.buildingDamage * run.Mods.Mul(Stat.EnemyBuildingDamage);
            var d = DamageInfo.Simple(dmg, KillSource.Other, gameObject);
            d.fromAir = Def.flying;
            d.knockback = Def.boss ? 4f : 1f;
            if (Def.special == "poison") { d.poisonDps = Def.specialValue; d.poisonTime = 4f; }
            if (Def.special == "slowOnHit") { d.slow = Def.specialValue; d.slowTime = 2f; }

            if (Def.special == "kamikaze")
            {
                d.amount = dmg;
                Combat.Splash(transform.position + Vector3.up * 0.5f, Def.specialValue, Team.Player, true, d, FxKind.Explosion);
                Fx.Shake(0.25f);
                dying = true;
                Health.Kill();
                return;
            }
            if (Def.ranged)
            {
                d.projectile = true;
                Projectile.Launch(new ProjectileSpec
                {
                    from = transform.position + Vector3.up * 1.2f,
                    target = target,
                    targetPoint = target.transform.position,
                    speed = Def.projectileSpeed,
                    maxRange = range,
                    targetTeam = Team.Player,
                    damage = d,
                    color = new Color(0.55f, 0.45f, 0.35f),
                    size = 0.3f,
                    impactFx = FxKind.Dust,
                    impactSound = "sfx_hit",
                });
                AudioService.Sfx("sfx_lob", transform.position, 1.2f, 0.4f);
            }
            else
            {
                Combat.Hit(target, d, target.isBuilding ? FxKind.Dust : FxKind.Hit, target.isBuilding ? "" : "sfx_hit");
                if (Def.special == "poison") Fx.Emit(FxKind.Poison, target.transform.position + Vector3.up, 6);
            }
            if (model != null) Tweener.Punch(model.transform, 0.2f, 0.2f);
        }

        // ---- damage & death -------------------------------------------------------------

        void OnDamaged(Health h, DamageInfo d)
        {
            flash.Flash();
            if (d.knockback > 0f && !Def.boss)
            {
                Vector3 dir = MapMath.Flat(transform.position - d.from);
                if (dir.sqrMagnitude < 0.001f) dir = -transform.forward;
                knock += dir.normalized * Mathf.Min(6f, d.knockback) * (Elite ? 0.5f : 1f) * 0.3f;
            }
            if (d.source == KillSource.Hero && d.amount > 25f) Fx.HitStop(0.035f);
        }

        void OnDied(Health h, DamageInfo d)
        {
            var run = Run.Current;
            // Re-Pressurization mutator: a chance to come back once at half health.
            // (Never for a kamikaze that just blew itself up: it is already 'dying'.)
            if (!revived && !dying && !Def.boss && run != null && Random.value < run.Mods.Get(Stat.EnemyRevive, 0f))
            {
                revived = true;
                StartCoroutine(ReviveSoon());
                return;
            }
            dying = true;
            if (run != null)
            {
                run.OnEnemyKilled(this, d.source);
                if (Def.special == "split" && !string.IsNullOrEmpty(Def.spawns))
                {
                    var child = Game.Db.Enemy(Def.spawns);
                    for (int i = 0; i < Mathf.Max(1, (int)Def.specialValue); i++)
                        if (child != null) Spawn(child, transform.position + Random.insideUnitSphere * 1.2f, false, run.Plan);
                }
            }
            Fx.Emit(Def.flying ? FxKind.Poison : FxKind.Oil, transform.position + Vector3.up * 0.8f, Def.boss ? 60 : 12);
            AudioService.Sfx(Def.boss ? "sfx_explosion" : "sfx_enemy_die", transform.position, Random.Range(0.85f, 1.15f), 0.8f);
            if (Def.boss) { Fx.Emit(FxKind.Explosion, transform.position + Vector3.up * 2f, 50); Fx.Shake(1f); Fx.HitStop(0.15f); }
            if (agent != null) agent.enabled = false;
            Tweener.ScaleOut(model.transform, 0.25f, () => { if (this != null) Destroy(gameObject); });
        }

        IEnumerator ReviveSoon()
        {
            reviving = true;
            Fx.Emit(FxKind.Oil, transform.position + Vector3.up, 10);
            if (model != null) model.SetActive(false);
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            yield return new WaitForSeconds(1.2f);
            if (model != null) model.SetActive(true);
            Health.Revive(0.5f);
            reviving = false;
            Fx.Emit(FxKind.Smoke, transform.position + Vector3.up, 12);
            AudioService.Sfx("sfx_enemy_spawn", transform.position);
        }

        void OnDestroy()
        {
            if (Run.Current != null) Run.Current.Unregister(this);
        }
    }

    /// <summary>Kick Detection perk: enemies are slowed by 40% for the first 6 seconds of each night.</summary>
    public static class KickDetection
    {
        public static float NightStartTime = -99f;
        public static float SlowFactor()
        {
            var run = Run.Current;
            if (run == null || !run.IsNight || !run.Mods.HasSpecial("kickDetection")) return 1f;
            return Time.time - NightStartTime < 6f ? 0.6f : 1f;
        }
    }
}
