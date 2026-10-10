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
        public Renderer shockRing; // gold ring quad, expanded by BombBlast

        float shockT = -1;
        float shockRadius;
        Color shockColor;
        MaterialPropertyBlock block;

        void Awake()
        {
            Instance = this;
            block = new MaterialPropertyBlock();
            shockColor = shockRing.sharedMaterial.GetColor("_BaseColor");
            shockRing.enabled = false;
        }

        void Update()
        {
            if (shockT < 0) return;
            // 0.35 s expanding, fading ring (Effects.bombBlast).
            shockT += Time.deltaTime;
            float k = shockT / 0.35f;
            if (k >= 1)
            {
                shockT = -1;
                shockRing.enabled = false;
                return;
            }
            float size = 0.2f + k * shockRadius * 2;
            shockRing.transform.localScale = new Vector3(size, size, 1);
            block.SetColor("_BaseColor", shockColor * (1 - k));
            shockRing.SetPropertyBlock(block);
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

        public void EnemyDeath(Vector3 pos, Color color)
        {
            Burst(pos + Vector3.up * 0.8f, color, 22, new Vector2(3, 10), new Vector2(0.1f, 0.35f));
            cameraFollow.AddShake(0.06f);
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
            shockRing.transform.position = new Vector3(pos.x, 0.25f, pos.z);
            shockRadius = radius;
            shockT = 0;
            shockRing.enabled = true;
            Burst(pos + Vector3.up * 0.5f, GameConfig.Gold, 90, new Vector2(8, 20), new Vector2(0.15f, 0.5f));
            cameraFollow.AddShake(0.5f);
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
