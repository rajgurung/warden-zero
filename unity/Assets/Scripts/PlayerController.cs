using UnityEngine;
using UnityEngine.InputSystem;

namespace WardenZero
{
    // The Warden: WASD to move, mouse to aim, hold left mouse to fire, Space to dash.
    // Drawn as a camera-facing sprite. Twin-stick style: he always faces the aim, so the
    // frame set (up / down / side, side mirrored for left) comes from the aim direction,
    // and moving against it plays the run cycle backwards-facing (backpedalling).
    public class PlayerController : MonoBehaviour
    {
        public enum Facing { Up, Down, Side }

        // Degrees of slack around the 45-degree boundaries so the frame doesn't flicker.
        const float FacingHysteresis = 8f;
        // Bolts fly at this height; the rifle tip is projected onto it so bolts leave the barrel.
        const float BoltHeight = 1.2f;
        // Rifle tip in sprite-local metres (pivot at the feet), measured on the 512 px frames
        // at 182.9 px/m: shoot (430,140), shoot_up (345,50), shoot_down (325,240).
        static readonly Vector2 MuzzleSide = new Vector2(0.95f, 2.04f);
        static readonly Vector2 MuzzleUp = new Vector2(0.49f, 2.53f);
        static readonly Vector2 MuzzleDown = new Vector2(0.38f, 1.48f);

        public Camera cam;
        public SpriteRenderer body;
        public Transform reticle;
        public Bolt boltPrefab;
        public Transform muzzleFlash;
        public Light glow;

        [Header("Frames (side frames face right)")]
        public Sprite idle;
        public Sprite shoot;
        public Sprite shootUp;
        public Sprite shootDown;
        public Sprite dash;
        public Sprite death;
        public Sprite[] runDown;
        public Sprite[] runSide;
        public Sprite[] runUp;

        public float Health { get; private set; } = GameConfig.PlayerMaxHealth;
        public bool IsDead => Health <= 0;
        // 0 while recharging, 1 when the dash is ready.
        public float DashReady => Mathf.Clamp01(1 - (dashReadyAt - Time.time) / GameConfig.DashCooldown);
        public Facing CurrentFacing { get; private set; } = Facing.Down;
        public bool FacingLeft { get; private set; }

        Vector3 aimDir = Vector3.forward;
        Vector3 aimPoint;
        Vector3 dashDir;
        float dashUntil;
        float dashReadyAt;
        float invulnUntil;
        float nextShot;
        float runTime;
        float firingPose; // keeps the shoot pose up briefly after each shot
        float glowBase;
        float muzzleTimer;
        float hurtTint;

        void Start()
        {
            glowBase = glow.intensity;
            muzzleFlash.gameObject.SetActive(false);
        }

        void Update()
        {
            if (IsDead || !GameManager.Instance.IsPlaying)
            {
                reticle.gameObject.SetActive(false);
                body.enabled = true;
                if (IsDead) body.sprite = death;
                return;
            }
            float dt = Time.deltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // --- movement
            Vector3 move = Vector3.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.z += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.z -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
            }
            move = Vector3.ClampMagnitude(move, 1);

            // --- aim at the cursor's point on the ground plane
            if (mouse != null)
            {
                Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                if (new Plane(Vector3.up, 0).Raycast(ray, out float enter))
                {
                    Vector3 hit = ray.GetPoint(enter);
                    aimPoint = hit;
                    reticle.position = new Vector3(hit.x, 0.03f, hit.z);
                    Vector3 to = hit - transform.position;
                    to.y = 0;
                    if (to.sqrMagnitude > 0.01f) aimDir = to.normalized;
                }
            }
            reticle.gameObject.SetActive(true);
            UpdateFacing();

            // --- dash
            if (kb != null && kb.spaceKey.wasPressedThisFrame && Time.time >= dashReadyAt)
            {
                dashDir = move.sqrMagnitude > 0.01f ? move.normalized : aimDir;
                dashUntil = Time.time + GameConfig.DashDuration;
                invulnUntil = Mathf.Max(invulnUntil, dashUntil);
                dashReadyAt = Time.time + GameConfig.DashCooldown;
                GameManager.Instance.PlaySound(GameManager.Instance.dashSound, 0.5f);
                Effects.Instance.Dash(transform.position);
            }
            bool dashing = Time.time < dashUntil;
            Vector3 velocity = dashing ? dashDir * GameConfig.DashSpeed : move * GameConfig.PlayerSpeed;
            transform.position = GameConfig.ResolveCircle(transform.position + velocity * dt, GameConfig.PlayerRadius);

            // --- fire
            bool firing = mouse != null && mouse.leftButton.isPressed;
            if (firing && Time.time >= nextShot)
            {
                nextShot = Time.time + GameConfig.FireInterval;
                Vector3 tip = RifleTip();
                Vector3 muzzle = OnBoltPlane(tip);
                // Aim from the barrel at the cursor; fall back to the plain aim when it's very close.
                Vector3 dir = aimPoint - muzzle;
                dir.y = 0;
                dir = dir.sqrMagnitude > 2.25f ? dir.normalized : aimDir;
                Instantiate(boltPrefab, muzzle, Quaternion.identity).Launch(dir);
                firingPose = 0.25f;
                GameManager.Instance.PlaySound(GameManager.Instance.shootSound, 0.18f);
                // Muzzle flash and a light pop, like Babylon's playerLight 0.7 -> 2.4.
                muzzleFlash.position = tip;
                muzzleFlash.localScale = Vector3.one * Random.Range(0.7f, 1f);
                muzzleFlash.gameObject.SetActive(true);
                muzzleTimer = 0.05f;
                glow.intensity = glowBase * 3.4f;
            }
            firingPose -= dt;
            muzzleTimer -= dt;
            if (muzzleTimer <= 0) muzzleFlash.gameObject.SetActive(false);
            glow.intensity += (glowBase - glow.intensity) * Mathf.Min(1, dt * 12);

            UpdateSprite(move.sqrMagnitude > 0.01f, dashing, firingPose > 0, dt);
        }

        void UpdateSprite(bool moving, bool dashing, bool shooting, float dt)
        {
            // Hurt blink (not during dash i-frames).
            body.enabled = dashing || Time.time >= invulnUntil || Mathf.FloorToInt(Time.time * 20) % 2 == 0;
            hurtTint = Mathf.Max(0, hurtTint - dt * 4);
            body.color = Color.Lerp(Color.white, new Color(1, 0.35f, 0.35f), hurtTint);
            float bob = 0;

            if (dashing)
            {
                body.sprite = dash;
                body.flipX = ScreenX(dashDir) < 0;
            }
            else
            {
                runTime = moving ? runTime + dt : 0;
                int f = (int)(runTime * 12) % 6;
                if (shooting)
                {
                    body.sprite = CurrentFacing == Facing.Up ? shootUp : CurrentFacing == Facing.Down ? shootDown : shoot;
                    // Footstep bob (two steps per 0.5 s run cycle) so he doesn't slide while firing.
                    if (moving) bob = Mathf.Abs(Mathf.Sin(runTime * Mathf.PI * 4)) * 0.09f;
                }
                else if (moving)
                {
                    body.sprite = (CurrentFacing == Facing.Up ? runUp : CurrentFacing == Facing.Down ? runDown : runSide)[f];
                }
                else
                {
                    // Standing: the aiming-up pose for up (there is no idle back view), else idle.
                    body.sprite = CurrentFacing == Facing.Up ? shootUp : idle;
                }
                body.flipX = FacingLeft;
            }
            body.transform.localPosition = new Vector3(0, bob, 0);
        }

        // Facing from the aim direction as seen on screen, with hysteresis at the
        // 45-degree boundaries and around straight up/down for the left/right mirror.
        void UpdateFacing()
        {
            float ax = ScreenX(aimDir), ay = ScreenY(aimDir);
            float angle = Mathf.Atan2(ay, ax) * Mathf.Rad2Deg; // 0 = right, 90 = up
            float toUp = Mathf.Abs(Mathf.DeltaAngle(angle, 90));
            float toDown = Mathf.Abs(Mathf.DeltaAngle(angle, -90));
            float keep = 45 + FacingHysteresis, enter = 45 - FacingHysteresis;
            if (CurrentFacing == Facing.Up && toUp < keep) { }
            else if (CurrentFacing == Facing.Down && toDown < keep) { }
            else if (CurrentFacing == Facing.Side) CurrentFacing = toUp < enter ? Facing.Up : toDown < enter ? Facing.Down : Facing.Side;
            else CurrentFacing = toUp < 45 ? Facing.Up : toDown < 45 ? Facing.Down : Facing.Side;

            float band = Mathf.Sin(FacingHysteresis * Mathf.Deg2Rad);
            float len = Mathf.Max(1e-4f, Mathf.Sqrt(ax * ax + ay * ay));
            if (ax / len < -band) FacingLeft = true;
            else if (ax / len > band) FacingLeft = false;
        }

        // Where the rifle tip is drawn for the current facing, in world space on the sprite.
        Vector3 RifleTip()
        {
            Vector2 m = CurrentFacing == Facing.Up ? MuzzleUp : CurrentFacing == Facing.Down ? MuzzleDown : MuzzleSide;
            if (FacingLeft) m.x = -m.x;
            return body.transform.TransformPoint(m);
        }

        // The point at bolt height that the camera sees in the same place as `p`.
        Vector3 OnBoltPlane(Vector3 p)
        {
            Vector3 from = cam.transform.position;
            Vector3 dir = p - from;
            if (Mathf.Abs(dir.y) < 1e-4f) return new Vector3(p.x, BoltHeight, p.z);
            return from + dir * ((BoltHeight - from.y) / dir.y);
        }

        // Direction components as seen on screen (camera right / camera forward on the ground).
        float ScreenX(Vector3 dir)
        {
            return Vector3.Dot(dir, cam.transform.right);
        }

        float ScreenY(Vector3 dir)
        {
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0;
            return Vector3.Dot(dir, fwd.normalized);
        }

        // Returns true if the hit landed (false while invulnerable).
        public bool TryHurt(float damage)
        {
            if (IsDead || Time.time < invulnUntil) return false;
            Health = Mathf.Max(0, Health - damage);
            invulnUntil = Time.time + GameConfig.HurtInvuln;
            hurtTint = 1;
            Effects.Instance.PlayerHurt(transform.position);
            GameManager.Instance.OnPlayerHurt();
            return true;
        }

        public void ResetPosition()
        {
            transform.position = Vector3.zero;
        }
    }
}
