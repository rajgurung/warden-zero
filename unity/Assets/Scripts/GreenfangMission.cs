using UnityEngine;

namespace WardenZero
{
    // Operation Greenfang (e3bf116:src/scenes/JungleScene.ts): a jungle assault with an arc.
    // INSERTION -> PUSH (secure Beacon Alpha) -> ADVANCE (secure Beacon Bravo) ->
    // WARLORD (kill the mini-boss) -> EXTRACTION (survive 40 s) -> victory.
    // GameManager hands over spawning, kills and the win condition to this component.
    public class GreenfangMission : MonoBehaviour
    {
        public enum Phase { Insertion, Push, Advance, Warlord, Extraction, Ended }

        const float PX = GameConfig.PX;
        // v1 jungle world, 2800 x 1900 px.
        public const float WorldW = 2800 * PX;
        public const float WorldD = 1900 * PX;
        public const float BeaconRadius = 150 * PX;
        public const float CaptureTime = 4;
        public const float ExtractionTime = 40;
        public const float MaxHealth = 130;
        public const int Objectives = 5;

        public static readonly Vector3 PlayerStart = ToWorld(480, 1520);
        public static readonly Vector3[] Beacons = { ToWorld(1050, 1240), ToWorld(1700, 820), ToWorld(2320, 430) };
        static readonly string[] BeaconNames = { "ALPHA", "BRAVO", "LZ" };
        static readonly Vector3 WarlordSpawn = ToWorld(1980, 640);
        static readonly Color Green = GameConfig.Hex(0x9bff67);

        public StrikeSystem strikes;
        public MissionHud hud;
        public Renderer[] beaconRings;
        public Renderer poundRing; // the Warlord's red telegraph
        public Vector3[] trunks; // x, z, radius
        public AudioClip strikeReady;

        public Phase CurrentPhase { get; private set; }
        public float Capture { get; private set; }
        public Enemy Warlord { get; private set; }
        public float ExtractionLeft { get; private set; }
        public int ObjectivesDone { get; private set; }

        int beacon;
        float phaseTimer;
        float spawnTimer;
        float regenTimer;
        int spawnedThisPhase;
        float poundAt;
        float summonAt;
        float poundTell = -1;

        // v1 world (x right, y down, origin top-left) to the ground plane, origin centre.
        public static Vector3 ToWorld(float x, float y) => new Vector3((x - 1400) * PX, 0, -(y - 950) * PX);

        void Awake()
        {
            World.Use(WorldW / 2, WorldD / 2, new WallRect[0], trunks);
        }

        public void Begin()
        {
            var gm = GameManager.Instance;
            var s = gm.Run.Stats;
            s.MaxHealth = s.Health = MaxHealth;
            gm.player.transform.position = PlayerStart;
            gm.cameraFollow.Snap();
            strikes.ResetForRun();
            Warlord = null;
            ObjectivesDone = 0;
            Capture = 0;
            beacon = 0;
            spawnTimer = 1.2f;
            regenTimer = 0;
            poundTell = -1;
            poundRing.enabled = false;
            for (int i = 0; i < beaconRings.Length; i++) beaconRings[i].gameObject.SetActive(i == 0);
            CurrentPhase = Phase.Insertion;
            phaseTimer = 2.4f;
            gm.hud.SetBoss(0, 1);
            gm.hud.Banner("OPERATION GREENFANG", 2.2f, GameConfig.Gold);
            hud.SetObjective("INSERTION");
            hud.SetCapture(null);

            // Debug URL: ?phase=push|advance|warlord|extraction skips ahead.
            var m = System.Text.RegularExpressions.Regex.Match(Application.absoluteURL, @"[?&]phase=(\w+)");
            if (m.Success && System.Enum.TryParse(m.Groups[1].Value, true, out Phase p)) Enter(p);
        }

        // Test and debug hook: jump to a phase.
        public void Enter(Phase p)
        {
            switch (p)
            {
                case Phase.Push: EnterPush(); break;
                case Phase.Advance: EnterAdvance(); break;
                case Phase.Warlord: EnterWarlord(); break;
                case Phase.Extraction: EnterExtraction(); break;
            }
        }

        void EnterPush()
        {
            CurrentPhase = Phase.Push;
            beacon = 0;
            spawnedThisPhase = 0;
            Capture = 0;
            GameManager.Instance.hud.Banner("PHASE 1 · BREACH THE TREELINE", 2f, Green);
            hud.SetObjective("SECURE BEACON ALPHA");
        }

        void EnterAdvance()
        {
            CurrentPhase = Phase.Advance;
            beacon = 1;
            spawnedThisPhase = 0;
            Capture = 0;
            GameManager.Instance.hud.Banner("PHASE 2 · CUT THE CORRIDOR", 2f, Green);
            hud.SetObjective("SECURE BEACON BRAVO");
            beaconRings[1].gameObject.SetActive(true);
        }

        void EnterWarlord()
        {
            var gm = GameManager.Instance;
            CurrentPhase = Phase.Warlord;
            hud.SetCapture(null);
            gm.hud.Banner("WARLORD · GREENFANG'S CHAMPION", 2f, GameConfig.Magenta);
            hud.SetObjective("ELIMINATE THE WARLORD");
            // The Warlord between Bravo and the LZ, plus a small guard.
            Warlord = gm.SpawnEnemyAt(EnemyType.Warlord, WarlordSpawn);
            poundAt = Time.time + 4;
            summonAt = Time.time + 6;
            for (int i = 0; i < 6; i++)
                gm.SpawnEnemyAt(EnemyType.Grunt, WarlordSpawn + new Vector3(Random.Range(-140, 140) * PX, 0, Random.Range(-120, 120) * PX));
        }

        void EnterExtraction()
        {
            var gm = GameManager.Instance;
            CurrentPhase = Phase.Extraction;
            beacon = 2;
            spawnedThisPhase = 0;
            Warlord = null;
            gm.hud.SetBoss(0, 1);
            ExtractionLeft = ExtractionTime;
            gm.hud.Banner("EXTRACTION INBOUND · HOLD THE LZ", 2f, GameConfig.Gold);
            beaconRings[2].gameObject.SetActive(true);
            gm.PlaySound(strikeReady, 0.6f);
        }

        public void Tick(float dt)
        {
            var gm = GameManager.Instance;
            if (CurrentPhase == Phase.Insertion)
            {
                phaseTimer -= dt;
                if (phaseTimer <= 0) EnterPush();
            }
            else if (CurrentPhase == Phase.Push || CurrentPhase == Phase.Advance)
            {
                UpdateCapture(dt);
            }
            else if (CurrentPhase == Phase.Extraction)
            {
                ExtractionLeft -= dt;
                hud.SetObjective($"HOLD THE LZ · 0:{Mathf.CeilToInt(Mathf.Max(0, ExtractionLeft)):00}");
                if (ExtractionLeft <= 0)
                {
                    ObjectivesDone = Objectives;
                    CurrentPhase = Phase.Ended;
                    gm.MissionComplete("EXTRACTION COMPLETE");
                    return;
                }
            }

            // Field medic: +4 HP every 1.2 s.
            regenTimer += dt;
            if (regenTimer >= 1.2f)
            {
                regenTimer -= 1.2f;
                gm.player.Heal(4);
            }

            spawnTimer -= dt;
            if (spawnTimer <= 0)
            {
                spawnTimer = 1.2f;
                SpawnTick();
            }
            UpdateWarlord();
            AnimateRings();
            hud.SetStrikes(strikes);
            hud.PointAt(ObjectivePosition(), gm.player.cam);
        }

        // Presence-based hold (the e3bf116 soft-lock fix): standing in the ring builds
        // progress, leaving bleeds it back 1.5x faster. Enemies can't freeze it.
        void UpdateCapture(float dt)
        {
            var gm = GameManager.Instance;
            Vector3 d = gm.player.transform.position - Beacons[beacon];
            d.y = 0;
            bool inRing = d.magnitude < BeaconRadius;
            Capture = inRing ? Capture + dt : Mathf.Max(0, Capture - dt * 1.5f);
            if (Capture >= CaptureTime)
            {
                SecureBeacon();
                return;
            }
            hud.SetCapture(Capture > 0 || inRing ? Capture / CaptureTime : (float?)null);
        }

        void SecureBeacon()
        {
            var gm = GameManager.Instance;
            hud.SetCapture(null);
            gm.hud.Banner($"BEACON {BeaconNames[beacon]} SECURED", 2f, Green);
            gm.PlaySound(strikeReady, 0.6f);
            strikes.AddAirCharges(2); // rearm at the objective
            Effects.Instance.Blast(Beacons[beacon], 60 * PX, Green, 0.2f);
            beaconRings[beacon].gameObject.SetActive(false);
            ObjectivesDone++;
            if (CurrentPhase == Phase.Push) EnterAdvance();
            else EnterWarlord();
        }

        public void OnEnemyKilled(Enemy e)
        {
            if (e != Warlord) return;
            Warlord = null;
            GameManager.Instance.hud.SetBoss(0, 1);
            Effects.Instance.cameraFollow.AddShake(0.8f);
            ObjectivesDone++;
            if (CurrentPhase == Phase.Warlord) EnterExtraction();
        }

        // The Warlord pounds every 6 s (telegraphed ring, then a knockback shockwave)
        // and calls three grunts every 8 s.
        void UpdateWarlord()
        {
            var gm = GameManager.Instance;
            if (Warlord == null || Warlord.IsDying) return;
            gm.hud.SetBoss(Warlord.Health, Warlord.Stats.MaxHealth);
            float r = 200 * PX;
            if (poundTell >= 0)
            {
                poundTell += Time.deltaTime;
                float k = Mathf.Clamp01(poundTell / 0.7f);
                float eased = 1 - Mathf.Pow(1 - k, 3);
                float size = r * 2 * Mathf.Lerp(0.4f, 1, eased);
                poundRing.transform.position = new Vector3(Warlord.transform.position.x, 0.05f, Warlord.transform.position.z);
                poundRing.transform.localScale = new Vector3(size, size, 1);
                if (k >= 1)
                {
                    poundTell = -1;
                    poundRing.enabled = false;
                    Pound(r);
                }
            }
            else if (Time.time >= poundAt)
            {
                poundAt = Time.time + 6;
                poundTell = 0;
                poundRing.enabled = true;
            }
            if (Time.time >= summonAt)
            {
                summonAt = Time.time + 8;
                if (Enemy.All.Count < 16)
                    for (int i = 0; i < 3; i++)
                        gm.SpawnEnemyAt(EnemyType.Grunt, Warlord.transform.position + new Vector3(Random.Range(-90, 90) * PX, 0, Random.Range(-90, 90) * PX));
            }
        }

        void Pound(float r)
        {
            var gm = GameManager.Instance;
            Vector3 w = Warlord.transform.position;
            Effects.Instance.Blast(w, r, GameConfig.Hex(0xff5a4a), 0.5f);
            Vector3 d = gm.player.transform.position - w;
            d.y = 0;
            if (d.magnitude < r && gm.player.TryHurt(18))
                gm.player.Knock((d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward) * 380 * PX);
        }

        // Clusters ahead of the Warden (toward the objective) during the pushes,
        // from any side during extraction.
        void SpawnTick()
        {
            int alive = Enemy.All.Count - (Warlord != null ? 1 : 0);
            if (CurrentPhase == Phase.Push || CurrentPhase == Phase.Advance)
            {
                int budget = CurrentPhase == Phase.Push ? 14 : 24;
                int maxAlive = CurrentPhase == Phase.Push ? 10 : 14;
                if (spawnedThisPhase >= budget || alive >= maxAlive) return;
                Vector3 p = GameManager.Instance.player.transform.position;
                Vector3 dir = Beacons[beacon] - p;
                dir.y = 0;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
                SpawnCluster(p + dir * Random.Range(650, 880) * PX, Random.Range(3, 6));
            }
            else if (CurrentPhase == Phase.Extraction)
            {
                if (spawnedThisPhase >= 30 || alive >= 16) return;
                float a = Random.value * Mathf.PI * 2;
                Vector3 p = GameManager.Instance.player.transform.position;
                SpawnCluster(p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Random.Range(700, 900) * PX, Random.Range(3, 6));
            }
        }

        void SpawnCluster(Vector3 c, int n)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 p = c + new Vector3(Random.Range(-120, 120) * PX, 0, Random.Range(-100, 100) * PX);
                p.x = Mathf.Clamp(p.x, -WorldW / 2 + 50 * PX, WorldW / 2 - 50 * PX);
                p.z = Mathf.Clamp(p.z, -WorldD / 2 + 50 * PX, WorldD / 2 - 90 * PX);
                GameManager.Instance.SpawnEnemyAt(PickType(), p);
                spawnedThisPhase++;
            }
        }

        EnemyType PickType()
        {
            float r = Random.value;
            if (CurrentPhase == Phase.Push) return r < 0.7f ? EnemyType.Grunt : EnemyType.Runner;
            if (CurrentPhase == Phase.Advance)
            {
                if (r < 0.4f) return EnemyType.Grunt;
                if (r < 0.65f) return EnemyType.Runner;
                if (r < 0.85f) return EnemyType.Brute;
                return EnemyType.Spitter;
            }
            if (r < 0.3f) return EnemyType.Grunt;
            if (r < 0.5f) return EnemyType.Runner;
            if (r < 0.68f) return EnemyType.Brute;
            if (r < 0.84f) return EnemyType.Spitter;
            return EnemyType.Tank;
        }

        public Vector3? ObjectivePosition()
        {
            switch (CurrentPhase)
            {
                case Phase.Push: return Beacons[0];
                case Phase.Advance: return Beacons[1];
                case Phase.Warlord: return Warlord != null ? Warlord.transform.position : (Vector3?)null;
                case Phase.Extraction: return Beacons[2];
                default: return null;
            }
        }

        // Beacon rings breathe between 90% and 100% of their radius.
        void AnimateRings()
        {
            float k = 0.95f + 0.05f * Mathf.Sin(Time.time * Mathf.PI / 0.9f);
            foreach (var ring in beaconRings)
                ring.transform.localScale = new Vector3(BeaconRadius * 2 * k, BeaconRadius * 2 * k, 1);
        }
    }
}
