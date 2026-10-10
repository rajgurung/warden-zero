using System.Collections.Generic;
using UnityEngine;

namespace WardenZero
{
    // A straight-flying energy bolt. Hits the first enemy it touches (or passes through
    // with piercing), dies on walls.
    // Pooled per prefab: spent bolts are deactivated and reused instead of destroyed.
    public class Bolt : MonoBehaviour
    {
        static readonly Dictionary<Bolt, Stack<Bolt>> Pools = new Dictionary<Bolt, Stack<Bolt>>();

        Bolt source; // prefab this came from (null for bolts made with Instantiate)
        Vector3 baseScale;
        TrailRenderer trail;
        Vector3 velocity;
        float life;
        float damage;
        bool piercing;
        float radius;
        readonly HashSet<Enemy> hits = new HashSet<Enemy>();

        // A ready bolt from the pool (or a new one) at position.
        public static Bolt Spawn(Bolt prefab, Vector3 position)
        {
            if (Pools.TryGetValue(prefab, out var stack))
            {
                while (stack.Count > 0)
                {
                    var b = stack.Pop();
                    if (b == null) continue; // destroyed with a previous scene
                    b.transform.SetPositionAndRotation(position, Quaternion.identity);
                    b.gameObject.SetActive(true);
                    return b;
                }
            }
            var fresh = Instantiate(prefab, position, Quaternion.identity);
            fresh.source = prefab;
            return fresh;
        }

        // Back to the pool (or destroyed when it didn't come from one).
        public void Release()
        {
            if (source == null)
            {
                Destroy(gameObject);
                return;
            }
            gameObject.SetActive(false);
            if (!Pools.TryGetValue(source, out var stack)) Pools[source] = stack = new Stack<Bolt>();
            stack.Push(this);
        }

        void Awake()
        {
            baseScale = transform.localScale;
            trail = GetComponent<TrailRenderer>();
        }

        public void Launch(Vector3 direction, float speed, float damage, bool piercing, float size)
        {
            velocity = direction * speed;
            life = GameConfig.BoltLife;
            this.damage = damage;
            this.piercing = piercing;
            radius = GameConfig.BoltRadius * size;
            transform.rotation = Quaternion.LookRotation(direction);
            transform.localScale = baseScale * size;
            hits.Clear();
            if (trail != null) trail.Clear();
        }

        void Update()
        {
            // No kills (or score) once the run has ended; frozen while paused or picking.
            var gm = GameManager.Instance;
            if (!gm.RunActive)
            {
                Release();
                return;
            }
            if (!gm.IsPlaying) return;
            life -= Time.deltaTime;
            Vector3 p = transform.position + velocity * Time.deltaTime;
            transform.position = p;
            if (life <= 0 || GameConfig.PointInWall(p))
            {
                if (life > 0) Effects.Instance.BoltImpact(p);
                Release();
                return;
            }
            // Backwards by index: a kill removes only that enemy, and nothing is allocated per frame.
            var all = Enemy.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (i >= all.Count) continue;
                var e = all[i];
                if (hits.Contains(e)) continue;
                float rr = e.Radius + radius;
                Vector3 d = e.transform.position - p;
                if (d.x * d.x + d.z * d.z > rr * rr) continue;
                hits.Add(e);
                Effects.Instance.BoltImpact(p);
                e.TakeHit(damage);
                if (piercing) continue;
                Release();
                return;
            }
        }
    }
}
