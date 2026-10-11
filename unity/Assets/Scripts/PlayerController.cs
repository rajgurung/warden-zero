using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WardenZero
{
    // The Warden's gameplay: WASD to move, mouse to aim, hold left mouse to fire,
    // Space to dash, E or right mouse to bomb. On touch: virtual stick, auto-aim at the
    // nearest enemy and auto-fire (Game.frame). Numbers come from the run's PlayerStats
    // (upgrades change them). Drawing is delegated to a WardenView.
    // In the behind view (CameraFollow) WASD and the stick are relative to the camera, he aims
    // at whatever is under the centre crosshair, faces the camera's way while moving or
    // firing, and bolts fly from the muzzle straight to that point (touch fires with FIRE).
    public class PlayerController : MonoBehaviour
    {
        public Camera cam;
        public WardenView view;
        public RectTransform reticle; // screen-space, follows the pointer (Babylon's DOM reticle)
        public Bolt boltPrefab;
        public Bolt critBoltPrefab;
        public Transform muzzleFlash;
        public Light glow;
        public TouchControls touch;
        // Set pieces can make him run (a landing run-out) while gameplay is off.
        [System.NonSerialized] public Vector3 cinematicMove;

        static PlayerStats Stats => GameManager.Instance.Run.Stats;
        public float Health => Stats.Health;
        public bool IsDead => Stats.Health <= 0;
        // 0..1 readiness of each ability.
        public float DashReady => Ready(dashReadyAt, Stats.DashCooldownMs);
        public float BombReady => Ready(bombReadyAt, Stats.BombCooldownMs);
        public Vector3 AimDirection => aimDir;
        public Vector3 AimPoint => aimPoint;

        Vector3 aimDir = Vector3.forward;
        Vector3 aimPoint; // cursor on the aim plane (rifle height); behind: the crosshair's point
        Ray aimRay; // behind: the crosshair's ray from the camera
        Vector3 groundPoint; // cursor on the ground (where strikes land)
        Vector3 dashDir;
        float dashUntil;
        float dashReadyAt;
        float bombReadyAt;
        float invulnUntil;
        float nextShot;
        float firingPose; // keeps the shoot pose up briefly after each shot
        float glowBase;
        float muzzleTimer;
        float hurtTint;
        Vector3 knock; // shove from the Warlord's pound, decays quickly

        void Awake()
        {
            glowBase = glow.intensity;
            muzzleFlash.gameObject.SetActive(false);
        }

        // Fresh state for a new run.
        public void ResetForRun()
        {
            transform.position = Vector3.zero;
            dashUntil = dashReadyAt = bombReadyAt = invulnUntil = nextShot = 0;
            firingPose = hurtTint = 0;
            knock = Vector3.zero;
            glow.intensity = glowBase;
        }

        // Set pieces pose the Warden themselves: face along his own root (aim = local forward).
        public void SetAim(Vector3 direction)
        {
            aimDir = direction;
        }

        public void ResetPosition()
        {
            transform.position = Vector3.zero;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (!gm.IsPlaying)
            {
                reticle.gameObject.SetActive(false);
                muzzleFlash.gameObject.SetActive(false);
                bool running = cinematicMove.sqrMagnitude > 0.01f;
                view.Show(new WardenPose { Aim = aimDir, Dead = IsDead, Moving = running, MoveDir = cinematicMove });
                return;
            }
            float dt = Time.deltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var follow = gm.cameraFollow;
            bool behind = follow.IsBehind;

            // --- movement
            Vector3 move = Vector3.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.z += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.z -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
            }
            bool touching = TouchControls.Active;
            if (touching) move += new Vector3(touch.Stick.x, 0, touch.Stick.y);
            move = Vector3.ClampMagnitude(move, 1);
            if (behind) move = Quaternion.Euler(0, follow.Yaw, 0) * move;
            bool firing = touching ? (behind ? touch.FireHeld : Enemy.All.Count > 0) : mouse != null && mouse.leftButton.isPressed;

            // --- aim: the crosshair (behind view), the nearest enemy on touch, else the cursor's
            // point on the aim plane
            if (behind)
            {
                AimAtCrosshair(follow.Yaw, firing || move.sqrMagnitude > 0.01f);
                reticle.anchoredPosition = Vector2.zero;
            }
            else if (touching)
            {
                Enemy best = NearestEnemy(GameConfig.AutoAimRange);
                Vector3 to = best != null ? best.transform.position - transform.position : move;
                to.y = 0;
                if (to.sqrMagnitude > 0.01f) aimDir = to.normalized;
                aimPoint = transform.position + aimDir * 6 + Vector3.up * GameConfig.AimHeight;
                groundPoint = transform.position + aimDir * 6;
            }
            else if (mouse != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                Ray ray = cam.ScreenPointToRay(screen);
                float floor = transform.position.y;
                if (new Plane(Vector3.up, new Vector3(0, floor + GameConfig.AimHeight, 0)).Raycast(ray, out float enter))
                {
                    aimPoint = ray.GetPoint(enter);
                    Vector3 to = aimPoint - transform.position;
                    to.y = 0;
                    if (to.sqrMagnitude > 0.01f) aimDir = to.normalized;
                }
                if (new Plane(Vector3.up, new Vector3(0, floor, 0)).Raycast(ray, out float ground)) groundPoint = ray.GetPoint(ground);
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)reticle.parent, screen, null, out var local);
                reticle.anchoredPosition = local;
            }
            reticle.gameObject.SetActive(behind || !touching);
            view.UpdateFacing(aimDir);

            // --- abilities
            bool dashPressed = (kb != null && kb.spaceKey.wasPressedThisFrame) | (touching && touch.ConsumeDash());
            bool rightClick = mouse != null && mouse.rightButton.wasPressedThisFrame;
            bool touchSecondary = touching && touch.ConsumeBomb();
            if (dashPressed) TryDash(move);
            var strikes = gm.mission != null ? gm.mission.strikes : null;
            if (strikes != null)
            {
                // Greenfang: Q arms the other strike, right-click (or the touch button) calls it.
                if (kb != null && kb.qKey.wasPressedThisFrame) strikes.Cycle();
                if (rightClick) strikes.Fire(groundPoint, transform.position);
                if (touchSecondary) TouchStrike(strikes);
            }
            else if ((kb != null && kb.eKey.wasPressedThisFrame) || rightClick || touchSecondary)
            {
                TryBomb();
            }
            bool dashing = Time.time < dashUntil;
            Vector3 velocity = dashing ? dashDir * Stats.DashSpeed * GameConfig.PX : move * Stats.Speed * GameConfig.PX;
            velocity += knock;
            knock *= Mathf.Exp(-6 * dt);
            Vector3 next = GameConfig.ResolveCircle(transform.position + velocity * dt, GameConfig.PlayerRadius);
            next = World.PushOutOfTrunks(next, GameConfig.PlayerRadius);
            next.y = World.HeightAt(next);
            transform.position = next;

            // --- fire
            if (firing && Time.time >= nextShot)
            {
                nextShot = Time.time + Stats.FireRateMs / 1000f;
                Fire(behind);
            }
            firingPose -= dt;
            muzzleTimer -= dt;
            if (muzzleTimer <= 0) muzzleFlash.gameObject.SetActive(false);
            glow.intensity += (glowBase - glow.intensity) * Mathf.Min(1, dt * 12);

            hurtTint = Mathf.Max(0, hurtTint - dt * 4);
            bool blinkOff = !dashing && Time.time < invulnUntil && Mathf.FloorToInt(Time.time * 20) % 2 == 1;
            view.Show(new WardenPose
            {
                Aim = aimDir,
                MoveDir = move,
                DashDir = dashDir,
                Moving = move.sqrMagnitude > 0.01f,
                Dashing = dashing,
                Shooting = firingPose > 0,
                AimPitch = behind ? AimPitch() : 0,
                Hidden = blinkOff,
                HurtTint = hurtTint,
            });
        }

        // The behind view's aim: the first thing along the crosshair's ray beyond the Warden
        // (the ground, a trunk, a rock, a wall or an enemy), else a far point on it. He turns to
        // the camera's heading while moving or firing, and keeps his facing at rest.
        void AimAtCrosshair(float yaw, bool turn)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            aimRay = ray;
            float skip = Mathf.Max(0, Vector3.Dot(transform.position + Vector3.up * GameConfig.AimHeight - ray.origin, ray.direction));
            Vector3 from = ray.GetPoint(skip);
            aimPoint = from + ray.direction * (WorldCast.Cast(from, ray.direction, GameConfig.AimRange, 0, true, out float hit) ? hit : GameConfig.AimRange);
            groundPoint = new Vector3(aimPoint.x, World.HeightAt(aimPoint), aimPoint.z);
            if (!turn) return;
            Vector3 to = aimPoint - transform.position;
            to.y = 0;
            aimDir = to.sqrMagnitude > 2.25f ? to.normalized : Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        }

        // Degrees the rifle tips down (negative: up) to point from his shoulder at the aim point.
        float AimPitch()
        {
            Vector3 to = aimPoint - (transform.position + Vector3.up * GameConfig.AimHeight);
            float flat = new Vector2(to.x, to.z).magnitude;
            return flat < 0.5f ? 0 : -Mathf.Atan2(to.y, flat) * Mathf.Rad2Deg;
        }

        // One trigger pull: BulletCount bolts in an 8-degree spread, each may crit.
        void Fire(bool behind)
        {
            var s = Stats;
            Vector3 tip = view.RifleTip();
            Vector3 muzzle, dir;
            if (behind)
            {
                // From the barrel to the crosshair's point, in 3D. When that point is not ahead
                // of the barrel, or cover the camera sees past is in the barrel's way (he stands
                // at a trunk's edge), the bolt leaves from the crosshair's ray beside the barrel
                // (never beyond the point itself, e.g. a trunk at the muzzle) and flies along it.
                muzzle = tip;
                Vector3 to = aimPoint - tip;
                float reach = to.magnitude;
                dir = to / Mathf.Max(reach, 1e-4f);
                bool ahead = Vector3.Dot(to, aimRay.direction) > 0.3f;
                if (!ahead || WorldCast.InScenery(tip) || WorldCast.Cast(tip, dir, reach - 0.05f, 0, false, out _))
                {
                    float depth = Vector3.Dot(tip - aimRay.origin, aimRay.direction);
                    muzzle = aimRay.GetPoint(Mathf.Min(depth, Vector3.Dot(aimPoint - aimRay.origin, aimRay.direction) - 0.05f));
                    dir = aimRay.direction;
                }
            }
            else
            {
                muzzle = OnBoltPlane(tip);
                // Straight from the barrel to the point under the cursor, using the real aim (not
                // the body's eased turn). When the cursor is on top of the Warden, use the plain aim.
                dir = aimPoint - muzzle;
                dir.y = 0;
                Vector3 fromBody = aimPoint - transform.position;
                fromBody.y = 0;
                dir = fromBody.sqrMagnitude > 2.25f && dir.sqrMagnitude > 0.04f ? dir.normalized : aimDir;
            }
            float start = -(s.BulletCount - 1) / 2f * GameConfig.MultishotSpread;
            for (int i = 0; i < s.BulletCount; i++)
            {
                bool crit = Random.value < s.CritChance;
                Vector3 d = Quaternion.Euler(0, start + i * GameConfig.MultishotSpread, 0) * dir;
                float damage = crit ? Mathf.Round(s.BulletDamage * s.CritMult) : s.BulletDamage;
                Bolt.Spawn(crit ? critBoltPrefab : boltPrefab, muzzle)
                    .Launch(d, s.BulletSpeed * GameConfig.PX, damage, s.BulletPiercing, s.BulletSize, behind);
            }
            firingPose = 0.25f;
            view.OnFire();
            GameManager.Instance.PlaySound(GameManager.Instance.shootSound, 0.22f, GameManager.Detune(200));
            // Muzzle flash and a light pop, like Babylon's playerLight 0.7 -> 2.4.
            muzzleFlash.position = tip;
            muzzleFlash.localScale = Vector3.one * Random.Range(0.7f, 1f);
            muzzleFlash.gameObject.SetActive(true);
            muzzleTimer = 0.05f;
            glow.intensity = glowBase * 3.4f;
        }

        void TryDash(Vector3 move)
        {
            if (Time.time < dashReadyAt || Time.time < dashUntil) return;
            var s = Stats;
            dashDir = move.sqrMagnitude > 0.01f ? move.normalized : aimDir;
            dashUntil = Time.time + s.DashDurationMs / 1000f;
            invulnUntil = Mathf.Max(invulnUntil, dashUntil);
            dashReadyAt = Time.time + s.DashCooldownMs / 1000f;
            GameManager.Instance.PlaySound(GameManager.Instance.dashSound, 0.5f);
            Effects.Instance.Dash(transform.position);
        }

        // Radial blast around the Warden (Game.tryBomb).
        void TryBomb()
        {
            if (Time.time < bombReadyAt) return;
            var s = Stats;
            bombReadyAt = Time.time + s.BombCooldownMs / 1000f;
            float r = s.BombRadius * GameConfig.PX;
            Vector3 p = transform.position;
            Effects.Instance.BombBlast(p, r);
            GameManager.Instance.PlaySound(GameManager.Instance.bombSound, 0.6f);
            foreach (var e in Enemy.All.ToArray())
            {
                Vector3 d = e.transform.position - p;
                d.y = 0;
                if (d.magnitude <= r + e.Radius) e.TakeHit(s.BombDamage);
            }
        }

        // Touch has no cursor: call whichever strike is ready (artillery first) on the nearest enemy.
        void TouchStrike(StrikeSystem strikes)
        {
            Enemy target = NearestEnemy(GameConfig.AutoAimRange);
            if (target == null) return;
            var type = strikes.CanFire(StrikeType.Artillery) ? StrikeType.Artillery : StrikeType.Air;
            strikes.Fire(type, target.transform.position, transform.position);
        }

        public void Knock(Vector3 velocity)
        {
            knock += velocity;
        }

        Enemy NearestEnemy(float range)
        {
            Enemy best = null;
            float bestD = range * range;
            Vector3 p = transform.position;
            foreach (var e in Enemy.All)
            {
                Vector3 d = e.transform.position - p;
                float d2 = d.x * d.x + d.z * d.z;
                if (d2 < bestD) { bestD = d2; best = e; }
            }
            return best;
        }

        // The point at bolt height that the camera sees in the same place as `p`.
        Vector3 OnBoltPlane(Vector3 p)
        {
            float h = transform.position.y + GameConfig.AimHeight;
            Vector3 from = cam.transform.position;
            Vector3 dir = p - from;
            if (Mathf.Abs(dir.y) < 1e-4f) return new Vector3(p.x, h, p.z);
            return from + dir * ((h - from.y) / dir.y);
        }

        float Ready(float readyAt, float cooldownMs)
        {
            if (Time.time >= readyAt) return 1;
            return Mathf.Clamp01(1 - (readyAt - Time.time) / (cooldownMs / 1000f));
        }

        public void Heal(float n)
        {
            Stats.Health = Mathf.Min(Stats.MaxHealth, Stats.Health + n);
        }

        // Returns true if the hit landed (false while invulnerable or dead).
        public bool TryHurt(float damage)
        {
            if (IsDead || Time.time < invulnUntil || !GameManager.Instance.IsPlaying) return false;
            Stats.Health = Mathf.Max(0, Stats.Health - damage);
            invulnUntil = Time.time + GameConfig.HurtInvuln;
            hurtTint = 1;
            view.OnHurt();
            Effects.Instance.PlayerHurt(transform.position);
            GameManager.Instance.OnPlayerHurt();
            return true;
        }
    }
}
