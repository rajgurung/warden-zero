using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // The road tile in the real jungle scene: the ground the game samples is the right
    // terrain's (review item 1), the seams agree, and bolts live past the old 66 m bounds.
    public class RoadPlayTests : JungleTestBase
    {
        [UnityTest]
        public IEnumerator Ground_IsTheTilesTerrain_OnTheRoad_AndAgreesAtTheSeams()
        {
            yield return LoadJungle(DropStart.Landed);
            Assert.IsNotNull(Stage.roadTerrain, "the tile is in the scene");
            var tile = Stage.roadTerrain;
            float TileH(Vector3 p) => tile.SampleHeight(p) + tile.transform.position.y;
            foreach (var road in new[] { RoadLayout.Drive, RoadLayout.Ride })
                for (float s = 0; s <= road.Length; s += 25)
                {
                    var a = road.At(s);
                    var p = new Vector3(a.Position.x, 0, a.Position.y);
                    if (!RoadLayout.InTile(p.x, p.z)) continue;
                    Assert.AreEqual(TileH(p), World.HeightAt(p), 1e-3f, "the tile's own ground");
                    Assert.AreEqual(JungleLayout.Height(p.x, p.z), World.HeightAt(p), 0.15f, "and the layout's");
                }
            // The patch-tile seam (flat at the rim) and a free edge to the landscape.
            for (float x = RoadLayout.TileMinX + 1; x < RoadLayout.PatchHalf; x += 7)
            {
                float below = World.HeightAt(new Vector3(x, 0, RoadLayout.SeamZ - 0.05f)), above = World.HeightAt(new Vector3(x, 0, RoadLayout.SeamZ + 0.05f));
                Assert.AreEqual(below, above, 0.05f, $"patch to tile at x = {x}");
            }
            for (float z = RoadLayout.TileMinZ + 1; z < RoadLayout.TileMaxZ; z += 23)
            {
                float inside = World.HeightAt(new Vector3(RoadLayout.TileMaxX - 0.05f, 0, z)), outside = World.HeightAt(new Vector3(RoadLayout.TileMaxX + 0.05f, 0, z));
                Assert.AreEqual(inside, outside, 0.6f, $"tile to landscape at z = {z}");
            }
        }

        [UnityTest]
        public IEnumerator Bolt_UpTheRoad_LivesPastTheOld66mBounds()
        {
            yield return LoadJungle(DropStart.Landed);
            var a = RoadLayout.Drive.At(RoadLayout.Drive.Nearest(new Vector2(56, 60)).S);
            var start = new Vector3(a.Position.x, World.HeightAt(new Vector3(a.Position.x, 0, a.Position.y)) + 1.5f, a.Position.y);
            var bolt = Object.Instantiate(Player.boltPrefab, start, Quaternion.identity);
            bolt.Launch(new Vector3(a.Tangent.x, 0.02f, a.Tangent.y).normalized, 30, 10, false, 1, true);
            float passed = 0;
            for (float t = 0; t < 0.6f && bolt != null && bolt.gameObject.activeSelf; t += Time.deltaTime)
            {
                passed = Mathf.Max(passed, bolt.transform.position.z);
                yield return null;
            }
            Assert.Greater(passed, JungleStage.PlayHalfSize + 4, "flew on past z = 66");
        }
    }
}
