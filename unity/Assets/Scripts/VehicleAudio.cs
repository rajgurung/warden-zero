using UnityEngine;
using J = WardenZero.GameConfig.Jeep;

namespace WardenZero
{
    // The jeep's sound (synthesised, SynthAudio): the engine through three simulated gears
    // (pitch climbs with speed in each, drops at a shift; louder under throttle), tyres
    // skidding when it slides, and a clunk (with a camera shake) when it lands hard or hits
    // something. Quiet while parked with nobody in it.
    public class VehicleAudio : MonoBehaviour
    {
        // Top of each gear, m/s.
        public static readonly float[] Gears = { 7, 14, J.TopSpeed };
        public const float IdlePitch = 0.6f, LowPitch = 0.75f, HighPitch = 1.55f;

        // The engine's pitch at a speed and throttle.
        public static float EnginePitch(float speed, float throttle)
        {
            speed = Mathf.Abs(speed);
            float lo = 0;
            foreach (float hi in Gears)
            {
                if (speed <= hi || hi == Gears[Gears.Length - 1])
                {
                    float rev = Mathf.Clamp01((speed - lo) / (hi - lo));
                    float pitch = Mathf.Lerp(LowPitch, HighPitch, rev) + 0.08f * Mathf.Clamp01(Mathf.Abs(throttle));
                    // Pulling away from rest the clutch slips up from the idle.
                    if (lo == 0) pitch = Mathf.Lerp(IdlePitch + 0.15f * Mathf.Abs(throttle), pitch, Mathf.Clamp01(speed / 2));
                    return pitch;
                }
                lo = hi;
            }
            return HighPitch;
        }

        public static float EngineVolume(float throttle, bool occupied) => occupied ? 0.22f + 0.2f * Mathf.Clamp01(Mathf.Abs(throttle)) : 0;

        // How much of a shake (0..1) a knock of this speed gives the camera.
        public static float ImpactShake(float impact) => Mathf.Clamp01((impact - 1.5f) / 8);

        AudioSource engine, skid, sfx;
        VehicleController jeep;
        float throttle;

        public float EngineLevel => engine.volume;

        void Awake()
        {
            jeep = GetComponent<VehicleController>();
            engine = Loop(SynthAudio.Engine);
            skid = Loop(SynthAudio.Skid);
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0.6f;
        }

        AudioSource Loop(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = 0;
            s.spatialBlend = 0.6f;
            s.playOnAwake = false;
            s.Play();
            return s;
        }

        // From VehicleController each frame: the throttle the driver (or the ride) asked for.
        public void Throttle(float t) => throttle = t;

        public void Gunshot() => sfx.PlayOneShot(SynthAudio.Hmg, 0.55f);

        void Update()
        {
            var gm = GameManager.Instance;
            bool live = gm != null && gm.IsPlaying;
            var v = jeep.Vehicle;
            bool running = live && (jeep.Occupied != VehicleController.Seat.None || jeep.Ride != null);
            float dt = Time.deltaTime;
            engine.pitch = Mathf.Lerp(engine.pitch, EnginePitch(v.ForwardSpeed, throttle), Mathf.Min(1, dt * 8));
            engine.volume = Mathf.MoveTowards(engine.volume, EngineVolume(throttle, running), dt * 1.5f);
            float slide = Mathf.Abs(Vector2.Dot(v.Velocity, new Vector2(v.Right.x, v.Right.z)));
            skid.volume = Mathf.MoveTowards(skid.volume, running && !v.Airborne ? Mathf.Clamp01((slide - 1.5f) / 4) * 0.5f : 0, dt * 3);
            float knock = Mathf.Max(v.Impact, v.Landing * 0.6f);
            if (live && knock > 2)
            {
                sfx.PlayOneShot(SynthAudio.Clunk, Mathf.Clamp01(knock / 8));
                gm.cameraFollow.AddShake(ImpactShake(knock));
            }
            if (!live) engine.volume = skid.volume = 0;
        }
    }
}
