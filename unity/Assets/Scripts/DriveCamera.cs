using UnityEngine;

namespace WardenZero
{
    // The chase camera behind the jeep, as plain pose maths (CameraFollow places the driveCam
    // with it; EditMode tests check it like CameraFollow.BehindPose):
    // - a pivot PivotHeight above the jeep, the camera Distance behind it, Pitch degrees down;
    //   further back and wider with speed;
    // - its heading follows the jeep's through a spring (HalfLife), never lagging more than
    //   MaxLag, so the jeep's side shows in a turn; it follows the jeep's nose, so reversing
    //   keeps it behind;
    // - look input orbits it (OrbitYaw, OrbitPitch), and RecentreAfter seconds after the last
    //   look it eases back (RecentreHalfLife);
    // - portrait screens step back and widen the lens, like CameraFollow.Rig.
    // Collision (WorldCast, skipping the jeep's own box, and the ground) is CameraFollow's.
    public class DriveCamera
    {
        public const float PivotHeight = 2.4f;
        public const float Distance = 8, DistanceAtSpeed = 9.6f;
        public const float Pitch = 10;
        public const float Fov = 58, FovAtSpeed = 68;
        public const float HalfLife = 0.18f, MaxLag = 25;
        public const float OrbitYawMax = 150, OrbitPitchMin = -10, OrbitPitchMax = 35;
        public const float RecentreAfter = 1.2f, RecentreHalfLife = 0.35f;
        public const float PortraitBack = 1.5f;
        public const float Clearance = 0.8f, Radius = 0.3f; // above the ground; the collision sphere

        public float Heading { get; private set; } // where the rig points, degrees
        public float OrbitYaw { get; private set; }
        public float OrbitPitch { get; private set; }
        public float SinceLook { get; private set; } = 100;

        // Straight behind the jeep, no orbit (a cut).
        public void Reset(float heading)
        {
            Heading = heading;
            OrbitYaw = OrbitPitch = 0;
            SinceLook = 100;
        }

        public void Look(float yaw, float pitch)
        {
            OrbitYaw = Mathf.Clamp(OrbitYaw + yaw, -OrbitYawMax, OrbitYawMax);
            OrbitPitch = Mathf.Clamp(OrbitPitch + pitch, OrbitPitchMin, OrbitPitchMax);
            if (Mathf.Abs(yaw) + Mathf.Abs(pitch) > 1e-4f) SinceLook = 0;
        }

        // Advance the spring and the recentring by dt.
        public void Tick(float heading, float dt)
        {
            float k = 1 - Mathf.Pow(0.5f, dt / HalfLife);
            float lag = Mathf.DeltaAngle(heading, Heading);
            lag = Mathf.Clamp(lag * (1 - k), -MaxLag, MaxLag);
            Heading = Mathf.Repeat(heading + lag, 360);
            SinceLook += dt;
            if (SinceLook >= RecentreAfter)
            {
                float e = Mathf.Pow(0.5f, dt / RecentreHalfLife);
                OrbitYaw *= e;
                OrbitPitch *= e;
            }
        }

        public static float SpeedFraction(float speed) => Mathf.Clamp01(speed / GameConfig.Jeep.TopSpeed);

        // The camera's pose and lens for the jeep at `origin` (no collision).
        public Pose Pose(Vector3 origin, float speed, float aspect, out float fov, out Vector3 pivot)
        {
            float f = SpeedFraction(speed);
            float narrow = Mathf.Clamp01(1 - aspect);
            float distance = Mathf.Lerp(Distance, DistanceAtSpeed, f) + PortraitBack * narrow;
            fov = Mathf.Lerp(Fov, FovAtSpeed, f);
            if (aspect < 1) fov = Mathf.Min(80, Camera.HorizontalToVerticalFieldOfView(Mathf.Lerp(62, 70, f), aspect));
            var rot = Quaternion.Euler(Pitch + OrbitPitch, Heading + OrbitYaw, 0);
            pivot = origin + Vector3.up * PivotHeight;
            return new Pose(pivot - rot * Vector3.forward * distance, rot);
        }

        // Pulled in along the arm to the first thing behind the jeep (its own box skipped), and
        // kept Clearance above the ground.
        public static Vector3 Collide(Vector3 pivot, Vector3 camera)
        {
            Vector3 arm = camera - pivot;
            float len = arm.magnitude;
            if (len > 1e-3f && WorldCast.Cast(pivot, arm / len, len, Radius, false, out float hit, BlockFlags.Vehicle))
                camera = pivot + arm / len * Mathf.Max(hit, 1);
            float ground = World.HeightAt(camera) + Clearance;
            if (camera.y < ground) camera.y = ground;
            return camera;
        }

        // The high view while driving: world-aligned above and behind the jeep, looking ahead
        // along its velocity. Under the tree crowns (about 12 m up), like the on-foot high view.
        public const float HighUp = 10, HighBack = 12.5f, HighLead = 0.8f, HighLeadMax = 14;

        public static Vector3 HighFocus(Vector3 origin, Vector2 velocity) =>
            origin + Vector3.ClampMagnitude(new Vector3(velocity.x, 0, velocity.y) * HighLead, HighLeadMax);

        // ------------------------------------------------------------------ the gun

        // The gunner's views: Close over his shoulder behind the gun, Wide further back and up.
        // The camera is the aim; the gun follows it.
        // Close sits high over his right shoulder (he stands behind the gun), so he doesn't
        // hide what the gun points at.
        public const float GunPivotUp = 1.5f, GunBack = 3.2f, GunRight = 0.95f, GunRaise = 0.35f, GunFov = 50, GunZoomFov = 32;
        public const float GunWideBack = 6, GunWideUp = 2.2f, GunWideFov = 60;

        public static Pose GunPose(Vector3 yoke, float yaw, float pitch, bool wide, out Vector3 pivot)
        {
            var rot = Quaternion.Euler(-pitch, yaw, 0);
            pivot = yoke + Vector3.up * GunPivotUp;
            Vector3 at = wide
                ? pivot - rot * Vector3.forward * GunWideBack + Vector3.up * GunWideUp
                : pivot - rot * Vector3.forward * GunBack + rot * Vector3.right * GunRight + Vector3.up * GunRaise;
            // Wide looks down at the gun's line from above: aim along the same ray past the pivot.
            Vector3 target = pivot + rot * Vector3.forward * 30;
            return new Pose(at, Quaternion.LookRotation(target - at));
        }
    }
}
