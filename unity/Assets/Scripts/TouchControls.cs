using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WardenZero
{
    // Touch scheme from src/systems/Input.ts: drag anywhere (outside the buttons) for a
    // virtual stick, DASH and BOMB buttons on the right that fire on touch-down. Aiming and
    // firing are automatic on touch in the high view (PlayerController reads Active). In the
    // behind view (LookMode) the stick takes the left half of the screen, a drag on the right
    // half turns the camera, and FIRE fires while held. VIEW switches the camera. A quick tap
    // that starts and ends inside one frame still counts, through the touch's tap control.
    // In the jungle the context button is the traversal JUMP (labelled JUMP, VAULT, CLIMB or
    // UP; held, it keeps climbing), and DROP shows while he can let go or lower over an edge.
    // In the jeep (M3) the same buttons change jobs (Scheme, TouchLayout): driving, GAS and BRAKE
    // and the handbrake are held, EXIT and RESET show only when they apply, the stick's x
    // steers and a right-half drag orbits the chase camera; on the gun, FIRE is held and ZOOM
    // toggles, and a drag anywhere aims.
    public class TouchControls : MonoBehaviour
    {
        // Tests and desktop debugging can force the touch scheme on.
        public static bool ForceTouch;
        public static bool Active => ForceTouch || IsTouchDevice;

        // Babylon's test: a coarse primary pointer (phones, tablets, iPadOS Safari).
        // Checked once; desktop browsers report a fine pointer.
        static bool? touchDevice;
        static bool IsTouchDevice => touchDevice ??= DetectTouch();

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern int WZ_IsCoarsePointer();
        static bool DetectTouch() => WZ_IsCoarsePointer() == 1 || Application.isMobilePlatform;
#else
        static bool DetectTouch() => Application.isMobilePlatform;
#endif

        const float StickTravel = 52; // canvas units, like Babylon's 52 px

        public RectTransform canvasRect;
        public RectTransform joy;
        public RectTransform knob;
        public RectTransform dashButton;
        public RectTransform bombButton;
        // Campaign set pieces: one context button (BOARD, JUMP, DEPLOY, FLARE) and SKIP.
        public RectTransform actionButton;
        public Text actionLabel;
        public RectTransform skipButton;
        public RectTransform fireButton; // behind view: hold to fire
        public RectTransform viewButton; // high view / behind view
        public RectTransform dropButton; // jungle: let go, or lower over an edge
        public GameObject viewBadge; // "AUTO" on VIEW while the context camera is in charge
        public CanvasGroup dashGroup;
        public CanvasGroup bombGroup;
        public GameObject[] desktopOnly; // ability chips: hidden on touch, as in Babylon

        public Vector2 Stick { get; private set; }
        public TouchLayout.Scheme Scheme { get; private set; }
        public bool FireHeld { get; private set; }
        public bool DashHeld { get; private set; } // driving: the handbrake
        public bool ActionHeld { get; private set; } // a touch that started on the context button is down
        // Set by CameraFollow each frame: the behind view splits the screen into stick and look.
        [System.NonSerialized] public bool LookMode;

        int stickId = -1;
        Vector2 origin;
        bool dashPressed;
        bool bombPressed;
        bool actionPressed;
        bool skipPressed;
        bool viewPressed;
        bool dropPressed;
        int lookId = -1;
        Vector2 laidOutCanvas;
        Rect laidOutSafe;
        Vector2 lookLast;
        Vector2 look;
        // Each touch triggers a button at most once (its press, or its tap on release). Keyed by
        // the touch's start time: browsers reuse a finished touch's id for the next one.
        // The last few, so a release-tap of one button after another button's press still
        // counts as the same touch.
        readonly double[] handled = { -1, -1, -1, -1 };
        int nextHandled;

        public void PressDash() => dashPressed = true;
        public void PressBomb() => bombPressed = true;
        public void PressAction() => actionPressed = true;
        public void PressSkip() => skipPressed = true;
        public void PressView() => viewPressed = true;
        public void PressDrop() => dropPressed = true;

        // Presses made while paused or on the upgrade picker are dropped (Babylon clearPresses).
        public void ClearPresses()
        {
            dashPressed = bombPressed = actionPressed = skipPressed = viewPressed = dropPressed = false;
            look = Vector2.zero;
        }

        // The context button, on touch screens only (desktop uses keys).
        public void ShowAction(string label)
        {
            actionLabel.text = label;
            actionButton.gameObject.SetActive(Active);
            actionPressed = false;
        }

        // For per-frame callers: shows it if hidden; if already up, only the label changes, so
        // a pending press is kept (the traversal JUMP relabels as he moves).
        public void ShowActionOnce(string label)
        {
            if (actionButton.gameObject.activeSelf == Active)
            {
                if (actionLabel.text != label) actionLabel.text = label;
                return;
            }
            ShowAction(label);
        }

        public void HideAction()
        {
            actionButton.gameObject.SetActive(false);
            actionPressed = false;
        }

        // The jeep's buttons: the same slots with other names (labels kept for going back).
        public void SetScheme(TouchLayout.Scheme scheme)
        {
            if (scheme == Scheme) return;
            if (labels == null)
            {
                buttons = new[] { actionButton, fireButton, dropButton, dashButton, bombButton };
                labels = System.Array.ConvertAll(buttons, b => b.Find("Label").GetComponent<Text>());
                onFootLabels = System.Array.ConvertAll(labels, l => l.text);
            }
            Scheme = scheme;
            string[] names = scheme == TouchLayout.Scheme.Drive ? DriveLabels : scheme == TouchLayout.Scheme.Gun ? GunLabels : onFootLabels;
            for (int i = 0; i < labels.Length; i++) labels[i].text = names[i];
            ClearPresses();
            bool on = Active && scheme != TouchLayout.Scheme.Gun;
            dashButton.gameObject.SetActive(on);
            bombButton.gameObject.SetActive(on && scheme == TouchLayout.Scheme.OnFoot);
            dropButton.gameObject.SetActive(Active && scheme == TouchLayout.Scheme.Gun); // ZOOM
            actionButton.gameObject.SetActive(Active && scheme != TouchLayout.Scheme.OnFoot);
            dashGroup.alpha = bombGroup.alpha = 1;
        }

        static readonly string[] DriveLabels = { "GAS", "BRAKE", "EXIT", "HANDBRAKE", "RESET" };
        static readonly string[] GunLabels = { "FIRE", "", "ZOOM", "", "" };
        RectTransform[] buttons;
        Text[] labels;
        string[] onFootLabels;

        // RESET while driving (BOMB's slot), only when it applies.
        public void ShowBomb(bool on)
        {
            on &= Active;
            if (bombButton.gameObject.activeSelf == on) return;
            bombButton.gameObject.SetActive(on);
            bombPressed = false;
        }

        public void ShowDrop(bool on)
        {
            on &= Active;
            if (dropButton.gameObject.activeSelf == on) return;
            dropButton.gameObject.SetActive(on);
            dropPressed = false;
        }

        public bool ConsumeDrop()
        {
            bool p = dropPressed;
            dropPressed = false;
            return p;
        }

        public void ShowSkip(bool on)
        {
            skipButton.gameObject.SetActive(on && Active);
            skipPressed = false;
        }

        public bool ConsumeAction()
        {
            bool p = actionPressed;
            actionPressed = false;
            return p;
        }

        public bool ConsumeSkip()
        {
            bool p = skipPressed;
            skipPressed = false;
            return p;
        }

        public bool ConsumeDash()
        {
            bool p = dashPressed;
            dashPressed = false;
            return p;
        }

        public bool ConsumeBomb()
        {
            bool p = bombPressed;
            bombPressed = false;
            return p;
        }

        public bool ConsumeView()
        {
            bool p = viewPressed;
            viewPressed = false;
            return p;
        }

        // Canvas units dragged on the look side since the last call.
        public Vector2 ConsumeLook()
        {
            Vector2 d = look;
            look = Vector2.zero;
            return d;
        }

        // TouchLayout's places for this screen shape and safe area (redone when either changes).
        void Layout()
        {
            Vector2 canvas = canvasRect.rect.size;
            Rect safe = Screen.safeArea;
            if (canvas == laidOutCanvas && safe == laidOutSafe || Screen.width <= 0) return;
            laidOutCanvas = canvas;
            laidOutSafe = safe;
            float k = canvas.x / Screen.width;
            var insets = new Vector4(safe.xMin * k, safe.yMin * k, (Screen.width - safe.xMax) * k, (Screen.height - safe.yMax) * k);
            var places = TouchLayout.Compute(canvas, insets);
            void Put(RectTransform rt, TouchLayout.Button b)
            {
                var p = places[b];
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0, 1); // the button scales from its top-left corner
                rt.anchoredPosition = new Vector2(p.Rect.x, p.Rect.yMax);
                rt.localScale = Vector3.one * p.Scale;
            }
            Put(actionButton, TouchLayout.Button.Jump);
            Put(fireButton, TouchLayout.Button.Fire);
            Put(dropButton, TouchLayout.Button.Drop);
            Put(dashButton, TouchLayout.Button.Dash);
            Put(bombButton, TouchLayout.Button.Bomb);
            Put(viewButton, TouchLayout.Button.View);
            Put(skipButton, TouchLayout.Button.Skip);
        }

        void OnEnable()
        {
            bool on = Active;
            laidOutCanvas = Vector2.zero;
            dashButton.gameObject.SetActive(on);
            bombButton.gameObject.SetActive(on);
            foreach (var go in desktopOnly) go.SetActive(!on);
            actionButton.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(false);
            fireButton.gameObject.SetActive(false);
            viewButton.gameObject.SetActive(false);
            dropButton.gameObject.SetActive(false);
            Release();
            ClearPresses();
        }

        void Update()
        {
            if (!Active) return;
            Layout();
            var gm = GameManager.Instance;
            bool playing = gm != null && gm.player.isActiveAndEnabled && gm.IsPlaying;
            bool onFoot = Scheme == TouchLayout.Scheme.OnFoot;
            if (playing && onFoot)
            {
                dashGroup.alpha = gm.player.DashReady < 1 ? 0.35f : 1;
                bombGroup.alpha = gm.player.BombReady < 1 ? 0.35f : 1;
            }
            // FIRE: the behind view's trigger on foot, BRAKE while driving, and the gun's
            // trigger is the corner button.
            bool fireOn = playing && (onFoot ? LookMode : Scheme == TouchLayout.Scheme.Drive);
            if (fireButton.gameObject.activeSelf != fireOn) fireButton.gameObject.SetActive(fireOn);
            bool canSwitch = playing && gm.cameraFollow.CanSwitch;
            if (viewButton.gameObject.activeSelf != canSwitch) viewButton.gameObject.SetActive(canSwitch);
            if (canSwitch && viewBadge != null)
            {
                var follow = gm.cameraFollow;
                bool auto = follow.ContextCamera && follow.Context.Auto && !follow.Context.Pinned && !follow.Gunning;
                if (viewBadge.activeSelf != auto) viewBadge.SetActive(auto);
            }
            FireHeld = ActionHeld = DashHeld = false;
            var screen = Touchscreen.current;
            if (screen == null) return;

            bool stickHeld = false, lookHeld = false;
            foreach (var t in screen.touches)
            {
                int id = t.touchId.ReadValue();
                double began = t.startTime.ReadValue();
                Vector2 start = t.startPosition.ReadValue();
                bool onDash = Contains(dashButton, start), onBomb = Contains(bombButton, start);
                bool onAction = Contains(actionButton, start), onSkip = Contains(skipButton, start);
                bool onFire = Contains(fireButton, start), onView = Contains(viewButton, start);
                bool onDrop = Contains(dropButton, start);
                bool onButton = onDash || onBomb || onAction || onSkip || onFire || onView || onDrop;
                bool live = gm != null && (gm.IsPlaying || gm.CurrentMode == GameManager.Mode.Cinematic);
                bool down = (t.press.wasPressedThisFrame || t.tap.wasPressedThisFrame) && live;
                if (down && onButton && !onFire && System.Array.IndexOf(handled, began) < 0)
                {
                    handled[nextHandled] = began;
                    nextHandled = (nextHandled + 1) % handled.Length;
                    if (onAction) actionPressed = true;
                    else if (onDrop) dropPressed = true;
                    else if (onSkip) skipPressed = true;
                    else if (onView) viewPressed = true;
                    else if (onDash) dashPressed = true;
                    else bombPressed = true;
                }
                if (!t.press.isPressed) continue;
                if (onFire) FireHeld = true;
                if (onAction) ActionHeld = true;
                if (onDash) DashHeld = true;
                // Behind view: a touch that starts outside the stick zone (TouchLayout) turns the camera, and so
                // does a drag that starts on FIRE (aim while firing).
                bool gun = Scheme == TouchLayout.Scheme.Gun;
                bool lookSide = gun || LookMode && !TouchLayout.StickZone(start, Screen.width, Screen.height);
                bool looks = gun ? onAction || !onButton : LookMode && (onFire || lookSide && !onButton);
                if (looks && (lookId < 0 || lookId == id))
                {
                    Vector2 at = ToCanvas(t.position.ReadValue());
                    if (lookId != id)
                    {
                        lookId = id;
                        lookLast = at;
                    }
                    look += at - lookLast;
                    lookLast = at;
                    lookHeld = true;
                    continue;
                }
                if (onFire) continue;
                // A held touch that didn't start on a button becomes the stick.
                if (stickId < 0 && !onButton && !lookSide)
                {
                    stickId = id;
                    origin = ToCanvas(start);
                    joy.anchoredPosition = origin;
                    joy.gameObject.SetActive(true);
                }
                if (id != stickId) continue;
                stickHeld = true;
                Vector2 d = ToCanvas(t.position.ReadValue()) - origin;
                if (d.magnitude > StickTravel) d = d.normalized * StickTravel;
                knob.anchoredPosition = d;
                Stick = d / StickTravel;
            }
            if (stickId >= 0 && !stickHeld) Release();
            if (!lookHeld) lookId = -1;
        }

        void Release()
        {
            stickId = -1;
            Stick = Vector2.zero;
            joy.gameObject.SetActive(false);
        }

        static bool Contains(RectTransform rt, Vector2 screenPos)
        {
            return rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos);
        }

        // Screen pixels to canvas units, relative to the canvas centre.
        Vector2 ToCanvas(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out var local);
            return local;
        }
    }
}
