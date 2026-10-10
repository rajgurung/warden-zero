using System.Collections.Generic;
using UnityEngine;

namespace WardenZero
{
    // A straight-flying energy bolt. Hits the first enemy it touches (or passes through
    // with piercing), dies on walls.
    public class Bolt : MonoBehaviour
    {
        Vector3 velocity;
        float life;
        float damage;
        bool piercing;
        float radius;
        readonly HashSet<Enemy> hits = new HashSet<Enemy>();

        public void Launch(Vector3 direction, float speed, float damage, bool piercing, float size)
        {
            velocity = direction * speed;
            life = GameConfig.BoltLife;
            this.damage = damage;
            this.piercing = piercing;
            radius = GameConfig.BoltRadius * size;
            transform.rotation = Quaternion.LookRotation(direction);
            transform.localScale *= size;
        }

        void Update()
        {
            // No kills (or score) once the run has ended; frozen while paused or picking.
            var gm = GameManager.Instance;
            if (!gm.RunActive)
            {
                Destroy(gameObject);
                return;
            }
            if (!gm.IsPlaying) return;
            life -= Time.deltaTime;
            Vector3 p = transform.position + velocity * Time.deltaTime;
            transform.position = p;
            if (life <= 0 || GameConfig.PointInWall(p))
            {
                if (life > 0) Effects.Instance.BoltImpact(p);
                Destroy(gameObject);
                return;
            }
            foreach (var e in Enemy.All.ToArray())
            {
                if (hits.Contains(e)) continue;
                float rr = e.Radius + radius;
                Vector3 d = e.transform.position - p;
                if (d.x * d.x + d.z * d.z > rr * rr) continue;
                hits.Add(e);
                Effects.Instance.BoltImpact(p);
                e.TakeHit(damage);
                if (piercing) continue;
                Destroy(gameObject);
                return;
            }
        }
    }
}
