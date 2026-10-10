using UnityEngine;

namespace WardenZero
{
    // Two-handed rifle hold. Sits on the Animator's GameObject (OnAnimatorIK is only called
    // there). The rifle hangs off the body frame, so both grips are known before IK runs:
    // the right hand goes to the pistol grip, the left to the handguard, and the elbow hints
    // sit below and outside each arm so the elbows bend down instead of flipping up. Only
    // positions are set; the hands keep the fire clip's gripping orientation.
    [RequireComponent(typeof(Animator))]
    public class WardenHandIK : MonoBehaviour
    {
        public Transform rightGrip;
        public Transform leftGrip;
        public Transform rightElbowHint;
        public Transform leftElbowHint;
        [Range(0, 1)] public float weight = 1;

        Animator animator;

        void Awake()
        {
            animator = GetComponent<Animator>();
        }

        void OnAnimatorIK(int layer)
        {
            Set(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, rightGrip, rightElbowHint);
            Set(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, leftGrip, leftElbowHint);
        }

        void Set(AvatarIKGoal goal, AvatarIKHint hint, Transform target, Transform hintTarget)
        {
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKRotationWeight(goal, 0);
            animator.SetIKHintPositionWeight(hint, weight);
            if (weight <= 0) return;
            animator.SetIKPosition(goal, target.position);
            animator.SetIKHintPosition(hint, hintTarget.position);
        }
    }
}
