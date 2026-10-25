// Base class for everything built on a slot. Inheritance:
//   BuildingBase -> MainRig, House (pumpjacks, tank batteries), Tower, Wall, Barracks
// It owns the health, the collider, the NavMeshObstacle that carves the NavMesh, the model
// for the current upgrade, the hit flash, and the knock-out / dawn rebuild cycle.
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Rigfall.Runtime
{
    public abstract class BuildingBase : MonoBehaviour
    {
        public BuildingDef Def { get; private set; }
        public BuildingSlot Slot { get; set; }
        public List<string> Path { get; private set; } = new List<string>();
        public Health Health { get; private set; }
        public bool Destroyed { get; private set; }

        public BuildingOption Option => BuildingRules.Current(Def, Path);
        public int Tier => Path.Count - 1;
        public string DisplayName { get { var o = Option; return o != null ? o.name : Def.name; } }

        protected GameObject visual;
        GameObject rubble;
        BoxCollider body;
        NavMeshObstacle obstacle;
        HitFlash flash;
        float lastHitSound;
        protected Run run => Run.Current;

        public void Setup(BuildingDef def, BuildingSlot slot, List<string> path)
        {
            Def = def;
            Slot = slot;
            Path = path;
            Health = gameObject.AddComponent<Health>();
            Health.isBuilding = true;
            Health.isRig = def.kind == "rig";
            Health.team = Team.Player;
            Health.radius = def.kind == "wall" ? 0.9f : def.footprint * 0.8f;
            Health.displayName = def.name;
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;

            body = gameObject.AddComponent<BoxCollider>();
            obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            flash = gameObject.AddComponent<HitFlash>();

            run.Register(this);
            ApplyOption(false);
            Health.Init(Team.Player, MaxHp(), 0f);
            Health.RefreshColliders();
            OnSetup();
        }

        protected virtual void OnSetup() { }

        /// <summary>Length of a wall segment along its local X axis (1 for everything else).</summary>
        protected float WidthScale => Def.kind == "wall" && Slot != null ? Mathf.Max(0.4f, Slot.Def.length / 3.2f) : 1f;

        protected virtual string ModelName()
        {
            var o = Option;
            if (o != null)
            {
                string variant = Def.model + "@" + o.id;
                if (Models.Exists(variant)) return variant;
            }
            return Def.model;
        }

        /// <summary>Rebuild the model and stats for the current option. juice = play the upgrade feedback.</summary>
        public virtual void ApplyOption(bool juice)
        {
            if (visual != null) Destroy(visual);
            string model = ModelName();
            visual = Models.Spawn(model, transform);
            visual.transform.localScale = new Vector3(WidthScale, 1f, 1f);
            flash.Bind(visual, Materials.Lit(Color.white));

            var data = Models.Data(model);
            float h = Mathf.Max(1.2f, data.height);
            if (Def.kind == "wall")
            {
                body.size = new Vector3(3.2f * WidthScale, h, 1.1f);
                obstacle.size = new Vector3(3.2f * WidthScale + 0.2f, h, 1.3f);
            }
            else
            {
                float w = Def.footprint * 1.5f;
                body.size = new Vector3(w, h, w);
                obstacle.size = new Vector3(w + 0.3f, h, w + 0.3f);
            }
            body.center = new Vector3(0f, h * 0.5f, 0f);
            obstacle.center = body.center;

            if (Health != null)
            {
                Health.SetMax(MaxHp(), juice);
                if (juice) Health.HealFull();
            }
            OnOptionChanged();
            Health?.RefreshColliders();

            if (juice)
            {
                Tweener.SetRestScale(transform);
                Tweener.Punch(transform, 0.25f, 0.4f);
                Fx.Emit(FxKind.Build, transform.position + Vector3.up, 16);
                Fx.Emit(FxKind.Coin, transform.position + Vector3.up * 2f, 10);
                Fx.Shake(0.12f);
                AudioService.Sfx("sfx_upgrade", transform.position);
                Ui.Floating(transform.position + Vector3.up * (h + 0.5f), DisplayName + "!", Palette.Crit, 1.2f);
            }
        }

        protected virtual void OnOptionChanged() { }

        public virtual void RefreshStats()
        {
            if (Health != null) Health.SetMax(MaxHp(), false);
            OnOptionChanged();
        }

        protected float MaxHp()
        {
            var o = Option;
            float hp = o != null ? o.hp : 100f;
            var mods = run != null ? run.Mods : null;
            if (mods == null) return hp;
            switch (Def.kind)
            {
                case "wall": hp *= mods.Mul(Stat.HpWall); break;
                case "house":
                case "mill": hp *= mods.Mul(Stat.HpHouse); break;
                case "tower": hp *= mods.Mul(Stat.HpTower); break;
                case "rig": hp *= mods.Mul(Stat.HpRig); break;
            }
            return hp * mods.Mul(Stat.HpBuilding);
        }

        protected virtual void OnDamaged(Health h, DamageInfo d)
        {
            flash.Flash();
            if (Time.time - lastHitSound > 0.25f)
            {
                lastHitSound = Time.time;
                AudioService.Sfx("sfx_building_hit", transform.position, Random.Range(0.9f, 1.05f), 0.8f);
                if (visual != null) Tweener.Punch(visual.transform, 0.06f, 0.15f);
            }
        }

        protected virtual void OnDied(Health h, DamageInfo d) { KnockOut(); }

        /// <summary>The building is wrecked for the rest of the night; enemies can walk through.</summary>
        protected void KnockOut()
        {
            if (Destroyed) return;
            Destroyed = true;
            if (visual != null) visual.SetActive(false);
            rubble = Models.Spawn("prop-rubble", transform);
            rubble.transform.localScale = new Vector3(WidthScale * (Def.kind == "wall" ? 1.6f : Def.footprint), 1f, Def.kind == "wall" ? 0.7f : Def.footprint);
            body.enabled = false;
            obstacle.enabled = false;
            Fx.Emit(FxKind.Explosion, transform.position + Vector3.up, 16);
            Fx.Emit(FxKind.Smoke, transform.position + Vector3.up, 14);
            Fx.Shake(0.35f);
            AudioService.Sfx("sfx_building_destroyed", transform.position);
            OnKnockedOut();
            if (run != null) run.OnBuildingDestroyed(this);
            if (Slot != null) Slot.OnBuildingStateChanged();
        }

        protected virtual void OnKnockedOut() { }

        public virtual void OnDayStart() { }
        public virtual void OnNightStart() { }

        /// <summary>At dawn everything is repaired; wrecked buildings are rebuilt for free.</summary>
        public virtual void OnDawn()
        {
            if (Destroyed)
            {
                Destroyed = false;
                if (rubble != null) Destroy(rubble);
                if (visual != null) visual.SetActive(true);
                body.enabled = true;
                obstacle.enabled = true;
                Health.Revive(1f);
                Tweener.ScaleIn(transform, 0.4f);
                Fx.Emit(FxKind.Build, transform.position + Vector3.up, 10);
                if (Slot != null) Slot.OnBuildingStateChanged();
                OnRebuilt();
            }
            else Health.HealFull();
        }

        protected virtual void OnRebuilt() { }

        protected virtual void Update()
        {
            if (run == null || Destroyed || !run.IsNight || !Health.Alive) return;
            float regen = run.Mods.Get(Stat.RegenBuilding, 0f);
            if (regen > 0f && Health.current < Health.max && Time.time - Health.LastDamageTime > 1f) Health.Heal(regen * Time.deltaTime);
        }

        /// <summary>Helper for subclasses that shoot.</summary>
        protected Turret EnsureTurret(bool on)
        {
            var t = GetComponent<Turret>();
            if (on && t == null)
            {
                t = gameObject.AddComponent<Turret>();
                t.owner = Health;
            }
            else if (!on && t != null) { Destroy(t); t = null; }
            return t;
        }
    }
}
