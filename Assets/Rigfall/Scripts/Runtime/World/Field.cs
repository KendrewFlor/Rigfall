using System.Collections.Generic;
using Rigfall.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Rigfall.Runtime
{
    /// <summary>The built field: roots for everything placed on it, its slots and spawn points.</summary>
    public sealed class Field : MonoBehaviour
    {
        public MapDef Def;
        public Transform GroundRoot, BuildingRoot, SlotRoot, UnitRoot, EnemyRoot, PreviewRoot;
        public NavMeshSurface Surface;
        public readonly List<BuildingSlot> Slots = new List<BuildingSlot>();
        public BuildingSlot RigSlot;
        public Vector3[] Spawns = new Vector3[0];
        public Vector3 HeroSpawn = new Vector3(0f, 0f, -5f);
        public Light Sun;
        public Light RigLamp;
        float dayAmount = 1f, dayTarget = 1f;
        readonly List<GameObject> previews = new List<GameObject>();

        public Vector3 SpawnPoint(int lane) => lane >= 0 && lane < Spawns.Length ? Spawns[lane] : Vector3.forward * 20f;

        /// <summary>Fade lighting toward day (1) or night (0).</summary>
        public void SetDay(bool day, bool instant)
        {
            dayTarget = day ? 1f : 0f;
            if (instant) { dayAmount = dayTarget; ApplyLighting(); }
        }

        void Update()
        {
            if (Mathf.Approximately(dayAmount, dayTarget)) return;
            dayAmount = Mathf.MoveTowards(dayAmount, dayTarget, Time.deltaTime * 0.5f);
            ApplyLighting();
        }

        void ApplyLighting()
        {
            if (Def == null) return;
            float d = dayAmount;
            var skyDay = Palette.Hex(Def.skyDay);
            var skyNight = Palette.Hex(Def.skyNight);
            var sunDay = Palette.Hex(Def.sun);
            var moon = new Color(0.55f, 0.65f, 0.95f);
            if (Sun != null)
            {
                Sun.color = Color.Lerp(moon, sunDay, d);
                Sun.intensity = Mathf.Lerp(0.42f, 1.15f, d);
                Sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(62f, 48f, d), Mathf.Lerp(-150f, -35f, d), 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(new Color(0.16f, 0.2f, 0.32f), Color.Lerp(skyDay, Color.white, 0.25f) * 0.62f, d);
            var fog = Palette.Hex(Def.fog);
            RenderSettings.fogColor = Color.Lerp(skyNight * 1.2f, fog, d);
            // Tight Hole mutator: thick night fog closes in to just past the hero (the camera sits ~24 m away).
            var run = Run.Current;
            bool thick = run != null && run.Field == this && run.Mods.HasSpecial("fog");
            RenderSettings.fogStartDistance = Mathf.Lerp(thick ? 22f : 38f, 38f, d);
            RenderSettings.fogEndDistance = Mathf.Lerp(thick ? 46f : 95f, 95f, d);
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = Color.Lerp(skyNight, skyDay, d);
            if (RigLamp != null) RigLamp.intensity = Mathf.Lerp(2.2f, 0f, d);
        }

        public void ShowLanePreview(WavePlan plan, bool detailed)
        {
            HideLanePreview();
            if (plan == null) return;
            var counts = new Dictionary<int, int>();
            foreach (var s in plan.spawns) { int n; counts.TryGetValue(s.lane, out n); counts[s.lane] = n + 1; }
            foreach (int lane in plan.lanes)
            {
                Vector3 from = SpawnPoint(lane);
                Vector3 dir = (Vector3.zero - from); dir.y = 0f;
                float len = dir.magnitude;
                dir.Normalize();
                bool primary = lane == plan.primaryLane;
                var color = primary ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.6f, 0.2f);
                for (float t = 1.5f; t < Mathf.Min(len * 0.55f, 16f); t += 2.6f)
                {
                    var chevron = Models.Spawn("prop-chevron", PreviewRoot, Materials.Glow(color), false);
                    chevron.transform.position = from + dir * t + Vector3.up * 0.08f;
                    chevron.transform.rotation = Quaternion.LookRotation(dir);
                    chevron.AddComponent<Bobber>().Setup(0.12f, 2.2f + t * 0.1f);
                    previews.Add(chevron);
                }
                var marker = Models.Spawn("prop-spawn-marker", PreviewRoot, Materials.Glow(color), false);
                marker.transform.position = from + Vector3.up * 0.05f;
                marker.AddComponent<Spinner>().speed = 40f;
                previews.Add(marker);
                int c;
                counts.TryGetValue(lane, out c);
                string label = detailed ? c + " enemies" : (primary ? "Main wave" : "Flank");
                if (primary && !string.IsNullOrEmpty(plan.bossId)) label += " + BOSS";
                previews.Add(Ui.WorldLabel(from + Vector3.up * 2.4f, label, color));
            }
        }

        public void HideLanePreview()
        {
            foreach (var p in previews) if (p != null) Destroy(p);
            previews.Clear();
        }
    }
}
