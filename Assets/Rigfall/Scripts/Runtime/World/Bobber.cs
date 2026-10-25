using System.Collections.Generic;
using Rigfall.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Rigfall.Runtime
{
    /// <summary>Gentle up-and-down motion for chevrons and the sea.</summary>
    public sealed class Bobber : MonoBehaviour
    {
        float amp = 0.1f, speed = 2f, phase;
        Vector3 basePos;
        bool started;
        public void Setup(float amplitude, float frequency) { amp = amplitude; speed = frequency; phase = Random.value * 6f; }
        void Start() { basePos = transform.localPosition; started = true; }
        void Update() { if (started) transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * speed + phase) * amp; }
    }
}
