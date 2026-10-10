using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    public class GameConfigTests
    {
        [Test]
        public void Walls_MatchBabylonLayout()
        {
            Assert.AreEqual(10, GameConfig.Walls.Length);
            // First wall: v1 (1280, 760, 70x260) -> centre (-17.33, 11.33), 2.33 x 8.67 m.
            var w = GameConfig.Walls[0];
            Assert.AreEqual(-17.333f, (w.MinX + w.MaxX) / 2, 0.01f);
            Assert.AreEqual(11.333f, (w.MinZ + w.MaxZ) / 2, 0.01f);
            Assert.AreEqual(2.333f, w.MaxX - w.MinX, 0.01f);
            Assert.AreEqual(8.667f, w.MaxZ - w.MinZ, 0.01f);
        }

        [Test]
        public void PointInWall_TrueInsideWallAndOutsideArena()
        {
            Assert.IsTrue(GameConfig.PointInWall(new Vector3(-17.33f, 0, 11.33f)));
            Assert.IsTrue(GameConfig.PointInWall(new Vector3(GameConfig.HalfW + 1, 0, 0)));
            Assert.IsFalse(GameConfig.PointInWall(Vector3.zero));
        }

        [Test]
        public void ResolveCircle_PushesCircleOutOfWall()
        {
            var w = GameConfig.Walls[0];
            float r = GameConfig.PlayerRadius;
            // Overlapping the wall's right face.
            var p = GameConfig.ResolveCircle(new Vector3(w.MaxX + r * 0.5f, 0, (w.MinZ + w.MaxZ) / 2), r);
            Assert.AreEqual(w.MaxX + r, p.x, 1e-4f);
            // Centre inside the wall: pushed out along the shallowest axis.
            p = GameConfig.ResolveCircle(new Vector3(w.MaxX - 0.1f, 0, (w.MinZ + w.MaxZ) / 2), r);
            Assert.IsFalse(GameConfig.PointInWall(p));
        }

        [Test]
        public void ResolveCircle_KeepsCircleInsideArena()
        {
            var p = GameConfig.ResolveCircle(new Vector3(500, 0, -500), 1);
            Assert.LessOrEqual(p.x, GameConfig.HalfW - 1);
            Assert.GreaterOrEqual(p.z, -GameConfig.HalfD + 1);
        }

        [Test]
        public void Stats_ConvertPixelsToMetres()
        {
            Assert.AreEqual(245f, new PlayerStats().Speed);
            Assert.AreEqual(105f / 30f, GameConfig.Enemy(EnemyType.Grunt).Speed, 1e-4f);
            Assert.AreEqual(2, GameConfig.Waves.Length);
        }
    }
}
