using UnityEngine;

namespace WardenZero
{
    // The Warden's rifle stance, done procedurally in the IK pass (OnAnimatorIK is only called
    // on the Animator's GameObject). Modelled on a carbine shooter:
    // - Shouldered (firing): stock in the right shoulder pocket, rifle along the aim line at
    //   cheek height, firing hand high on the pistol grip with the firing elbow ~45 degrees out,
    //   support hand on a vertical foregrip (palm against it, knuckles forward), support elbow
    //   down and slightly out, torso leaning a little into the gun.
    // - Low ready (not firing): same hold, rifle lowered and carried across the body, muzzle
    //   down and to his left, so the support hand reaches the foregrip from an upright torso.
    // The rifle is placed relative to the animated body every frame (so it bobs with him),
    // then both wrists are put on their grips with matching rotations, so nothing stretches
    // or snaps at any aim angle. A per-shot kick moves the rifle back and up; the hands follow.
    [RequireComponent(typeof(Animator))]
    public class WardenHandIK : MonoBehaviour
    {
        public Transform yaw; // aim frame: +Z is the aim direction
        public Transform rifle; // pivot at the pistol grip; +Z along the barrel
        public Transform rightGrip; // palm point on the pistol grip (child of the rifle)
        public Transform leftGrip; // palm point on the vertical foregrip (child of the rifle)
        public Transform stock; // butt of the stock (child of the rifle)
        [Range(0, 1)] public float weight = 1;

        // The right shoulder pocket relative to the right upper-arm joint (aim frame, metres):
        // forward of the joint and just inside it, where the stock butt seats.
        public static readonly Vector3 PocketFromShoulder = new Vector3(-0.08f, -0.08f, 0.13f);

        // Low ready: the butt drops from the pocket and slides forward, muzzle tipped down.
        static readonly Vector3 LowReadyDrop = new Vector3(-0.04f, -0.2f, 0.06f);
        const float LowReadyPitch = 32; // degrees muzzle-down
        const float LowReadyYaw = 30; // degrees muzzle-left, across the body
        // Elbow hints relative to the body centre, aim frame: firing elbow out ~45 degrees,
        // support elbow down and slightly out.
        static readonly Vector3 RightElbowHint = new Vector3(0.75f, 0.2f, 0.05f);
        static readonly Vector3 LeftElbowHint = new Vector3(-0.35f, -0.05f, 0.55f);
        // Measured on this rig with a throwaway PlayMode probe: hand bone rotation =
        // IK goal rotation * C, so goal = wanted * inverse(C).
        static readonly Quaternion RightC = Quaternion.Euler(353.8f, 98f, 110f);
        static readonly Quaternion LeftC = Quaternion.Euler(354.1f, 264f, 246.6f);
        // Hand bone origin is the wrist; the palm centre sits ahead of it along the metacarpals
        // (model units: this component is on the scaled model).
        const float WristToPalm = 0.08f;
        const float PalmDepth = 0.03f;

        public float Shouldered { get; private set; } // 0 low ready .. 1 shouldered
        // Last targets set, for tests: wrist positions and wanted hand bone rotations.
        public Vector3 RightWrist { get; private set; }
        public Vector3 LeftWrist { get; private set; }
        public Quaternion RightHandRotation { get; private set; }
        public Quaternion LeftHandRotation { get; private set; }
        public bool WantShouldered { get; set; }
        public float AimPitch { get; set; } // degrees down the shouldered rifle tips (behind view)

        Animator animator;
        Transform rightHand, leftHand;
        Vector3 rightFingerLocal, rightPalmLocal, leftFingerLocal, leftPalmLocal;
        float kick;
        Transform rightShoulder;
        // Right shoulder joint relative to the Warden root in the aim frame, from the last
        // animated pose (stable while turning, since it's in the aim frame).
        Vector3 shoulderLocal = new Vector3(0.33f, 2.2f, -0.05f);

        void Awake()
        {
            animator = GetComponent<Animator>();
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            // Hand axes in bone space, measured from the mesh (principal axes of the vertices
            // skinned to each hand): fingers run along the bone's +Y, the palm normal is the
            // thinnest axis, signed to face the body in the rest pose.
            rightFingerLocal = new Vector3(0.15f, 0.99f, -0.06f).normalized;
            rightPalmLocal = new Vector3(-0.78f, 0.12f, 0.61f).normalized;
            leftFingerLocal = new Vector3(0.055f, 1f, -0.094f).normalized;
            leftPalmLocal = new Vector3(0.81f, 0.15f, 0.57f).normalized;
            rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }

        void LateUpdate()
        {
            if (weight > 0) shoulderLocal = Quaternion.Inverse(yaw.rotation) * (rightShoulder.position - yaw.position);
        }

        // A shot: kick the rifle back and up.
        public void Kick() => kick = 1;

        float gripTo = -1, gripRate;

        // Take hold of the rifle (or let go) over a moment instead of at once (after landing).
        public void Grip(bool on, float seconds)
        {
            gripTo = on ? 1 : 0;
            gripRate = 1 / Mathf.Max(0.01f, seconds);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // ~0.15 s to shoulder, ~0.3 s back down to low ready.
            Shouldered = Mathf.MoveTowards(Shouldered, WantShouldered ? 1 : 0, dt / (WantShouldered ? 0.15f : 0.3f));
            kick = Mathf.Max(0, kick - dt * 9);
            if (gripTo >= 0)
            {
                weight = Mathf.MoveTowards(weight, gripTo, dt * gripRate);
                if (weight == gripTo) gripTo = -1;
            }
        }

        void OnAnimatorIK(int layer)
        {
            if (weight <= 0)
            {
                foreach (var g in new[] { AvatarIKGoal.RightHand, AvatarIKGoal.LeftHand })
                {
                    animator.SetIKPositionWeight(g, 0);
                    animator.SetIKRotationWeight(g, 0);
                }
                animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0);
                animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0);
                animator.SetLookAtWeight(0);
                return;
            }

            // Place the rifle: blend low ready -> shouldered, plus the recoil kick
            // (back 6 cm and 5 degrees up, absorbed through the shoulder).
            // The stock butt seats in the right shoulder pocket; the rifle runs from there along
            // the aim line. Low ready drops and tips it; the kick pushes it back into the shoulder.
            float s = Mathf.SmoothStep(0, 1, Shouldered);
            Quaternion aim = yaw.rotation;
            float k = Mathf.Sin(kick * Mathf.PI * 0.5f);
            Vector3 butt = yaw.position + aim * (shoulderLocal + PocketFromShoulder
                + LowReadyDrop * (1 - s) + new Vector3(0, 0.01f, -0.05f) * k);
            float pitch = Mathf.Clamp(AimPitch, -30, 30);
            rifle.rotation = aim * Quaternion.Euler(Mathf.Lerp(LowReadyPitch, pitch, s) - 5 * k, Mathf.Lerp(-LowReadyYaw, 0, s), 0);
            rifle.position = butt - (stock.position - rifle.position);
            Vector3 body = animator.bodyPosition;

            // Firing hand: metacarpals forward and down along the pistol grip, palm against its right side.
            (RightWrist, RightHandRotation) = Hand(AvatarIKGoal.RightHand, rightGrip.position,
                rifle.rotation * new Vector3(-0.1f, -0.45f, 0.88f), rifle.rotation * Vector3.left,
                rightFingerLocal, rightPalmLocal, RightC);
            // Support hand on the vertical foregrip: knuckles forward, palm against its left side.
            (LeftWrist, LeftHandRotation) = Hand(AvatarIKGoal.LeftHand, leftGrip.position,
                rifle.rotation * new Vector3(0.12f, -0.4f, 0.9f), rifle.rotation * Vector3.right,
                leftFingerLocal, leftPalmLocal, LeftC);

            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, weight);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, body + aim * RightElbowHint);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, weight);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, body + aim * LeftElbowHint);

            // Lean a little into the gun and keep the head on the sights (cheek weld).
            animator.SetLookAtWeight(weight, 0.25f * s, 0.6f, 0, 0.5f);
            animator.SetLookAtPosition(body + aim * Quaternion.Euler(Mathf.Clamp(AimPitch, -30, 30) * s, 0, 0) * new Vector3(0.1f, 0.2f, 8));
        }

        // Put the wrist where the palm lands on gripPoint, with the hand turned to match.
        (Vector3, Quaternion) Hand(AvatarIKGoal goal, Vector3 gripPoint, Vector3 finger, Vector3 palm,
            Vector3 fingerLocal, Vector3 palmLocal, Quaternion c)
        {
            finger.Normalize();
            float scale = transform.lossyScale.x;
            Vector3 wrist = gripPoint - finger * WristToPalm * scale - palm * PalmDepth * scale;
            // Wanted bone rotation D with D*fingerLocal = finger and D*palmLocal = palm.
            Quaternion wanted = Quaternion.LookRotation(finger, palm) * Quaternion.Inverse(Quaternion.LookRotation(fingerLocal, palmLocal));
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKRotationWeight(goal, weight);
            animator.SetIKPosition(goal, wrist);
            animator.SetIKRotation(goal, wanted * Quaternion.Inverse(c));
            return (wrist, wanted);
        }
    }
}
