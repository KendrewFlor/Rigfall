// The player's hero: movement, the five weapon styles (melee arc, ground slam, ranged
// skill shots, mud spray, lobbed seismic pulses), a dash, the class ability, taking
// damage, going down and respawning at the rig.
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Rigfall.Runtime
{
    public sealed class Hero : MonoBehaviour
    {
        public ClassDef Class { get; private set; }
        public WeaponDef Weapon { get; private set; }
        public int WeaponLevel { get; private set; }
        public Health Health { get; private set; }
        public bool Alive => Health != null && Health.Alive;
        public bool Downed { get; private set; }
        public float RespawnIn => Downed ? Mathf.Max(0f, respawnAt - Time.time) : 0f;
        public bool WaitingForDawn { get; private set; }
        public float AbilityCooldown { get; private set; }
        public float AbilityReady => AbilityCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(abilityLeft / AbilityCooldown);
        public float AttackRange => range;

        CharacterController cc;
        GameObject model;
        Transform weaponPivot;
        HitFlash flash;
        CommandSystem commands;
        Vector3 velocity, facing = Vector3.forward, dashDir, spawn;
        float attackLeft, abilityLeft, dashLeft, dashCooldown, respawnAt, invulnerableUntil, swingAnim, yVelocity;
        float damage, rate, range, speed, crit, knockbackMult, burn, slow;
        bool abilityDash;
        readonly HashSet<Health> dashHits = new HashSet<Health>();
        static readonly List<Health> scratch = new List<Health>();

        public void Setup(ClassDef cls, WeaponDef weapon, int weaponLevel, Vector3 spawnPoint)
        {
            Class = cls;
            Weapon = weapon;
            WeaponLevel = weaponLevel;
            spawn = spawnPoint;
            transform.position = spawnPoint;

            cc = gameObject.AddComponent<CharacterController>();
            cc.radius = 0.45f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;

            model = Models.Spawn(cls.model, transform);
            var hand = Models.Anchor(cls.model, "hand", new Vector3(0.45f, 1.05f, 0.25f));
            weaponPivot = new GameObject("Weapon").transform;
            weaponPivot.SetParent(model.transform, false);
            weaponPivot.localPosition = hand;
            var weaponModel = Models.Spawn(weapon.model, weaponPivot);
            weaponModel.transform.localScale = Vector3.one * 0.9f;
            if (weapon.rarity == "brilliant" || weapon.rarity == "legendary")
                weaponModel.AddComponent<RarityGlow>().Setup(Palette.Rarity(weapon.rarity));

            flash = gameObject.AddComponent<HitFlash>();
            flash.Bind(model, Materials.Lit(Color.white));

            Health = gameObject.AddComponent<Health>();
            Health.isHero = true;
            Health.radius = 0.45f;
            Health.displayName = cls.name;
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
            RefreshStats();
            Health.Init(Team.Player, Health.max, Health.armor);
            Health.RefreshColliders();

            commands = gameObject.AddComponent<CommandSystem>();
            commands.baseRadius = cls.commandRadius;
            AbilityCooldown = cls.abilityCooldown;
        }

        public void RefreshStats()
        {
            var run = Run.Current;
            var mods = run != null ? run.Mods : new ModifierSet();
            damage = Weapon.damage * WeaponUpgrades.DamageMult(WeaponLevel) * mods.Mul(Stat.HeroDamage);
            rate = Weapon.rate * WeaponUpgrades.RateMult(WeaponLevel) * mods.Mul(Stat.HeroRate);
            range = Weapon.range * mods.Mul(Stat.HeroRange);
            speed = Class.moveSpeed * mods.Mul(Stat.HeroSpeed);
            crit = mods.Get(Stat.HeroCrit, 0f) + (Weapon.effect == "crit" ? Weapon.effectValue : 0f);
            knockbackMult = mods.Mul(Stat.HeroKnockback);
            burn = mods.Get(Stat.HeroBurn, 0f);
            slow = mods.Get(Stat.HeroSlow, 0f);
            float hp = Class.maxHp * mods.Mul(Stat.HeroHp);
            if (Health != null)
            {
                Health.SetMax(hp, false);
                Health.armor = Class.armor + mods.Get(Stat.HeroArmor, 0f);
            }
            AbilityCooldown = Class.abilityCooldown * mods.Mul(Stat.HeroAbilityCooldown);
        }

        // ---- update ---------------------------------------------------------------------

        void Update()
        {
            if (Game.InputBlocked || Run.Current == null) return;
            float dt = Time.deltaTime;
            if (Downed)
            {
                if (!WaitingForDawn && Time.time >= respawnAt) Respawn();
                return;
            }
            attackLeft -= dt;
            abilityLeft -= dt;
            dashCooldown -= dt;
            Health.invulnerable = Time.time < invulnerableUntil || dashLeft > 0f;

            Move(dt);
            Animate(dt);
            if (Health.Stunned) return;

            if (InputReader.DashPressed && dashCooldown <= 0f && dashLeft <= 0f) StartDash(false);
            if (InputReader.AbilityPressed)
            {
                if (abilityLeft <= 0f) UseAbility();
                else AudioService.Ui("sfx_buy_fail", 1.2f, 0.5f);
            }

            bool wantAttack = InputReader.AttackHeld;
            if (!wantAttack && Game.Settings.autoAttack && Run.Current.IsNight)
                wantAttack = Health.Nearest(transform.position, range + 0.5f, Team.Enemy, true, false) != null;
            if (wantAttack && attackLeft <= 0f && dashLeft <= 0f)
            {
                Attack();
                Tutorial.Notify("attack");
            }
        }

        void Move(float dt)
        {
            Vector2 input = InputReader.Move;
            Vector3 dir = new Vector3(input.x, 0f, input.y);
            if (dir.sqrMagnitude > 0.01f) Tutorial.Notify("move");
            float slowFactor = Health.SlowFactor * (Health.Stunned ? 0f : 1f);
            Vector3 targetVel = dir * speed * slowFactor;
            if (InputReader.AttackHeld && (Weapon.attack == "spray")) targetVel *= 0.6f;
            velocity = Vector3.MoveTowards(velocity, targetVel, speed * 8f * dt);

            if (dashLeft > 0f)
            {
                dashLeft -= dt;
                velocity = dashDir * (abilityDash ? 18f : 15f);
                if (abilityDash) DashHits();
            }

            yVelocity = cc.isGrounded ? -1f : yVelocity - 20f * dt;
            Vector3 before = transform.position;
            cc.Move((velocity + Vector3.up * yVelocity) * dt);

            // Don't let the hero leave the NavMesh (the deck edge offshore, the pad edge onshore).
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(transform.position, out hit, 1.5f, NavMesh.AllAreas)) Teleport(before);

            if (dir.sqrMagnitude > 0.01f && dashLeft <= 0f && !InputReader.AttackHeld) facing = dir.normalized;
            if (InputReader.AttackHeld || dashLeft > 0f) { var aim = AimDirection(); if (aim.sqrMagnitude > 0.01f && dashLeft <= 0f) facing = aim; }
            if (facing.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), dt * 18f);
        }

        void Teleport(Vector3 p)
        {
            cc.enabled = false;
            transform.position = p;
            cc.enabled = true;
        }

        void Animate(float dt)
        {
            if (model == null) return;
            float moving = Mathf.Clamp01(MapMath.Flat(velocity).magnitude / Mathf.Max(0.1f, speed));
            model.transform.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 11f)) * 0.12f * moving;
            model.transform.localRotation = Quaternion.Euler(moving * 8f, 0f, 0f);
            if (swingAnim > 0f)
            {
                swingAnim -= dt * 6f;
                float s = Mathf.Sin(Mathf.Clamp01(swingAnim) * Mathf.PI);
                weaponPivot.localRotation = Quaternion.Euler(-70f * s, 40f * s, 0f);
            }
            else weaponPivot.localRotation = Quaternion.identity;
        }

        /// <summary>Mouse direction when aiming with the mouse, otherwise auto-aim at the best enemy in front.</summary>
        Vector3 AimDirection()
        {
            if (InputReader.AimWithMouse && Camera.main != null)
            {
                var ray = Camera.main.ScreenPointToRay(InputReader.MouseScreen);
                var plane = new Plane(Vector3.up, new Vector3(0f, 1f, 0f));
                float enter;
                if (plane.Raycast(ray, out enter))
                {
                    Vector3 d = MapMath.Flat(ray.GetPoint(enter) - transform.position);
                    if (d.sqrMagnitude > 0.1f) return d.normalized;
                }
            }
            var best = BestTargetInFront(range + 4f);
            if (best != null) return MapMath.Flat(best.transform.position - transform.position).normalized;
            return facing;
        }

        Vector3 AimPoint(float maxRange)
        {
            if (InputReader.AimWithMouse && Camera.main != null)
            {
                var ray = Camera.main.ScreenPointToRay(InputReader.MouseScreen);
                var plane = new Plane(Vector3.up, Vector3.zero);
                float enter;
                if (plane.Raycast(ray, out enter))
                {
                    Vector3 p = ray.GetPoint(enter);
                    Vector3 d = MapMath.Flat(p - transform.position);
                    return transform.position + Vector3.ClampMagnitude(d, maxRange);
                }
            }
            var best = BestTargetInFront(maxRange + 2f);
            return best != null ? best.transform.position : transform.position + facing * Mathf.Min(maxRange, 7f);
        }

        Health BestTargetInFront(float maxRange)
        {
            Vector3 fwd = facing;
            return Health.Nearest(transform.position, maxRange, Team.Enemy, true, false,
                h => Vector3.Angle(fwd, MapMath.Flat(h.transform.position - transform.position)) * 0.06f);
        }

        // ---- attacks --------------------------------------------------------------------

        DamageInfo MakeDamage(float amount)
        {
            var d = DamageInfo.Simple(amount, KillSource.Hero, gameObject);
            d.crit = Random.value < crit;
            if (d.crit) d.amount *= 2f;
            d.knockback = Weapon.knockback * knockbackMult;
            d.bonusVsElite = 1f;
            switch (Weapon.effect)
            {
                case "burn": d.burnDps = Weapon.effectValue; d.burnTime = Weapon.effectDuration; break;
                case "slow": d.slow = Weapon.effectValue; d.slowTime = Weapon.effectDuration; break;
                case "armorPierce": d.armorPierce = Weapon.effectValue; break;
                case "stun":
                    if (Weapon.attack == "spray") { if (Random.value < Weapon.effectValue) d.stunTime = Weapon.effectDuration; }
                    else d.stunTime = Weapon.effectDuration;
                    break;
            }
            if (burn > 0f) { d.burnDps = Mathf.Max(d.burnDps, burn); d.burnTime = Mathf.Max(d.burnTime, 3f); }
            if (slow > 0f) { d.slow = Mathf.Max(d.slow, slow); d.slowTime = Mathf.Max(d.slowTime, 2f); }
            return d;
        }

        void Attack()
        {
            attackLeft = 1f / Mathf.Max(0.1f, rate);
            swingAnim = 1f;
            switch (Weapon.attack)
            {
                case "melee": Melee(); break;
                case "slam": Slam(); break;
                case "ranged": Shoot(); break;
                case "spray": Spray(); break;
                case "lob": Lob(); break;
            }
        }

        void Melee()
        {
            Vector3 fwd = facing;
            Health.InRadius(transform.position, range + 1f, Team.Enemy, true, false, scratch);
            var victims = scratch.ToArray();
            int hits = 0;
            foreach (var h in victims)
            {
                Vector3 to = MapMath.Flat(h.transform.position - transform.position);
                if (to.magnitude - h.radius > range) continue;
                if (Weapon.arc < 359f && Vector3.Angle(fwd, to) > Weapon.arc * 0.5f) continue;
                var d = MakeDamage(damage);
                Combat.Hit(h, d, FxKind.Spark, "sfx_hit");
                hits++;
            }
            for (int i = -2; i <= 2; i++)
                Fx.Emit(FxKind.Spark, transform.position + Vector3.up + Quaternion.Euler(0f, i * Weapon.arc * 0.2f, 0f) * fwd * range * 0.8f, 1);
            AudioService.Sfx("sfx_swing", transform.position, Random.Range(0.95f, 1.1f), 0.8f);
            if (hits > 0) Fx.Shake(0.06f + 0.02f * Mathf.Min(hits, 5));
        }

        void Slam()
        {
            Vector3 center = transform.position + facing * range * 0.75f;
            var d = MakeDamage(damage);
            Health.InRadius(center, Weapon.radius, Team.Enemy, false, false, scratch);
            var victims = scratch.ToArray();
            foreach (var h in victims) { var dd = d; dd.from = center; Combat.Hit(h, dd, FxKind.Hit, ""); }
            Fx.Emit(FxKind.Dust, center, 14);
            Fx.Emit(FxKind.Shockwave, center + Vector3.up * 0.1f, 12);
            AudioService.Sfx(victims.Length > 0 ? "sfx_hit_heavy" : "sfx_swing", center, Random.Range(0.85f, 1f));
            Fx.Shake(victims.Length > 0 ? 0.22f : 0.08f);
        }

        void Shoot()
        {
            Vector3 dir = AimDirection();
            int n = Mathf.Max(1, Weapon.projectiles);
            for (int i = 0; i < n; i++)
            {
                float angle = n == 1 ? 0f : (i - (n - 1) * 0.5f) * Weapon.spread;
                Vector3 d = Quaternion.Euler(0f, angle, 0f) * dir;
                var dmg = MakeDamage(damage);
                dmg.projectile = true;
                Projectile.Launch(new ProjectileSpec
                {
                    from = transform.position + Vector3.up * 1.2f + dir * 0.6f,
                    direction = d,
                    straight = true,
                    speed = Weapon.projectileSpeed,
                    maxRange = range,
                    pierce = Weapon.pierce,
                    splash = Weapon.radius,
                    canHitAir = true,
                    targetTeam = Team.Enemy,
                    damage = dmg,
                    color = Palette.Hex(Weapon.tint),
                    size = Weapon.radius > 0f ? 0.4f : 0.22f,
                    impactFx = Weapon.effect == "burn" ? FxKind.Fire : FxKind.Spark,
                });
            }
            Fx.Emit(FxKind.Spark, transform.position + Vector3.up * 1.2f + dir * 0.8f, 4);
            AudioService.Sfx(Weapon.radius > 0f ? "sfx_shoot_heavy" : "sfx_shoot", transform.position, Random.Range(0.95f, 1.08f));
        }

        float sprayFx;
        void Spray()
        {
            Vector3 fwd = facing;
            Health.InRadius(transform.position, range + 1f, Team.Enemy, true, false, scratch);
            var victims = scratch.ToArray();
            foreach (var h in victims)
            {
                Vector3 to = MapMath.Flat(h.transform.position - transform.position);
                if (to.magnitude - h.radius > range || Vector3.Angle(fwd, to) > Weapon.arc * 0.5f) continue;
                var d = MakeDamage(damage);
                h.TakeDamage(d);
                if (d.crit) Ui.Floating(h.transform.position + Vector3.up, Mathf.RoundToInt(d.amount) + "!", Palette.Crit, 1f);
            }
            sprayFx -= 1f / Mathf.Max(1f, rate);
            if (sprayFx <= 0f)
            {
                sprayFx = 0.12f;
                for (float r = 1f; r <= range; r += 1.5f)
                    Fx.Emit(FxKind.Mud, transform.position + Vector3.up * 0.9f + fwd * r, 2, Palette.Hex(Weapon.tint));
                AudioService.Sfx("sfx_spray", transform.position, Random.Range(0.95f, 1.05f), 0.5f);
            }
        }

        void Lob()
        {
            Vector3 point = AimPoint(range);
            int n = Mathf.Max(1, Weapon.projectiles);
            for (int i = 0; i < n; i++)
            {
                Vector3 offset = n == 1 ? Vector3.zero : Quaternion.Euler(0f, (i - (n - 1) * 0.5f) * Weapon.spread * 3f, 0f) * facing * 1.8f;
                var dmg = MakeDamage(damage);
                dmg.projectile = true;
                Projectile.Launch(new ProjectileSpec
                {
                    from = transform.position + Vector3.up * 1.6f,
                    targetPoint = point + offset,
                    speed = Weapon.projectileSpeed,
                    maxRange = range,
                    lob = true,
                    arcHeight = 3.5f,
                    splash = Weapon.radius,
                    canHitAir = true,
                    targetTeam = Team.Enemy,
                    damage = dmg,
                    color = Palette.Hex(Weapon.tint),
                    size = 0.35f,
                    impactFx = Weapon.effect == "burn" ? FxKind.Fire : FxKind.Shockwave,
                });
            }
            AudioService.Sfx("sfx_lob", transform.position, Random.Range(0.9f, 1.05f));
        }

        // ---- dash & abilities -----------------------------------------------------------

        void StartDash(bool ability)
        {
            abilityDash = ability;
            dashDir = facing;
            dashLeft = ability ? 0.35f : 0.18f;
            dashCooldown = 1.2f;
            dashHits.Clear();
            Fx.Emit(FxKind.Dust, transform.position, 8);
            AudioService.Sfx("sfx_swing", transform.position, 0.7f, 0.6f);
        }

        void DashHits()
        {
            Health.InRadius(transform.position, 1.8f, Team.Enemy, true, false, scratch);
            var victims = scratch.ToArray();
            foreach (var h in victims)
            {
                if (dashHits.Contains(h)) continue;
                dashHits.Add(h);
                var d = MakeDamage(damage * 2f);
                d.knockback = 4f;
                Combat.Hit(h, d, FxKind.Spark, "sfx_hit_heavy");
            }
            Fx.Emit(FxKind.Spark, transform.position + Vector3.up, 2);
        }

        void UseAbility()
        {
            abilityLeft = AbilityCooldown;
            var mods = Run.Current.Mods;
            float power = mods.Mul(Stat.HeroDamage);
            AudioService.Sfx("sfx_ability", transform.position);
            Fx.Shake(0.25f);
            switch (Class.ability)
            {
                case "spud-in":
                    StartDash(true);
                    break;
                case "rally-horn":
                {
                    Rally.Start(transform.position, 12f, 6f);
                    foreach (var u in Run.Current.Units)
                        if (u != null && u.Alive && Vector3.Distance(u.transform.position, transform.position) < 12f)
                        {
                            u.Health.Heal(u.Health.max * 0.3f);
                            Fx.Emit(FxKind.Heal, u.transform.position + Vector3.up, 6);
                        }
                    Fx.Emit(FxKind.Shockwave, transform.position + Vector3.up * 0.2f, 30, Palette.Hazard);
                    Ui.Toast("Rally Horn! Crew damage +40% for 6 s");
                    break;
                }
                case "shaped-charge-volley":
                {
                    Vector3 dir = AimDirection();
                    for (int i = 0; i < 9; i++)
                    {
                        Vector3 d = Quaternion.Euler(0f, (i - 4) * 8.5f, 0f) * dir;
                        var dmg = MakeDamage(damage * 1.2f);
                        dmg.projectile = true;
                        Projectile.Launch(new ProjectileSpec
                        {
                            from = transform.position + Vector3.up * 1.2f,
                            direction = d,
                            straight = true,
                            speed = 30f,
                            maxRange = range + 4f,
                            pierce = 1,
                            canHitAir = true,
                            targetTeam = Team.Enemy,
                            damage = dmg,
                            color = Palette.Hex("#FFD23F"),
                            size = 0.3f,
                        });
                    }
                    break;
                }
                case "kill-mud":
                {
                    var d = DamageInfo.Simple(40f * power, KillSource.Hero, gameObject);
                    d.knockback = 6f;
                    d.slow = 0.5f; d.slowTime = 4f;
                    Combat.Splash(transform.position, 8f, Team.Enemy, true, d, FxKind.Mud);
                    for (int i = 0; i < 16; i++)
                        Fx.Emit(FxKind.Mud, transform.position + Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * 4f + Vector3.up * 0.5f, 4);
                    break;
                }
                case "seismic-shot":
                {
                    Vector3 point = AimPoint(9f);
                    StartCoroutine(SeismicShot(point, power));
                    break;
                }
            }
        }

        System.Collections.IEnumerator SeismicShot(Vector3 point, float power)
        {
            Fx.Emit(FxKind.Dust, point, 10);
            yield return new WaitForSeconds(0.45f);
            var d = DamageInfo.Simple(60f * power, KillSource.Hero, gameObject);
            d.stunTime = 1.5f;
            d.knockback = 2f;
            Combat.Splash(point, 7f, Team.Enemy, true, d, FxKind.Shockwave);
            Fx.Emit(FxKind.Dust, point, 40);
            Fx.Shake(0.6f);
        }

        // ---- damage, down, respawn ------------------------------------------------------

        void OnDamaged(Health h, DamageInfo d)
        {
            flash.Flash();
            Fx.Shake(0.12f);
            AudioService.Sfx("sfx_hero_hurt", transform.position, Random.Range(0.95f, 1.05f), 0.8f);
            Ui.HurtVignette();
            if (d.knockback > 0f) velocity += MapMath.Flat(transform.position - d.from).normalized * Mathf.Min(8f, d.knockback * 2f);
        }

        void OnDied(Health h, DamageInfo d)
        {
            Downed = true;
            var run = Run.Current;
            model.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            AudioService.Sfx("sfx_hero_down", transform.position);
            Fx.Emit(FxKind.Dust, transform.position, 20);
            WaitingForDawn = run != null && run.Mods.Flag(Stat.HeroNoRespawn) && run.IsNight;
            float delay = 8f * (run != null ? run.Difficulty.heroRespawn * run.Mods.Mul(Stat.HeroRespawn) : 1f);
            respawnAt = Time.time + delay;
            Ui.Toast(WaitingForDawn ? "You are down until dawn (Zero-Tolerance HSE)" : "You are down! Back on the rig floor in " + Mathf.CeilToInt(delay) + " s");
        }

        void Respawn()
        {
            Downed = false;
            WaitingForDawn = false;
            Teleport(spawn);
            velocity = Vector3.zero;
            Health.Revive(1f);
            invulnerableUntil = Time.time + 2f;
            model.transform.localRotation = Quaternion.identity;
            Tweener.ScaleIn(model.transform, 0.3f);
            Fx.Emit(FxKind.Heal, transform.position + Vector3.up, 16);
            AudioService.Sfx("sfx_heal", transform.position);
        }

        public void OnDayStart()
        {
            if (Downed) Respawn();
            Health.ClearStatus();
        }

        public void OnDawn()
        {
            if (Downed) Respawn();
            Health.HealFull();
        }
    }
}
