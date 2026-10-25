// Fixed-slot building system. A BuildingSlot sits on an empty object at a predefined spot
// on the field. It has a trigger collider: when the hero walks in during the day, a prompt
// shows what can be built (or upgraded) there and the price. Holding the interact button
// pays, the barrels fly from the hero to the slot, and the building appears immediately.
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class BuildingSlot : MonoBehaviour
    {
        public SlotDef Def { get; private set; }
        public BuildingDef Building0 { get; private set; }   // what this slot builds
        public BuildingBase Building { get; private set; }   // what stands here now (null = empty)
        public Field Field { get; private set; }

        public const float HoldToBuy = 0.45f;

        SphereCollider trigger;
        GameObject ghost, pad;
        bool interactive = true, playerInside;
        int selected;                 // which option is highlighted when there's a choice
        float hold;
        bool purchasing;
        List<UpgradeChoice> choices = new List<UpgradeChoice>();
        float recheck;

        public bool PlayerInside => playerInside && interactive;

        public void Setup(SlotDef def, BuildingDef building, Field field)
        {
            Def = def;
            Building0 = building;
            Field = field;
            trigger = GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(2.2f, building.footprint + 1.4f);
            trigger.center = new Vector3(0f, 1f, 0f);
            // A kinematic rigidbody guarantees trigger messages when the hero's CharacterController enters.
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            if (def.id != "rig")
            {
                pad = Models.Spawn("prop-slot-pad", transform, Materials.Lit(Palette.Hex("#D9C49A")), false);
                if (building.kind == "wall") pad.transform.localScale = new Vector3(Mathf.Max(1f, def.length / 2.4f), 1f, 0.6f);
                ShowGhost();
            }
        }

        /// <summary>Restore a building from a saved path (Continue / Retry from dawn).</summary>
        public void Restore(List<string> path)
        {
            if (path == null || path.Count == 0 || !BuildingRules.IsValidPath(Building0, path)) return;
            CreateBuilding(new List<string>(path), false);
        }

        /// <summary>The rig is created by the Run; the rig slot just upgrades it.</summary>
        public void AttachExisting(BuildingBase b)
        {
            Building = b;
            b.Slot = this;
            transform.position = b.transform.position;
        }

        public void SetInteractive(bool on)
        {
            interactive = on;
            if (!on) { hold = 0f; Ui.HidePrompt(this); }
            if (ghost != null) ghost.SetActive(on && Building == null);
            if (pad != null) pad.SetActive(Building == null || Building.Destroyed);
            RefreshChoices();
        }

        void ShowGhost()
        {
            if (ghost != null) Destroy(ghost);
            ghost = Models.Spawn(Building0.model, transform, Materials.Glow(new Color(1f, 1f, 1f, 0.18f)), false);
            ghost.transform.localScale = Vector3.one * 0.92f;
            if (Building0.kind == "wall") ghost.transform.localScale = new Vector3(Mathf.Max(0.4f, Def.length / 3.2f), 0.92f, 0.92f);
            ghost.AddComponent<Bobber>().Setup(0.08f, 1.5f);
            ghost.SetActive(interactive && Building == null);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<Hero>() == null) return;
            playerInside = true;
            selected = 0;
            RefreshChoices();
        }

        void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<Hero>() == null) return;
            playerInside = false;
            hold = 0f;
            Ui.HidePrompt(this);
        }

        void RefreshChoices()
        {
            var run = Run.Current;
            if (run == null) { choices.Clear(); return; }
            var path = Building != null ? Building.Path : null;
            choices = BuildingRules.NextChoices(Building0, path, run.RigTier, run.Mods, run.FirstPurchaseToday);
            if (selected >= choices.Count) selected = 0;
        }

        void Update()
        {
            var run = Run.Current;
            if (run == null || run.Hero == null) return;

            // Fallback for respawns/teleports: re-check distance a few times a second.
            recheck -= Time.deltaTime;
            if (recheck <= 0f)
            {
                recheck = 0.25f;
                bool near = MapMath.Flat(run.Hero.transform.position - transform.position).magnitude <= trigger.radius;
                if (playerInside && !near) { playerInside = false; hold = 0f; Ui.HidePrompt(this); }
                else if (!playerInside && near && run.Hero.Alive) { playerInside = true; RefreshChoices(); }
            }

            if (!PlayerInside || !run.Hero.Alive || Game.InputBlocked || purchasing) return;
            if (Building != null && Building.Destroyed) { Ui.ShowPrompt(this, Building0.name + " (knocked out)", "Repairs itself at dawn", null, 0, 0f); return; }

            RefreshChoices();
            if (choices.Count == 0)
            {
                Ui.ShowPrompt(this, Building != null ? Building.DisplayName : Building0.name, "Fully upgraded", null, 0, 0f);
                return;
            }

            int key = InputReader.OptionKeyPressed;
            if (key >= 0 && key < choices.Count) { selected = key; hold = 0f; AudioService.Ui("sfx_ui_hover"); }
            if (InputReader.CyclePressed && choices.Count > 1) { selected = (selected + 1) % choices.Count; hold = 0f; AudioService.Ui("sfx_ui_hover"); }

            var choice = choices[selected];
            bool canBuy = !choice.locked && run.CanAfford(choice.price);
            bool holdToBuild = Game.Settings.holdToBuild;
            if (InputReader.InteractHeld && canBuy)
            {
                hold += Time.deltaTime;
                if (!holdToBuild || hold >= HoldToBuy) Buy(choice);
            }
            else
            {
                if (InputReader.InteractHeld && !canBuy && hold == 0f)
                {
                    AudioService.Ui("sfx_buy_fail");
                    Tweener.Punch(transform, 0.08f, 0.2f);
                    hold = -0.6f; // don't buzz every frame
                }
                if (!InputReader.InteractHeld) hold = 0f;
                else hold = Mathf.Min(0f, hold + Time.deltaTime);
            }

            string title = Building == null ? "Build " + Building0.name : (Building.DisplayName + " → upgrade");
            Ui.ShowPrompt(this, title, "", choices, selected, Mathf.Clamp01(hold / HoldToBuy));
        }

        void Buy(UpgradeChoice choice)
        {
            var run = Run.Current;
            if (!run.Spend(choice.price))
            {
                AudioService.Ui("sfx_buy_fail");
                return;
            }
            purchasing = true;
            hold = 0f;
            bool upgrade = Building != null;
            Ui.HidePrompt(this);
            AudioService.Sfx("sfx_coin", transform.position);
            int barrels = Mathf.Clamp(choice.price, 1, 10);
            Fx.FlyBarrels(run.Hero.transform.position, transform, barrels, () =>
            {
                purchasing = false;
                if (this == null) return;
                if (upgrade && Building != null)
                {
                    Building.Path.Add(choice.option.id);
                    Building.ApplyOption(true);
                }
                else
                {
                    var path = new List<string> { choice.option.id };
                    CreateBuilding(path, true);
                }
                if (Building != null) run.OnBuilt(Building, upgrade);
                RefreshChoices();
            });
        }

        void CreateBuilding(List<string> path, bool juice)
        {
            var go = new GameObject(Building0.name + " @ " + Def.id);
            go.transform.SetParent(Field.BuildingRoot, false);
            go.transform.position = transform.position;
            go.transform.rotation = transform.rotation;
            BuildingBase b;
            switch (Building0.kind)
            {
                case "tower": b = go.AddComponent<Tower>(); break;
                case "wall": b = go.AddComponent<Wall>(); break;
                case "barracks": b = go.AddComponent<Barracks>(); break;
                case "house":
                case "mill": b = go.AddComponent<House>(); break;
                default: b = go.AddComponent<House>(); break;
            }
            b.Setup(Building0, this, path);
            Building = b;
            if (ghost != null) ghost.SetActive(false);
            if (pad != null) pad.SetActive(false);
            if (juice)
            {
                Tweener.ScaleIn(b.transform, 0.45f);
                Fx.Emit(FxKind.Build, transform.position + Vector3.up * 0.5f, 18);
                Fx.Shake(0.15f);
                AudioService.Sfx("sfx_build", transform.position);
            }
        }

        /// <summary>Called by a building when it is knocked out, so the slot pad shows again.</summary>
        public void OnBuildingStateChanged()
        {
            if (pad != null) pad.SetActive(Building == null || Building.Destroyed);
        }

        void OnDisable() { Ui.HidePrompt(this); }
    }
}
