using UnityEngine;

namespace WardenZero
{
    // The extraction chopper (Tripo body, separate main and tail rotors). Flies scripted legs
    // (eased, or at a steady cruise). Its attitude follows the flight like a real one: nose
    // down ~8 degrees at cruise and a little more while speeding up, nose up to flare when it
    // slows, banked only in turns (a coordinated turn's angle for its speed and turn rate,
    // capped at 22 degrees), and a slow drift and bob in the hover. Rotor blades spin up with
    // `spin`; past a certain speed a soft blurred disc takes over, because real rotor speed
    // strobes at 60 fps.
    public class Chopper : MonoBehaviour
    {
        public Transform body; // pitch and bank go here; the root carries position and heading
        public Transform mainRotor;
        public Transform tailRotor;
        public Renderer mainDisc;
        public Renderer tailDisc;
        public Transform door; // cabin door, at floor height: where the Warden boards and jumps
        public Transform seat; // where he rides in the cabin
        public ParticleSystem wash; // rotor wash dust, only near the ground
        [Range(0, 1)] public float spin = 1;

        public bool Moving => legTime < legDuration;
        public Vector3 Velocity { get; private set; }

        Vector3 from, to;
        float fromYaw, toYaw;
        float legTime, legDuration;
        bool eased = true;
        float lastYaw, yawRate, lastForward, forwardAccel;
        float yaw;
        float mainAngle, tailAngle;
        Vector3 lastPos;
        Vector3 smoothVel;
        float bobPhase;
        MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // Ground height under the chopper, for the wash (the arena floor by default).
        public System.Func<Vector3, float> groundHeight = _ => 0;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            yaw = transform.eulerAngles.y;
            lastPos = transform.position;
        }

        public void Place(Vector3 position, float heading)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, heading, 0));
            yaw = heading;
            from = to = lastPos = position;
            fromYaw = toYaw = heading;
            legTime = legDuration = 0;
            Velocity = smoothVel = Vector3.zero;
            lastYaw = heading;
            yawRate = lastForward = forwardAccel = 0;
        }

        // Fly to `target`, turning to `heading` (degrees), over `seconds`, eased in and out
        // (or at a constant speed: a cruise leg that is already under way).
        public void FlyTo(Vector3 target, float heading, float seconds, bool ease = true)
        {
            eased = ease;
            from = transform.position;
            to = target;
            fromYaw = yaw;
            toYaw = fromYaw + Mathf.DeltaAngle(fromYaw, heading);
            legTime = 0;
            legDuration = Mathf.Max(0.01f, seconds);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            if (legTime < legDuration)
            {
                legTime = Mathf.Min(legDuration, legTime + dt);
                float k = eased ? Mathf.SmoothStep(0, 1, legTime / legDuration) : legTime / legDuration;
                transform.position = Vector3.Lerp(from, to, k);
                yaw = Mathf.Lerp(fromYaw, toYaw, k);
                transform.rotation = Quaternion.Euler(0, yaw, 0);
            }

            Vector3 vel = (transform.position - lastPos) / dt;
            lastPos = transform.position;
            smoothVel = Vector3.Lerp(smoothVel, vel, Mathf.Min(1, dt * 3));
            Velocity = smoothVel;

            float heading = transform.eulerAngles.y;
            yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, heading) / dt, Mathf.Min(1, dt * 3));
            lastYaw = heading;
            float forward = Vector3.Dot(smoothVel, transform.forward);
            forwardAccel = Mathf.Lerp(forwardAccel, (forward - lastForward) / dt, Mathf.Min(1, dt * 2));
            lastForward = forward;
            float speed = new Vector2(smoothVel.x, smoothVel.z).magnitude;
            float pitch = Mathf.Clamp(forward * 0.12f + forwardAccel * 0.6f, -8, 10);
            float bank = Mathf.Clamp(-Mathf.Atan(speed * yawRate * Mathf.Deg2Rad / 9.81f) * Mathf.Rad2Deg, -22, 22);
            // Hover: a slow wander and bob (fades out with speed and when the rotor stops).
            bobPhase += dt;
            float hover = (1 - Mathf.Clamp01(speed / 8)) * (spin > 0.9f ? 1 : 0);
            Vector3 drift = new Vector3(Mathf.PerlinNoise(bobPhase * 0.25f, 3) - 0.5f, Mathf.Sin(bobPhase * 1.3f) * 0.12f, Mathf.PerlinNoise(7, bobPhase * 0.25f) - 0.5f) * hover;
            drift.x *= 0.6f;
            drift.z *= 0.6f;
            pitch += (Mathf.PerlinNoise(bobPhase * 0.4f, 11) - 0.5f) * 2 * hover;
            bank += (Mathf.PerlinNoise(13, bobPhase * 0.4f) - 0.5f) * 2 * hover;
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(pitch, 0, bank), Mathf.Min(1, dt * 1.5f));
            body.localPosition = drift;

            // Rotors: blades turn at up to ~2 rev/s on screen, then the blur disc fades in.
            mainAngle += Mathf.Lerp(0, 720, spin) * dt;
            tailAngle += Mathf.Lerp(0, 1500, spin) * dt;
            mainRotor.localRotation = Quaternion.Euler(0, mainAngle, 0);
            tailRotor.localRotation = Quaternion.Euler(tailAngle, 0, 0);
            float blur = Mathf.Clamp01((spin - 0.5f) / 0.5f);
            SetAlpha(mainDisc, blur * 0.32f);
            SetAlpha(tailDisc, blur * 0.4f);

            if (wash != null)
            {
                float height = transform.position.y - groundHeight(transform.position);
                var emission = wash.emission;
                emission.rateOverTime = spin > 0.5f && height < 22 ? Mathf.Lerp(90, 10, height / 22) : 0;
                var p = transform.position;
                wash.transform.position = new Vector3(p.x, groundHeight(p) + 0.2f, p.z);
            }
        }

        void SetAlpha(Renderer r, float a)
        {
            if (r == null) return;
            r.enabled = a > 0.01f;
            block.SetColor(BaseColorId, new Color(1, 1, 1, a));
            r.SetPropertyBlock(block);
        }
    }
}
