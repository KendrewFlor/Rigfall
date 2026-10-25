using System.Collections.Generic;
using Rigfall.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Rigfall.Runtime
{
    /// <summary>Tumbleweeds roll slowly across the Permian Pad.</summary>
    public sealed class Tumbleweed : MonoBehaviour
    {
        Vector3 dir;
        void Start() { dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward; }
        void Update()
        {
            transform.position += dir * Time.deltaTime * 0.8f;
            transform.Rotate(Vector3.Cross(Vector3.up, dir), 120f * Time.deltaTime, Space.World);
            if (transform.position.magnitude > 60f) transform.position = -transform.position.normalized * 45f;
        }
    }
}
