using System.Collections;
using UnityEngine;

namespace WardenZero
{
    public enum StrikeType { Artillery, Air }

    // Call-in strikes from v1 (e3bf116:src/systems/StrikeSystem.ts): arm with Q, call with
    // right-click. Telegraph, tension delay, then a barrage that launches the crowd. Every
    // impact also hurts the Warden if he stands in it (unless dashing).
    public class StrikeSystem : MonoBehaviour
    {
        public const int AirMaxCharges = 4;
        public const float ImpactRadius = 170 * GameConfig.PX;
        public const float ImpactDamage = 420;
        public const float SelfDamage = 25;
        public static readonly float[] Cooldowns = { 7, 11 };
        public static readonly string[] Labels = { "ARTILLERY", "AIR STRIKE" };
        public static readonly Color[] Colors = { GameConfig.Gold, GameConfig.Accent };

        public Renderer zoneRing; // artillery target ring (gold)
        public Transform airLine; // air-strike telegraph strip
        public Transform jet;
        public SpriteRenderer scorchPrefab;
        public AudioClip shellWhistle;
        public AudioClip jetPass;
        public AudioClip strikeReady;

        public StrikeType Armed { get; private set; }
        public int AirCharges { get; private set; } = AirMaxCharges;

        readonly float[] readyAt = new float[2];
        readonly bool[] wasReady = { true, true };
        int killTally;
        float tallyAt = -1;

        public void ResetForRun()
        {
            StopAllCoroutines();
            Armed = StrikeType.Artillery;
            AirCharges = AirMaxCharges;
            readyAt[0] = readyAt[1] = 0;
            wasReady[0] = wasReady[1] = true;
            zoneRing.enabled = false;
            airLine.gameObject.SetActive(false);
            jet.gameObject.SetActive(false);
            killTally = 0;
            tallyAt = -1;
        }

        public void AddAirCharges(int n)
        {
            AirCharges = Mathf.Clamp(AirCharges + n, 0, AirMaxCharges);
        }

        public bool IsReady(StrikeType t) => Time.time >= readyAt[(int)t];

        public bool CanFire(StrikeType t) => IsReady(t) && (t != StrikeType.Air || AirCharges > 0);

        // 0..1 progress back to ready.
        public float Cooldown(StrikeType t)
        {
            float left = readyAt[(int)t] - Time.time;
            return left <= 0 ? 1 : Mathf.Clamp01(1 - left / Cooldowns[(int)t]);
        }

        public void Cycle()
        {
            Armed = Armed == StrikeType.Artillery ? StrikeType.Air : StrikeType.Artillery;
            GameManager.Instance.PlaySound(strikeReady, 0.4f);
        }

        // Fire the armed strike at target; origin (the Warden) sets the air-strike run direction.
        public bool Fire(Vector3 target, Vector3 origin) => Fire(Armed, target, origin);

        public bool Fire(StrikeType type, Vector3 target, Vector3 origin)
        {
            if (!CanFire(type)) return false;
            readyAt[(int)type] = Time.time + Cooldowns[(int)type];
            wasReady[(int)type] = false;
            target.y = 0;
            if (type == StrikeType.Air)
            {
                AirCharges -= 1;
                StartCoroutine(AirStrike(target, origin));
            }
            else
            {
                StartCoroutine(Artillery(target));
            }
            Effects.Instance.cameraFollow.AddShake(0.15f);
            var gm = GameManager.Instance;
            gm.hud.Banner(type == StrikeType.Artillery ? "FIRE MISSION · ARTILLERY" : "AIR SUPPORT INBOUND", 1.4f, Colors[(int)type]);
            return true;
        }

        void Update()
        {
            // Chime when a strike comes back off cooldown.
            for (int i = 0; i < 2; i++)
            {
                bool ready = IsReady((StrikeType)i);
                if (ready && !wasReady[i]) GameManager.Instance.PlaySound(strikeReady, 0.5f);
                wasReady[i] = ready;
            }
            if (tallyAt >= 0 && Time.time >= tallyAt)
            {
                GameManager.Instance.hud.Banner($"{killTally} ELIMINATED", 1.2f, GameConfig.Gold);
                killTally = 0;
                tallyAt = -1;
            }
            if (zoneRing.enabled)
            {
                // Pulsing target ring.
                float k = 0.9f + 0.1f * Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI / 0.24f));
                float size = ImpactRadius * 1.4f * 2 * k;
                zoneRing.transform.localScale = new Vector3(size, size, 1);
            }
        }

        // A cluster of six shells walking out from the centre, after a 2 s telegraph.
        IEnumerator Artillery(Vector3 c)
        {
            GameManager.Instance.PlaySound(shellWhistle, 0.7f);
            zoneRing.transform.position = new Vector3(c.x, 0.04f, c.z);
            zoneRing.enabled = true;
            yield return new WaitForSeconds(2);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2 + Random.value;
                float r = i == 0 ? 0 : Random.Range(40, 170) * GameConfig.PX;
                if (i == 0) zoneRing.enabled = false;
                Impact(c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), i == 0);
                yield return new WaitForSeconds(0.15f);
            }
        }

        // A jet strafes a 1400 px line through the target, from the Warden's side.
        IEnumerator AirStrike(Vector3 t, Vector3 o)
        {
            GameManager.Instance.PlaySound(shellWhistle, 0.35f);
            Vector3 dir = t - o;
            dir.y = 0;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            float half = 700 * GameConfig.PX;
            Vector3 s = t - dir * half, e = t + dir * half;
            airLine.position = new Vector3(t.x, 0.05f, t.z);
            airLine.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0);
            airLine.localScale = new Vector3(ImpactRadius * 1.3f, half * 2, 1);
            airLine.gameObject.SetActive(true);
            yield return new WaitForSeconds(1.5f);
            airLine.gameObject.SetActive(false);
            GameManager.Instance.PlaySound(jetPass, 0.6f);
            StartCoroutine(FlyJet(s, e, dir));
            yield return new WaitForSeconds(0.12f);
            for (int i = 0; i < 7; i++)
            {
                Impact(Vector3.Lerp(s, e, i / 6f), i == 3);
                yield return new WaitForSeconds(0.08f);
            }
        }

        IEnumerator FlyJet(Vector3 s, Vector3 e, Vector3 dir)
        {
            jet.rotation = Quaternion.LookRotation(dir);
            jet.gameObject.SetActive(true);
            for (float k = 0; k < 1; k += Time.deltaTime / 0.65f)
            {
                // Sine ease-in, like the v1 tween.
                float eased = 1 - Mathf.Cos(k * Mathf.PI / 2);
                jet.position = Vector3.Lerp(s, e, eased) + Vector3.up * 7;
                yield return null;
            }
            jet.gameObject.SetActive(false);
        }

        // One shell: blast FX, area damage that launches the crowd, friendly fire, scorch.
        void Impact(Vector3 p, bool big)
        {
            var gm = GameManager.Instance;
            if (!gm.RunActive) return;
            Effects.Instance.Blast(p, big ? ImpactRadius * 1.2f : ImpactRadius, GameConfig.Gold, big ? 0.55f : 0.3f);
            gm.PlaySound(gm.bombSound, big ? 0.6f : 0.35f, GameManager.Detune(300));
            int kills = 0;
            foreach (var e in Enemy.All.ToArray())
            {
                Vector3 d = e.transform.position - p;
                d.y = 0;
                if (d.magnitude > ImpactRadius) continue;
                e.KnockFrom(p);
                e.TakeHit(ImpactDamage);
                if (e.IsDying) kills++;
            }
            if (kills > 0)
            {
                killTally += kills;
                tallyAt = Time.time + 1.1f;
            }
            Vector3 w = gm.player.transform.position - p;
            w.y = 0;
            if (w.magnitude <= ImpactRadius) gm.player.TryHurt(SelfDamage);
            var scorch = Instantiate(scorchPrefab, new Vector3(p.x, 0.03f, p.z), scorchPrefab.transform.rotation);
            scorch.transform.localScale = Vector3.one * ImpactRadius * 2.2f;
        }
    }
}
