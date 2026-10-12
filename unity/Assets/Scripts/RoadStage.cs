using System.Collections;
using UnityEngine;

namespace WardenZero
{
    // The campaign after checkpoint A (M3), ticked by JungleStage once A is secured:
    // TAKE THE JEEP (parked at A's motor pool) -> DRIVE TO CHECKPOINT B -> HOLD CHECKPOINT B (its
    // ring counts on foot or in a stopped jeep) -> saved -> a short beat -> the swap (a fade:
    // the jeep at B's gate, the squadmate at the wheel, the Warden on the gun, the gate open)
    // -> the gun ride to the roadblock, CLEAR THE ROAD -> "MILESTONE 3 COMPLETE". On foot the
    // road is always walkable to B, and the jeep waits where he left it (B brings it to its
    // gate). Continue at B (and at the end) replays the ride. No pausing during the swap fade.
    public class RoadStage : MonoBehaviour
    {
        public enum Phase { Idle, TakeJeep, Drive, HoldB, SecuredB, Swap, Ride, Done }

        public const float SwapBeat = 2.5f; // after B is secured (or Continue at B)
        public const float FadeTime = 0.45f;

        public const float CaptureTime = 4;
        public const float StoppedBelow = 1; // a jeep in B's ring counts once it is this slow

        public VehicleController jeep;
        public Transform beaconB;
        public Renderer ringB;
        public Light lightB;
        public Transform gate; // B's barricade across the ride road; pushed aside for the ride
        public Transform squadmate; // at the wheel for the ride (the greybox stand-in until the clone)
        public Destructible[] targets;

        public TurretRide Ride { get; } = new TurretRide();
        public bool Fading { get; private set; }
        public int TargetsDown => System.Array.FindAll(targets, t => t.Down).Length;

        public Phase CurrentPhase { get; private set; }
        public float Capture { get; private set; }

        JungleStage stage;
        float phaseTime;

        GameManager Gm => GameManager.Instance;
        PlayerController Player => Gm.player;
        Vector3 B => beaconB.position;

        void Awake()
        {
            stage = GetComponent<JungleStage>();
        }

        Vector3 gateHome;

        // The jeep waits at A's motor pool from the start of the jungle; B's gate is shut; the
        // targets stand (plates lie flat) and are the registry's.
        public void Setup()
        {
            jeep.Park(RoadLayout.ParkingA);
            jeep.Ride = null;
            jeep.Vehicle.Road = RoadLayout.Drive;
            CurrentPhase = Phase.Idle;
            ringB.gameObject.SetActive(true);
            lightB.color = GameConfig.Gold;
            if (gateHome == Vector3.zero) gateHome = gate.position;
            gate.position = gateHome;
            World.Blocks[stage.GateSlot] = RoadLayout.GateBlock(true);
            stage.rideOpen = false;
            squadmate.gameObject.SetActive(false);
            Destructible.All.Clear();
            foreach (var t in targets)
            {
                t.ResetState();
                Destructible.All.Add(t);
            }
        }

        // A is secured (or Continue at A): on to the jeep.
        public void Begin()
        {
            Capture = 0;
            Set(Player.Seated ? Phase.Drive : Phase.TakeJeep);
        }

        // A start in the driver's seat (debug ?campaign=drive): straight in, no step.
        public void SeatDriver()
        {
            jeep.Enter(Player, VehicleController.Seat.Driver, true);
            Set(Phase.Drive);
            Gm.cameraFollow.Snap();
        }

        // Continue at B (and ?campaign=checkpointb): the jeep at B's gate, B already taken; the
        // ride follows after the beat.
        public void BeginAtB()
        {
            jeep.Park(RoadLayout.ParkingB);
            Set(Phase.SecuredB);
            stage.hud.SetObjective("CHECKPOINT B SECURED");
            stage.hud.PointAt(null, null);
            lightB.color = GameConfig.Hex(0x9bff67);
            ringB.gameObject.SetActive(false);
        }

        void Set(Phase p)
        {
            CurrentPhase = p;
            phaseTime = 0;
        }

        public void Tick(float dt)
        {
            phaseTime += dt;
            var hud = stage.hud;
            switch (CurrentPhase)
            {
                case Phase.TakeJeep:
                    hud.SetObjective("TAKE THE JEEP");
                    hud.PointAt(jeep.transform.position + Vector3.up * 2, Player.cam);
                    if (Player.Seated) Set(Phase.Drive);
                    break;
                case Phase.Drive:
                case Phase.HoldB:
                    TickToB(dt);
                    break;
                case Phase.SecuredB:
                    if (phaseTime >= SwapBeat && !Fading) StartCoroutine(Swap());
                    break;
                case Phase.Ride:
                    TickRide();
                    break;
            }
            if (ringB.gameObject.activeSelf) ringB.transform.Rotate(0, 0, dt * 14, Space.Self);
        }

        // Drive (or walk) to B and hold its ring.
        void TickToB(float dt)
        {
            var hud = stage.hud;
            Vector3 d = Player.transform.position - B;
            d.y = 0;
            bool inRing = d.magnitude < RoadLayout.CaptureRadiusB && (!Player.Seated || jeep.Vehicle.Speed < StoppedBelow);
            Capture = inRing ? Capture + dt : Mathf.Max(0, Capture - dt * 1.5f);
            hud.SetCapture(Capture > 0 || inRing ? Capture / CaptureTime : (float?)null);
            if (CurrentPhase == Phase.Drive && inRing) Set(Phase.HoldB);
            if (CurrentPhase == Phase.HoldB && !inRing && Capture <= 0) Set(Phase.Drive);
            hud.SetObjective(CurrentPhase == Phase.HoldB ? "HOLD CHECKPOINT B" : "DRIVE TO CHECKPOINT B");
            hud.PointAt(B + Vector3.up * 2, Player.cam);
            if (Capture >= CaptureTime) SecureB();
        }

        // ?campaign=ride: straight onto the gun.
        public void BeginRide()
        {
            BeginAtB();
            phaseTime = SwapBeat;
        }

        // Behind a fade (in play: the pointer lock and the HUD stay; no pausing meanwhile): the
        // jeep to B's gate, out of the driver's seat into the bed, the squadmate at the wheel,
        // the gate open, the ride road inside the bounds; then a cut to the gun.
        IEnumerator Swap()
        {
            Fading = true;
            Set(Phase.Swap);
            StageLoader.FadeOut(1 / FadeTime);
            while (StageLoader.Fade < 0.99f) yield return null;
            if (Player.Seated) jeep.Exit();
            jeep.Park(RoadLayout.ParkingB);
            jeep.Vehicle.Road = RoadLayout.Ride;
            stage.rideOpen = true;
            World.Blocks[stage.GateSlot] = RoadLayout.GateBlock(false);
            gate.position = gateHome + gate.right * (RoadLayout.GateHalf + 4);
            squadmate.gameObject.SetActive(true);
            squadmate.SetParent(jeep.rig.driver, false);
            squadmate.localPosition = Vector3.down * VehicleController.HipHeight;
            squadmate.localRotation = Quaternion.identity;
            jeep.Turret.Reset();
            Ride.Reset();
            jeep.Ride = Ride;
            jeep.Enter(Player, VehicleController.Seat.Gunner, true);
            Gm.cameraFollow.Snap();
            stage.hud.SetObjective("CLEAR THE ROAD");
            Gm.hud.Banner("CLEAR THE ROAD", 2, GameConfig.Gold);
            Set(Phase.Ride);
            StageLoader.FadeIn(1 / FadeTime);
            while (StageLoader.Fade > 0.01f) yield return null;
            Fading = false;
        }

        // Plates rise as the jeep comes up; the ride ends at the roadblock once its plates are
        // down (or after a while there).
        void TickRide()
        {
            var hud = stage.hud;
            foreach (var t in targets)
                if (t.popAt >= 0 && !t.Up && Ride.S >= t.popAt) t.Pop();
            hud.SetObjective(RideObjective(TargetsDown, targets.Length));
            hud.PointAt(null, null);
            if (!Ride.AtEnd) return;
            bool blockCleared = System.Array.TrueForAll(targets, t => !t.roadblock || t.Down);
            if (blockCleared || Ride.SinceEnd >= RoadLayout.EndWait)
            {
                Set(Phase.Done);
                hud.SetObjective("THE ROAD IS CLEAR");
                Gm.MilestoneComplete(3);
            }
        }

        // "CLEAR THE ROAD · TARGETS n/m", built once per count.
        string objective;
        int objectiveDown = -1;

        string RideObjective(int down, int total)
        {
            if (down != objectiveDown)
            {
                objectiveDown = down;
                objective = $"CLEAR THE ROAD  ·  TARGETS {down}/{total}";
            }
            return objective;
        }

        void SecureB()
        {
            Campaign.Save(Gm.Run, CampaignSave.CheckpointB);
            Set(Phase.SecuredB);
            var hud = stage.hud;
            hud.SetCapture(null);
            hud.PointAt(null, null);
            hud.SetObjective("CHECKPOINT B SECURED");
            Gm.hud.Banner("CHECKPOINT B SECURED", 2.4f, GameConfig.Hex(0x9bff67));
            Gm.PlaySound(stage.alert, 0.7f);
            Effects.Instance.Blast(B, 3, GameConfig.Hex(0x9bff67), 0.2f);
            lightB.color = GameConfig.Hex(0x9bff67);
            ringB.gameObject.SetActive(false);
        }
    }
}
