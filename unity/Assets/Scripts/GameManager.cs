using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace WardenZero
{
    // Runs the waves, keeps score, and handles game over / restart.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerController player;
        public Hud hud;
        public Enemy enemyPrefab;
        public Sprite[] gruntWalk;
        public Sprite[] runnerWalk;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip shootSound;
        public AudioClip enemyHitSound;
        public AudioClip enemyDieSound;
        public AudioClip hurtSound;
        public AudioClip dashSound;
        public AudioClip waveStartSound;
        public AudioClip gameOverSound;

        enum State { Playing, WaveClear, GameOver, Victory }

        State state;
        int wave; // 1-based
        int score;
        float spawnTimer;
        float stateTimer;
        readonly List<EnemyType> queue = new List<EnemyType>();

        public bool IsPlaying => state == State.Playing || state == State.WaveClear;
        public int Wave => wave;
        public int Score => score;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            hud.SetScore(0);
            BeginWave(1);
        }

        void BeginWave(int n)
        {
            wave = n;
            state = State.Playing;
            hud.SetWave(n, GameConfig.Waves.Length);
            hud.Banner($"WAVE {n}", 1.6f);
            PlaySound(waveStartSound, 0.4f);
            queue.Clear();
            foreach (var (type, count) in GameConfig.Waves[n - 1])
                for (int i = 0; i < count; i++) queue.Add(type);
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (queue[i], queue[j]) = (queue[j], queue[i]);
            }
            spawnTimer = 0.4f;
        }

        void Update()
        {
            hud.SetHealth(player.Health, GameConfig.PlayerMaxHealth);
            hud.SetDash(player.DashReady);

            switch (state)
            {
                case State.Playing:
                    UpdateSpawning();
                    if (queue.Count == 0 && Enemy.All.Count == 0)
                    {
                        if (wave >= GameConfig.Waves.Length)
                        {
                            state = State.Victory;
                            stateTimer = 0;
                            hud.Banner($"ARENA SECURED\n<size=26>SCORE {score}   ·   PRESS R TO PLAY AGAIN</size>", -1);
                        }
                        else
                        {
                            state = State.WaveClear;
                            stateTimer = GameConfig.WaveClearDelay;
                            hud.Banner("WAVE CLEAR", GameConfig.WaveClearDelay);
                        }
                    }
                    break;
                case State.WaveClear:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0)
                    {
                        player.ResetPosition();
                        BeginWave(wave + 1);
                    }
                    break;
                case State.GameOver:
                case State.Victory:
                    stateTimer += Time.deltaTime;
                    var kb = Keyboard.current;
                    var mouse = Mouse.current;
                    bool restart = (kb != null && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                        || (mouse != null && mouse.leftButton.wasPressedThisFrame);
                    if (restart && stateTimer > 1f) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    break;
            }
        }

        void UpdateSpawning()
        {
            if (queue.Count == 0) return;
            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0) return;
            spawnTimer = GameConfig.SpawnInterval;
            for (int i = 0; i < GameConfig.SpawnBatch && queue.Count > 0; i++)
            {
                if (Enemy.All.Count >= GameConfig.MaxEnemies) break;
                Spawn(queue[0]);
                queue.RemoveAt(0);
            }
        }

        // Spawn on a ring around the Warden (off screen), clamped into the arena.
        void Spawn(EnemyType type)
        {
            var stats = GameConfig.Enemy(type);
            float a = Random.value * Mathf.PI * 2;
            float d = Random.Range(GameConfig.SpawnMin, GameConfig.SpawnMax);
            Vector3 p = player.transform.position + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
            float m = 40 * GameConfig.PX;
            p.x = Mathf.Clamp(p.x, -GameConfig.HalfW + m, GameConfig.HalfW - m);
            p.z = Mathf.Clamp(p.z, -GameConfig.HalfD + m, GameConfig.HalfD - m);
            p = GameConfig.ResolveCircle(p, stats.Radius);
            var e = Instantiate(enemyPrefab, p, Quaternion.identity);
            e.Init(stats, stats.RunnerArt ? runnerWalk : gruntWalk);
        }

        public void OnEnemyKilled(int value)
        {
            score += value;
            hud.SetScore(score);
            PlaySound(enemyDieSound, 0.35f);
        }

        public void OnPlayerHurt()
        {
            hud.SetHealth(player.Health, GameConfig.PlayerMaxHealth);
            PlaySound(hurtSound, 0.5f);
            if (!player.IsDead) return;
            state = State.GameOver;
            stateTimer = 0;
            hud.Banner($"GAME OVER\n<size=26>SCORE {score}   ·   PRESS R TO RESTART</size>", -1, true);
            PlaySound(gameOverSound, 0.6f);
        }

        public void PlaySound(AudioClip clip, float volume)
        {
            if (clip != null) audioSource.PlayOneShot(clip, volume);
        }
    }
}
