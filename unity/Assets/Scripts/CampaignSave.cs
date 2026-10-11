using System.Collections.Generic;
using UnityEngine;

namespace WardenZero
{
    // A campaign checkpoint in the browser (PlayerPrefs, which WebGL keeps in IndexedDB):
    // where the run is, and the Warden's health, level, upgrades and stats.
    // Upgrades are stored by id and replayed onto fresh stats when loading, so the stats
    // always match the upgrade rules.
    [System.Serializable]
    public class CampaignSave
    {
        public const string Key = "wz.campaign";
        public const int CurrentVersion = 1;

        // Places a save can resume from.
        public const string AfterArena = "flight"; // boarded the chopper; resumes at the flight
        public const string CheckpointA = "checkpointA";

        public int Version = CurrentVersion;
        public string Place;
        public float Health;
        public int Level;
        public int Xp;
        public int Score;
        public int Coins;
        public int Kills;
        public int Wave;
        public float Seconds;
        public List<string> Upgrades = new List<string>();

        public static CampaignSave From(RunState run, string place)
        {
            return new CampaignSave
            {
                Place = place,
                Health = run.Stats.Health,
                Level = run.Level,
                Xp = run.Xp,
                Score = run.Score,
                Coins = run.Coins,
                Kills = run.Kills,
                Wave = run.Wave,
                Seconds = run.Seconds,
                Upgrades = new List<string>(run.Upgrades),
            };
        }

        public RunState ToRun()
        {
            var run = new RunState();
            foreach (var id in Upgrades) WardenZero.Upgrades.Apply(run, id);
            run.Level = Mathf.Max(1, Level);
            run.XpToNext = RunState.XpFor(run.Level);
            run.Xp = Xp;
            run.Score = Score;
            run.Coins = Coins;
            run.Kills = Kills;
            run.Wave = Wave;
            run.Seconds = Seconds;
            run.Stats.Health = Mathf.Clamp(Health, 1, run.Stats.MaxHealth);
            return run;
        }

        public static bool Exists => Load() != null;

        public static void Store(CampaignSave save)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        // Null when there is no save, or it is unreadable or from another version.
        public static CampaignSave Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            if (json.Length == 0) return null;
            try
            {
                var save = JsonUtility.FromJson<CampaignSave>(json);
                bool known = save != null && (save.Place == AfterArena || save.Place == CheckpointA);
                return known && save.Version == CurrentVersion ? save : null;
            }
            catch (System.ArgumentException)
            {
                return null;
            }
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
