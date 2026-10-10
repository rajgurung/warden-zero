using UnityEngine;
using UnityEngine.UI;

namespace WardenZero
{
    // Health bar, wave, score, dash state and a centre banner.
    public class Hud : MonoBehaviour
    {
        public RectTransform healthFill;
        public Text healthText;
        public Text waveText;
        public Text scoreText;
        public Text dashText;
        public Text bannerText;

        float bannerUntil = -1;

        public void SetHealth(float hp, float max)
        {
            healthFill.anchorMax = new Vector2(Mathf.Clamp01(hp / max), 1);
            healthText.text = $"{Mathf.CeilToInt(hp)} / {max}";
        }

        public void SetWave(int wave, int total)
        {
            waveText.text = $"WAVE {wave}/{total}";
        }

        public void SetScore(int score)
        {
            scoreText.text = $"SCORE {score}";
        }

        public void SetDash(float ready)
        {
            dashText.text = ready >= 1 ? "DASH READY  [SPACE]" : $"DASH {Mathf.RoundToInt(ready * 100)}%";
            dashText.color = ready >= 1 ? GameConfig.Accent : GameConfig.TextDim;
        }

        // seconds < 0 keeps the banner up until replaced.
        public void Banner(string text, float seconds)
        {
            bannerText.text = text;
            bannerText.enabled = true;
            bannerUntil = seconds < 0 ? float.MaxValue : Time.time + seconds;
        }

        void Update()
        {
            if (bannerText.enabled && Time.time > bannerUntil) bannerText.enabled = false;
        }
    }
}
