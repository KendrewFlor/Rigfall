// "Follow & Hold" commands. While the command button is held, every unit inside a
// Physics.OverlapSphere around the hero switches to FollowPlayer and walks to a slot in a
// formation behind the hero. When the button is released, those units switch to
// HoldPosition at the spot where they are standing. A short tap sends nearby units back to
// their posts.
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class CommandSystem : MonoBehaviour
    {
        public float baseRadius = 6f;
        readonly List<FriendlyUnit> following = new List<FriendlyUnit>();
        readonly Collider[] buffer = new Collider[128];
        float heldFor;
        bool wasHeld;
        RangeRing ring;
        Hero hero;

        public float Radius => baseRadius * (Run.Current != null ? Run.Current.Mods.Mul(Stat.CommandRadius) : 1f);
        public int FollowingCount => following.Count;

        void Start()
        {
            hero = GetComponent<Hero>();
            ring = RangeRing.Create(transform, new Color(0.4f, 0.75f, 1f, 0.7f));
        }

        void Update()
        {
            if (hero == null || !hero.Alive || Game.InputBlocked) { if (wasHeld) Release(); return; }
            bool held = InputReader.CommandHeld;
            if (held)
            {
                heldFor += Time.deltaTime;
                ring.SetRadius(Radius);
                ring.Show(true);
                Gather();
            }
            else if (wasHeld)
            {
                if (heldFor < 0.25f)
                {
                    // A quick tap: everyone nearby (and anyone just picked up) goes back to their post.
                    int picked = 0;
                    foreach (var u in following) if (u != null && u.Alive) { u.ReturnToPost(); picked++; }
                    following.Clear();
                    ring.Show(false);
                    heldFor = 0f;
                    Recall(picked);
                }
                else Release();
            }
            wasHeld = held;
        }

        /// <summary>Recruit every unit inside the sphere and give it a formation slot behind the hero.</summary>
        void Gather()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, Radius, buffer, Physics.AllLayers, QueryTriggerInteraction.Collide);
            bool added = false;
            for (int i = 0; i < n; i++)
            {
                var h = Health.Of(buffer[i]);
                if (h == null || !h.isUnit || !h.Alive) continue;
                var u = h.GetComponent<FriendlyUnit>();
                if (u == null || following.Contains(u)) continue;
                following.Add(u);
                added = true;
                Fx.Emit(FxKind.Spark, u.transform.position + Vector3.up * 2f, 4, new Color(0.4f, 0.8f, 1f));
            }
            following.RemoveAll(u => u == null || !u.Alive);
            for (int i = 0; i < following.Count; i++) following[i].Follow(transform, FormationOffset(i, following.Count));
            if (added)
            {
                AudioService.Ui("sfx_command", 1f + 0.05f * following.Count, 0.7f);
                Tutorial.Notify("command");
            }
        }

        /// <summary>Rows of four, 2 m behind the hero, 1.4 m apart.</summary>
        static Vector3 FormationOffset(int i, int count)
        {
            int perRow = 4;
            int row = i / perRow;
            int inRow = Mathf.Min(perRow, count - row * perRow);
            float x = (i % perRow - (inRow - 1) * 0.5f) * 1.4f;
            return new Vector3(x, 0f, -2f - row * 1.4f);
        }

        void Release()
        {
            ring.Show(false);
            heldFor = 0f;
            for (int i = 0; i < following.Count; i++)
            {
                var u = following[i];
                if (u == null || !u.Alive) continue;
                u.Hold(transform.position + transform.rotation * FormationOffset(i, following.Count));
                Fx.Emit(FxKind.Dust, u.transform.position, 3);
            }
            if (following.Count > 0)
            {
                AudioService.Ui("sfx_command", 0.85f, 0.7f);
                Ui.Toast(following.Count + " unit" + (following.Count == 1 ? "" : "s") + " holding here");
            }
            following.Clear();
            wasHeld = false;
        }

        void Recall(int alreadyRecalled)
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, Radius * 1.6f, buffer, Physics.AllLayers, QueryTriggerInteraction.Collide);
            int count = alreadyRecalled;
            for (int i = 0; i < n; i++)
            {
                var h = Health.Of(buffer[i]);
                var u = h != null && h.isUnit ? h.GetComponent<FriendlyUnit>() : null;
                if (u == null || u.CurrentMode == FriendlyUnit.Mode.Defend) continue;
                u.ReturnToPost();
                count++;
            }
            if (count > 0) Ui.Toast(count + " unit" + (count == 1 ? "" : "s") + " back to their posts");
        }
    }
}
