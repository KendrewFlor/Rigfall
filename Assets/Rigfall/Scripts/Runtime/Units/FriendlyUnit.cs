// A crew member from a unit building. Uses a NavMeshAgent and a small state machine:
//   Defend  - guard its post next to the building, engage enemies within a leash radius
//   Follow  - follow the hero (set by the Follow & Hold command), hitting anything in reach
//   Hold    - guard the spot where the command button was released
// Targets are found by a coroutine every 0.2 s rather than every frame.
using System.Collections;
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Rigfall.Runtime
{
    public sealed class FriendlyUnit : MonoBehaviour
    {
        public enum Mode { Defend, Follow, Hold }

        public UnitDef Def { get; private set; }
        public Barracks Home { get; private set; }
        public Health Health { get; private set; }
        public Mode CurrentMode { get; private set; } = Mode.Defend;
        public bool Alive => Health != null && Health.Alive;

        NavMeshAgent agent;
        GameObject model;
        HitFlash flash;
        Vector3 post, holdPoint, followOffset;
        Transform followTarget;
        Health target;
        float bonus = 1f, damage, range, speed, attackRate, cooldown, healTick, stuckTimer;
        bool dead;
        static readonly WaitForSeconds scanWait = new WaitForSeconds(0.2f);
        static readonly List<Health> scratch = new List<Health>();
        const float Leash = 7f;

        public static FriendlyUnit Spawn(UnitDef def, Barracks home, Vector3 post, float bonus)
        {
            var go = new GameObject(def.name);
            go.transform.SetParent(Run.Current.Field.UnitRoot, false);
            go.transform.position = MapMath.OnNavMesh(home.transform.position + home.transform.forward * (home.Def.footprint + 1f));
            var u = go.AddComponent<FriendlyUnit>();
            u.Setup(def, home, post, bonus);
            return u;
        }

        void Setup(UnitDef def, Barracks home, Vector3 post, float bonus)
        {
            Def = def;
            Home = home;
            this.post = post;
            this.bonus = bonus;
            model = Models.Spawn(def.model, transform);
            model.transform.localScale = Vector3.one * def.scale;
            if (def.flying) model.transform.localPosition = Vector3.up * 2.2f;
            flash = gameObject.AddComponent<HitFlash>();
            flash.Bind(model, Materials.Lit(Color.white));

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.45f * def.scale;
            col.height = 1.8f * def.scale;
            col.center = new Vector3(0f, def.flying ? 2.2f : 0.9f * def.scale, 0f);
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            Health = gameObject.AddComponent<Health>();
            Health.isUnit = true;
            Health.flying = def.flying;
            Health.radius = 0.45f * def.scale;
            Health.displayName = def.name;
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;

            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f * Mathf.Max(1f, def.scale);
            agent.height = 1.8f;
            agent.angularSpeed = 720f;
            agent.acceleration = 30f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(30, 70);

            RefreshStats();
            Health.Init(Team.Player, Health.max, Health.armor);
            Health.RefreshColliders();
            if (def.special == "heal") HseMedicAura.Register(this);
            Run.Current.Register(this);
            StartCoroutine(Scan());
        }

        public void SetBonus(float b) { bonus = b; RefreshStats(); }
        public void SetPost(Vector3 p) { post = p; }

        public void RefreshStats()
        {
            var mods = Run.Current != null ? Run.Current.Mods : new ModifierSet();
            float hp = Def.hp * bonus * mods.Mul(Stat.UnitHp);
            if (Health.max != hp) Health.SetMax(hp, false);
            Health.armor = Def.armor + mods.Get(Stat.UnitArmor, 0f);
            damage = Def.damage * bonus * mods.Mul(Stat.UnitDamage);
            range = Def.attackRange * (Def.ranged ? mods.Mul(Stat.UnitRange) : 1f);
            speed = Def.speed * mods.Mul(Stat.UnitSpeed);
            attackRate = Def.attackRate;
            if (agent != null) agent.speed = speed;
        }

        // ---- commands -------------------------------------------------------------------

        public void Follow(Transform who, Vector3 offset)
        {
            if (!Alive) return;
            CurrentMode = Mode.Follow;
            followTarget = who;
            followOffset = offset;
            target = null;
        }

        public void Hold(Vector3 at)
        {
            CurrentMode = Mode.Hold;
            holdPoint = MapMath.OnNavMesh(at);
            followTarget = null;
        }

        public void ReturnToPost()
        {
            CurrentMode = Mode.Defend;
            followTarget = null;
        }

        Vector3 Anchor => CurrentMode == Mode.Hold ? holdPoint : post;

        // ---- targeting (every 0.2 s) ------------------------------------------------------

        IEnumerator Scan()
        {
            while (true)
            {
                if (Alive) PickTarget();
                yield return scanWait;
            }
        }

        void PickTarget()
        {
            bool air = Def.canHitAir;
            if (CurrentMode == Mode.Follow)
            {
                // While following, only fight what is already in reach.
                target = Health.Nearest(transform.position, range + 0.5f, Team.Enemy, air, false);
                return;
            }
            if (target != null && target.Alive && MapMath.Flat(target.transform.position - Anchor).magnitude <= Leash + range) return;
            target = Health.Nearest(Anchor, Leash + range, Team.Enemy, air, false);
        }

        // ---- behaviour ------------------------------------------------------------------

        void Update()
        {
            if (dead || Run.Current == null) return;
            cooldown -= Time.deltaTime;
            if (Def.special == "heal") HealAllies();
            if (Health.Stunned) { if (agent.isOnNavMesh) agent.isStopped = true; return; }
            if (agent.isOnNavMesh) agent.isStopped = false;
            agent.speed = speed * Health.SlowFactor;

            if (target != null && target.Alive)
            {
                float dist = target.DistanceTo(transform.position);
                if (dist <= range)
                {
                    Stop();
                    Face(target.transform.position);
                    if (cooldown <= 0f) Attack();
                    return;
                }
                if (CurrentMode != Mode.Follow) { MoveTo(target.transform.position); return; }
            }

            if (CurrentMode == Mode.Follow && followTarget != null)
            {
                Vector3 goal = followTarget.position + followTarget.rotation * followOffset;
                if (MapMath.Flat(goal - transform.position).sqrMagnitude > 0.6f) MoveTo(goal); else Stop();
            }
            else
            {
                if (MapMath.Flat(Anchor - transform.position).sqrMagnitude > 0.5f) MoveTo(Anchor); else Stop();
            }
        }

        void MoveTo(Vector3 p)
        {
            if (!agent.isOnNavMesh)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > 1f) { agent.Warp(MapMath.OnNavMesh(transform.position, 8f)); stuckTimer = 0f; }
                return;
            }
            stuckTimer = 0f;
            agent.stoppingDistance = 0.2f;
            if ((agent.destination - p).sqrMagnitude > 0.25f) agent.SetDestination(p);
        }

        void Stop()
        {
            if (agent.isOnNavMesh && agent.hasPath) agent.ResetPath();
        }

        void Face(Vector3 p)
        {
            Vector3 d = MapMath.Flat(p - transform.position);
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 12f);
        }

        float CurrentDamage()
        {
            float dmg = damage;
            if (Def.special == "berserk") dmg *= 1f + (1f - Health.Fraction);          // up to double when nearly dead
            if (Home != null && Rally.Active(transform.position)) dmg *= 1.4f;        // Toolpusher's Rally Horn
            return dmg;
        }

        void Attack()
        {
            cooldown = 1f / Mathf.Max(0.1f, attackRate);
            var d = DamageInfo.Simple(CurrentDamage(), Def.ranged ? KillSource.RangedUnit : KillSource.MeleeUnit, gameObject);
            d.bonusVsElite = Def.special == "monsterHunter" ? Def.specialValue : 1f;
            if (Def.special == "antiAir" && target.flying) d.amount *= Def.specialValue;
            if (Def.special == "burn") { d.burnDps = Def.specialValue; d.burnTime = 3f; }
            if (Def.special == "control") { d.slow = Def.specialValue; d.slowTime = 1.5f; d.knockback = 2.5f; }
            if (Def.special == "charge" && Vector3.Distance(transform.position, Anchor) > 3f) d.amount *= Def.specialValue;

            if (Def.ranged)
            {
                d.projectile = true;
                Projectile.Launch(new ProjectileSpec
                {
                    from = transform.position + Vector3.up * (Def.flying ? 2.2f : 1.3f),
                    target = target,
                    targetPoint = target.transform.position,
                    speed = Def.projectileSpeed,
                    maxRange = range,
                    lob = Def.splash > 0f,
                    arcHeight = 2f,
                    splash = Def.splash,
                    canHitAir = Def.canHitAir,
                    targetTeam = Team.Enemy,
                    damage = d,
                    color = Def.special == "burn" ? new Color(1f, 0.5f, 0.2f) : Def.special == "heal" ? new Color(0.5f, 1f, 0.7f) : new Color(0.7f, 0.9f, 1f),
                    size = 0.2f,
                    impactFx = Def.special == "burn" ? FxKind.Fire : FxKind.Spark,
                });
                AudioService.Sfx("sfx_shoot", transform.position, 1.15f, 0.55f);
            }
            else if (Def.splash > 0f)
            {
                d.knockback = 1.5f;
                Combat.Splash(transform.position + Vector3.up * 0.5f, Def.splash, Team.Enemy, false, d, FxKind.Hit);
                AudioService.Sfx("sfx_swing", transform.position, 0.9f, 0.6f);
            }
            else
            {
                Combat.Hit(target, d, FxKind.Hit, "sfx_hit");
            }
            if (model != null) Tweener.Punch(model.transform, 0.15f, 0.18f);
        }

        void HealAllies()
        {
            healTick -= Time.deltaTime;
            if (healTick > 0f || !Alive) return;
            healTick = 1f;
            Health.InRadius(transform.position, 5f, Team.Player, true, false, scratch);
            bool any = false;
            foreach (var h in scratch)
            {
                if (h.current >= h.max) continue;
                h.Heal(Def.specialValue * bonus);
                Fx.Emit(FxKind.Heal, h.transform.position + Vector3.up, 3);
                any = true;
            }
            if (any) AudioService.Sfx("sfx_heal", transform.position, 1.2f, 0.4f);
        }

        // ---- damage & death -------------------------------------------------------------

        void OnDamaged(Health h, DamageInfo d)
        {
            flash.Flash();
            if (d.knockback > 0f && agent.isOnNavMesh)
            {
                Vector3 dir = MapMath.Flat(transform.position - d.from).normalized;
                agent.Move(dir * Mathf.Min(1.2f, d.knockback * 0.25f));
            }
        }

        void OnDied(Health h, DamageInfo d)
        {
            dead = true;
            target = null;
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
            model.SetActive(false);
            Fx.Emit(FxKind.Dust, transform.position + Vector3.up * 0.6f, 10);
            AudioService.Sfx("sfx_hero_hurt", transform.position, 1.3f, 0.6f);
            if (Def.special == "berserk")
            {
                // The Wildcatter's sacrifice: heal everyone nearby.
                Health.InRadius(transform.position, 4f, Team.Player, true, false, scratch);
                foreach (var ally in scratch) ally.Heal(Def.specialValue);
                Fx.Emit(FxKind.Heal, transform.position + Vector3.up, 20);
            }
            if (Home != null) Home.OnUnitDied(this);
            if (Run.Current != null) Run.Current.OnUnitDied(this);
        }

        public void Respawn(Vector3 at)
        {
            Vector3 p = MapMath.OnNavMesh(at);
            transform.position = p;
            agent.enabled = true;
            agent.Warp(p);
            dead = false;
            Health.Revive(1f);
            model.SetActive(true);
            Tweener.ScaleIn(model.transform, 0.3f);
            Fx.Emit(FxKind.Build, p + Vector3.up * 0.5f, 6);
            if (CurrentMode == Mode.Follow) ReturnToPost();
        }

        public void OnDayStart() { }

        public void OnDawn(bool heal)
        {
            if (dead) Respawn(CurrentMode == Mode.Hold ? holdPoint : post);
            else if (heal) Health.HealFull();
            if (CurrentMode == Mode.Follow) ReturnToPost();
        }

        void OnDestroy()
        {
            HseMedicAura.Unregister(this);
            if (Run.Current != null) Run.Current.Unregister(this);
        }
    }

    /// <summary>HSE Medics hold up a blast shield: allies near a living medic take 30% less ranged damage.</summary>
    public static class HseMedicAura
    {
        static readonly List<FriendlyUnit> medics = new List<FriendlyUnit>();
        public static void Register(FriendlyUnit u) { if (!medics.Contains(u)) medics.Add(u); }
        public static void Unregister(FriendlyUnit u) { medics.Remove(u); }

        public static bool Covers(Vector3 p)
        {
            for (int i = medics.Count - 1; i >= 0; i--)
            {
                var m = medics[i];
                if (m == null) { medics.RemoveAt(i); continue; }
                if (m.Alive && (m.transform.position - p).sqrMagnitude < 4.5f * 4.5f) return true;
            }
            return false;
        }
    }

    /// <summary>The Toolpusher's Rally Horn: a temporary damage aura around a point.</summary>
    public static class Rally
    {
        static Vector3 center;
        static float until, radius;
        public static void Start(Vector3 at, float r, float seconds) { center = at; radius = r; until = Time.time + seconds; }
        public static bool Active(Vector3 p) => Time.time < until && (p - center).sqrMagnitude < radius * radius;
    }
}
