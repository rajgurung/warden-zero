using UnityEngine;
using UnityEngine.UI;

namespace WardenZero
{
    // In-run HUD: health, wave, score and coins, ability chips, XP bar, boss bar and the
    // centre banner. Layout and colours follow the Babylon DOM HUD (index.html, src/style.css).
    public class Hud : MonoBehaviour
    {
        public RectTransform healthFill;
        public Text healthText;
        public Text waveText;
        public Text scoreText;
        public Text coinsText;
        public Text dashText;
        public Image dashBar;
        public Text bombText;
        public Image bombBar;
        public RectTransform xpFill;
        public Text levelText;
        public GameObject bossBar;
        public RectTransform bossFill;
        public Text bannerText;
        public Outline bannerGlow;
        public TouchControls touch;
        public RectTransform reticle;
        public GameObject lookHint; // behind view on desktop: click to lock the pointer

        float bannerUntil = -1;
        // Last values shown, so per-frame calls only rebuild text when something changed.
        int shownHp = -1;
        float shownMax = -1;
        int shownDash = -1, shownBomb = -1;

        public void SetHealth(float hp, float max)
        {
            healthFill.anchorMax = new Vector2(Mathf.Clamp01(hp / max), 1);
            int shown = Mathf.Max(0, Mathf.CeilToInt(hp));
            if (shown == shownHp && max == shownMax) return;
            shownHp = shown;
            shownMax = max;
            healthText.text = $"HEALTH   <color=#e6ecff><b>{shown} / {max}</b></color>";
        }

        public void SetWave(int wave, int total)
        {
            waveText.text = $"{wave}<size=15><color=#8a96b8> / {total}</color></size>";
        }

        public void SetScore(int score, int coins)
        {
            scoreText.text = score.ToString("N0");
            coinsText.text = coins.ToString();
        }

        public void SetXp(int xp, int need, int level)
        {
            xpFill.anchorMax = new Vector2(Mathf.Clamp01((float)xp / need), 1);
            levelText.text = $"LV {level}";
        }

        public void SetBoss(float hp, float max)
        {
            bossBar.SetActive(hp > 0);
            if (hp > 0) bossFill.anchorMax = new Vector2(Mathf.Clamp01(hp / max), 1);
        }

        // 0..1 readiness of each ability.
        public void SetCooldowns(float dash, float bomb)
        {
            Chip(dashText, dashBar, "DASH", "Space", dash, ref shownDash);
            Chip(bombText, bombBar, "BOMB", "E / RMB", bomb, ref shownBomb);
        }

        static void Chip(Text text, Image bar, string name, string key, float ready, ref int shownState)
        {
            var rt = bar.rectTransform;
            rt.anchorMax = new Vector2(ready, rt.anchorMax.y);
            bool cooling = ready < 1;
            int state = cooling ? 0 : 1;
            if (state == shownState) return;
            shownState = state;
            string colour = cooling ? "#8a96b8" : "#4fd1ff";
            text.text = $"<color={colour}><b>{name}</b></color>  <size=11><color=#8a96b8>{key}</color></size>";
            bar.color = cooling ? GameConfig.TextDim : GameConfig.Accent;
        }

        // seconds < 0 keeps the banner up until replaced. Danger banners are red.
        public void Banner(string text, float seconds, bool danger = false)
        {
            bannerText.text = text;
            bannerText.enabled = true;
            bannerText.color = danger ? GameConfig.Health : GameConfig.TextBright;
            var glow = danger ? GameConfig.Health : GameConfig.Accent;
            bannerGlow.effectColor = new Color(glow.r, glow.g, glow.b, 0.2f);
            bannerUntil = seconds < 0 ? float.MaxValue : Time.time + seconds;
        }

        // A banner in a mission colour (Greenfang's green, gold and magenta callouts).
        public void Banner(string text, float seconds, Color color)
        {
            Banner(text, seconds);
            bannerText.color = color;
            bannerGlow.effectColor = new Color(color.r, color.g, color.b, 0.2f);
        }

        public bool BannerShowing(string text) => bannerText.enabled && bannerText.text == text;

        public void SetLookHint(bool on)
        {
            if (lookHint.activeSelf != on) lookHint.SetActive(on);
        }

        void OnEnable()
        {
            bannerText.enabled = false;
        }

        void Update()
        {
            if (bannerText.enabled && Time.time > bannerUntil) bannerText.enabled = false;
        }
    }
}
