using UnityEngine;

namespace WardenZero
{
    // Eases the camera toward the Warden each frame, plus a decaying shake.
    // Mirrors Stage.follow() in the Babylon version.
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;

        Vector3 lookPoint;
        float shake;

        void Start()
        {
            Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            transform.position = target.position + GameConfig.CameraOffset;
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
            float dt = Time.deltaTime;
            Vector3 want = target.position + GameConfig.CameraOffset;
            transform.position = Vector3.Lerp(transform.position, want, Mathf.Min(1, dt * 6));
            Vector3 wantLook = target.position + new Vector3(0, 0, GameConfig.CameraLookAhead);
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
