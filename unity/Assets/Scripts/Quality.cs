using UnityEngine;

namespace WardenZero
{
    public enum QualityTier { Low, High }

    // Two quality levels: High for desktops, Low for phones and tablets (a coarse pointer).
    // A choice made in the pause menu is remembered and wins over the guess.
    // The levels themselves (render scale, MSAA, shadows) live in the URP assets that
    // SceneBuilder.ConfigurePipeline sets up; scenes read Current for their own extras
    // (vegetation density, draw distance, post-processing).
    public static class Quality
    {
        public const string PrefKey = "wz.quality";

        public static QualityTier Current { get; private set; } = QualityTier.High;
        public static event System.Action Changed;

        // saved: -1 = no choice made, otherwise the tier's number.
        public static QualityTier Pick(bool touch, int saved)
        {
            if (saved == (int)QualityTier.Low || saved == (int)QualityTier.High) return (QualityTier)saved;
            return touch ? QualityTier.Low : QualityTier.High;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Apply(Pick(TouchControls.Active, PlayerPrefs.GetInt(PrefKey, -1)));
        }

        // The pause menu's override: apply and remember.
        public static void Choose(QualityTier tier)
        {
            PlayerPrefs.SetInt(PrefKey, (int)tier);
            PlayerPrefs.Save();
            Apply(tier);
        }

        public static void Apply(QualityTier tier)
        {
            Current = tier;
            int level = System.Array.IndexOf(QualitySettings.names, tier.ToString());
            if (level >= 0) QualitySettings.SetQualityLevel(level, true);
            Changed?.Invoke();
        }
    }
}
