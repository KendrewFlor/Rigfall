// "Juice": particle bursts, screen shake, hit-stop and barrels flying to a building.
// One pooled ParticleSystem per effect kind, configured in code and fired with Emit(),
// so effects cost almost nothing and need no prefab assets.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rigfall.Runtime
{
    public enum FxKind { Spark, Hit, Oil, Dust, Fire, Explosion, Smoke, Coin, Heal, Mud, Ice, Poison, Build, Shockwave, Brilliant }

    public sealed class Fx : MonoBehaviour
    {
        static Fx instance;
        readonly Dictionary<FxKind, ParticleSystem> systems = new Dictionary<FxKind, ParticleSystem>();
        float density = 1f;
        float hitStopUntil;
        bool hitStopActive;

        public static void Init(Transform parent)
        {
            if (instance != null) return;
            var go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            instance = go.AddComponent<Fx>();
            foreach (FxKind k in Enum.GetValues(typeof(FxKind))) instance.systems[k] = instance.Build(k);
        }

        /// <summary>Particle density from the graphics setting (Low 0.4, Medium 0.7, High 1).</summary>
        public static void SetDensity(float d) { if (instance != null) instance.density = Mathf.Clamp(d, 0.1f, 1f); }

        public static void Emit(FxKind kind, Vector3 position, int count) => Emit(kind, position, count, null);

        public static void Emit(FxKind kind, Vector3 position, int count, Color? color)
        {
            if (instance == null) return;
            ParticleSystem ps;
            if (!instance.systems.TryGetValue(kind, out ps) || ps == null) return;
            int n = Mathf.Max(1, Mathf.RoundToInt(count * instance.density));
            var ep = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            if (color.HasValue) ep.startColor = color.Value;
            ps.Emit(ep, n);
        }

        // ---- shake & hit-stop ------------------------------------------------------------

        public static void Shake(float amount)
        {
            var cam = CameraRig.Instance;
            if (cam != null) cam.Shake(amount * Game.Settings.screenShake);
        }

        /// <summary>Freeze the game for a moment on big hits. Skipped while paused.</summary>
        public static void HitStop(float seconds)
        {
            if (instance == null || Time.timeScale <= 0.01f || Game.Paused) return;
            instance.hitStopUntil = Time.unscaledTime + seconds;
            if (!instance.hitStopActive) instance.StartCoroutine(instance.HitStopRoutine());
        }

        IEnumerator HitStopRoutine()
        {
            hitStopActive = true;
            Time.timeScale = 0.05f;
            while (Time.unscaledTime < hitStopUntil && !Game.Paused) yield return null;
            if (!Game.Paused) Time.timeScale = 1f;
            hitStopActive = false;
        }

        // ---- barrels flying to a building -------------------------------------------------

        /// <summary>
        /// Barrels arc from 'from' to 'to' one after another. Each arrival punches the target's
        /// scale and plays a coin sound; onAllArrived runs after the last one lands.
        /// </summary>
        public static void FlyBarrels(Vector3 from, Transform to, int count, Action onAllArrived)
        {
            if (instance == null) { onAllArrived?.Invoke(); return; }
            instance.StartCoroutine(instance.FlyRoutine(from, to, Mathf.Clamp(count, 1, 12), onAllArrived));
        }

        IEnumerator FlyRoutine(Vector3 from, Transform to, int count, Action done)
        {
            int landed = 0;
            for (int i = 0; i < count; i++)
            {
                var barrel = Models.Spawn("prop-barrel", null);
                barrel.transform.position = from + Vector3.up * 1.2f;
                barrel.transform.localScale = Vector3.one * 0.55f;
                Vector3 target = to != null ? to.position + Vector3.up * 1.2f : from;
                float height = 2.2f + 0.25f * i;
                Tweener.Arc(barrel.transform, barrel.transform.position, target, height, 0.38f, () =>
                {
                    landed++;
                    if (barrel != null) Destroy(barrel);
                    if (to != null) Tweener.Punch(to, 0.12f, 0.2f);
                    Emit(FxKind.Coin, target, 6);
                    AudioService.Ui("sfx_coin", 0.85f + 0.06f * landed);
                });
                yield return new WaitForSeconds(0.06f);
            }
            float wait = 0f;
            while (landed < count && wait < 2f) { wait += Time.deltaTime; yield return null; }
            done?.Invoke();
        }

        // ---- particle system definitions --------------------------------------------------

        ParticleSystem Build(FxKind k)
        {
            var go = new GameObject("Fx " + k);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;      // kept playing with zero rate, so particles from Emit() always simulate
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            bool additive = false;
            Color a = Color.white, b = Color.white;
            float lifeMin = 0.3f, lifeMax = 0.6f, speedMin = 2f, speedMax = 5f, sizeMin = 0.15f, sizeMax = 0.35f, gravity = 0f;

            switch (k)
            {
                case FxKind.Spark: additive = true; a = Palette.Hex("#FFE08A"); b = Palette.Hex("#FF8A3D"); speedMin = 4; speedMax = 9; sizeMin = 0.08f; sizeMax = 0.16f; lifeMin = 0.15f; lifeMax = 0.35f; gravity = 1.5f; break;
                case FxKind.Hit: a = Color.white; b = Palette.Hex("#FFD0A0"); speedMin = 2; speedMax = 5; sizeMin = 0.2f; sizeMax = 0.4f; lifeMin = 0.15f; lifeMax = 0.3f; break;
                case FxKind.Oil: a = Palette.Hex("#1A1410"); b = Palette.Hex("#3A2A1C"); speedMin = 2; speedMax = 6; sizeMin = 0.15f; sizeMax = 0.35f; lifeMin = 0.4f; lifeMax = 0.8f; gravity = 2.5f; break;
                case FxKind.Dust: a = Palette.Hex("#C9A26B"); b = Palette.Hex("#8B6B47"); speedMin = 0.5f; speedMax = 2; sizeMin = 0.4f; sizeMax = 0.9f; lifeMin = 0.6f; lifeMax = 1.2f; gravity = -0.1f; break;
                case FxKind.Fire: additive = true; a = Palette.Hex("#FFD23F"); b = Palette.Hex("#FF4A1A"); speedMin = 0.5f; speedMax = 2; sizeMin = 0.25f; sizeMax = 0.5f; lifeMin = 0.3f; lifeMax = 0.7f; gravity = -1.2f; break;
                case FxKind.Explosion: additive = true; a = Palette.Hex("#FFF2C4"); b = Palette.Hex("#FF6A13"); speedMin = 3; speedMax = 9; sizeMin = 0.4f; sizeMax = 0.9f; lifeMin = 0.25f; lifeMax = 0.6f; shape.radius = 0.5f; break;
                case FxKind.Smoke: a = Palette.Hex("#5D6B70"); b = Palette.Hex("#2A3438"); speedMin = 0.5f; speedMax = 1.5f; sizeMin = 0.6f; sizeMax = 1.2f; lifeMin = 1f; lifeMax = 2f; gravity = -0.3f; break;
                case FxKind.Coin: additive = true; a = Palette.Hex("#FFD76A"); b = Palette.Hex("#FFB000"); speedMin = 2; speedMax = 5; sizeMin = 0.1f; sizeMax = 0.22f; lifeMin = 0.3f; lifeMax = 0.6f; gravity = 1f; break;
                case FxKind.Heal: additive = true; a = Palette.Hex("#B6FFCF"); b = Palette.Hex("#4FD18B"); speedMin = 0.5f; speedMax = 1.5f; sizeMin = 0.15f; sizeMax = 0.3f; lifeMin = 0.6f; lifeMax = 1f; gravity = -0.8f; break;
                case FxKind.Mud: a = Palette.Hex("#8B6B47"); b = Palette.Hex("#5C4A3A"); speedMin = 3; speedMax = 7; sizeMin = 0.15f; sizeMax = 0.35f; lifeMin = 0.3f; lifeMax = 0.6f; gravity = 2f; break;
                case FxKind.Ice: additive = true; a = Palette.Hex("#E8F7FF"); b = Palette.Hex("#7FD0FF"); speedMin = 2; speedMax = 5; sizeMin = 0.12f; sizeMax = 0.28f; lifeMin = 0.4f; lifeMax = 0.8f; gravity = 0.5f; break;
                case FxKind.Poison: a = Palette.Hex("#C6D42A"); b = Palette.Hex("#7D8A1A"); speedMin = 0.4f; speedMax = 1.2f; sizeMin = 0.3f; sizeMax = 0.6f; lifeMin = 0.6f; lifeMax = 1.1f; gravity = -0.3f; break;
                case FxKind.Build: a = Palette.Hex("#E8EEF0"); b = Palette.Hex("#9DB4BB"); speedMin = 1.5f; speedMax = 4; sizeMin = 0.4f; sizeMax = 0.8f; lifeMin = 0.5f; lifeMax = 0.9f; shape.radius = 1.2f; break;
                case FxKind.Shockwave: additive = true; a = Palette.Hex("#FFFFFF"); b = Palette.Hex("#9DB4BB"); speedMin = 8; speedMax = 12; sizeMin = 0.3f; sizeMax = 0.6f; lifeMin = 0.25f; lifeMax = 0.4f; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.5f; break;
                case FxKind.Brilliant: additive = true; a = Palette.Hex("#FF2BD6"); b = Palette.Hex("#3FA7FF"); speedMin = 3; speedMax = 8; sizeMin = 0.2f; sizeMax = 0.45f; lifeMin = 0.6f; lifeMax = 1.2f; gravity = -0.2f; break;
            }

            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            renderer.sharedMaterial = Materials.Particle(additive);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(g);
            ps.Play();
            return ps;
        }
    }
}
