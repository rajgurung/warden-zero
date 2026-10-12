using UnityEngine;

namespace WardenZero
{
    // A Warden in the jeep, laid over the animation like TraversalPose (the procedural seat
    // until the `sit` clip arrives):
    // - Driver: hips on the cushion, thighs forward, shins down to the pedals; hands on the
    //   steering wheel's rim at ten and two, turning with it (WardenHandIK holds); the head
    //   looks into the turn and the body leans against the cornering.
    // - Gunner: standing in the bed behind the gun, facing along it, hands on the spade grips,
    //   knees soft over the bumps.
    // The player's comes from his seat; the squadmate's (driverOf set) is always the driver.
    [DefaultExecutionOrder(110)] // after TraversalPose, whose holds it replaces in the seat
    public class VehiclePose : MonoBehaviour
    {
        public Animator animator;
        public WardenHandIK handIK;
        public Transform yaw; // the facing frame the model hangs under
        public PlayerController player; // the Warden (null for the squadmate)
        public VehicleController driverOf; // the squadmate: the jeep he drives

        // The steering wheel's grips at nine and three o'clock, in the Steering pivot's space (the
        // Tripo wheel's rim tube; ArtSource/M3Vehicles/README.md), and the gun's spade grips in
        // the Gun pivot's (the trunnion's), both at the game scale.
        public static readonly Vector3 WheelLeft = new Vector3(-0.095f, 0, 0.158f) * 1.26f * 1.187f;
        public static readonly Vector3 WheelRight = new Vector3(-0.095f, 0, -0.158f) * 1.26f * 1.187f;
        public static readonly Vector3 GunLeft = new Vector3(-0.113f, -0.053f, -0.412f) * 1.187f;
        public static readonly Vector3 GunRight = new Vector3(0.152f, -0.053f, -0.405f) * 1.187f;
        public const float LookIntoTurn = 30, LeanPerAccel = 1.2f;

        static readonly int[] Sides = { -1, 1 };
        Transform lThigh, rThigh, lLeg, rLeg, lFoot, rFoot, head;
        float weight;
        float lean;

        public VehicleController Jeep => driverOf != null ? driverOf : player != null ? player.Vehicle : null;
        public VehicleController.Seat Seat => driverOf != null ? VehicleController.Seat.Driver : Jeep != null ? Jeep.Occupied : VehicleController.Seat.None;

        void Start()
        {
            lThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            lLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        // The hands' places on the wheel (left, right): on its rim, turned with it.
        public static void WheelGrips(Transform steering, out Vector3 left, out Vector3 right)
        {
            left = steering.TransformPoint(WheelLeft);
            right = steering.TransformPoint(WheelRight);
        }

        // The gun's spade grips (left, right), behind the receiver.
        public static void GunGrips(Transform gun, out Vector3 left, out Vector3 right)
        {
            left = gun.TransformPoint(GunLeft);
            right = gun.TransformPoint(GunRight);
        }

        // Before the Animator: the hand holds.
        void Update()
        {
            var jeep = Jeep;
            var seat = Seat;
            weight = Mathf.MoveTowards(weight, seat == VehicleController.Seat.None ? 0 : 1, Time.deltaTime * 4);
            // On foot TraversalPose owns the holds.
            if (jeep == null || seat == VehicleController.Seat.None || player != null && jeep.Entering) return;
            var rig = jeep.rig;
            var left = new WardenHandIK.Hold { Weight = weight };
            var right = new WardenHandIK.Hold { Weight = weight };
            if (seat == VehicleController.Seat.Driver)
            {
                WheelGrips(rig.steering, out left.Point, out right.Point);
                // Fingers round the rim (up its sides), palms toward the dash (the column, local X).
                left.Fingers = right.Fingers = rig.steering.up;
                left.Palm = right.Palm = rig.steering.right;
            }
            else
            {
                GunGrips(rig.gun, out left.Point, out right.Point);
                // Fists round the upright spade grips, knuckles forward, palms inward.
                left.Fingers = right.Fingers = rig.gun.forward;
                left.Palm = rig.gun.right;
                right.Palm = -rig.gun.right;
            }
            handIK.LeftHold = left;
            handIK.RightHold = right;
            // Lean against the cornering (left in a right turn).
            var v = jeep.Vehicle;
            float side = v.Speed * v.YawRate;
            lean = Mathf.Lerp(lean, Mathf.Clamp(-side * LeanPerAccel, -10, 10), Mathf.Min(1, Time.deltaTime * 5));
        }

        // After the Animator and IK: the legs, the lean, the head.
        void LateUpdate()
        {
            var jeep = Jeep;
            if (jeep == null || weight <= 1e-3f || lThigh == null) return;
            Vector3 U = jeep.transform.up, F = yaw.forward, R = yaw.right;
            if (Seat == VehicleController.Seat.Driver)
            {
                // The player's facing is set afresh each frame (WardenModelView); the
                // squadmate's is the seat's.
                yaw.localRotation = (driverOf != null ? Quaternion.identity : yaw.localRotation) * Quaternion.Euler(0, 0, lean);
                foreach (int side in Sides)
                {
                    bool right = side > 0;
                    // Thighs along the seat, knees a little apart; shins down to the pedals.
                    Aim(right ? rThigh : lThigh, right ? rLeg : lLeg, (F - U * 0.12f + R * side * 0.12f).normalized, weight);
                    Aim(right ? rLeg : lLeg, right ? rFoot : lFoot, (-U + F * 0.35f).normalized, weight);
                }
                if (head != null)
                {
                    // Looking into the turn.
                    float look = Mathf.Clamp(jeep.Vehicle.SteerInput, -1, 1) * LookIntoTurn * weight;
                    head.rotation = Quaternion.AngleAxis(look, U) * head.rotation;
                }
                return;
            }
            // The gunner: knees soft, more over the bumps.
            float soft = 0.18f + Mathf.Clamp01(Mathf.Abs(jeep.rig.BodyPitch) / 6) * 0.15f;
            foreach (int side in Sides)
            {
                bool right = side > 0;
                Aim(right ? rThigh : lThigh, right ? rLeg : lLeg, (-U + F * soft + R * side * 0.08f).normalized, weight);
                Aim(right ? rLeg : lLeg, right ? rFoot : lFoot, (-U - F * soft).normalized, weight);
            }
        }

        // Turn `bone` so the direction to `child` points along `dir`, by weight w.
        static void Aim(Transform bone, Transform child, Vector3 dir, float w)
        {
            if (bone == null || child == null || w <= 0) return;
            Vector3 now = child.position - bone.position;
            if (now.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now, dir), w) * bone.rotation;
        }
    }
}
