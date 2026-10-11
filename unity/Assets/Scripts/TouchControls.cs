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
        public CanvasGroup dashGroup;
        public CanvasGroup bombGroup;
        public GameObject[] desktopOnly; // ability chips: hidden on touch, as in Babylon

        public Vector2 Stick { get; private set; }
        public bool FireHeld { get; private set; }
        // Set by CameraFollow each frame: the behind view splits the screen into stick and look.
        [System.NonSerialized] public bool LookMode;

        int stickId = -1;
        Vector2 origin;
        bool dashPressed;
        bool bombPressed;
        bool actionPressed;
        bool skipPressed;
        bool viewPressed;
        int lookId = -1;
        Vector2 lookLast;
        Vector2 look;
        int lastButtonTouch = -1; // each touch triggers a button at most once

        public void PressDash() => dashPressed = true;
        public void PressBomb() => bombPressed = true;
        public void PressAction() => actionPressed = true;
        public void PressSkip() => skipPressed = true;
        public void PressView() => viewPressed = true;

        // Presses made while paused or on the upgrade picker are dropped (Babylon clearPresses).
        public void ClearPresses()
        {
            dashPressed = bombPressed = actionPressed = skipPressed = viewPressed = false;
            look = Vector2.zero;
        }

        // The context button, on touch screens only (desktop uses keys).
        public void ShowAction(string label)
        {
            actionLabel.text = label;
            actionButton.gameObject.SetActive(Active);
            actionPressed = false;
        }

        // For per-frame callers: (re)show only when the label changes, keeping a pending press.
        public void ShowActionOnce(string label)
        {
            if (actionLabel.text == label && actionButton.gameObject.activeSelf == Active) return;
            ShowAction(label);
        }

        public void HideAction()
        {
            actionButton.gameObject.SetActive(false);
            actionPressed = false;
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

        void OnEnable()
        {
            bool on = Active;
            dashButton.gameObject.SetActive(on);
            bombButton.gameObject.SetActive(on);
            foreach (var go in desktopOnly) go.SetActive(!on);
            actionButton.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(false);
            fireButton.gameObject.SetActive(false);
            viewButton.gameObject.SetActive(false);
            Release();
            ClearPresses();
        }

        void Update()
        {
            if (!Active) return;
            var gm = GameManager.Instance;
            bool playing = gm != null && gm.player.isActiveAndEnabled && gm.IsPlaying;
            if (playing)
            {
                dashGroup.alpha = gm.player.DashReady < 1 ? 0.35f : 1;
                bombGroup.alpha = gm.player.BombReady < 1 ? 0.35f : 1;
            }
            if (fireButton.gameObject.activeSelf != (playing && LookMode)) fireButton.gameObject.SetActive(playing && LookMode);
            bool canSwitch = playing && gm.cameraFollow.CanSwitch;
            if (viewButton.gameObject.activeSelf != canSwitch) viewButton.gameObject.SetActive(canSwitch);
            FireHeld = false;
            var screen = Touchscreen.current;
            if (screen == null) return;

            bool stickHeld = false, lookHeld = false;
            foreach (var t in screen.touches)
            {
                int id = t.touchId.ReadValue();
                Vector2 start = t.startPosition.ReadValue();
                bool onDash = Contains(dashButton, start), onBomb = Contains(bombButton, start);
                bool onAction = Contains(actionButton, start), onSkip = Contains(skipButton, start);
                bool onFire = Contains(fireButton, start), onView = Contains(viewButton, start);
                bool onButton = onDash || onBomb || onAction || onSkip || onFire || onView;
                bool live = gm != null && (gm.IsPlaying || gm.CurrentMode == GameManager.Mode.Cinematic);
                bool down = (t.press.wasPressedThisFrame || t.tap.wasPressedThisFrame) && live;
                if (down && onButton && !onFire && id != lastButtonTouch)
                {
                    lastButtonTouch = id;
                    if (onAction) actionPressed = true;
                    else if (onSkip) skipPressed = true;
                    else if (onView) viewPressed = true;
                    else if (onDash) dashPressed = true;
                    else bombPressed = true;
                }
                if (!t.press.isPressed) continue;
                if (onFire)
                {
                    FireHeld = true;
                    continue;
                }
                // Behind view: a touch that starts on the right half turns the camera.
                bool lookSide = LookMode && start.x >= Screen.width * 0.5f;
                if (lookSide && !onButton && (lookId < 0 || lookId == id))
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
