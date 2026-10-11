using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    // The traversal course on the real layout (CourseLayout over JungleLayout's ground), driven
    // through Traversal directly: a scripted bot runs it from the LZ to checkpoint A, nothing
    // overlaps the spawn points, the gate can't be walked round, and no beat traps him.
    public class CourseTests
    {
        const float Dt = 1 / 60f;
        static Vector3 Lz => Ground(JungleLayout.Lz);
        static Vector3 Checkpoint => Ground(JungleLayout.Checkpoint);

        [SetUp]
        public void Jungle()
        {
            World.Use(JungleStage.PlayHalfSize, JungleStage.PlayHalfSize, new WallRect[0], new Vector3[0]);
            World.Ground = p => JungleLayout.Height(p.x, p.z);
            World.Blocks = CourseLayout.Blocks;
            World.TraversalEnabled = true;
        }

        [TearDown]
        public void Reset() => World.UseArena();

        static Vector3 Ground(Vector3 p) => new Vector3(p.x, JungleLayout.Height(p.x, p.z), p.z);

        static Block Piece(string name) => CourseLayout.Pieces.Single(p => p.Name == name).Block;

        // A point in a piece's own frame (x along it, z out of its +z face), on the ground.
        static Vector3 P(string piece, float x, float z) => Ground(Piece(piece).ToWorld(new Vector3(x, 0, z)));

        // Flat distance from p to a block's footprint.
        static float Gap(Block b, Vector3 p)
        {
            var l = b.ToLocal(p);
            float dx = Mathf.Max(0, Mathf.Abs(l.x) - b.Half.x), dz = Mathf.Max(0, Mathf.Abs(l.z) - b.Half.y);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        [Test]
        public void NoBlock_OverlapsTheLz_TheSpawns_OrTheCaptureRing()
        {
            float r = GameConfig.PlayerRadius + 0.3f;
            foreach (var piece in CourseLayout.Pieces)
            {
                var b = piece.Block;
                Assert.Greater(Gap(b, Lz), 7 + r, $"{piece.Name} on the LZ ring");
                Assert.Greater(Gap(b, Lz + JungleStage.LandedOffset), r, $"{piece.Name} on the landed spawn");
                Assert.Greater(Gap(b, Checkpoint + JungleStage.NearCheckpointOffset), r, $"{piece.Name} on the near-checkpoint spawn");
                Assert.Greater(Gap(b, Checkpoint + JungleStage.CheckpointAOffset), r, $"{piece.Name} on the checkpoint spawn");
                Assert.Greater(Gap(b, Checkpoint), JungleStage.CaptureRadius + r, $"{piece.Name} in the capture ring");
            }
        }

        [Test]
        public void Heights_FitTheirBeats()
        {
            float Rise(string name, float z) => Piece(name).Top - P(name, 0, z).y;
            Assert.That(Rise("1_Log", -Piece("1_Log").Half.y - 0.6f), Is.InRange(GameConfig.VaultMin, GameConfig.VaultMax), "the log vaults");
            Assert.That(Rise("4_Ledge", -Piece("4_Ledge").Half.y - 0.6f), Is.InRange(GameConfig.VaultMax, GameConfig.MantleMax), "the ledge mantles");
            Assert.That(Rise("6_Terrace", -Piece("6_Terrace").Half.y - 0.6f), Is.InRange(GameConfig.MantleMax, GameConfig.JumpGrabMax), "the terrace is a jump and grab");
            foreach (var side in new[] { -1, 1 })
            {
                float wall = Rise("5_WallVine", side * (Piece("5_WallVine").Half.y + 0.6f));
                Assert.That(wall, Is.InRange(GameConfig.JumpGrabMax, GameConfig.SafeDrop), "the wall: too high to grab, safe to drop from");
            }
            foreach (var piece in CourseLayout.Pieces.Where(p => p.Name.StartsWith("5_Rubble") || p.Name.StartsWith("5_Outcrop")))
            {
                Assert.AreEqual(0, piece.Block.Ledges, piece.Name);
                Assert.AreEqual(0, piece.Block.Climb, piece.Name);
                var b = piece.Block;
                foreach (var side in new[] { new Vector3(b.Half.x + 1, 0, 0), new Vector3(-b.Half.x - 1, 0, 0), new Vector3(0, 0, b.Half.y + 1), new Vector3(0, 0, -b.Half.y - 1) })
                {
                    var o = b.ToWorld(side);
                    Assert.Greater(b.Top - JungleLayout.Height(o.x, o.z), GameConfig.JumpHeight + GameConfig.StepUp + 0.3f, $"{piece.Name}: no jumping onto it");
                }
            }
        }

        // ------------------------------------------------------------------ the bot

        // Drives Traversal with the stick and buttons, step by step along the route.
        class Bot
        {
            public readonly Traversal T = new Traversal();
            public float Time;
            public float Speed;
            public readonly List<string> Log = new List<string>();
            public float WorstFall;
            Vector3 facing = Vector3.forward;

            public Bot(Vector3 start, float speed)
            {
                T.Reset(start);
                Speed = speed;
            }

            void Tick(Vector3 move, bool jump = false, bool drop = false, bool held = false)
            {
                move.y = 0;
                if (move.sqrMagnitude > 1e-4f) facing = move.normalized;
                T.Tick(new TraversalInput
                {
                    Move = move, Velocity = T.Grounded ? move * Speed : Vector3.zero, Facing = facing, Jump = jump, JumpHeld = held, Drop = drop,
                }, Dt);
                Time += Dt;
                if (T.Landed) WorstFall = Mathf.Max(WorstFall, T.FallHeight);
            }

            Vector3 Toward(Vector3 to)
            {
                Vector3 d = to - T.Position;
                d.y = 0;
                return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.zero;
            }

            static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

            void Wait(string what, System.Func<bool> done, System.Action tick, float limit = 20)
            {
                float start = Time;
                while (!done())
                {
                    if (Time - start > limit) Assert.Fail($"bot stuck at '{what}': {T.State} at {T.Position} after {Time:F1} s ({string.Join(" > ", Log)})");
                    tick();
                }
                Log.Add(what);
            }

            // Run to a point on the ground.
            public void Go(string what, Vector3 to, float within = 0.4f) =>
                Wait(what, () => T.Grounded && Flat(T.Position, to) < within, () => Tick(Toward(to)));

            // Run at a point, pressing Space once the probe offers a move, until he stands again.
            public void Use(string what, Vector3 to, TraversalProbe.Kind want)
            {
                Wait(what + " (probe)", () => !T.Grounded, () =>
                {
                    Vector3 m = Toward(to);
                    Tick(m, TraversalProbe.Find(T.Position, m).Kind == want);
                });
                Wait(what, () => T.Grounded || T.State == TraversalState.Hang, () => Tick(Toward(to), held: true));
                if (T.State != TraversalState.Hang) return;
                // A jump and grab: then up.
                Wait(what + " (climb up)", () => T.Grounded, () => Tick(Vector3.zero, T.StateTime > 0.2f));
            }

            // A running jump toward a point, until he lands.
            // The stick tilted for the distance (a level hop's flight at TraversalSpeed), held.
            public void Jump(string what, Vector3 to)
            {
                float flight = 2 * Traversal.JumpSpeed / GameConfig.Gravity;
                Vector3 stick = Toward(to) * Mathf.Min(1, Flat(T.Position, to) / (flight * GameConfig.TraversalSpeed));
                Tick(stick, true);
                Wait(what, () => T.Grounded, () => Tick(stick));
            }

            // C toward an edge to lower into a hang, along it for a while, then let go.
            public void HangAndDrop(string what, Vector3 outward, Vector3 along, float shimmy)
            {
                Tick(outward, drop: true);
                Wait(what + " (lower)", () => T.State == TraversalState.Hang, () => Tick(Vector3.zero));
                for (float s = 0; s < shimmy; s += Dt) Tick(along);
                Tick(Vector3.zero, drop: true);
                Wait(what, () => T.Grounded, () => Tick(Vector3.zero));
            }
        }

        // The route: every beat, the gate's full sequence included.
        static Bot RunCourse(float speed)
        {
            var bot = new Bot(Lz + JungleStage.LandedOffset, speed);
            const TraversalProbe.Kind vault = TraversalProbe.Kind.Vault, mantle = TraversalProbe.Kind.Mantle;
            // 1. Over the log.
            bot.Go("to the log", P("1_Log", 0, -2.2f));
            bot.Use("vault the log", P("1_Log", 0, 2.5f), vault);
            // 2. Up the crates, over to the shelf, down off it.
            bot.Go("to the crate", P("2_Crate", 0, -2));
            bot.Use("onto the crate", P("2_Crate", 0, 0), mantle);
            bot.Use("onto the stack", P("2_CrateStack", 0, 0), mantle);
            bot.Jump("over to the shelf", P("2_Shelf", 0, 0));
            Assert.AreEqual(Piece("2_Shelf").Top, bot.T.Position.y, 0.01f, "landed on the shelf");
            bot.Go("off the shelf", P("2_Shelf", -0.5f, 3.2f));
            // 3. Across the ford, block to block.
            bot.Go("to the ford", P("3_Ford0", 0, -2.0f));
            for (int k = 0; k < 4; k++)
            {
                bot.Jump($"ford block {k}", P($"3_Ford{k}", 0, 0));
                Assert.AreEqual(CourseLayout.FordTop, bot.T.Position.y, 0.01f, $"on ford block {k}");
            }
            bot.Jump("the far bank", P("3_Ford3", 0, 2.2f));
            // 4. Up the rock ledge and down its far side.
            bot.Go("to the ledge", P("4_Ledge", 0, -2.2f));
            bot.Use("up the ledge", P("4_Ledge", 0, 0), mantle);
            bot.Go("off the ledge", P("4_Ledge", 0, 3));
            // 5. The gate: up the vines, along the top, over the far side into a hang, shimmy, drop.
            var vine = Piece("5_WallVine");
            bot.Go("to the wall", P("5_WallVine", 0, -2));
            bot.Use("climb the wall", P("5_WallVine", 0, 0), TraversalProbe.Kind.Climb);
            Assert.AreEqual(vine.Top, bot.T.Position.y, 0.01f, "on the wall top");
            bot.Go("along the top", Ground(P("5_WallPlain", 0, 0)) + Vector3.up * vine.Top, 0.4f);
            bot.Go("back along the top", Ground(P("5_WallVine", -0.8f, 0)) + Vector3.up * vine.Top, 0.4f);
            bot.HangAndDrop("hang and shimmy off the far side", vine.Normal(0), vine.Turn(Vector3.right), 1.2f);
            // 6. Jump and grab the terrace, climb up, down the far side, into the ring.
            bot.Go("to the terrace", P("6_Terrace", 0, -1.9f));
            bot.Use("up the terrace", P("6_Terrace", 0, 0), TraversalProbe.Kind.JumpGrab);
            Assert.AreEqual(Piece("6_Terrace").Top, bot.T.Position.y, 0.01f, "on the terrace");
            bot.Go("off the terrace", P("6_Terrace", 0, 3));
            bot.Go("into checkpoint A", Checkpoint, JungleStage.CaptureRadius - 1);
            return bot;
        }

        [Test]
        public void Bot_RunsTheCourse_AtBaseSpeed()
        {
            var bot = RunCourse(new PlayerStats().Speed * GameConfig.PX);
            Debug.Log($"[Course] base speed: {bot.Time:F1} s, worst fall {bot.WorstFall:F2} m: {string.Join(" > ", bot.Log)}");
            Assert.Less(bot.Time, 120);
            Assert.Less(bot.WorstFall, GameConfig.SafeDrop, "no fall on the course hurts");
        }

        [Test]
        public void Bot_RunsTheCourse_AtMaxUpgrades()
        {
            float speed = new PlayerStats().Speed;
            for (int i = 0; i < 4; i++) speed = Mathf.Round(speed * 1.12f); // Adrenaline x4
            var bot = RunCourse(speed * GameConfig.PX);
            Debug.Log($"[Course] max speed: {bot.Time:F1} s");
            Assert.Less(bot.Time, 120);
        }

        // ------------------------------------------------------------------ reachability

        // Flood fill on a 0.5 m grid of standing spots (no taller block within his radius),
        // inside `allowed`; whether `to` is reached from `from`.
        static bool Reachable(Vector3 from, System.Func<Vector3, bool> to, System.Func<Vector3, bool> allowed)
        {
            const float cell = 0.5f;
            float half = JungleStage.PlayHalfSize - 1;
            int n = Mathf.CeilToInt(half * 2 / cell);
            var seen = new bool[n, n];
            Vector3 At(int i, int k) => Ground(new Vector3(-half + i * cell, 0, -half + k * cell));
            bool Open(Vector3 p) => allowed(p) && (World.PushOutOfBlocks(p, GameConfig.PlayerRadius, p.y) - p).sqrMagnitude < 1e-6f;
            var queue = new Queue<(int, int)>();
            int si = Mathf.RoundToInt((from.x + half) / cell), sk = Mathf.RoundToInt((from.z + half) / cell);
            // The grid point nearest the start may sit against a block: the nearest open one.
            if (!Open(At(si, sk)))
            {
                var near = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) }.Select(d => (si + d.Item1, sk + d.Item2)).Where(c => Open(At(c.Item1, c.Item2)));
                Assert.IsTrue(near.Any(), $"the start {from} is open");
                (si, sk) = near.First();
            }
            queue.Enqueue((si, sk));
            seen[si, sk] = true;
            while (queue.Count > 0)
            {
                var (i, k) = queue.Dequeue();
                if (to(At(i, k))) return true;
                foreach (var (di, dk) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int a = i + di, b = k + dk;
                    if (a < 0 || b < 0 || a >= n || b >= n || seen[a, b]) continue;
                    seen[a, b] = true;
                    if (Open(At(a, b))) queue.Enqueue((a, b));
                }
            }
            return false;
        }

        // Within this far of the trail the gate can't be walked round (the open jungle beyond
        // gets hard corridor bounds in M4).
        const float Corridor = 16;

        static bool NearTrail(Vector3 p, float d) => JungleLayout.DistanceToPath(JungleLayout.Trail, p.x, p.z) < d;

        [Test]
        public void Gate_TheWayRoundIsBlocked()
        {
            Vector3 before = P("5_WallVine", 0, -6), after = P("5_WallVine", 0, 3.5f);
            bool Beyond(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(after.x, after.z)) < 1;
            Assert.IsFalse(Reachable(before, Beyond, p => NearTrail(p, Corridor)), $"walked round the gate within {Corridor} m of the trail");
            // And it is only the gate: without its blocks the same walk is open.
            World.Blocks = CourseLayout.Pieces.Where(p => !p.Name.StartsWith("5_")).Select(p => p.Block).ToArray();
            Assert.IsTrue(Reachable(before, Beyond, p => NearTrail(p, Corridor)));
        }

        [Test]
        public void NoBeat_TrapsHim()
        {
            int checkedPoints = 0;
            foreach (var piece in CourseLayout.Pieces)
            {
                var b = piece.Block;
                for (int a = 0; a < 8; a++)
                {
                    // Just outside the footprint, all round.
                    Vector3 dir = Quaternion.Euler(0, a * 45, 0) * Vector3.forward;
                    Vector3 local = new Vector3(dir.x * (b.Half.x + 1.2f), 0, dir.z * (b.Half.y + 1.2f));
                    Vector3 p = Ground(b.ToWorld(local));
                    if ((World.PushOutOfBlocks(p, GameConfig.PlayerRadius, p.y) - p).sqrMagnitude > 1e-6f) continue; // against another piece
                    checkedPoints++;
                    Assert.IsTrue(Reachable(p, q => NearTrail(q, 1.5f), q => true), $"trapped next to {piece.Name} at {p}");
                }
            }
            Debug.Log($"[Course] {checkedPoints} points round the beats reach the trail");
        }
    }
}
