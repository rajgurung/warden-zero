using UnityEngine;

namespace WardenZero
{
    // A straight-flying energy bolt. Hits the first enemy it touches, dies on walls.
    public class Bolt : MonoBehaviour
    {
        Vector3 velocity;
        float life;

        public void Launch(Vector3 direction)
        {
            velocity = direction * GameConfig.BoltSpeed;
            life = GameConfig.BoltLife;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        void Update()
        {
            life -= Time.deltaTime;
            Vector3 p = transform.position + velocity * Time.deltaTime;
            transform.position = p;
            if (life <= 0 || GameConfig.PointInWall(p))
            {
                Destroy(gameObject);
                return;
            }
            foreach (var e in Enemy.All)
            {
                float rr = e.Radius + GameConfig.BoltRadius;
                Vector3 d = e.transform.position - p;
                if (d.x * d.x + d.z * d.z > rr * rr) continue;
                e.TakeHit(GameConfig.BoltDamage);
                Destroy(gameObject);
                return;
            }
        }
    }
}
