using UnityEngine;

namespace WardenZero
{
    // Debug URL ?sidecam: a picture-in-picture side view (bottom right) of the Warden, or of
    // the chopper while it is flying without him, for checking poses and attitude in the
    // set pieces from a second angle.
    public class DebugSideCam : MonoBehaviour
    {
        Camera cam;

        public static void AddIfAsked(string url)
        {
            if (url == null || !url.Contains("sidecam")) return;
            var go = new GameObject("DebugSideCam");
            go.AddComponent<DebugSideCam>();
        }

        void Start()
        {
            cam = gameObject.AddComponent<Camera>();
            cam.rect = new Rect(0.6f, 0.03f, 0.38f, 0.38f);
            cam.depth = 50;
            cam.fieldOfView = 40;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 3000;
            cam.clearFlags = CameraClearFlags.Skybox;
        }

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            Transform target = gm.player.transform;
            float distance = 7;
            var chopper = FindFirstObjectByType<Chopper>();
            // The chopper while he rides it (or when it flies alone, in the arena), else him.
            if (chopper != null && chopper.isActiveAndEnabled && (target.parent != null || FindFirstObjectByType<Extraction>() != null))
            {
                distance = target.parent != null ? 22 : 40;
                target = chopper.transform;
            }
            // From his right side, level with him, a little ahead.
            Vector3 heading = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (heading.sqrMagnitude < 1e-3f) heading = Vector3.ProjectOnPlane(target.up, Vector3.up);
            heading.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, heading);
            Vector3 focus = target.position + Vector3.up * (target == gm.player.transform ? 1.3f : 3);
            transform.position = focus + side * distance + heading * distance * 0.3f + Vector3.up * distance * 0.1f;
            transform.LookAt(focus);
        }
    }
}
