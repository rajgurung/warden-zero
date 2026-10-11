using UnityEngine;

namespace WardenZero
{
    // The traversal course on the trail from the LZ to checkpoint A, as data: one static list
    // of blocks. The scene builder places a greybox (later a Tripo prop) for each piece, and
    // JungleStage registers the blocks with World. Heights come from JungleLayout.Height, the
    // same function as the terrain, so the EditMode course bot plays the real layout.
    //
    // Beats, in order along the trail:
    // 1. a fallen log across it (vault, 0.9 m: touch auto-vaults it);
    // 2. crate steps: a 1.1 m crate up to a 2.2 m stack (mantles), then a jump to a rock shelf;
    // 3. flat stepping blocks over the stream at the ford (jumps);
    // 4. a 2.3 m rock ledge (mantle);
    // 5. the gate, the one mandatory beat: a 4.4 m ruin wall across the trail with a vine
    //    face (wall climb), its top to walk, its far edge to hang from and shimmy along, then
    //    drop. Collapsed rubble and a rock outcrop flank it, too high to jump and with no edge
    //    to grab, so the trail's only way on is over the wall;
    // 6. a 3.2 m terrace short of checkpoint A (jump, grab and climb up).
    // Every beat but the gate can be walked round.
    public static class CourseLayout
    {
        public struct Piece
        {
            public string Name; // the greybox object, by beat
            public string Prop; // the Tripo prop fitted to the block (one of Props)
            // The prop's turn in the block's frame: the props' long axis is their own z, so a
            // block long along its x turns it -90 (which also puts the prop's -x face, the
            // wall's vines, on the block's -z face).
            public float PropYaw;
            public Block Block;
        }

        // The props the art pass makes for the course (Assets/Art/Campaign/Traversal/<name>.fbx).
        public static readonly string[] Props = { "fallen_log", "supply_crate", "supply_crate_b", "ruin_wall", "rock_ledge", "gate_rubble", "gate_rubble_tall", "rock_outcrop" };

        public const float GateYaw = 41;
        public static readonly Vector2 Gate = new Vector2(12, 20);
        public const float WallTop = 4.4f; // above the highest ground under it: drops stay under SafeDrop

        // The course start (?campaign=course) faces up the trail toward the log.
        public static readonly Vector3 StartFacing = new Vector3(5, 0, 12).normalized;
        public const float FordTop = 0.15f; // the stepping blocks, 0.75 m above the water

        public static readonly Piece[] Pieces = Build();

        static Block[] blocks;
        public static Block[] Blocks
        {
            get
            {
                if (blocks == null) blocks = System.Array.ConvertAll(Pieces, p => p.Block);
                return blocks;
            }
        }

        static Piece[] Build()
        {
            var list = new System.Collections.Generic.List<Piece>();
            const BlockFlags vault = BlockFlags.Vault | BlockFlags.Ledge;
            const BlockFlags ledge = BlockFlags.Ledge;
            const int all = Block.AllEdges;

            // 1. The fallen log, across the trail.
            var log = new Vector2(-9.83f, -20);
            list.Add(Rel("1_Log", "fallen_log", At(log, 22.6f, 0, 0), 22.6f, 2.7f, 0.6f, 0.9f, vault, 1 << 0 | 1 << 2));

            // 2. Crate steps (the long hard case, then two crates stacked) and the rock shelf.
            var crates = new Vector2(-7.5f, -13);
            const float cy = 16.7f;
            list.Add(Rel("2_Crate", "supply_crate_b", At(crates, cy, 0, 0), cy, 1.05f, 0.73f, 1.1f, vault, all));
            list.Add(Rel("2_CrateStack", "supply_crate", At(crates, cy, 0, 1.43f), cy, 0.68f, 0.7f, 2.2f, ledge, all));
            list.Add(Rel("2_Shelf", "rock_ledge", At(crates, cy, 0.2f, 5.4f), cy + 8, 1.4f, 1.4f, 1.8f, ledge, all));

            // 3. Stepping blocks over the stream where the trail crosses it, beside the old stones.
            var ford = At(new Vector2(-3.08f, -0.21f), 20.6f, 1.4f, 1);
            const float fy = 20.6f;
            for (int k = 0; k < 4; k++)
                list.Add(Abs($"3_Ford{k}", "rock_ledge", At(ford, fy, 0, (k - 1.5f) * 3.4f), fy + (k % 2 * 2 - 1) * 8, 0.8f, 0.8f, FordTop, ledge, all));

            // 4. The rock ledge.
            list.Add(Rel("4_Ledge", "rock_ledge", new Vector2(2.7f, 9.8f), 31, 1.6f, 1.2f, 2.3f, ledge, all));

            // 5. The gate: the vine section on the trail (its far top edge is the shimmy ledge)
            // and a plain section turned so its vines face away; tall rubble to the west with a
            // lower heap at its end, and an outcrop to the east, out across the stream.
            var g = Gate;
            float gy = GateYaw;
            // One top for the whole wall: WallTop above the highest ground under any of it.
            var whole = Shape(At(g, gy, 2.5f, 0), gy, 5, 0.75f, BlockFlags.None, 0);
            Ground(whole, out _, out float wallGround);
            var vine = Abs("5_WallVine", "ruin_wall", At(g, gy, 0, 0), gy, 2.5f, 0.75f, wallGround + WallTop, ledge, 1 << 0);
            vine.Block.Flags |= BlockFlags.Climbable;
            vine.Block.Climb = 1 << 2;
            list.Add(vine);
            var plain = Abs("5_WallPlain", "ruin_wall", At(g, gy, 5, 0), gy, 2.5f, 0.75f, wallGround + WallTop, BlockFlags.Climbable, 0);
            plain.Block.Climb = 1 << 0; // its vines face the far side: a way back over
            plain.PropYaw = 90;
            list.Add(plain);
            list.Add(Rel("5_Rubble1", "gate_rubble_tall", At(g, gy, -5.3f, 0.3f), gy + 6, 2.9f, 1.65f, 3.4f, BlockFlags.None, 0));
            list.Add(Rel("5_Rubble2", "gate_rubble_tall", At(g, gy, -10.6f, 0.6f), gy - 6, 2.9f, 1.75f, 3.3f, BlockFlags.None, 0));
            list.Add(Rel("5_Rubble3", "gate_rubble", At(g, gy, -15.9f, 0.3f), gy + 8, 2.9f, 2.0f, 2.3f, BlockFlags.None, 0));
            list.Add(Rel("5_Outcrop1", "rock_outcrop", At(g, gy, 10.4f, 0.5f), gy - 10, 3.0f, 2.8f, 4.2f, BlockFlags.None, 0));
            list.Add(Rel("5_Outcrop2", "rock_outcrop", At(g, gy, 15.8f, 1.0f), gy + 12, 2.9f, 2.8f, 5.0f, BlockFlags.None, 0));
            list.Add(Rel("5_Outcrop3", "rock_outcrop", At(g, gy, 21.0f, 0.6f), gy - 5, 2.9f, 2.8f, 4.4f, BlockFlags.None, 0));

            // 6. The terrace short of checkpoint A.
            list.Add(Rel("6_Terrace", "rock_ledge", new Vector2(16.2f, 25), 39.8f, 2.5f, 1.2f, 3.2f, ledge, all));
            return list.ToArray();
        }

        // A point `right` and `forward` metres from o in a frame turned by yaw degrees.
        public static Vector2 At(Vector2 o, float yaw, float right, float forward)
        {
            float a = yaw * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(o.x + right * c + forward * s, o.y - right * s + forward * c);
        }

        // A block whose top is `height` above the highest ground under it, sunk below the lowest.
        static Piece Rel(string name, string prop, Vector2 c, float yaw, float hx, float hz, float height, BlockFlags flags, int ledges)
        {
            var b = Shape(c, yaw, hx, hz, flags, ledges);
            Ground(b, out float lo, out float hi);
            b.Bottom = lo - 0.5f;
            b.Top = hi + height;
            return new Piece { Name = name, Prop = prop, Block = b, PropYaw = LongYaw(b) };
        }

        // A block with its top at a fixed height (the ford's, just above the water).
        static Piece Abs(string name, string prop, Vector2 c, float yaw, float hx, float hz, float top, BlockFlags flags, int ledges)
        {
            var b = Shape(c, yaw, hx, hz, flags, ledges);
            Ground(b, out float lo, out _);
            b.Bottom = lo - 0.5f;
            b.Top = top;
            return new Piece { Name = name, Prop = prop, Block = b, PropYaw = LongYaw(b) };
        }

        static float LongYaw(Block b) => b.Half.x >= b.Half.y ? -90 : 0;

        static Block Shape(Vector2 c, float yaw, float hx, float hz, BlockFlags flags, int ledges)
        {
            return new Block { Center = c, Half = new Vector2(hx, hz), Yaw = yaw, Flags = flags, Ledges = ledges };
        }

        // Lowest and highest ground over the footprint (corners, edge midpoints, centre).
        static void Ground(Block b, out float lo, out float hi)
        {
            lo = float.MaxValue;
            hi = float.MinValue;
            for (int i = -1; i <= 1; i++)
                for (int k = -1; k <= 1; k++)
                {
                    var p = b.ToWorld(new Vector3(i * b.Half.x, 0, k * b.Half.y));
                    float h = JungleLayout.Height(p.x, p.z);
                    lo = Mathf.Min(lo, h);
                    hi = Mathf.Max(hi, h);
                }
        }

        // Whether (x, z) is within `margin` of any piece's footprint (trees, rocks and ground
        // cover keep off).
        public static bool Covers(float x, float z, float margin)
        {
            var p = new Vector3(x, 0, z);
            foreach (var piece in Pieces)
                if (piece.Block.Contains(p, -margin)) return true;
            return false;
        }
    }
}
