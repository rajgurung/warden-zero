using UnityEngine;

namespace WardenZero
{
    // A road's centreline on the ground plane (x, z): a centripetal Catmull-Rom curve through
    // control points, resampled every metre of arc length, with a height profile. Plain C#, so
    // the terrain builder, the jeep, the bounds and their EditMode tests share it.
    public class RoadPath
    {
        public struct Sample
        {
            public Vector2 Position;
            public Vector2 Tangent; // unit, along the road
            public float Curvature; // 1/m, positive turning left
            public float Height; // the road's surface
        }

        public struct Hit
        {
            public float S; // arc length along the road
            public float Lateral; // signed distance from the centreline, positive to the right
            public int Segment; // the 1 m piece it falls on
            public float Distance => Mathf.Abs(Lateral);
        }

        public const float Step = 1; // metres between samples
        public const float CentreSmooth = 8; // the centreline's smoothing, metres either side

        public readonly Vector2[] Points;
        public float Length { get; private set; }
        public int Count => pos.Length;
        public float MinHeight { get; private set; }

        Vector2[] pos;
        Vector2[] tan;
        float[] curv;
        float[] height;
        Vector2 min, max; // bounds of the samples

        // heights: the profile at each control point (null: flat at 0). The profile is linear
        // in arc length between them, then smoothed over `smooth` metres either side, so its
        // grade never exceeds the steepest straight piece and crests stay round.
        public RoadPath(Vector2[] points, float[] heights = null, float smooth = 12)
        {
            Points = points;
            // Dense samples of the curve, then even ones by arc length.
            var dense = new System.Collections.Generic.List<Vector2>();
            var knotAt = new float[points.Length]; // arc length of each control point
            const int perSegment = 64;
            float run = 0;
            dense.Add(points[0]);
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 p0 = i > 0 ? points[i - 1] : 2 * points[0] - points[1];
                Vector2 p1 = points[i], p2 = points[i + 1];
                Vector2 p3 = i + 2 < points.Length ? points[i + 2] : 2 * points[i + 1] - points[i];
                for (int k = 1; k <= perSegment; k++)
                {
                    var q = Centripetal(p0, p1, p2, p3, k / (float)perSegment);
                    run += Vector2.Distance(dense[dense.Count - 1], q);
                    dense.Add(q);
                }
                knotAt[i + 1] = run;
            }
            // Even samples by arc length, then a light smoothing (CentreSmooth metres either side)
            // that evens out the curvature the spline bunches at its points, then even again.
            pos = Resample(dense, run);
            float raw = run;
            var smoothed = Smooth(pos, Mathf.RoundToInt(CentreSmooth / Step));
            pos = Resample(smoothed, Polyline(smoothed));
            for (int i = 0; i < knotAt.Length; i++) knotAt[i] *= Length / raw;
            int count = pos.Length;
            tan = new Vector2[count];
            curv = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 a = pos[Mathf.Max(0, i - 1)], b = pos[Mathf.Min(count - 1, i + 1)];
                tan[i] = (b - a).normalized;
            }
            for (int i = 0; i < count; i++)
            {
                int a = Mathf.Max(0, i - 2), b = Mathf.Min(count - 1, i + 2);
                float turn = Vector2.SignedAngle(tan[a], tan[b]) * Mathf.Deg2Rad;
                curv[i] = turn / Mathf.Max(SAt(b) - SAt(a), 1e-3f);
            }
            min = max = pos[0];
            foreach (var p in pos)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            height = Profile(heights, knotAt, smooth);
            MinHeight = Mathf.Min(height);
        }

        static float Polyline(System.Collections.Generic.IList<Vector2> pts)
        {
            float run = 0;
            for (int i = 1; i < pts.Count; i++) run += Vector2.Distance(pts[i - 1], pts[i]);
            return run;
        }

        // Points every Step metres along a polyline `length` long (the last one at its end).
        Vector2[] Resample(System.Collections.Generic.IList<Vector2> pts, float length)
        {
            Length = length;
            int n = Mathf.FloorToInt(length / Step) + 1;
            int count = n + (length - (n - 1) * Step > 0.01f ? 1 : 0);
            var res = new Vector2[count];
            float walked = 0;
            int j = 0;
            for (int i = 0; i < count; i++)
            {
                float s = Mathf.Min(i * Step, length);
                while (j < pts.Count - 2 && walked + Vector2.Distance(pts[j], pts[j + 1]) < s)
                {
                    walked += Vector2.Distance(pts[j], pts[j + 1]);
                    j++;
                }
                float seg = Vector2.Distance(pts[j], pts[j + 1]);
                res[i] = Vector2.Lerp(pts[j], pts[j + 1], seg > 1e-6f ? (s - walked) / seg : 0);
            }
            return res;
        }

        // A moving average over `w` samples either side; the window shrinks at the ends, so they
        // stay put.
        static Vector2[] Smooth(Vector2[] pts, int w)
        {
            var res = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                int r = Mathf.Min(w, Mathf.Min(i, pts.Length - 1 - i));
                Vector2 sum = Vector2.zero;
                for (int q = i - r; q <= i + r; q++) sum += pts[q];
                res[i] = sum / (2 * r + 1);
            }
            return res;
        }

        float SAt(int i) => Mathf.Min(i * Step, Length);

        float[] Profile(float[] heights, float[] knotAt, float smooth)
        {
            var h = new float[pos.Length];
            if (heights == null) return h;
            var lin = new float[pos.Length];
            int k = 0;
            for (int i = 0; i < pos.Length; i++)
            {
                float s = SAt(i);
                while (k < knotAt.Length - 2 && s > knotAt[k + 1]) k++;
                float span = knotAt[k + 1] - knotAt[k];
                lin[i] = Mathf.Lerp(heights[k], heights[k + 1], span > 1e-6f ? Mathf.Clamp01((s - knotAt[k]) / span) : 0);
            }
            int w = Mathf.RoundToInt(smooth / Step);
            for (int i = 0; i < pos.Length; i++)
            {
                // A window that shrinks at the ends, so the end heights stay exact.
                int r = Mathf.Min(w, Mathf.Min(i, pos.Length - 1 - i));
                float sum = 0;
                for (int q = i - r; q <= i + r; q++) sum += lin[q];
                h[i] = sum / (2 * r + 1);
            }
            return h;
        }

        // Centripetal Catmull-Rom (alpha 0.5): no cusps or loops between the points.
        static Vector2 Centripetal(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float u)
        {
            float t0 = 0;
            float t1 = t0 + Mathf.Sqrt(Mathf.Max(Vector2.Distance(p0, p1), 1e-4f));
            float t2 = t1 + Mathf.Sqrt(Mathf.Max(Vector2.Distance(p1, p2), 1e-4f));
            float t3 = t2 + Mathf.Sqrt(Mathf.Max(Vector2.Distance(p2, p3), 1e-4f));
            float t = Mathf.Lerp(t1, t2, u);
            Vector2 a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            Vector2 a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            Vector2 a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            Vector2 b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            Vector2 b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }

        // The road at arc length s (clamped to its ends).
        public Sample At(float s)
        {
            s = Mathf.Clamp(s, 0, Length);
            int i = Mathf.Min(Mathf.FloorToInt(s / Step), pos.Length - 2);
            float f = Mathf.Clamp01((s - SAt(i)) / Mathf.Max(SAt(i + 1) - SAt(i), 1e-6f));
            return new Sample
            {
                Position = Vector2.Lerp(pos[i], pos[i + 1], f),
                Tangent = Vector2.Lerp(tan[i], tan[i + 1], f).normalized,
                Curvature = Mathf.Lerp(curv[i], curv[i + 1], f),
                Height = Mathf.Lerp(height[i], height[i + 1], f),
            };
        }

        public Vector2 Position(int i) => pos[i];

        // The nearest point of the road to p. Beyond an end, the lateral distance is measured to
        // that end point (so the corridor ends in a round cap).
        public Hit Nearest(Vector2 p)
        {
            var best = new Hit { S = 0, Lateral = float.MaxValue, Segment = 0 };
            float bestD2 = float.MaxValue;
            for (int i = 0; i < pos.Length - 1; i++)
            {
                Vector2 a = pos[i], d = pos[i + 1] - a;
                float len2 = d.sqrMagnitude;
                float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, d) / len2) : 0;
                Vector2 q = a + d * t;
                float d2 = (p - q).sqrMagnitude;
                if (d2 >= bestD2) continue;
                bestD2 = d2;
                // Right of the road: the tangent turned a quarter clockwise, (tz, -tx).
                float side = d.y * (p.x - q.x) - d.x * (p.y - q.y);
                best = new Hit { S = SAt(i) + (SAt(i + 1) - SAt(i)) * t, Lateral = Mathf.Sqrt(d2) * (side < 0 ? -1 : 1), Segment = i };
            }
            return best;
        }

        // Whether p is within `reach` of the samples' bounds (a quick reject before Nearest).
        public bool Near(Vector2 p, float reach) =>
            p.x >= min.x - reach && p.x <= max.x + reach && p.y >= min.y - reach && p.y <= max.y + reach;
    }
}
