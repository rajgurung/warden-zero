using UnityEngine;

namespace WardenZero
{
    // The Warden's jump poses, laid over the animation. Each limb bone is aimed at a direction
    // given in his body frame (U = head, F = belly/front, R = his right), after the Animator
    // has posed him, so the result does not depend on the rig's bone axes. Nothing here
    // flutters or oscillates: the play-test found the waving arms unconvincing.
    // - door: crouched in the cabin door, hands up on the frame;
    // - freefall: a calm, symmetric belly-to-earth arch, upper arms out at shoulder height,
    //   forearms bent ~90 degrees toward the head, knees bent ~45 degrees; `track` (W) sweeps
    //   the arms back, `brake` (S) reaches them forward, both smoothed by JungleStage;
    // - canopy: the canopy_hold clip has the arms up on the toggles; `toggleLeft/Right` pull
    //   a hand down (turns), both down is the flare; legs hang together;
    // - crouch: knees buckled, leaning forward (a hard landing) over the land clip; the arms
    //   stay the clip's.
    [DefaultExecutionOrder(100)]
    public class SkydivePose : MonoBehaviour
    {
        public Animator animator;
        public Transform body; // the Warden's root: its axes are his up / front / right
        [Range(0, 1)] public float door;
        [Range(0, 1)] public float freefall;
        [Range(0, 1)] public float canopy;
        [Range(0, 1)] public float crouch;
        [Range(-1, 1)] public float track; // +1 W (dive), -1 S (brake)
        [Range(0, 1)] public float toggleLeft;
        [Range(0, 1)] public float toggleRight;

        static readonly int[] Sides = { -1, 1 };
        Transform lArm, rArm, lFore, rFore, lHand, rHand, lThigh, rThigh, lLeg, rLeg, lFoot, rFoot, spine, chest;

        void Start()
        {
            lArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lFore = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rFore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            lThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            lLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        }

        void LateUpdate()
        {
            float total = door + freefall + canopy + crouch;
            if (total < 0.01f) return;
            Vector3 U = body.up, F = body.forward, R = body.right;

            // Spine: arched back in freefall (chest lifted away from the ground), bent forward
            // in a crouch.
            Bend(spine, R, -10 * freefall + 18 * crouch + 8 * door);
            Bend(chest, R, -8 * freefall + 10 * crouch);

            foreach (int side in Sides)
            {
                var upper = side < 0 ? lArm : rArm;
                var fore = side < 0 ? lFore : rFore;
                var hand = side < 0 ? lHand : rHand;
                Vector3 S = R * side; // out to this side

                // Arms: the arch, the door frame, or a toggle pulled down from the clip's hold.
                Vector3 ffArm = (S + U * (0.15f - track * 0.45f) - F * 0.25f).normalized;
                Vector3 ffFore = (U * (1 - Mathf.Max(0, track) * 0.8f) + S * (0.2f + Mathf.Max(0, track) * 0.6f) - F * 0.2f).normalized;
                float pull = canopy * (side < 0 ? toggleLeft : toggleRight);
                Vector3 cArm = (-U * 0.2f + S * 0.6f + F * 0.1f).normalized;
                Vector3 cFore = (-U * 0.7f + F * 0.2f + S * 0.05f).normalized;
                Vector3 dArm = (S * 0.7f + U * 0.5f + F * 0.3f).normalized;
                Vector3 dFore = (U * 0.9f + S * 0.2f).normalized;
                float armW = freefall + door + pull;
                if (armW > 1e-3f)
                {
                    Aim(upper, fore, (ffArm * freefall + dArm * door + cArm * pull).normalized, Mathf.Clamp01(armW));
                    Aim(fore, hand, (ffFore * freefall + dFore * door + cFore * pull).normalized, Mathf.Clamp01(armW));
                }

                // Legs.
                var thigh = side < 0 ? lThigh : rThigh;
                var leg = side < 0 ? lLeg : rLeg;
                var foot = side < 0 ? lFoot : rFoot;
                Vector3 ffThigh = (-U + S * 0.22f - F * 0.15f).normalized;
                Vector3 ffShin = (-U * 0.7f - F * 0.7f + S * 0.1f).normalized; // ~45 degrees at the knee, heels up
                Vector3 cThigh = (-U + F * 0.05f + S * 0.04f).normalized;
                Vector3 cShin = (-U - F * 0.1f).normalized;
                Vector3 dThigh = (-U * 0.4f + F * 0.9f + S * 0.2f).normalized;
                Vector3 dShin = (-U * 0.95f - F * 0.2f).normalized;
                Vector3 kThigh = (-U * 0.2f + F * 0.95f + S * 0.25f).normalized;
                Vector3 kShin = (-U * 0.85f - F * 0.45f).normalized;
                float legW = Mathf.Clamp01(total);
                Aim(thigh, leg, Blend(ffThigh, cThigh, dThigh, kThigh), legW);
                Aim(leg, foot, Blend(ffShin, cShin, dShin, kShin), legW);
            }
        }

        // Weighted blend of the pose targets with the animated direction (null = keep).
        Vector3? Blend(Vector3 ff, Vector3 c, Vector3 d, Vector3 k)
        {
            float w = freefall + canopy + door + crouch;
            if (w < 1e-3f) return null;
            Vector3 v = ff * freefall + c * canopy + d * door + k * crouch;
            return v.sqrMagnitude > 1e-6f ? (Vector3?)(v.normalized) : null;
        }

        // Turn `bone` so the direction to `child` points along `dir`, by weight w (so the
        // animation shows through while blending in or out).
        static void Aim(Transform bone, Transform child, Vector3? dir, float w)
        {
            if (bone == null || child == null || dir == null || w <= 0) return;
            Vector3 now = child.position - bone.position;
            if (now.sqrMagnitude < 1e-8f) return;
            var turn = Quaternion.FromToRotation(now, dir.Value);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, w) * bone.rotation;
        }

        static void Bend(Transform bone, Vector3 axis, float degrees)
        {
            if (bone != null && Mathf.Abs(degrees) > 0.01f) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
