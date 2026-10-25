using System.Collections.Generic;
using Rigfall.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Rigfall.Runtime
{
    public sealed class Spinner : MonoBehaviour
    {
        public float speed = 90f;
        void Update() { transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.World); }
    }
}
