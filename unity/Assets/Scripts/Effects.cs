using UnityEngine;

namespace WardenZero
{
    // Particle bursts and camera shake, mirroring src/systems/Effects.ts.
    // One world-space particle system serves every burst; each Emit call
    // sets its own colour, speed and size.
    public class Effects : MonoBehaviour
    {
        public static Effects Instance { get; private set; }

        public ParticleSystem sparks;
        public CameraFollow cameraFollow;
        public Renderer shockRing; // ring quad template, expanded by Blast

        // A few rings so overlapping blasts (strike barrages) each get one.
        class Shock { public Renderer ring; public float t = -1; public float radius; public Color color; }
        Shock[] shocks;
        int nextShock;
        Color shockColor;
        MaterialPropertyBlock block;

        void Awake()
        {
            Instance = this;
            block = new MaterialPropertyBlock();
            shockColor = shockRing.sharedMaterial.GetColor("_BaseColor");
            shocks = new Shock[8];
            for (int i = 0; i < shocks.Length; i++)
            {
                var r = i == 0 ? shockRing : Instantiate(shockRing, shockRing.transform.parent);
                r.enabled = false;
                shocks[i] = new Shock { ring = r };
            }
        }

        void Update()
        {
            foreach (var s in shocks)
            {
                if (s.t < 0) continue;
                // 0.35 s expanding, fading ring (Effects.bombBlast).
                s.t += Time.deltaTime;
                float k = s.t / 0.35f;
                if (k >= 1)
                {
                    s.t = -1;
                    s.ring.enabled = false;
                    continue;
                }
                float size = 0.2f + k * s.radius * 2;
                s.ring.transform.localScale = new Vector3(size, size, 1);
                block.SetColor("_BaseColor", s.color * (1 - k));
                s.ring.SetPropertyBlock(block);
            }
        }

        // Expanding shockwave ring and a burst (bomb, strikes, the Warlord's pound).
        public void Blast(Vector3 pos, float radius, Color burstColor, float shake)
        {
            var s = shocks[nextShock];
            nextShock = (nextShock + 1) % shocks.Length;
            s.ring.transform.position = new Vector3(pos.x, 0.25f, pos.z);
            s.radius = radius;
            s.color = shockColor;
            s.t = 0;
            s.ring.enabled = true;
            Burst(pos + Vector3.up * 0.5f, burstColor, 90, new Vector2(8, 20), new Vector2(0.15f, 0.5f));
            cameraFollow.AddShake(shake);
        }

        public void Burst(Vector3 pos, Color color, int count, Vector2 power, Vector2 size)
        {
            var main = sparks.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(power.x, power.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            var p = new ParticleSystem.EmitParams { position = pos, applyShapeToPosition = true, startColor = color };
            sparks.Emit(p, count);
        }

        public void BoltImpact(Vector3 pos)
        {
            Burst(pos, GameConfig.Accent, 5, new Vector2(2, 6), new Vector2(0.06f, 0.18f));
        }

        // big = the Colossus: a much larger burst and a heavy shake.
        public void EnemyDeath(Vector3 pos, Color color, bool big = false)
        {
            if (big) Burst(pos + Vector3.up * 3, color, 120, new Vector2(6, 18), new Vector2(0.2f, 0.7f));
            else Burst(pos + Vector3.up * 0.8f, color, 22, new Vector2(3, 10), new Vector2(0.1f, 0.35f));
            cameraFollow.AddShake(big ? 1 : 0.06f);
        }

        public void PlayerHurt(Vector3 pos)
        {
            Burst(pos + Vector3.up * 1.2f, GameConfig.Health, 18, new Vector2(3, 9), new Vector2(0.08f, 0.3f));
            cameraFollow.AddShake(0.35f);
        }

        public void Dash(Vector3 pos)
        {
            Burst(pos + Vector3.up * 0.8f, GameConfig.Accent, 30, new Vector2(2, 5), new Vector2(0.08f, 0.3f));
        }

        public void BombBlast(Vector3 pos, float radius)
        {
            Blast(pos, radius, GameConfig.Gold, 0.5f);
        }

        public void LevelUp(Vector3 pos)
        {
            Burst(pos + Vector3.up, GameConfig.Accent, 60, new Vector2(3, 8), new Vector2(0.08f, 0.3f));
        }

        public void PlayerDeath(Vector3 pos)
        {
            Burst(pos + Vector3.up, GameConfig.Health, 80, new Vector2(4, 12), new Vector2(0.08f, 0.3f));
        }
    }
}
