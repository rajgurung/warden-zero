using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WardenZero.Tests
{
    // The Warden's milestone 2 traversal clips, as imported (Warden3DImport.Clips).
    public class ClipTests
    {
        static AnimationClip Clip(string file, string name) =>
            AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Warden3D/Anims/" + file).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);

        [Test]
        public void TraversalClips_Exist_WithTheirTrims_LoopFlags_AndHumanMotion()
        {
            foreach (var (file, name, length, loop) in new[]
            {
                ("warden_anims_m2_traversal.fbx", "jump_takeoff", 0.6f, false),
                ("warden_anims_m2_traversal.fbx", "jump_grab", 0.75f, false),
                ("warden_anims_m2_traversal.fbx", "climb_step", 0.75f, true),
                ("warden_anims_m1_fall_look.fbx", "climb_ladder", 3.42f, true),
            })
            {
                var c = Clip(file, name);
                Assert.IsNotNull(c, name);
                Assert.AreEqual(length, c.length, 0.05f, name);
                Assert.AreEqual(loop, c.isLooping, name);
                Assert.IsTrue(c.isHumanMotion, name);
            }
            // The rejected re-trims are not imported.
            Assert.IsNull(Clip("warden_anims_m1_fall_look.fbx", "jump_m1"));
            Assert.IsNull(Clip("warden_anims_m2_traversal.fbx", "jump_hop"));
        }
    }
}
