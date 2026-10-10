using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WardenZero
{
    // Full-screen overlays from the Babylon index.html: main menu, upgrade picker,
    // pause and the result screen. Buttons call into GameManager.
    public class Menus : MonoBehaviour
    {
        public GameObject menuPanel;
        public GameObject upgradePanel;
        public GameObject pausePanel;
        public GameObject resultPanel;

        public Button playButton;
        public Button resumeButton;
        public Button pauseMenuButton;
        public Button retryButton;
        public Button resultMenuButton;

        [Header("Controls list (swapped for the touch scheme)")]
        public Text[] controlKeys;
        public Text[] controlDescs;

        [Header("Upgrade cards")]
        public Text upgradeLevel;
        public Button[] cards;
        public Text[] cardTitles;
        public Text[] cardDescriptions;
        public Text[] cardStacks;

        [Header("Result")]
        public Text resultEyebrow;
        public Text resultTitle;
        public Text resultWave;
        public Text resultScore;
        public Text resultKills;
        public Text resultLevel;
        public Text resultTime;

        void Start()
        {
            var gm = GameManager.Instance;
            playButton.onClick.AddListener(gm.StartRun);
            retryButton.onClick.AddListener(gm.StartRun);
            resumeButton.onClick.AddListener(() => gm.Pause(false));
            pauseMenuButton.onClick.AddListener(gm.EnterMenu);
            resultMenuButton.onClick.AddListener(gm.EnterMenu);
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                cards[i].onClick.AddListener(() => gm.PickUpgrade(index));
            }
        }

        public void HideAll()
        {
            menuPanel.SetActive(false);
            upgradePanel.SetActive(false);
            pausePanel.SetActive(false);
            resultPanel.SetActive(false);
        }

        public void ShowMenu()
        {
            HideAll();
            if (TouchControls.Active) ShowTouchControls();
            menuPanel.SetActive(true);
        }

        // Babylon main.ts rewrites the controls list on touch devices.
        void ShowTouchControls()
        {
            string[,] rows = { { "Left thumb", "Drag anywhere to move" }, { "Auto", "Aims and fires at the nearest enemy" }, { "Dash · Bomb", "Buttons on the right" } };
            for (int i = 0; i < controlKeys.Length; i++)
            {
                bool used = i < rows.GetLength(0);
                controlKeys[i].text = used ? rows[i, 0] : "";
                controlDescs[i].text = used ? rows[i, 1] : "";
            }
        }

        public void ShowPause(bool on)
        {
            pausePanel.SetActive(on);
        }

        public void ShowUpgrades(RunState run, List<Upgrade> choices)
        {
            upgradeLevel.text = $"LEVEL {run.Level}";
            for (int i = 0; i < cards.Length; i++)
            {
                bool used = i < choices.Count;
                cards[i].gameObject.SetActive(used);
                if (!used) continue;
                var u = choices[i];
                cardTitles[i].text = u.Title;
                cardDescriptions[i].text = u.Description;
                cardStacks[i].text = $"{Upgrades.Stacks(run, u.Id)}/{u.MaxStacks}";
            }
            upgradePanel.SetActive(true);
        }

        public void HideUpgrades()
        {
            upgradePanel.SetActive(false);
        }

        public void ShowResult(bool win, RunState run)
        {
            resultEyebrow.text = win ? "VICTORY" : "RUN OVER";
            resultTitle.text = win ? "The Colossus falls" : "The line broke";
            resultWave.text = run.Wave.ToString();
            resultScore.text = run.Score.ToString("N0");
            resultKills.text = run.Kills.ToString();
            resultLevel.text = run.Level.ToString();
            int sec = Mathf.FloorToInt(run.Seconds);
            resultTime.text = $"{sec / 60}:{sec % 60:00}";
            resultPanel.SetActive(true);
        }
    }
}
