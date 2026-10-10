using UnityEngine;

namespace WardenZero
{
    public enum EnemyType { Grunt, Swarmer, Runner, Brute, Tank, Skeleton, Spider, Demon, Boss, Spitter, Warlord }

    // Which v1 sprite set an enemy uses: Toon walk cycles or a Kenney pixel tile.
    public enum EnemyArt { Grunt, Runner, Skeleton, Spider, Demon }

    public struct EnemyStats
    {
        public EnemyType Type;
        public float MaxHealth;
        public float Speed; // metres per second
        public float ContactDamage;
        public int Score;
        public float Radius; // metres
        public float VisualScale; // sprite scale relative to a grunt (v1: height = radius x 5 px)
        public Color Tint;
        public Color FxColor; // particle colour on hit and death
        public EnemyArt Art;
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

        // Player tuning (speed, fire rate, dash, bomb...) lives in PlayerStats, since upgrades change it.

        // Game.ts
        public const float PlayerRadius = 18 * PX;
        // Bolts fly level at the rifle's height; the cursor is projected onto this plane so
        // every shot passes under the cursor (Babylon Input.updateAim uses its gun height, 1.2 m).
        public const float AimHeight = 2.02f; // the 3D Warden's muzzle height (measured in PlayMode)
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
        public const int GemScore = 50;
        public const float HeartHeal = 18;
        public const int CoinValue = 25;
        public const float PickupLife = 8;
        public const float PickupChance = 0.25f;
        public const float HeartShare = 0.35f;
        public const float BossSummonEvery = 4;
        public const float AutoAimRange = 22;
        public const float MultishotSpread = 8; // degrees between bolts
        public const float DyingTime = 1.1f; // death or boss-kill beat before the result screen

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

        // enemies.ts (speed/radius converted to metres). Sizes follow v1, where every
        // sprite was drawn radius x 5 px tall, so the scale is radius / 16 (a grunt).
        public static EnemyStats Enemy(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Swarmer: return Make(type, 16, 205, 6, 60, 11, 0xbfff5a, 0xbfff5a, EnemyArt.Grunt);
                case EnemyType.Runner: return Make(type, 30, 185, 8, 120, 13, -1, 0xffd75a, EnemyArt.Runner);
                case EnemyType.Brute: return Make(type, 260, 54, 26, 320, 30, 0x6f9a4f, 0xff7a59, EnemyArt.Grunt);
                case EnemyType.Tank: return Make(type, 160, 66, 20, 250, 24, 0xb15cff, 0xb15cff, EnemyArt.Grunt);
                case EnemyType.Skeleton: return Make(type, 45, 120, 10, 110, 15, -1, 0xe6dcc0, EnemyArt.Skeleton);
                case EnemyType.Spider: return Make(type, 22, 175, 7, 90, 12, -1, 0x9a6a44, EnemyArt.Spider);
                case EnemyType.Demon: return Make(type, 70, 140, 14, 160, 16, -1, 0xff5a5a, EnemyArt.Demon);
                case EnemyType.Boss: return Make(type, 60000, 95, 45, 5000, 70, 0x9b2230, 0xff3344, EnemyArt.Grunt);
                case EnemyType.Spitter: return Make(type, 40, 120, 8, 140, 14, 0x7fd23a, 0x9bff67, EnemyArt.Runner);
                case EnemyType.Warlord: return Make(type, 1400, 60, 22, 1500, 50, 0x356a2a, 0x9b2230, EnemyArt.Grunt);
                default: return Make(type, 50, 105, 10, 100, 16, -1, 0xff7a59, EnemyArt.Grunt);
            }
        }

        // Pixel values in, metres out. tint -1 = untinted.
        static EnemyStats Make(EnemyType type, float hp, float speed, float contact, int score, float radius, int tint, int fx, EnemyArt art)
        {
            return new EnemyStats
            {
                Type = type, MaxHealth = hp, Speed = speed * PX, ContactDamage = contact, Score = score,
                Radius = radius * PX, VisualScale = radius / 16f, Tint = tint < 0 ? Color.white : Hex(tint), FxColor = Hex(fx), Art = art,
            };
        }

        // Spitter (v1 JungleScene): stops inside 340 px and spits every 2.2 s.
        public const float SpitRange = 340 * PX;
        public const float SpitInterval = 2.2f;
        public const float SpitWindup = 0.16f;
        public const float SpitSpeed = 300 * PX;
        public const float SpitLife = 2.6f;
        public const float SpitDamage = 12;

        // waves.ts: eight waves, then the Colossus.
        public static readonly (EnemyType type, int count)[][] Waves =
        {
            new[] { (EnemyType.Grunt, 14), (EnemyType.Swarmer, 8) },
            new[] { (EnemyType.Grunt, 18), (EnemyType.Swarmer, 16), (EnemyType.Runner, 6) },
            new[] { (EnemyType.Grunt, 22), (EnemyType.Swarmer, 24), (EnemyType.Runner, 10), (EnemyType.Brute, 1), (EnemyType.Skeleton, 6) },
            new[] { (EnemyType.Grunt, 26), (EnemyType.Swarmer, 30), (EnemyType.Runner, 14), (EnemyType.Brute, 2), (EnemyType.Tank, 2), (EnemyType.Skeleton, 8), (EnemyType.Spider, 8) },
            new[] { (EnemyType.Grunt, 30), (EnemyType.Swarmer, 40), (EnemyType.Runner, 18), (EnemyType.Brute, 3), (EnemyType.Tank, 3), (EnemyType.Skeleton, 10), (EnemyType.Spider, 12), (EnemyType.Demon, 3) },
            new[] { (EnemyType.Grunt, 34), (EnemyType.Swarmer, 48), (EnemyType.Runner, 22), (EnemyType.Brute, 4), (EnemyType.Tank, 4), (EnemyType.Skeleton, 12), (EnemyType.Spider, 16), (EnemyType.Demon, 5) },
            new[] { (EnemyType.Grunt, 38), (EnemyType.Swarmer, 56), (EnemyType.Runner, 26), (EnemyType.Brute, 5), (EnemyType.Tank, 5), (EnemyType.Skeleton, 14), (EnemyType.Spider, 20), (EnemyType.Demon, 7) },
            new[] { (EnemyType.Grunt, 44), (EnemyType.Swarmer, 70), (EnemyType.Runner, 30), (EnemyType.Brute, 7), (EnemyType.Tank, 7), (EnemyType.Skeleton, 16), (EnemyType.Spider, 26), (EnemyType.Demon, 10) },
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

        // Push a circle out of every wall and keep it inside the play area (port of resolveCircle).
        // Uses the current scene's World (the arena by default).
        public static Vector3 ResolveCircle(Vector3 pos, float r)
        {
            foreach (var w in World.Walls)
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
            pos.x = Mathf.Clamp(pos.x, -World.HalfW + r + margin, World.HalfW - r - margin);
            pos.z = Mathf.Clamp(pos.z, -World.HalfD + r + margin, World.HalfD - r - margin);
            return pos;
        }

        public static bool PointInWall(Vector3 p)
        {
            foreach (var w in World.Walls)
                if (p.x > w.MinX && p.x < w.MaxX && p.z > w.MinZ && p.z < w.MaxZ) return true;
            return Mathf.Abs(p.x) > World.HalfW || Mathf.Abs(p.z) > World.HalfD;
        }

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
        }
    }
}
