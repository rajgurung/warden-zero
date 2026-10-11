using UnityEngine;

namespace WardenZero
{
    // What Space would do from where he stands: finds the best block edge or face in front of
    // him (within ProbeReach of his centre and ProbeAngle of his move or facing, nearest and
    // straightest first) and says which move fits its height. Plain C# over World.Blocks.
    public static class TraversalProbe
    {
        public enum Kind { None, Vault, Mantle, JumpGrab, Climb }

        public struct Hit
        {
            public Kind Kind;
            public int Block;
            public int Face;
            public Vector3 Point; // on the top edge (y = Top), nearest his centre
            public Vector3 Normal; // the face's outward normal (flat)
            public float Distance; // his centre to the face
            public float Top;
            public Vector3 Landing; // vault: feet beyond it; mantle: feet on top

            public bool Found => Kind != Kind.None;

            // The context label for Space / JUMP.
            public string Label
            {
                get
                {
                    switch (Kind)
                    {
                        case Kind.Vault: return "VAULT";
                        case Kind.Mantle:
                        case Kind.JumpGrab:
                        case Kind.Climb: return "CLIMB";
                        default: return "JUMP";
                    }
                }
            }
        }

        // Keeps hands and feet off the very ends of an edge.
        public const float EdgeInset = 0.35f;

        // The best move from `feet` toward `dir` (flat; zero finds nothing).
        public static Hit Find(Vector3 feet, Vector3 dir)
        {
            var best = new Hit();
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-4f) return best;
            dir.Normalize();
            float bestScore = float.MaxValue;
            var blocks = World.Blocks;
            float cos = Mathf.Cos(GameConfig.ProbeAngle * Mathf.Deg2Rad);
            for (int i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i];
                float h = b.Top - feet.y;
                if (h <= GameConfig.StepUp || b.Bottom > feet.y + 1) continue;
                for (int f = 0; f < 4; f++)
                {
                    bool ledge = b.HasLedge(f), climb = b.HasClimb(f);
                    if (!ledge && !climb) continue;
                    Vector3 n = b.Normal(f);
                    if (Vector3.Dot(-n, dir) < cos) continue;
                    Vector3 p = NearestOnEdge(b, f, feet);
                    Vector3 off = feet - p;
                    off.y = 0;
                    float d = Vector3.Dot(off, n);
                    if (d < -0.05f || off.magnitude > GameConfig.ProbeReach) continue;
                    var hit = Classify(b, i, f, p, n, feet, ledge, climb);
                    if (!hit.Found) continue;
                    hit.Distance = d;
                    float score = off.magnitude + (1 - Vector3.Dot(-n, dir));
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = hit;
                    }
                }
            }
            return best;
        }

        // Standing on a block top: the ledge he can lower himself over with C (within reach of
        // an edge he faces or moves toward; the nearest when he is still).
        public static Hit FindLower(Vector3 feet, Vector3 dir)
        {
            var best = new Hit();
            dir.y = 0;
            bool still = dir.sqrMagnitude < 1e-4f;
            if (!still) dir.Normalize();
            float bestD = float.MaxValue;
            var blocks = World.Blocks;
            for (int i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i];
                if (Mathf.Abs(b.Top - feet.y) > 0.05f || !b.Contains(feet)) continue;
                for (int f = 0; f < 4; f++)
                {
                    if (!b.HasLedge(f)) continue;
                    Vector3 n = b.Normal(f);
                    if (!still && Vector3.Dot(dir, n) < 0.5f) continue;
                    Vector3 p = NearestOnEdge(b, f, feet);
                    float d = Vector3.Dot(new Vector3(p.x - feet.x, 0, p.z - feet.z), n);
                    if (d > GameConfig.ProbeReach - 0.3f || d >= bestD || !HangClear(b, p, n)) continue;
                    bestD = d;
                    best = new Hit { Kind = Kind.Mantle, Block = i, Face = f, Point = p, Normal = n, Top = b.Top, Distance = d };
                }
            }
            return best;
        }

        // Room to hang from this edge: his feet (HangDepth under it) clear of the ground or a
        // block below by 0.1 m. Low tops (crates, the log, a ledge) have none.
        public static bool HangClear(Block b, Vector3 edge, Vector3 n)
        {
            Vector3 feet = edge + n * GameConfig.WallGap;
            float y = b.Top - GameConfig.HangDepth;
            return World.SurfaceAt(feet, b.Top) <= y - 0.1f;
        }

        // Any edge or vine face he could use within `range` of his feet (the camera frames for
        // platforming near one).
        public static bool Near(Vector3 feet, float range)
        {
            foreach (var b in World.Blocks)
            {
                if (b.Top <= feet.y + GameConfig.StepUp || b.Bottom > feet.y + 1) continue;
                if ((b.Flags & (BlockFlags.Ledge | BlockFlags.Climbable)) == 0 || b.Ledges == 0 && b.Climb == 0) continue;
                var l = b.ToLocal(feet);
                float dx = Mathf.Max(0, Mathf.Abs(l.x) - b.Half.x), dz = Mathf.Max(0, Mathf.Abs(l.z) - b.Half.y);
                if (dx * dx + dz * dz <= range * range) return true;
            }
            return false;
        }

        // Which move suits this edge from this height, if any.
        static Hit Classify(Block b, int index, int face, Vector3 edge, Vector3 n, Vector3 feet, bool ledge, bool climb)
        {
            var hit = new Hit { Block = index, Face = face, Point = edge, Normal = n, Top = b.Top };
            float h = b.Top - feet.y;
            float depth = b.Depth(face);
            if (climb)
            {
                hit.Kind = Kind.Climb;
                hit.Landing = OnTop(b, edge, n, depth);
                return hit;
            }
            if (!ledge) return hit;
            if (h >= GameConfig.VaultMin && h <= GameConfig.VaultMax && (b.Flags & BlockFlags.Vault) != 0 && depth <= GameConfig.VaultMaxDepth
                && VaultLanding(b, edge, n, depth, feet.y, out var land))
            {
                hit.Kind = Kind.Vault;
                hit.Landing = land;
                return hit;
            }
            if (h <= GameConfig.MantleMax)
            {
                var top = OnTop(b, edge, n, depth);
                if (!Headroom(top, index)) return hit;
                hit.Kind = Kind.Mantle;
                hit.Landing = top;
                return hit;
            }
            if (h <= GameConfig.JumpGrabMax) hit.Kind = Kind.JumpGrab;
            return hit;
        }

        // Where a mantle or climb ends: on the top, a little in from the edge.
        public static Vector3 OnTop(Block b, Vector3 edge, Vector3 n, float depth)
        {
            Vector3 p = edge - n * Mathf.Min(0.7f, depth / 2);
            p.y = b.Top;
            return p;
        }

        // Beyond the block, clear of everything, on ground no higher than its top and no more
        // than a metre below where he started (a long drop is a climb onto it instead).
        static bool VaultLanding(Block b, Vector3 edge, Vector3 n, float depth, float feetY, out Vector3 land)
        {
            land = edge - n * (depth + GameConfig.PlayerRadius + 0.35f);
            float s = World.SurfaceAt(land, b.Top);
            land.y = s;
            if (s < feetY - 1 || s > b.Top - 0.3f) return false;
            Vector3 pushed = World.PushOutOfBlocks(land, GameConfig.PlayerRadius, s);
            return (pushed - land).sqrMagnitude < 1e-6f;
        }

        // Nothing above the top where he would stand up.
        public static bool Headroom(Vector3 top, int self)
        {
            var blocks = World.Blocks;
            for (int i = 0; i < blocks.Length; i++)
            {
                if (i == self) continue;
                var o = blocks[i];
                if (o.Top > top.y + 0.01f && o.Bottom < top.y + GameConfig.BodyHeight && o.Contains(top, -0.3f)) return false;
            }
            return true;
        }

        // The point on a face's top edge nearest `p`, kept EdgeInset from its ends.
        public static Vector3 NearestOnEdge(Block b, int face, Vector3 p)
        {
            b.Edge(face, out var a, out var e);
            return OnEdge(a, e, Along(a, e, p));
        }

        // Distance along a→e of p's projection, clamped inside the edge.
        public static float Along(Vector3 a, Vector3 e, Vector3 p)
        {
            Vector3 d = e - a;
            d.y = 0;
            float len = d.magnitude;
            float s = Vector3.Dot(new Vector3(p.x - a.x, 0, p.z - a.z), d / Mathf.Max(len, 1e-4f));
            return ClampAlong(s, len);
        }

        public static float ClampAlong(float s, float len) => len <= EdgeInset * 2 ? len / 2 : Mathf.Clamp(s, EdgeInset, len - EdgeInset);

        public static Vector3 OnEdge(Vector3 a, Vector3 e, float s)
        {
            Vector3 d = e - a;
            float len = new Vector2(d.x, d.z).magnitude;
            return a + d * (s / Mathf.Max(len, 1e-4f));
        }
    }
}
