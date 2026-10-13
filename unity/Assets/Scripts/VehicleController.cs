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
    // On the gun (the ride: Ride set) the jeep follows the ride road by itself and the Warden
    // works the gun: it points where the gun camera aims (CameraFollow), fires FireRate rounds
    // a second while the left button or FIRE is held (Turret: heat, no shooting the cab), each a
    // hitscan through WorldCast to the scenery or a target, drawn as a tracer.
    [DefaultExecutionOrder(-5)] // after the look input, before the Warden is put in his seat
    public class VehicleController : MonoBehaviour
    {
        public enum Seat { None, Driver, Gunner }

        public VehicleRig rig;
        public JungleStage stage;
        public Material tracerMaterial;

        public const float EnterTime = 0.6f, DoorReach = 2.5f;
        // His feet below the driver's cushion: the seated hold's hips on it (VehiclePose puts the
        // legs to the footwell).
        public const float HipHeight = 0.85f;
        public const float GunnerBack = 0.75f; // the gunner's feet behind the pedestal
        public const float FadeTime = 0.3f;

        public static VehicleController Active { get; private set; }

        public Vehicle Vehicle { get; } = new Vehicle();
        public Turret Turret { get; } = new Turret();
        public TurretRide Ride { get; set; } // the gun ride under way (null: none)
        public int RoundsFired { get; private set; }
        public Destructible LastHit { get; private set; }
        public Vector3 AimPoint { get; private set; }
        // Test hook: holds the trigger.
        [System.NonSerialized] public bool TriggerHeld;
        public PlayerController Rider { get; private set; }
        public Seat Occupied { get; private set; }
        public bool Entering => entering < EnterTime;
        public bool Fading { get; private set; }
        // Shown on RESET: offered by a stuck rule, or (touch) after a while below ResetSpeed.
        public bool ResetAvailable => Vehicle.ResetOffered || Vehicle.Speed < J.ResetSpeed && (!TouchControls.Active || Vehicle.SlowTime >= J.TouchResetAfter);
        public bool CanExit => Vehicle.Speed < J.ExitSpeed && !Entering && !Fading;
        // Test hook: drives instead of the keys.
        public System.Func<Vehicle, VehicleInput> Autopilot;

        // DRIVE ASSIST (pause menu; on by default on touch, off on desktop): with the stick
        // centred on the road the jeep steers gently along it (at most AssistSteer of full
        // lock), and the throttle eases off for the bends ahead.
        public const string AssistKey = "wz.assist.drive";
        public const float AssistSteer = 0.3f, StickDeadZone = 0.12f;
        public static bool DriveAssist
        {
            get => PlayerPrefs.GetInt(AssistKey, TouchControls.Active ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt(AssistKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        float entering = EnterTime;
        Vector3 enterFrom;

        VehicleAudio audio;

        void Awake()
        {
            Active = this;
            audio = GetComponent<VehicleAudio>();
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
        public void Reset(bool thenExit = false)
        {
            if (!Fading) StartCoroutine(ResetRoutine(thenExit));
        }

        IEnumerator ResetRoutine(bool thenExit)
        {
            Fading = true;
            StageLoader.FadeOut(1 / FadeTime);
            while (StageLoader.Fade < 0.99f) yield return null;
            Vehicle.ResetToRoad();
            rig.Snap(Vehicle);
            UpdateBlock();
            if (Rider != null) Rider.transform.position = RiderPosition();
            if (thenExit && Rider != null) Exit();
            GameManager.Instance.cameraFollow.Snap();
            StageLoader.FadeIn(1 / FadeTime);
            while (StageLoader.Fade > 0.01f) yield return null;
            Fading = false;
        }

        void Update()
        {
            // Impact and Landing belong to the Tick that set them: on a frame with no Tick (out
            // of the seat, entering, the reset fade, paused) they would clunk and shake again.
            if (!(Occupied == Seat.Driver && !Entering && !Fading) && Ride == null) Vehicle.ClearEvents();
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;
            if (TouchControls.Active) gm.hud.touch.SetScheme(Occupied == Seat.Driver ? TouchLayout.Scheme.Drive : Occupied == Seat.Gunner ? TouchLayout.Scheme.Gun : TouchLayout.Scheme.OnFoot);
            float dt = Time.deltaTime;
            if (Entering) entering += dt;
            if (Occupied == Seat.Driver && !Entering && !Fading) Drive(dt);
            if (Ride != null) FollowRide(dt);
            if (Occupied != Seat.Gunner && stage != null) stage.hud.SetHeat(null, false);
            if (Occupied == Seat.Gunner && !Entering) Gun(dt);
            rig.Pose(Vehicle, dt);
            UpdateBlock();
            FadeTracers(dt);
        }

        void Drive(float dt)
        {
            var kb = Keyboard.current;
            var touch = GameManager.Instance.hud.touch;
            bool touching = TouchControls.Active;
            VehicleInput input = Autopilot != null ? Autopilot(Vehicle) : Keys(kb);
            bool steered = Mathf.Abs(input.Steer) > 0;
            if (touching && Autopilot == null)
            {
                // GAS and BRAKE held, the stick's x steers, the handbrake held.
                float x = touch.Stick.x;
                if (Mathf.Abs(x) >= StickDeadZone)
                {
                    input.Steer = Mathf.Clamp(input.Steer + x, -1, 1);
                    steered = true;
                }
                input.Throttle = Mathf.Clamp(input.Throttle + (touch.ActionHeld ? 1 : 0) - (touch.FireHeld ? 1 : 0), -1, 1);
                input.Handbrake |= touch.DashHeld;
            }
            if (DriveAssist && Autopilot == null) input = Assist(input, steered);
            Vehicle.Tick(input, dt, touching ? J.TouchSteerSmooth : J.SteerSlew);
            if (audio != null) audio.Throttle(input.Throttle);
            bool exit = (kb != null && kb.cKey.wasPressedThisFrame) | (touching && touch.ConsumeDrop());
            bool reset = (kb != null && kb.rKey.wasPressedThisFrame) | (touching && touch.ConsumeBomb());
            if (exit && CanExit && !Exit())
            {
                // Hemmed in on every side: back onto the road, and out there, on the one press.
                Debug.Log("[WZ] no room to get out of the jeep: resetting it onto the road");
                Reset(thenExit: true);
            }
            else if (reset && ResetAvailable || Vehicle.AutoReset) Reset();
            if (!touching) GameManager.Instance.hud.SetPrompt(Prompt(CanExit, ResetAvailable));
            else
            {
                touch.ShowDrop(CanExit);
                touch.ShowBomb(ResetAvailable);
            }
        }

        // DRIVE ASSIST: along the road while the stick is centred, and easing off for bends.
        VehicleInput Assist(VehicleInput input, bool steered)
        {
            var road = Vehicle.Road;
            if (road == null || Vehicle.RoadDistance > RoadLayout.ShoulderHalf) return input;
            var bot = DriveBot.Input(Vehicle, road);
            if (!steered) input.Steer = Mathf.Clamp(bot.Steer, -AssistSteer, AssistSteer);
            if (input.Throttle > 0)
            {
                float s = road.Nearest(new Vector2(Vehicle.Position.x, Vehicle.Position.z)).S;
                float safe = DriveBot.SafeSpeed(road, s, Vehicle.Speed);
                if (safe < Vehicle.Speed) input.Throttle = Mathf.Min(input.Throttle, Mathf.Clamp((safe - Vehicle.Speed) * 0.3f, -0.5f, 1));
            }
            return input;
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

        // ------------------------------------------------------------------ the gun ride

        // Along the ride road at the ride's own pace.
        void FollowRide(float dt)
        {
            Ride.Tick(dt);
            if (audio != null) audio.Throttle(Ride.Speed > 0.5f ? 0.5f : 0);
            var here = Ride.Here;
            Vehicle.MoveAlong(here.Position, Ride.Heading, Ride.Speed, Ride.SteerAngle, dt);
        }

        readonly System.Collections.Generic.List<Vector3> assistTargets = new System.Collections.Generic.List<Vector3>();

        void Gun(float dt)
        {
            var follow = GameManager.Instance.cameraFollow;
            if (TouchControls.Active && DriveAssist)
            {
                // Touch: the look slows near a target and is pulled onto it.
                var eye = DriveCamera.GunPose(rig.gunYoke.position, follow.GunYaw, follow.GunPitch, follow.GunWide, out _).position;
                assistTargets.Clear();
                foreach (var t in Destructible.All)
                    if (t.Alive) assistTargets.Add(t.Centre);
                var pull = AimAssist.Pull(eye, follow.GunYaw, follow.GunPitch, assistTargets, ClearFn, dt, out float slow);
                follow.GunTouchSlow = slow;
                follow.GunLook(pull.x, pull.y);
            }
            else follow.GunTouchSlow = 1;
            // The gun camera's ray (this frame's aim) decides where the gun points: at the first
            // thing on it. It starts where the camera is, pulled in by collision as PlaceGunCam
            // pulls it, so a wall behind the gun never takes the ray.
            var pose = DriveCamera.GunPose(rig.gunYoke.position, follow.GunYaw, follow.GunPitch, follow.GunWide, out Vector3 pivotAt);
            Vector3 eyeAt = DriveCamera.Collide(pivotAt, pose.position);
            Vector3 dir = pose.rotation * Vector3.forward;
            AimPoint = eyeAt + dir * Shot(eyeAt, dir, out _);
            Vector3 muzzle = rig.muzzle.position;
            Vector3 toAim = AimPoint - rig.gunYoke.position;
            Turret.Aim(Mathf.Atan2(toAim.x, toAim.z) * Mathf.Rad2Deg, Mathf.Atan2(toAim.y, new Vector2(toAim.x, toAim.z).magnitude) * Mathf.Rad2Deg, Vehicle.Yaw);
            rig.gunYoke.localRotation = Quaternion.Euler(0, Turret.Yaw, 0);
            rig.gun.localRotation = Quaternion.Euler(-Turret.Pitch, 0, 0);
            var mouse = Mouse.current;
            bool trigger = TriggerHeld || mouse != null && mouse.leftButton.isPressed && !TouchControls.Active
                || TouchControls.Active && GameManager.Instance.hud.touch.ActionHeld;
            int rounds = Turret.Tick(trigger, dt);
            for (int i = 0; i < rounds; i++) FireRound(muzzle);
            if (stage != null) stage.hud.SetHeat(Turret.Heat, Turret.Overheated);
        }

        // One round: from the muzzle toward the aim point, a little spread, to the first thing
        // it meets (the jeep's own box left out).
        void FireRound(Vector3 muzzle)
        {
            RoundsFired++;
            Vector3 dir = (AimPoint - muzzle).normalized;
            dir = Quaternion.Euler(Random.Range(-Spread, Spread), Random.Range(-Spread, Spread), 0) * dir;
            float d = Shot(muzzle, dir, out var target);
            Vector3 end = muzzle + dir * d;
            if (target != null)
            {
                LastHit = target;
                target.Hit(Turret.Damage);
            }
            if (d < Turret.Range) Effects.Instance.BoltImpact(end);
            Tracer(muzzle, end);
            rig.Flash();
            if (audio != null) audio.Gunshot();
            GameManager.Instance.cameraFollow.AddShake(0.12f);
        }

        public const float Spread = 0.6f; // degrees

        static readonly System.Func<Vector3, Vector3, bool> ClearFn = Clear;

        // The line from a to b is open (no scenery; the jeep's own box left out).
        static bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            return len < 1e-3f || !WorldCast.Cast(a, d / len, len - 0.6f, 0, false, out _, BlockFlags.Vehicle);
        }

        // Distance to the first thing along the ray: scenery or a live target.
        static float Shot(Vector3 from, Vector3 dir, out Destructible target)
        {
            WorldCast.Cast(from, dir, Turret.Range, 0, false, out float scenery, BlockFlags.Vehicle);
            target = Destructible.Cast(from, dir, scenery, 0, out float hit);
            return target != null ? hit : scenery;
        }

        // Tracers: short-lived lines from the muzzle, pooled.
        readonly System.Collections.Generic.List<LineRenderer> tracers = new System.Collections.Generic.List<LineRenderer>();
        readonly System.Collections.Generic.List<float> tracerLife = new System.Collections.Generic.List<float>();
        const float TracerTime = 0.07f;

        void Tracer(Vector3 from, Vector3 to)
        {
            if (tracerMaterial == null) return;
            int i = tracerLife.FindIndex(t => t <= 0);
            if (i < 0)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = tracerMaterial;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.widthMultiplier = 0.06f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tracers.Add(lr);
                tracerLife.Add(0);
                i = tracers.Count - 1;
            }
            // A streak about a fifth of the way, toward the hit.
            tracers[i].SetPosition(0, Vector3.Lerp(from, to, 0.05f));
            tracers[i].SetPosition(1, Vector3.Lerp(from, to, Mathf.Min(1, 0.05f + 18 / Mathf.Max(1, (to - from).magnitude))));
            tracers[i].enabled = true;
            tracerLife[i] = TracerTime;
        }

        void FadeTracers(float dt)
        {
            for (int i = 0; i < tracers.Count; i++)
            {
                if (tracerLife[i] <= 0) continue;
                tracerLife[i] -= dt;
                if (tracerLife[i] <= 0) tracers[i].enabled = false;
            }
        }

        // The way he faces in his seat: along the jeep, or along the gun.
        public Vector3 RiderFacing => Occupied == Seat.Gunner ? Quaternion.Euler(0, Vehicle.Yaw + Turret.Yaw, 0) * Vector3.forward : Vehicle.Forward;

        // Where he sits this frame (easing in from where he stood while he gets in).
        public Vector3 RiderPosition()
        {
            Vector3 seat = SeatTransform.position - (Occupied == Seat.Driver ? Vector3.up * HipHeight : Vector3.zero);
            // The gunner walks round the pedestal with the gun, staying behind its grips.
            if (Occupied == Seat.Gunner)
            {
                Vector3 behind = rig.gunYoke.position - RiderFacing * GunnerBack;
                seat = new Vector3(behind.x, seat.y, behind.z);
            }
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
