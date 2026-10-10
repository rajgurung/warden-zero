using UnityEngine;

namespace WardenZero
{
    // The chopper's sound: rotor chop and turbine whine as a 3D source that fades with
    // distance, plus a muffled cabin mix for when the Warden rides inside. The Doppler shift
    // is worked out here from the listener's and the chopper's speeds (WebGL audio has no
    // built-in Doppler). Spool-up follows Chopper.spin.
    [RequireComponent(typeof(Chopper))]
    public class ChopperAudio : MonoBehaviour
    {
        [Range(0, 1)] public float inside; // 0 = heard from outside, 1 = from the cabin
        public float volume = 0.9f;

        Chopper chopper;
        AudioSource open, cabin;
        Transform listener;
        Vector3 lastListener;
        float shownPitch = 1;

        void Awake()
        {
            chopper = GetComponent<Chopper>();
            open = Source("Rotor", SynthAudio.Rotor, 1);
            cabin = Source("Cabin", SynthAudio.RotorMuffled, 0);
        }

        AudioSource Source(string name, AudioClip clip, float spatial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 5, 0);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = spatial;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 14;
            s.maxDistance = 900;
            s.dopplerLevel = 0;
            s.volume = 0;
            return s;
        }

        void OnEnable()
        {
            if (open == null) return;
            open.Play();
            cabin.Play();
        }

        void OnDisable()
        {
            if (open == null) return;
            open.Stop();
            cabin.Stop();
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            if (listener == null)
            {
                var l = FindFirstObjectByType<AudioListener>();
                if (l == null) return;
                listener = l.transform;
                lastListener = listener.position;
            }
            Vector3 lv = (listener.position - lastListener) / dt;
            lastListener = listener.position;
            // Doppler: closing speed along the line between them.
            Vector3 toSource = transform.position - listener.position;
            float dist = toSource.magnitude;
            float closing = dist > 1e-3f ? Vector3.Dot(lv - chopper.Velocity, toSource / dist) : 0;
            float doppler = Mathf.Clamp(343f / (343f - Mathf.Clamp(closing, -120, 120)), 0.75f, 1.35f);
            float spool = Mathf.Lerp(0.55f, 1, chopper.spin);
            shownPitch = Mathf.Lerp(shownPitch, doppler * spool, Mathf.Min(1, dt * 8));
            open.pitch = shownPitch;
            cabin.pitch = spool;
            float on = chopper.spin > 0.01f ? volume : 0;
            open.volume = on * (1 - inside);
            cabin.volume = on * inside * 0.8f;
        }
    }
}
