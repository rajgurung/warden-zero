using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // The drop's polish: the relaxed idle and its look-around, the HALO gear from the chopper
    // to the landing (the pack left on the ground), calm freefall arms, hands on the canopy
    // lines, and the land clip handing back to the idle.
    public class JumpPolishTests : ArenaTestBase
    {
        static JungleStage Stage => Object.FindFirstObjectByType<JungleStage>();

        static IEnumerator LoadJungle(DropStart start)
        {
            yield return LoadMenu();
            Campaign.Active = true;
            Campaign.Run = new RunState();
            Campaign.Start = start;
            StageLoader.LoadJungle();
            float end = Time.realtimeSinceStartup + 60;
            while (Stage == null || GameManager.Instance.jungle != Stage)
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out loading the jungle");
                yield return null;
            }
            yield return Frames(2);
        }

        static IEnumerator Until(System.Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        // Space: the stage's own pull (deploy and the canopy phase), as the player does it.
        IEnumerator Pull()
        {
            Press(keyboard.spaceKey);
            yield return null;
            Release(keyboard.spaceKey);
            yield return null;
        }

        static string TopClip(Animator a) => a.GetCurrentAnimatorClipInfo(0).OrderByDescending(c => c.weight).First().clip.name;

        // Runs after the animation, SkydivePose and the stage's line update each frame.
        [DefaultExecutionOrder(300)]
        class AfterPose : MonoBehaviour
        {
            public System.Action tick;
            void LateUpdate() => tick?.Invoke();
        }

        [UnityTest]
        public IEnumerator Rest_IsTheRelaxedIdle_WithAnOccasionalLookAround()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            Stats.MaxHealth = Stats.Health = 1e6f;
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(View.animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            Assert.AreEqual("idle_relaxed", TopClip(View.animator), "standing on the relaxed idle");
            Assert.That(View.NextLookAround, Is.InRange(WardenModelView.LookAroundMin, WardenModelView.LookAroundMax));
            View.LookAroundSoon();
            yield return new WaitForSeconds(1);
            Assert.IsTrue(View.LookingAround);
            Assert.IsTrue(View.animator.GetCurrentAnimatorStateInfo(0).IsName("LookAround"));
            Assert.Greater(View.handIK.weight, 0.99f, "the rifle stays in both hands");
            // Moving ends it at once.
            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.7f);
            Release(keyboard.dKey);
            Assert.IsFalse(View.LookingAround);
            Assert.IsTrue(View.animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
        }

        [UnityTest]
        public IEnumerator Gear_WornForTheDrop_UnclippedAfterLanding_PackLeftOnTheGround()
        {
            yield return LoadJungle(DropStart.Jump);
            var view = View;
            Assert.IsTrue(view.GearOn);
            foreach (var g in view.gear)
            {
                Assert.IsTrue(g.gameObject.activeInHierarchy, g.name + " worn in the door");
                Assert.IsTrue(g.IsChildOf(view.animator.transform), g.name + " on his rig");
            }
            Stage.JumpNow();
            Time.timeScale = 8;
            yield return Until(() => Stage.Dive.Altitude < 250, 30, "the pull height");
            yield return Pull();
            yield return Until(() => Stage.CurrentPhase == JungleStage.Phase.Canopy, 10, "the canopy");
            Assert.IsTrue(view.gear.All(g => g.gameObject.activeInHierarchy), "worn under the canopy");
            yield return Until(() => Stage.CurrentPhase == JungleStage.Phase.Walk, 60, "the walk");
            Time.timeScale = 1;
            yield return new WaitForSeconds(0.7f); // the pack finishes falling
            Assert.IsFalse(view.GearOn);
            Assert.IsFalse(view.gear[1].gameObject.activeInHierarchy, "helmet off");
            Assert.IsFalse(view.gear[2].gameObject.activeInHierarchy, "altimeter off");
            var pack = view.DroppedPack;
            Assert.IsNotNull(pack, "the pack is dropped");
            Assert.IsTrue(pack.gameObject.activeInHierarchy, "and stays as a prop");
            Assert.IsFalse(pack.IsChildOf(Player.transform), "no longer on him");
            Assert.Less(pack.position.y - Stage.Ground(pack.position), 0.6f, "on the ground");
            Assert.Less(Vector3.Distance(new Vector3(pack.position.x, 0, pack.position.z), new Vector3(Player.transform.position.x, 0, Player.transform.position.z)), 2.5f);
            // His own kit back: rifle in both hands.
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(1, view.handIK.weight, 1e-3f);
        }

        [UnityTest]
        public IEnumerator Gear_OffWhenStartingOnFoot()
        {
            yield return LoadJungle(DropStart.Landed);
            Assert.IsFalse(View.GearOn);
            Assert.IsTrue(View.gear.All(g => !g.gameObject.activeInHierarchy));
            // A hurt tint while worn reaches the gear too (and nothing throws without it).
            View.SetGear(true);
            Player.TryHurt(1);
            yield return null;
            var block = new MaterialPropertyBlock();
            View.gear[0].GetComponentInChildren<Renderer>().GetPropertyBlock(block, 0);
            Assert.Less(block.GetColor("_BaseColor").g, 0.9f, "the pack flashes with him");
        }

        [UnityTest]
        public IEnumerator Freefall_ArmsHoldStill_NoWaving()
        {
            yield return LoadJungle(DropStart.Jump);
            Stage.JumpNow();
            yield return new WaitForSeconds(1.2f); // off the door and into the arch
            var a = View.animator;
            var chest = a.GetBoneTransform(HumanBodyBones.Chest);
            var bones = new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm }
                .Select(a.GetBoneTransform).ToArray();
            var last = new Quaternion[bones.Length];
            bool first = true;
            float worst = 0;
            int frames = 0;
            var probe = new GameObject("ArmProbe").AddComponent<AfterPose>();
            probe.tick = () =>
            {
                for (int i = 0; i < bones.Length; i++)
                {
                    var rel = Quaternion.Inverse(chest.rotation) * bones[i].rotation;
                    if (!first && Time.deltaTime > 0) worst = Mathf.Max(worst, Quaternion.Angle(last[i], rel) / Time.deltaTime);
                    last[i] = rel;
                }
                first = false;
                frames++;
            };
            yield return new WaitForSeconds(3);
            Object.Destroy(probe.gameObject);
            Debug.Log($"[Jump] freefall arms: fastest {worst:F2} deg/s relative to the chest over {frames} frames");
            Assert.Greater(frames, 30, "sampled");
            Assert.AreEqual(JungleStage.Phase.Freefall, Stage.CurrentPhase);
            Assert.Less(worst, 15, "arms held still in the arch");
        }

        [UnityTest]
        public IEnumerator Canopy_LinesEndInHisHands_ThroughTurns()
        {
            yield return LoadJungle(DropStart.Jump);
            Stage.JumpNow();
            yield return new WaitForSeconds(0.5f);
            yield return Pull();
            Time.timeScale = 3;
            yield return Until(() => Stage.Dive.Current == Skydive.State.Canopy, 20, "the open canopy");
            Time.timeScale = 1;
            Assert.IsTrue(View.animator.GetCurrentAnimatorStateInfo(0).IsName("Canopy") || View.animator.GetNextAnimatorStateInfo(0).IsName("Canopy"));
            var a = View.animator;
            var lh = a.GetBoneTransform(HumanBodyBones.LeftHand);
            var rh = a.GetBoneTransform(HumanBodyBones.RightHand);
            float worst = 0;
            var probe = new GameObject("LineProbe").AddComponent<AfterPose>();
            probe.tick = () =>
            {
                for (int i = 0; i < Stage.lines.Length; i++)
                {
                    bool left = Stage.lineAnchors[i].localPosition.x < 0;
                    // From the line's end to the hand: wrist to fist.
                    Vector3 end = Stage.lines[i].GetPosition(1);
                    Vector3 wrist = (left ? lh : rh).position;
                    Vector3 fist = Stage.LineEnd(left);
                    float t = Mathf.Clamp01(Vector3.Dot(end - wrist, fist - wrist) / Mathf.Max(1e-6f, (fist - wrist).sqrMagnitude));
                    worst = Mathf.Max(worst, Vector3.Distance(end, wrist + (fist - wrist) * t));
                }
            };
            yield return new WaitForSeconds(1);
            Press(keyboard.aKey);
            yield return new WaitForSeconds(1);
            Assert.Greater(Stage.pose.toggleLeft, 0.3f, "A pulls the left toggle");
            Release(keyboard.aKey);
            yield return null; // a key change per frame, as on a real keyboard
            Press(keyboard.dKey);
            yield return new WaitForSeconds(1);
            Assert.Greater(Stage.pose.toggleRight, 0.3f, "D pulls the right toggle");
            Release(keyboard.dKey);
            Object.Destroy(probe.gameObject);
            Debug.Log($"[Jump] canopy: lines end {worst * 100:F1} cm from his hands at worst");
            Assert.Less(worst, 0.05f, "the lines end in his hands");
        }

        [UnityTest]
        public IEnumerator SoftLanding_LandClip_BlendsToTheIdle_WithinASecondAndAHalf()
        {
            yield return LoadJungle(DropStart.Jump);
            Stage.JumpNow();
            Time.timeScale = 8;
            yield return Until(() => Stage.Dive.Altitude < 250, 30, "the pull height");
            yield return Pull();
            yield return Until(() => Stage.Dive.Altitude < 10, 60, "the flare height");
            Time.timeScale = 1;
            Stage.Dive.Flare();
            yield return Until(() => Stage.CurrentPhase == JungleStage.Phase.Landing, 20, "touchdown");
            Assert.IsFalse(Stage.HardLanding);
            var a = View.animator;
            float start = Time.time;
            yield return null;
            Assert.IsTrue(a.GetCurrentAnimatorStateInfo(0).IsName("Land") || a.GetNextAnimatorStateInfo(0).IsName("Land"), "feet down into the land clip");
            yield return Until(() => a.GetCurrentAnimatorStateInfo(0).IsName("Locomotion") && !a.IsInTransition(0), 5, "the idle");
            float took = Time.time - start;
            Debug.Log($"[Jump] land to idle in {took:F2} s");
            Assert.Less(took, 1.6f);
            Assert.AreEqual("idle_relaxed", TopClip(a));
        }
    }
}
