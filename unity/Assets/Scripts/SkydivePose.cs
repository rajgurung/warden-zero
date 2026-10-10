using UnityEngine;

namespace WardenZero
{
    // The Warden's jump poses, laid over the animation (Tripo has no skydiving clips):
    // freefall = the arch (arms out and bent up, knees bent), canopy = hands up on the
    // steering toggles. Weights blend between them and the normal animation.
    [DefaultExecutionOrder(100)]
    public class SkydivePose : MonoBehaviour
    {
        public Animator animator;
        public Transform body; // the Warden's root: its axes are his up / front / right
        [Range(0, 1)] public float freefall;
        [Range(0, 1)] public float canopy;

        // Degrees about the body's front axis (arms) and right axis (legs).
        public float archArm = 80, archElbow = 75, archKnee = 65;
        public float canopyArm = 150, canopyElbow = 20;

        Transform lArm, rArm, lFore, rFore, lLeg, rLeg, lThigh, rThigh;

        void Start()
        {
            lArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lFore = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rFore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            lThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            lLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        }

        void LateUpdate()
        {
            if (freefall < 0.01f && canopy < 0.01f) return;
            Vector3 front = body.forward, right = body.right;
            float arm = archArm * freefall + canopyArm * canopy;
            float elbow = archElbow * freefall + canopyElbow * canopy;
            // Left arm swings out to his left about the front axis, the right one mirrored.
            Turn(lArm, -arm, front);
            Turn(rArm, arm, front);
            Turn(lFore, -elbow, front);
            Turn(rFore, elbow, front);
            Turn(lThigh, -12 * freefall, right);
            Turn(rThigh, -12 * freefall, right);
            Turn(lLeg, archKnee * freefall, right);
            Turn(rLeg, archKnee * freefall, right);
        }

        static void Turn(Transform bone, float degrees, Vector3 axis)
        {
            if (bone != null) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
