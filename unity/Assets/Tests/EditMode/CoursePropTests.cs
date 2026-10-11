using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WardenZero.Tests
{
    // The built jungle scene: every course block has its Tripo prop, fitted to it.
    public class CoursePropTests
    {
        Scene scene;

        [SetUp]
        public void Open() => scene = EditorSceneManager.OpenScene("Assets/Scenes/Jungle.unity", OpenSceneMode.Additive);

        [TearDown]
        public void Close() => EditorSceneManager.CloseScene(scene, true);

        [Test]
        public void EveryBlock_HasItsProp_FootprintWithinAQuarterMetre()
        {
            var course = scene.GetRootGameObjects().Single(g => g.name == "Course").transform;
            foreach (var piece in CourseLayout.Pieces)
            {
                var b = piece.Block;
                var root = course.Find(piece.Name);
                Assert.IsNotNull(root, piece.Name);
                var filters = root.GetComponentsInChildren<MeshFilter>();
                string fbx = $"Assets/Art/Campaign/Traversal/{piece.Prop}.fbx";
                Assert.IsTrue(filters.Length > 0 && filters.All(f => AssetDatabase.GetAssetPath(f.sharedMesh) == fbx), $"{piece.Name}: {piece.Prop}");
                // The prop's bounds in the block's own frame.
                var min = Vector3.positiveInfinity;
                var max = Vector3.negativeInfinity;
                foreach (var f in filters)
                {
                    var mb = f.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = mb.center + Vector3.Scale(mb.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2 - Vector3.one);
                        var l = b.ToLocal(f.transform.TransformPoint(c));
                        min = Vector3.Min(min, l);
                        max = Vector3.Max(max, l);
                    }
                }
                Assert.AreEqual(b.Half.x, (max.x - min.x) / 2, 0.25f, $"{piece.Name} width");
                Assert.AreEqual(b.Half.y, (max.z - min.z) / 2, 0.25f, $"{piece.Name} depth");
                Assert.AreEqual(0, (max.x + min.x) / 2, 0.25f, $"{piece.Name} centred");
                Assert.AreEqual(0, (max.z + min.z) / 2, 0.25f, $"{piece.Name} centred");
            }
        }
    }
}
