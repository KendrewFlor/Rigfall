// A tiny tween engine (transform.localScale punches, pop-ins, arcs and value tweens).
// One MonoBehaviour updates every running tween; nothing is allocated per frame.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class Tweener : MonoBehaviour
    {
        enum Kind { Punch, ScaleIn, ScaleOut, Arc, Value }

        sealed class Job
        {
            public Kind kind;
            public Transform target;
            public Vector3 baseScale, from, to;
            public float duration, elapsed, amount, height;
            public bool unscaled;
            public Action<float> step;
            public Action done;
        }

        static Tweener instance;
        readonly List<Job> jobs = new List<Job>();
        readonly List<Job> adding = new List<Job>();
        static readonly Dictionary<Transform, Vector3> baseScales = new Dictionary<Transform, Vector3>();

        public static void Init(Transform parent)
        {
            if (instance != null) return;
            var go = new GameObject("Tweener");
            go.transform.SetParent(parent, false);
            instance = go.AddComponent<Tweener>();
        }

        static void Add(Job j)
        {
            if (instance == null) { j.done?.Invoke(); return; }
            instance.adding.Add(j);
        }

        static Vector3 BaseScale(Transform t)
        {
            Vector3 s;
            if (!baseScales.TryGetValue(t, out s)) { s = t.localScale; baseScales[t] = s; }
            return s;
        }

        /// <summary>Remember the current scale as the object's resting scale (after rebuilding it).</summary>
        public static void SetRestScale(Transform t) { if (t != null) baseScales[t] = t.localScale; }

        /// <summary>Squash-and-stretch punch: grows by 'amount' then settles back with a wobble.</summary>
        public static void Punch(Transform t, float amount = 0.2f, float duration = 0.3f)
        {
            if (t == null) return;
            Add(new Job { kind = Kind.Punch, target = t, baseScale = BaseScale(t), amount = amount, duration = duration, unscaled = true });
        }

        /// <summary>Pop in from zero with an overshoot (new buildings, UI cards).</summary>
        public static void ScaleIn(Transform t, float duration = 0.35f, bool unscaled = false)
        {
            if (t == null) return;
            var s = t.localScale;
            baseScales[t] = s;
            t.localScale = Vector3.zero;
            Add(new Job { kind = Kind.ScaleIn, target = t, baseScale = s, duration = duration, unscaled = unscaled });
        }

        public static void ScaleOut(Transform t, float duration, Action done)
        {
            if (t == null) { done?.Invoke(); return; }
            Add(new Job { kind = Kind.ScaleOut, target = t, baseScale = t.localScale, duration = duration, done = done });
        }

        /// <summary>Move along a parabola from 'from' to 'to'.</summary>
        public static void Arc(Transform t, Vector3 from, Vector3 to, float height, float duration, Action done)
        {
            Add(new Job { kind = Kind.Arc, target = t, from = from, to = to, height = height, duration = duration, done = done });
        }

        /// <summary>Call step(0..1) every frame for 'duration' seconds (unscaled = keeps running while paused).</summary>
        public static void Value(float duration, Action<float> step, Action done = null, bool unscaled = true)
        {
            Add(new Job { kind = Kind.Value, duration = duration, step = step, done = done, unscaled = unscaled });
        }

        public static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        public static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

        void LateUpdate()
        {
            if (adding.Count > 0) { jobs.AddRange(adding); adding.Clear(); }
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                var j = jobs[i];
                if (j.kind != Kind.Value && j.target == null) { jobs.RemoveAt(i); continue; }
                j.elapsed += j.unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float x = j.duration <= 0f ? 1f : Mathf.Clamp01(j.elapsed / j.duration);
                switch (j.kind)
                {
                    case Kind.Punch:
                        float wobble = Mathf.Sin(x * Mathf.PI * 3f) * (1f - x);
                        j.target.localScale = new Vector3(j.baseScale.x * (1f + j.amount * wobble), j.baseScale.y * (1f - j.amount * 0.6f * wobble), j.baseScale.z * (1f + j.amount * wobble));
                        if (x >= 1f) j.target.localScale = j.baseScale;
                        break;
                    case Kind.ScaleIn:
                        j.target.localScale = j.baseScale * Mathf.Max(0f, EaseOutBack(x));
                        break;
                    case Kind.ScaleOut:
                        j.target.localScale = j.baseScale * (1f - EaseOutCubic(x));
                        break;
                    case Kind.Arc:
                        var p = Vector3.Lerp(j.from, j.to, x);
                        p.y += Mathf.Sin(x * Mathf.PI) * j.height;
                        j.target.position = p;
                        j.target.Rotate(0f, 540f * Time.deltaTime, 0f, Space.World);
                        break;
                    case Kind.Value:
                        j.step?.Invoke(x);
                        break;
                }
                if (x >= 1f)
                {
                    jobs.RemoveAt(i);
                    j.done?.Invoke();
                }
            }
        }
    }
}
