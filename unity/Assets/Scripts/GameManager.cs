using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WardenZero
{
    // Runs the game flow (src/core/Game.ts): menu, waves, XP and levelling, drops,
    // pause, death and the result screen.
    public class GameManager : MonoBehaviour
    {
        public enum Mode { Menu, Play, Upgrade, Paused, Dying, Won, Over }

        public static GameManager Instance { get; private set; }

        public PlayerController player;
        public CameraFollow cameraFollow;
        public Hud hud;
        public Menus menus;
        public Enemy enemyPrefab;
        public Gem gemPrefab;
        public Pickup heartPrefab;
        public Pickup coinPrefab;
        public Spit spitPrefab;
        public Sprite[] gruntWalk;
        public Sprite[] runnerWalk;
        public Sprite skeletonTile;
        public Sprite spiderTile;
        public Sprite demonTile;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip shootSound;
        public AudioClip enemyHitSound;
        public AudioClip enemyDieSound;
        public AudioClip hurtSound;
        public AudioClip dashSound;
        public AudioClip waveStartSound;
        public AudioClip gameOverSound;
        public AudioClip bombSound;
        public AudioClip pickupSound;
        public AudioClip upgradeSound;

        public Mode CurrentMode { get; private set; }
        public RunState Run { get; private set; } = new RunState();
        public bool IsPlaying => CurrentMode == Mode.Play;
        // A run is in progress (possibly paused or picking an upgrade).
        public bool RunActive => CurrentMode == Mode.Play || CurrentMode == Mode.Upgrade || CurrentMode == Mode.Paused;
        // Nothing left to spawn or kill: gems get vacuumed to the Warden.
        public bool WaveIsEmpty => queue.Count == 0 && Enemy.All.Count == 0;
        public int Wave => Run.Wave;
        public int Score => Run.Score;
        public IReadOnlyList<Upgrade> Offered => offered;
        public Enemy Boss => boss;
        public int QueuedSpawns => queue.Count;

        readonly List<EnemyType> queue = new List<EnemyType>();
        readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
        AudioSource[] voices;
        int nextVoice;
        List<Upgrade> offered = new List<Upgrade>();
        float spawnTimer;
        float regenTimer;
        float stateTimer;
        float clearTimer;
        bool transitioning;
        Enemy boss;
        bool bossSpawned;
        float bossSummonTimer;

        void Awake()
        {
            Instance = this;
            // A few voices so clips can be detuned individually (Sound.play's detune).
            voices = new AudioSource[8];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
        }

        void Start()
        {
            EnterMenu();
        }

        void OnDestroy()
        {
            Time.timeScale = 1;
        }

        // ------------------------------------------------------------------ flow

        public void EnterMenu()
        {
            ClearWorld();
            SetMode(Mode.Menu);
            player.gameObject.SetActive(false);
            hud.gameObject.SetActive(false);
            cameraFollow.attract = true;
            menus.ShowMenu();
        }

        public void StartRun()
        {
            ClearWorld();
            Run = new RunState();
            player.gameObject.SetActive(true);
            player.ResetForRun();
            cameraFollow.attract = false;
            cameraFollow.Snap();
            hud.gameObject.SetActive(true);
            menus.HideAll();
            transitioning = false;
            regenTimer = 0;
            boss = null;
            bossSpawned = false;
            hud.SetBoss(0, 1);
            SetMode(Mode.Play);
            RefreshHud();
            // Debug shortcuts in the page URL: ?boss skips to the Colossus (as in Babylon),
            // ?wave=N starts at wave N.
            string url = Application.absoluteURL;
            var waveParam = System.Text.RegularExpressions.Regex.Match(url, @"[?&]wave=(\d+)");
            if (url.Contains("?boss") || url.Contains("&boss")) JumpToBoss();
            else if (waveParam.Success) BeginWave(Mathf.Clamp(int.Parse(waveParam.Groups[1].Value), 1, GameConfig.Waves.Length));
            else BeginWave(1);
        }

        // Debug and test shortcut: final wave number, empty arena, Colossus inbound.
        public void JumpToBoss()
        {
            queue.Clear();
            Run.Wave = GameConfig.Waves.Length;
            hud.SetWave(Run.Wave, GameConfig.Waves.Length);
            StartBossFight();
        }

        // Test hook: stop the current wave's remaining spawns.
        public void ClearQueue() => queue.Clear();

        // Debug and test shortcut: start wave n now.
        public void JumpToWave(int n)
        {
            ClearWorld();
            BeginWave(n);
        }

        void StartBossFight()
        {
            bossSpawned = true;
            hud.Banner("COLOSSUS INBOUND", 1.7f, true);
            PlaySound(waveStartSound, 0.6f);
            cameraFollow.AddShake(0.6f);
            boss = SpawnEnemy(EnemyType.Boss);
            bossSummonTimer = GameConfig.BossSummonEvery;
            hud.SetBoss(boss.Health, boss.Stats.MaxHealth);
        }

        public void Pause(bool on)
        {
            if (on && CurrentMode != Mode.Play) return;
            if (!on && CurrentMode != Mode.Paused) return;
            SetMode(on ? Mode.Paused : Mode.Play);
            menus.ShowPause(on);
        }

        public void PickUpgrade(int index)
        {
            if (CurrentMode != Mode.Upgrade || index < 0 || index >= offered.Count) return;
            Upgrades.Apply(Run, offered[index].Id);
            menus.HideUpgrades();
            PlaySound(upgradeSound, 0.5f);
            SetMode(Mode.Play);
            Effects.Instance.LevelUp(player.transform.position);
            RefreshHud();
            // A pick can leave enough XP banked for another level.
            if (Run.Xp >= Run.XpToNext) LevelUp();
        }

        // Time is frozen outside play; the cursor shows whenever a menu is up.
        void SetMode(Mode m)
        {
            CurrentMode = m;
            Time.timeScale = m == Mode.Upgrade || m == Mode.Paused ? 0 : 1;
            Cursor.visible = m != Mode.Play;
        }

        void BeginWave(int n)
        {
            Run.Wave = n;
            transitioning = false;
            hud.SetWave(n, GameConfig.Waves.Length);
            hud.Banner($"WAVE {n}", 1.7f);
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

        void LevelUp()
        {
            Run.Xp -= Run.XpToNext;
            Run.Level += 1;
            Run.XpToNext = RunState.XpFor(Run.Level);
            hud.SetXp(Run.Xp, Run.XpToNext, Run.Level);
            PlaySound(upgradeSound, 0.4f);
            offered = Upgrades.PickThree(Run);
            if (offered.Count == 0)
            {
                player.Heal(25);
                return;
            }
            SetMode(Mode.Upgrade);
            menus.ShowUpgrades(Run, offered);
        }

        void GameOver()
        {
            SetMode(Mode.Dying);
            stateTimer = 0;
            queue.Clear();
            PlaySound(gameOverSound, 0.6f);
            Effects.Instance.PlayerDeath(player.transform.position);
        }

        void Victory()
        {
            SetMode(Mode.Won);
            stateTimer = 0;
            hud.Banner("COLOSSUS DOWN", 1.7f);
        }

        void Finish(bool win)
        {
            SetMode(Mode.Over);
            menus.ShowResult(win, Run);
        }

        // Remove everything a run leaves in the world.
        void ClearWorld()
        {
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) Destroy(e.gameObject);
            foreach (var b in FindObjectsByType<Bolt>(FindObjectsSortMode.None)) Destroy(b.gameObject);
            foreach (var sp in FindObjectsByType<Spit>(FindObjectsSortMode.None)) Destroy(sp.gameObject);
            foreach (var g in Gem.All.ToArray()) Destroy(g.gameObject);
            foreach (var p in Pickup.All.ToArray()) Destroy(p.gameObject);
            Enemy.All.Clear();
            Gem.All.Clear();
            Pickup.All.Clear();
            queue.Clear();
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            HandleKeys();
            switch (CurrentMode)
            {
                case Mode.Play:
                    UpdatePlay(Time.deltaTime);
                    break;
                case Mode.Dying:
                case Mode.Won:
                    stateTimer += Time.deltaTime;
                    if (stateTimer > GameConfig.DyingTime) Finish(CurrentMode == Mode.Won);
                    break;
            }
        }

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
            {
                if (CurrentMode == Mode.Play) Pause(true);
                else if (CurrentMode == Mode.Paused) Pause(false);
            }
            if (CurrentMode == Mode.Upgrade)
            {
                if (kb.digit1Key.wasPressedThisFrame) PickUpgrade(0);
                else if (kb.digit2Key.wasPressedThisFrame) PickUpgrade(1);
                else if (kb.digit3Key.wasPressedThisFrame) PickUpgrade(2);
            }
            if (CurrentMode == Mode.Over && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) StartRun();
        }

        void UpdatePlay(float dt)
        {
            var s = Run.Stats;
            Run.Seconds += dt;

            regenTimer += dt;
            if (regenTimer >= 1)
            {
                regenTimer -= 1;
                if (s.Regen > 0 && s.Health < s.MaxHealth) player.Heal(s.Regen);
            }

            UpdateSpawning(dt);

            if (boss != null)
            {
                bossSummonTimer -= dt;
                if (bossSummonTimer <= 0)
                {
                    bossSummonTimer = GameConfig.BossSummonEvery;
                    for (int i = 0; i < 4; i++) SpawnEnemy(EnemyType.Swarmer);
                }
                hud.SetBoss(boss.Health, boss.Stats.MaxHealth);
            }

            if (transitioning)
            {
                clearTimer -= dt;
                if (clearTimer <= 0)
                {
                    foreach (var b in FindObjectsByType<Bolt>(FindObjectsSortMode.None)) Destroy(b.gameObject);
                    player.ResetPosition();
                    BeginWave(Run.Wave + 1);
                }
            }
            else if (WaveIsEmpty && Gem.All.Count == 0)
            {
                if (Run.Wave >= GameConfig.Waves.Length)
                {
                    if (!bossSpawned) StartBossFight();
                }
                else
                {
                    transitioning = true;
                    clearTimer = GameConfig.WaveClearDelay;
                    hud.Banner("WAVE CLEAR", GameConfig.WaveClearDelay);
                }
            }

            hud.SetHealth(s.Health, s.MaxHealth);
            hud.SetCooldowns(player.DashReady, player.BombReady);
        }

        void UpdateSpawning(float dt)
        {
            if (queue.Count == 0) return;
            spawnTimer -= dt;
            if (spawnTimer > 0) return;
            spawnTimer = GameConfig.SpawnInterval;
            for (int i = 0; i < GameConfig.SpawnBatch && queue.Count > 0; i++)
            {
                if (SpawnEnemy(queue[0]) == null) break;
                queue.RemoveAt(0);
            }
        }

        // Spawn on a ring around the Warden (off screen), clamped into the arena.
        // Returns null when the arena is at its cap (the boss ignores the cap).
        public Enemy SpawnEnemy(EnemyType type)
        {
            if (Enemy.All.Count >= GameConfig.MaxEnemies && type != EnemyType.Boss) return null;
            var stats = GameConfig.Enemy(type);
            float a = Random.value * Mathf.PI * 2;
            float d = Random.Range(GameConfig.SpawnMin, GameConfig.SpawnMax);
            Vector3 p = player.transform.position + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
            float m = 40 * GameConfig.PX;
            p.x = Mathf.Clamp(p.x, -GameConfig.HalfW + m, GameConfig.HalfW - m);
            p.z = Mathf.Clamp(p.z, -GameConfig.HalfD + m, GameConfig.HalfD - m);
            p = GameConfig.ResolveCircle(p, stats.Radius);
            var e = Instantiate(enemyPrefab, p, Quaternion.identity);
            e.Init(stats, FramesFor(stats.Art));
            return e;
        }

        Sprite[] FramesFor(EnemyArt art)
        {
            switch (art)
            {
                case EnemyArt.Runner: return runnerWalk;
                case EnemyArt.Skeleton: return new[] { skeletonTile };
                case EnemyArt.Spider: return new[] { spiderTile };
                case EnemyArt.Demon: return new[] { demonTile };
                default: return gruntWalk;
            }
        }

        // ------------------------------------------------------------------ events

        public void OnEnemyKilled(Enemy e)
        {
            Run.Score += e.Stats.Score;
            Run.Kills += 1;
            if (e == boss)
            {
                boss = null;
                hud.SetBoss(0, 1);
                PlaySound(enemyDieSound, 1);
                hud.SetScore(Run.Score, Run.Coins);
                Victory();
                return;
            }
            PlaySound(enemyDieSound, 0.4f, Detune(400));
            Vector3 p = e.transform.position;
            Instantiate(gemPrefab, new Vector3(p.x, 0.6f, p.z), Quaternion.identity);
            if (Random.value <= GameConfig.PickupChance)
            {
                var prefab = Random.value < GameConfig.HeartShare ? heartPrefab : coinPrefab;
                Instantiate(prefab, new Vector3(p.x + (Random.value - 0.5f) * 0.8f, 0.7f, p.z + (Random.value - 0.5f) * 0.8f), prefab.transform.rotation);
            }
            if (Run.Stats.Lifesteal > 0) player.Heal(Run.Stats.Lifesteal);
            hud.SetScore(Run.Score, Run.Coins);
        }

        public void OnGemCollected()
        {
            Run.Score += GameConfig.GemScore;
            Run.Xp += 1;
            PlaySound(pickupSound, 0.3f, Detune(300));
            hud.SetScore(Run.Score, Run.Coins);
            if (Run.Xp >= Run.XpToNext) LevelUp();
            else hud.SetXp(Run.Xp, Run.XpToNext, Run.Level);
        }

        public void OnPickupCollected(bool heart)
        {
            PlaySound(pickupSound, 0.5f);
            if (heart)
            {
                player.Heal(GameConfig.HeartHeal);
            }
            else
            {
                Run.Coins += 1;
                Run.Score += GameConfig.CoinValue;
                hud.SetScore(Run.Score, Run.Coins);
            }
        }

        public void OnPlayerHurt()
        {
            hud.SetHealth(Run.Stats.Health, Run.Stats.MaxHealth);
            PlaySound(hurtSound, 0.55f);
            if (player.IsDead) GameOver();
        }

        void RefreshHud()
        {
            var s = Run.Stats;
            hud.SetHealth(s.Health, s.MaxHealth);
            hud.SetWave(Run.Wave, GameConfig.Waves.Length);
            hud.SetScore(Run.Score, Run.Coins);
            hud.SetXp(Run.Xp, Run.XpToNext, Run.Level);
        }

        // Throttled per clip so 80-enemy hit storms don't clip; detune in cents (Sound.play).
        public void PlaySound(AudioClip clip, float volume, float detune = 0)
        {
            if (clip == null) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(clip, out float last) && now - last < 0.035f) return;
            lastPlayed[clip] = now;
            if (detune == 0)
            {
                audioSource.PlayOneShot(clip, volume);
                return;
            }
            var v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            v.pitch = Mathf.Pow(2, detune / 1200);
            v.PlayOneShot(clip, volume);
        }

        // Random detune of up to +/- range/2 cents.
        public static float Detune(float range) => (Random.value - 0.5f) * range;

        void OnApplicationFocus(bool focused)
        {
            if (!focused) Pause(true);
        }
    }
}
