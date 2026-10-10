using UnityEngine;

namespace WardenZero
{
    // The playable area of the current scene: bounds, solid walls, and tree trunks that
    // only block the Warden (as in v1's jungle). Defaults to the arena.
    public static class World
    {
        public static float HalfW { get; private set; } = GameConfig.HalfW;
        public static float HalfD { get; private set; } = GameConfig.HalfD;
        public static WallRect[] Walls { get; private set; } = GameConfig.Walls;
        // x, z = trunk centre; y = trunk radius.
        public static Vector3[] Trunks { get; private set; } = new Vector3[0];
        // Ground height at a point (the jungle's terrain); flat at 0 when null.
        public static System.Func<Vector3, float> Ground;

        public static float HeightAt(Vector3 p) => Ground != null ? Ground(p) : 0;

        public static void UseArena()
        {
            Use(GameConfig.HalfW, GameConfig.HalfD, GameConfig.Walls, new Vector3[0]);
        }

        // Flat ground; the jungle sets Ground after this.
        public static void Use(float halfW, float halfD, WallRect[] walls, Vector3[] trunks)
        {
            Ground = null;
            HalfW = halfW;
            HalfD = halfD;
            Walls = walls;
            Trunks = trunks;
        }

        // Push a circle out of any trunk it overlaps (the Warden only).
        public static Vector3 PushOutOfTrunks(Vector3 pos, float r)
        {
            foreach (var t in Trunks)
            {
                float dx = pos.x - t.x, dz = pos.z - t.z;
                float min = r + t.y;
                float d2 = dx * dx + dz * dz;
                if (d2 >= min * min) continue;
                float d = Mathf.Sqrt(d2);
                if (d < 1e-4f) { dx = 1; dz = 0; d = 1; }
                pos.x = t.x + dx / d * min;
                pos.z = t.z + dz / d * min;
            }
            return pos;
        }
    }
}
