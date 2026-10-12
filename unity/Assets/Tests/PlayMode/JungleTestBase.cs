using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace WardenZero.Tests
{
    // Loads the jungle the way the game does (Addressables), at a given start.
    public abstract class JungleTestBase : ArenaTestBase
    {
        protected static JungleStage Stage => Object.FindFirstObjectByType<JungleStage>();

        protected static IEnumerator LoadJungle(DropStart start, RunState run = null)
        {
            // From the arena, so a jungle left over from another test can't pass for this one.
            yield return LoadMenu();
            Campaign.Active = true;
            Campaign.Run = run ?? new RunState();
            Campaign.Start = start;
            StageLoader.LoadJungle();
            yield return WaitFor(() => Stage != null && GameManager.Instance != null && GameManager.Instance.jungle == Stage, 60, "the jungle scene");
            yield return Frames(3);
        }

        protected static IEnumerator WaitFor(System.Func<bool> condition, float seconds, string what, System.Func<string> state = null)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what + (state != null ? " (" + state() + ")" : ""));
                yield return null;
            }
        }
    }
}
