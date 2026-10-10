using UnityEngine;

namespace WardenZero
{
    // Keeps a sprite parallel to the camera's view plane so the 2D art reads
    // undistorted from the angled top-down camera. The sprite pivot sits at the
    // feet, so characters stay planted on the ground.
    [DefaultExecutionOrder(100)] // after CameraFollow has moved the camera
    public class Billboard : MonoBehaviour
    {
        static Transform cam;

        // Extra roll in degrees (enemy waddle).
        public float tilt;

        void LateUpdate()
        {
            if (cam == null)
            {
                if (Camera.main == null) return;
                cam = Camera.main.transform;
            }
            transform.rotation = tilt == 0 ? cam.rotation : cam.rotation * Quaternion.Euler(0, 0, tilt);
        }
    }
}
