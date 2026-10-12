using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    public class WorldTests
    {
        [TearDown]
        public void Reset() => World.UseArena();

        [Test]
        public void Jungle_UsesItsOwnBounds_AndNoWalls()
        {
            World.Use(GreenfangMission.WorldW / 2, GreenfangMission.WorldD / 2, new WallRect[0], new Vector3[0]);
            var p = GameConfig.ResolveCircle(new Vector3(100, 0, 0), 0.5f);
            Assert.AreEqual(GreenfangMission.WorldW / 2 - 1.1f, p.x, 1e-3f);
            // An arena wall position is open ground in the jungle.
            Assert.IsFalse(GameConfig.PointInWall(new Vector3(-17.33f, 0, 11.33f)));
        }

        [Test]
        public void PushOutOfTrunks_KeepsTheWardenOutside()
        {
            World.Use(50, 50, new WallRect[0], new[] { new Vector3(0, 0.5f, 0) });
            var p = World.PushOutOfTrunks(new Vector3(0.3f, 0, 0), 0.6f);
            Assert.AreEqual(1.1f, p.x, 1e-4f);
        }

        // A 4 x 2 m block turned 30 degrees, 1.2 m tall, centred at (10, 0).
        static Block Box(float top = 1.2f, float yaw = 30) => new Block
        {
            Center = new Vector2(10, 0), Half = new Vector2(2, 1), Yaw = yaw, Bottom = -0.3f, Top = top,
            Flags = BlockFlags.Vault | BlockFlags.Ledge, Ledges = Block.AllEdges,
        };

        [Test]
        public void Block_FramesRoundTrip_AndFacesPointOut()
        {
            var b = Box();
            var p = new Vector3(11.3f, 0.4f, -0.7f);
            Assert.Less((b.ToWorld(b.ToLocal(p)) - p).magnitude, 1e-4f);
            // Face 0 is the block's own +z: turned 30 degrees, it points north-north-east.
            Assert.Less(Vector3.Angle(b.Normal(0), Quaternion.Euler(0, 30, 0) * Vector3.forward), 0.01f);
            b.Edge(0, out var a, out var e);
            Assert.AreEqual(4, Vector3.Distance(a, e), 1e-4f, "the +z edge runs the 4 m length");
            Assert.AreEqual(1.2f, a.y, 1e-4f);
            Assert.AreEqual(2, b.Depth(0), 1e-4f);
            Assert.AreEqual(4, b.Depth(1), 1e-4f);
        }

        [Test]
        public void PushOutOfBlocks_CircleAgainstATurnedBox()
        {
            World.Use(50, 50, new WallRect[0], new Vector3[0]);
            var b = Box();
            World.Blocks = new[] { b };
            // Walking into the +z face: pushed back out along its normal, r from the face.
            Vector3 face = b.ToWorld(new Vector3(0, 0, 1));
            Vector3 into = face + b.Normal(0) * 0.3f;
            var p = World.PushOutOfBlocks(into, 0.6f, 0);
            Assert.AreEqual(0.6f, Vector3.Dot(p - face, b.Normal(0)), 1e-3f);
            // Centre right inside it: out through the nearest side.
            p = World.PushOutOfBlocks(b.ToWorld(new Vector3(1.8f, 0, 0)), 0.6f, 0);
            Assert.AreEqual(2.6f, b.ToLocal(p).x, 1e-3f);
            // Clear of it: untouched.
            var free = new Vector3(0, 0, 0);
            Assert.AreEqual(free, World.PushOutOfBlocks(free, 0.6f, 0));
        }

        [Test]
        public void SurfaceAt_StandsOnATop_WithinAStep()
        {
            World.Use(50, 50, new WallRect[0], new Vector3[0]);
            World.Ground = p => 0.1f;
            World.Blocks = new[] { Box(0.45f), };
            Vector3 on = new Vector3(10, 0, 0);
            Assert.AreEqual(0.45f, World.SurfaceAt(on, 0.1f), 1e-4f, "a 0.35 m step up");
            Assert.AreEqual(0.1f, World.SurfaceAt(new Vector3(0, 0, 0), 0.1f), 1e-4f, "off it: the ground");
            // A 1.2 m top is not a step from the ground, but it is the floor once he is up there.
            World.Blocks = new[] { Box(1.2f) };
            Assert.AreEqual(0.1f, World.SurfaceAt(on, 0.1f), 1e-4f);
            Assert.AreEqual(1.2f, World.SurfaceAt(on, 1.2f), 1e-4f);
        }

        [Test]
        public void StepUpLimit_LowBlocksDontPush_TallOnesDo()
        {
            World.Use(50, 50, new WallRect[0], new Vector3[0]);
            var b = Box(GameConfig.StepUp - 0.01f);
            World.Blocks = new[] { b };
            Vector3 near = b.ToWorld(new Vector3(0, 0, 1.2f));
            Assert.AreEqual(near, World.PushOutOfBlocks(near, 0.6f, 0), "a step: walked onto");
            b.Top = GameConfig.StepUp + 0.05f;
            World.Blocks = new[] { b };
            Assert.AreNotEqual(near, World.PushOutOfBlocks(near, 0.6f, 0), "over a step: a wall");
        }

        [Test]
        public void NoPush_FromABlockBelowHisFeet()
        {
            World.Use(50, 50, new WallRect[0], new Vector3[0]);
            var b = Box(2.2f);
            World.Blocks = new[] { b };
            Vector3 onTop = b.ToWorld(new Vector3(1.9f, 0, 0.9f));
            Assert.AreEqual(onTop, World.PushOutOfBlocks(onTop, 0.6f, 2.2f), "standing at its edge");
            Assert.AreEqual(onTop, World.PushOutOfBlocks(onTop, 0.6f, 3.5f), "in the air above it");
        }

        [Test]
        public void Use_ClearsBlocks_AndTraversal()
        {
            World.Blocks = new[] { Box() };
            World.TraversalEnabled = true;
            World.Use(50, 50, new WallRect[0], new Vector3[0]);
            Assert.AreEqual(0, World.Blocks.Length);
            Assert.IsFalse(World.TraversalEnabled);
        }

        [Test]
        public void GreenfangLayout_MatchesV1()
        {
            // v1 (1050, 1240) in a 2800 x 1900 px world.
            Assert.AreEqual((1050 - 1400) / 30f, GreenfangMission.Beacons[0].x, 1e-4f);
            Assert.AreEqual(-(1240 - 950) / 30f, GreenfangMission.Beacons[0].z, 1e-4f);
            Assert.AreEqual(5f, GreenfangMission.BeaconRadius, 1e-4f);
            Assert.AreEqual(170 / 30f, StrikeSystem.ImpactRadius, 1e-4f);
        }

        // ------------------------------------------------------------------ M3: jungle bounds, trunk grid

        static void JungleBounds(bool ride = false)
        {
            World.Use(JungleStage.PlayHalfSize, JungleStage.PlayHalfSize, new WallRect[0], new Vector3[0]);
            World.Bounds = (p, r) => RoadLayout.Clamp(p, r, RoadLayout.FootCorridorHalf, ride);
            World.OutOfBounds = RoadLayout.OutOfWorld;
        }

        [Test]
        public void JungleBounds_ThePatch_TheCorridor_AndTheirUnion()
        {
            JungleBounds();
            float r = GameConfig.PlayerRadius;
            // Inside the walkable square: untouched.
            var inside = new Vector3(-40, 0, -30);
            Assert.AreEqual(inside, GameConfig.ResolveCircle(inside, r));
            // Off the square to the south (no road there): back on its edge.
            var south = GameConfig.ResolveCircle(new Vector3(-40, 0, -75), r);
            Assert.AreEqual(-(JungleStage.PlayHalfSize - r - RoadLayout.ClampMargin), south.z, 1e-3f);
            // On the road far up the tile: untouched, and so is a step off it into the corridor.
            var a = RoadLayout.Drive.At(300);
            var onRoad = new Vector3(a.Position.x, 0, a.Position.y);
            Assert.AreEqual(onRoad, GameConfig.ResolveCircle(onRoad, r));
            var right = new Vector3(a.Tangent.y, 0, -a.Tangent.x);
            var near = onRoad + right * 12;
            Assert.AreEqual(near, GameConfig.ResolveCircle(near, r));
            // Too far into the forest: pulled back to the corridor's edge.
            var far = GameConfig.ResolveCircle(onRoad + right * 40, r);
            Assert.AreEqual(RoadLayout.FootCorridorHalf - r, RoadLayout.Drive.Nearest(new Vector2(far.x, far.z)).Distance, 0.05f);
            // Where the road leaves the square, the union holds: just off the square beside the road.
            var rim = RoadLayout.Drive.At(RoadLayout.Drive.Nearest(new Vector2(56, 74)).S);
            var byRim = new Vector3(rim.Position.x + 6, 0, rim.Position.y);
            Assert.AreEqual(byRim, GameConfig.ResolveCircle(byRim, r));
            // The ride's road is shut until the ride starts.
            var ride = RoadLayout.Ride.At(200);
            var onRide = new Vector3(ride.Position.x, 0, ride.Position.y);
            Assert.AreNotEqual(onRide, GameConfig.ResolveCircle(onRide, r));
            JungleBounds(ride: true);
            Assert.AreEqual(onRide, GameConfig.ResolveCircle(onRide, r));
        }

        [Test]
        public void JungleBounds_BoltsLiveOverThePatchAndTheTile()
        {
            JungleBounds();
            Assert.IsFalse(GameConfig.PointInWall(new Vector3(70, 2, 70)), "past 66 but still on the patch");
            Assert.IsFalse(GameConfig.PointInWall(new Vector3(200, 30, 300)), "over the road tile");
            Assert.IsTrue(GameConfig.PointInWall(new Vector3(200, 30, 60)), "over the landscape");
            Assert.IsTrue(GameConfig.PointInWall(new Vector3(0, 0, 500)), "beyond the tile");
        }

        [Test]
        public void Arena_BoundsUnchanged()
        {
            World.UseArena();
            Assert.IsNull(World.Bounds);
            Assert.IsNull(World.OutOfBounds);
            var p = GameConfig.ResolveCircle(new Vector3(100, 0, 0), 0.5f);
            Assert.AreEqual(GameConfig.HalfW - 1.1f, p.x, 1e-3f);
            Assert.IsTrue(GameConfig.PointInWall(new Vector3(GameConfig.HalfW + 1, 0, 0)));
        }

        [Test]
        public void TrunkGrid_FindsWhatBruteForceFinds()
        {
            var rng = new System.Random(3);
            var trunks = new Vector3[3000];
            for (int i = 0; i < trunks.Length; i++)
                trunks[i] = new Vector3((float)rng.NextDouble() * 400 - 60, 0.2f + (float)rng.NextDouble() * 0.9f, (float)rng.NextDouble() * 500 - 60);
            World.Use(66, 66, new WallRect[0], trunks);
            for (int k = 0; k < 300; k++)
            {
                var p = new Vector3((float)rng.NextDouble() * 440 - 80, 0, (float)rng.NextDouble() * 540 - 80);
                float r = (float)rng.NextDouble() * 3;
                // Every trunk that touches the circle is in the query (each once).
                var found = new System.Collections.Generic.HashSet<int>();
                foreach (int i in World.TrunksNear(p.x - r, p.z - r, p.x + r, p.z + r)) Assert.IsTrue(found.Add(i), "once");
                for (int i = 0; i < trunks.Length; i++)
                {
                    var t = trunks[i];
                    if (new Vector2(t.x - p.x, t.z - p.z).magnitude < r + t.y) Assert.IsTrue(found.Contains(i), $"trunk {i} near {p}");
                }
            }
        }

        [Test]
        public void TrunkGrid_FollowsNewTrunks_AndCastsStillHit()
        {
            World.Use(50, 50, new WallRect[0], new[] { new Vector3(10, 0.5f, 0) });
            Assert.IsTrue(WorldCast.Cast(new Vector3(0, 1, 0), Vector3.right, 20, 0, false, out float d));
            Assert.AreEqual(9.5f, d, 1e-3f);
            World.Use(50, 50, new WallRect[0], new[] { new Vector3(5, 0.5f, 0) });
            Assert.IsTrue(WorldCast.Cast(new Vector3(0, 1, 0), Vector3.right, 20, 0, false, out d));
            Assert.AreEqual(4.5f, d, 1e-3f, "the grid is rebuilt for the new world");
            Assert.IsTrue(WorldCast.InScenery(new Vector3(5.2f, 1, 0)));
            Assert.IsFalse(WorldCast.InScenery(new Vector3(10, 1, 0)));
        }
    }
}
