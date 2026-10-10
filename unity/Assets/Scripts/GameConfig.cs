using UnityEngine;

namespace WardenZero
{
    public enum EnemyType { Grunt, Swarmer, Runner }

    public struct EnemyStats
    {
        public float MaxHealth;
        public float Speed; // metres per second
        public float ContactDamage;
        public int Score;
        public float Radius; // metres
        public float VisualScale; // sprite scale relative to a grunt
        public Color Tint;
        public bool RunnerArt;
    }

    public struct WallRect
    {
        public float MinX, MaxX, MinZ, MaxZ;
    }

    // Tuning carried over from the Babylon version (src/config/*.ts and src/core/Game.ts).
    // Values are kept in the original pixel units and converted with PX, so balance matches.
    public static class GameConfig
    {
        // world.ts: v2 renders in metres, 30 px = 1 m.
        public const float PX = 1f / 30f;

        // constants.ts / world.ts
        public const float ArenaW = 3600 * PX; // 120 m
        public const float ArenaD = 2200 * PX; // ~73 m
        public const float HalfW = ArenaW / 2;
        public const float HalfD = ArenaD / 2;
        public const float WallHeight = 2.4f;

        // playerStats.ts
        public const float PlayerMaxHealth = 100;
        public const float PlayerSpeed = 245 * PX;
        public const float FireInterval = 0.150f;
        public const int BoltDamage = 25;
        public const float BoltSpeed = 720 * PX;
        public const float DashSpeed = 700 * PX;
        public const float DashDuration = 0.150f;
        public const float DashCooldown = 1.5f;

        // Game.ts
        public const float PlayerRadius = 18 * PX;
        public const float BoltLife = 0.9f;
        public const float BoltRadius = 0.15f;
        public const float HurtInvuln = 0.7f;
        public const float ContactCooldown = 0.5f;
        public const float SpawnInterval = 0.16f;
        public const int SpawnBatch = 3;
        public const int MaxEnemies = 80;
        public const float SpawnMin = 720 * PX;
        public const float SpawnMax = 920 * PX;
        public const float WaveClearDelay = 1.4f;

        // Stage.ts camera: behind and above the Warden, angled down.
        public static readonly Vector3 CameraOffset = new Vector3(0, 20, -14);
        public const float CameraFovDegrees = 0.8f * Mathf.Rad2Deg; // Babylon fov is vertical, in radians
        public const float CameraLookAhead = 2.5f;

        // constants.ts COLORS
        public static readonly Color BgDeep = Hex(0x05070f);
        public static readonly Color Panel = Hex(0x141b30);
        public static readonly Color PanelEdge = Hex(0x243154);
        public static readonly Color BgMid = Hex(0x0b1020);
        public static readonly Color Accent = Hex(0x4fd1ff);
        public static readonly Color Magenta = Hex(0xff5ca8);
        public static readonly Color Gold = Hex(0xffd75a);
        public static readonly Color Health = Hex(0xff4d5e);
        public static readonly Color HealthBack = Hex(0x3a1620);
        public static readonly Color TextBright = Hex(0xe6ecff);
        public static readonly Color TextDim = Hex(0x8a96b8);

        // enemies.ts (speed/radius converted to metres).
        public static EnemyStats Enemy(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Swarmer:
                    return new EnemyStats { MaxHealth = 16, Speed = 205 * PX, ContactDamage = 6, Score = 60, Radius = 11 * PX, VisualScale = 0.75f, Tint = Hex(0xbfff5a) };
                case EnemyType.Runner:
                    return new EnemyStats { MaxHealth = 30, Speed = 185 * PX, ContactDamage = 8, Score = 120, Radius = 13 * PX, VisualScale = 0.95f, Tint = Color.white, RunnerArt = true };
                default:
                    return new EnemyStats { MaxHealth = 50, Speed = 105 * PX, ContactDamage = 10, Score = 100, Radius = 16 * PX, VisualScale = 1f, Tint = Color.white };
            }
        }

        // waves.ts: the first two waves.
        public static readonly (EnemyType type, int count)[][] Waves =
        {
            new[] { (EnemyType.Grunt, 14), (EnemyType.Swarmer, 8) },
            new[] { (EnemyType.Grunt, 18), (EnemyType.Swarmer, 16), (EnemyType.Runner, 6) },
        };

        // world.ts WALL_DEFS, in v1 pixel space (centre x, y, width, height).
        static readonly float[,] WallDefs =
        {
            { 1280, 760, 70, 260 }, { 1920, 760, 70, 260 }, { 1280, 1240, 70, 260 }, { 1920, 1240, 70, 260 },
            { 1600, 460, 300, 70 }, { 1600, 1540, 300, 70 }, { 760, 1000, 70, 320 }, { 2440, 1000, 70, 320 },
            { 980, 1400, 220, 70 }, { 2220, 600, 220, 70 },
        };

        public static readonly WallRect[] Walls = BuildWalls();

        static WallRect[] BuildWalls()
        {
            var rects = new WallRect[WallDefs.GetLength(0)];
            for (int i = 0; i < rects.Length; i++)
            {
                // v1 world (y down, origin top-left) -> ground plane (z up-screen, origin centre).
                float cx = (WallDefs[i, 0] - 1800) * PX;
                float cz = -(WallDefs[i, 1] - 1100) * PX;
                float hw = WallDefs[i, 2] * PX / 2;
                float hd = WallDefs[i, 3] * PX / 2;
                rects[i] = new WallRect { MinX = cx - hw, MaxX = cx + hw, MinZ = cz - hd, MaxZ = cz + hd };
            }
            return rects;
        }

        // Push a circle out of every wall and keep it inside the arena (port of resolveCircle).
        public static Vector3 ResolveCircle(Vector3 pos, float r)
        {
            foreach (var w in Walls)
            {
                float cx = Mathf.Clamp(pos.x, w.MinX, w.MaxX);
                float cz = Mathf.Clamp(pos.z, w.MinZ, w.MaxZ);
                float dx = pos.x - cx;
                float dz = pos.z - cz;
                float d2 = dx * dx + dz * dz;
                if (d2 >= r * r) continue;
                if (d2 > 1e-6f)
                {
                    float d = Mathf.Sqrt(d2);
                    pos.x = cx + dx / d * r;
                    pos.z = cz + dz / d * r;
                }
                else
                {
                    // Centre is inside the box: shove out along the shallowest axis.
                    float left = pos.x - (w.MinX - r), right = w.MaxX + r - pos.x;
                    float down = pos.z - (w.MinZ - r), up = w.MaxZ + r - pos.z;
                    float min = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
                    if (min == left) pos.x = w.MinX - r;
                    else if (min == right) pos.x = w.MaxX + r;
                    else if (min == down) pos.z = w.MinZ - r;
                    else pos.z = w.MaxZ + r;
                }
            }
            const float margin = 0.6f;
            pos.x = Mathf.Clamp(pos.x, -HalfW + r + margin, HalfW - r - margin);
            pos.z = Mathf.Clamp(pos.z, -HalfD + r + margin, HalfD - r - margin);
            return pos;
        }

        public static bool PointInWall(Vector3 p)
        {
            foreach (var w in Walls)
                if (p.x > w.MinX && p.x < w.MaxX && p.z > w.MinZ && p.z < w.MaxZ) return true;
            return Mathf.Abs(p.x) > HalfW || Mathf.Abs(p.z) > HalfD;
        }

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
        }
    }
}
