using System.Collections.Generic;
using UnityEngine;

namespace WardenZero
{
    // Heart (+18 HP) or coin (+1 coin, +25 score) dropped by some kills
    // (Game.dropPickup / updatePickups). Bobs, spins, blinks in its last second, expires after 8 s.
    public class Pickup : MonoBehaviour
    {
        public static readonly List<Pickup> All = new List<Pickup>();

        public bool isHeart;
        public Renderer visual;

        float t;

        void OnEnable()
        {
            All.Add(this);
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
            transform.Rotate(0, 2.5f * Mathf.Rad2Deg * dt, 0, Space.World);
            Vector3 m = transform.position;
            m.y = 0.7f + Mathf.Sin(t * 4.5f) * 0.12f;
            transform.position = m;
            visual.enabled = !(t > GameConfig.PickupLife - 1 && Mathf.FloorToInt(t * 7) % 2 == 0);

            Vector3 p = gm.player.transform.position;
            float d = new Vector2(p.x - m.x, p.z - m.z).magnitude;
            if (d < GameConfig.PlayerRadius + 0.4f)
            {
                Effects.Instance.Burst(m, isHeart ? GameConfig.Health : GameConfig.Gold, 16, new Vector2(2, 5), new Vector2(0.08f, 0.3f));
                gm.OnPickupCollected(isHeart);
                Destroy(gameObject);
            }
            else if (t >= GameConfig.PickupLife)
            {
                Destroy(gameObject);
            }
        }
    }
}
