using UnityEngine;
using J = WardenZero.GameConfig.Jeep;

namespace WardenZero
{
    // Draws the jeep from its Vehicle (visuals only). The pivots keep their names whatever
    // hangs under them, so the Tripo parts (M3 iteration 12) drop in where the greybox is:
    //   Jeep (the chassis on the ground: the Vehicle's position, yaw, pitch and roll)
    //     Body (sprung: squats, dives, leans and bounces on top of the chassis)
    //       Steering (turns about its column, its local X, 270 degrees at full lock)
    //       GunYoke (yaws on the pedestal) / Gun (pitches) / Muzzle
    //       Driver, Gunner (seats), DoorL, DoorR (where he gets in and out)
    //     Wheel_FL, Wheel_FR, Wheel_RL, Wheel_RR (hubs: the fronts steer, all spin)
    public class VehicleRig : MonoBehaviour
    {
        public Transform body;
        public Transform[] wheels; // FL, FR, RL, RR
        public Transform steering;
        public Transform gunYoke;
        public Transform gun;
        public Transform muzzle;
        public Transform driver;
        public Transform gunner;
        public Transform doorL;
        public Transform doorR;
        public ParticleSystem dust; // thrown up behind the rear wheels
        public Transform muzzleFlash; // a glow at the muzzle, shown for a moment per round
        public Light muzzleLight;

        public const float DustPerSpeed = 5; // particles a second per m/s (half on Low)
        public const float FlashTime = 0.05f;
        float flash;
        int flashFrame; // shown for at least the frame it fired in, however long that frame

        public const float SteeringTurns = 270; // degrees of steering wheel at full lock
        // The body's spring: degrees of pitch per m/s² of braking or drive, of roll per m/s² of
        // cornering, and how fast it settles.
        public const float PitchPerAccel = 0.45f, RollPerAccel = 0.5f, Stiffness = 60, Damping = 9;

        Vector3 bodyHome;
        Vector3 tilt, tiltSpeed; // pitch, roll, heave (degrees, degrees, metres)
        float lastForward;
        Quaternion[] wheelHome;
        Quaternion steeringHome;
        Vector3 flashHome;

        public float BodyPitch => tilt.x;
        public float BodyRoll => tilt.y;

        void Awake()
        {
            bodyHome = body.localPosition;
            wheelHome = System.Array.ConvertAll(wheels, w => w.localRotation);
            steeringHome = steering.localRotation;
            if (muzzleFlash != null) flashHome = muzzleFlash.localScale;
        }

        // Put the jeep where the Vehicle is, at rest (a spawn, a reset): no spring motion.
        public void Snap(Vehicle v)
        {
            tilt = tiltSpeed = Vector3.zero;
            lastForward = v.ForwardSpeed;
            Pose(v, 0);
        }

        public void Pose(Vehicle v, float dt)
        {
            transform.SetPositionAndRotation(v.Position, v.Rotation);
            if (dt > 0)
            {
                // The spring is pushed by the jeep's accelerations: braking dips the nose,
                // driving squats it, cornering leans it out, a landing drops it.
                float along = (v.ForwardSpeed - lastForward) / dt;
                lastForward = v.ForwardSpeed;
                float side = v.Airborne ? 0 : v.Speed * v.YawRate;
                Vector3 want = new Vector3(-along * PitchPerAccel, side * RollPerAccel, 0);
                want.x = Mathf.Clamp(want.x, -6, 6);
                want.y = Mathf.Clamp(want.y, -7, 7);
                if (v.Landing > 0) tiltSpeed.z -= v.Landing * 0.35f;
                Vector3 accel = (want - tilt) * Stiffness - tiltSpeed * Damping;
                tiltSpeed += accel * dt;
                tilt += tiltSpeed * dt;
                tilt.z = Mathf.Clamp(tilt.z, -0.25f, 0.15f);
            }
            body.localPosition = bodyHome + Vector3.up * tilt.z;
            // Pitch about the side axis (nose up positive), roll about the long axis (right down).
            body.localRotation = Quaternion.Euler(-tilt.x, 0, -tilt.y);

            float spin = v.WheelTurn * Mathf.Rad2Deg;
            for (int i = 0; i < wheels.Length; i++)
            {
                float steer = i < 2 ? v.SteerAngle : 0;
                wheels[i].localRotation = Quaternion.Euler(0, steer, 0) * wheelHome[i] * Quaternion.Euler(spin, 0, 0);
            }
            steering.localRotation = steeringHome * Quaternion.Euler(v.SteerInput * SteeringTurns, 0, 0);

            if (dust != null)
            {
                // Dust off the dirt with speed; none in the air; half as much on phones.
                var em = dust.emission;
                float rate = v.Airborne || v.Speed < 3 ? 0 : v.Speed * DustPerSpeed;
                em.rateOverTime = Quality.Current == QualityTier.Low ? rate / 2 : rate;
            }
            flash -= dt;
            if (muzzleFlash != null && muzzleFlash.gameObject.activeSelf && flash <= 0 && Time.frameCount != flashFrame)
            {
                muzzleFlash.gameObject.SetActive(false);
                if (muzzleLight != null) muzzleLight.enabled = false;
            }
        }

        // A round leaves the gun: the flash and its light for a moment.
        public void Flash()
        {
            if (muzzleFlash == null) return;
            flash = FlashTime;
            flashFrame = Time.frameCount;
            muzzleFlash.gameObject.SetActive(true);
            muzzleFlash.localScale = flashHome * Random.Range(0.8f, 1.2f);
            if (muzzleLight != null) muzzleLight.enabled = true;
        }
    }
}
