using System.Linq;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using J = WardenZero.GameConfig.Jeep;

namespace WardenZero.Tests
{
    // The jeep in the real jungle: getting in and out, the reset, and the drive into B.
    public class VehiclePlayTests : JungleTestBase
    {
        string savedPrefs;

        public override void Setup()
        {
            base.Setup();
            savedPrefs = PlayerPrefs.GetString(CampaignSave.Key, null);
            CampaignSave.Clear();
        }

        public override void TearDown()
        {
            if (string.IsNullOrEmpty(savedPrefs)) PlayerPrefs.DeleteKey(CampaignSave.Key);
            else PlayerPrefs.SetString(CampaignSave.Key, savedPrefs);
            base.TearDown();
        }

        static VehicleController Jeep => Stage.Road.jeep;

        // At checkpoint A (secured), standing at the jeep's driver door.
        IEnumerator AtTheDoor()
        {
            yield return LoadJungle(DropStart.CheckpointA);
            Assert.AreEqual(RoadStage.Phase.TakeJeep, Stage.Road.CurrentPhase);
            var door = Jeep.rig.doorL.position;
            door.y = World.HeightAt(door);
            Player.transform.position = door;
            yield return Frames(2);
        }

        IEnumerator GetIn()
        {
            yield return AtTheDoor();
            PressAndRelease(keyboard.spaceKey);
            yield return new WaitForSeconds(VehicleController.EnterTime + 0.2f);
            Assert.IsTrue(Player.Seated, "seated");
        }

        [UnityTest]
        public IEnumerator SpaceAtTheDoor_SeatsHim_RifleSlung_NoGroundMarks()
        {
            yield return AtTheDoor();
            Assert.IsTrue(Jeep.CanEnter(Player.transform.position));
            Assert.AreEqual(PlayerController.DrivePrompt, Gm.hud.Prompt, "the prompt says DRIVE");
            float y = Player.transform.position.y;
            PressAndRelease(keyboard.spaceKey);
            yield return null;
            Assert.IsTrue(Player.Seated);
            Assert.AreEqual(TraversalState.Grounded, Player.Traversal.State, "the press did not jump");
            yield return new WaitForSeconds(VehicleController.EnterTime + 0.2f);
            Assert.IsTrue(View.Slung, "rifle slung");
            Assert.IsFalse(Stage.groundMarks[0].activeSelf, "no ground ring in the jeep");
            Assert.Less(Vector3.Distance(Player.transform.position, Jeep.RiderPosition()), 0.01f, "in the seat");
            Assert.AreEqual(RoadStage.Phase.Drive, Stage.Road.CurrentPhase);
            Assert.AreEqual(VehicleController.ExitAndReset, Gm.hud.Prompt, "stopped: C gets out, R resets");
        }

        [UnityTest]
        public IEnumerator C_AtSpeed_DoesNothing_Stopped_GetsOutOnTheFreeSide()
        {
            yield return GetIn();
            Press(keyboard.wKey);
            yield return WaitFor(() => Jeep.Vehicle.Speed > J.ExitSpeed + 1, 5, "speed");
            PressAndRelease(keyboard.cKey);
            yield return null;
            Assert.IsTrue(Player.Seated, "no getting out at speed");
            Release(keyboard.wKey);
            Press(keyboard.sKey);
            yield return WaitFor(() => Jeep.Vehicle.Speed < 0.3f, 6, "the stop");
            Release(keyboard.sKey);
            yield return Frames(2);
            PressAndRelease(keyboard.cKey);
            yield return null;
            Assert.IsFalse(Player.Seated, "out");
            var p = Player.transform.position;
            Assert.Less(Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(Jeep.rig.doorL.position.x, 0, Jeep.rig.doorL.position.z)), 0.1f, "at the driver's door");
            yield return Frames(3);
            Assert.IsTrue(Stage.groundMarks[0].activeSelf, "ground ring back");
            // Blocked on the driver's side: out of the other door.
            yield return GetBackIn();
            var left = Jeep.rig.doorL.position;
            World.Blocks = AddWall(World.Blocks, left);
            PressAndRelease(keyboard.cKey);
            yield return null;
            Assert.IsFalse(Player.Seated);
            p = Player.transform.position;
            Assert.Less(Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(Jeep.rig.doorR.position.x, 0, Jeep.rig.doorR.position.z)), 0.1f, "at the other door");
        }

        [UnityTest]
        public IEnumerator C_HemmedIn_ResetsOntoTheRoad_AndGetsOut()
        {
            yield return GetIn();
            yield return WaitFor(() => !Jeep.Entering, 2, "seated");
            // Off the road beside it, every way out walled.
            var at = RoadLayout.Drive.At(60);
            var side = new Vector3(at.Tangent.y, 0, -at.Tangent.x) * 9;
            Jeep.Park(new Pose(new Vector3(at.Position.x, 0, at.Position.y) + side, Quaternion.Euler(0, Mathf.Atan2(at.Tangent.x, at.Tangent.y) * Mathf.Rad2Deg, 0)));
            Player.transform.position = Jeep.SeatTransform.position;
            yield return Frames(2);
            var blocks = World.Blocks;
            var behind = Jeep.transform.position - Jeep.Vehicle.Forward * (J.FootprintSpacing + J.FootprintRadius + 1.2f);
            foreach (var w in new[] { Jeep.rig.doorL.position, Jeep.rig.doorR.position, behind }) blocks = AddWall(blocks, w);
            World.Blocks = blocks;
            var parked = Jeep.transform.position;
            PressAndRelease(keyboard.cKey);
            yield return null;
            Assert.IsTrue(Jeep.Fading, "no way out: the reset fade");
            yield return WaitFor(() => !Jeep.Fading, 3, "the fade");
            Assert.IsFalse(Player.Seated, "out, on the same press");
            Assert.Greater(Vector3.Distance(Jeep.transform.position, parked), 5, "back on the road");
        }

        IEnumerator GetBackIn()
        {
            var door = Jeep.rig.doorL.position;
            door.y = World.HeightAt(door);
            Player.transform.position = door;
            yield return Frames(2);
            PressAndRelease(keyboard.spaceKey);
            yield return new WaitForSeconds(VehicleController.EnterTime + 0.2f);
            Assert.IsTrue(Player.Seated);
        }

        static Block[] AddWall(Block[] blocks, Vector3 at)
        {
            var list = new System.Collections.Generic.List<Block>(blocks);
            list.Add(new Block { Center = new Vector2(at.x, at.z), Half = new Vector2(0.6f, 0.6f), Bottom = at.y - 2, Top = at.y + 4 });
            return list.ToArray();
        }

        [UnityTest]
        public IEnumerator R_ResetsOntoTheRoad_AndNoPauseInTheFade()
        {
            yield return GetIn();
            // Off into the forest, nose into the bank.
            var a = RoadLayout.Drive.At(200);
            var right = new Vector3(a.Tangent.y, 0, -a.Tangent.x);
            Jeep.Vehicle.Place(new Vector3(a.Position.x, 0, a.Position.y) + right * 13, 90);
            yield return Frames(2);
            Assert.Greater(Jeep.Vehicle.RoadDistance, 10);
            PressAndRelease(keyboard.rKey);
            yield return null;
            Assert.IsTrue(Jeep.Fading);
            Gm.Pause(true);
            Assert.AreNotEqual(GameManager.Mode.Paused, Gm.CurrentMode, "no pause during the fade");
            yield return WaitFor(() => !Jeep.Fading, 3, "the reset");
            Assert.Less(Jeep.Vehicle.RoadDistance, 0.3f, "on the centreline");
            Assert.AreEqual(0, Jeep.Vehicle.Speed);
            Assert.IsTrue(Player.Seated);
            Assert.Less(Vector3.Distance(Player.transform.position, Jeep.RiderPosition()), 0.01f, "still in the seat");
        }

        [UnityTest]
        public IEnumerator Bot_DrivesTheLast120m_IntoB_AndBSaves()
        {
            yield return GetIn();
            var road = RoadLayout.Drive;
            var a = road.At(road.Length - 120);
            Jeep.Vehicle.Place(new Vector3(a.Position.x, 0, a.Position.y), Mathf.Atan2(a.Tangent.x, a.Tangent.y) * Mathf.Rad2Deg);
            Jeep.Autopilot = v => DriveBot.Input(v, road);
            Time.timeScale = 3;
            yield return WaitFor(() => Stage.Road.CurrentPhase == RoadStage.Phase.SecuredB, 40, "checkpoint B", () => $"phase {Stage.Road.CurrentPhase} capture {Stage.Road.Capture:0.0} at {Jeep.Vehicle.Position} speed {Jeep.Vehicle.Speed:0.0}");
            Time.timeScale = 1;
            var save = CampaignSave.Load();
            Assert.IsNotNull(save);
            Assert.AreEqual(CampaignSave.CheckpointB, save.Place);
            Assert.IsTrue(Player.Seated, "held from the stopped jeep");
        }

        [UnityTest]
        public IEnumerator Continue_AtB_BesideTheJeep_WithTheRun()
        {
            var run = new RunState { Level = 6, Score = 777 };
            Upgrades.Apply(run, "max_health");
            run.Stats.Health = 70;
            Campaign.Save(run, CampaignSave.CheckpointB);
            yield return LoadMenu();
            Gm.menus.ShowMenu();
            Assert.IsTrue(Gm.menus.continueButton.interactable);
            Gm.menus.continueButton.onClick.Invoke();
            yield return WaitFor(() => Stage != null && GameManager.Instance.jungle == Stage, 60, "the jungle scene");
            yield return Frames(3);
            Assert.AreEqual(GameManager.Mode.Play, Gm.CurrentMode);
            Assert.AreEqual(6, Gm.Run.Level);
            Assert.AreEqual(777, Gm.Run.Score);
            Assert.AreEqual(70, Stats.Health);
            Assert.AreEqual(120, Stats.MaxHealth);
            var b = RoadLayout.CheckpointB;
            Assert.Less(Vector3.Distance(Player.transform.position, b), 10, "at B");
            Assert.Less(Vector3.Distance(Jeep.transform.position, RoadLayout.ParkingB.position), 0.5f, "the jeep at B's gate");
            Assert.AreEqual(RoadStage.Phase.SecuredB, Stage.Road.CurrentPhase);
            Assert.IsTrue(Gm.hud.BannerShowing("CHECKPOINT B"), "resumed at B, not A");
        }

        [UnityTest]
        public IEnumerator Art_LoadsByAddress_ElseTheGreyboxShows()
        {
            yield return LoadJungle(DropStart.CheckpointA);
            var art = Stage.art;
            yield return WaitFor(() => art.Dressed || art.Failed, 10, "the vehicle art");
            Assert.IsTrue(art.Dressed);
            Assert.IsFalse(Jeep.rig.body.Find("Greybox").gameObject.activeSelf, "the art, not the greybox");
            Assert.IsNotNull(Jeep.rig.body.Find("Body(Clone)"), "the Tripo body on the jeep");
            // A load that fails falls back to the greybox at once.
            VehicleArtLoader.LoadAddress = "NoSuchArt";
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return LoadJungle(DropStart.CheckpointA);
                yield return WaitFor(() => Stage.art.Dressed || Stage.art.Failed, 10, "the failed load");
                Assert.IsTrue(Stage.art.Failed);
                Assert.IsTrue(Jeep.rig.body.Find("Greybox").gameObject.activeSelf, "greybox instead");
            }
            finally
            {
                VehicleArtLoader.LoadAddress = VehicleArtLoader.Address;
            }
        }

        [UnityTest]
        public IEnumerator Art_Stalled_GreyboxBeforeTheSwap_ThenTheArtWhenItComes()
        {
            VehicleArtLoader.Stall = true;
            try
            {
                yield return LoadJungle(DropStart.CheckpointB);
                var art = Stage.art;
                Assert.IsTrue(art.Loading);
                Assert.IsFalse(Jeep.rig.body.Find("Greybox").gameObject.activeSelf, "hidden at first");
                yield return WaitFor(() => art.GreyboxShown, VehicleArtLoader.WantedDeadline + 0.5f, "the greybox");
                Assert.AreEqual(RoadStage.Phase.SecuredB, Stage.Road.CurrentPhase, "before the swap");
                Assert.IsTrue(Jeep.rig.body.Find("Greybox").gameObject.activeSelf, "the jeep is seen");
                Assert.IsTrue(Stage.Road.targets.All(t => t.transform.Find("Greybox") == null || t.transform.Find("Greybox").gameObject.activeSelf), "and the targets");
                Assert.IsFalse(art.Dressed);
                VehicleArtLoader.Stall = false;
                yield return WaitFor(() => art.Dressed, 10, "the late art");
                Assert.IsFalse(art.GreyboxShown);
                Assert.IsFalse(Jeep.rig.body.Find("Greybox").gameObject.activeSelf, "greybox hidden again");
                Assert.IsNotNull(Jeep.rig.body.Find("Body(Clone)"), "the Tripo body on the jeep");
            }
            finally
            {
                VehicleArtLoader.Stall = false;
            }
        }
    }
}
