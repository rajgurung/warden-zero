using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace WardenZero.Tests
{
    // The behind view: camera-relative movement, crosshair aim, camera collision, the view
    // toggle and its memory per stage, touch look and fire, and the upright rest stance.
    public class CameraTests : ArenaTestBase
    {
        static CameraFollow Follow => GameManager.Instance.cameraFollow;
        static Transform Cam => Player.cam.transform;
        static JungleStage Stage => Object.FindFirstObjectByType<JungleStage>();

        static IEnumerator Behind()
        {
            Follow.SetView(CameraFollow.View.Behind);
            yield return Frames(2);
            Assert.IsTrue(Follow.IsBehind);
        }

        static IEnumerator Look(float yaw, float pitch, float settle = 0.5f)
        {
            Follow.SetLook(yaw, pitch);
            yield return new WaitForSeconds(settle);
        }

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
            yield return Frames(3);
            Gm.ClearQueue();
        }

        // No enemies in the way, but one frozen in a far corner of the arena, so the wave never
        // clears (a cleared wave puts the Warden back in the middle).
        static void ClearEnemiesAndBolts()
        {
            Gm.ClearQueue();
            foreach (var e in Enemy.All.ToArray()) Object.Destroy(e.gameObject);
            Enemy.All.Clear();
            foreach (var b in Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None)) b.Release();
            if (Gm.jungle == null) Gm.SpawnEnemyAt(EnemyType.Grunt, new Vector3(-55, 0, 33)).enabled = false;
        }

        // Fire one shot and follow the bolt until it is spent: its path, and the crosshair's
        // point and the camera position at the moment of firing.
        IEnumerator FireOne(List<Vector3> path, Vector3[] aim)
        {
            ClearEnemiesAndBolts();
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            aim[0] = Player.AimPoint;
            aim[1] = Cam.position;
            var bolt = Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None).Single();
            path.Clear();
            float end = Time.time + GameConfig.BoltLife + 0.2f;
            while (bolt.gameObject.activeSelf && Time.time < end)
            {
                path.Add(bolt.transform.position);
                yield return null;
            }
        }

        // Angle at the camera between the point and the nearest point of the bolt's path.
        static float MissAngle(List<Vector3> path, Vector3 point, Vector3 camera)
        {
            float best = path.Count > 0 ? Vector3.Distance(point, path[path.Count - 1]) : float.MaxValue;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                float t = Mathf.Clamp01(Vector3.Dot(point - a, b - a) / Mathf.Max(1e-6f, (b - a).sqrMagnitude));
                best = Mathf.Min(best, Vector3.Distance(point, a + (b - a) * t));
            }
            return Mathf.Atan2(best, Vector3.Distance(camera, point)) * Mathf.Rad2Deg;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        // ------------------------------------------------------------------ movement and aim

        [UnityTest]
        public IEnumerator Behind_MovementIsCameraRelative_AndHeFacesTheCameraWay()
        {
            yield return LoadArena();
            ClearEnemiesAndBolts();
            Player.transform.position = new Vector3(10, 0, -25); // open floor, clear of the walls
            yield return Behind();
            yield return Look(90, 8);
            Vector3 start = Player.transform.position;
            Press(keyboard.wKey);
            yield return new WaitForSeconds(0.5f);
            Release(keyboard.wKey);
            Vector3 moved = Player.transform.position - start;
            Assert.Greater(moved.x, 1.5f, "W runs where the camera looks (east)");
            Assert.Less(Mathf.Abs(moved.z), 0.2f);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(View.Yaw, 90)), 3, "he turns to the camera's heading");

            start = Player.transform.position;
            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.5f);
            Release(keyboard.dKey);
            moved = Player.transform.position - start;
            Assert.Less(moved.z, -1.5f, "D strafes to the camera's right (south)");
            Assert.Less(Mathf.Abs(moved.x), 0.2f);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(View.Yaw, 90)), 3, "strafing keeps the facing");

            start = Player.transform.position;
            Press(keyboard.sKey);
            yield return new WaitForSeconds(0.4f);
            Release(keyboard.sKey);
            Assert.Less((Player.transform.position - start).x, -1, "S backpedals");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(View.Yaw, 90)), 3);
        }

        [UnityTest]
        public IEnumerator Behind_CameraSitsBehindAndAboveHim_OverTheRightShoulder()
        {
            yield return LoadArena();
            ClearEnemiesAndBolts();
            yield return Behind();
            yield return Look(0, GameConfig.BehindPitch);
            Vector3 off = Cam.position - Player.transform.position;
            Assert.That(-off.z, Is.InRange(3.5f, 5f), "3.5-5 m behind");
            Assert.That(off.x, Is.InRange(0.5f, 1.3f), "right of his shoulder");
            Assert.That(off.y, Is.InRange(2.9f, 4f), "a little above his head (2.65 m)");
            Assert.Less(Vector3.Angle(Cam.forward, Quaternion.Euler(GameConfig.BehindPitch, 0, 0) * Vector3.forward), 0.5f);
        }

        [UnityTest]
        public IEnumerator Crosshair_BoltsGoToThePointUnderIt_AtManyYawsAndPitches()
        {
            yield return LoadArena();
            ClearEnemiesAndBolts();
            yield return Behind();
            var path = new List<Vector3>();
            var aim = new Vector3[2];
            foreach (var (yaw, pitch) in new[] { (0f, 14f), (90f, 20f), (180f, 30f), (270f, 16f), (45f, 40f), (315f, 24f) })
            {
                yield return Look(yaw, pitch);
                yield return FireOne(path, aim);
                Vector3 p = aim[0], c = aim[1];
                Assert.Less(Vector3.Angle(p - c, Cam.forward), 0.5f, $"the aim point is under the crosshair (yaw {yaw}, pitch {pitch})");
                float miss = MissAngle(path, p, c);
                Assert.Less(miss, 2, $"bolt passes the crosshair point (yaw {yaw}, pitch {pitch})");
            }
            // At the sky: the bolt flies along the crosshair line toward its far point.
            yield return Look(30, -20);
            yield return FireOne(path, aim);
            Vector3 flight = path[path.Count - 1] - path[0];
            Assert.Less(Vector3.Angle(flight, aim[0] - path[0]), 2, "toward the far point");
        }

        [UnityTest]
        public IEnumerator Crosshair_HitsAnEnemyUnderIt()
        {
            yield return LoadArena();
            ClearEnemiesAndBolts();
            yield return Behind();
            Player.transform.position = Vector3.zero;
            yield return Look(0, 10);
            var grunt = Gm.SpawnEnemyAt(EnemyType.Grunt, Vector3.zero);
            Stats.MaxHealth = Stats.Health = 1e6f;
            // On the crosshair's line, 9 m in front of him, held there while he fires.
            for (float t = 0; t < 1.5f; t += Time.deltaTime)
            {
                Ray ray = Player.cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                Vector3 at = ray.origin + ray.direction * (9 + 4.2f);
                grunt.transform.position = new Vector3(at.x, 0, at.z);
                Press(mouse.leftButton);
                yield return null;
                if (grunt == null || grunt.IsDying) break;
            }
            Release(mouse.leftButton);
            Assert.IsTrue(grunt == null || grunt.IsDying || grunt.Health < GameConfig.Enemy(EnemyType.Grunt).MaxHealth, "bolts hit the grunt under the crosshair");
        }

        [UnityTest]
        public IEnumerator Crosshair_NearCover_BoltsStillGoWhereItPoints()
        {
            yield return LoadJungle(DropStart.Landed);
            ClearEnemiesAndBolts();
            Assert.IsTrue(Follow.IsBehind, "the jungle opens on the behind view");
            // The biggest trunk near the LZ; he stands just right of it, a step behind.
            Vector3 lz = Stage.lz;
            Vector3 trunk = World.Trunks.Where(t => Vector2.Distance(new Vector2(t.x, t.z), new Vector2(lz.x, lz.z)) < 40).OrderByDescending(t => t.y).First();
            Player.transform.position = new Vector3(trunk.x + trunk.y + 0.6f, 0, trunk.z - 2.2f);
            var path = new List<Vector3>();
            var aim = new Vector3[2];
            int onTrunk = 0, past = 0;
            for (float yaw = -60; yaw <= 10; yaw += 5)
            {
                yield return Look(yaw, 15);
                yield return FireOne(path, aim);
                Vector3 p = aim[0];
                bool hitTrunk = new Vector2(p.x - trunk.x, p.z - trunk.z).magnitude < trunk.y + 0.05f;
                if (hitTrunk) onTrunk++;
                else past++;
                Assert.Less(MissAngle(path, p, aim[1]), 2, $"yaw {yaw}: the bolt reaches the crosshair point ({(hitTrunk ? "on the trunk" : "past it")})");
            }
            Debug.Log($"[Camera] near cover: {onTrunk} shots on the trunk, {past} past its edge");
            Assert.Greater(onTrunk, 0, "some shots were on the trunk");
            Assert.Greater(past, 0, "some shots went past its edge");
        }

        [UnityTest]
        public IEnumerator Crosshair_OverAWallTop_BoltsReachTheEnemyBehindIt()
        {
            yield return LoadArena();
            ClearEnemiesAndBolts();
            Stats.MaxHealth = Stats.Health = 1e6f;
            yield return Behind();
            // A wall 10 m wide and 2.4 m tall, z 20.2 to 22.5; he stands 6 m south of it and a
            // grunt waits 5 m north of it.
            var wall = GameConfig.Walls[4];
            float x = (wall.MinX + wall.MaxX) / 2;
            Player.transform.position = new Vector3(x, 0, wall.MinZ - 6);
            var grunt = Gm.SpawnEnemyAt(EnemyType.Grunt, new Vector3(x, 0, wall.MaxZ + 5));
            grunt.enabled = false; // holds still
            Vector3 chest = grunt.transform.position + Vector3.up * 2.5f; // his head, over the wall top
            for (int i = 0; i < 3; i++)
            {
                Vector3 to = chest - Follow.AimRay().origin;
                Follow.SetLook(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg);
                yield return new WaitForSeconds(0.3f);
            }
            var ray = Follow.AimRay();
            Assert.IsTrue(WorldCast.Cast(ray.origin, ray.direction, 60, 0, true, out float hit), "the crosshair finds something");
            Assert.Greater(ray.GetPoint(hit).z, wall.MaxZ, "the crosshair passes over the wall to the grunt");
            float health = grunt.Health;
            Press(mouse.leftButton);
            yield return new WaitForSeconds(1.2f);
            Release(mouse.leftButton);
            Assert.IsTrue(grunt == null || grunt.Health < health || grunt.IsDying, "bolts cleared the wall top and hit the grunt");
        }

        [UnityTest]
        public IEnumerator Crosshair_UsesThisFramesLook()
        {
            yield return LoadArena();
            ClearEnemiesAndBolts();
            yield return Behind();
            yield return Look(0, 10);
            Follow.SetLook(120, 20);
            yield return null; // the Warden aims in Update, before the camera moves this frame
            var ray = Follow.AimRay();
            Assert.Less(Vector3.Angle(ray.direction, Quaternion.Euler(20, 120, 0) * Vector3.forward), 0.01f);
            Vector3 toAim = Player.AimPoint - ray.origin;
            Assert.Less(Vector3.Angle(toAim, ray.direction), 0.5f, "the aim point is on this frame's crosshair line");
            yield return new WaitForSeconds(0.5f);
            Assert.Less(Vector3.Angle(Cam.forward, ray.direction), 0.5f, "and the camera settles on that line");
        }

        [UnityTest]
        public IEnumerator EscAfterALostLock_DoesNotResumeAtOnce()
        {
            yield return LoadArena();
            ClearEnemiesAndBolts();
            yield return Behind();
            Follow.PauseForLostLock();
            Assert.AreEqual(GameManager.Mode.Paused, Gm.CurrentMode);
            yield return null;
            PressAndRelease(keyboard.escapeKey); // the browser passes the same Esc on
            yield return null;
            Assert.AreEqual(GameManager.Mode.Paused, Gm.CurrentMode, "still paused");
            yield return new WaitForSecondsRealtime(CameraFollow.EscGuard + 0.05f);
            PressAndRelease(keyboard.escapeKey);
            yield return null;
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode, "a later Esc resumes");
        }

        // ------------------------------------------------------------------ collision

        [UnityTest]
        public IEnumerator Collision_PullsInAtATrunk_AndNeverGoesIntoScenery()
        {
            yield return LoadJungle(DropStart.Landed);
            ClearEnemiesAndBolts();
            Stats.MaxHealth = Stats.Health = 1e6f;
            Vector3 lz = Stage.lz;
            Vector3 trunk = World.Trunks.Where(t => Vector2.Distance(new Vector2(t.x, t.z), new Vector2(lz.x, lz.z)) < 40).OrderByDescending(t => t.y).First();
            // A trunk right behind his right shoulder: the camera comes in over the shoulder.
            Player.transform.position = new Vector3(trunk.x - GameConfig.ShoulderOffset.x, 0, trunk.z + trunk.y + 1.3f);
            yield return Look(0, GameConfig.BehindPitch, 0.8f);
            float back = Vector3.Distance(Flat(Cam.position), Flat(Player.transform.position));
            Assert.Less(back, 2.5f, "pulled in");
            Assert.Greater(new Vector2(Cam.position.x - trunk.x, Cam.position.z - trunk.z).magnitude, trunk.y + 0.2f, "not inside the trunk");
            // In the open LZ clearing: it eases back out.
            Player.transform.position = lz;
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(Vector3.Distance(Flat(Cam.position), Flat(Player.transform.position)), 3.5f, "back out");

            // Turning all the way round, looking up and down, walking: never under the ground,
            // never inside a trunk or a rock.
            int frames = 0;
            float worst = float.MaxValue;
            void Check()
            {
                Vector3 c = Cam.position;
                float clear = c.y - Stage.Ground(c);
                worst = Mathf.Min(worst, clear);
                Assert.Greater(clear, 0.3f, $"camera under or at the ground at {c}");
                foreach (var t in World.Trunks)
                    Assert.Greater(new Vector2(c.x - t.x, c.z - t.z).magnitude, t.y + 0.1f, $"camera inside a trunk at {c}");
                foreach (var r in World.Rocks)
                    Assert.Greater(Vector3.Distance(c, r), r.w + 0.1f, $"camera inside a rock at {c}");
                frames++;
            }
            Player.transform.position = new Vector3(trunk.x + 1.5f, 0, trunk.z + trunk.y + 1.2f);
            foreach (float pitch in new[] { GameConfig.MinPitch, 10f, GameConfig.MaxPitch })
                for (float yaw = 0; yaw < 360; yaw += 30)
                {
                    Follow.SetLook(yaw, pitch);
                    for (int i = 0; i < 4; i++)
                    {
                        yield return null;
                        Check();
                    }
                }
            Press(keyboard.wKey);
            for (float t = 0; t < 3; t += Time.deltaTime)
            {
                Follow.Look(Time.deltaTime * 120, 0);
                Follow.SetLook(Follow.Yaw, Mathf.Sin(t * 3) * 35);
                yield return null;
                Check();
            }
            Release(keyboard.wKey);
            Debug.Log($"[Camera] {frames} frames checked, lowest lens clearance {worst:F2} m");
        }

        // ------------------------------------------------------------------ the toggle

        [UnityTest]
        public IEnumerator Toggle_V_SwitchesTheView_AndEachStageRemembersIt()
        {
            yield return LoadArena();
            Assert.AreEqual(CameraFollow.View.High, Follow.Current, "the arena opens on the high view");
            Assert.IsFalse(Follow.IsBehind);
            PressAndRelease(keyboard.vKey);
            yield return Frames(2);
            Assert.IsTrue(Follow.IsBehind);
            Assert.AreEqual(1, PlayerPrefs.GetInt("wz.view.arena"));
            Assert.IsTrue(Player.reticle.gameObject.activeSelf);
            Assert.AreEqual(Vector2.zero, Player.reticle.anchoredPosition, "a centre crosshair");

            // The jungle keeps its own choice (behind by default); switch it to high there.
            yield return LoadJungle(DropStart.Landed);
            Assert.IsTrue(Follow.IsBehind);
            PressAndRelease(keyboard.vKey);
            yield return Frames(2);
            Assert.AreEqual(CameraFollow.View.High, Follow.Current);

            yield return LoadArena();
            Assert.IsTrue(Follow.IsBehind, "the arena remembers behind");
            yield return LoadJungle(DropStart.Landed);
            Assert.AreEqual(CameraFollow.View.High, Follow.Current, "the jungle remembers high");
        }

        [UnityTest]
        public IEnumerator Toggle_NotInGreenfang_NorInTheMenu()
        {
            yield return LoadMenu();
            PressAndRelease(keyboard.vKey);
            yield return Frames(2);
            Assert.IsFalse(Follow.IsBehind, "not on the menu");
            Assert.IsFalse(PlayerPrefs.HasKey("wz.view.arena"));
            SceneManager.LoadScene("Greenfang");
            yield return Frames(3);
            PressAndRelease(keyboard.vKey);
            yield return Frames(2);
            Assert.AreEqual(CameraFollow.View.High, Follow.Current, "Greenfang's strikes are aimed from above");
        }

        [UnityTest]
        public IEnumerator Landing_SettlesBehindHim_ThenPlays()
        {
            yield return LoadJungle(DropStart.Jump);
            Stage.JumpNow();
            Time.timeScale = 8;
            float end = Time.realtimeSinceStartup + 60;
            while (Stage.CurrentPhase != JungleStage.Phase.Walk)
            {
                if (Stage.Dive.Current == Skydive.State.Freefall && Stage.Dive.Altitude < 250) Stage.Dive.Deploy();
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for the walk");
                yield return null;
            }
            Time.timeScale = 1;
            yield return Frames(3);
            Assert.IsTrue(Follow.IsBehind);
            // Looking along his landing heading (if something stands right behind him the camera
            // comes in over his shoulder, so it isn't always looking past him).
            Vector3 heading = Quaternion.Euler(0, Stage.Dive.Heading, 0) * Vector3.forward;
            Assert.Less(Vector3.Angle(Flat(Cam.forward), heading), 5, "looking along his heading");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(Follow.Yaw, Stage.Dive.Heading)), 2, "along his landing heading");
        }

        [UnityTest]
        public IEnumerator PointerLockLost_Pauses()
        {
            yield return LoadArena();
            ClearEnemiesAndBolts();
            yield return Behind();
            yield return Frames(2);
            if (!CameraFollow.PointerLocked) Assert.Ignore("No pointer lock in this editor session");
            Cursor.lockState = CursorLockMode.None; // what Esc does in the browser
            yield return Frames(2);
            Assert.AreEqual(GameManager.Mode.Paused, Gm.CurrentMode);
            Assert.IsTrue(Gm.menus.pausePanel.activeSelf);
        }

        // ------------------------------------------------------------------ the rest stance

        [UnityTest]
        public IEnumerator Rest_StandsUpright_BothHandsOnTheRifle_ShoulderedOnlyFiring()
        {
            yield return LoadArena();
            ClearEnemiesAndBolts();
            var probe = View.gameObject.AddComponent<StanceProbe>();
            probe.anim = View.animator;
            probe.ik = View.handIK;
            probe.view = View;
            yield return new WaitForSeconds(1);
            probe.measuring = true;
            float lean = 0, hipsLow = 99, kneeStraight = 0, kneeBent = 0;
            var a = View.animator;
            for (float t = 0; t < 2; t += Time.deltaTime)
            {
                yield return null;
                float s = a.transform.lossyScale.y;
                Vector3 lf = a.GetBoneTransform(HumanBodyBones.LeftFoot).position, rf = a.GetBoneTransform(HumanBodyBones.RightFoot).position;
                Vector3 hips = a.GetBoneTransform(HumanBodyBones.Hips).position, head = a.GetBoneTransform(HumanBodyBones.Head).position;
                lean = Mathf.Max(lean, Vector3.Angle(head - hips, Vector3.up));
                hipsLow = Mathf.Min(hipsLow, (hips.y - Mathf.Min(lf.y, rf.y)) / s);
                float l = Knee(a, true), r = Knee(a, false);
                kneeStraight = Mathf.Max(kneeStraight, Mathf.Min(l, r));
                kneeBent = Mathf.Max(kneeBent, Mathf.Max(l, r));
            }
            probe.measuring = false;
            Debug.Log($"[Stance] rest: lean {lean:F1} deg, hips {hipsLow:F3} m (model), knees {kneeStraight:F0}/{kneeBent:F0} deg, hands {probe.left:F3}/{probe.right:F3} m");
            Assert.Less(lean, 10, "upright, not hunched over the rifle");
            Assert.Greater(hipsLow, 0.93f, "pelvis at standing height (0.99 m on this rig), not crouched");
            Assert.Less(kneeStraight, 15, "standing on a straight leg");
            Assert.Less(kneeBent, 45, "the other knee only relaxed");
            Assert.Less(probe.left, 0.02f, "left hand on the foregrip at rest");
            Assert.Less(probe.right, 0.02f, "right hand on the pistol grip at rest");
            Assert.AreEqual(0, a.GetLayerWeight(WardenModelView.UpperLayer), 0.01f, "the combat lean is off at rest");

            Press(mouse.leftButton);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, a.GetLayerWeight(WardenModelView.UpperLayer), 0.01f, "leans into the gun while firing");
            Assert.AreEqual(1, View.handIK.Shouldered, 0.01f);
            Release(mouse.leftButton);
            yield return new WaitForSeconds(0.9f);
            Assert.AreEqual(0, a.GetLayerWeight(WardenModelView.UpperLayer), 0.01f, "and back to rest");
        }

        static float Knee(Animator a, bool left)
        {
            Vector3 hip = a.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg).position;
            Vector3 knee = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg).position;
            Vector3 ankle = a.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot).position;
            return 180 - Vector3.Angle(hip - knee, ankle - knee);
        }
    }

    // Touch in the behind view: left thumb moves, right thumb looks, FIRE fires, VIEW switches.
    public class CameraTouchTests : ArenaTestBase
    {
        Touchscreen screen;

        public override void Setup()
        {
            base.Setup();
            screen = InputSystem.AddDevice<Touchscreen>();
            TouchControls.ForceTouch = true;
        }

        static CameraFollow Follow => GameManager.Instance.cameraFollow;

        static Vector2 Centre(RectTransform rt) => rt.TransformPoint(rt.rect.center);

        [UnityTest]
        public IEnumerator Touch_RightThumbLooks_LeftThumbMoves_FireHolds_ViewSwitches()
        {
            yield return LoadArena();
            Gm.ClearQueue();
            Stats.MaxHealth = Stats.Health = 1e6f;
            var touch = Gm.hud.touch;
            Assert.IsTrue(touch.viewButton.gameObject.activeSelf, "VIEW shows on touch");
            Assert.IsFalse(touch.fireButton.gameObject.activeSelf, "FIRE only in the behind view");
            touch.PressView();
            yield return Frames(2);
            Assert.IsTrue(Follow.IsBehind);
            Assert.IsTrue(touch.fireButton.gameObject.activeSelf);
            Follow.SetLook(0, 8);

            // Right half: a drag turns the camera; no stick.
            Vector2 right = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f);
            BeginTouch(1, right);
            yield return null;
            for (int i = 1; i <= 10; i++)
            {
                MoveTouch(1, right + new Vector2(i * 8, 0));
                yield return null;
            }
            Assert.Greater(Mathf.DeltaAngle(0, Follow.Yaw), 5, "dragging right turns right");
            Assert.AreEqual(Vector2.zero, touch.Stick);
            EndTouch(1, right + new Vector2(80, 0));
            yield return Frames(2);

            // Left half: the stick, camera-relative.
            float yaw = Follow.Yaw;
            Vector3 start = Player.transform.position;
            Vector2 left = new Vector2(Screen.width * 0.2f, Screen.height * 0.4f);
            BeginTouch(2, left);
            yield return null;
            MoveTouch(2, left + new Vector2(0, 80));
            yield return new WaitForSeconds(0.4f);
            Vector3 moved = Player.transform.position - start;
            Vector3 ahead = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            Assert.Greater(Vector3.Dot(moved, ahead), 1, "pushing up runs where the camera looks");
            EndTouch(2, left + new Vector2(0, 80));
            yield return Frames(2);

            // FIRE, held.
            foreach (var b in Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None)) b.Release();
            Vector2 fire = Centre(touch.fireButton);
            BeginTouch(3, fire);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(touch.FireHeld);
            Assert.GreaterOrEqual(Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None).Length, 2, "fires while held");
            EndTouch(3, fire);
            yield return Frames(2);
            Assert.IsFalse(touch.FireHeld);

            // A drag that starts on FIRE fires and turns the camera at once.
            foreach (var b in Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None)) b.Release();
            float before = Follow.Yaw;
            BeginTouch(5, fire);
            yield return null;
            for (int i = 1; i <= 10; i++)
            {
                MoveTouch(5, fire + new Vector2(-i * 8, 0));
                yield return null;
            }
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(touch.FireHeld, "still firing");
            Assert.Less(Mathf.DeltaAngle(before, Follow.Yaw), -5, "dragging left off FIRE turns left");
            Assert.GreaterOrEqual(Object.FindObjectsByType<Bolt>(FindObjectsSortMode.None).Length, 1);
            EndTouch(5, fire + new Vector2(-80, 0));
            yield return Frames(2);

            // VIEW back to the high view: touch aims and fires by itself again.
            // Queued, so the press edge lands in the frame's own input update (as on a device).
            BeginTouch(4, Centre(touch.viewButton), queueEventOnly: true);
            yield return Frames(2);
            EndTouch(4, Centre(touch.viewButton), queueEventOnly: true);
            yield return Frames(2);
            Assert.IsFalse(Follow.IsBehind);
            Assert.IsFalse(touch.fireButton.gameObject.activeSelf);
        }
    }
}
