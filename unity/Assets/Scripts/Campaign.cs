using System.Text.RegularExpressions;
using UnityEngine;

namespace WardenZero
{
    // Where the jungle scene starts when it loads.
    public enum DropStart { Flight, Jump, Landed, NearCheckpoint, CheckpointA }

    // The campaign across scenes: Stage 1 (the arena) -> extraction -> flight and jump ->
    // the jungle. Level, upgrades and stats travel in Run from scene to scene.
    public static class Campaign
    {
        // A campaign run is in progress (set from the menu's Campaign and Continue).
        public static bool Active;
        public static RunState Run;
        public static DropStart Start;

        public static void Begin()
        {
            Active = true;
            Run = null;
            Start = DropStart.Flight;
            StageLoader.Preload();
        }

        public static void End()
        {
            Active = false;
            Run = null;
        }

        // Resume the save: the flight (after the arena) or checkpoint A.
        public static bool Continue()
        {
            var save = CampaignSave.Load();
            if (save == null) return false;
            Active = true;
            Run = save.ToRun();
            Start = save.Place == CampaignSave.CheckpointA ? DropStart.CheckpointA : DropStart.Flight;
            StageLoader.LoadJungle();
            return true;
        }

        public static void Save(RunState run, string place)
        {
            CampaignSave.Store(CampaignSave.From(run, place));
        }

        // Debug URL ?campaign=extraction|flight|jump|jungle|walk|checkpoint (null when absent).
        public static string DebugStart(string url)
        {
            var m = Regex.Match(url ?? "", @"[?&]campaign=(\w+)");
            return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
        }

        // The jungle start a debug value asks for; extraction stays in the arena (null).
        public static DropStart? DropFor(string debug)
        {
            switch (debug)
            {
                case "flight": return DropStart.Flight;
                case "jump": return DropStart.Jump;
                case "jungle": return DropStart.Landed;
                case "walk": return DropStart.NearCheckpoint;
                case "checkpoint": return DropStart.CheckpointA;
                default: return null;
            }
        }
    }
}
