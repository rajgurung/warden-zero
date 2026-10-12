using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WardenZero.Tests
{
    // M3's Tripo art (VehicleArt, the Vehicles group) fitted to the greybox jeep and the ride:
    // the parts are there, the wheels are the jeep's size, the hands' grips and the muzzle sit on
    // the meshes, the outpost's blocks fit its hesco, and the jungle bundle shares nothing with it.
    public class VehicleArtTests
    {
        const string Prefab = "Assets/Prefabs/Vehicles/VehicleArt.prefab";
        const string Jungle = "Assets/Scenes/Jungle.unity";
        static readonly string[] Parts = { "Body", "Riser", "Wheel", "Steering", "Yoke", "Gun", "Drum", "Plate", "Trailer", "Nest", "Tower", "Roadblock", "Hesco" };

        Scene scene;
        GameObject art;

        [SetUp]
        public void Open()
        {
            scene = EditorSceneManager.OpenScene(Jungle, OpenSceneMode.Additive);
            art = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        }

        [TearDown]
        public void Close() => EditorSceneManager.CloseScene(scene, true);

        static VehicleRig Rig(Scene s) => s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<VehicleRig>(true)).Single();

        // A copy of an art part under a pivot, as VehicleArtLoader makes it.
        GameObject Put(string part, Transform pivot) => Object.Instantiate(art.transform.Find(part).gameObject, pivot, false);

        // The nearest mesh vertex (world) to p among the renderers under `root`.
        static float Nearest(GameObject root, Vector3 p) =>
            root.GetComponentsInChildren<MeshFilter>().Min(f => f.sharedMesh.vertices.Min(v => Vector3.Distance(f.transform.TransformPoint(v), p)));

        [Test]
        public void Art_HasEveryPart_FromTheTripoFiles()
        {
            Assert.IsNotNull(art, "VehicleArt built");
            foreach (var p in Parts)
            {
                var t = art.transform.Find(p);
                Assert.IsNotNull(t, p);
                Assert.IsTrue(t.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh != null), p);
            }
            foreach (var v in new[] { "jeep_wheel", "jeep_steering", "gun_yoke", "gun_hmg", "fuel_drum", "target_plate", "sandbag_nest", "watchtower", "fuel_trailer", "roadblock", "hesco_wall" })
                Assert.IsTrue(art.GetComponentsInChildren<MeshFilter>().Any(f => AssetDatabase.GetAssetPath(f.sharedMesh) == $"Assets/Art/Campaign/Vehicles/{v}.fbx"), v);
            var body = art.transform.Find("Body").GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.AreEqual("jeep_body_cut", body.name, "the body without the fused dash wheel");
        }

        [Test]
        public void Wheels_AreTheJeepsSize_OnTheHubs()
        {
            var rig = Rig(scene);
            var wheel = Put("Wheel", rig.wheels[0]);
            var f = wheel.GetComponentInChildren<MeshFilter>();
            var b = f.sharedMesh.bounds;
            // The spin axis is the thin one; the radius is half the other two, in world units.
            var size = Vector3.Scale(b.size, f.transform.lossyScale);
            float radius = (size.x + size.y + size.z - Mathf.Min(size.x, Mathf.Min(size.y, size.z))) / 4;
            Assert.AreEqual(GameConfig.Jeep.WheelRadius, radius, GameConfig.Jeep.WheelRadius * 0.03f, "within 3%");
            Assert.Less(Vector3.Distance(f.transform.TransformPoint(b.center), rig.wheels[0].position), 0.05f, "centred on the hub");
            Object.DestroyImmediate(wheel);
        }

        [Test]
        public void Grips_AndMuzzle_SitOnTheParts()
        {
            var rig = Rig(scene);
            var steering = Put("Steering", rig.steering);
            VehiclePose.WheelGrips(rig.steering, out var l, out var r);
            Assert.Less(Nearest(steering, l), 0.05f, "left hand on the rim");
            Assert.Less(Nearest(steering, r), 0.05f, "right hand on the rim");
            var gun = Put("Gun", rig.gun);
            VehiclePose.GunGrips(rig.gun, out l, out r);
            Assert.Less(Nearest(gun, l), 0.08f, "left spade grip");
            Assert.Less(Nearest(gun, r), 0.08f, "right spade grip");
            Assert.Less(Nearest(gun, rig.muzzle.position), 0.1f, "the muzzle at the flash hider");
            var yoke = Put("Yoke", rig.gunYoke);
            Assert.Less(Nearest(yoke, rig.gun.position), 0.25f, "the trunnion in the yoke");
            Object.DestroyImmediate(steering);
            Object.DestroyImmediate(gun);
            Object.DestroyImmediate(yoke);
        }

        [Test]
        public void Outpost_BlocksFitTheirHesco()
        {
            var blocks = RoadLayout.RideBlocks();
            var go = new GameObject("Probe");
            foreach (var h in RoadLayout.Outpost)
            {
                go.transform.SetPositionAndRotation(h.position, h.rotation);
                var copy = Put("Hesco", go.transform);
                var min = Vector3.positiveInfinity;
                var max = Vector3.negativeInfinity;
                var block = blocks.First(b => Vector2.Distance(b.Center, new Vector2(h.position.x, h.position.z)) < 0.01f);
                foreach (var f in copy.GetComponentsInChildren<MeshFilter>())
                    foreach (var v in f.sharedMesh.vertices)
                    {
                        var l = block.ToLocal(f.transform.TransformPoint(v));
                        min = Vector3.Min(min, l);
                        max = Vector3.Max(max, l);
                    }
                Assert.AreEqual(block.Half.x, (max.x - min.x) / 2, 0.25f, "length");
                Assert.AreEqual(block.Half.y, (max.z - min.z) / 2, 0.25f, "depth");
                Assert.AreEqual(0, (max.x + min.x) / 2, 0.25f, "centred");
                Assert.AreEqual(0, (max.z + min.z) / 2, 0.25f, "centred");
                Object.DestroyImmediate(copy);
            }
            Object.DestroyImmediate(go);
        }

        [Test]
        public void JungleBundle_HoldsNoVehicleArt_AndSharesOnlyTheShader()
        {
            var jungle = AssetDatabase.GetDependencies(Jungle, true);
            Assert.IsFalse(jungle.Contains(Prefab), "the scene never references the art");
            Assert.IsFalse(jungle.Any(d => d.StartsWith("Assets/Art/Campaign/Vehicles/")), "no Vehicles meshes or textures in the jungle");
            // What the bundle would hold: dependencies followed through everything but the model
            // files (an FBX's own import-time default material is not packed with its mesh).
            System.Collections.Generic.HashSet<string> Deps(string root)
            {
                var seen = new System.Collections.Generic.HashSet<string>();
                var todo = new System.Collections.Generic.Stack<string>();
                todo.Push(root);
                while (todo.Count > 0)
                {
                    var a = todo.Pop();
                    if (!seen.Add(a) || a.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (var d in AssetDatabase.GetDependencies(a, false)) todo.Push(d);
                }
                return seen;
            }
            var shared = Deps(Prefab).Intersect(Deps(Jungle)).Where(d => !d.EndsWith(".cs")).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader" }, shared, "only the Lit shader (each bundle has the variants it uses)");
        }

        [Test]
        public void BundleCheck_FailsLoudly_WhenTheVehiclesPrefabIsMissing()
        {
            // AddressablesSetup lives in the editor scripts' assembly, out of the tests' reach.
            var setup = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("WardenZero.EditorTools.AddressablesSetup")).First(t => t != null);
            var check = setup.GetMethod("CheckBundles");
            Assert.DoesNotThrow(() => check.Invoke(null, new object[] { Jungle, Prefab }), "the real prefab passes");
            var e = Assert.Throws<System.Reflection.TargetInvocationException>(() => check.Invoke(null, new object[] { Jungle, "Assets/Prefabs/Vehicles/Missing.prefab" }));
            StringAssert.Contains("missing", e.InnerException.Message);
        }
    }
}
