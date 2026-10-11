using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    // The behind view's casts against trunks, rocks, walls and the ground (no physics).
    public class WorldCastTests
    {
        [TearDown]
        public void Reset() => World.UseArena();

        static float Cast(Vector3 from, Vector3 dir, float max, float radius = 0)
        {
            return WorldCast.Cast(from, dir.normalized, max, radius, false, out float d) ? d : -1;
        }

        [Test]
        public void Trunk_StopsTheRay_AtItsSurface_PlusTheSphere()
        {
            World.Use(50, 50, new WallRect[0], new[] { new Vector3(0, 0.5f, 10) });
            Assert.AreEqual(9.5f, Cast(new Vector3(0, 2, 0), Vector3.forward, 30), 1e-3f);
            Assert.AreEqual(9.2f, Cast(new Vector3(0, 2, 0), Vector3.forward, 30, 0.3f), 1e-3f);
            // Passing beside it, and stopping short of it.
            Assert.AreEqual(-1, Cast(new Vector3(1, 2, 0), Vector3.forward, 30));
            Assert.AreEqual(-1, Cast(new Vector3(0, 2, 0), Vector3.forward, 5));
            // Starting inside it (the camera's pivot never is) does not count.
            Assert.AreEqual(-1, Cast(new Vector3(0, 2, 10), Vector3.forward, 30));
        }

        [Test]
        public void Ground_StopsTheRay_EvenOnSlopes()
        {
            World.Use(50, 50, new WallRect[0], new Vector3[0]);
            Assert.AreEqual(10, Cast(new Vector3(0, 10, 0), Vector3.down, 30), 1e-2f);
            // A ramp rising 0.5 m per metre north: a level ray at 3 m meets it 6 m out.
            World.Ground = p => p.z * 0.5f;
            Assert.AreEqual(6, Cast(new Vector3(0, 3, 0), Vector3.forward, 30), 1e-2f);
            Assert.AreEqual(5.4f, Cast(new Vector3(0, 3, 0), Vector3.forward, 30, 0.3f), 1e-2f);
        }

        [Test]
        public void Rocks_And_Walls_StopTheRay()
        {
            World.Use(50, 50, new[] { new WallRect { MinX = -1, MaxX = 1, MinZ = 20, MaxZ = 22 } }, new Vector3[0]);
            World.Rocks = new[] { new Vector4(0, 1, 10, 1.5f) };
            Assert.AreEqual(8.5f, Cast(new Vector3(0, 1, 0), Vector3.forward, 30), 1e-3f);
            Assert.AreEqual(5, Cast(new Vector3(0, 1, 15), Vector3.forward, 30), 1e-3f);
            // Over the top of the wall (2.4 m tall).
            Assert.AreEqual(-1, Cast(new Vector3(0, 3, 15), Vector3.forward, 30));
        }

        [Test]
        public void InScenery_GroundTrunksRocks()
        {
            World.Use(50, 50, new WallRect[0], new[] { new Vector3(0, 0.5f, 10) });
            World.Rocks = new[] { new Vector4(5, 0, 5, 1) };
            Assert.IsTrue(WorldCast.InScenery(new Vector3(0, -0.1f, 0)));
            Assert.IsTrue(WorldCast.InScenery(new Vector3(0.2f, 3, 10)));
            Assert.IsTrue(WorldCast.InScenery(new Vector3(5, 0.5f, 5)));
            Assert.IsFalse(WorldCast.InScenery(new Vector3(2, 1, 2)));
        }

        // A jungle-sized world: ~600 trunks on gently rolling ground. Counts how often each
        // cast samples the terrain (Terrain.SampleHeight in the game).
        static int samples;

        static void Jungle()
        {
            var rng = new System.Random(3);
            var trunks = new Vector3[600];
            for (int i = 0; i < trunks.Length; i++)
                trunks[i] = new Vector3((float)rng.NextDouble() * 140 - 70, 0.3f + (float)rng.NextDouble() * 0.6f, (float)rng.NextDouble() * 140 - 70);
            World.Use(70, 70, new WallRect[0], trunks);
            World.Ground = p => { samples++; return Mathf.Sin(p.x * 0.1f) * 1.5f + Mathf.Cos(p.z * 0.08f); };
        }

        [Test]
        public void Casts_SampleTheTerrainSparingly()
        {
            Jungle();
            World.TrunkBase(0); // the trunk heights, sampled once per world
            samples = 0;
            WorldCast.Cast(new Vector3(0, 3.4f, -4), new Vector3(0, -0.05f, 1).normalized, 120, 0, true, out _); // the crosshair
            int aim = samples;
            samples = 0;
            WorldCast.Cast(new Vector3(0, 2.3f, 0), new Vector3(0.2f, 0.25f, -1).normalized, 4.2f, 0.3f, false, out _); // the camera
            int camera = samples;
            Debug.Log($"[Cast] terrain samples: crosshair {aim}, camera {camera}");
            Assert.Less(aim, 80);
            Assert.Less(camera, 40);
        }
    }
}
