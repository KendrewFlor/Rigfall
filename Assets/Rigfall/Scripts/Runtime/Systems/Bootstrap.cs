// Entry point. Runs automatically after the first scene loads, so the project needs no
// hand-made scene content: an empty scene is enough (the build script creates one).
using UnityEngine;

namespace Rigfall.Runtime
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            // Remove a default camera/light if the scene happens to have them.
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam.GetComponent<CameraRig>() == null) Object.Destroy(cam.gameObject);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                Object.Destroy(light.gameObject);
            Game.Boot();
        }
    }
}
