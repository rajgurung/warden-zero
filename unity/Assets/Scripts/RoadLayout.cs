using UnityEngine;

namespace WardenZero
{
    // The road past checkpoint A as data (M3): the drive from A's motor pool up out of the
    // valley to checkpoint B, and the gun ride on from B to the roadblock, both on a 384 m
    // terrain tile north of the patch (JungleRoad). The scene builder, the jeep, the bounds
    // and the EditMode tests all read it, like CourseLayout.
    //
    // Heights: each road has a profile (its surface). Inside the patch the drive is carved
    // into the valley floor (Carve), never near the course, the LZ or checkpoint A, and not
    // on the flat rim band where the patch meets the tile. On the tile the road runs level
    // across, with shoulders, then banks rising between BankFrom and BankTo, then valley
    // walls up to the hills (TileHeight). The same valley lowers the far landscape around the
    // tile (JungleLayout.Landscape), and the tile blends into the landscape's own 16 m grid
    // over EdgeBlend metres at its edges, so the three terrains meet without a seam.
    public static class RoadLayout
    {
        // The tile, in world x and z (multiples of the landscape's 16 m cells).
        public const float TileMinX = -48, TileMaxX = 336, TileMinZ = 80, TileMaxZ = 464;
        public const float PatchHalf = 80; // the patch terrain's half size
        public const float LandCell = 16; // the landscape's heightmap spacing

        // Across the road, metres from the centreline.
        public const float RoadHalf = 3.5f; // level surface (mud)
        public const float ShoulderHalf = 6.5f; // still level (grass)
        public const float BankFrom = 8, BankTo = 16; // banks rise between these
        public const float CorridorHalf = 18; // the jeep's hidden clamp
        public const float FootCorridorHalf = 20; // his
        public const float WallSlope = 0.45f; // the valley walls beyond the banks (24 degrees)
        public const float FillSlope = 1; // embankments where the ground is lower than the road
        public const float EdgeBlend = 32;
        public const float CarveFull = 4.5f, CarveOuter = 10; // the drive's carve in the patch
        public const float SeamZ = 80, SeamBand = 8; // the rim band: no carve within it

        // Motor pool by checkpoint A, up the valley's north side, over the rim (D2), across the
        // tile's lowlands, a hairpin, and up the valley to checkpoint B.
        public static readonly Vector2[] DrivePoints =
        {
            new Vector2(-8, 50), new Vector2(6, 50), new Vector2(18, 50), new Vector2(28, 50), new Vector2(42, 53.75f), new Vector2(52.25f, 64),
            new Vector2(56, 78), new Vector2(56, 94), new Vector2(58, 112), new Vector2(70, 128), new Vector2(96, 140), new Vector2(140, 150),
            new Vector2(200, 152), new Vector2(236, 174), new Vector2(238, 212), new Vector2(214, 234), new Vector2(186, 246), new Vector2(164, 270),
            new Vector2(168, 300), new Vector2(186, 322), new Vector2(212, 336), new Vector2(240, 352),
        };
        public const int D2 = 6, D3 = 9; // over the rim; the first point 40 m clear of the tile's free edges
        // The climb out of the valley rises 9.5% until it reaches the rim (7 m) before the band.
        static readonly float[] DriveHeights =
        {
            0.8f, 1.75f, 2.9f, 3.85f, 5.3f, 6.6f, 7, 7, 8, 9.5f, 12, 15.5f, 20.5f, 24, 27, 29.5f, 32, 34.5f, 37, 39.5f, 41.8f, 44,
        };

        // B to the roadblock: the gate, warm-up drums, a sandbag nest, the watchtower, the fuel
        // dump, two plates, then the roadblock past the end.
        public static readonly Vector2[] RidePoints =
        {
            new Vector2(240, 352), new Vector2(252, 368), new Vector2(258, 388), new Vector2(252, 406), new Vector2(238, 417), new Vector2(218, 422),
            new Vector2(196, 421), new Vector2(172, 415), new Vector2(150, 410), new Vector2(130, 413), new Vector2(110, 418), new Vector2(88, 417),
            new Vector2(68, 412), new Vector2(48, 412), new Vector2(30, 412), new Vector2(15, 406), new Vector2(4, 396), new Vector2(-5, 385),
        };
        static readonly float[] RideHeights =
        {
            44, 44.5f, 45, 45.5f, 46, 46.5f, 47, 47, 47, 46.5f, 46, 45.5f, 45, 44.5f, 44, 43.5f, 43, 42.5f,
        };

        public static readonly RoadPath Drive = new RoadPath(DrivePoints, DriveHeights);
        public static readonly RoadPath Ride = new RoadPath(RidePoints, RideHeights);

        // Checkpoint B: the outpost clearing where the drive meets the ride.
        public static readonly Vector3 CheckpointB = new Vector3(240, 44, 352);
        public const float CaptureRadiusB = 6;
        public const float ClearingRadius = 14; // level ground around B

        // Where the jeep waits: at A's motor pool (the drive's start) and at B's gate.
        public static Pose ParkingA => PoseAt(Drive, 4);
        public static Pose ParkingB => PoseAt(Ride, 4);

        static Pose PoseAt(RoadPath road, float s)
        {
            var a = road.At(s);
            return new Pose(new Vector3(a.Position.x, a.Height, a.Position.y), Quaternion.LookRotation(new Vector3(a.Tangent.x, 0, a.Tangent.y)));
        }

        // ------------------------------------------------------------------ bounds

        // Where a circle of radius r may be: inside the walkable patch square, or within `half`
        // of the drive (and of the ride once it is open). Outside both, the nearer of the two
        // edges. ClampMargin keeps it off the square's edge, as GameConfig.ResolveCircle does.
        public const float ClampMargin = 0.6f;

        public static Vector3 Clamp(Vector3 p, float r, float half, bool ride)
        {
            float lim = JungleStage.PlayHalfSize - r - ClampMargin;
            if (Mathf.Abs(p.x) <= lim && Mathf.Abs(p.z) <= lim) return p;
            Vector3 best = new Vector3(Mathf.Clamp(p.x, -lim, lim), p.y, Mathf.Clamp(p.z, -lim, lim));
            float bestD = (best - p).sqrMagnitude;
            Corridor(Drive, p, half - r, ref best, ref bestD);
            if (bestD > 0 && ride) Corridor(Ride, p, half - r, ref best, ref bestD);
            return best;
        }

        static void Corridor(RoadPath road, Vector3 p, float allowed, ref Vector3 best, ref float bestD)
        {
            var q = new Vector2(p.x, p.z);
            var hit = road.Nearest(q);
            if (hit.Distance <= allowed)
            {
                best = p;
                bestD = 0;
                return;
            }
            var c = road.At(hit.S).Position;
            var to = c + (q - c).normalized * allowed;
            float d = (to - q).sqrMagnitude;
            if (d >= bestD) return;
            bestD = d;
            best = new Vector3(to.x, p.y, to.y);
        }

        // Past the edge of the world: neither on the patch nor on the tile (bolts end there).
        public static bool OutOfWorld(Vector3 p) => !InPatch(p.x, p.z) && !InTile(p.x, p.z);

        // ------------------------------------------------------------------ the gun ride

        // Where the ride stops: short of the roadblock at the ride road's end.
        public const float RoadblockGap = 14;
        public static float RideEnd => Ride.Length - RoadblockGap;

        // The jeep's speed along the ride (arc length, m/s): out through B's gate, the warm-up
        // drums, the nest, the watchtower, a crawl and a pause at the fuel dump, then on to the
        // roadblock.
        public static readonly Vector2[] RideSpeeds =
        {
            new Vector2(0, 3), new Vector2(14, 5), new Vector2(30, 8), new Vector2(75, 7), new Vector2(118, 7),
            new Vector2(155, 4), new Vector2(182, 4), new Vector2(200, 8), new Vector2(262, 8), new Vector2(290, 6),
        };
        public const float FuelDumpPause = 170, PauseTime = 2;
        public const float EndWait = 8; // at the end: until the roadblock's plates are down, or this long

        public static float RideSpeed(float s)
        {
            if (s >= RideEnd) return 0;
            // Slowing to a stop over the last 10 m.
            float stop = Mathf.Sqrt(Mathf.Max(0, RideEnd - s) * 2 * 1.2f);
            float v = RideSpeeds[RideSpeeds.Length - 1].y;
            for (int i = 0; i < RideSpeeds.Length - 1; i++)
                if (s < RideSpeeds[i + 1].x)
                {
                    v = Mathf.Lerp(RideSpeeds[i].y, RideSpeeds[i + 1].y, Mathf.InverseLerp(RideSpeeds[i].x, RideSpeeds[i + 1].x, s));
                    break;
                }
            return Mathf.Max(0.5f, Mathf.Min(v, stop));
        }

        public enum TargetKind { Drum, Plate, Crate, Trailer }

        // A target beside the ride: `S` metres along it, `Side` metres right of the centreline
        // (left negative), `Lift` above the ground (a plate on the watchtower), facing the road.
        // Plates lie flat until the jeep reaches `PopAt`. Roadblock plates end the ride.
        public struct Target
        {
            public TargetKind Kind;
            public float S, Side, Lift, PopAt;
            public bool Roadblock;
        }

        // Cover and set dressing the targets stand behind (the gun's rounds stop at them).
        public enum CoverKind { Log, Nest, Tower, Roadblock }

        public struct Cover
        {
            public CoverKind Kind;
            public float S, Side;
        }

        static Target T(TargetKind k, float s, float side, float popAt = -1, float lift = 0, bool roadblock = false) =>
            new Target { Kind = k, S = s, Side = side, PopAt = popAt, Lift = lift, Roadblock = roadblock };

        public static readonly Target[] Targets = BuildTargets();
        public static readonly Cover[] Covers = BuildCovers();

        static Target[] BuildTargets()
        {
            float end = RideEnd;
            return new[]
            {
                // Warm-up: three drums together, a plate behind a log.
                T(TargetKind.Drum, 34, 10), T(TargetKind.Drum, 36.5f, 11.5f), T(TargetKind.Drum, 38.5f, 9.8f),
                T(TargetKind.Plate, 58, -11, 40),
                // The sandbag nest: two plates; a crate stack across the road.
                T(TargetKind.Plate, 83.5f, -13.2f, 68), T(TargetKind.Plate, 86.5f, -12.6f, 68),
                T(TargetKind.Crate, 92, 11), T(TargetKind.Crate, 93.5f, 11.8f),
                // The watchtower: a plate on its platform, a drum pile at its foot.
                T(TargetKind.Plate, 126, 13.5f, 104, 5.2f),
                T(TargetKind.Drum, 121, 11.2f), T(TargetKind.Drum, 122.4f, 10.2f), T(TargetKind.Drum, 123.6f, 11.4f), T(TargetKind.Drum, 122.2f, 12.4f),
                // The fuel dump: the trailer and six drums around it.
                T(TargetKind.Trailer, 170, -13.5f),
                T(TargetKind.Drum, 164, -11), T(TargetKind.Drum, 165.5f, -10.2f), T(TargetKind.Drum, 167, -15.5f),
                T(TargetKind.Drum, 173.5f, -10.8f), T(TargetKind.Drum, 175, -11.8f), T(TargetKind.Drum, 176, -15),
                // Two plates either side.
                T(TargetKind.Plate, 212, 10.5f, 194), T(TargetKind.Plate, 214, -10.5f, 194),
                T(TargetKind.Drum, 255, 11), T(TargetKind.Drum, 257, -12),
                // The roadblock: plates in its two nests and one behind the barricade.
                T(TargetKind.Plate, end + 9, -6.2f, end - 40, 0, true), T(TargetKind.Plate, end + 9, 6.2f, end - 40, 0, true),
                T(TargetKind.Plate, end + 14.5f, 0, end - 40, 0.8f, true),
            };
        }

        static Cover[] BuildCovers()
        {
            float end = RideEnd;
            return new[]
            {
                new Cover { Kind = CoverKind.Log, S = 56, Side = -10 },
                new Cover { Kind = CoverKind.Nest, S = 85, Side = -11.6f },
                new Cover { Kind = CoverKind.Tower, S = 126, Side = 13.5f },
                new Cover { Kind = CoverKind.Nest, S = end + 8, Side = -6.2f },
                new Cover { Kind = CoverKind.Nest, S = end + 8, Side = 6.2f },
                new Cover { Kind = CoverKind.Roadblock, S = end + 13, Side = 0 },
            };
        }

        // Where a ride spot is in the world (on the ground), and which way faces the road.
        public static Vector3 RidePoint(float s, float side, out Quaternion facing)
        {
            var a = Ride.At(Mathf.Min(s, Ride.Length));
            var right = new Vector2(a.Tangent.y, -a.Tangent.x);
            // Past the road's end: straight on.
            float past = Mathf.Max(0, s - Ride.Length);
            var p = a.Position + a.Tangent * past + right * side;
            var toRoad = side == 0 ? -a.Tangent : -right * Mathf.Sign(side);
            facing = Quaternion.LookRotation(new Vector3(toRoad.x, 0, toRoad.y));
            return new Vector3(p.x, JungleLayout.Height(p.x, p.y), p.y);
        }

        // Whether (x, z) is within `margin` of a target or cover (trees keep off).
        public static bool NearRideSpot(float x, float z, float margin)
        {
            var p = new Vector2(x, z);
            foreach (var t in Targets)
            {
                var w = RidePoint(t.S, t.Side, out _);
                if (Vector2.Distance(p, new Vector2(w.x, w.z)) < margin) return true;
            }
            foreach (var c in Covers)
            {
                var w = RidePoint(c.S, c.Side, out _);
                if (Vector2.Distance(p, new Vector2(w.x, w.z)) < margin + (c.Kind == CoverKind.Roadblock ? 5 : 2)) return true;
            }
            return false;
        }

        // B's gate across the ride road, closed until the ride.
        public const float GateS = 11;
        public const float GateHalf = 8; // across the road, metres either side

        // The covers as solid boxes (rounds stop at them; plates rise behind them), then B's gate.
        public static Block[] RideBlocks()
        {
            var list = new System.Collections.Generic.List<Block>();
            foreach (var c in Covers)
            {
                var p = RidePoint(c.S, c.Side, out var facing);
                float yaw = facing.eulerAngles.y;
                // Sizes along the road (x) and toward it (z), and height.
                Vector3 size = c.Kind == CoverKind.Log ? new Vector3(4.5f, 0.9f, 0.9f)
                    : c.Kind == CoverKind.Nest ? new Vector3(3.2f, 1.2f, 1.1f)
                    : c.Kind == CoverKind.Roadblock ? new Vector3(11, 1.3f, 1.4f)
                    : Vector3.zero;
                if (size == Vector3.zero) continue; // the watchtower's legs stop nothing
                list.Add(new Block { Center = new Vector2(p.x, p.z), Half = new Vector2(size.x / 2, size.z / 2), Yaw = yaw, Bottom = p.y - 0.5f, Top = p.y + size.y });
            }
            list.Add(GateBlock(true));
            return list.ToArray();
        }

        // B's gate: a barricade across the ride road (closed), or nothing (open).
        public static Block GateBlock(bool closed)
        {
            var a = Ride.At(GateS);
            float y = JungleLayout.Height(a.Position.x, a.Position.y);
            float yaw = Mathf.Atan2(a.Tangent.x, a.Tangent.y) * Mathf.Rad2Deg;
            return new Block
            {
                Center = a.Position, Half = new Vector2(GateHalf, 0.6f), Yaw = yaw,
                Bottom = closed ? y - 0.5f : -1000, Top = closed ? y + 2.4f : -1000,
            };
        }

        public static bool InTile(float x, float z) => x >= TileMinX && x <= TileMaxX && z >= TileMinZ && z <= TileMaxZ;

        public static bool InPatch(float x, float z) => Mathf.Abs(x) <= PatchHalf && Mathf.Abs(z) <= PatchHalf;

        // ------------------------------------------------------------------ the patch

        // The drive's carve into the patch's valley floor: the road level at its profile, blended
        // back to the ground `h` from CarveFull to CarveOuter. Only past the motor pool and short
        // of the rim band. Exactly h (no arithmetic) everywhere else, so the course, the LZ and
        // checkpoint A keep their heights bit for bit.
        public static float Carve(float x, float z, float h)
        {
            if (z > SeamZ - SeamBand / 2 || !Drive.Near(new Vector2(x, z), CarveOuter)) return h;
            var hit = Drive.Nearest(new Vector2(x, z));
            if (hit.Distance >= CarveOuter) return h;
            float w = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(CarveFull, CarveOuter, hit.Distance)))
                * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4, 12, hit.S))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(SeamZ - SeamBand, SeamZ - SeamBand / 2, z)));
            if (w <= 0) return h;
            return Mathf.Lerp(h, Drive.At(hit.S).Height, w);
        }

        // ------------------------------------------------------------------ the valley

        // The valley the roads run in: each road's surface, its banks, then walls rising at
        // WallSlope. The lower of the two roads' valleys. The far landscape is never higher than
        // this, and the tile's ground away from the roads follows it.
        public static float Valley(float x, float z, float ceiling = float.MaxValue)
        {
            var p = new Vector2(x, z);
            return Mathf.Min(RoadValley(Drive, p, ceiling), RoadValley(Ride, p, ceiling));
        }

        // How high the bank is at a point of the road: 2 m on straights, 5 m at the tightest bends
        // (steeper than the jeep can climb).
        public static float BankHeight(float curvature) => 2 + 3 * Mathf.Clamp01((Mathf.Abs(curvature) - 0.01f) / 0.03f);

        static float RoadValley(RoadPath road, Vector2 p, float ceiling)
        {
            // Too far from this road for its valley to matter under the ceiling: skip the search.
            if (ceiling < float.MaxValue && !road.Near(p, BankTo + (ceiling - road.MinHeight) / WallSlope)) return float.MaxValue;
            var hit = road.Nearest(p);
            var a = road.At(hit.S);
            return Shape(a.Height, BankHeight(a.Curvature), hit.Distance);
        }

        static float Shape(float surface, float bank, float d) =>
            surface + bank * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(BankFrom, BankTo, d)) + WallSlope * Mathf.Max(0, d - BankTo);

        // ------------------------------------------------------------------ the tile

        // The tile's ground: the road level across its width; where the land is higher, banks
        // and valley walls up to it (never above it); where it is lower, an embankment down
        // to it; a level clearing at B; and at the tile's edges a blend into the landscape's own
        // 16 m grid. `natural` is the land before the valley (JungleLayout.NaturalRaw).
        public static float TileHeight(float x, float z, float natural)
        {
            var p = new Vector2(x, z);
            var dHit = Drive.Nearest(p);
            var rHit = Ride.Nearest(p);
            var near = dHit.Distance <= rHit.Distance ? Drive.At(dHit.S) : Ride.At(rHit.S);
            float d = Mathf.Min(dHit.Distance, rHit.Distance);
            var da = Drive.At(dHit.S);
            var ra = Ride.At(rHit.S);
            float valley = Mathf.Min(Shape(da.Height, BankHeight(da.Curvature), dHit.Distance), Shape(ra.Height, BankHeight(ra.Curvature), rHit.Distance));
            float h = Mathf.Min(natural, valley);
            // Never below the road near it: the fill's slope down from the shoulder.
            float floor = near.Height - FillSlope * Mathf.Max(0, d - ShoulderHalf);
            h = Mathf.Max(h, floor);
            // The outpost clearing at B.
            float b = Vector2.Distance(p, new Vector2(CheckpointB.x, CheckpointB.z));
            if (b < ClearingRadius + 8) h = Mathf.Lerp(CheckpointB.y, h, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ClearingRadius, ClearingRadius + 8, b)));
            float e = EdgeWeight(x, z);
            if (e > 0) h = Mathf.Lerp(h, LandGrid(x, z), e);
            return h;
        }

        // 1 at the tile's edges, 0 from EdgeBlend in.
        public static float EdgeWeight(float x, float z)
        {
            float d = Mathf.Min(Mathf.Min(x - TileMinX, TileMaxX - x), Mathf.Min(z - TileMinZ, TileMaxZ - z));
            return 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(d / EdgeBlend));
        }

        // The landscape as its terrain draws it: JungleLayout.Landscape at the 16 m grid
        // points, bilinear between (exact along grid lines, where the tile's edges lie).
        public static float LandGrid(float x, float z)
        {
            float gx = Mathf.Floor(x / LandCell) * LandCell, gz = Mathf.Floor(z / LandCell) * LandCell;
            float fx = (x - gx) / LandCell, fz = (z - gz) / LandCell;
            float h00 = JungleLayout.Landscape(gx, gz), h10 = JungleLayout.Landscape(gx + LandCell, gz);
            float h01 = JungleLayout.Landscape(gx, gz + LandCell), h11 = JungleLayout.Landscape(gx + LandCell, gz + LandCell);
            return Mathf.Lerp(Mathf.Lerp(h00, h10, fx), Mathf.Lerp(h01, h11, fx), fz);
        }
    }
}
