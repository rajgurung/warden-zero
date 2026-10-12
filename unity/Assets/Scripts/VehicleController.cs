using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using J = WardenZero.GameConfig.Jeep;

namespace WardenZero
{
    // The jeep in the jungle: ticks its Vehicle from the driver's input, poses the rig, and
    // keeps its box in World.Blocks (a wall to him on foot, and to the camera). He gets in at a
    // door (Space / DRIVE near it: a 0.6 s move into the seat, rifle slung) and out with C below
    // ExitSpeed (to the driver's side, else the other, else behind; if every way out is blocked
    // it resets first). R resets onto the road below ResetSpeed or whenever RESET is offered;
    // the worst stuck states reset by themselves. Resets fade to black for a moment (no
    // pausing then) and cut. While he is seated this owns the keys and buttons; PlayerController
    // only sits him in the seat.
    [DefaultExecutionOrder(-5)] // after the look input, before the Warden is put in his seat
    public class VehicleController : MonoBehaviour
    {
        public enum Seat { None, Driver, Gunner }

        public VehicleRig rig;
        public JungleStage stage;

        public const float EnterTime = 0.6f, DoorReach = 2.5f;
        // His feet below the driver's cushion: the hips on it (VehiclePose bends the legs).
        public const float HipHeight = 1.25f;
        public const float FadeTime = 0.3f;

        public static VehicleController Active { get; private set; }

        public Vehicle Vehicle { get; } = new Vehicle();
        public PlayerController Rider { get; private set; }
        public Seat Occupied { get; private set; }
        public bool Entering => entering < EnterTime;
        public bool Fading { get; private set; }
        // Shown on RESET: offered by a stuck rule, or (touch) after a while below ResetSpeed.
        public bool ResetAvailable => Vehicle.ResetOffered || Vehicle.Speed < J.ResetSpeed && (!TouchControls.Active || Vehicle.SlowTime >= J.TouchResetAfter);
        public bool CanExit => Vehicle.Speed < J.ExitSpeed && !Entering && !Fading;
        // Test hook: drives instead of the keys.
        public System.Func<Vehicle, VehicleInput> Autopilot;

        float entering = EnterTime;
        Vector3 enterFrom;

        void Awake()
        {
            Active = this;
            Vehicle.Ground = World.HeightAt;
            Vehicle.Road = RoadLayout.Drive;
            Vehicle.Clamp = (p, r) => RoadLayout.Clamp(p, r, RoadLayout.CorridorHalf, stage != null && stage.rideOpen);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        // At rest at a parking spot.
        public void Park(Pose spot)
        {
            Vehicle.Place(spot.position, spot.rotation.eulerAngles.y);
            rig.Snap(Vehicle);
            UpdateBlock();
        }

        public Transform SeatTransform => Occupied == Seat.Gunner ? rig.gunner : rig.driver;

        // Within reach of a door, nobody in it, not moving.
        public bool CanEnter(Vector3 p)
        {
            if (Occupied != Seat.None || Fading || Vehicle.Speed > 0.5f) return false;
            return Flat(p - rig.doorL.position).magnitude <= DoorReach || Flat(p - rig.doorR.position).magnitude <= DoorReach;
        }

        public void Enter(PlayerController player, Seat seat = Seat.Driver, bool instantly = false)
        {
            Rider = player;
            Occupied = seat;
            entering = instantly ? EnterTime : 0;
            enterFrom = player.transform.position;
            player.SeatIn(this);
        }

        // Out on the free side: the driver's door, the other door, then behind. Returns false
        // when every way out is blocked (then he stays in).
        public bool Exit()
        {
            if (Rider == null) return false;
            Vector3? spot = FreeSpot(rig.doorL.position) ?? FreeSpot(rig.doorR.position) ?? FreeSpot(transform.position - Vehicle.Forward * (J.FootprintSpacing + J.FootprintRadius + 1.2f));
            if (spot == null) return false;
            var player = Rider;
            Rider = null;
            Occupied = Seat.None;
            player.LeaveSeat(spot.Value);
            return true;
        }

        // Where he could stand at p: on the ground, inside the bounds, clear of trunks and blocks
        // (the jeep's own too), and not far above or below the jeep.
        Vector3? FreeSpot(Vector3 p)
        {
            float r = GameConfig.PlayerRadius;
            p.y = World.HeightAt(p);
            if (Mathf.Abs(p.y - Vehicle.Position.y) > 1.5f) return null;
            if (GameConfig.ResolveCircle(p, r) != p) return null;
            if ((World.PushOutOfTrunks(p, r) - p).sqrMagnitude > 1e-6f) return null;
            if ((World.PushOutOfBlocks(p, r, p.y) - p).sqrMagnitude > 1e-6f) return null;
            return p;
        }

        // Reset onto the road behind a short fade, then a camera cut.
        public void Reset()
        {
            if (!Fading) StartCoroutine(ResetRoutine());
        }

        IEnumerator ResetRoutine()
        {
            Fading = true;
            StageLoader.FadeOut(1 / FadeTime);
            while (StageLoader.Fade < 0.99f) yield return null;
            Vehicle.ResetToRoad();
            rig.Snap(Vehicle);
            UpdateBlock();
            if (Rider != null) Rider.transform.position = RiderPosition();
            GameManager.Instance.cameraFollow.Snap();
            StageLoader.FadeIn(1 / FadeTime);
            while (StageLoader.Fade > 0.01f) yield return null;
            Fading = false;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;
            float dt = Time.deltaTime;
            if (Entering) entering += dt;
            if (Occupied == Seat.Driver && !Entering && !Fading) Drive(dt);
            rig.Pose(Vehicle, dt);
            UpdateBlock();
        }

        void Drive(float dt)
        {
            var kb = Keyboard.current;
            VehicleInput input = Autopilot != null ? Autopilot(Vehicle) : Keys(kb);
            Vehicle.Tick(input, dt);
            bool exit = kb != null && kb.cKey.wasPressedThisFrame;
            bool reset = kb != null && kb.rKey.wasPressedThisFrame;
            if (exit && CanExit && !Exit())
            {
                // Hemmed in on every side: back onto the road first.
                Debug.Log("[WZ] no room to get out of the jeep: resetting it onto the road");
                Reset();
            }
            else if (reset && ResetAvailable || Vehicle.AutoReset) Reset();
            if (!TouchControls.Active) GameManager.Instance.hud.SetPrompt(Prompt(CanExit, ResetAvailable));
        }

        // Desktop: what C and R would do now.
        public static string Prompt(bool exit, bool reset) => exit ? reset ? ExitAndReset : ExitPrompt : reset ? ResetPrompt : null;

        public const string ExitPrompt = "C · EXIT", ResetPrompt = "R · RESET", ExitAndReset = "C · EXIT    R · RESET";

        static VehicleInput Keys(Keyboard kb)
        {
            var input = new VehicleInput();
            if (kb == null) return input;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.Throttle += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.Throttle -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.Steer += 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.Steer -= 1;
            input.Handbrake = kb.spaceKey.isPressed;
            return input;
        }

        // Where he sits this frame (easing in from where he stood while he gets in).
        public Vector3 RiderPosition()
        {
            Vector3 seat = SeatTransform.position - (Occupied == Seat.Driver ? Vector3.up * HipHeight : Vector3.zero);
            if (!Entering) return seat;
            float k = Mathf.SmoothStep(0, 1, entering / EnterTime);
            return Vector3.Lerp(enterFrom, seat, k);
        }

        // The jeep's box in its reserved slot: a wall to him on foot and to the camera.
        void UpdateBlock()
        {
            int slot = World.VehicleSlot;
            if (slot < 0 || slot >= World.Blocks.Length) return;
            var p = Vehicle.Position;
            World.Blocks[slot] = new Block
            {
                Center = new Vector2(p.x, p.z),
                Half = new Vector2(J.FootprintRadius, J.FootprintSpacing + J.FootprintRadius),
                Yaw = Vehicle.Yaw,
                Bottom = p.y - 0.3f,
                Top = p.y + J.Height,
                Flags = BlockFlags.Vehicle,
            };
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
