using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    // The Warden's traversal rules on flat ground with test blocks (no scene).
    public class TraversalTests
    {
        const float Dt = 1 / 60f;

        [SetUp]
        public void Flat() => World.Use(50, 50, new WallRect[0], new Vector3[0]);

        [TearDown]
        public void Reset() => World.UseArena();

        // A box facing south (its -z face, face 2, toward a Warden coming from the south).
        static Block Box(float z, float top, float depth = 1, float width = 6, BlockFlags flags = BlockFlags.Vault | BlockFlags.Ledge, int climb = 0, float bottom = 0)
        {
            return new Block
            {
                Center = new Vector2(0, z + depth / 2), Half = new Vector2(width / 2, depth / 2), Bottom = bottom, Top = top,
                Flags = flags | (climb != 0 ? BlockFlags.Climbable : 0), Ledges = Block.AllEdges, Climb = climb,
            };
        }

        static Traversal At(Vector3 feet)
        {
            var t = new Traversal();
            t.Reset(feet);
            return t;
        }

        static TraversalInput North(bool jump = false) => new TraversalInput
        {
            Move = Vector3.forward, Velocity = Vector3.forward * 8, Facing = Vector3.forward, Jump = jump,
        };

        static TraversalInput Still(bool jump = false) => new TraversalInput { Facing = Vector3.forward, Jump = jump };

        // Tick until `until` holds or `seconds` pass; returns the seconds taken (or -1).
        static float Run(Traversal t, TraversalInput input, float seconds, System.Func<Traversal, bool> until = null)
        {
            for (float s = 0; s < seconds; s += Dt)
            {
                t.Tick(input, Dt);
                input.Jump = input.Drop = false;
                if (until != null && until(t)) return s;
            }
            return until == null ? seconds : -1;
        }

        // ------------------------------------------------------------------ ground, jump and fall

        [Test]
        public void Jump_ReachesItsApex_AndLands()
        {
            var t = At(Vector3.zero);
            t.Tick(Still(true), Dt);
            Assert.IsTrue(t.Jumped);
            Assert.AreEqual(TraversalState.Air, t.State);
            float peak = 0;
            float took = Run(t, Still(), 2, x => { peak = Mathf.Max(peak, x.Position.y); return x.Landed; });
            Assert.AreEqual(GameConfig.JumpHeight, peak, 0.05f);
            Assert.AreEqual(2 * Traversal.JumpSpeed / GameConfig.Gravity, took, 0.05f, "up and down");
            Assert.AreEqual(GameConfig.JumpHeight, t.FallHeight, 0.05f);
            Assert.AreEqual(0, t.Position.y, 1e-4f);
        }

        [Test]
        public void Jump_UsesTheFixedTraversalSpeed()
        {
            var t = At(Vector3.zero);
            var fast = North(true);
            fast.Velocity = Vector3.forward * 20; // max upgrades and then some
            t.Tick(fast, Dt);
            Assert.AreEqual(GameConfig.TraversalSpeed, t.AirVelocity.magnitude, 1e-3f);
        }

        [Test]
        public void Coyote_ALateJumpOffAnEdgeStillCounts()
        {
            // A 2 m platform with no ledges; he runs off its north edge.
            World.Blocks = new[] { new Block { Center = new Vector2(0, 0), Half = new Vector2(3, 3), Bottom = 0, Top = 2 } };
            var t = At(new Vector3(0, 2, 2));
            Assert.Greater(Run(t, North(), 1, x => x.State == TraversalState.Air), 0);
            Run(t, North(), 0.08f);
            t.Tick(North(true), Dt);
            Assert.IsTrue(t.Jumped, "within the coyote time");

            var late = At(new Vector3(0, 2, 2));
            Run(late, North(), 1, x => x.State == TraversalState.Air);
            Run(late, North(), GameConfig.CoyoteTime + 0.05f);
            late.Tick(North(true), Dt);
            Assert.IsFalse(late.Jumped, "too late");
        }

        [Test]
        public void Buffer_APressJustBeforeLandingJumps()
        {
            var t = At(new Vector3(0, 0, 0));
            t.Tick(Still(true), Dt);
            // About 0.1 s before touchdown.
            Run(t, Still(), 2, x => x.VerticalSpeed < 0 && x.Position.y < 0.6f);
            t.Tick(Still(true), Dt);
            Run(t, Still(), 0.3f, x => x.Jumped);
            Assert.IsTrue(t.Jumped, "buffered");

            var early = At(Vector3.zero);
            early.Tick(Still(true), Dt);
            Run(early, Still(), 2, x => x.VerticalSpeed < 0);
            early.Tick(Still(true), Dt); // ~0.3 s before touchdown
            Run(early, Still(), 1, x => x.Landed);
            Run(early, Still(), 0.1f);
            Assert.AreEqual(TraversalState.Grounded, early.State, "too early to count");
        }

        [Test]
        public void Landing_OnABlockTop()
        {
            // 0.9 m, no ledges (so Space is a plain jump), 2 m ahead.
            var b = Box(1.5f, 0.9f, 2, 6, BlockFlags.None);
            b.Ledges = 0;
            World.Blocks = new[] { b };
            var t = At(Vector3.zero);
            t.Tick(North(true), Dt);
            Run(t, North(), 2, x => x.Landed);
            Assert.AreEqual(TraversalState.Grounded, t.State);
            Assert.AreEqual(0.9f, t.Position.y, 1e-4f);
            Assert.IsTrue(b.Contains(t.Position));
        }

        [Test]
        public void WalkingOffAnEdge_Falls()
        {
            World.Blocks = new[] { new Block { Center = new Vector2(0, 0), Half = new Vector2(3, 3), Bottom = 0, Top = 2 } };
            var t = At(new Vector3(0, 2, 2));
            Run(t, North(), 3, x => x.Landed);
            Assert.AreEqual(0, t.Position.y, 1e-4f);
            Assert.AreEqual(2, t.FallHeight, 0.01f);
        }

        [Test]
        public void StepUp_UpToHalfAMetre()
        {
            World.Blocks = new[] { Box(2, GameConfig.StepUp - 0.05f, 2) };
            var t = At(Vector3.zero);
            Run(t, North(), 0.35f);
            Assert.AreEqual(GameConfig.StepUp - 0.05f, t.Position.y, 1e-4f, "stepped up onto it");

            World.Blocks = new[] { Box(2, GameConfig.StepUp + 0.1f, 2) };
            t = At(Vector3.zero);
            Run(t, North(), 0.6f);
            Assert.AreEqual(0, t.Position.y, 1e-4f);
            Assert.AreEqual(2 - GameConfig.PlayerRadius, t.Position.z, 1e-3f, "stopped at its face");
        }

        [Test]
        public void DropDamage_NoneUpToFiveMetres_ThenScaled_NeverLethal()
        {
            Assert.AreEqual(0, Traversal.DropDamage(GameConfig.SafeDrop, 100));
            float six = Traversal.DropDamage(6, 100), nine = Traversal.DropDamage(9, 100);
            Assert.Greater(six, 0);
            Assert.Greater(nine, six);
            Assert.AreEqual(9, Traversal.DropDamage(40, 10), 1e-4f, "leaves him on 1 HP");
            Assert.AreEqual(0, Traversal.DropDamage(40, 1), 1e-4f);
        }

        [Test]
        public void Dash_OnlyWhileGrounded()
        {
            var t = At(Vector3.zero);
            Assert.IsTrue(t.CanDash);
            t.Tick(Still(true), Dt);
            Assert.IsFalse(t.CanDash, "not in the air");
        }

        [Test]
        public void Dash_AtTheLog_WithALongFrame_DoesNotPassThrough()
        {
            World.Blocks = new[] { Box(2, 1.0f, 1) };
            var t = At(Vector3.zero);
            var dash = new TraversalInput { Velocity = Vector3.forward * 700 * GameConfig.PX, Facing = Vector3.forward };
            for (int i = 0; i < 3; i++) t.Tick(dash, 0.1f);
            Assert.AreEqual(2 - GameConfig.PlayerRadius, t.Position.z, 1e-3f, "stopped at the log's face");
        }

        // ------------------------------------------------------------------ moves

        // Hanging from a 3.2 m ledge 1 m north.
        static Traversal Hanging(out Block b, float width = 4)
        {
            b = Box(1, 3.2f, 2, width);
            World.Blocks = new[] { b };
            var t = At(new Vector3(0, 0, 0.3f));
            t.Tick(Still(true), Dt);
            Run(t, Still(), 2, x => x.State == TraversalState.Hang);
            Assert.AreEqual(TraversalState.Hang, t.State, "jump-grabbed the ledge");
            return t;
        }

        [Test]
        public void Knock_IgnoredWhileAttached()
        {
            var t = Hanging(out _);
            Vector3 at = t.Position;
            Run(t, new TraversalInput { Facing = Vector3.forward, Knock = Vector3.right * 30 }, 0.5f);
            Assert.AreEqual(TraversalState.Hang, t.State);
            Assert.Less((t.Position - at).magnitude, 1e-3f);
        }

        [Test]
        public void Death_WhileHanging_DropsHimFirst()
        {
            var t = Hanging(out _);
            t.LetGo();
            Assert.AreEqual(TraversalState.Air, t.State);
            Run(t, Still(), 2, x => x.Landed);
            Assert.AreEqual(TraversalState.Grounded, t.State);
            Assert.AreEqual(0, t.Position.y, 1e-4f);
        }

        [Test]
        public void Vault_OnlyInItsHeightBand()
        {
            foreach (var (top, want) in new[] { (0.4f, TraversalProbe.Kind.None), (1.0f, TraversalProbe.Kind.Vault), (1.4f, TraversalProbe.Kind.Vault), (1.6f, TraversalProbe.Kind.Mantle) })
            {
                World.Blocks = new[] { Box(1, top, 0.8f) };
                Assert.AreEqual(want, TraversalProbe.Find(Vector3.zero, Vector3.forward).Kind, $"top {top}");
            }
            // Over a 1 m log: up, over and down beyond it.
            var log = Box(1, 1, 0.8f);
            World.Blocks = new[] { log };
            var t = At(new Vector3(0, 0, 0.3f));
            t.Tick(North(true), Dt);
            Assert.AreEqual(TraversalState.Vault, t.State);
            float peak = 0;
            Run(t, Still(), 2, x => { peak = Mathf.Max(peak, x.Position.y); return x.Grounded; });
            Assert.Greater(t.Position.z, 1.8f + GameConfig.PlayerRadius, "beyond the log");
            Assert.AreEqual(0, t.Position.y, 1e-4f);
            Assert.Greater(peak, 1, "over its top");
        }

        [Test]
        public void Vault_NeverIntoABlock()
        {
            // A tall stack right behind the log: no room to land, so he climbs onto the log.
            World.Blocks = new[] { Box(1, 1, 0.8f), Box(1.9f, 3, 2) };
            var hit = TraversalProbe.Find(new Vector3(0, 0, 0.3f), Vector3.forward);
            Assert.AreEqual(TraversalProbe.Kind.Mantle, hit.Kind);
            Assert.AreEqual(0, hit.Block);
        }

        [Test]
        public void Mantle_NeedsHeadroom()
        {
            World.Blocks = new[] { Box(1, 2, 2) };
            var t = At(new Vector3(0, 0, 0.3f));
            t.Tick(North(true), Dt);
            Assert.AreEqual(TraversalState.Mantle, t.State);
            Run(t, Still(), 3, x => x.Grounded);
            Assert.AreEqual(2, t.Position.y, 1e-4f, "on top");
            Assert.IsTrue(World.Blocks[0].Contains(t.Position));

            // A slab 1 m above the top: no room to stand.
            World.Blocks = new[] { Box(1, 2, 2), Box(1, 6, 2, 6, BlockFlags.None, 0, 3) };
            Assert.AreNotEqual(TraversalProbe.Kind.Mantle, TraversalProbe.Find(new Vector3(0, 0, 0.3f), Vector3.forward).Kind);
        }

        [Test]
        public void Grab_OnlyWhileFallingNearTheEdge()
        {
            var b = Box(1, 3.2f, 2);
            World.Blocks = new[] { b };
            var t = At(new Vector3(0, 0, 0.3f));
            t.Tick(Still(true), Dt);
            bool hungRising = false;
            Run(t, Still(), 2, x =>
            {
                if (x.State == TraversalState.Hang && x.StateTime == 0 && x.VerticalSpeed > 0) hungRising = true;
                return x.State == TraversalState.Hang;
            });
            Assert.IsFalse(hungRising);
            Assert.AreEqual(TraversalState.Hang, t.State);
            Assert.AreEqual(3.2f - GameConfig.HangDepth, t.Position.y, 1e-3f);

            // Jumping 3 m short of the wall: nothing to grab.
            t = At(new Vector3(0, 0, -2.5f));
            t.Tick(Still(true), Dt);
            Run(t, Still(), 2, x => x.Landed);
            Assert.AreEqual(TraversalState.Grounded, t.State);
        }

        [Test]
        public void Shimmy_StopsAtTheEdgeEnds()
        {
            var t = Hanging(out var b, 4);
            var east = new TraversalInput { Move = Vector3.right, Facing = Vector3.forward };
            Run(t, east, 5);
            Assert.AreEqual(TraversalState.Hang, t.State);
            Assert.AreEqual(2 - TraversalProbe.EdgeInset, t.Position.x, 0.02f, "short of the corner");
            Assert.AreEqual(2, t.Face, "still on the same edge");
            Run(t, new TraversalInput { Move = Vector3.left, Facing = Vector3.forward }, 8);
            Assert.AreEqual(-2 + TraversalProbe.EdgeInset, t.Position.x, 0.02f);
        }

        [Test]
        public void ClimbUp_FromAHang_LandsOnTop()
        {
            var t = Hanging(out var b);
            Run(t, Still(), 0.3f);
            t.Tick(Still(true), Dt);
            Assert.AreEqual(TraversalState.ClimbUp, t.State);
            Run(t, Still(), 2, x => x.Grounded);
            Assert.AreEqual(3.2f, t.Position.y, 1e-4f);
            Assert.IsTrue(b.Contains(t.Position));
        }

        [Test]
        public void Drop_FromAHang_Falls()
        {
            var t = Hanging(out _);
            var drop = Still();
            drop.Drop = true;
            t.Tick(drop, Dt);
            Assert.AreEqual(TraversalState.Air, t.State);
            bool regrabbed = false;
            Run(t, Still(), 2, x => { regrabbed |= x.State == TraversalState.Hang; return x.Landed; });
            Assert.IsFalse(regrabbed);
            Assert.AreEqual(0, t.Position.y, 1e-4f);
        }

        [Test]
        public void WallClimb_EndsInAClimbUp_OnTop()
        {
            // A 4.5 m ruin wall with vines on its south face.
            var wall = Box(1, 4.5f, 1.2f, 6, BlockFlags.Ledge, 1 << 2);
            World.Blocks = new[] { wall };
            Assert.AreEqual("CLIMB", TraversalProbe.Find(new Vector3(0, 0, 0.3f), Vector3.forward).Label);
            var t = At(new Vector3(0, 0, 0.3f));
            t.Tick(North(true), Dt);
            Assert.AreEqual(TraversalState.WallClimb, t.State);
            bool climbedUp = false;
            Run(t, North(), 8, x => { climbedUp |= x.State == TraversalState.ClimbUp; return x.Grounded; });
            Assert.IsTrue(climbedUp);
            Assert.AreEqual(4.5f, t.Position.y, 1e-4f);
            Assert.IsTrue(wall.Contains(t.Position));
        }

        [Test]
        public void Lower_FromATopsEdge_IntoAHang()
        {
            var b = Box(1, 3.2f, 2);
            World.Blocks = new[] { b };
            // On the top, 0.4 m in from its south edge, facing out over it.
            var t = At(new Vector3(0, 3.2f, 1.4f));
            var lower = new TraversalInput { Facing = Vector3.back, Drop = true };
            t.Tick(lower, Dt);
            Assert.AreEqual(TraversalState.Lower, t.State);
            Run(t, Still(), 2, x => x.State == TraversalState.Hang);
            Assert.AreEqual(TraversalState.Hang, t.State);
            Assert.AreEqual(2, t.Face, "the south edge");
            Assert.AreEqual(3.2f - GameConfig.HangDepth, t.Position.y, 1e-3f);
            Assert.Less(t.Position.z, 1, "off the face, outside the block");
            Assert.Less(Vector3.Angle(t.Facing, Vector3.forward), 1, "facing the wall");
            // Facing back over the top, or in the middle of it: nothing to lower over.
            t = At(new Vector3(0, 3.2f, 1.4f));
            t.Tick(new TraversalInput { Facing = Vector3.forward, Drop = true }, Dt);
            Assert.AreEqual(TraversalState.Grounded, t.State);
        }

        [Test]
        public void Probe_Near_FindsAnEdgeWithinRange()
        {
            World.Blocks = new[] { Box(2.5f, 2.2f, 1) };
            Assert.IsTrue(TraversalProbe.Near(Vector3.zero, 3));
            Assert.IsFalse(TraversalProbe.Near(new Vector3(0, 0, -1), 3));
            Assert.IsFalse(TraversalProbe.Near(new Vector3(0, 2.2f, 3), 3), "standing on it");
        }

        [Test]
        public void Probe_PicksTheNearestValidEdge_WithTheRightLabel()
        {
            // Two logs side by side, the right one a little nearer.
            var left = Box(0.9f, 1, 0.6f, 1.6f);
            left.Center.x = -1;
            var right = Box(0.7f, 1, 0.6f, 1.6f);
            right.Center.x = 1;
            World.Blocks = new[] { left, right };
            var hit = TraversalProbe.Find(Vector3.zero, Vector3.forward);
            Assert.AreEqual(1, hit.Block, "the nearer log");
            Assert.AreEqual("VAULT", hit.Label);
            // A log right behind the first one: no room to land, so it is a climb.
            World.Blocks = new[] { Box(1.6f, 1, 0.6f), Box(0.8f, 1, 0.6f) };
            Assert.AreEqual("CLIMB", TraversalProbe.Find(Vector3.zero, Vector3.forward).Label);
            // Out of reach or behind him: nothing (Space jumps).
            Assert.AreEqual("JUMP", TraversalProbe.Find(new Vector3(0, 0, -2), Vector3.forward).Label);
            Assert.AreEqual("JUMP", TraversalProbe.Find(Vector3.zero, Vector3.back).Label);
            // Mantle height and a high ledge read CLIMB.
            World.Blocks = new[] { Box(0.8f, 2.2f, 1) };
            Assert.AreEqual("CLIMB", TraversalProbe.Find(Vector3.zero, Vector3.forward).Label);
            World.Blocks = new[] { Box(0.8f, 3.4f, 1) };
            Assert.AreEqual(TraversalProbe.Kind.JumpGrab, TraversalProbe.Find(Vector3.zero, Vector3.forward).Kind);
            // Far too high: just a jump.
            World.Blocks = new[] { Box(0.8f, 5, 1) };
            Assert.AreEqual("JUMP", TraversalProbe.Find(Vector3.zero, Vector3.forward).Label);
        }
    }
}
