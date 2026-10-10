using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WardenZero
{
    // Stage 2's opening (the drop slice), in the jungle scene:
    // FLIGHT (Cinemachine shots of the chopper over the jungle, skippable) -> GREEN LIGHT
    // (jump with Space / JUMP) -> FREEFALL (steer, deploy with Space / DEPLOY, auto-deploy
    // low) -> CANOPY (steer, flare near the ground) -> LANDED (chute released, camera
    // settles into the gameplay view) -> WALK to checkpoint A -> hold it -> saved ->
    // "Milestone 1 complete". The flight model is Skydive; this drives scene objects.
    public class JungleStage : MonoBehaviour
    {
        public enum Phase { Flight, GreenLight, Freefall, Canopy, Landing, Walk, Secured }

        public const float FlightTime = 15;
        public const float JumpAltitude = 900; // above the LZ
        public const float AutoJumpAfter = 7;
        public const float SettleTime = 2.2f;
        public const float CaptureTime = 4;
        public const float CaptureRadius = 5;
        public const float CompleteDelay = 2.8f;
        public const float PlayHalfSize = 66; // the walkable square; the valley rim lies beyond

        [Header("Scene")]
        public Terrain terrain;
        public Vector3 lz;
        public Vector3 checkpoint;
        public Vector3[] trunks;
        public Chopper chopper;
        public Transform parachute; // canopy model; lines run from its anchors to his shoulders
        public Transform[] lineAnchors;
        public LineRenderer[] lines;
        public SkydivePose pose;
        public Renderer lzMarker;
        public Transform beacon;
        public Renderer beaconRing;
        public Light beaconLight;
        public AudioClip alert;

        [Header("Cameras")]
        public CinemachineBrain brain;
        public CinemachineCamera chaseCam;
        public CinemachineCamera passCam;
        public CinemachineCamera doorCam;
        public CinemachineCamera freefallCam;
        public CinemachineCamera canopyCam;
        public CinemachineCamera settleCam;
        public Transform diveRig; // at the Warden, turned to his heading; the dive cameras follow it
        public GameObject[] groundMarks; // his ground ring, blob shadow and glow: off in the air

        [Header("HUD")]
        public MissionHud hud;
        public DropHud dropHud;

        public Phase CurrentPhase { get; private set; }
        public Skydive Dive { get; private set; }
        public float Capture { get; private set; }
        public float LandingDamage { get; private set; }

        float phaseTime;
        int shot;
        Vector3 dropPoint;
        float flightHeading;
        Transform rifle;
        Vector3 chuteDrift;
        float chuteGone;
        float groundFog = 0.011f;

        GameManager Gm => GameManager.Instance;
        PlayerController Player => Gm.player;
        Transform Warden => Gm.player.transform;

        public float Ground(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        void Awake()
        {
            World.Use(PlayHalfSize, PlayHalfSize, new WallRect[0], trunks);
            World.Ground = Ground;
            chopper.groundHeight = Ground;
            groundFog = RenderSettings.fogDensity;
            // The flight comes in from the south-west; the jump point is short of the LZ, so
            // the Warden has to steer the last few hundred metres.
            flightHeading = 40;
            Vector3 dir = Quaternion.Euler(0, flightHeading, 0) * Vector3.forward;
            dropPoint = lz - dir * 330 + Vector3.up * (Ground(lz) + JumpAltitude);
        }

        public void Begin()
        {
            parachute.gameObject.SetActive(false);
            dropHud.Show(false);
            hud.SetCapture(null);
            beaconLight.color = GameConfig.Gold;
            var view = (WardenModelView)Player.view;
            rifle = view.rifle;
            switch (Campaign.Start)
            {
                case DropStart.Jump: StartFlight(true); break;
                case DropStart.Landed: StandAt(lz + new Vector3(2, 0, 2), false); break;
                case DropStart.CheckpointA: StandAt(checkpoint + new Vector3(-3, 0, -3), true); break;
                default: StartFlight(false); break;
            }
            StageLoader.FadeIn();
        }

        // ------------------------------------------------------------------ flight

        void StartFlight(bool atDropPoint)
        {
            Gm.BeginCinematic();
            CurrentPhase = Phase.Flight;
            phaseTime = 0;
            shot = -1;
            brain.enabled = true;
            Gm.cameraFollow.enabled = false;
            Vector3 dir = Quaternion.Euler(0, flightHeading, 0) * Vector3.forward;
            chopper.gameObject.SetActive(true);
            chopper.spin = 1;
            chopper.Place(dropPoint - dir * 1100, flightHeading);
            chopper.FlyTo(dropPoint, flightHeading, FlightTime);
            ShowGroundMarks(false);
            // The Warden rides at the open door.
            Warden.SetParent(chopper.seat, false);
            Warden.localPosition = Vector3.zero;
            Warden.localRotation = Quaternion.identity;
            Player.SetAim(Vector3.forward);
            hud.SetObjective("");
            Gm.hud.touch.ShowSkip(true);
            if (atDropPoint) SkipFlight();
            else Cut(chaseCam);
        }

        void SkipFlight()
        {
            Vector3 dir = Quaternion.Euler(0, flightHeading, 0) * Vector3.forward;
            chopper.Place(dropPoint, flightHeading);
            Cut(doorCam);
            GreenLight(dir);
        }

        void GreenLight(Vector3 dir)
        {
            CurrentPhase = Phase.GreenLight;
            phaseTime = 0;
            Gm.hud.touch.ShowSkip(false);
            // Keep going at a slower cruise while he gets ready.
            chopper.FlyTo(chopper.transform.position + dir * 260, flightHeading, AutoJumpAfter + 3);
            Gm.hud.Banner("GREEN LIGHT", 1.8f, GameConfig.Hex(0x9bff67));
            Gm.PlaySound(alert, 0.6f);
            dropHud.Show(true);
            dropHud.Prompt(TouchControls.Active ? "TAP JUMP" : "SPACE · JUMP");
            Gm.hud.touch.ShowAction("JUMP");
        }

        void TickFlight(float dt)
        {
            // Three shots: a chase behind the chopper, a pass from ahead, then the door.
            int want = phaseTime < 5 ? 0 : phaseTime < 10 ? 1 : 2;
            if (want != shot)
            {
                shot = want;
                if (want == 1)
                {
                    // Off to the side of where it will be in ~3 s, looking back along the path.
                    Vector3 ahead = chopper.transform.position + chopper.transform.forward * 260 + chopper.transform.right * 60 + Vector3.down * 15;
                    passCam.transform.position = ahead;
                }
                Show(want == 0 ? chaseCam : want == 1 ? passCam : doorCam);
            }
            if (SkipPressed()) SkipFlight();
            else if (phaseTime >= FlightTime) GreenLight(chopper.transform.forward);
        }

        void TickGreenLight()
        {
            dropHud.Set(Altitude(Warden.position), JumpAltitude, 0, Flat(Warden.position - lz).magnitude);
            dropHud.Track(lz, Player.cam);
            bool jump = phaseTime > 0.4f && (KeyPressed(Keyboard.current?.spaceKey) || Gm.hud.touch.ConsumeAction());
            if (jump || phaseTime > AutoJumpAfter) Jump();
        }

        // ------------------------------------------------------------------ the jump

        void Jump()
        {
            CurrentPhase = Phase.Freefall;
            phaseTime = 0;
            Vector3 exit = chopper.door.position;
            Warden.SetParent(null, true);
            Dive = new Skydive
            {
                Position = exit,
                Velocity = chopper.Velocity * 0.7f - chopper.transform.right * 3,
                Heading = HeadingTo(lz - exit),
                Target = lz,
                Ground = Ground,
            };
            // The rifle is strapped on for the jump.
            if (rifle != null) rifle.gameObject.SetActive(false);
            ((WardenModelView)Player.view).handIK.weight = 0;
            dropHud.Prompt("A/D TURN  ·  W TRACK  ·  S BRAKE");
            Gm.hud.touch.HideAction();
            chopper.FlyTo(chopper.transform.position + chopper.transform.forward * 900 + Vector3.up * 60, flightHeading + 20, 14);
            PlaceDiver();
            Show(freefallCam);
        }

        void TickDive(float dt)
        {
            var kb = Keyboard.current;
            Vector2 steer = Vector2.zero;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer.x += 1;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) steer.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) steer.y -= 1;
            }
            if (TouchControls.Active) steer += Gm.hud.touch.Stick;
            bool action = KeyPressed(kb?.spaceKey) || Gm.hud.touch.ConsumeAction();
            var state = Dive.Current;
            if (action)
            {
                if (state == Skydive.State.Freefall) Dive.Deploy();
                else Dive.Flare();
            }
            Dive.Step(steer, dt);

            if (state == Skydive.State.Freefall && Dive.Current == Skydive.State.Opening)
            {
                CurrentPhase = Phase.Canopy;
                parachute.gameObject.SetActive(true);
                foreach (var line in lines) line.enabled = true;
                Gm.hud.Banner(Dive.AutoDeployed ? "AUTO-DEPLOY" : "CANOPY OPEN", 1.4f, Dive.AutoDeployed ? GameConfig.Health : GameConfig.Accent);
                Show(canopyCam);
            }
            PlaceDiver();

            float alt = Dive.Altitude;
            dropHud.Track(lz, Player.cam);
            dropHud.Set(alt, JumpAltitude, Mathf.Max(0, -Dive.Velocity.y), Flat(Dive.Position - lz).magnitude);
            if (Dive.Current == Skydive.State.Freefall)
            {
                bool pull = alt < Skydive.DeployPrompt;
                dropHud.Prompt(pull ? (TouchControls.Active ? "TAP DEPLOY" : "SPACE · DEPLOY CHUTE") : "A/D TURN  ·  W TRACK  ·  S BRAKE", pull);
                if (pull) Gm.hud.touch.ShowActionOnce("DEPLOY");
            }
            else if (Dive.Current == Skydive.State.Canopy)
            {
                bool flare = alt < Skydive.FlareHeight;
                dropHud.Prompt(flare ? (TouchControls.Active ? "TAP FLARE" : "SPACE · FLARE") : "A/D STEER  ·  W DIVE  ·  S BRAKE", flare && !Dive.Flared);
                if (flare && !Dive.Flared) Gm.hud.touch.ShowActionOnce("FLARE");
                else Gm.hud.touch.HideAction();
            }
            if (Dive.Current == Skydive.State.Landed) Land();
        }

        // Body, rig and chute follow the flight model.
        void PlaceDiver()
        {
            float open = Dive.Current == Skydive.State.Freefall ? 0 : Mathf.Clamp01((Dive.SinceDeploy - Skydive.SnatchDelay) / 1.2f);
            // Belly to earth in freefall, swung upright under the canopy; banked into turns.
            float pitch = Mathf.Lerp(78, 0, open);
            Warden.SetPositionAndRotation(Dive.Position, Quaternion.Euler(0, Dive.Heading, 0) * Quaternion.Euler(pitch, 0, 0));
            pose.freefall = 1 - open;
            pose.canopy = open;
            diveRig.SetPositionAndRotation(Dive.Position + Vector3.up * 1.2f, Quaternion.Euler(0, Dive.Heading, 0));

            if (parachute.gameObject.activeSelf)
            {
                // Inflates from a streamer to the full wing over the opening.
                float k = Mathf.Clamp01(Dive.SinceDeploy / (Skydive.SnatchDelay + Skydive.InflateTime));
                float span = Mathf.Lerp(0.15f, 1, Mathf.SmoothStep(0, 1, k)) * (1 + Mathf.Sin(k * Mathf.PI) * 0.08f);
                parachute.localScale = new Vector3(span, Mathf.Lerp(0.4f, 1, k), Mathf.Lerp(0.5f, 1, k));
                parachute.SetPositionAndRotation(Dive.Position + Vector3.up * Mathf.Lerp(3, 7.2f, k), Quaternion.Euler(0, Dive.Heading, 0));
                UpdateLines();
            }
        }

        void UpdateLines()
        {
            var anim = ((WardenModelView)Player.view).animator;
            var l = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var r = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i].SetPosition(0, lineAnchors[i].position);
                lines[i].SetPosition(1, (lineAnchors[i].localPosition.x < 0 ? l : r).position);
            }
        }

        void Land()
        {
            CurrentPhase = Phase.Landing;
            phaseTime = 0;
            float damage = Skydive.LandingDamage(Dive.TouchdownSpeed);
            var s = Gm.Run.Stats;
            // Hard landings hurt but never kill.
            LandingDamage = Mathf.Min(damage, Mathf.Max(0, s.Health - 1));
            s.Health -= LandingDamage;
            if (LandingDamage > 0)
            {
                Gm.hud.Banner($"HARD LANDING  ·  -{Mathf.CeilToInt(LandingDamage)} HP", 2.2f, true);
                Player.view.OnHurt();
                Effects.Instance.PlayerHurt(Dive.Position);
                Gm.PlaySound(Gm.hurtSound, 0.6f);
            }
            else
            {
                Gm.hud.Banner(Dive.Flared ? "PERFECT LANDING" : "LANDED", 1.8f, GameConfig.Hex(0x9bff67));
            }
            Gm.hud.SetHealth(s.Health, s.MaxHealth);
            pose.freefall = pose.canopy = 0;
            // Gameplay keeps the root unrotated and turns the model to the aim.
            Warden.SetPositionAndRotation(Dive.Position, Quaternion.identity);
            Player.SetAim(Quaternion.Euler(0, Dive.Heading, 0) * Vector3.forward);
            ((WardenModelView)Player.view).SnapYaw(Dive.Heading);
            if (rifle != null) rifle.gameObject.SetActive(true);
            ((WardenModelView)Player.view).handIK.weight = 1;
            ShowGroundMarks(true);
            // Cut away: the chute carries on a little and collapses.
            chuteDrift = Quaternion.Euler(0, Dive.Heading, 0) * Vector3.forward * 3;
            chuteGone = 0;
            foreach (var line in lines) line.enabled = false;
            dropHud.Show(false);
            Gm.hud.touch.HideAction();

            // Settle on the gameplay view: a still camera where CameraFollow will be.
            Vector3 p = Warden.position;
            settleCam.transform.position = p + Gm.cameraFollow.offset;
            settleCam.transform.LookAt(p + new Vector3(0, 0, GameConfig.CameraLookAhead));
            Show(settleCam);
        }

        void TickLanding(float dt)
        {
            if (phaseTime >= SettleTime) BeginWalk();
        }

        // ------------------------------------------------------------------ on foot

        void BeginWalk()
        {
            CurrentPhase = Phase.Walk;
            brain.enabled = false;
            foreach (var c in new[] { chaseCam, passCam, doorCam, freefallCam, canopyCam, settleCam }) c.gameObject.SetActive(false);
            Gm.cameraFollow.enabled = true;
            Gm.cameraFollow.Snap();
            Gm.BeginPlay();
            Capture = 0;
            hud.SetObjective("REACH CHECKPOINT A");
            lzMarker.gameObject.SetActive(false);
        }

        // Debug and Continue: on foot at a spot, checkpoint A already taken or not.
        void StandAt(Vector3 p, bool secured)
        {
            chopper.gameObject.SetActive(false);
            p.y = Ground(p);
            Warden.SetParent(null, false);
            Warden.SetPositionAndRotation(p, Quaternion.identity);
            Gm.BeginPlay();
            brain.enabled = false;
            Gm.cameraFollow.enabled = true;
            Gm.cameraFollow.Snap();
            lzMarker.gameObject.SetActive(false);
            CurrentPhase = Phase.Walk;
            hud.SetObjective("REACH CHECKPOINT A");
            if (secured) MarkSecured();
        }

        void TickWalk(float dt)
        {
            Vector3 d = Flat(Warden.position - checkpoint);
            bool inRing = d.magnitude < CaptureRadius;
            Capture = inRing ? Capture + dt : Mathf.Max(0, Capture - dt * 1.5f);
            hud.SetCapture(Capture > 0 || inRing ? Capture / CaptureTime : (float?)null);
            hud.SetObjective(inRing ? "HOLD THE CHECKPOINT" : "REACH CHECKPOINT A");
            hud.PointAt(checkpoint + Vector3.up * 2, Player.cam);
            if (Capture >= CaptureTime) SecureCheckpoint();
        }

        // Checkpoint A taken: saved, then the slice ends.
        public void SecureCheckpoint()
        {
            Campaign.Save(Gm.Run, CampaignSave.CheckpointA);
            MarkSecured();
            Gm.hud.Banner("CHECKPOINT A SECURED", 2.4f, GameConfig.Hex(0x9bff67));
            Gm.PlaySound(alert, 0.7f);
            Effects.Instance.Blast(checkpoint, 3, GameConfig.Hex(0x9bff67), 0.2f);
            phaseTime = 0;
            CurrentPhase = Phase.Secured;
            completeShown = false;
        }

        void MarkSecured()
        {
            CurrentPhase = Phase.Secured;
            completeShown = true;
            hud.SetCapture(null);
            hud.SetObjective("CHECKPOINT A SECURED");
            hud.PointAt(null, null);
            beaconLight.color = GameConfig.Hex(0x9bff67);
            beaconRing.gameObject.SetActive(false);
        }

        bool completeShown;

        void TickSecured()
        {
            if (completeShown || phaseTime < CompleteDelay) return;
            completeShown = true;
            Gm.MilestoneComplete();
        }

        // ------------------------------------------------------------------ frame

        public void Tick(float dt)
        {
            phaseTime += dt;
            switch (CurrentPhase)
            {
                case Phase.Flight: TickFlight(dt); break;
                case Phase.GreenLight: TickGreenLight(); break;
                case Phase.Freefall:
                case Phase.Canopy: TickDive(dt); break;
                case Phase.Landing: TickLanding(dt); break;
                case Phase.Walk: TickWalk(dt); break;
                case Phase.Secured: TickSecured(); break;
            }
            if (lzMarker.gameObject.activeSelf) lzMarker.transform.Rotate(0, 0, dt * 10, Space.Self);
            if (beaconRing.gameObject.activeSelf) beaconRing.transform.Rotate(0, 0, dt * 14, Space.Self);
            UpdateChuteRelease(dt);
        }

        // After the cut-away the chute drifts on and sinks in a heap.
        void UpdateChuteRelease(float dt)
        {
            if (CurrentPhase != Phase.Landing && CurrentPhase != Phase.Walk || !parachute.gameObject.activeSelf) return;
            chuteGone += dt;
            Vector3 p = parachute.position + chuteDrift * dt;
            float ground = Ground(p) + 0.4f;
            p.y = Mathf.MoveTowards(p.y, ground, dt * 3.5f);
            parachute.position = p;
            parachute.localScale = new Vector3(Mathf.Lerp(1, 0.8f, chuteGone / 2), Mathf.Lerp(1, 0.12f, chuteGone / 2.5f), 1);
            chuteDrift *= Mathf.Exp(-dt * 0.8f);
            if (chuteGone > 7) parachute.gameObject.SetActive(false);
        }

        // Thicker haze near the ground, clear air up high (the drop starts at ~900 m).
        void LateUpdate()
        {
            var cam = Player.cam.transform.position;
            float h = cam.y - Ground(cam);
            RenderSettings.fogDensity = Mathf.Lerp(groundFog, groundFog * 0.025f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(15, 160, h)));
        }

        // ------------------------------------------------------------------ helpers

        void ShowGroundMarks(bool on)
        {
            foreach (var g in groundMarks) g.SetActive(on);
            ((WardenModelView)Player.view).SetXRay(on);
        }

        void Show(CinemachineCamera cam)
        {
            foreach (var c in new[] { chaseCam, passCam, doorCam, freefallCam, canopyCam, settleCam })
                c.gameObject.SetActive(c == cam);
        }

        // Switch without a blend (the first shot, a skip).
        void Cut(CinemachineCamera cam)
        {
            StopCoroutine(nameof(RestoreBlend));
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
            Show(cam);
            StartCoroutine(nameof(RestoreBlend));
        }

        System.Collections.IEnumerator RestoreBlend()
        {
            yield return null;
            yield return null;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.8f);
        }

        bool SkipPressed()
        {
            var kb = Keyboard.current;
            return KeyPressed(kb?.spaceKey) || KeyPressed(kb?.enterKey) || Gm.hud.touch.ConsumeSkip();
        }

        static bool KeyPressed(UnityEngine.InputSystem.Controls.KeyControl key) => key != null && key.wasPressedThisFrame;

        float Altitude(Vector3 p) => p.y - Ground(p);

        static float HeadingTo(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        // Test hooks.
        public void SkipToJump() => SkipFlight();
        public void JumpNow() => Jump();
    }
}
