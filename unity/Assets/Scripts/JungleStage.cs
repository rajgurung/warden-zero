using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WardenZero
{
    // Stage 2's opening (the drop slice), in the jungle scene:
    // FLIGHT (Cinemachine shots of the chopper over the jungle, skippable) -> GREEN LIGHT
    // (crouched in the door; Space / JUMP leans him out) -> FREEFALL (a short tumble off the
    // door settling into a stable arch; steer, deploy with Space / DEPLOY, auto-deploy low)
    // -> CANOPY (pilot chute, canopy unfurls, opening jolt and the body swinging upright
    // under the risers; steer on the toggles, flare near the ground) -> LANDING (run-out, or
    // a crouched hard landing that hurts; the canopy collapses behind him) -> WALK to
    // checkpoint A -> hold it -> saved -> "Milestone 1 complete".
    // The flight model is Skydive; this drives the scene objects, cameras and sound.
    public class JungleStage : MonoBehaviour
    {
        public enum Phase { Flight, GreenLight, Freefall, Canopy, Landing, Walk, Secured }

        public const float FlightTime = 15;
        public const float JumpAltitude = 900; // above the LZ
        public const float AutoJumpAfter = 7;
        public const float LeanOutTime = 0.35f; // from the key press to leaving the door
        public const float SettleTime = 2.4f;
        public const float CaptureTime = 4;
        public const float CaptureRadius = 5;
        public const float CompleteDelay = 2.8f;
        public const float PlayHalfSize = 66; // the walkable square; the valley rim lies beyond
        // The flight model tracks his chest; the canopy pivots him at the shoulders (metres
        // above his feet when standing: the Warden is ~2.6 m tall in game units).
        public const float BodyCenter = 1.4f;
        public const float Shoulders = 2.1f;
        public const float LineLength = 7.2f;

        [Header("Scene")]
        public Terrain terrain;
        public Vector3 lz;
        public Vector3 checkpoint;
        public Vector3[] trunks;
        public Chopper chopper;
        public Transform parachute; // canopy model; lines run from its anchors to his shoulders
        public Transform pilotChute;
        public Transform[] lineAnchors;
        public LineRenderer[] lines;
        public SkydivePose pose;
        public Renderer lzMarker;
        public Transform beacon;
        public Renderer beaconRing;
        public Light beaconLight;
        public AudioClip alert;
        public ParticleSystem windStreaks;

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
        public bool HardLanding { get; private set; }
        public float BodyPitch { get; private set; } // degrees from upright (90 = belly to earth)
        public Pendulum Swing => swing;
        public bool LinesTaut { get; private set; }

        float phaseTime;
        int shot;
        Vector3 dropPoint;
        float flightHeading;
        Transform rifle;
        float groundFog = 0.011f;
        bool leaning;
        float exitTime;
        float turn, trackIn; // smoothed steering
        Pendulum swing, sway;
        float shake;
        Vector3 runDir;
        float runSpeed;
        Vector3 chuteDrift;
        float chuteGone;
        AudioSource wind, flutter, ambience, sfx;
        float ambienceTarget;
        ChopperAudio chopperAudio;
        bool autoFlare; // debug URL ?autoflare: flares at 6 m (for recording landings)

        GameManager Gm => GameManager.Instance;
        PlayerController Player => Gm.player;
        Transform Warden => Gm.player.transform;

        public float Ground(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        void Awake()
        {
            World.Use(PlayHalfSize, PlayHalfSize, new WallRect[0], trunks);
            World.Ground = Ground;
            chopper.groundHeight = Ground;
            chopperAudio = chopper.GetComponent<ChopperAudio>();
            groundFog = RenderSettings.fogDensity;
            autoFlare = Application.absoluteURL.Contains("autoflare");
            // The flight comes in from the south-west; the jump point is short of the LZ, so
            // the Warden has to steer the last few hundred metres.
            flightHeading = 40;
            Vector3 dir = Quaternion.Euler(0, flightHeading, 0) * Vector3.forward;
            dropPoint = lz - dir * 330 + Vector3.up * (Ground(lz) + JumpAltitude);
            wind = Loop(SynthAudio.Wind);
            flutter = Loop(SynthAudio.Flutter);
            ambience = Loop(SynthAudio.Jungle);
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
        }

        AudioSource Loop(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = 0;
            s.playOnAwake = false;
            s.Play();
            return s;
        }

        public void Begin()
        {
            parachute.gameObject.SetActive(false);
            pilotChute.gameObject.SetActive(false);
            dropHud.Show(false);
            hud.SetCapture(null);
            beaconLight.color = GameConfig.Gold;
            var view = (WardenModelView)Player.view;
            rifle = view.rifle;
            switch (Campaign.Start)
            {
                case DropStart.Jump: StartFlight(true); break;
                case DropStart.Landed: StandAt(lz + new Vector3(2, 0, 2), false); break;
                case DropStart.NearCheckpoint: StandAt(checkpoint + new Vector3(-7, 0, -9), false); break;
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
            chopper.FlyTo(dropPoint, flightHeading, FlightTime, false);
            ShowGroundMarks(false);
            // The Warden rides at the open door.
            Warden.SetParent(chopper.seat, false);
            Warden.localPosition = Vector3.zero;
            Warden.localRotation = Quaternion.identity;
            Player.SetAim(Vector3.forward);
            if (rifle != null) rifle.gameObject.SetActive(false);
            ((WardenModelView)Player.view).handIK.weight = 0;
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
            leaning = false;
            Gm.hud.touch.ShowSkip(false);
            // Slowed to a near hover for the jump, drifting on.
            chopper.FlyTo(chopper.transform.position + dir * 110, flightHeading, AutoJumpAfter + 4);
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
                Show(want == 0 ? chaseCam : want == 1 ? passCam : doorCam, 1.8f);
            }
            // The cabin mix while he rides; the open door when we look at it.
            chopperAudio.inside = Mathf.MoveTowards(chopperAudio.inside, shot < 2 ? 0.6f : 0, dt);
            if (SkipPressed()) SkipFlight();
            else if (phaseTime >= FlightTime) GreenLight(chopper.transform.forward);
        }

        void TickGreenLight(float dt)
        {
            chopperAudio.inside = Mathf.MoveTowards(chopperAudio.inside, 0, dt);
            // Crouched in the door, hands on the frame, shuffled out to the edge.
            pose.door = Mathf.MoveTowards(pose.door, 1, dt * 3);
            Warden.localPosition = Vector3.MoveTowards(Warden.localPosition, new Vector3(0, 0, 0.55f), dt * 1.5f);
            dropHud.Set(Altitude(Warden.position), JumpAltitude, 0, Flat(Warden.position - lz).magnitude);
            dropHud.Track(lz, Player.cam);
            if (!leaning)
            {
                bool jump = phaseTime > 0.4f && (KeyPressed(Keyboard.current?.spaceKey) || Gm.hud.touch.ConsumeAction());
                if (jump || phaseTime > AutoJumpAfter)
                {
                    leaning = true;
                    exitTime = 0;
                    Gm.hud.touch.HideAction();
                }
                return;
            }
            // A beat leaning out over the edge, then he goes.
            exitTime += dt;
            Warden.localRotation = Quaternion.Euler(Mathf.SmoothStep(0, 35, exitTime / LeanOutTime), 0, 0);
            if (exitTime >= LeanOutTime) Jump();
        }

        // ------------------------------------------------------------------ the jump

        void Jump()
        {
            CurrentPhase = Phase.Freefall;
            phaseTime = 0;
            exitTime = 0;
            leaning = false;
            Vector3 center = Warden.position + Warden.up * BodyCenter;
            float heading = Warden.eulerAngles.y;
            Warden.SetParent(null, true);
            Dive = new Skydive
            {
                Position = center,
                // Carried along by the chopper, pushed out of the door.
                Velocity = chopper.Velocity * 0.9f + Warden.forward * 2.5f,
                Heading = heading,
                Target = lz,
                Ground = p => Ground(p) + BodyCenter,
            };
            turn = trackIn = 0;
            swing = sway = default;
            LinesTaut = false;
            pose.door = 0;
            dropHud.Prompt("A/D TURN  ·  W TRACK  ·  S BRAKE");
            chopper.FlyTo(chopper.transform.position + chopper.transform.forward * 700 + Vector3.up * 40, flightHeading + 25, 16);
            PlaceDiver(0);
            Show(freefallCam, 1.2f);
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
            steer = Vector2.ClampMagnitude(steer, 1);
            // The first moment off the door belongs to the tumble.
            if (Dive.Current == Skydive.State.Freefall && exitTime < 0.5f) steer = Vector2.zero;
            bool action = KeyPressed(kb?.spaceKey) || Gm.hud.touch.ConsumeAction();
            var state = Dive.Current;
            if (action)
            {
                if (state == Skydive.State.Freefall) Dive.Deploy();
                else Dive.Flare();
            }
            if (autoFlare && Dive.Altitude < 6) Dive.Flare();
            Dive.Step(steer, dt);
            turn = Mathf.Lerp(turn, steer.x, Mathf.Min(1, dt * 4));
            trackIn = Mathf.Lerp(trackIn, steer.y, Mathf.Min(1, dt * 3));

            if (state == Skydive.State.Freefall && Dive.Current == Skydive.State.Opening)
            {
                CurrentPhase = Phase.Canopy;
                Gm.hud.Banner(Dive.AutoDeployed ? "AUTO-DEPLOY" : "PULL", 1.2f, Dive.AutoDeployed ? GameConfig.Health : GameConfig.Accent);
                pilotChute.gameObject.SetActive(true);
                parachute.gameObject.SetActive(true);
                foreach (var line in lines) line.enabled = true;
            }
            PlaceDiver(dt);
            UpdateDiveFeel(dt);

            float alt = Dive.Altitude;
            dropHud.Track(lz, Player.cam);
            dropHud.Set(alt, JumpAltitude, Mathf.Max(0, -Dive.Velocity.y), Flat(Dive.Position - lz).magnitude);
            if (Dive.Current == Skydive.State.Freefall)
            {
                bool pull = alt < Skydive.DeployPrompt;
                dropHud.Prompt(pull ? (TouchControls.Active ? "TAP DEPLOY" : "SPACE · DEPLOY CHUTE") : "A/D TURN  ·  W TRACK  ·  S BRAKE", pull);
                Gm.hud.touch.ShowActionOnce("DEPLOY"); // a pull is allowed at any height
            }
            else if (Dive.Current == Skydive.State.Opening)
            {
                dropHud.Prompt("CANOPY OPENING");
                Gm.hud.touch.HideAction();
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

        // Body, pose, chute and camera rig follow the flight model.
        void PlaceDiver(float dt)
        {
            var yawQ = Quaternion.Euler(0, Dive.Heading, 0);
            Quaternion body;
            if (Dive.Current == Skydive.State.Freefall || !LinesTaut)
            {
                // Off the door: a short tumble that settles into the arch within half a second.
                exitTime += dt;
                float settle = 1 - Mathf.Exp(-exitTime * 7);
                float arch = 90 + trackIn * 12; // W: head-down dive, S: flatter brake
                float pitch = Mathf.Lerp(35, arch, settle) + 24 * Mathf.Sin(exitTime * 10) * Mathf.Exp(-exitTime * 7);
                // Lean into turns (a roll about his head-to-toe axis), plus the exit wobble.
                float lean = -turn * 26 + 16 * Mathf.Sin(exitTime * 8 + 1) * Mathf.Exp(-exitTime * 6);
                BodyPitch = pitch;
                body = yawQ * Quaternion.AngleAxis(pitch, Vector3.right) * Quaternion.AngleAxis(lean, Vector3.up);
                Vector3 c = Dive.Position;
                Warden.SetPositionAndRotation(c - body * Vector3.up * BodyCenter, body);
                pose.freefall = Mathf.Clamp01(exitTime * 3);
                pose.canopy = 0;
                pose.track = trackIn;
                if (Dive.Current != Skydive.State.Freefall && Dive.SinceDeploy >= Skydive.SnatchDelay) LineStretch();
            }
            else
            {
                // Hanging from the risers: a damped pendulum about the shoulders swings him from
                // horizontal to upright; turns lean him out, the flare kicks his legs forward.
                swing.Step(dt, 11, 1.7f, Dive.Flared ? -20 : 0);
                sway.Step(dt, 5, 1.4f, -turn * 14);
                BodyPitch = swing.Angle;
                body = yawQ * Quaternion.AngleAxis(swing.Angle, Vector3.right) * Quaternion.AngleAxis(sway.Angle, Vector3.forward);
                Vector3 harness = Dive.Position + Vector3.up * (Shoulders - BodyCenter);
                Warden.SetPositionAndRotation(harness - body * Vector3.up * Shoulders, body);
                float upright = 1 - Mathf.Clamp01(Mathf.Abs(swing.Angle) / 80);
                pose.freefall = 1 - upright;
                pose.canopy = upright;
                pose.track = 0;
                pose.toggleLeft = Dive.Flared ? 1 : Mathf.Max(0, -turn) * 0.8f + Mathf.Max(0, -trackIn) * 0.4f;
                pose.toggleRight = Dive.Flared ? 1 : Mathf.Max(0, turn) * 0.8f + Mathf.Max(0, -trackIn) * 0.4f;
            }
            diveRig.SetPositionAndRotation(Dive.Position, yawQ);
            if (parachute.gameObject.activeSelf) PlaceChute(yawQ);
        }

        // The lines come tight: the opening jolt.
        void LineStretch()
        {
            LinesTaut = true;
            swing.Angle = BodyPitch;
            swing.Speed = 0;
            shake = 1;
            sfx.PlayOneShot(SynthAudio.Snap, 0.9f);
            Show(canopyCam, 0.6f);
        }

        // Pilot chute out, the canopy pulled out of the bag, then unfurling and inflating over
        // about a second after the lines stretch, with a little overshoot.
        void PlaceChute(Quaternion yawQ)
        {
            float t = Dive.SinceDeploy;
            Vector3 harness = Dive.Position + Vector3.up * (Shoulders - BodyCenter);
            float snatch = Skydive.SnatchDelay;
            float k = Mathf.Clamp01((t - snatch) / 1.0f);
            float grow = Mathf.SmoothStep(0, 1, k);
            float over = Mathf.Sin(k * Mathf.PI) * 0.12f;
            // Out of the bag: the canopy streams up behind him before the lines are tight.
            float reach = t < snatch ? Mathf.Lerp(1.5f, LineLength, t / snatch) : LineLength;
            Vector3 up = Vector3.up;
            parachute.SetPositionAndRotation(harness + up * reach, yawQ * Quaternion.Euler(-4, 0, sway.Angle * 0.25f));
            parachute.localScale = new Vector3(Mathf.Lerp(0.1f, 1, grow) + over, Mathf.Lerp(0.3f, 1, grow), Mathf.Lerp(0.15f, 1, grow) + over * 0.5f);
            // The pilot chute leads the way, then rides above the canopy until it's open.
            pilotChute.gameObject.SetActive(t < snatch + 1.2f);
            float pc = Mathf.Clamp01(t / 0.3f);
            pilotChute.position = harness + up * (Mathf.Lerp(0.5f, 3, pc) + (t < snatch ? reach : LineLength + 1.8f));
            UpdateLines();
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

        // Camera: wider and shakier the faster he falls; the opening jolt kicks it. Sound:
        // wind rising with speed, flutter under the canopy, the chopper far away.
        void UpdateDiveFeel(float dt)
        {
            float speed = Dive.Velocity.magnitude;
            float v = Mathf.Clamp01(speed / Skydive.Terminal);
            shake = Mathf.MoveTowards(shake, 0, dt * 1.6f);
            bool free = Dive.Current == Skydive.State.Freefall || !LinesTaut;
            var lens = freefallCam.Lens;
            lens.FieldOfView = 56 + 16 * v;
            freefallCam.Lens = lens;
            SetNoise(freefallCam, 0.25f + 0.9f * v + 2.5f * shake);
            SetNoise(canopyCam, 0.15f + 3f * shake);
            var em = windStreaks.emission;
            em.rateOverTime = free ? 60 * v : 0;
            wind.volume = Mathf.Lerp(wind.volume, free ? 0.12f + 0.5f * v : 0.1f, Mathf.Min(1, dt * 3));
            wind.pitch = free ? 0.7f + 0.5f * v : 0.75f;
            flutter.volume = Mathf.Lerp(flutter.volume, LinesTaut ? 0.06f + 0.25f * shake : 0, Mathf.Min(1, dt * 4));
        }

        static void SetNoise(CinemachineCamera cam, float amplitude)
        {
            var noise = cam.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (noise != null) noise.AmplitudeGain = amplitude;
        }

        void Land()
        {
            CurrentPhase = Phase.Landing;
            phaseTime = 0;
            float damage = Skydive.LandingDamage(Dive.TouchdownSpeed);
            var s = Gm.Run.Stats;
            // Hard landings hurt but never kill.
            LandingDamage = Mathf.Min(damage, Mathf.Max(0, s.Health - 1));
            HardLanding = damage > 0;
            s.Health -= LandingDamage;
            runDir = Quaternion.Euler(0, Dive.Heading, 0) * Vector3.forward;
            float ground = Flat(Dive.Velocity).magnitude;
            if (HardLanding)
            {
                // Feet, knees, roll: he folds into a crouch, a hand down, and gets back up.
                Gm.hud.Banner($"HARD LANDING  ·  -{Mathf.CeilToInt(LandingDamage)} HP", 2.2f, true);
                Player.view.OnHurt();
                Effects.Instance.PlayerHurt(Dive.Position);
                Gm.PlaySound(Gm.hurtSound, 0.6f);
                sfx.PlayOneShot(SynthAudio.Roll, 0.9f);
                runSpeed = Mathf.Min(ground, 4);
                shake = 1;
            }
            else
            {
                Gm.hud.Banner(Dive.Flared ? "PERFECT LANDING" : "LANDED", 1.8f, GameConfig.Hex(0x9bff67));
                // A few running steps to bleed off the canopy's forward speed.
                runSpeed = Mathf.Clamp(ground, 2.5f, 5.5f);
            }
            sfx.PlayOneShot(SynthAudio.Thud, HardLanding ? 1 : 0.6f);
            Gm.hud.SetHealth(s.Health, s.MaxHealth);
            pose.freefall = pose.canopy = pose.toggleLeft = pose.toggleRight = 0;
            pose.crouch = HardLanding ? 1 : 0;
            // Gameplay keeps the root unrotated and turns the model to the aim.
            Vector3 feet = Dive.Position - Vector3.up * BodyCenter;
            Warden.SetPositionAndRotation(feet, Quaternion.identity);
            Player.SetAim(runDir);
            ((WardenModelView)Player.view).SnapYaw(Dive.Heading);
            // Cut away: the chute surges on past him and collapses in a heap.
            chuteDrift = runDir * 3 + Vector3.down * 0.5f;
            chuteGone = 0;
            pilotChute.gameObject.SetActive(false);
            foreach (var line in lines) line.enabled = false;
            dropHud.Show(false);
            Gm.hud.touch.HideAction();
            ambienceTarget = 0.35f;

            // Settle on the gameplay view: a still camera where CameraFollow will be once he
            // has stopped.
            Vector3 stop = feet + runDir * (HardLanding ? runSpeed * 0.3f : runSpeed * 0.55f);
            settleCam.transform.position = stop + Gm.cameraFollow.offset;
            settleCam.transform.LookAt(stop + new Vector3(0, 0, GameConfig.CameraLookAhead));
            Show(settleCam, 2f);
        }

        void TickLanding(float dt)
        {
            // Run-out (soft) or a short skid (hard), slowing to a stop.
            runSpeed = Mathf.MoveTowards(runSpeed, 0, dt * (HardLanding ? 10 : 5));
            Vector3 p = Warden.position + runDir * runSpeed * dt;
            p = World.PushOutOfTrunks(GameConfig.ResolveCircle(p, GameConfig.PlayerRadius), GameConfig.PlayerRadius);
            float crouch = HardLanding ? Mathf.Clamp01(1.6f - phaseTime) : 0;
            pose.crouch = crouch;
            p.y = Ground(p) - 0.55f * crouch; // the folded legs would lift his feet off the ground
            Warden.position = p;
            Player.cinematicMove = !HardLanding && runSpeed > 0.5f ? runDir : Vector3.zero;
            wind.volume = Mathf.MoveTowards(wind.volume, 0, dt * 0.3f);
            flutter.volume = Mathf.MoveTowards(flutter.volume, 0, dt * 0.3f);
            shake = Mathf.MoveTowards(shake, 0, dt * 2);
            SetNoise(settleCam, 1.2f * shake);
            if (phaseTime >= SettleTime) BeginWalk();
        }

        // ------------------------------------------------------------------ on foot

        void BeginWalk()
        {
            CurrentPhase = Phase.Walk;
            brain.enabled = false;
            foreach (var c in new[] { chaseCam, passCam, doorCam, freefallCam, canopyCam, settleCam }) c.gameObject.SetActive(false);
            pose.crouch = 0;
            Player.cinematicMove = Vector3.zero;
            Warden.position = new Vector3(Warden.position.x, Ground(Warden.position), Warden.position.z);
            if (rifle != null) rifle.gameObject.SetActive(true);
            ((WardenModelView)Player.view).handIK.weight = 1;
            ShowGroundMarks(true);
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
            ambienceTarget = 0.35f;
            if (secured)
            {
                // Continued at checkpoint A, the end of this slice: say so, then show the
                // milestone result with its way back to the menu.
                MarkSecured();
                completeShown = false;
                phaseTime = 0;
                Gm.hud.Banner("CHECKPOINT A", 3, GameConfig.Hex(0x9bff67));
                hud.SetObjective("STAGE 2 CONTINUES IN THE NEXT MILESTONE");
            }
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
            if (completeShown || phaseTime < CompleteDelay + 0.8f) return;
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
                case Phase.GreenLight: TickGreenLight(dt); break;
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

        // After the cut-away the chute slides back, sags flat and sinks into the undergrowth.
        void UpdateChuteRelease(float dt)
        {
            if (CurrentPhase != Phase.Landing && CurrentPhase != Phase.Walk || !parachute.gameObject.activeSelf) return;
            chuteGone += dt;
            Vector3 p = parachute.position + chuteDrift * dt;
            float ground = Ground(p) + 0.3f;
            p.y = Mathf.MoveTowards(p.y, ground, dt * Mathf.Lerp(2, 6, chuteGone / 2));
            parachute.position = p;
            float sag = Mathf.Clamp01(chuteGone / 1.6f);
            float fade = Mathf.Clamp01((chuteGone - 1.5f) / 2f);
            // Fabric folds in: narrower, flatter and shorter as it settles, then sinks away
            // into the undergrowth.
            parachute.localScale = new Vector3(Mathf.Lerp(1, 0.55f, sag), Mathf.Lerp(1, 0.35f, sag), Mathf.Lerp(1, 0.6f, sag)) * (1 - fade);
            if (fade > 0) parachute.position -= Vector3.up * fade * dt * 2;
            chuteDrift *= Mathf.Exp(-dt * 0.7f);
            if (fade >= 1) parachute.gameObject.SetActive(false);
        }

        // Thicker haze near the ground, clear air up high (the drop starts at ~900 m).
        void LateUpdate()
        {
            var cam = Player.cam.transform.position;
            float h = cam.y - Ground(cam);
            RenderSettings.fogDensity = Mathf.Lerp(groundFog, groundFog * 0.025f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(15, 160, h)));
            ambience.volume = Mathf.MoveTowards(ambience.volume, ambienceTarget, Time.unscaledDeltaTime * 0.12f);
        }

        // ------------------------------------------------------------------ helpers

        void ShowGroundMarks(bool on)
        {
            foreach (var g in groundMarks) g.SetActive(on);
        }

        void Show(CinemachineCamera cam, float blend)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blend);
            foreach (var c in new[] { chaseCam, passCam, doorCam, freefallCam, canopyCam, settleCam })
                c.gameObject.SetActive(c == cam);
        }

        // Switch without a blend (the first shot, a skip).
        void Cut(CinemachineCamera cam)
        {
            Show(cam, 0);
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
        }

        bool SkipPressed()
        {
            var kb = Keyboard.current;
            return KeyPressed(kb?.spaceKey) || KeyPressed(kb?.enterKey) || Gm.hud.touch.ConsumeSkip();
        }

        static bool KeyPressed(UnityEngine.InputSystem.Controls.KeyControl key) => key != null && key.wasPressedThisFrame;

        float Altitude(Vector3 p) => p.y - Ground(p);

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        // Test hooks.
        public void SkipToJump() => SkipFlight();
        public void JumpNow() => Jump();
    }
}
