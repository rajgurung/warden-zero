using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        public Button campaignButton;
        public Button continueButton;
        public Button playButton; // the standalone arena
        public Button greenfangButton;
        public Button qualityButton; // pause menu: High / Low
        public Text qualityLabel;
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
        public Text resultWaveLabel;
        public Text resultWave;
        public Text resultScore;
        public Text resultKills;
        public Text resultLevel;
        public Text resultTime;

        void Start()
        {
            var gm = GameManager.Instance;
            campaignButton.onClick.AddListener(gm.StartCampaign);
            continueButton.onClick.AddListener(() => Campaign.Continue());
            playButton.onClick.AddListener(gm.StartRun);
            qualityButton.onClick.AddListener(() =>
                Quality.Choose(Quality.Current == QualityTier.High ? QualityTier.Low : QualityTier.High));
            Quality.Changed += ShowQuality;
            ShowQuality();
            greenfangButton.onClick.AddListener(() => SceneManager.LoadScene("Greenfang"));
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

        void OnDestroy()
        {
            Quality.Changed -= ShowQuality;
        }

        void ShowQuality()
        {
            qualityLabel.text = $"QUALITY · {Quality.Current.ToString().ToUpperInvariant()}";
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
            bool saved = CampaignSave.Exists;
            continueButton.interactable = saved;
            continueButton.GetComponentInChildren<Text>().color = saved ? GameConfig.TextBright : GameConfig.PanelEdge;
            menuPanel.SetActive(true);
        }

        // Babylon main.ts rewrites the controls list on touch devices.
        void ShowTouchControls()
        {
            string[,] rows =
            {
                { "Left thumb", "Drag to move" }, { "Auto", "Aims and fires at the nearest enemy" }, { "Dash · Bomb", "Buttons on the right" },
                { "VIEW", "Behind view: right thumb looks, FIRE shoots" },
            };
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

        // Greenfang's result: objectives instead of the wave.
        public void ShowMissionResult(bool win, RunState run, int objectives, int total)
        {
            ShowResult(win, run);
            resultEyebrow.text = win ? "MISSION COMPLETE" : "MISSION FAILED";
            resultTitle.text = win ? "The sector belongs to the Warden" : "The Warden fell in the green";
            resultWaveLabel.text = "OBJ";
            resultWave.text = $"{objectives}/{total}";
        }

        // The drop slice's end: checkpoint A is saved; back to the menu from here.
        public void ShowMilestoneResult(RunState run)
        {
            ShowResult(true, run);
            resultEyebrow.text = "MILESTONE 1 COMPLETE";
            resultTitle.text = "Checkpoint A secured · saved";
            resultWaveLabel.text = "STAGE";
            resultWave.text = "2";
            retryButton.gameObject.SetActive(false);
        }

        public void ShowResult(bool win, RunState run)
        {
            retryButton.gameObject.SetActive(true);
            resultWaveLabel.text = "WAVE";
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
