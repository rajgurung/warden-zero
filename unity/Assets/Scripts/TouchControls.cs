using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WardenZero
{
    // Touch scheme from src/systems/Input.ts: drag anywhere (outside the buttons) for a
    // virtual stick, DASH and BOMB buttons on the right that fire on touch-down. Aiming and
    // firing are automatic on touch (PlayerController reads Active). A quick tap that starts
    // and ends inside one frame still counts, through the touch's tap control.
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
        public CanvasGroup dashGroup;
        public CanvasGroup bombGroup;
        public GameObject[] desktopOnly; // ability chips: hidden on touch, as in Babylon

        public Vector2 Stick { get; private set; }

        int stickId = -1;
        Vector2 origin;
        bool dashPressed;
        bool bombPressed;
        int lastButtonTouch = -1; // each touch triggers a button at most once

        public void PressDash() => dashPressed = true;
        public void PressBomb() => bombPressed = true;

        // Presses made while paused or on the upgrade picker are dropped (Babylon clearPresses).
        public void ClearPresses()
        {
            dashPressed = bombPressed = false;
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

        void OnEnable()
        {
            bool on = Active;
            dashButton.gameObject.SetActive(on);
            bombButton.gameObject.SetActive(on);
            foreach (var go in desktopOnly) go.SetActive(!on);
            Release();
            dashPressed = bombPressed = false;
        }

        void Update()
        {
            if (!Active) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.player.isActiveAndEnabled)
            {
                dashGroup.alpha = gm.player.DashReady < 1 ? 0.35f : 1;
                bombGroup.alpha = gm.player.BombReady < 1 ? 0.35f : 1;
            }
            var screen = Touchscreen.current;
            if (screen == null) return;

            bool stickHeld = false;
            foreach (var t in screen.touches)
            {
                int id = t.touchId.ReadValue();
                Vector2 start = t.startPosition.ReadValue();
                bool onDash = Contains(dashButton, start), onBomb = Contains(bombButton, start);
                bool down = (t.press.wasPressedThisFrame || t.tap.wasPressedThisFrame) && gm != null && gm.IsPlaying;
                if (down && (onDash || onBomb) && id != lastButtonTouch)
                {
                    lastButtonTouch = id;
                    if (onDash) dashPressed = true;
                    else bombPressed = true;
                }
                if (!t.press.isPressed) continue;
                // A held touch that didn't start on a button becomes the stick.
                if (stickId < 0 && !onDash && !onBomb)
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
