using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WardenZero
{
    // A gun-ride target (M3): fuel drums that explode and set off their neighbours, a fuel
    // trailer for a big chain, crates that splinter, and steel pop-up plates that rise from
    // behind cover when the jeep comes near and fall with one round. Its box (Shape) is what
    // rounds and bolts hit. Score only (no XP: no upgrade picker mid-ride). The registry (All)
    // is filled by RoadStage for the ride and emptied with the world.
    public class Destructible : MonoBehaviour
    {
        public static readonly List<Destructible> All = new List<Destructible>();

        // Emptied with the scene it belongs to (and on a fresh play session).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            All.Clear();
            SceneManager.sceneUnloaded -= Forget;
            SceneManager.sceneUnloaded += Forget;
        }

        static void Forget(Scene scene) => All.Clear();

        public RoadLayout.TargetKind kind;
        public Vector3 size = Vector3.one; // the box, metres (x across, y up, z deep)
        public float popAt = -1; // plates: rise when the ride reaches this
        public bool roadblock; // its fall counts toward the ride's end

        public float Health { get; private set; }
        public bool Up { get; private set; } // standing (plates lie flat until popped)
        public bool Down { get; private set; } // destroyed
        public bool Alive => Up && !Down;

        public const float ChainDelay = 0.12f; // a drum sets off the next a moment later

        public static float MaxHealth(RoadLayout.TargetKind k) => k == RoadLayout.TargetKind.Plate ? 1 : k == RoadLayout.TargetKind.Drum ? 60 : k == RoadLayout.TargetKind.Crate ? 90 : 300;
        public static float BlastRadius(RoadLayout.TargetKind k) => k == RoadLayout.TargetKind.Drum ? 6 : k == RoadLayout.TargetKind.Trailer ? 11 : 0;
        // The box at the game scale of the Tripo art (README): a drum 0.92 x 1.24, a plate 0.87 x
        // 1.68 on its hinge, the trailer 3.6 long (along the road, its own x).
        public static Vector3 Size(RoadLayout.TargetKind k) =>
            k == RoadLayout.TargetKind.Drum ? new Vector3(0.92f, 1.24f, 0.92f)
            : k == RoadLayout.TargetKind.Plate ? new Vector3(0.87f, 1.68f, 0.3f)
            : k == RoadLayout.TargetKind.Crate ? new Vector3(1.4f, 1.4f, 1.4f)
            : new Vector3(3.62f, 2.1f, 2.34f);

        public static int Points(RoadLayout.TargetKind k) => k == RoadLayout.TargetKind.Plate ? 150 : k == RoadLayout.TargetKind.Drum ? 100 : k == RoadLayout.TargetKind.Crate ? 50 : 500;

        Vector3 home;
        Quaternion homeRot;
        float rise; // 0 flat .. 1 up (plates)
        float fall;

        void Awake()
        {
            home = transform.localPosition;
            homeRot = transform.localRotation;
        }

        // Back as built: standing, or a plate lying flat behind its cover.
        public void ResetState()
        {
            Health = MaxHealth(kind);
            Down = false;
            Up = popAt < 0;
            rise = Up ? 1 : 0;
            fall = 0;
            gameObject.SetActive(true);
            transform.localPosition = home;
            Pose();
        }

        public void Pop()
        {
            if (Up || Down) return;
            Up = true;
        }

        // The box rounds hit, from the transform (centre at the base).
        public Block Shape => new Block
        {
            Center = new Vector2(transform.position.x, transform.position.z),
            Half = new Vector2(size.x / 2, size.z / 2),
            Yaw = transform.eulerAngles.y,
            Bottom = transform.position.y,
            Top = transform.position.y + size.y,
        };

        public Vector3 Centre => transform.position + Vector3.up * size.y / 2;

        public void Hit(float damage)
        {
            if (!Alive) return;
            Health -= damage;
            if (Health <= 0) Destroyed();
        }

        void Destroyed()
        {
            Down = true;
            var gm = GameManager.Instance;
            if (gm != null) gm.AddScore(Points(kind));
            float r = BlastRadius(kind);
            if (r > 0)
            {
                Effects.Instance.BombBlast(Centre, r * 0.7f);
                if (gm != null)
                {
                    gm.PlaySound(gm.bombSound, kind == RoadLayout.TargetKind.Trailer ? 0.9f : 0.6f, GameManager.Detune(300));
                    gm.cameraFollow.AddShake(kind == RoadLayout.TargetKind.Trailer ? 0.6f : 0.3f);
                }
                StartCoroutine(Chain(r));
                return;
            }
            Effects.Instance.Burst(Centre, kind == RoadLayout.TargetKind.Plate ? new Color(0.9f, 0.9f, 0.8f) : new Color(0.55f, 0.45f, 0.3f), 14, new Vector2(3, 7), new Vector2(0.08f, 0.2f));
            if (gm != null) gm.PlaySound(gm.enemyHitSound, 0.6f, GameManager.Detune(300));
            if (kind != RoadLayout.TargetKind.Plate) gameObject.SetActive(false);
        }

        // Everything in the blast takes it, a moment later (a chain you can watch).
        System.Collections.IEnumerator Chain(float r)
        {
            gameObject.SetActive(true);
            foreach (var mr in GetComponentsInChildren<Renderer>()) mr.enabled = false;
            yield return new WaitForSeconds(ChainDelay);
            Vector3 c = Centre;
            foreach (var d in All.ToArray())
                if (d != this && d.Alive && (d.Centre - c).magnitude <= r) d.Hit(MaxHealth(d.kind) * 2);
            gameObject.SetActive(false);
            foreach (var mr in GetComponentsInChildren<Renderer>(true)) mr.enabled = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (kind != RoadLayout.TargetKind.Plate) return;
            rise = Mathf.MoveTowards(rise, Up ? 1 : 0, dt / 0.35f);
            if (Down) fall = Mathf.MoveTowards(fall, 1, dt / 0.3f);
            Pose();
        }

        // A plate hinges at its foot: flat (down, behind cover) to upright; hit, it falls back.
        void Pose()
        {
            if (kind != RoadLayout.TargetKind.Plate) return;
            float k = Mathf.SmoothStep(0, 1, rise) * (1 - Mathf.SmoothStep(0, 1, fall));
            transform.localRotation = homeRot * Quaternion.Euler(-90 * (1 - k), 0, 0);
        }

        // The first live target along a ray within maxDistance (sphere of `radius`).
        public static Destructible Cast(Vector3 origin, Vector3 dir, float maxDistance, float radius, out float distance)
        {
            Destructible best = null;
            distance = maxDistance;
            foreach (var d in All)
            {
                if (!d.Alive) continue;
                float t = WorldCast.CastBlock(origin, dir, d.Shape, radius);
                if (t >= 0 && t < distance)
                {
                    distance = t;
                    best = d;
                }
            }
            return best;
        }

        // A live target's box contains p.
        public static Destructible At(Vector3 p)
        {
            foreach (var d in All)
            {
                if (!d.Alive) continue;
                var b = d.Shape;
                if (p.y > b.Bottom && p.y < b.Top && b.Contains(p)) return d;
            }
            return null;
        }
    }
}
