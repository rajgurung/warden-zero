using System.Collections.Generic;
using UnityEngine;

namespace WardenZero
{
    // Chases the Warden with soft separation from neighbours and deals contact damage.
    public class Enemy : MonoBehaviour
    {
        // Live (not dying) enemies, used by bolts, separation and wave-clear checks.
        public static readonly List<Enemy> All = new List<Enemy>();

        public SpriteRenderer body;

        public float Radius { get; private set; }

        EnemyStats stats;
        Sprite[] walkFrames;
        float hp;
        float lastContact = -9;
        float hitFlash;
        float grow;
        float phase;
        float dieTimer = -1;

        public void Init(EnemyStats s, Sprite[] frames)
        {
            stats = s;
            walkFrames = frames;
            hp = s.MaxHealth;
            Radius = s.Radius;
            phase = Random.value * 10;
            body.sprite = frames[0];
            body.color = s.Tint;
            transform.localScale = Vector3.one * 0.01f;
            All.Add(this);
        }

        void OnDestroy()
        {
            All.Remove(this);
        }

        public void TakeHit(int damage)
        {
            if (dieTimer >= 0) return;
            hp -= damage;
            hitFlash = 0.1f;
            if (hp > 0)
            {
                GameManager.Instance.PlaySound(GameManager.Instance.enemyHitSound, 0.25f);
                return;
            }
            All.Remove(this);
            dieTimer = 0;
            Effects.Instance.EnemyDeath(transform.position, stats.FxColor);
            GameManager.Instance.OnEnemyKilled(stats.Score);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (dieTimer >= 0)
            {
                // Quick squash and fade, then remove.
                dieTimer += dt;
                float k = Mathf.Clamp01(1 - dieTimer / 0.18f);
                transform.localScale = new Vector3(stats.VisualScale * (1 + (1 - k) * 0.4f), stats.VisualScale * k, 1);
                body.color = new Color(1, 0.4f, 0.3f, k);
                if (k <= 0) Destroy(gameObject);
                return;
            }

            // Pop in on spawn.
            grow = Mathf.Min(1, grow + dt * 5.5f);
            float pulse = hitFlash > 0 ? 1.12f : 1;
            transform.localScale = Vector3.one * (stats.VisualScale * Mathf.Max(0.01f, EaseOutBack(grow)) * pulse);
            body.color = hitFlash > 0 ? new Color(1, 0.55f, 0.55f) : stats.Tint;
            hitFlash -= dt;

            var gm = GameManager.Instance;
            if (!gm.IsPlaying) return;

            Vector3 p = transform.position;
            Vector3 target = gm.player.transform.position;
            float dx = target.x - p.x, dz = target.z - p.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist < 1e-4f) dist = 1;
            dx /= dist;
            dz /= dist;

            float sx = 0, sz = 0;
            foreach (var o in All)
            {
                if (o == this) continue;
                Vector3 op = o.transform.position;
                float ox = p.x - op.x, oz = p.z - op.z;
                float min = Radius + o.Radius;
                float od2 = ox * ox + oz * oz;
                if (od2 < min * min && od2 > 1e-6f)
                {
                    float od = Mathf.Sqrt(od2);
                    float k = (min - od) / min;
                    sx += ox / od * k * 2.5f;
                    sz += oz / od * k * 2.5f;
                }
            }
            p.x += (dx + sx) * stats.Speed * dt;
            p.z += (dz + sz) * stats.Speed * dt;
            transform.position = GameConfig.ResolveCircle(p, Radius);

            // Walk cycle; faster enemies step faster. Mirror when heading screen-left.
            float gait = Time.time * (4 + stats.Speed * 1.2f) + phase;
            body.sprite = walkFrames[(int)gait % walkFrames.Length];
            body.flipX = dx < 0;

            if (dist < Radius + GameConfig.PlayerRadius && Time.time - lastContact >= GameConfig.ContactCooldown)
            {
                if (gm.player.TryHurt(stats.ContactDamage)) lastContact = Time.time;
            }
        }

        static float EaseOutBack(float g)
        {
            return g < 1 ? 1 + 2.2f * Mathf.Pow(g - 1, 3) + 1.2f * Mathf.Pow(g - 1, 2) : 1;
        }
    }
}
