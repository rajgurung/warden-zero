using UnityEngine;

namespace WardenZero
{
    public enum TraversalState { Grounded, Air, Vault, Mantle, Hang, ClimbUp, WallClimb, Lower }

    // One frame's wishes for the Warden's body.
    public struct TraversalInput
    {
        public Vector3 Move; // flat, 0..1: the stick or WASD in world space
        public Vector3 Velocity; // on the ground: run or dash speed (m/s)
        public Vector3 Facing; // flat: where he faces (the aim); the probe uses it when still
        public bool Jump; // Space / JUMP pressed this frame
        public bool JumpHeld;
        public bool Drop; // C / DROP pressed this frame
        public Vector3 Knock; // a shove (m/s); ignored while attached to a block
    }

    // The Warden's body in the jungle, without a physics engine: ground, jump and fall, and
    // the platforming moves on World.Blocks. Plain C#, ticked by PlayerController, so every
    // rule runs in EditMode tests.
    // - Grounded: runs at the input velocity, steps up to StepUp, falls off higher edges.
    //   Space (buffered JumpBuffer, or within CoyoteTime of leaving an edge) does the probe's
    //   move: vault, mantle, wall climb, a jump toward a high ledge, or a plain jump.
    // - Air: gravity, TraversalSpeed in the air with AirControl steering (no stick brakes); falling near a
    //   ledge his hands reach, he grabs it (Hang). Landing reports the fall height.
    // - Vault, Mantle, ClimbUp: timed curves from a start pose to an end pose.
    // - Lower: C at the edge of a top lowers him over it into a hang.
    // - Hang: hands on an edge; the move along it shimmies, stopping short of its ends;
    //   Space climbs up (if there is headroom), C lets go.
    // - WallClimb: on a vine face; toward the wall (or Space held) climbs, away descends;
    //   reaching the top ends in a climb up. C lets go.
    // Movement is sub-stepped to MaxStep, so a dash never tunnels through a block.
    public class Traversal
    {
        public const float VaultTime = 0.6f;
        public const float ClimbUpTime = 0.9f;
        public const float RegrabDelay = 0.5f; // after letting go
        public const float LowerTime = 0.6f;
        public const float AirAccel = 30; // the ground's, m/s²; AirControl of it in the air

        public TraversalState State { get; private set; }
        public Vector3 Position { get; private set; } // his feet
        public float VerticalSpeed { get; private set; }
        public Vector3 AirVelocity { get; private set; } // flat
        public Vector3 Facing { get; private set; } // while a move owns it
        public float StateTime { get; private set; }
        public float Progress { get; private set; } // 0..1 through a timed move

        // The block and face he is on (Hang, WallClimb, ClimbUp, Mantle, Vault).
        public int Block { get; private set; } = -1;
        public int Face { get; private set; }
        public Vector3 Normal { get; private set; }
        public Vector3 EdgeA { get; private set; }
        public Vector3 EdgeB { get; private set; }
        public float Along { get; private set; } // hands' distance along EdgeA→EdgeB
        public float Top { get; private set; } // the edge or obstacle top

        // Events from the last Tick.
        public bool Jumped { get; private set; }
        public bool Landed { get; private set; }
        public bool Grabbed { get; private set; }
        public float FallHeight { get; private set; } // on Landed: peak to landing
        // This time in the air began with a jump (not a step off an edge), and with a jump at a
        // high ledge (the hands reach up for it).
        public bool FromJump { get; private set; }
        public bool GrabJump { get; private set; }
        public float ClimbRate { get; private set; } // WallClimb: -1 down .. 1 up

        public bool Attached => State != TraversalState.Grounded && State != TraversalState.Air;
        public bool OwnsFacing => Attached;
        public bool Grounded => State == TraversalState.Grounded;
        public bool CanDash => State == TraversalState.Grounded;
        public bool CanFire => State == TraversalState.Grounded || State == TraversalState.Air;
        public Vector3 Tangent => Flat(EdgeB - EdgeA).normalized;

        public static float JumpSpeed => Mathf.Sqrt(2 * GameConfig.Gravity * GameConfig.JumpHeight);

        float clock;
        float groundedAt = -1, jumpAt = -10, noGrabUntil;
        float peakY;
        Vector3 from, to; // a timed move's start and end feet
        float duration;
        bool coyote; // off an edge without jumping: a late jump still counts

        // On the ground at `feet` (a new run, a set piece's end, a test moving him).
        public void Reset(Vector3 feet)
        {
            State = TraversalState.Grounded;
            Position = feet;
            VerticalSpeed = 0;
            AirVelocity = Vector3.zero;
            StateTime = Progress = 0;
            Block = -1;
            groundedAt = clock;
            jumpAt = -10;
            coyote = false;
            Jumped = Landed = Grabbed = false;
        }

        // Let go of whatever he holds (C, or death): he falls from here.
        public void LetGo()
        {
            if (!Attached) return;
            Vector3 p = Position;
            if (State == TraversalState.Hang || State == TraversalState.WallClimb) p += Normal * 0.05f;
            Fall(p, Vector3.zero);
            noGrabUntil = clock + RegrabDelay;
        }

        // What Space / JUMP would do now (null mid-move or in the air): the probe's JUMP, VAULT
        // or CLIMB on the ground, UP while hanging or climbing.
        public string Label(Vector3 move, Vector3 facing)
        {
            switch (State)
            {
                case TraversalState.Grounded:
                    return TraversalProbe.Find(Position, Flat(move).sqrMagnitude > 0.01f ? move : facing).Label;
                case TraversalState.Hang:
                case TraversalState.WallClimb:
                    return "UP";
                default:
                    return null;
            }
        }

        // Whether C / DROP does something now: let go, or lower over the edge of a top.
        public bool CanDrop(Vector3 move, Vector3 facing)
        {
            if (State == TraversalState.Hang || State == TraversalState.WallClimb) return true;
            return State == TraversalState.Grounded && TraversalProbe.FindLower(Position, Flat(move).sqrMagnitude > 0.01f ? move : facing).Found;
        }

        // Damage for a fall: none up to SafeDrop, then DropDamagePerMetre, never lethal.
        public static float DropDamage(float height, float health)
        {
            if (height <= GameConfig.SafeDrop) return 0;
            float d = 5 + (height - GameConfig.SafeDrop) * GameConfig.DropDamagePerMetre;
            return Mathf.Max(0, Mathf.Min(d, health - 1));
        }

        public TraversalState Tick(TraversalInput input, float dt)
        {
            clock += dt;
            StateTime += dt;
            Jumped = Landed = Grabbed = false;
            if (input.Jump) jumpAt = clock;
            if (Attached) input.Knock = Vector3.zero;
            switch (State)
            {
                case TraversalState.Grounded: TickGround(input, dt); break;
                case TraversalState.Air: TickAir(input, dt); break;
                case TraversalState.Hang: TickHang(input, dt); break;
                case TraversalState.WallClimb: TickWallClimb(input, dt); break;
                default: TickMove(dt); break;
            }
            return State;
        }

        // ------------------------------------------------------------------ ground and air

        void TickGround(TraversalInput input, float dt)
        {
            if (clock - jumpAt <= GameConfig.JumpBuffer && ContextMove(input)) return;
            if (input.Drop && Lower(input)) return;
            Vector3 v = Flat(input.Velocity + input.Knock);
            Vector3 p = Position;
            int steps = Steps(v.magnitude * dt);
            for (int i = 0; i < steps; i++)
            {
                Vector3 next = Collide(p + v * (dt / steps), p.y);
                float s = World.SurfaceAt(next, p.y);
                if (s < p.y - GameConfig.StepUp)
                {
                    // Off an edge: a fall, but a jump still counts for a moment.
                    Position = next;
                    Fall(next, ClampFlat(v, GameConfig.TraversalSpeed));
                    coyote = true;
                    return;
                }
                next.y = s;
                p = next;
            }
            Position = p;
            groundedAt = clock;
        }

        void TickAir(TraversalInput input, float dt)
        {
            if (coyote && clock - groundedAt <= GameConfig.CoyoteTime && clock - jumpAt <= GameConfig.JumpBuffer)
            {
                Jump(input.Move);
                return;
            }
            // Air control steers toward the stick, and letting go of it brakes.
            AirVelocity = Vector3.MoveTowards(AirVelocity, Vector3.ClampMagnitude(Flat(input.Move), 1) * GameConfig.TraversalSpeed, GameConfig.AirControl * AirAccel * dt);
            Vector3 v = AirVelocity + Flat(input.Knock);
            Vector3 p = Position;
            float vy0 = VerticalSpeed;
            VerticalSpeed -= GameConfig.Gravity * dt;
            float dy = (vy0 + VerticalSpeed) / 2 * dt;
            int steps = Steps(Mathf.Max(v.magnitude * dt, Mathf.Abs(dy)));
            for (int i = 0; i < steps; i++)
            {
                Vector3 next = p + v * (dt / steps);
                next.y = p.y + dy / steps;
                next = Collide(next, next.y);
                peakY = Mathf.Max(peakY, next.y);
                float s = World.SurfaceAt(next, Mathf.Max(p.y, next.y));
                if (next.y <= s)
                {
                    next.y = s;
                    Position = next;
                    Land();
                    return;
                }
                p = next;
            }
            Position = p;
            if (VerticalSpeed <= 0 && clock >= noGrabUntil) TryGrab(input);
        }

        void Land()
        {
            FallHeight = Mathf.Max(0, peakY - Position.y);
            Landed = true;
            Enter(TraversalState.Grounded);
            VerticalSpeed = 0;
            AirVelocity = Vector3.zero;
            groundedAt = clock;
            coyote = false;
        }

        void Fall(Vector3 p, Vector3 velocity)
        {
            Position = p;
            Enter(TraversalState.Air);
            VerticalSpeed = 0;
            AirVelocity = velocity;
            peakY = p.y;
            Block = -1;
            coyote = false;
            FromJump = GrabJump = false;
        }

        void Jump(Vector3 move)
        {
            jumpAt = -10;
            coyote = false;
            Enter(TraversalState.Air);
            VerticalSpeed = JumpSpeed;
            AirVelocity = Vector3.ClampMagnitude(Flat(move), 1) * GameConfig.TraversalSpeed;
            peakY = Position.y;
            Jumped = true;
            FromJump = true;
            GrabJump = false;
        }

        // Space on the ground: the probe's move toward the stick (or his facing when still).
        bool ContextMove(TraversalInput input)
        {
            jumpAt = -10;
            Vector3 dir = Flat(input.Move).sqrMagnitude > 0.01f ? input.Move : input.Facing;
            var hit = TraversalProbe.Find(Position, dir);
            switch (hit.Kind)
            {
                case TraversalProbe.Kind.Vault:
                    Attach(hit);
                    StartMove(TraversalState.Vault, hit.Landing, VaultTime);
                    break;
                case TraversalProbe.Kind.Mantle:
                    Attach(hit);
                    StartMove(TraversalState.Mantle, hit.Landing, 0.45f + 0.25f * (hit.Top - Position.y));
                    break;
                case TraversalProbe.Kind.Climb:
                    Attach(hit);
                    Enter(TraversalState.WallClimb);
                    Position = OnFace(Position.y + 0.2f);
                    Grabbed = true;
                    break;
                case TraversalProbe.Kind.JumpGrab:
                    // Up and in toward the wall; the grab happens on the way down.
                    Jump(Vector3.zero);
                    GrabJump = true;
                    AirVelocity = -hit.Normal * Mathf.Clamp((hit.Distance - GameConfig.PlayerRadius) / 0.3f, 0, 2.5f);
                    break;
                default:
                    Jump(input.Move);
                    break;
            }
            return true;
        }

        // C at the edge of the top he stands on: over the edge into a hang.
        bool Lower(TraversalInput input)
        {
            Vector3 dir = Flat(input.Move).sqrMagnitude > 0.01f ? input.Move : input.Facing;
            var hit = TraversalProbe.FindLower(Position, dir);
            if (!hit.Found) return false;
            Attach(hit);
            Vector3 end = Hanging();
            StartMove(TraversalState.Lower, end, LowerTime);
            return true;
        }

        // Falling past a ledge his hands reach, facing it or drifting toward it: hang on.
        void TryGrab(TraversalInput input)
        {
            var blocks = World.Blocks;
            Vector3 feet = Position;
            Vector3 want = Flat(input.Move).sqrMagnitude > 0.01f ? Flat(input.Move) : Flat(input.Facing);
            for (int i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i];
                float reach = b.Top - feet.y;
                if (reach < GameConfig.HangDepth - 0.45f || reach > GameConfig.HangDepth + 0.15f) continue;
                for (int f = 0; f < 4; f++)
                {
                    if (!b.HasLedge(f) && !b.HasClimb(f)) continue;
                    Vector3 n = b.Normal(f);
                    Vector3 p = TraversalProbe.NearestOnEdge(b, f, feet);
                    Vector3 off = Flat(feet - p);
                    float d = Vector3.Dot(off, n);
                    float side = Mathf.Abs(Vector3.Dot(off, Vector3.Cross(Vector3.up, n)));
                    if (d < 0 || d > GameConfig.PlayerRadius + 0.3f || side > 0.3f) continue;
                    if (Vector3.Dot(want, -n) < 0.2f && Vector3.Dot(AirVelocity, -n) <= 0.1f) continue;
                    if (!TraversalProbe.HangClear(b, p, n)) continue; // no room under it: he lands instead
                    Attach(new TraversalProbe.Hit { Block = i, Face = f, Point = p, Normal = n, Top = b.Top });
                    Enter(TraversalState.Hang);
                    Position = Hanging();
                    VerticalSpeed = 0;
                    Grabbed = true;
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ on a block

        void Attach(TraversalProbe.Hit hit)
        {
            var b = World.Blocks[hit.Block];
            Block = hit.Block;
            Face = hit.Face;
            Normal = hit.Normal;
            Top = b.Top;
            b.Edge(hit.Face, out var a, out var e);
            EdgeA = a;
            EdgeB = e;
            Along = TraversalProbe.Along(a, e, hit.Point);
            Facing = -hit.Normal;
        }

        void TickHang(TraversalInput input, float dt)
        {
            if (input.Drop)
            {
                LetGo();
                return;
            }
            if (clock - jumpAt <= GameConfig.JumpBuffer && StateTime > 0.15f)
            {
                jumpAt = -10;
                ClimbUp();
                if (State != TraversalState.Hang) return;
            }
            Along = TraversalProbe.ClampAlong(Along + Vector3.Dot(Flat(input.Move), Tangent) * GameConfig.ShimmySpeed * dt, EdgeLength);
            Position = Hanging();
        }

        void TickWallClimb(TraversalInput input, float dt)
        {
            if (input.Drop)
            {
                LetGo();
                return;
            }
            Vector3 move = Flat(input.Move);
            float up = Mathf.Clamp(Vector3.Dot(move, -Normal) + (input.JumpHeld ? 1 : 0), -1, 1);
            if (Mathf.Abs(up) < 0.3f) up = 0;
            ClimbRate = up;
            float y = Position.y + up * GameConfig.ClimbSpeed * dt;
            Along = TraversalProbe.ClampAlong(Along + Vector3.Dot(move, Tangent) * GameConfig.ClimbSpeed * 0.5f * dt, EdgeLength);
            if (y + GameConfig.HangDepth >= Top)
            {
                // Hands at the top: over it.
                Position = OnFace(Top - GameConfig.HangDepth);
                ClimbUp();
                if (State == TraversalState.WallClimb) Enter(TraversalState.Hang);
                return;
            }
            Vector3 p = OnFace(y);
            float ground = World.SurfaceAt(p, y);
            if (y <= ground)
            {
                p = OnFace(ground) + Normal * (GameConfig.PlayerRadius - GameConfig.WallGap + 0.01f);
                Position = p;
                Enter(TraversalState.Grounded);
                groundedAt = clock;
                Block = -1;
                return;
            }
            Position = p;
        }

        // From a hang or the top of a climb: up onto the top, if he can stand there.
        void ClimbUp()
        {
            var b = World.Blocks[Block];
            Vector3 top = TraversalProbe.OnTop(b, TraversalProbe.OnEdge(EdgeA, EdgeB, Along), Normal, b.Depth(Face));
            if (!TraversalProbe.Headroom(top, Block)) return;
            StartMove(TraversalState.ClimbUp, top, ClimbUpTime);
        }

        void StartMove(TraversalState state, Vector3 end, float time)
        {
            Enter(state);
            from = Position;
            to = end;
            duration = time;
            Progress = 0;
            VerticalSpeed = 0;
        }

        // A timed move: vault (up, over, down), mantle and climb up (up the face, then in), or
        // lower (out to the edge, then down the face).
        void TickMove(float dt)
        {
            Progress = Mathf.Min(1, Progress + dt / duration);
            float u = Progress;
            Vector3 p;
            if (State == TraversalState.Lower)
            {
                const float edge = 0.35f;
                if (u < edge)
                {
                    p = Vector3.Lerp(Flat(from), Flat(to), Ease(u / edge));
                    p.y = from.y;
                }
                else
                {
                    p = Flat(to);
                    p.y = Mathf.Lerp(from.y, to.y, Ease((u - edge) / (1 - edge)));
                }
                Position = p;
                if (u < 1) return;
                Position = to;
                Enter(TraversalState.Hang);
                Grabbed = true;
                return;
            }
            if (State == TraversalState.Vault)
            {
                float over = Top + 0.15f;
                float y = u < 0.35f ? Mathf.Lerp(from.y, over, Ease(u / 0.35f))
                    : u < 0.65f ? over
                    : Mathf.Lerp(over, to.y, Ease((u - 0.65f) / 0.35f));
                p = Vector3.Lerp(from, to, u);
                p.y = y;
            }
            else
            {
                // At the face while rising, then in over the top.
                Vector3 wall = TraversalProbe.OnEdge(EdgeA, EdgeB, Along) + Normal * GameConfig.WallGap;
                const float rise = 0.65f;
                if (u < rise)
                {
                    float k = Ease(u / rise);
                    p = Vector3.Lerp(Flat(from), Flat(wall), Mathf.Min(1, k * 2));
                    p.y = Mathf.Lerp(from.y, Top, k);
                }
                else
                {
                    p = Vector3.Lerp(Flat(wall), Flat(to), Ease((u - rise) / (1 - rise)));
                    p.y = Top;
                }
            }
            Position = p;
            if (u < 1) return;
            Position = to;
            Enter(TraversalState.Grounded);
            groundedAt = clock;
            Block = -1;
        }

        // ------------------------------------------------------------------ helpers

        float EdgeLength => Flat(EdgeB - EdgeA).magnitude;

        // Feet while hanging from the edge at Along.
        Vector3 Hanging()
        {
            Vector3 p = TraversalProbe.OnEdge(EdgeA, EdgeB, Along) + Normal * GameConfig.WallGap;
            p.y = Top - GameConfig.HangDepth;
            return p;
        }

        // Feet on the face at height y, under the hands at Along.
        Vector3 OnFace(float y)
        {
            Vector3 p = TraversalProbe.OnEdge(EdgeA, EdgeB, Along) + Normal * GameConfig.WallGap;
            p.y = y;
            return p;
        }

        void Enter(TraversalState s)
        {
            State = s;
            StateTime = 0;
        }

        // Walls, bounds, trunks and blocks, for feet at feetY.
        static Vector3 Collide(Vector3 p, float feetY)
        {
            float y = p.y;
            p = GameConfig.ResolveCircle(p, GameConfig.PlayerRadius);
            p = World.PushOutOfTrunks(p, GameConfig.PlayerRadius);
            p = World.PushOutOfBlocks(p, GameConfig.PlayerRadius, feetY);
            p.y = y;
            return p;
        }

        static int Steps(float distance) => Mathf.Clamp(Mathf.CeilToInt(distance / GameConfig.MaxStep), 1, 64);

        static float Ease(float t) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        static Vector3 ClampFlat(Vector3 v, float max) => Vector3.ClampMagnitude(Flat(v), max);
    }
}
