// Pooled projectiles for towers, ranged units, ranged enemies and the hero.
// Three flight modes: homing (follows its target), piercing (straight line through
// several enemies) and lob (parabolic arc that bursts where it lands).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class ProjectileSpec
    {
        public Vector3 from;
        public Health target;
        public Vector3 targetPoint;
        public Vector3 direction;     // for piercing shots
        public float speed = 20f;
        public float maxRange = 20f;
        public bool lob;
        public float arcHeight = 3f;
        public int pierce;
        public float splash;
        public bool straight;          // fly in a straight line (skill shots); pierce > 0 also implies straight
        public bool canHitAir = true;
        public Team targetTeam = Team.Enemy;
        public DamageInfo damage;
        public Color color = Color.white;
        public float size = 0.25f;
        public FxKind impactFx = FxKind.Spark;
        public string impactSound = "sfx_hit";
    }

    public sealed class Projectile : MonoBehaviour
    {
        static readonly Stack<Projectile> pool = new Stack<Projectile>();
        static Transform poolRoot;
        static readonly List<Health> scratch = new List<Health>();

        ProjectileSpec spec;
        Vector3 start, lastTargetPos;
        float travelled, flightTime, t;
        readonly HashSet<Health> hitAlready = new HashSet<Health>();
        int pierceLeft;
        MeshRenderer meshRenderer;

        public static void Launch(ProjectileSpec spec)
        {
            var p = pool.Count > 0 ? pool.Pop() : Create();
            p.Begin(spec);
        }

        public static void ClearAll()
        {
            if (poolRoot == null) return;
            foreach (Transform child in poolRoot)
            {
                var p = child.GetComponent<Projectile>();
                if (p != null && p.gameObject.activeSelf) p.Recycle();
            }
        }

        static Projectile Create()
        {
            if (poolRoot == null)
            {
                poolRoot = new GameObject("Projectiles").transform;
                DontDestroyOnLoad(poolRoot.gameObject);
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "Projectile";
            go.transform.SetParent(poolRoot, false);
            var p = go.AddComponent<Projectile>();
            p.meshRenderer = go.GetComponent<MeshRenderer>();
            p.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.meshRenderer.receiveShadows = false;
            return p;
        }

        void Begin(ProjectileSpec s)
        {
            spec = s;
            start = s.from;
            transform.position = s.from;
            transform.localScale = Vector3.one * s.size;
            meshRenderer.sharedMaterial = Materials.Glow(s.color);
            travelled = 0f;
            t = 0f;
            hitAlready.Clear();
            pierceLeft = s.pierce;
            lastTargetPos = s.target != null ? Aim(s.target) : s.targetPoint;
            if (s.lob)
            {
                float dist = Vector3.Distance(Flat(start), Flat(lastTargetPos));
                flightTime = Mathf.Clamp(dist / Mathf.Max(1f, s.speed), 0.25f, 1.6f);
            }
            gameObject.SetActive(true);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        static Vector3 Aim(Health h) => h.transform.position + Vector3.up * (h.flying ? 0.2f : 0.8f);

        void Update()
        {
            if (spec == null) { Recycle(); return; }
            float dt = Time.deltaTime;
            if (spec.lob) UpdateLob(dt);
            else if (spec.straight || spec.pierce > 0) UpdatePierce(dt);
            else UpdateHoming(dt);
        }

        void UpdateLob(float dt)
        {
            t += dt / flightTime;
            if (spec.target != null && spec.target.Alive) lastTargetPos = spec.target.transform.position;
            Vector3 p = Vector3.Lerp(start, lastTargetPos, t);
            p.y += Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * spec.arcHeight;
            transform.position = p;
            if (t >= 1f) Impact(lastTargetPos, null);
        }

        void UpdateHoming(float dt)
        {
            if (spec.target != null && spec.target.Alive) lastTargetPos = Aim(spec.target);
            Vector3 to = lastTargetPos - transform.position;
            float step = spec.speed * dt;
            if (to.magnitude <= step + 0.15f)
            {
                transform.position = lastTargetPos;
                Impact(lastTargetPos, spec.target != null && spec.target.Alive ? spec.target : null);
                return;
            }
            transform.position += to.normalized * step;
            travelled += step;
            if (travelled > spec.maxRange * 2.5f) Recycle();
        }

        void UpdatePierce(float dt)
        {
            float step = spec.speed * dt;
            transform.position += spec.direction * step;
            travelled += step;
            Health.InRadius(transform.position, 0.7f, spec.targetTeam, spec.canHitAir, true, scratch);
            foreach (var h in scratch)
            {
                if (hitAlready.Contains(h)) continue;
                hitAlready.Add(h);
                if (spec.splash > 0f)
                {
                    Combat.Splash(transform.position, spec.splash, spec.targetTeam, spec.canHitAir, spec.damage, spec.impactFx);
                    Recycle();
                    return;
                }
                Combat.Hit(h, spec.damage, spec.impactFx, spec.impactSound);
                if (pierceLeft-- <= 0) { Recycle(); return; }
            }
            if (travelled > spec.maxRange)
            {
                if (spec.splash > 0f) Combat.Splash(transform.position, spec.splash, spec.targetTeam, spec.canHitAir, spec.damage, spec.impactFx);
                Recycle();
            }
        }

        void Impact(Vector3 point, Health direct)
        {
            if (spec.splash > 0f)
            {
                Combat.Splash(point, spec.splash, spec.targetTeam, spec.canHitAir, spec.damage, spec.impactFx);
            }
            else if (direct != null)
            {
                Combat.Hit(direct, spec.damage, spec.impactFx, spec.impactSound);
            }
            Recycle();
        }

        void Recycle()
        {
            spec = null;
            gameObject.SetActive(false);
            if (!pool.Contains(this)) pool.Push(this);
        }
    }

    /// <summary>Shared damage helpers so every attacker applies hits, splash and juice the same way.</summary>
    public static class Combat
    {
        static readonly List<Health> scratch = new List<Health>();

        public static void Hit(Health target, DamageInfo d, FxKind fx = FxKind.Spark, string sound = "sfx_hit")
        {
            if (target == null || !target.Alive) return;
            target.TakeDamage(d);
            Vector3 at = target.transform.position + Vector3.up * (target.isBuilding ? 1.2f : 0.8f);
            Fx.Emit(fx, at, d.crit ? 10 : 5);
            if (!string.IsNullOrEmpty(sound)) AudioService.Sfx(sound, at, d.crit ? 1.15f : 1f);
            if (d.amount >= 1f) Ui.Floating(at, Mathf.RoundToInt(d.amount).ToString() + (d.crit ? "!" : ""), d.crit ? Palette.Crit : (target.team == Team.Player ? Palette.Hurt : Color.white), d.crit ? 1.3f : 1f);
        }

        /// <summary>Damage everything of a team within a radius. Returns how many were hit.</summary>
        public static int Splash(Vector3 center, float radius, Team team, bool includeAir, DamageInfo d, FxKind fx = FxKind.Explosion)
        {
            Fx.Emit(fx, center + Vector3.up * 0.4f, 14);
            AudioService.Sfx("sfx_explosion", center, UnityEngine.Random.Range(0.9f, 1.1f));
            Health.InRadius(center, radius, team, includeAir, true, scratch);
            int count = 0;
            var copy = scratch.ToArray(); // TakeDamage can kill and change the registry
            foreach (var h in copy)
            {
                var dd = d;
                dd.from = center;
                h.TakeDamage(dd);
                if (dd.amount >= 1f) Ui.Floating(h.transform.position + Vector3.up, Mathf.RoundToInt(dd.amount).ToString(), team == Team.Player ? Palette.Hurt : Color.white, 0.9f);
                count++;
            }
            return count;
        }
    }
}
