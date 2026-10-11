using UnityEngine;

namespace WardenZero
{
    // A damped pendulum (angle in degrees from hanging straight down): the body swinging up
    // under the canopy after the opening jolt, and swaying in turns.
    public struct Pendulum
    {
        public float Angle;
        public float Speed; // degrees per second

        // stiffness in 1/s^2 (higher swings faster), damping in 1/s. `rest` is where it
        // settles (a lean in a turn).
        public void Step(float dt, float stiffness, float damping, float rest = 0)
        {
            float a = Angle * Mathf.Deg2Rad, r = rest * Mathf.Deg2Rad;
            float accel = -stiffness * (Mathf.Sin(a) - Mathf.Sin(r)) * Mathf.Rad2Deg - damping * Speed;
            Speed += accel * dt;
            Angle += Speed * dt;
        }
    }
}
