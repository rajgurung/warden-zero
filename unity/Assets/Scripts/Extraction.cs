using Unity.Cinemachine;
using UnityEngine;

namespace WardenZero
{
    // Stage 1's ending in the campaign. After the Colossus: "Extraction inbound", the chopper
    // flies in and lands at the LZ; the Warden holds the LZ for HoldTime (the clock only runs
    // while he is inside the ring) while the horde keeps coming; then he walks into the cabin
    // door (or taps BOARD) to board. The door gunner clears the LZ, the run is saved, and the
    // chopper lifts off under a Cinemachine camera while the jungle loads.
    public class Extraction : MonoBehaviour
    {
        public enum Phase { Idle, Inbound, Hold, Board, LiftOff }

        public const float HoldTime = 20;
        public const float LzRadius = 11;
        public const float BoardRadius = 2.4f;
        public const float InboundTime = 9;
        public const float LiftOffTime = 5;
        // Open deck east of the walls; the chopper lands nose west, cabin door to the south
        // (toward the camera).
        public static readonly Vector3 Lz = new Vector3(40, 0, -20);
        public const float LzHeading = -90;

        public Chopper chopper;
        public MissionHud hud;
        public Renderer lzRing;
        public Renderer doorMarker;
        public CinemachineBrain brain;
        public CinemachineCamera snapCam; // copies the gameplay view, so the blend starts from it
        public CinemachineCamera liftCam;
        public AudioClip alert;

        public Phase CurrentPhase { get; private set; }
        public float HoldLeft { get; private set; }
        public bool Running => CurrentPhase != Phase.Idle;
        public bool InLz { get; private set; }

        float phaseTimer;
        float spawnTimer;
        float gunTimer;
        int shownSeconds = -1;
        bool wasInLz;
        WallRect[] arenaWalls;

        void Awake()
        {
            Hide();
        }

        void Hide()
        {
            chopper.gameObject.SetActive(false);
            lzRing.gameObject.SetActive(false);
            doorMarker.gameObject.SetActive(false);
            brain.enabled = false;
            snapCam.gameObject.SetActive(false);
            liftCam.gameObject.SetActive(false);
        }

        public void ResetForRun()
        {
            CurrentPhase = Phase.Idle;
            StopAllCoroutines();
            var gm = GameManager.Instance;
            gm.player.transform.SetParent(null, true);
            gm.player.transform.rotation = Quaternion.identity;
            if (gm.player.view is WardenModelView v) v.SetXRay(true);
            gm.cameraFollow.enabled = true;
            gm.hud.touch.HideAction();
            Hide();
            if (arenaWalls != null) World.UseArena();
            hud.SetObjective("");
            hud.SetCapture(null);
            hud.PointAt(null, null);
        }

        // The Colossus is down.
        public void Begin()
        {
            var gm = GameManager.Instance;
            CurrentPhase = Phase.Inbound;
            HoldLeft = HoldTime;
            phaseTimer = InboundTime;
            spawnTimer = 2;
            gm.hud.Banner("EXTRACTION INBOUND", 2.2f, GameConfig.Gold);
            gm.PlaySound(alert, 0.6f);
            hud.SetObjective("REACH THE LZ");
            lzRing.gameObject.SetActive(true);
            lzRing.transform.position = Lz + Vector3.up * 0.03f;
            lzRing.transform.localScale = new Vector3(LzRadius * 2, LzRadius * 2, 1);
            // In from the north-east, high, then down onto the pad.
            chopper.gameObject.SetActive(true);
            chopper.spin = 1;
            chopper.Place(Lz + new Vector3(90, 45, 80), -130);
            chopper.FlyTo(Lz + new Vector3(0, 14, 0), LzHeading, InboundTime * 0.65f);
        }

        public void Tick(float dt)
        {
            var gm = GameManager.Instance;
            Vector3 p = gm.player.transform.position;
            InLz = Flat(p - Lz).magnitude < LzRadius;

            switch (CurrentPhase)
            {
                case Phase.Inbound:
                    phaseTimer -= dt;
                    if (phaseTimer < InboundTime * 0.35f && !chopper.Moving && chopper.transform.position.y > 1)
                        chopper.FlyTo(Lz, LzHeading, InboundTime * 0.35f);
                    if (phaseTimer <= 0) Land();
                    break;
                case Phase.Hold:
                    if (InLz) HoldLeft = Mathf.Max(0, HoldLeft - dt);
                    int seconds = Mathf.CeilToInt(HoldLeft);
                    if (seconds != shownSeconds || InLz != wasInLz)
                    {
                        shownSeconds = seconds;
                        wasInLz = InLz;
                        hud.SetObjective(InLz ? $"HOLD THE LZ · 0:{seconds:00}" : $"RETURN TO THE LZ · 0:{seconds:00}");
                    }
                    hud.SetCapture(1 - HoldLeft / HoldTime);
                    if (HoldLeft <= 0) OpenDoor();
                    break;
                case Phase.Board:
                    if (Flat(p - chopper.door.position).magnitude < BoardRadius || gm.hud.touch.ConsumeAction()) Board();
                    break;
            }

            if (CurrentPhase == Phase.Inbound || CurrentPhase == Phase.Hold) SpawnTick(dt);
            if (CurrentPhase == Phase.Hold || CurrentPhase == Phase.Board) DoorGun(dt);
            if (lzRing.gameObject.activeSelf)
                lzRing.transform.Rotate(0, 0, dt * 12, Space.Self);
            Vector3? target = CurrentPhase == Phase.Board ? chopper.door.position : InLz ? (Vector3?)null : Lz;
            hud.PointAt(target, gm.player.cam);
        }

        void Land()
        {
            var gm = GameManager.Instance;
            CurrentPhase = Phase.Hold;
            chopper.Place(Lz, LzHeading);
            chopper.spin = 0.85f;
            // The fuselage blocks the Warden and the horde while it sits on the pad.
            arenaWalls = World.Walls;
            var walls = new WallRect[arenaWalls.Length + 1];
            arenaWalls.CopyTo(walls, 0);
            walls[arenaWalls.Length] = new WallRect { MinX = Lz.x - 7.5f, MaxX = Lz.x + 7.5f, MinZ = Lz.z - 1.6f, MaxZ = Lz.z + 1.6f };
            World.Use(World.HalfW, World.HalfD, walls, World.Trunks);
            gm.hud.Banner("HOLD THE LZ", 1.8f, GameConfig.Gold);
            gm.cameraFollow.AddShake(0.25f);
        }

        void OpenDoor()
        {
            var gm = GameManager.Instance;
            CurrentPhase = Phase.Board;
            hud.SetCapture(null);
            hud.SetObjective("BOARD THE CHOPPER");
            gm.hud.Banner("BOARD THE CHOPPER", 2f, GameConfig.Accent);
            gm.PlaySound(alert, 0.6f);
            lzRing.gameObject.SetActive(false);
            doorMarker.gameObject.SetActive(true);
            doorMarker.transform.position = new Vector3(chopper.door.position.x, 0.04f, chopper.door.position.z);
            gm.hud.touch.ShowAction("BOARD");
        }

        // Walked into the door: the gunner sweeps the LZ, the run is saved, and off we go.
        public void Board()
        {
            var gm = GameManager.Instance;
            gm.hud.touch.HideAction();
            foreach (var e in Enemy.All.ToArray()) e.TakeHit(1e6f);
            doorMarker.gameObject.SetActive(false);
            hud.SetObjective("");
            hud.PointAt(null, null);
            Campaign.Run = gm.Run;
            Campaign.Save(gm.Run, CampaignSave.AfterArena);
            CurrentPhase = Phase.LiftOff;
            gm.BeginCinematic();

            // The Warden rides in the cabin (no see-through silhouette through the hull).
            ((WardenModelView)gm.player.view).SetXRay(false);
            var warden = gm.player.transform;
            warden.SetParent(chopper.seat, false);
            warden.localPosition = Vector3.zero;
            warden.localRotation = Quaternion.identity;

            // Blend from the gameplay view to a chase camera on the chopper.
            snapCam.transform.SetPositionAndRotation(gm.player.cam.transform.position, gm.player.cam.transform.rotation);
            snapCam.Lens = LensSettings.FromCamera(gm.player.cam);
            snapCam.gameObject.SetActive(true);
            gm.cameraFollow.enabled = false;
            brain.enabled = true;
            liftCam.gameObject.SetActive(true);

            World.UseArena();
            chopper.spin = 1;
            chopper.FlyTo(Lz + new Vector3(0, 12, 0), LzHeading, 2.2f);
            phaseTimer = 0;
            StartCoroutine(LiftOff());
        }

        System.Collections.IEnumerator LiftOff()
        {
            yield return new WaitForSeconds(2.2f);
            // Nose over and away to the west, climbing.
            chopper.FlyTo(Lz + new Vector3(-160, 70, 40), -75, 6);
            yield return new WaitForSeconds(LiftOffTime - 2.2f);
            StageLoader.LoadJungle();
        }

        void SpawnTick(float dt)
        {
            spawnTimer -= dt;
            if (spawnTimer > 0) return;
            spawnTimer = 1.6f;
            var gm = GameManager.Instance;
            if (Enemy.All.Count >= 24) return;
            EnemyType[] mix = { EnemyType.Grunt, EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Runner, EnemyType.Spider, EnemyType.Skeleton };
            for (int i = 0; i < 2; i++) gm.SpawnEnemy(mix[Random.Range(0, mix.Length)]);
        }

        // Covering fire from the cabin door: a bolt at the nearest enemy on the door's side
        // (the hull blocks the other side).
        void DoorGun(float dt)
        {
            gunTimer -= dt;
            if (gunTimer > 0) return;
            gunTimer = 0.3f;
            Vector3 outward = Flat(-chopper.transform.right).normalized;
            Vector3 from = Flat(chopper.door.position) + outward * 1.2f + Vector3.up * GameConfig.AimHeight;
            Enemy target = null;
            float best = 20 * 20;
            foreach (var e in Enemy.All)
            {
                if (e.IsDying) continue;
                Vector3 to = Flat(e.transform.position - from);
                if (Vector3.Dot(to, outward) < 0.5f) continue;
                if (to.sqrMagnitude < best) { best = to.sqrMagnitude; target = e; }
            }
            if (target == null) return;
            var gm = GameManager.Instance;
            Bolt.Spawn(gm.player.boltPrefab, from).Launch(Flat(target.transform.position - from).normalized, 34, 35, false, 1.2f);
            gm.PlaySound(gm.shootSound, 0.15f, GameManager.Detune(300));
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
