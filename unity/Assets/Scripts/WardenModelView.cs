using UnityEngine;

namespace WardenZero
{
    // Draws the Warden as the rigged Tripo model (Humanoid). The body turns to the aim with
    // Babylon-style easing (a fraction of the remaining angle each frame, so fast but never a
    // snap) through a full 360 degrees. Legs play idle/walk/run by speed; an upper-body
    // layer (Avatar Mask) keeps the rifle shouldered with the `fire` clip, and plays the hit
    // flinch; death plays defeat_03. The rifle hangs off the body frame along his facing and
    // both hands hold it through IK (WardenHandIK), so the gun turns with him and stays level.
    [DefaultExecutionOrder(50)]
    public class WardenModelView : WardenView
    {
        public const int UpperLayer = 1;
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int AnimSpeedId = Animator.StringToHash("AnimSpeed");
        static readonly int UpperSpeedId = Animator.StringToHash("UpperSpeed");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public Transform yaw; // turns to the aim; the model sits under it
        public Animator animator;
        public Transform rightHand;
        public Transform rifle; // child of the model frame at chest height
        public Transform muzzle; // child of the rifle, at the barrel tip
        public WardenHandIK handIK;
        public Renderer[] renderers;
        // Babylon eased with dr * min(1, dt * 20); 24 is a touch snappier (about 5 degrees left
        // 0.15 s after a 180-degree flip, against 9 at 20).
        public float turnRate = 24;

        public float Yaw { get; private set; }
        public bool Dead { get; private set; }

        float targetYaw;
        float hitUntil = -1;
        Transform rifleHome;
        Vector3 rifleHomePos;
        Quaternion rifleHomeRot;
        MaterialPropertyBlock block;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            rifleHome = rifle.parent;
            rifleHomePos = rifle.localPosition;
            rifleHomeRot = rifle.localRotation;
        }

        public override void UpdateFacing(Vector3 aim)
        {
            if (aim.sqrMagnitude > 1e-4f) targetYaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
        }

        public override Vector3 RifleTip() => muzzle.position;

        public override void Show(WardenPose p)
        {
            float dt = Time.deltaTime;
            // Hurt: flash red rather than blinking out, so he stays readable in a crowd.
            float tint = Mathf.Max(p.HurtTint, p.Hidden ? 0.6f : 0);
            block.SetColor(BaseColorId, Color.Lerp(Color.white, new Color(1, 0.35f, 0.35f), tint));
            foreach (var r in renderers) r.SetPropertyBlock(block);

            if (p.Dead)
            {
                if (!Dead)
                {
                    Dead = true;
                    animator.CrossFadeInFixedTime("Death", 0.15f, 0);
                    animator.SetLayerWeight(UpperLayer, 0);
                    // Let go: the rifle falls with his right hand.
                    handIK.weight = 0;
                    rifle.SetParent(rightHand, true);
                }
                return;
            }
            if (Dead)
            {
                // A new run after death.
                Dead = false;
                handIK.weight = 1;
                rifle.SetParent(rifleHome, false);
                rifle.localPosition = rifleHomePos;
                rifle.localRotation = rifleHomeRot;
                animator.Play("Locomotion", 0, 0);
                animator.Play("Aim", UpperLayer, 0);
                animator.SetLayerWeight(UpperLayer, 1);
            }

            UpdateFacing(p.Aim);
            Yaw += Mathf.DeltaAngle(Yaw, targetYaw) * Mathf.Min(1, dt * turnRate);
            Yaw = Mathf.Repeat(Yaw, 360);
            yaw.localRotation = Quaternion.Euler(0, Yaw, 0);

            // Legs: run when moving (there is no strafe set, so the run plays backwards
            // when he backpedals against his aim); faster during a dash.
            bool moving = p.Moving || p.Dashing;
            animator.SetFloat(SpeedId, moving ? 1 : 0, 0.08f, dt);
            bool backpedal = p.Moving && Vector3.Dot(p.MoveDir, p.Aim) < -0.25f;
            animator.SetFloat(AnimSpeedId, p.Dashing ? 1.8f : backpedal ? -1 : 1);
            // Upper body: shouldered rifle, recoil only while shooting.
            animator.SetFloat(UpperSpeedId, p.Shooting ? 1 : 0.12f);
            if (hitUntil >= 0 && Time.time >= hitUntil)
            {
                hitUntil = -1;
                animator.CrossFadeInFixedTime("Aim", 0.2f, UpperLayer);
            }
        }

        public override void OnHurt()
        {
            if (Dead) return;
            // A short flinch on the upper body; the clip starts in a guard, so blend in and out.
            animator.CrossFadeInFixedTime("Hit", 0.08f, UpperLayer, 0.15f);
            hitUntil = Time.time + 0.4f;
        }
    }
}
