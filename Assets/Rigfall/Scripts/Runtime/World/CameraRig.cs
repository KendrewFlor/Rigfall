// The top-down camera: follows the hero at a fixed tilt with a little look-ahead, shakes on
// big hits, and slowly orbits the field behind the main menu.
using UnityEngine;

namespace Rigfall.Runtime
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        public const float Pitch = 52f;
        public const float BaseDistance = 24f;

        Camera cam;
        Transform target;
        Vector3 velocity, lastTargetPos, orbitCenter;
        float shake, orbitAngle;
        bool orbit;

        public Camera Cam => cam;

        public static CameraRig Create(Transform parent)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.08f, 0.12f, 0.15f);
            c.fieldOfView = 38f;
            c.nearClipPlane = 0.5f;
            c.farClipPlane = 260f;
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<CameraRig>();
            rig.cam = c;
            Instance = rig;
            return rig;
        }

        public void Follow(Transform t, bool snap)
        {
            orbit = false;
            target = t;
            if (t != null) lastTargetPos = t.position;
            if (snap && t != null) transform.position = Desired(t.position, Vector3.zero);
            transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
        }

        public void MenuOrbit(Vector3 center)
        {
            orbit = true;
            target = null;
            orbitCenter = center;
        }

        public void Shake(float amount) { shake = Mathf.Min(1.2f, Mathf.Max(shake, amount)); }

        Vector3 Desired(Vector3 focus, Vector3 lead)
        {
            float dist = BaseDistance * Game.Settings.cameraZoom;
            Vector3 back = Quaternion.Euler(Pitch, 0f, 0f) * Vector3.back * dist;
            return focus + lead + back + Vector3.up * 1f;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (orbit)
            {
                orbitAngle += dt * 4f;
                Vector3 pos = orbitCenter + Quaternion.Euler(0f, orbitAngle, 0f) * new Vector3(0f, 16f, -30f);
                transform.position = pos;
                transform.LookAt(orbitCenter + Vector3.up * 3f);
            }
            else if (target != null)
            {
                Vector3 moved = (target.position - lastTargetPos) / Mathf.Max(0.0001f, Time.deltaTime);
                lastTargetPos = target.position;
                Vector3 lead = Vector3.ClampMagnitude(MapMath.Flat(moved) * 0.35f, 3f);
                Vector3 want = Desired(target.position, Time.deltaTime > 0f ? lead : Vector3.zero);
                transform.position = Vector3.SmoothDamp(transform.position, want, ref velocity, 0.18f, Mathf.Infinity, dt);
                transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
            }

            if (shake > 0.001f)
            {
                transform.position += Random.insideUnitSphere * shake * 0.45f;
                shake = Mathf.MoveTowards(shake, 0f, dt * 2.8f);
            }
        }
    }
}
