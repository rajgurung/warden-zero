using UnityEngine;
using UnityEngine.UI;

namespace WardenZero
{
    // Health, wave, score, dash chip and centre banner.
    // Layout and colours follow the Babylon DOM HUD (index.html + src/style.css).
    public class Hud : MonoBehaviour
    {
        public RectTransform healthFill;
        public Text healthText;
        public Text waveText;
        public Text scoreText;
        public Text dashText;
        public Image dashBar;
        public Text bannerText;
        public Outline bannerGlow;

        float bannerUntil = -1;

        public void SetHealth(float hp, float max)
        {
            healthFill.anchorMax = new Vector2(Mathf.Clamp01(hp / max), 1);
            healthText.text = $"HEALTH   <color=#e6ecff><b>{Mathf.CeilToInt(hp)} / {max}</b></color>";
        }

        public void SetWave(int wave, int total)
        {
            waveText.text = $"{wave}<size=15><color=#8a96b8> / {total}</color></size>";
        }

        public void SetScore(int score)
        {
            scoreText.text = score.ToString();
        }

        public void SetDash(float ready)
        {
            var rt = dashBar.rectTransform;
            rt.anchorMax = new Vector2(ready, rt.anchorMax.y);
            bool cooling = ready < 1;
            dashText.text = cooling ? "<color=#8a96b8><b>DASH</b></color>  <size=11><color=#8a96b8>Space</color></size>"
                                    : "<color=#4fd1ff><b>DASH</b></color>  <size=11><color=#8a96b8>Space</color></size>";
            dashBar.color = cooling ? GameConfig.TextDim : GameConfig.Accent;
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

        void Update()
        {
            if (bannerText.enabled && Time.time > bannerUntil) bannerText.enabled = false;
        }
    }
}
