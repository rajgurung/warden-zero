using UnityEngine;

namespace WardenZero
{
    // The campaign after checkpoint A (M3), ticked by JungleStage once A is secured:
    // TAKE THE JEEP (parked at A's motor pool) -> DRIVE TO CHECKPOINT B -> HOLD CHECKPOINT B (its
    // ring counts on foot or in a stopped jeep) -> saved. On foot the road is always walkable
    // to B, and the jeep waits where he left it.
    public class RoadStage : MonoBehaviour
    {
        public enum Phase { Idle, TakeJeep, Drive, HoldB, SecuredB }

        public const float CaptureTime = 4;
        public const float StoppedBelow = 1; // a jeep in B's ring counts once it is this slow

        public VehicleController jeep;
        public Transform beaconB;
        public Renderer ringB;
        public Light lightB;

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

        // The jeep waits at A's motor pool from the start of the jungle.
        public void Setup()
        {
            jeep.Park(RoadLayout.ParkingA);
            CurrentPhase = Phase.Idle;
            ringB.gameObject.SetActive(true);
            lightB.color = GameConfig.Gold;
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

        // Continue at B: the jeep at B's gate, B already taken.
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
