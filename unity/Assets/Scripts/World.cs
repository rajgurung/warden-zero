using UnityEngine;

namespace WardenZero
{
    [System.Flags]
    public enum BlockFlags { None = 0, Vault = 1, Ledge = 2, Climbable = 4, Vehicle = 8 }

    // A solid authored box the Warden can stand on, vault, climb or hang from (a log, a crate,
    // a ruin wall, a rock shelf): a footprint turned by Yaw about its centre, from Bottom to Top.
    // Faces and their top edges are numbered in its own frame: 0 +z, 1 +x, 2 -z, 3 -x.
    // Ledges marks the top edges he can grab or climb over, Climb the faces with vines.
    [System.Serializable]
    public struct Block
    {
        public Vector2 Center; // x, z
        public Vector2 Half; // half extents along its own x and z
        public float Yaw; // degrees
        public float Bottom, Top;
        public BlockFlags Flags;
        public int Ledges; // bit per top edge
        public int Climb; // bit per face

        public const int AllEdges = 0xF;

        // A point in the block's frame (x, z turned; y unchanged).
        public Vector3 ToLocal(Vector3 p)
        {
            float a = Yaw * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            float dx = p.x - Center.x, dz = p.z - Center.y;
            return new Vector3(dx * c - dz * s, p.y, dx * s + dz * c);
        }

        public Vector3 ToWorld(Vector3 local)
        {
            float a = Yaw * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector3(Center.x + local.x * c + local.z * s, local.y, Center.y - local.x * s + local.z * c);
        }

        // A direction from the block's frame to the world's.
        public Vector3 Turn(Vector3 local) => Quaternion.Euler(0, Yaw, 0) * local;

        static readonly Vector3[] FaceNormals = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        public Vector3 Normal(int face) => Turn(FaceNormals[face]);

        // The top edge above a face, at Top, from one end to the other.
        public void Edge(int face, out Vector3 a, out Vector3 b)
        {
            float x = Half.x, z = Half.y;
            switch (face)
            {
                case 0: a = new Vector3(x, Top, z); b = new Vector3(-x, Top, z); break;
                case 1: a = new Vector3(x, Top, -z); b = new Vector3(x, Top, z); break;
                case 2: a = new Vector3(-x, Top, -z); b = new Vector3(x, Top, -z); break;
                default: a = new Vector3(-x, Top, z); b = new Vector3(-x, Top, -z); break;
            }
            a = ToWorld(a);
            b = ToWorld(b);
        }

        // How far the block reaches back from a face (along its inward normal).
        public float Depth(int face) => face % 2 == 0 ? Half.y * 2 : Half.x * 2;

        // Whether a point is over the footprint, shrunk by `inset` (negative grows it).
        public bool Contains(Vector3 p, float inset = 0)
        {
            var l = ToLocal(p);
            return Mathf.Abs(l.x) <= Half.x - inset && Mathf.Abs(l.z) <= Half.y - inset;
        }

        public bool HasLedge(int face) => (Flags & BlockFlags.Ledge) != 0 && (Ledges & (1 << face)) != 0;
        public bool HasClimb(int face) => (Flags & BlockFlags.Climbable) != 0 && (Climb & (1 << face)) != 0;
    }

    // The playable area of the current scene: bounds, solid walls, and tree trunks that
    // only block the Warden (as in v1's jungle), and in the jungle the traversal blocks.
    // Defaults to the arena.
    public static class World
    {
        public static float HalfW { get; private set; } = GameConfig.HalfW;
        public static float HalfD { get; private set; } = GameConfig.HalfD;
        public static WallRect[] Walls { get; private set; } = GameConfig.Walls;
        // x, z = trunk centre; y = trunk radius.
        public static Vector3[] Trunks { get; private set; } = new Vector3[0];
        // The jungle's bounds (null: the centred HalfW x HalfD rectangle). Bounds moves a circle
        // of radius r back to where it may be (the walkable patch, or near the road); OutOfBounds
        // says a point has left the world altogether (bolts end there).
        public static System.Func<Vector3, float, Vector3> Bounds;
        public static System.Func<Vector3, bool> OutOfBounds;
        // The jeep's slot in Blocks (-1: none). It moves, so its box is rewritten each frame.
        public static int VehicleSlot = -1;
        // Boulders the camera and aimed bolts can't pass: x, y, z = centre; w = radius.
        public static Vector4[] Rocks = new Vector4[0];
        // Solid boxes he can climb (the jungle's traversal course); the camera, the crosshair
        // and aimed bolts stop at them too.
        public static Block[] Blocks = new Block[0];
        // Jump, vault and climb (the jungle); elsewhere Space dashes and the ground is all.
        public static bool TraversalEnabled;
        // Ground height at a point (the jungle's terrain); flat at 0 when null.
        public static System.Func<Vector3, float> Ground
        {
            get => ground;
            set
            {
                ground = value;
                trunkBase = null;
            }
        }
        static System.Func<Vector3, float> ground;
        static float[] trunkBase;

        public static float HeightAt(Vector3 p) => ground != null ? ground(p) : 0;

        // The ground height at each trunk's foot, sampled once per world and ground (casts
        // test hundreds of trunks a frame).
        public static float TrunkBase(int i)
        {
            if (trunkBase == null || trunkBase.Length != Trunks.Length)
            {
                trunkBase = new float[Trunks.Length];
                for (int k = 0; k < Trunks.Length; k++) trunkBase[k] = HeightAt(Trunks[k]);
            }
            return trunkBase[i];
        }

        public static void UseArena()
        {
            Use(GameConfig.HalfW, GameConfig.HalfD, GameConfig.Walls, new Vector3[0]);
        }

        // Flat ground; the jungle sets Ground after this.
        public static void Use(float halfW, float halfD, WallRect[] walls, Vector3[] trunks)
        {
            Rocks = new Vector4[0];
            Blocks = new Block[0];
            TraversalEnabled = false;
            Bounds = null;
            OutOfBounds = null;
            VehicleSlot = -1;
            grid = null;
            HalfW = halfW;
            HalfD = halfD;
            Walls = walls;
            Trunks = trunks;
            Ground = null; // also drops the trunk heights
        }

        // The ground under his feet: the terrain, or the top of a block under p that is no
        // higher than a step above feetY (anything taller is a wall to him).
        public static float SurfaceAt(Vector3 p, float feetY)
        {
            float best = HeightAt(p);
            foreach (var b in Blocks)
                if (b.Top > best && b.Top <= feetY + GameConfig.StepUp && b.Contains(p)) best = b.Top;
            return best;
        }

        // Push a circle out of every block too tall to step onto from feetY (and not wholly
        // above or below him): circle against the turned box, like ResolveCircle's walls.
        // Blocks with any of the `skip` flags are left out (the jeep, for itself).
        public static Vector3 PushOutOfBlocks(Vector3 pos, float r, float feetY, BlockFlags skip = BlockFlags.None)
        {
            foreach (var b in Blocks)
            {
                if ((b.Flags & skip) != 0) continue;
                if (b.Top <= feetY + GameConfig.StepUp || feetY + GameConfig.BodyHeight <= b.Bottom) continue;
                var l = b.ToLocal(pos);
                float cx = Mathf.Clamp(l.x, -b.Half.x, b.Half.x), cz = Mathf.Clamp(l.z, -b.Half.y, b.Half.y);
                float dx = l.x - cx, dz = l.z - cz;
                float d2 = dx * dx + dz * dz;
                if (d2 >= r * r) continue;
                if (d2 > 1e-6f)
                {
                    float d = Mathf.Sqrt(d2);
                    l.x = cx + dx / d * r;
                    l.z = cz + dz / d * r;
                }
                else
                {
                    // Centre inside: out through the nearest side.
                    float right = b.Half.x - l.x, left = l.x + b.Half.x, front = b.Half.y - l.z, back = l.z + b.Half.y;
                    float min = Mathf.Min(Mathf.Min(left, right), Mathf.Min(front, back));
                    if (min == right) l.x = b.Half.x + r;
                    else if (min == left) l.x = -b.Half.x - r;
                    else if (min == front) l.z = b.Half.y + r;
                    else l.z = -b.Half.y - r;
                }
                var w = b.ToWorld(l);
                pos.x = w.x;
                pos.z = w.z;
            }
            return pos;
        }

        // Push a circle out of any trunk it overlaps (the Warden and the jeep).
        public static Vector3 PushOutOfTrunks(Vector3 pos, float r)
        {
            foreach (int i in TrunksNear(pos.x - r, pos.z - r, pos.x + r, pos.z + r))
            {
                var t = Trunks[i];
                float dx = pos.x - t.x, dz = pos.z - t.z;
                float min = r + t.y;
                float d2 = dx * dx + dz * dz;
                if (d2 >= min * min) continue;
                float d = Mathf.Sqrt(d2);
                if (d < 1e-4f) { dx = 1; dz = 0; d = 1; }
                pos.x = t.x + dx / d * min;
                pos.z = t.z + dz / d * min;
            }
            return pos;
        }

        // ------------------------------------------------------------------ trunk grid

        // The trunks sorted into TrunkCell-metre cells, so casts, pushes and bolts only look at
        // the few near them (the road's corridor triples the jungle's trunks). Built on first
        // use for the current Trunks.
        public const float TrunkCell = 16;

        internal class Grid
        {
            public Vector3[] For;
            public float X0, Z0, Reach; // the first cell's corner; the widest trunk's radius
            public int W, H;
            public int[] Start; // per cell, into Order; one extra at the end
            public int[] Order; // trunk indices, cell by cell
        }

        static Grid grid;

        static Grid TrunkGrid()
        {
            if (grid != null && grid.For == Trunks) return grid;
            var g = new Grid { For = Trunks };
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            foreach (var t in Trunks)
            {
                x0 = Mathf.Min(x0, t.x);
                z0 = Mathf.Min(z0, t.z);
                x1 = Mathf.Max(x1, t.x);
                z1 = Mathf.Max(z1, t.z);
                g.Reach = Mathf.Max(g.Reach, t.y);
            }
            if (Trunks.Length == 0) x0 = z0 = x1 = z1 = 0;
            g.X0 = x0;
            g.Z0 = z0;
            g.W = Mathf.FloorToInt((x1 - x0) / TrunkCell) + 1;
            g.H = Mathf.FloorToInt((z1 - z0) / TrunkCell) + 1;
            g.Start = new int[g.W * g.H + 1];
            var cell = new int[Trunks.Length];
            for (int i = 0; i < Trunks.Length; i++)
            {
                cell[i] = Mathf.FloorToInt((Trunks[i].z - z0) / TrunkCell) * g.W + Mathf.FloorToInt((Trunks[i].x - x0) / TrunkCell);
                g.Start[cell[i] + 1]++;
            }
            for (int c = 0; c < g.W * g.H; c++) g.Start[c + 1] += g.Start[c];
            g.Order = new int[Trunks.Length];
            var fill = (int[])g.Start.Clone();
            for (int i = 0; i < Trunks.Length; i++) g.Order[fill[cell[i]]++] = i;
            return grid = g;
        }

        // The trunks that could touch the box from (minX, minZ) to (maxX, maxZ): those in the grid
        // cells it covers, grown by the widest trunk. Each comes once. foreach over the result
        // allocates nothing.
        public static TrunkQuery TrunksNear(float minX, float minZ, float maxX, float maxZ)
        {
            var g = TrunkGrid();
            int cx0 = Mathf.Max(0, Mathf.FloorToInt((minX - g.Reach - g.X0) / TrunkCell));
            int cz0 = Mathf.Max(0, Mathf.FloorToInt((minZ - g.Reach - g.Z0) / TrunkCell));
            int cx1 = Mathf.Min(g.W - 1, Mathf.FloorToInt((maxX + g.Reach - g.X0) / TrunkCell));
            int cz1 = Mathf.Min(g.H - 1, Mathf.FloorToInt((maxZ + g.Reach - g.Z0) / TrunkCell));
            return new TrunkQuery(g, cx0, cz0, cx1, cz1);
        }

        public struct TrunkQuery
        {
            readonly Grid g;
            readonly int cx0, cx1, cz1;
            int cx, cz, k, end;

            internal TrunkQuery(Grid g, int cx0, int cz0, int cx1, int cz1)
            {
                this.g = g;
                this.cx0 = cx0;
                this.cx1 = cx1;
                this.cz1 = cz1;
                cx = cx0 - 1;
                cz = cz0;
                k = end = 0;
            }

            public TrunkQuery GetEnumerator() => this;

            public int Current => g.Order[k];

            public bool MoveNext()
            {
                k++;
                while (k >= end)
                {
                    if (++cx > cx1)
                    {
                        cx = cx0;
                        cz++;
                    }
                    if (cz > cz1 || cx0 > cx1) return false;
                    int c = cz * g.W + cx;
                    k = g.Start[c];
                    end = g.Start[c + 1];
                }
                return true;
            }
        }
    }
}
