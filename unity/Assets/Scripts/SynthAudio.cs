using System;
using UnityEngine;

namespace WardenZero
{
    // Sounds for the drop, synthesised at runtime (no files, no licences): the chopper's rotor
    // and turbine, freefall wind, the canopy's opening snap and flutter, landing thud and roll,
    // and a jungle bed of insects and birds. Loops are generated a little long and cross-faded
    // into their start, so they repeat without a click. Clips are made once and cached.
    public static class SynthAudio
    {
        public const int Rate = 22050;

        static AudioClip rotor, rotorMuffled, wind, flutter, snap, thud, roll, jungle;

        public static AudioClip Rotor => rotor ??= Loop("Rotor", 2f, 0.25f, 1, t => RotorSample(t, false));
        public static AudioClip RotorMuffled => rotorMuffled ??= Loop("RotorMuffled", 2f, 0.25f, 2, t => RotorSample(t, true));
        public static AudioClip Wind => wind ??= Loop("Wind", 3f, 0.5f, 3, WindSample);
        public static AudioClip Flutter => flutter ??= Loop("Flutter", 1.5f, 0.25f, 4, FlutterSample);
        public static AudioClip Snap => snap ??= OneShot("Snap", 0.9f, 5, SnapSample);
        public static AudioClip Thud => thud ??= OneShot("Thud", 0.45f, 6, ThudSample);
        public static AudioClip Roll => roll ??= OneShot("Roll", 0.9f, 7, RollSample);
        public static AudioClip Jungle => jungle ??= Loop("Jungle", 12f, 1f, 8, JungleSample);

        // ------------------------------------------------------------------ generators

        // Per-clip state for the generators (filters, noise). Reset for each clip.
        static System.Random rng;
        static float lp1, lp2, lp3;
        static float Noise() => (float)rng.NextDouble() * 2 - 1;
        static float Lp(ref float state, float x, float cutoff) => state += (x - state) * (1 - Mathf.Exp(-2 * Mathf.PI * cutoff / Rate));
        static float Sin(float hz, float t) => Mathf.Sin(2 * Mathf.PI * hz * t);

        // Blade passes at 16 Hz (a four-blade rotor at 4 rev/s): each is a sharp chop of
        // low-passed noise and a low thump; one blade a touch louder per revolution; the
        // turbine adds a whine and a hiss. Muffled = the cabin's view: lows only.
        static float RotorSample(float t, bool muffled)
        {
            float phase = t * 16 % 1;
            float chop = Mathf.Exp(-phase * 7) * (1 + 0.18f * Sin(4, t));
            float n = Noise();
            float body = Lp(ref lp1, n, muffled ? 140 : 420) * 3.2f;
            float thump = Sin(32, t) * 0.5f + Sin(48, t) * 0.25f;
            float s = (body + thump) * chop;
            float hiss = n - Lp(ref lp2, n, 2500);
            float whine = Sin(1200, t) * 0.5f + Sin(2400, t) * 0.25f + Sin(1800, t) * 0.15f;
            if (muffled) s += Lp(ref lp3, Noise(), 60) * 1.5f + whine * 0.02f;
            else s += whine * 0.07f + hiss * 0.05f;
            return s;
        }

        // Broadband rush with a slow gust: freefall (loud, pitched up with speed) and canopy (soft).
        static float WindSample(float t)
        {
            float n = Noise();
            float a = Lp(ref lp1, n, 900);
            float b = Lp(ref lp2, n, 250);
            float gust = 0.75f + 0.25f * Sin(1 / 3f, t) + 0.08f * Sin(2, t);
            return (a * 0.9f + b * 1.6f) * gust;
        }

        // Fabric flapping at ~12 Hz.
        static float FlutterSample(float t)
        {
            float n = Lp(ref lp1, Noise(), 1500);
            float flap = Mathf.Pow(0.5f + 0.5f * Sin(12, t), 3);
            return n * flap * 1.5f;
        }

        // The canopy opening: a deep whump as it bites, then a decaying flutter.
        static float SnapSample(float t)
        {
            float whump = Lp(ref lp1, Noise(), 500) * 3 * Mathf.Exp(-t * 9) + Mathf.Sin(2 * Mathf.PI * (110 - 60 * t) * t) * Mathf.Exp(-t * 7) * 0.8f;
            float crack = Noise() * Mathf.Exp(-t * 60) * 0.6f;
            float flap = Lp(ref lp2, Noise(), 1800) * Mathf.Pow(0.5f + 0.5f * Sin(13, t), 3) * Mathf.Exp(-t * 3) * 1.4f;
            return whump + crack + flap;
        }

        static float ThudSample(float t)
        {
            return Sin(55, t) * Mathf.Exp(-t * 14) * 1.2f + Lp(ref lp1, Noise(), 300) * Mathf.Exp(-t * 25) * 3 + Noise() * Mathf.Exp(-t * 90) * 0.3f;
        }

        // Rolling on leaf litter: rustle in a few bumps.
        static float RollSample(float t)
        {
            float bumps = Mathf.Exp(-((t - 0.1f) * (t - 0.1f)) / 0.004f) + 0.7f * Mathf.Exp(-((t - 0.35f) * (t - 0.35f)) / 0.006f) + 0.4f * Mathf.Exp(-((t - 0.6f) * (t - 0.6f)) / 0.01f);
            float rustle = Lp(ref lp1, Noise(), 2200) - Lp(ref lp2, Noise(), 200) * 0.5f;
            return rustle * bumps * 1.6f + ThudSample(t) * 0.4f;
        }

        // Cicadas swelling and fading, a second high insect, a few bird calls, leaves.
        static readonly float[] BirdTimes = { 0.7f, 1.1f, 3.4f, 3.6f, 5.9f, 7.2f, 7.45f, 9.3f, 10.8f };
        static float JungleSample(float t)
        {
            float swell = 0.5f + 0.5f * Sin(1 / 6f, t);
            float cicada = Sin(4400, t) * Mathf.Pow(0.5f + 0.5f * Sin(45, t), 4) * swell * 0.18f;
            float cricket = Sin(6200, t) * Mathf.Pow(Mathf.Max(0, Sin(18, t)), 6) * (0.5f + 0.5f * Sin(1 / 4f, t + 1)) * 0.06f;
            float bird = 0;
            for (int i = 0; i < BirdTimes.Length; i++)
            {
                float d = t - BirdTimes[i];
                if (d < 0 || d > 0.22f) continue;
                float f = 2600 + 1400 * (d / 0.22f) * (i % 2 == 0 ? 1 : -0.6f) + 150 * Mathf.Sin(d * 2 * Mathf.PI * 30);
                bird += Mathf.Sin(2 * Mathf.PI * f * d) * Mathf.Sin(Mathf.PI * d / 0.22f) * 0.22f;
            }
            float leaves = Lp(ref lp1, Noise(), 700) * (0.4f + 0.3f * Sin(1 / 12f, t)) * 0.6f;
            return cicada + cricket + bird + leaves;
        }

        // ------------------------------------------------------------------ clip building

        static AudioClip Loop(string name, float seconds, float fade, int seed, Func<float, float> sample)
        {
            int n = Mathf.RoundToInt(seconds * Rate), f = Mathf.RoundToInt(fade * Rate);
            var raw = Render(n + f, seed, sample);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = raw[i];
            // The tail fades out over the head: sample n continues smoothly into sample 0.
            for (int i = 0; i < f; i++)
            {
                float k = i / (float)f;
                data[i] = raw[i] * k + raw[n + i] * (1 - k);
            }
            return Make(name, data);
        }

        static AudioClip OneShot(string name, float seconds, int seed, Func<float, float> sample)
        {
            var data = Render(Mathf.RoundToInt(seconds * Rate), seed, sample);
            int tail = Rate / 50;
            for (int i = 0; i < tail; i++) data[data.Length - 1 - i] *= i / (float)tail;
            return Make(name, data);
        }

        static float[] Render(int count, int seed, Func<float, float> sample)
        {
            rng = new System.Random(seed);
            lp1 = lp2 = lp3 = 0;
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = sample(i / (float)Rate);
            return data;
        }

        static AudioClip Make(string name, float[] data)
        {
            float peak = 1e-6f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            float gain = 0.9f / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
