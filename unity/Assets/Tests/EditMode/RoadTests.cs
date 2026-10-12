using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    // The road past checkpoint A as data (RoadLayout, RoadPath): the curves, the profile, the
    // corridor's spacing, the seams between the three terrains, and that the patch the M2
    // course stands on keeps its heights.
    public class RoadTests
    {
        static readonly RoadPath Drive = RoadLayout.Drive, Ride = RoadLayout.Ride;

        static float H(Vector2 p) => JungleLayout.Height(p.x, p.y);

        static Vector2 Right(RoadPath.Sample a) => new Vector2(a.Tangent.y, -a.Tangent.x);

        [Test]
        public void ArcLength_IsMonotonic_AndNearestAgreesWithBruteForce()
        {
            foreach (var road in new[] { Drive, Ride })
            {
                for (int i = 1; i < road.Count; i++)
                {
                    float step = Vector2.Distance(road.Position(i - 1), road.Position(i));
                    Assert.That(step, Is.InRange(i < road.Count - 1 ? 0.98f : 0, 1.001f), "even 1 m samples");
                }
                var rng = new System.Random(5);
                for (int k = 0; k < 200; k++)
                {
                    var a = road.At((float)rng.NextDouble() * road.Length);
                    var p = a.Position + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 60;
                    float brute = float.MaxValue;
                    for (float s = 0; s <= road.Length; s += 0.05f) brute = Mathf.Min(brute, Vector2.Distance(p, road.At(s).Position));
                    brute = Mathf.Min(brute, Vector2.Distance(p, road.At(road.Length).Position));
                    var hit = road.Nearest(p);
                    Assert.AreEqual(brute, hit.Distance, 0.02f);
                    Assert.AreEqual(hit.Distance, Vector2.Distance(p, road.At(hit.S).Position), 0.02f);
                }
            }
        }

        [Test]
        public void Nearest_SignsTheSide()
        {
            var a = Drive.At(200);
            var right = a.Position + Right(a) * 5;
            var left = a.Position - Right(a) * 5;
            Assert.AreEqual(5, Drive.Nearest(right).Lateral, 0.05f);
            Assert.AreEqual(-5, Drive.Nearest(left).Lateral, 0.05f);
        }

        [Test]
        public void Curves_AreWideEnough()
        {
            float MinRadius(RoadPath road)
            {
                float k = 0;
                for (float s = 0; s <= road.Length; s += 1) k = Mathf.Max(k, Mathf.Abs(road.At(s).Curvature));
                return 1 / k;
            }
            Assert.GreaterOrEqual(MinRadius(Drive), 22, "the drive");
            Assert.GreaterOrEqual(MinRadius(Ride), 28, "the ride");
        }

        [Test]
        public void Profile_GradeAtMost10Percent_CrestsRound()
        {
            foreach (var road in new[] { Drive, Ride })
            {
                for (float s = 0; s + 1 <= road.Length; s += 1)
                {
                    float grade = road.At(s + 1).Height - road.At(s).Height;
                    Assert.LessOrEqual(Mathf.Abs(grade), 0.1f, $"grade at s = {s}");
                }
                // Vertical curvature: the grade changes by at most 5% over 10 m.
                for (float s = 0; s + 11 <= road.Length; s += 1)
                {
                    float g0 = road.At(s + 1).Height - road.At(s).Height, g1 = road.At(s + 11).Height - road.At(s + 10).Height;
                    Assert.LessOrEqual(Mathf.Abs(g1 - g0), 0.05f, $"crest at s = {s}");
                }
            }
        }

        // The terrain under the road: level across (4% at most), and on its profile, wherever
        // the road is carved (past the motor pool, off the rim band).
        [Test]
        public void Ground_UnderTheRoad_IsLevelAcross()
        {
            foreach (var road in new[] { Drive, Ride })
            {
                float start = road == Drive ? 12 : 0;
                for (float s = start; s <= road.Length; s += 2)
                {
                    var a = road.At(s);
                    var r = Right(a) * RoadLayout.RoadHalf;
                    float left = H(a.Position - r), right = H(a.Position + r);
                    Assert.LessOrEqual(Mathf.Abs(right - left) / (2 * RoadLayout.RoadHalf), 0.04f, $"cross-slope at s = {s} {a.Position}");
                    var b = new Vector2(RoadLayout.CheckpointB.x, RoadLayout.CheckpointB.z);
                    bool blended = RoadLayout.EdgeWeight(a.Position.x, a.Position.y) > 0 || Mathf.Abs(a.Position.y - RoadLayout.SeamZ) < RoadLayout.SeamBand
                        || Vector2.Distance(a.Position, b) < RoadLayout.ClearingRadius + 8;
                    if (!blended) Assert.AreEqual(a.Height, H(a.Position), 0.05f, $"on the profile at s = {s}");
                }
            }
        }

        [Test]
        public void Legs_FarApart_NoShortcuts()
        {
            float min = 2 * RoadLayout.CorridorHalf;
            void Apart(RoadPath a, RoadPath b, System.Func<float, float, bool> adjacent, string what)
            {
                for (float s = 0; s <= a.Length; s += 2)
                    for (float t = 0; t <= b.Length; t += 2)
                    {
                        if (adjacent(s, t)) continue;
                        Assert.GreaterOrEqual(Vector2.Distance(a.At(s).Position, b.At(t).Position), min, $"{what} at {s} and {t}");
                    }
            }
            Apart(Drive, Drive, (s, t) => Mathf.Abs(s - t) < 90, "the drive");
            Apart(Ride, Ride, (s, t) => Mathf.Abs(s - t) < 90, "the ride");
            Apart(Drive, Ride, (s, t) => Drive.Length - s + t < 90, "the drive and the ride");
        }

        // Distance from p to the tile's free edges (the ones that meet the landscape; the
        // south edge west of x = 80 meets the patch).
        static float FreeEdge(Vector2 p)
        {
            float d = Mathf.Min(p.x - RoadLayout.TileMinX, Mathf.Min(RoadLayout.TileMaxX - p.x, RoadLayout.TileMaxZ - p.y));
            var corner = new Vector2(RoadLayout.PatchHalf, RoadLayout.TileMinZ);
            return Mathf.Min(d, p.x >= corner.x ? p.y - corner.y : Vector2.Distance(p, corner));
        }

        [Test]
        public void Roads_StayClearOfTheTilesFreeEdges()
        {
            // D3, the first point past the rim, and everything after it. (Between the rim and D3
            // the road passes within 30 m of the landscape's corner at (80, 80), where all three
            // terrains are flat at the rim height.)
            Assert.GreaterOrEqual(FreeEdge(RoadLayout.DrivePoints[RoadLayout.D3]), 40, "D3");
            float d3 = Drive.Nearest(RoadLayout.DrivePoints[RoadLayout.D3]).S;
            for (float s = d3; s <= Drive.Length; s += 1) Assert.GreaterOrEqual(FreeEdge(Drive.At(s).Position), 40, $"drive at {s}");
            for (float s = 0; s <= Ride.Length; s += 1) Assert.GreaterOrEqual(FreeEdge(Ride.At(s).Position), 40, $"ride at {s}");
        }

        [Test]
        public void Patch_KeepsItsHeights_ForTheCourseAndTheSpawns()
        {
            void Same(float x, float z, string what) =>
                Assert.AreEqual(JungleLayout.NaturalRaw(x, z), JungleLayout.Height(x, z), 0, $"{what} at ({x}, {z})");
            foreach (var piece in CourseLayout.Pieces)
            {
                var b = piece.Block;
                for (int i = -1; i <= 1; i++)
                    for (int k = -1; k <= 1; k++)
                    {
                        var p = b.ToWorld(new Vector3(i * b.Half.x, 0, k * b.Half.y));
                        Same(p.x, p.z, piece.Name);
                    }
            }
            var lz = JungleLayout.Lz;
            var a = JungleLayout.Checkpoint;
            for (int i = 0; i < 32; i++)
            {
                var o = Quaternion.Euler(0, i * 360 / 32f, 0) * Vector3.forward;
                for (float r = 0; r <= 8; r += 1) Same(lz.x + o.x * r, lz.z + o.z * r, "the LZ ring");
                for (float r = 0; r <= JungleStage.CaptureRadius + 1; r += 0.5f) Same(a.x + o.x * r, a.z + o.z * r, "checkpoint A's ring");
            }
            foreach (var p in new[] { lz + JungleStage.LandedOffset, a + JungleStage.NearCheckpointOffset, a + JungleStage.CheckpointAOffset })
                Same(p.x, p.z, "a spawn");
        }

        [Test]
        public void NoCourseBlock_NearTheRoad()
        {
            foreach (var piece in CourseLayout.Pieces)
            {
                var b = piece.Block;
                for (float i = -1; i <= 1; i += 0.25f)
                    for (float k = -1; k <= 1; k += 0.25f)
                    {
                        var p = b.ToWorld(new Vector3(i * b.Half.x, 0, k * b.Half.y));
                        Assert.GreaterOrEqual(Drive.Nearest(new Vector2(p.x, p.z)).Distance, 8, piece.Name);
                    }
            }
        }

        // Review item 3: the patch and the tile agree along their shared edge, which is flat
        // (the rim band), and the tile's free edges are the landscape's own.
        [Test]
        public void Seams_PatchAndTileAgree_FlatAtTheRim_LandscapeAtTheFreeEdges()
        {
            for (float x = RoadLayout.TileMinX; x <= RoadLayout.PatchHalf; x += 0.25f)
            {
                float edge = JungleLayout.Height(x, RoadLayout.SeamZ);
                Assert.AreEqual(JungleLayout.Rim, edge, 1e-3f, $"rim at x = {x}");
                Assert.AreEqual(JungleLayout.NaturalRaw(x, RoadLayout.SeamZ), edge, 1e-4f, "the patch's own value");
                // Flat across the band either side (the patch's carve stops half a band short).
                foreach (float dz in new[] { -RoadLayout.SeamBand / 2, -2f, 2f, RoadLayout.SeamBand })
                    Assert.AreEqual(edge, JungleLayout.Height(x, RoadLayout.SeamZ + dz), 0.25f, $"band at x = {x}, dz = {dz}");
            }
            void OnLandscape(float x, float z) => Assert.AreEqual(RoadLayout.LandGrid(x, z), JungleLayout.Height(x, z), 1e-3f, $"free edge at ({x}, {z})");
            for (float t = 0; t <= 1; t += 1 / 256f)
            {
                OnLandscape(RoadLayout.TileMinX, Mathf.Lerp(RoadLayout.TileMinZ, RoadLayout.TileMaxZ, t));
                OnLandscape(RoadLayout.TileMaxX, Mathf.Lerp(RoadLayout.TileMinZ, RoadLayout.TileMaxZ, t));
                OnLandscape(Mathf.Lerp(RoadLayout.TileMinX, RoadLayout.TileMaxX, t), RoadLayout.TileMaxZ);
                OnLandscape(Mathf.Lerp(RoadLayout.PatchHalf, RoadLayout.TileMaxX, t), RoadLayout.TileMinZ);
            }
            // The landscape's grid points are its own function (the bilinear is exact there).
            for (float x = RoadLayout.TileMinX; x <= RoadLayout.TileMaxX; x += RoadLayout.LandCell)
                Assert.AreEqual(JungleLayout.Landscape(x, RoadLayout.TileMaxZ), RoadLayout.LandGrid(x, RoadLayout.TileMaxZ), 1e-4f);
        }

        [Test]
        public void CheckpointB_EndsTheDrive_StartsTheRide_OnALevelClearing()
        {
            var b = new Vector2(RoadLayout.CheckpointB.x, RoadLayout.CheckpointB.z);
            Assert.Less(Vector2.Distance(Drive.At(Drive.Length).Position, b), 0.01f);
            Assert.Less(Vector2.Distance(Ride.At(0).Position, b), 0.01f);
            Assert.AreEqual(RoadLayout.CheckpointB.y, Drive.At(Drive.Length).Height, 0.01f);
            Assert.AreEqual(RoadLayout.CheckpointB.y, Ride.At(0).Height, 0.01f);
            for (int i = 0; i < 16; i++)
            {
                var o = (Vector2)(Quaternion.Euler(0, 0, i * 22.5f) * Vector2.right);
                for (float r = 0; r <= RoadLayout.CaptureRadiusB + 2; r += 1)
                    Assert.AreEqual(RoadLayout.CheckpointB.y, H(b + o * r), 0.05f, "level in the ring");
            }
        }
    }
}
