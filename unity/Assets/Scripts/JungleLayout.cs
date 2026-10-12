using UnityEngine;

namespace WardenZero
{
    // The jungle patch's shape in plain numbers: the ground height function, the LZ, checkpoint
    // A, the stream and the trail (the road past A is RoadLayout). The scene builder makes the terrain from Height, and the
    // traversal course (CourseLayout) and its EditMode tests use the same function, so they
    // agree with the terrain without loading the scene (the terrain's heightmap samples this
    // every 0.625 m).
    public static class JungleLayout
    {
        public const float Rim = 7;
        public static readonly Vector3 Lz = new Vector3(-14, 0, -30);
        public static readonly Vector3 Checkpoint = new Vector3(22, 0, 32);
        public static readonly Vector2[] Stream = { new Vector2(-58, -6), new Vector2(-34, 4), new Vector2(-12, -3), new Vector2(4, 2), new Vector2(22, 10), new Vector2(40, 4), new Vector2(58, 9) };
        public static readonly Vector2[] Trail = { new Vector2(-14, -30), new Vector2(-9, -18), new Vector2(-6, -8), new Vector2(-3, 0), new Vector2(3, 10), new Vector2(12, 20), new Vector2(22, 32) };

        // Height of the ground (metres) anywhere: the patch's valley (with the drive carved into
        // it), the road tile (RoadLayout), and the landscape around them.
        public static float Height(float x, float z)
        {
            if (RoadLayout.InTile(x, z)) return RoadLayout.TileHeight(x, z, NaturalRaw(x, z));
            if (RoadLayout.InPatch(x, z)) return RoadLayout.Carve(x, z, NaturalRaw(x, z));
            return Landscape(x, z);
        }

        // The far landscape terrain's ground: the hills, lowered into the road's valley around
        // the tile (RoadLayout.Valley), so the tile's edges meet it.
        public static float Landscape(float x, float z)
        {
            float h = NaturalRaw(x, z);
            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) <= 86) return h;
            return Mathf.Min(h, RoadLayout.Valley(x, z, h));
        }

        // The land before any road: valley, rim, hills, mountains. It is flat at the rim (|x| or
        // |z| from 74 to 86 m), so the patch and the landscape meet without a seam at the patch
        // edge (80 m).
        public static float NaturalRaw(float x, float z)
        {
            float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            if (r <= 86)
            {
                float floor = (Fbm(x / 30, z / 30, 3) - 0.5f) * 2.4f;
                floor *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(12, 24, Vector2.Distance(new Vector2(x, z), new Vector2(Lz.x, Lz.z))));
                floor *= Mathf.SmoothStep(0.3f, 1, Mathf.InverseLerp(6, 14, Vector2.Distance(new Vector2(x, z), new Vector2(Checkpoint.x, Checkpoint.z))));
                float channel = Mathf.Exp(-Mathf.Pow(DistanceToPath(Stream, x, z) / 2.6f, 2)) * Mathf.Clamp01((62 - Mathf.Abs(x)) / 8);
                floor -= 2.6f * channel;
                return Mathf.Lerp(floor, Rim, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(44, 74, r)));
            }
            float d = new Vector2(x, z).magnitude;
            float rise = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(86, 420, r));
            float hills = Fbm(x / 420 + 7, z / 420 + 3, 4) * 140 + Mathf.Pow(Fbm(x / 1300 + 1, z / 1300 + 9, 3), 2) * 380;
            float mountains = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2200, 3800, d)) * (500 + Fbm(x / 700, z / 700, 3) * 500);
            return Rim + (hills - 40) * rise + mountains;
        }

        public static float Height(Vector3 p) => Height(p.x, p.z);

        public static float Fbm(float x, float z, int octaves)
        {
            float sum = 0, amp = 0.5f, freq = 1, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq + 13.7f * i, z * freq - 7.3f * i) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.1f;
            }
            return sum / norm;
        }

        public static float DistanceToPath(Vector2[] path, float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 a = path[i], b = path[i + 1];
                float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + (b - a) * t));
            }
            return best;
        }
    }
}
