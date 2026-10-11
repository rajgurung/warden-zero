using UnityEngine;

namespace WardenZero
{
    // Ray and sphere casts against the solid parts of the current World, without a physics
    // engine (the game has no colliders): the ground (World.HeightAt), tree trunks (upright
    // cylinders), rocks (spheres), the arena's walls (boxes WallHeight tall) and, if asked,
    // enemies (upright cylinders). The behind view uses it to keep the camera out of things
    // and to find what the crosshair is on; aimed bolts use InScenery.
    public static class WorldCast
    {
        public const float TrunkHeight = 30; // up into the crowns, above any camera

        // How far along `dir` (normalised) a sphere of `radius` moves from `origin` before it
        // touches something. Things the sphere already overlaps at the start are ignored.
        public static bool Cast(Vector3 origin, Vector3 dir, float maxDistance, float radius, bool enemies, out float distance)
        {
            float best = maxDistance;
            void Take(float t)
            {
                if (t >= 0 && t < best) best = t;
            }

            Take(Ground(origin, dir, maxDistance, radius));
            var trunks = World.Trunks;
            for (int i = 0; i < trunks.Length; i++)
            {
                var t = trunks[i];
                // Out of reach sideways: no height lookup at all.
                if (!NearPath(origin, dir, best, t, t.y + radius)) continue;
                float g = World.TrunkBase(i);
                Take(Cylinder(origin, dir, t, t.y + radius, g - 1, g + TrunkHeight));
            }
            foreach (var r in World.Rocks)
                Take(Sphere(origin, dir, r, r.w + radius));
            foreach (var w in World.Walls)
                Take(Box(origin, dir, new Vector3(w.MinX - radius, -radius, w.MinZ - radius), new Vector3(w.MaxX + radius, GameConfig.WallHeight + radius, w.MaxZ + radius)));
            if (enemies)
                foreach (var e in Enemy.All)
                {
                    if (e.IsDying) continue;
                    Vector3 p = e.transform.position;
                    Take(Cylinder(origin, dir, p, e.Radius + radius, p.y - 0.3f, p.y + e.HitHeight));
                }
            distance = best;
            return best < maxDistance;
        }

        // A point inside the ground, a trunk or a rock (where an aimed bolt stops).
        public static bool InScenery(Vector3 p)
        {
            if (p.y < World.HeightAt(p)) return true;
            var trunks = World.Trunks;
            for (int i = 0; i < trunks.Length; i++)
            {
                var t = trunks[i];
                float dx = p.x - t.x, dz = p.z - t.z;
                if (dx * dx + dz * dz < t.y * t.y && p.y < World.TrunkBase(i) + TrunkHeight) return true;
            }
            foreach (var r in World.Rocks)
                if ((p - (Vector3)r).sqrMagnitude < r.w * r.w) return true;
            return false;
        }

        // March along the ray until the sphere's bottom dips below the ground, then bisect
        // that last step. Each step is as long as the height above the ground allows, assuming
        // slopes of at most 45 degrees (the jungle's are gentler): long strides high over the
        // ground, short ones near it.
        static float Ground(Vector3 o, Vector3 d, float max, float r)
        {
            float Clearance(float t)
            {
                Vector3 p = o + d * t;
                return p.y - r - World.HeightAt(p);
            }
            if (World.Ground == null)
            {
                // Flat floor at 0: straight to the answer.
                if (o.y - r < 0) return -1;
                if (d.y >= 0) return -1;
                float hit = (o.y - r) / -d.y;
                return hit <= max ? hit : -1;
            }
            float c = Clearance(0);
            if (c < 0) return -1;
            float prev = 0;
            while (prev < max)
            {
                float step = Mathf.Clamp(c / (Mathf.Abs(d.y) + 1), 0.15f, 4);
                float t = Mathf.Min(prev + step, max);
                c = Clearance(t);
                if (c < 0)
                {
                    float lo = prev, hi = t;
                    for (int i = 0; i < 8; i++)
                    {
                        float mid = (lo + hi) / 2;
                        if (Clearance(mid) < 0) hi = mid;
                        else lo = mid;
                    }
                    return lo;
                }
                prev = t;
            }
            return -1;
        }

        // Whether the ray's first `length` metres pass within `reach` of c's vertical axis,
        // seen from above (a cheap reject before the full cylinder test).
        static bool NearPath(Vector3 o, Vector3 d, float length, Vector3 c, float reach)
        {
            float dx = d.x, dz = d.z;
            float ox = c.x - o.x, oz = c.z - o.z;
            float flat = dx * dx + dz * dz;
            float t = flat > 1e-8f ? Mathf.Clamp((ox * dx + oz * dz) / flat, 0, length) : 0;
            float ex = ox - dx * t, ez = oz - dz * t;
            return ex * ex + ez * ez <= reach * reach;
        }

        // An upright cylinder around c's vertical axis, from yMin to yMax, with its top cap.
        static float Cylinder(Vector3 o, Vector3 d, Vector3 c, float radius, float yMin, float yMax)
        {
            float ox = o.x - c.x, oz = o.z - c.z;
            float c0 = ox * ox + oz * oz - radius * radius;
            if (c0 <= 0 && o.y >= yMin && o.y <= yMax) return -1; // starts inside
            float best = -1;
            float a = d.x * d.x + d.z * d.z;
            float b = ox * d.x + oz * d.z;
            float disc = b * b - a * c0;
            if (a > 1e-8f && c0 > 0 && disc >= 0)
            {
                float t = (-b - Mathf.Sqrt(disc)) / a;
                float y = o.y + d.y * t;
                if (t >= 0 && y >= yMin && y <= yMax) best = t;
            }
            if (d.y < -1e-6f && o.y > yMax)
            {
                float t = (yMax - o.y) / d.y;
                float x = ox + d.x * t, z = oz + d.z * t;
                if (x * x + z * z <= radius * radius && (best < 0 || t < best)) best = t;
            }
            return best;
        }

        static float Sphere(Vector3 o, Vector3 d, Vector3 c, float radius)
        {
            Vector3 m = o - c;
            float b = Vector3.Dot(m, d);
            float c0 = m.sqrMagnitude - radius * radius;
            if (c0 <= 0 || b > 0) return -1;
            float disc = b * b - c0;
            return disc < 0 ? -1 : -b - Mathf.Sqrt(disc);
        }

        // Slab test; -1 when missed or when the ray starts inside.
        static float Box(Vector3 o, Vector3 d, Vector3 min, Vector3 max)
        {
            float t0 = 0, t1 = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Abs(d[i]) < 1e-8f)
                {
                    if (o[i] < min[i] || o[i] > max[i]) return -1;
                    continue;
                }
                float a = (min[i] - o[i]) / d[i], b = (max[i] - o[i]) / d[i];
                if (a > b) (a, b) = (b, a);
                t0 = Mathf.Max(t0, a);
                t1 = Mathf.Min(t1, b);
                if (t0 > t1) return -1;
            }
            return t0 > 0 ? t0 : -1;
        }
    }
}
