using UnityEngine;

namespace WardenZero
{
    // The Warden's traversal poses, laid over the animation like SkydivePose (and the fallback
    // for any traversal clip the art pass rejects). Hands go through WardenHandIK holds, set
    // here each frame before the Animator's IK pass; legs are aimed in his body frame after it.
    // - Hang and Lower: both hands on the edge, legs hanging together, a little toward the wall.
    // - Mantle and ClimbUp: hands on the edge while he rises, knees tucked as he comes over.
    // - Vault: the body sinks, the left hand plants on the top and both legs tuck to the side.
    // - WallClimb: hands reach up the face in turn, knees step up in turn.
    // - Air: knees tuck on the way up, legs reach down on the way down.
    // - Landing: a short knee dip, deeper for a longer fall.
    [DefaultExecutionOrder(100)]
    public class TraversalPose : MonoBehaviour
    {
        public Animator animator;
        public PlayerController player;
        public WardenHandIK handIK;
        public Transform yaw; // his facing frame; the model hangs under it
        public WardenModelView view; // a traversal clip playing leaves the legs to it

        static readonly int[] Sides = { -1, 1 };
        Transform lThigh, rThigh, lLeg, rLeg, lFoot, rFoot;
        float legWeight; // eased in and out of the leg poses
        float dip; // landing knee dip, 0..1
        float climbPhase;

        public float Sink { get; private set; } // metres the body drops below his feet' path (vault)
        public float Roll { get; private set; } // degrees the body rolls onto the support arm (vault)

        void Start()
        {
            lThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            lLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        // Before the Animator: hand holds and the body's sink for this frame.
        void Update()
        {
            var t = player.Traversal;
            float dt = Time.deltaTime;
            var left = new WardenHandIK.Hold();
            var right = new WardenHandIK.Hold();
            float sink = 0, roll = 0;
            if (World.TraversalEnabled && t.Block >= 0 && t.Attached)
            {
                Vector3 n = t.Normal, along = t.Tangent;
                Vector3 edge = TraversalProbe.OnEdge(t.EdgeA, t.EdgeB, t.Along);
                // Which way along the edge is his left (he faces the wall: -n).
                Vector3 leftward = Vector3.Cross(-n, Vector3.up).normalized;
                switch (t.State)
                {
                    case TraversalState.Hang:
                    case TraversalState.Lower:
                        left = OnTop(edge + leftward * 0.28f, n, 1);
                        right = OnTop(edge - leftward * 0.28f, n, 1);
                        break;
                    case TraversalState.Mantle:
                    case TraversalState.ClimbUp:
                    {
                        // Hands on the edge until he is up over it, then off.
                        float w = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.7f, 0.95f, t.Progress));
                        left = OnTop(edge + leftward * 0.3f, n, w);
                        right = OnTop(edge - leftward * 0.3f, n, w);
                        break;
                    }
                    case TraversalState.Vault:
                    {
                        // The support hand on the top at his left, from the jump to the clear.
                        float u = t.Progress;
                        float w = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.05f, 0.25f, u)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.55f, 0.75f, u)));
                        left = OnTop(edge + leftward * 0.25f - n * 0.35f, n, w);
                        // Lowest while the hand is down, back up for the landing; the body rolls
                        // onto the support arm.
                        float low = Mathf.Sin(Mathf.Clamp01(u / 0.8f) * Mathf.PI);
                        sink = low * 1.15f;
                        roll = low * 22;
                        break;
                    }
                    case TraversalState.WallClimb:
                    {
                        // Hands up the face in turn, with the climb.
                        climbPhase += (player.transform.position.y - lastY) * 2.2f;
                        float hands = player.transform.position.y + GameConfig.HangDepth - 0.1f;
                        float l = Mathf.Sin(climbPhase) * 0.22f, r = -l;
                        left = OnFace(new Vector3(edge.x, Mathf.Min(hands + l, t.Top), edge.z) + leftward * 0.3f, n);
                        right = OnFace(new Vector3(edge.x, Mathf.Min(hands + r, t.Top), edge.z) - leftward * 0.3f, n);
                        break;
                    }
                }
            }
            lastY = player.transform.position.y;
            handIK.LeftHold = left;
            handIK.RightHold = right;
            Sink = Mathf.MoveTowards(Sink, sink, dt * 5);
            Roll = Mathf.MoveTowards(Roll, roll, dt * 100);
            yaw.localPosition = Vector3.down * Sink;
            // WardenModelView set the facing this frame; the roll (left shoulder down) goes on top.
            if (Roll > 0.01f) yaw.localRotation *= Quaternion.Euler(0, 0, Roll);
            if (t.Landed) dip = Mathf.Clamp01(t.FallHeight / 2.5f);
            dip = Mathf.MoveTowards(dip, 0, dt * 3);
        }

        float lastY;

        // Palm down on a top edge, fingers over it.
        static WardenHandIK.Hold OnTop(Vector3 p, Vector3 n, float w) =>
            new WardenHandIK.Hold { Weight = w, Point = p - n * 0.05f + Vector3.up * 0.02f, Fingers = -n, Palm = Vector3.down };

        // Palm against a wall face, fingers up.
        static WardenHandIK.Hold OnFace(Vector3 p, Vector3 n) =>
            new WardenHandIK.Hold { Weight = 1, Point = p + n * 0.04f, Fingers = Vector3.up, Palm = -n };

        // After the Animator and IK: the legs.
        void LateUpdate()
        {
            var t = player.Traversal;
            if (!World.TraversalEnabled || lThigh == null) return;
            Vector3 U = Vector3.up, F = yaw.forward, R = yaw.right;
            Vector3? thigh = null, shin = null;
            Vector3? thighR = null, shinR = null;
            float want = 1;
            switch (t.State)
            {
                case TraversalState.Hang:
                case TraversalState.Lower:
                {
                    // Hanging together, toes toward the wall; a shimmy swings them a little.
                    thigh = (-U + F * 0.12f).normalized;
                    shin = (-U + F * 0.04f).normalized;
                    break;
                }
                case TraversalState.Mantle:
                case TraversalState.ClimbUp:
                {
                    // One knee up onto the edge, then both tucked as he comes over.
                    float over = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.4f, 0.8f, t.Progress));
                    thigh = Vector3.Lerp(-U + F * 0.15f, F * 0.85f - U * 0.35f, over).normalized;
                    shin = (-U * 0.9f - F * 0.3f).normalized;
                    thighR = (F * 0.9f - U * 0.3f).normalized;
                    shinR = (-U * 0.85f - F * 0.4f).normalized;
                    break;
                }
                case TraversalState.Vault:
                {
                    // Both legs swing up and to his right, knees bent, clear of the top.
                    float tuck = Mathf.Sin(Mathf.Clamp01(t.Progress) * Mathf.PI);
                    thigh = Vector3.Lerp(-U, F * 0.55f + R * 0.75f - U * 0.1f, tuck).normalized;
                    shin = Vector3.Lerp(-U, -U * 0.55f + R * 0.6f - F * 0.4f, tuck).normalized;
                    thighR = thigh;
                    shinR = shin;
                    break;
                }
                case TraversalState.WallClimb:
                {
                    // Knees step up the face in turn.
                    float l = Mathf.Sin(climbPhase) * 0.5f + 0.5f;
                    thigh = Vector3.Lerp(-U + F * 0.2f, F * 0.75f - U * 0.4f, l).normalized;
                    shin = (-U - F * 0.15f).normalized;
                    thighR = Vector3.Lerp(-U + F * 0.2f, F * 0.75f - U * 0.4f, 1 - l).normalized;
                    shinR = shin;
                    break;
                }
                case TraversalState.Air:
                {
                    // Knees up on the way up, reaching down on the way down.
                    float up = Mathf.Clamp01(t.VerticalSpeed / Traversal.JumpSpeed);
                    thigh = Vector3.Lerp(-U + F * 0.15f, F * 0.6f - U * 0.6f, up).normalized;
                    shin = (-U * 0.9f - F * Mathf.Lerp(0.15f, 0.5f, up)).normalized;
                    thighR = Vector3.Lerp(-U + F * 0.05f, F * 0.3f - U * 0.8f, up).normalized;
                    shinR = shin;
                    want = 0.85f;
                    break;
                }
                default:
                    want = 0;
                    break;
            }
            // A clip has the legs (the takeoff, the climbs): no leg pose over it.
            if (view != null && view.Action == null && view.BaseState != "Locomotion") want = 0;
            legWeight = Mathf.MoveTowards(legWeight, want, Time.deltaTime * 6);
            if (legWeight > 1e-3f && thigh != null)
            {
                foreach (int side in Sides)
                {
                    bool right = side > 0;
                    var th = right ? thighR ?? thigh : thigh;
                    var sh = right ? shinR ?? shin : shin;
                    Vector3 spread = R * side * 0.08f;
                    Aim(right ? rThigh : lThigh, right ? rLeg : lLeg, (th.Value + spread).normalized, legWeight);
                    Aim(right ? rLeg : lLeg, right ? rFoot : lFoot, sh.Value, legWeight);
                }
            }
            // A landing's knee dip, on the animated legs.
            if (dip > 1e-3f && t.Grounded)
            {
                foreach (int side in Sides)
                {
                    var thighB = side > 0 ? rThigh : lThigh;
                    var leg = side > 0 ? rLeg : lLeg;
                    var foot = side > 0 ? rFoot : lFoot;
                    Aim(thighB, leg, (F * 0.5f - U + R * side * 0.1f).normalized, dip * 0.6f);
                    Aim(leg, foot, (-U - F * 0.35f).normalized, dip * 0.6f);
                }
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
