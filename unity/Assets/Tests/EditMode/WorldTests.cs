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
    }
}
