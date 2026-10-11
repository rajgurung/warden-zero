using Unity.Cinemachine;
using UnityEngine;

namespace WardenZero
{
    // Keeps the behind-view camera out of trunks, rocks, walls and the ground (WorldCast; the
    // game has no colliders, so Cinemachine's own obstacle avoidance has nothing to hit).
    // After the body stage places the camera, spheres are cast like ThirdPersonFollow's own
    // avoidance: from the follow target (the pivot inside the Warden) to the shoulder point,
    // then from there back to the camera. The camera pulls in at once to where the sphere
    // first touches something, and eases back out once the way is clear, so a trunk right
    // behind him leaves it just over his shoulder. Last, it is lifted to stay above the
    // ground. Also carries CameraFollow's shake.
    public class CameraCollision : CinemachineExtension
    {
        public float radius = 0.3f;
        public float clearance = 0.45f; // lens above the ground
        public float easeOut = 4; // 1/s

        [System.NonSerialized] public Vector3 shake;
        public float Distance { get; private set; } = -1; // current camera distance behind the shoulder point

        public void ResetState() => Distance = -1;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Body || vcam.Follow == null) return;
            Vector3 pivot = vcam.Follow.position;
            var heading = Quaternion.Euler(0, vcam.Follow.eulerAngles.y, 0);
            var rig = vcam.GetComponent<CinemachineThirdPersonFollow>();
            Vector3 shoulder = pivot + heading * (rig != null ? rig.ShoulderOffset : GameConfig.ShoulderOffset);
            Vector3 toShoulder = shoulder - pivot;
            float reach = toShoulder.magnitude;
            if (reach > 1e-3f && WorldCast.Cast(pivot, toShoulder / reach, reach, radius, false, out float blocked))
                shoulder = pivot + toShoulder / reach * blocked;

            Vector3 offset = state.RawPosition - shoulder;
            float full = offset.magnitude;
            if (full < 1e-3f) return;
            Vector3 dir = offset / full;
            float allowed = WorldCast.Cast(shoulder, dir, full, radius, false, out float hit) ? hit : full;
            if (deltaTime < 0 || Distance < 0 || allowed < Distance) Distance = allowed;
            else Distance = Mathf.Lerp(Distance, allowed, 1 - Mathf.Exp(-easeOut * deltaTime));
            Vector3 p = shoulder + dir * Mathf.Min(Distance, full);
            float floor = World.HeightAt(p) + clearance;
            if (p.y < floor) p.y = floor;
            state.RawPosition = p + shake;
        }
    }
}
