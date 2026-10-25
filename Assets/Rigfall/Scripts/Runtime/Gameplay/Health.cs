// Health and status effects for everything that can be hurt: hero, units, enemies and
// buildings. Also keeps a registry (collider -> Health) so physics queries such as
// Physics.OverlapSphere can find targets without GetComponent calls.
using System;
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public enum Team { Player, Enemy }

    public struct DamageInfo
    {
        public float amount;
        public KillSource source;
        public GameObject attacker;
        public Vector3 from;
        public float knockback;
        public bool crit;
        public bool fromAir;
        public bool projectile;
        public float armorPierce;     // 0..1 share of armour ignored
        public float burnDps, burnTime;
        public float slow, slowTime;
        public float stunTime;
        public float poisonDps, poisonTime;
        public float bonusVsElite;    // multiplier vs elites/bosses (Kick Hunters)

        public static DamageInfo Simple(float amount, KillSource source, GameObject attacker)
        {
            var d = new DamageInfo { amount = amount, source = source, attacker = attacker, bonusVsElite = 1f };
            if (attacker != null) d.from = attacker.transform.position;
            return d;
        }
    }

    public class Health : MonoBehaviour
    {
        // ---- registry -------------------------------------------------------------------
        public static readonly List<Health> All = new List<Health>();
        static readonly Dictionary<Collider, Health> byCollider = new Dictionary<Collider, Health>();

        public static Health Of(Collider c)
        {
            Health h;
            return c != null && byCollider.TryGetValue(c, out h) ? h : null;
        }

        // ---- identity -------------------------------------------------------------------
        public Team team;
        public bool isBuilding, isHero, isRig, isUnit, flying, elite, boss;
        public float radius = 0.5f;           // used for "distance to edge" targeting
        public string displayName = "";

        // ---- values ---------------------------------------------------------------------
        public float max = 100f;
        public float current = 100f;
        public float armor;
        public bool invulnerable;

        public bool Alive => !dead && current > 0f;
        public float Fraction => max <= 0 ? 0 : Mathf.Clamp01(current / max);

        public event Action<Health, DamageInfo> Damaged;
        public event Action<Health, DamageInfo> Died;
        public event Action<Health> Healed;

        bool dead;
        DamageInfo lastHit;

        // ---- status effects -------------------------------------------------------------
        float burnDps, burnLeft, poisonDps, poisonLeft, slowAmount, slowLeft, stunLeft, tick;
        public bool Stunned => stunLeft > 0f;
        public bool Burning => burnLeft > 0f;
        public bool Poisoned => poisonLeft > 0f;
        /// <summary>Movement/attack speed multiplier from slows (1 = normal).</summary>
        public float SlowFactor => slowLeft > 0f ? 1f - slowAmount : 1f;
        public float LastDamageTime { get; private set; } = -99f;

        Collider[] colliders = new Collider[0];

        protected virtual void OnEnable()
        {
            All.Add(this);
            colliders = GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders) if (c != null) byCollider[c] = this;
        }

        protected virtual void OnDisable()
        {
            All.Remove(this);
            foreach (var c in colliders) if (c != null && byCollider.ContainsKey(c) && byCollider[c] == this) byCollider.Remove(c);
        }

        /// <summary>Call after adding colliders at runtime so the registry sees them.</summary>
        public void RefreshColliders()
        {
            OnDisable();
            if (isActiveAndEnabled) OnEnable();
        }

        public void Init(Team team, float maxHp, float armor)
        {
            this.team = team;
            max = Mathf.Max(1f, maxHp);
            current = max;
            this.armor = armor;
            dead = false;
            burnLeft = poisonLeft = slowLeft = stunLeft = 0f;
        }

        public void SetMax(float newMax, bool fill)
        {
            float frac = Fraction;
            max = Mathf.Max(1f, newMax);
            current = fill ? max : Mathf.Max(1f, frac * max);
        }

        public void TakeDamage(DamageInfo d)
        {
            if (!Alive || invulnerable || d.amount <= 0f) return;
            float amount = d.amount;
            if ((elite || boss) && d.bonusVsElite > 1f) amount *= d.bonusVsElite;

            // Run-wide modifiers on the player's side (Deluge System, H2S Scrubbers, medic shields).
            var run = Run.Current;
            if (team == Team.Player && run != null)
            {
                if (isBuilding) amount *= run.Mods.Mul(Stat.DamageTakenBuilding);
                if (d.fromAir) amount *= run.Mods.Mul(Stat.DamageTakenAir);
                if (d.projectile && !isBuilding && HseMedicAura.Covers(transform.position)) amount *= 0.7f;
            }

            float effectiveArmor = Mathf.Max(0f, armor * (1f - Mathf.Clamp01(d.armorPierce)));
            amount = Mathf.Max(amount * 0.2f, amount - effectiveArmor);
            current -= amount;
            LastDamageTime = Time.time;
            lastHit = d;
            lastHit.amount = amount;

            if (d.burnDps > 0f && d.burnTime > 0f) { burnDps = Mathf.Max(burnDps, d.burnDps); burnLeft = Mathf.Max(burnLeft, d.burnTime); }
            if (d.poisonDps > 0f && d.poisonTime > 0f) { poisonDps = Mathf.Max(poisonDps, d.poisonDps); poisonLeft = Mathf.Max(poisonLeft, d.poisonTime); }
            if (d.slow > 0f && d.slowTime > 0f && !isBuilding) { slowAmount = Mathf.Clamp(Mathf.Max(slowAmount, d.slow), 0f, boss ? 0.35f : 0.8f); slowLeft = Mathf.Max(slowLeft, d.slowTime); }
            if (d.stunTime > 0f && !isBuilding) stunLeft = Mathf.Max(stunLeft, boss ? d.stunTime * 0.3f : d.stunTime);

            Damaged?.Invoke(this, lastHit);
            if (current <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (!Alive || amount <= 0f) return;
            current = Mathf.Min(max, current + amount);
            Healed?.Invoke(this);
        }

        public void HealFull()
        {
            if (dead) return;
            current = max;
            burnLeft = poisonLeft = slowLeft = stunLeft = 0f;
            Healed?.Invoke(this);
        }

        public void Kill() { if (Alive) { current = 0; Die(); } }

        void Die()
        {
            if (dead) return;
            dead = true;
            current = 0f;
            Died?.Invoke(this, lastHit);
        }

        /// <summary>Bring a dead entity back (dawn rebuilds, hero respawn, Re-Pressurization).</summary>
        public void Revive(float fraction)
        {
            dead = false;
            current = Mathf.Max(1f, max * Mathf.Clamp01(fraction));
            burnLeft = poisonLeft = slowLeft = stunLeft = 0f;
            Healed?.Invoke(this);
        }

        public void ClearStatus() { burnLeft = poisonLeft = slowLeft = stunLeft = 0f; }

        protected virtual void Update()
        {
            if (!Alive) return;
            float dt = Time.deltaTime;
            if (slowLeft > 0f) slowLeft -= dt;
            if (stunLeft > 0f) stunLeft -= dt;
            if (burnLeft <= 0f && poisonLeft <= 0f) return;
            tick += dt;
            if (tick < 0.5f) return;
            float step = tick;
            tick = 0f;
            float dot = 0f;
            if (burnLeft > 0f) { dot += burnDps * step; burnLeft -= step; }
            if (poisonLeft > 0f) { dot += poisonDps * step; poisonLeft -= step; }
            if (dot > 0f)
            {
                var d = DamageInfo.Simple(dot, lastHit.source, lastHit.attacker);
                d.armorPierce = 1f; // damage over time ignores armour
                d.from = transform.position;
                TakeDamage(d);
                if (Burning) Fx.Emit(FxKind.Fire, transform.position + Vector3.up * 0.8f, 2);
                if (Poisoned) Fx.Emit(FxKind.Poison, transform.position + Vector3.up * 0.8f, 2);
            }
        }

        // ---- queries --------------------------------------------------------------------

        static readonly Collider[] buffer = new Collider[256];

        /// <summary>
        /// Every living Health of a team within a radius, found with Physics.OverlapSphere.
        /// The list is reused: copy it if you need to keep it.
        /// </summary>
        public static List<Health> InRadius(Vector3 center, float radius, Team team, bool includeAir, bool includeBuildings, List<Health> into)
        {
            into.Clear();
            int n = Physics.OverlapSphereNonAlloc(center, radius, buffer, Physics.AllLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var h = Of(buffer[i]);
                if (h == null || !h.Alive || h.team != team || into.Contains(h)) continue;
                if (h.flying && !includeAir) continue;
                if (h.isBuilding && !includeBuildings) continue;
                into.Add(h);
            }
            return into;
        }

        /// <summary>Closest living target of a team within range (measured to the target's edge).</summary>
        public static Health Nearest(Vector3 from, float range, Team team, bool includeAir, bool includeBuildings, Func<Health, float> scorePenalty = null)
        {
            Health best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < All.Count; i++)
            {
                var h = All[i];
                if (h == null || !h.Alive || h.team != team) continue;
                if (h.flying && !includeAir) continue;
                if (h.isBuilding && !includeBuildings) continue;
                Vector3 d = h.transform.position - from;
                d.y = 0f;
                float dist = d.magnitude - h.radius;
                if (dist > range) continue;
                float score = dist + (scorePenalty != null ? scorePenalty(h) : 0f);
                if (score < bestScore) { bestScore = score; best = h; }
            }
            return best;
        }

        public float DistanceTo(Vector3 p)
        {
            Vector3 d = transform.position - p;
            d.y = 0f;
            return Mathf.Max(0f, d.magnitude - radius);
        }
    }
}
