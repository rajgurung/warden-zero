using UnityEngine;

namespace WardenZero
{
    // What the Warden is doing this frame, as far as drawing him is concerned.
    public struct WardenPose
    {
        public Vector3 Aim; // ground-plane aim direction
        public Vector3 DashDir;
        public bool Moving;
        public bool Dashing;
        public bool Shooting; // firing, or within ~0.25 s of the last shot
        public bool Dead;
        public bool Hidden; // hurt-blink off frame
        public float HurtTint; // 0..1 red flash
    }

    // Draws the Warden as a camera-facing sprite. All Warden rendering lives here so a
    // 3D model can replace it: implement Show() and RifleTip() for the new model.
    // Twin-stick style: he always faces the aim, so the frame set (up / down / side,
    // side mirrored for left) comes from the aim direction, not the movement keys.
    public class WardenSpriteView : MonoBehaviour
    {
        public enum Facing { Up, Down, Side }

        // Degrees of slack around the 45-degree boundaries so the frame doesn't flicker.
        const float FacingHysteresis = 8f;
        // Rifle tip in sprite-local metres (pivot at the feet), measured on the 512 px frames
        // at 182.9 px/m: shoot (430,140), shoot_up (345,50), shoot_down (325,240).
        static readonly Vector2 MuzzleSide = new Vector2(0.95f, 2.04f);
        static readonly Vector2 MuzzleUp = new Vector2(0.49f, 2.53f);
        static readonly Vector2 MuzzleDown = new Vector2(0.38f, 1.48f);

        public Camera cam;
        public SpriteRenderer body;

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

        public Facing CurrentFacing { get; private set; } = Facing.Down;
        public bool FacingLeft { get; private set; }

        float runTime;

        public void Show(WardenPose p)
        {
            body.enabled = !p.Hidden;
            body.color = Color.Lerp(Color.white, new Color(1, 0.35f, 0.35f), p.HurtTint);
            float bob = 0;
            if (p.Dead)
            {
                body.sprite = death;
                body.enabled = true;
            }
            else if (p.Dashing)
            {
                body.sprite = dash;
                body.flipX = ScreenX(p.DashDir) < 0;
            }
            else
            {
                UpdateFacing(p.Aim);
                runTime = p.Moving ? runTime + Time.deltaTime : 0;
                int f = (int)(runTime * 12) % 6;
                if (p.Shooting)
                {
                    body.sprite = CurrentFacing == Facing.Up ? shootUp : CurrentFacing == Facing.Down ? shootDown : shoot;
                    // Footstep bob (two steps per 0.5 s run cycle) so he doesn't slide while firing.
                    if (p.Moving) bob = Mathf.Abs(Mathf.Sin(runTime * Mathf.PI * 4)) * 0.09f;
                }
                else if (p.Moving)
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
        public void UpdateFacing(Vector3 aim)
        {
            float ax = ScreenX(aim), ay = ScreenY(aim);
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

        // Where the rifle tip is drawn for the current facing, in world space.
        public Vector3 RifleTip()
        {
            Vector2 m = CurrentFacing == Facing.Up ? MuzzleUp : CurrentFacing == Facing.Down ? MuzzleDown : MuzzleSide;
            if (FacingLeft) m.x = -m.x;
            return body.transform.TransformPoint(m);
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
    }
}
