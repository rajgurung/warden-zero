using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace WardenZero
{
    // Runs the game flow (src/core/Game.ts): menu, waves, XP and levelling, drops,
    // pause, death and the result screen.
    public class GameManager : MonoBehaviour
    {
        // Cinematic: a set piece (lift-off, flight, jump) has the camera and the Warden.
        public enum Mode { Menu, Play, Upgrade, Paused, Dying, Won, Over, Cinematic }

        public static GameManager Instance { get; private set; }

        public PlayerController player;
        public CameraFollow cameraFollow;
        public Hud hud;
        public Menus menus;
        public GreenfangMission mission; // set in the Greenfang scene: it drives spawns and the win
        public Extraction extraction; // the arena's campaign ending (chopper, LZ, boarding)
        public JungleStage jungle; // set in the jungle scene: flight, jump, landing, checkpoint
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
        public bool RunActive => CurrentMode == Mode.Play || CurrentMode == Mode.Upgrade || CurrentMode == Mode.Paused || CurrentMode == Mode.Cinematic;
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
        Mode pausedFrom = Mode.Play;
        static bool debugUsed; // the ?campaign= shortcut applies once per page load

        void Awake()
        {
            Instance = this;
            if (mission == null && jungle == null) World.UseArena();
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
            DebugSideCam.AddIfAsked(Application.absoluteURL);
            // The mission and jungle scenes drop straight in; the arena opens on the menu.
            if (mission != null || jungle != null)
            {
                StartRun();
                return;
            }
            EnterMenu();
            if (!debugUsed) DebugCampaign(Campaign.DebugStart(Application.absoluteURL));
        }

        // ?campaign=extraction starts the campaign at the extraction; flight, jump, jungle and
        // checkpoint go straight to that part of the drop with a fresh run.
        public void DebugCampaign(string where)
        {
            if (where == null) return;
            debugUsed = true;
            if (where == "extraction")
            {
                StartCampaign();
                JumpToExtraction();
                // A level-1 Warden would not last the hold: start him at the LZ, sturdier.
                player.transform.position = Extraction.Lz + new Vector3(6, 0, -8);
                Run.Stats.MaxHealth = Run.Stats.Health = 400;
                return;
            }
            var drop = Campaign.DropFor(where);
            if (drop == null) return;
            Campaign.Begin();
            Campaign.Run = new RunState();
            Campaign.Start = drop.Value;
            StageLoader.LoadJungle();
        }

        void OnDestroy()
        {
            Time.timeScale = 1;
        }

        // ------------------------------------------------------------------ flow

        public void EnterMenu()
        {
            StageLoader.Cancel();
            Campaign.End();
            if (mission != null || jungle != null)
            {
                Time.timeScale = 1;
                SceneManager.LoadScene("Arena");
                return;
            }
            ClearWorld();
            if (extraction != null) extraction.ResetForRun();
            SetMode(Mode.Menu);
            player.gameObject.SetActive(false);
            hud.gameObject.SetActive(false);
            cameraFollow.attract = true;
            menus.ShowMenu();
        }

        // The menu's Campaign: Stage 1 is the arena, and its Colossus calls in the chopper.
        public void StartCampaign()
        {
            Campaign.Begin();
            StartRun();
        }

        public void StartRun()
        {
            ClearWorld();
            // The jungle carries on the campaign's run (level, upgrades, health, stats).
            Run = jungle != null && Campaign.Run != null ? Campaign.Run : new RunState();
            if (jungle != null) Campaign.Run = Run;
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
            if (mission != null)
            {
                mission.Begin();
                return;
            }
            if (jungle != null)
            {
                jungle.Begin();
                return;
            }
            if (extraction != null) extraction.ResetForRun();
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

        // Debug and test shortcut: the Colossus is down, the chopper is inbound.
        public void JumpToExtraction()
        {
            ClearWorld();
            Run.Wave = GameConfig.Waves.Length;
            hud.SetWave(Run.Wave, GameConfig.Waves.Length);
            bossSpawned = true;
            extraction.Begin();
        }

        // A set piece takes over the camera and the Warden; Play gives them back.
        public void BeginCinematic()
        {
            SetMode(Mode.Cinematic);
        }

        public void BeginPlay()
        {
            SetMode(Mode.Play);
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
            if (on && CurrentMode != Mode.Play && CurrentMode != Mode.Cinematic) return;
            // Not while the jungle is loading: the menu could leave mid-load.
            if (on && StageLoader.Busy) return;
            if (!on && CurrentMode != Mode.Paused) return;
            if (on) pausedFrom = CurrentMode;
            SetMode(on ? Mode.Paused : pausedFrom);
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
            if (hud != null && hud.touch != null) hud.touch.ClearPresses();
            Time.timeScale = m == Mode.Upgrade || m == Mode.Paused ? 0 : 1;
            Cursor.visible = m != Mode.Play && m != Mode.Cinematic;
            // No see-through silhouette in set pieces (he would show through the chopper's hull).
            if (player != null && player.view is WardenModelView v) v.SetXRay(m != Mode.Cinematic);
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

        // Greenfang's win: a short beat, then the result screen.
        public void MissionComplete(string banner)
        {
            SetMode(Mode.Won);
            stateTimer = -0.5f;
            hud.Banner(banner, 2, GameConfig.Hex(0x9bff67));
        }

        // The jungle's end of the drop slice: checkpoint A is saved; a banner, then the result.
        public void MilestoneComplete()
        {
            SetMode(Mode.Won);
            stateTimer = -1.6f;
            hud.Banner("MILESTONE 1 COMPLETE", 2.6f, GameConfig.Gold);
            PlaySound(upgradeSound, 0.6f);
        }

        void Victory()
        {
            if (Campaign.Active && extraction != null)
            {
                hud.Banner("COLOSSUS DOWN", 1.7f);
                extraction.Begin();
                return;
            }
            SetMode(Mode.Won);
            stateTimer = 0;
            hud.Banner("COLOSSUS DOWN", 1.7f);
        }

        void Finish(bool win)
        {
            SetMode(Mode.Over);
            if (jungle != null) menus.ShowMilestoneResult(Run);
            else if (mission != null) menus.ShowMissionResult(win, Run, mission.ObjectivesDone, GreenfangMission.Objectives);
            else menus.ShowResult(win, Run);
        }

        // Remove everything a run leaves in the world.
        void ClearWorld()
        {
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) Destroy(e.gameObject);
            foreach (var b in FindObjectsByType<Bolt>(FindObjectsSortMode.None)) b.Release();
            foreach (var sp in FindObjectsByType<Spit>(FindObjectsSortMode.None)) Destroy(sp.gameObject);
            foreach (var f in FindObjectsByType<FadeOut>(FindObjectsSortMode.None)) Destroy(f.gameObject);
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
                case Mode.Cinematic:
                    if (jungle != null) jungle.Tick(Time.deltaTime);
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
                if (CurrentMode == Mode.Play || CurrentMode == Mode.Cinematic) Pause(true);
                else if (CurrentMode == Mode.Paused) Pause(false);
            }
            if (CurrentMode == Mode.Upgrade)
            {
                if (kb.digit1Key.wasPressedThisFrame) PickUpgrade(0);
                else if (kb.digit2Key.wasPressedThisFrame) PickUpgrade(1);
                else if (kb.digit3Key.wasPressedThisFrame) PickUpgrade(2);
            }
            if (CurrentMode == Mode.Over && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                if (jungle != null) EnterMenu();
                else StartRun();
            }
        }

        void UpdatePlay(float dt)
        {
            var s = Run.Stats;
            Run.Seconds += dt;
            if (mission != null)
            {
                mission.Tick(dt);
                if (CurrentMode == Mode.Play)
                {
                    hud.SetHealth(s.Health, s.MaxHealth);
                    hud.SetCooldowns(player.DashReady, player.BombReady);
                }
                return;
            }
            if (jungle != null)
            {
                jungle.Tick(dt);
                hud.SetHealth(s.Health, s.MaxHealth);
                hud.SetCooldowns(player.DashReady, player.BombReady);
                return;
            }

            regenTimer += dt;
            if (regenTimer >= 1)
            {
                regenTimer -= 1;
                if (s.Regen > 0 && s.Health < s.MaxHealth) player.Heal(s.Regen);
            }

            if (extraction != null && extraction.Running)
            {
                extraction.Tick(dt);
                hud.SetHealth(s.Health, s.MaxHealth);
                hud.SetCooldowns(player.DashReady, player.BombReady);
                return;
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
                    foreach (var b in FindObjectsByType<Bolt>(FindObjectsSortMode.None)) b.Release();
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
            p.x = Mathf.Clamp(p.x, -World.HalfW + m, World.HalfW - m);
            p.z = Mathf.Clamp(p.z, -World.HalfD + m, World.HalfD - m);
            p = GameConfig.ResolveCircle(p, stats.Radius);
            var e = Instantiate(enemyPrefab, p, Quaternion.identity);
            e.Init(stats, FramesFor(stats.Art));
            return e;
        }

        // Spawn at a given spot (Greenfang clusters, the Warlord and its guard).
        public Enemy SpawnEnemyAt(EnemyType type, Vector3 position)
        {
            var stats = GameConfig.Enemy(type);
            var e = Instantiate(enemyPrefab, GameConfig.ResolveCircle(position, stats.Radius), Quaternion.identity);
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
            if (mission != null)
            {
                // Greenfang scoring (v1): 100 per kill, no gems or drops.
                Run.Score += 100;
                Run.Kills += 1;
                PlaySound(enemyDieSound, 0.4f, Detune(400));
                hud.SetScore(Run.Score, Run.Coins);
                mission.OnEnemyKilled(e);
                return;
            }
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
