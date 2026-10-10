using UnityEngine;

namespace WardenZero
{
    // Eases the camera toward the Warden each frame, plus a decaying shake.
    // Mirrors Stage.follow() in the Babylon version.
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        // Main menu: slow drift over the empty arena instead of following the Warden.
        public bool attract;
        // Behind and above the Warden; the jungle sits lower, under the tree crowns.
        public Vector3 offset = GameConfig.CameraOffset;

        Vector3 lookPoint;
        float shake;

        void Start()
        {
            Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            transform.position = target.position + offset;
            lookPoint = target.position + new Vector3(0, 0, GameConfig.CameraLookAhead);
            transform.LookAt(lookPoint);
        }

        public void AddShake(float amount)
        {
            shake = Mathf.Max(shake, amount);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            if (Time.timeScale == 0) return;
            Vector3 focus = target.position;
            if (attract)
            {
                float a = Time.unscaledTime / 9;
                focus = new Vector3(Mathf.Sin(a) * 18, 0, Mathf.Cos(a * 0.7f) * 10);
            }
            Vector3 want = focus + offset;
            transform.position = Vector3.Lerp(transform.position, want, Mathf.Min(1, dt * 6));
            Vector3 wantLook = focus + new Vector3(0, 0, GameConfig.CameraLookAhead);
            lookPoint = Vector3.Lerp(lookPoint, wantLook, Mathf.Min(1, dt * 8));
            if (shake > 0)
            {
                shake = Mathf.Max(0, shake - dt * 1.8f);
                transform.position += Random.insideUnitSphere * shake * 0.35f;
            }
            transform.LookAt(lookPoint);
        }
    }
}
