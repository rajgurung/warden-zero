using UnityEngine;

namespace WardenZero
{
    // What the Warden is doing this frame, as far as drawing him is concerned.
    public struct WardenPose
    {
        public Vector3 Aim; // ground-plane aim direction
        public Vector3 MoveDir; // ground-plane movement direction (zero when still)
        public Vector3 DashDir;
        public bool Moving;
        public bool Dashing;
        public bool Shooting; // firing, or within ~0.25 s of the last shot
        public bool Dead;
        public bool Hidden; // hurt-blink off frame
        public float HurtTint; // 0..1 red flash
    }

    // Draws the Warden. PlayerController only talks to this, so the sprite view and the
    // 3D model view are interchangeable (SceneBuilder picks one).
    public abstract class WardenView : MonoBehaviour
    {
        public abstract void Show(WardenPose pose);

        // Turn toward the aim (called before firing so the muzzle is current).
        public abstract void UpdateFacing(Vector3 aim);

        // Where shots leave the rifle, in world space.
        public abstract Vector3 RifleTip();

        public virtual void OnHurt() { }

        // A shot was fired (recoil).
        public virtual void OnFire() { }
    }
}
