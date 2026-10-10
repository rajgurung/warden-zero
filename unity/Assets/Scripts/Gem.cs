using System.Collections.Generic;
using UnityEngine;

namespace WardenZero
{
    // XP gem dropped on every kill (Game.dropGem / updateGems): pops up, settles, bobs,
    // and flies to the Warden inside the magnet range, or from anywhere once the wave is empty.
    public class Gem : MonoBehaviour
    {
        public static readonly List<Gem> All = new List<Gem>();

        Vector3 velocity;
        bool settled;
        float t;

        void OnEnable()
        {
            All.Add(this);
            t = Random.value * 6;
            velocity = new Vector3((Random.value - 0.5f) * 3, 5, (Random.value - 0.5f) * 3);
        }

        void OnDisable()
        {
            All.Remove(this);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (!gm.IsPlaying) return;
            float dt = Time.deltaTime;
            t += dt;
            transform.Rotate(1.4f * Mathf.Rad2Deg * dt, 3 * Mathf.Rad2Deg * dt, 0, Space.Self);
            Vector3 m = transform.position;
            if (!settled)
            {
                m += velocity * dt;
                velocity.y -= 18 * dt;
                if (m.y <= 0.55f) { m.y = 0.55f; settled = true; }
                transform.position = m;
                return;
            }
            m.y = 0.55f + Mathf.Sin(t * 4.8f) * 0.12f;
            Vector3 p = gm.player.transform.position;
            float dx = p.x - m.x, dz = p.z - m.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            bool vacuum = gm.WaveIsEmpty;
            if (d > 1e-3f && (vacuum || d < gm.Run.Stats.MagnetRange * GameConfig.PX))
            {
                float step = Mathf.Min(d, (vacuum ? 700 : 420) * GameConfig.PX * dt);
                m.x += dx / d * step;
                m.z += dz / d * step;
            }
            transform.position = m;
            if (d < GameConfig.PlayerRadius + 0.35f)
            {
                Effects.Instance.Burst(m, GameConfig.Accent, 6, new Vector2(1, 3), new Vector2(0.05f, 0.15f));
                Destroy(gameObject);
                gm.OnGemCollected();
            }
        }
    }
}
