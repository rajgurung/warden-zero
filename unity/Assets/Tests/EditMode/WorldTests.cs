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
