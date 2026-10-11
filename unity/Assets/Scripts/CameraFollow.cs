using System.Runtime.InteropServices;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace WardenZero
{
    // The gameplay camera, in one of two views. V (or the touch VIEW button) switches them, and
    // the choice is remembered per stage:
    // - High: eases toward a point behind and above the Warden, plus a decaying shake
    //   (Stage.follow() in the Babylon version). Twin-stick aiming at the cursor.
    // - Behind: over his right shoulder. A Cinemachine camera (ThirdPersonFollow) follows a
    //   pivot at his upper chest that the mouse (pointer lock, from a click) or a right-thumb
    //   drag turns; CameraCollision keeps it out of trunks, rocks, walls and the ground. He
    //   aims with the centre crosshair. Losing the pointer lock (Esc) pauses the game.
    //   Jungle plants (FoliageLit) fade near the lens and in a hole around him. In the jungle,
    //   platforming (a move other than standing, or an edge within 3 m) eases in a framing
    //   that centres the climb and sees the top: shoulder in to 0.4 m, 0.6 m further back,
    //   6 degrees more pitch; hanging, the yaw drifts back to the wall after 1 s without look.
    // Set pieces disable this component and drive the CinemachineBrain with their own cameras.
    [DefaultExecutionOrder(-10)] // look input before the Warden moves; the pivot before the brain
    public class CameraFollow : MonoBehaviour
    {
        public enum View { High, Behind }

        public Transform target;
        // Main menu: slow drift over the empty arena instead of following the Warden.
        public bool attract;
        // Behind and above the Warden; the jungle sits lower, under the tree crowns.
        public Vector3 offset = GameConfig.CameraOffset;

        [Header("Behind view")]
        public CinemachineBrain brain;
        public CinemachineCamera shoulderCam;
        public CameraCollision collision;
        public Transform pivot; // the shoulder camera's follow target
        public Volume behindFill; // the arena's exposure lift for the behind view (or none)
        // This stage's PlayerPrefs key for the chosen view; empty keeps the high view (Greenfang).
        public string viewKey;
        public View defaultView;

        public View Current { get; private set; }
        public bool IsBehind => Current == View.Behind && !attract && isActiveAndEnabled;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; } = GameConfig.BehindPitch;
        public float Framing => Mathf.SmoothStep(0, 1, framing); // 0 explore .. 1 platforming
        // The pivot's pitch: the look plus the platforming tilt.
        public float ViewPitch => Pitch + PlatformPitch * Framing;

        public const float PlatformShoulder = 0.4f, PlatformBack = 0.6f, PlatformPitch = 6;
        public const float PlatformRange = 3; // an edge this near frames for platforming
        public const float RecentreAfter = 1; // seconds without look input while hanging
        public string PrefsKey => "wz.view." + viewKey;

        static readonly int FadeId = Shader.PropertyToID("_WZFade");
        static readonly int FadeTargetId = Shader.PropertyToID("_WZFadeTarget");
        static readonly int FadeSizeId = Shader.PropertyToID("_WZFadeSize");

        Camera cam;
        CinemachineThirdPersonFollow rig;
        float highFov, highNear, highFar;
        Vector3 lookPoint;
        float shake;
        float lastLook = -10;
        float framing; // linear, toward the context
        bool lockWanted, hadLock;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern int WZ_PointerLocked();
        public static bool PointerLocked => WZ_PointerLocked() == 1;
#else
        public static bool PointerLocked => Cursor.lockState == CursorLockMode.Locked;
#endif

        void Awake()
        {
            cam = GetComponent<Camera>();
            highFov = cam.fieldOfView;
            highNear = cam.nearClipPlane;
            highFar = cam.farClipPlane;
            Current = string.IsNullOrEmpty(viewKey) ? View.High : (View)PlayerPrefs.GetInt(PrefsKey, (int)defaultView);
            if (shoulderCam != null)
            {
                rig = shoulderCam.GetComponent<CinemachineThirdPersonFollow>();
                var lens = shoulderCam.Lens;
                lens.FieldOfView = GameConfig.BehindFov;
                lens.NearClipPlane = GameConfig.BehindNearClip;
                lens.FarClipPlane = highFar;
                shoulderCam.Lens = lens;
            }
        }

        void Start()
        {
            Snap();
        }

        public bool CanSwitch => !string.IsNullOrEmpty(viewKey) && shoulderCam != null;

        public void Toggle() => SetView(Current == View.High ? View.Behind : View.High);

        public void SetView(View v)
        {
            if (!CanSwitch || v == Current) return;
            Current = v;
            PlayerPrefs.SetInt(PrefsKey, (int)v);
            PlayerPrefs.Save();
            Snap();
        }

        // Turn the behind view by degrees (mouse, touch drag, tests).
        public void Look(float yaw, float pitch)
        {
            Yaw = Mathf.Repeat(Yaw + yaw, 360);
            Pitch = Mathf.Clamp(Pitch + pitch, GameConfig.MinPitch, GameConfig.MaxPitch);
        }

        public void SetLook(float yaw, float pitch)
        {
            Yaw = 0;
            Pitch = 0;
            Look(yaw, pitch);
        }

        // Jump straight to the view's resting place (a new run, the end of a set piece). The
        // behind view starts looking where the Warden faces.
        public void Snap()
        {
            if (target == null) return;
            var player = target.GetComponent<PlayerController>();
            Vector3 aim = player != null ? player.AimDirection : Vector3.forward;
            SetLook(Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg, GameConfig.BehindPitch);
            framing = Platforming(player) ? 1 : 0;
            if (pivot != null) PlacePivot();
            if (shoulderCam != null) shoulderCam.PreviousStateIsValid = false;
            if (collision != null) collision.ResetState();
            transform.position = target.position + offset;
            lookPoint = target.position + new Vector3(0, 0, GameConfig.CameraLookAhead);
            transform.LookAt(lookPoint);
        }

        // The behind view's rig for a screen shape. Portrait screens keep a usable width of
        // view: a wider lens, the camera nearer his back line and a little further out, so he
        // isn't a giant in the corner.
        public static (Vector3 shoulder, float distance, float fov) Rig(float aspect, float framing = 0)
        {
            float narrow = Mathf.Clamp01(1 - aspect);
            var shoulder = new Vector3(GameConfig.ShoulderOffset.x * (1 - narrow * 0.6f), GameConfig.ShoulderOffset.y, 0);
            shoulder.x = Mathf.Lerp(shoulder.x, Mathf.Min(shoulder.x, PlatformShoulder), framing);
            float fov = aspect >= 1 ? GameConfig.BehindFov : Mathf.Min(75, Camera.HorizontalToVerticalFieldOfView(58, aspect));
            return (shoulder, GameConfig.BehindDistance + narrow * 2 + PlatformBack * framing, fov);
        }

        // Where the behind view would put the camera for the Warden standing at `feet` and
        // looking along yaw and pitch (no collision): the landing settles onto it, and the
        // crosshair's ray runs along it.
        public static Pose BehindPose(Vector3 feet, float yaw, float aspect, float pitch = GameConfig.BehindPitch, float framing = 0)
        {
            var (offset, distance, _) = Rig(aspect, framing);
            var rot = Quaternion.Euler(pitch, yaw, 0);
            Vector3 shoulder = feet + Vector3.up * GameConfig.PivotHeight + Quaternion.Euler(0, yaw, 0) * offset;
            return new Pose(shoulder - rot * Vector3.forward * distance, rot);
        }

        // The crosshair's ray for this frame's look and the Warden where he stands now (the
        // camera itself only moves in LateUpdate, a frame late for aiming). Collision pulls the
        // camera in along this same line, so the ray is the same.
        public Ray AimRay()
        {
            var pose = BehindPose(target.position, Yaw, cam.aspect, ViewPitch, Framing);
            return new Ray(pose.position, pose.rotation * Vector3.forward);
        }

        // Esc releases the pointer lock in the browser, and the game pauses. Some browsers
        // also pass the Esc key on a frame later, which would toggle the pause straight back
        // off; GameManager ignores Esc for this long after such a pause.
        public const float EscGuard = 0.35f;
        public static float LockLostAt { get; private set; } = -1;

        public static bool EscapeMayResume(float now, float lockLostAt) => lockLostAt < 0 || now - lockLostAt >= EscGuard;

        public void AddShake(float amount)
        {
            shake = Mathf.Max(shake, amount);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying || attract) return;
            var kb = Keyboard.current;
            var touch = gm.hud != null ? gm.hud.touch : null;
            bool toggle = (kb != null && kb.vKey.wasPressedThisFrame) | (touch != null && touch.ConsumeView());
            if (toggle) Toggle();
            if (Current != View.Behind) return;
            var mouse = Mouse.current;
            if (mouse != null && PointerLocked)
            {
                Vector2 d = mouse.delta.ReadValue() * GameConfig.MouseLook;
                Look(d.x, -d.y);
                if (d.sqrMagnitude > 1e-4f) lastLook = Time.time;
            }
            if (touch != null && TouchControls.Active)
            {
                Vector2 d = touch.ConsumeLook() * GameConfig.TouchLook;
                Look(d.x, -d.y);
                if (d.sqrMagnitude > 1e-4f) lastLook = Time.time;
            }
        }

        void LateUpdate()
        {
            if (target == null) return;
            var gm = GameManager.Instance;
            bool behind = Current == View.Behind && !attract;
            SetRig(behind);
            bool playing = gm != null && gm.IsPlaying;
            UpdateCursorLock(behind && playing);
            // The see-through silhouette is for the high view (walls and crowns between him and
            // the camera); behind him, camera collision and the foliage fade keep him in sight.
            if (playing && gm.player.view is WardenModelView view) view.SetXRay(!behind);
            if (gm != null && gm.hud != null)
            {
                gm.hud.touch.LookMode = behind;
                gm.hud.SetLookHint(behind && playing && !TouchControls.Active && !PointerLocked);
            }
            float dt = Time.unscaledDeltaTime;
            if (behindFill != null) behindFill.weight = Mathf.MoveTowards(behindFill.weight, behind ? 1 : 0, dt * 2);
            if (Time.timeScale == 0) return;
            if (shake > 0) shake = Mathf.Max(0, shake - dt * 1.8f);
            if (behind)
            {
                var player = target.GetComponent<PlayerController>();
                framing = Mathf.MoveTowards(framing, Platforming(player) ? 1 : 0, dt / 0.5f);
                Recentre(player, dt);
                PlacePivot();
                collision.shake = Random.insideUnitSphere * shake * 0.12f;
                var (offset, distance, fov) = Rig(cam.aspect, Framing);
                var lens = shoulderCam.Lens;
                lens.FieldOfView = fov;
                shoulderCam.Lens = lens;
                if (rig != null)
                {
                    rig.ShoulderOffset = offset;
                    rig.CameraDistance = distance;
                }
                return;
            }
            Vector3 focus = target.position;
            if (attract)
            {
                float a = Time.unscaledTime / 9;
                focus = new Vector3(Mathf.Sin(a) * 18, 0, Mathf.Cos(a * 0.7f) * 10);
            }
            Vector3 want = focus + offset;
            transform.position = Vector3.Lerp(transform.position, want, Mathf.Min(1, dt * 6));
            Vector3 wantLook = focus + new Vector3(0, 0, GameConfig.CameraLookAhead);
            lookPoint = Vector3.Lerp(lookPoint, wantLook, Mathf.Min(1, dt * 8));
            if (shake > 0) transform.position += Random.insideUnitSphere * shake * 0.35f;
            transform.LookAt(lookPoint);
        }

        void PlacePivot()
        {
            pivot.SetPositionAndRotation(target.position + Vector3.up * GameConfig.PivotHeight, Quaternion.Euler(ViewPitch, Yaw, 0));
        }

        // Jungle traversal: on the move, or an edge within reach of a few steps.
        static bool Platforming(PlayerController player)
        {
            if (!World.TraversalEnabled || player == null) return false;
            var t = player.Traversal;
            return !t.Grounded || TraversalProbe.Near(t.Position, PlatformRange);
        }

        // Hanging or climbing with no look input for a moment: the yaw drifts round to face
        // the wall, gently.
        void Recentre(PlayerController player, float dt)
        {
            if (player == null || !World.TraversalEnabled || Time.time - lastLook < RecentreAfter) return;
            var t = player.Traversal;
            if (t.State != TraversalState.Hang && t.State != TraversalState.WallClimb) return;
            float want = Mathf.Atan2(t.Facing.x, t.Facing.z) * Mathf.Rad2Deg;
            Yaw = Mathf.Repeat(Mathf.LerpAngle(Yaw, want, 1 - Mathf.Exp(-1.2f * dt)), 360);
        }

        // The behind view hands the camera to the brain and the shoulder camera; the high view
        // takes it back (with its own lens).
        void SetRig(bool on)
        {
            if (shoulderCam == null) return;
            if (shoulderCam.gameObject.activeSelf != on)
            {
                shoulderCam.gameObject.SetActive(on);
                if (on)
                {
                    PlacePivot();
                    shoulderCam.PreviousStateIsValid = false;
                    collision.ResetState();
                }
            }
            if (brain.enabled != on)
            {
                brain.enabled = on;
                if (!on)
                {
                    cam.fieldOfView = highFov;
                    cam.nearClipPlane = highNear;
                    cam.farClipPlane = highFar;
                }
            }
        }

        // The mouse turns the behind view only with the pointer locked. Browsers grant the lock
        // on a click, so asking is enough; Esc releases it, which pauses like Esc does.
        void UpdateCursorLock(bool want)
        {
            want &= !TouchControls.Active;
            if (want && !lockWanted)
            {
                // Unity may still think it holds a lock the browser dropped: ask afresh.
                Cursor.lockState = CursorLockMode.None;
                Cursor.lockState = CursorLockMode.Locked;
            }
            if (!want && lockWanted)
            {
                Cursor.lockState = CursorLockMode.None;
                hadLock = false;
            }
            lockWanted = want;
            if (!want) return;
            if (PointerLocked) hadLock = true;
            else if (hadLock)
            {
                hadLock = false;
                lockWanted = false;
                PauseForLostLock();
            }
        }

        // Test hook too: what the browser's Esc does to the pointer lock.
        public void PauseForLostLock()
        {
            LockLostAt = Time.unscaledTime;
            GameManager.Instance.Pause(true);
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += SetFoliageFade;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= SetFoliageFade;
            Shader.SetGlobalVector(FadeId, Vector4.zero);
            if (lockWanted) Cursor.lockState = CursorLockMode.None;
            lockWanted = hadLock = false;
        }

        // For the game camera in the behind view: plants fade from 1.8 m in to 0.7 m from the
        // lens, and in an ellipse around the Warden (clear across about 1.6 m by 2.9 m where
        // he stands, centred at his waist) when they are in front of him. Off for every other
        // camera and view.
        void SetFoliageFade(ScriptableRenderContext context, Camera c)
        {
            bool on = c == cam && Current == View.Behind && !attract && target != null;
            FoliageFade(on, on ? target.position + Vector3.up * 1.3f : Vector3.zero, 1.8f);
        }

        // The FoliageLit globals: plants fade from `nearTo` metres in to 0.7 m from the lens,
        // and in an ellipse around `centre` (the Warden's waist) when they are in front of it.
        public static void FoliageFade(bool on, Vector3 centre, float nearTo)
        {
            if (!on)
            {
                Shader.SetGlobalVector(FadeId, Vector4.zero);
                return;
            }
            Shader.SetGlobalVector(FadeId, new Vector4(1, 0.7f, nearTo, 0));
            Shader.SetGlobalVector(FadeTargetId, centre);
            Shader.SetGlobalVector(FadeSizeId, new Vector4(0.9f, 1.6f, 0, 0));
        }
    }
}
