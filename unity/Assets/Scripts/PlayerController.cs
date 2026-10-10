using UnityEngine;
using UnityEngine.InputSystem;

namespace WardenZero
{
    // The Warden: WASD to move, mouse to aim, hold left mouse to fire, Space to dash.
    // Drawn as a camera-facing sprite whose frame is picked from the move/aim direction.
    public class PlayerController : MonoBehaviour
    {
        public Camera cam;
        public SpriteRenderer body;
        public Transform reticle;
        public Bolt boltPrefab;

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

        Vector3 aimDir = Vector3.forward;
        Vector3 dashDir;
        float dashUntil;
        float dashReadyAt;
        float invulnUntil;
        float nextShot;
        float runTime;
        float firingPose; // keeps the shoot pose up briefly after each shot

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
                    reticle.position = new Vector3(hit.x, 0.03f, hit.z);
                    Vector3 to = hit - transform.position;
                    to.y = 0;
                    if (to.sqrMagnitude > 0.01f) aimDir = to.normalized;
                }
            }
            reticle.gameObject.SetActive(true);

            // --- dash
            if (kb != null && kb.spaceKey.wasPressedThisFrame && Time.time >= dashReadyAt)
            {
                dashDir = move.sqrMagnitude > 0.01f ? move.normalized : aimDir;
                dashUntil = Time.time + GameConfig.DashDuration;
                invulnUntil = Mathf.Max(invulnUntil, dashUntil);
                dashReadyAt = Time.time + GameConfig.DashCooldown;
                GameManager.Instance.PlaySound(GameManager.Instance.dashSound, 0.5f);
            }
            bool dashing = Time.time < dashUntil;
            Vector3 velocity = dashing ? dashDir * GameConfig.DashSpeed : move * GameConfig.PlayerSpeed;
            transform.position = GameConfig.ResolveCircle(transform.position + velocity * dt, GameConfig.PlayerRadius);

            // --- fire
            bool firing = mouse != null && mouse.leftButton.isPressed;
            if (firing && Time.time >= nextShot)
            {
                nextShot = Time.time + GameConfig.FireInterval;
                Vector3 muzzle = transform.position + aimDir * 0.6f + Vector3.up * 1.2f;
                Instantiate(boltPrefab, muzzle, Quaternion.identity).Launch(aimDir);
                firingPose = 0.25f;
                GameManager.Instance.PlaySound(GameManager.Instance.shootSound, 0.18f);
            }
            firingPose -= dt;

            UpdateSprite(move, dashing, firingPose > 0, dt);
        }

        void UpdateSprite(Vector3 move, bool dashing, bool shooting, float dt)
        {
            // Hurt blink (not during dash i-frames).
            body.enabled = dashing || Time.time >= invulnUntil || Mathf.FloorToInt(Time.time * 20) % 2 == 0;

            if (dashing)
            {
                body.sprite = dash;
                body.flipX = ScreenX(dashDir) < 0;
                return;
            }
            if (move.sqrMagnitude > 0.01f)
            {
                runTime += dt;
                int f = (int)(runTime * 12) % 6;
                float sx = ScreenX(move), sy = ScreenY(move);
                if (Mathf.Abs(sy) > Mathf.Abs(sx))
                {
                    body.sprite = sy > 0 ? runUp[f] : runDown[f];
                    body.flipX = false;
                }
                else
                {
                    body.sprite = runSide[f];
                    body.flipX = sx < 0;
                }
                return;
            }
            runTime = 0;
            // Standing still: face the cursor.
            float ax = ScreenX(aimDir), ay = ScreenY(aimDir);
            if (shooting && ay > Mathf.Abs(ax)) body.sprite = shootUp;
            else if (shooting && -ay > Mathf.Abs(ax)) body.sprite = shootDown;
            else body.sprite = shooting ? shoot : idle;
            body.flipX = ax < 0;
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
            GameManager.Instance.OnPlayerHurt();
            return true;
        }

        public void ResetPosition()
        {
            transform.position = Vector3.zero;
        }
    }
}
