using UnityEngine;

namespace WardenZero
{
    // A Spitter's glob (v1 JungleScene.spitFire): flies at the Warden, 12 damage on
    // contact (dash and hurt i-frames still protect him), dies on walls or after 2.6 s.
    public class Spit : MonoBehaviour
    {
        Vector3 velocity;
        float life;

        public void Launch(Vector3 direction)
        {
            velocity = direction * GameConfig.SpitSpeed;
            life = GameConfig.SpitLife;
        }

        void Update()
        {
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
                Destroy(gameObject);
                return;
            }
            Vector3 d = gm.player.transform.position - p;
            if (d.x * d.x + d.z * d.z < Mathf.Pow(GameConfig.PlayerRadius + 0.25f, 2))
            {
                Effects.Instance.Burst(p, GameConfig.Hex(0x9bff67), 10, new Vector2(2, 5), new Vector2(0.06f, 0.2f));
                gm.player.TryHurt(GameConfig.SpitDamage);
                Destroy(gameObject);
            }
        }
    }
}
