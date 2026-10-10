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

        void Awake()
        {
            Instance = this;
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
            Burst(pos + Vector3.up * 0.6f, GameConfig.Accent, 16, new Vector2(2, 6), new Vector2(0.08f, 0.25f));
        }
    }
}
