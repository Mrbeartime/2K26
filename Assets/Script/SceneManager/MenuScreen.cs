using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

namespace ProjectD.Menus
{
    /// <summary>Controls menu UI objects stored in the scene Hierarchy.</summary>
    public class MenuScreen : MonoBehaviour
    {
        public enum Page { Start, LevelSelect }

        [SerializeField] private Page page;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Text soundLabel;
        [SerializeField] private Text fullscreenLabel;
        [SerializeField] private Button[] levelButtons;
        [SerializeField] private Text[] levelLabels;
        [Header("Level details (scene objects)")]
        [SerializeField] private string[] levelDescriptions = new string[5];
        [SerializeField] private GameObject paperPanel;
        [SerializeField] private Text paperTitle;
        [SerializeField] private Text paperDescription;
        [SerializeField] private Button playButton;
        [SerializeField] private Button[] paperLevelButtons;
        [SerializeField] private Text[] paperLevelLabels;
        [SerializeField] private GameObject previousPageButton;
        [SerializeField] private GameObject nextPageButton;
        [SerializeField] private Text pageLabel;
        [SerializeField] private SecretLevel[] secretLevels = new SecretLevel[0];

        [Serializable]
        public class SecretLevel
        {
            public string id = "SecretLevel1";
            public string title = "Secret Level";
            [TextArea] public string description;
            public string scenePath;
            [Min(0)] public int requiredCompletedLevel = 5;
        }

        private enum PaperView { Closed, Completed, Secret, Details }
        private PaperView paperView;
        private PaperView detailReturnView;
        private readonly List<int> entries = new List<int>();
        private readonly int[] visibleLevels = new int[4];
        private int paperPage;
        private int selectedLevel;
        private SecretLevel selectedSecret;

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            GoBack();
        }

        public void GoBack()
        {
            if (settingsPanel != null && settingsPanel.activeSelf) CloseSettings();
            else if (paperView == PaperView.Details && detailReturnView != PaperView.Closed)
                OpenCollection(detailReturnView, false);
            else if (paperView != PaperView.Closed) ClosePaper();
            else if (page == Page.LevelSelect) OpenStart();
        }

        private void Awake()
        {
            Time.timeScale = 1f;
            if (mainPanel != null) mainPanel.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (paperPanel != null) paperPanel.SetActive(false);
            if (playButton != null) playButton.onClick.AddListener(PlaySelectedLevel);
            if (page == Page.LevelSelect)
            {
                for (int i = 0; levelButtons != null && i < levelButtons.Length; i++)
                {
                    int slot = i;
                    if (levelButtons[i] != null)
                    {
                        levelButtons[i].onClick.RemoveAllListeners();
                        levelButtons[i].onClick.AddListener(() => OpenLevelSlot(slot));
                    }
                }
                for (int i = 0; paperLevelButtons != null && i < paperLevelButtons.Length; i++)
                {
                    int slot = i;
                    if (paperLevelButtons[i] != null)
                        paperLevelButtons[i].onClick.AddListener(() => OpenPaperSlot(slot));
                }
            }
            RefreshUI();
        }

        public void OpenLevelSelect() => GameSceneManager.OpenLevelSelect();
        public void OpenStart() => GameSceneManager.OpenStart();
        public void ExitGame() => GameSceneManager.ExitGame();
        public void OpenLevel1() => ShowLevelDetails(1);
        public void OpenLevel2() => ShowLevelDetails(2);
        public void OpenLevel3() => ShowLevelDetails(3);
        public void OpenLevel4() => ShowLevelDetails(4);
        public void OpenLevel5() => ShowLevelDetails(5);

        public void OpenCompleted() => OpenCollection(PaperView.Completed, true);
        public void OpenSecret() => OpenCollection(PaperView.Secret, true);
        public void PreviousPaperPage() { paperPage = Mathf.Max(0, paperPage - 1); RefreshPaperCollection(); }
        public void NextPaperPage() { paperPage++; RefreshPaperCollection(); }

        public void ClosePaper()
        {
            paperView = PaperView.Closed;
            if (paperPanel != null) paperPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);
            RefreshUI();
        }

        private void OpenLevelSlot(int slot)
        {
            if (slot >= 0 && slot < visibleLevels.Length) ShowLevelDetails(visibleLevels[slot]);
        }

        private void ShowLevelDetails(int level)
        {
            if (!GameSceneManager.IsUnlocked(level)) return;
            detailReturnView = paperView == PaperView.Completed ? PaperView.Completed : PaperView.Closed;
            selectedLevel = level;
            selectedSecret = null;
            ShowDetails("Level " + level,
                levelDescriptions != null && level <= levelDescriptions.Length && !string.IsNullOrWhiteSpace(levelDescriptions[level - 1])
                    ? levelDescriptions[level - 1] : "Reach the exit before your turns run out.");
        }

        private void ShowDetails(string title, string description)
        {
            paperView = PaperView.Details;
            SetPaperVisible();
            if (paperTitle != null) paperTitle.text = title;
            if (paperDescription != null) paperDescription.text = description;
            SetCollectionControls(false);
            if (playButton != null) playButton.gameObject.SetActive(true);
        }

        private void PlaySelectedLevel()
        {
            if (paperView != PaperView.Details) return;
            if (selectedSecret == null) GameSceneManager.OpenLevel(selectedLevel);
            else if (IsSecretUnlocked(selectedSecret)) GameSceneManager.OpenSecretLevel(selectedSecret.scenePath);
        }

        private bool IsSecretUnlocked(SecretLevel level) => level != null &&
            (GameSceneManager.IsSecretUnlocked(level.id) ||
             (level.requiredCompletedLevel > 0 && GameSceneManager.IsCompleted(level.requiredCompletedLevel)));

        private void SetPaperVisible()
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            if (paperPanel != null) paperPanel.SetActive(true);
        }

        private void OpenCollection(PaperView view, bool resetPage)
        {
            paperView = view;
            if (resetPage) paperPage = 0;
            entries.Clear();
            if (view == PaperView.Completed)
            {
                for (int level = 1; level <= GameSceneManager.LevelCount; level++)
                    if (GameSceneManager.IsCompleted(level)) entries.Add(level);
            }
            else
            {
                for (int i = 0; secretLevels != null && i < secretLevels.Length; i++)
                    if (IsSecretUnlocked(secretLevels[i])) entries.Add(i);
            }
            SetPaperVisible();
            RefreshPaperCollection();
        }

        private int PaperCapacity => Mathf.Clamp(paperLevelButtons == null ? 0 : paperLevelButtons.Length, 1, 4);

        private void RefreshPaperCollection()
        {
            int pages = Mathf.Max(1, (entries.Count + PaperCapacity - 1) / PaperCapacity);
            paperPage = Mathf.Clamp(paperPage, 0, pages - 1);
            if (paperTitle != null) paperTitle.text = paperView == PaperView.Completed ? "Complete" : "Secret";
            if (paperDescription != null) paperDescription.text = entries.Count == 0
                ? (paperView == PaperView.Completed ? "No completed levels yet." : "No secret levels unlocked yet.")
                : "Select a paper to view its details.";
            if (playButton != null) playButton.gameObject.SetActive(false);
            SetCollectionControls(true);
            for (int i = 0; paperLevelButtons != null && i < paperLevelButtons.Length; i++)
            {
                bool visible = i < PaperCapacity && paperPage * PaperCapacity + i < entries.Count;
                if (paperLevelButtons[i] != null) paperLevelButtons[i].gameObject.SetActive(visible);
                if (visible && paperLevelLabels != null && i < paperLevelLabels.Length && paperLevelLabels[i] != null)
                    paperLevelLabels[i].text = paperView == PaperView.Completed
                        ? "Level " + entries[paperPage * PaperCapacity + i]
                        : secretLevels[entries[paperPage * PaperCapacity + i]].title;
            }
            if (previousPageButton != null) previousPageButton.SetActive(paperPage > 0);
            if (nextPageButton != null) nextPageButton.SetActive(paperPage + 1 < pages);
            if (pageLabel != null) { pageLabel.gameObject.SetActive(pages > 1); pageLabel.text = (paperPage + 1) + " / " + pages; }
        }

        private void SetCollectionControls(bool visible)
        {
            if (paperLevelButtons != null)
                foreach (Button button in paperLevelButtons) if (button != null) button.gameObject.SetActive(visible);
            if (previousPageButton != null) previousPageButton.SetActive(visible);
            if (nextPageButton != null) nextPageButton.SetActive(visible);
            if (pageLabel != null) pageLabel.gameObject.SetActive(visible);
        }

        private void OpenPaperSlot(int slot)
        {
            int entry = paperPage * PaperCapacity + slot;
            if (slot < 0 || slot >= PaperCapacity || entry >= entries.Count) return;
            if (paperView == PaperView.Completed) ShowLevelDetails(entries[entry]);
            else if (paperView == PaperView.Secret)
            {
                selectedSecret = secretLevels[entries[entry]];
                detailReturnView = PaperView.Secret;
                ShowDetails(selectedSecret.title, selectedSecret.description);
            }
        }

        public void OpenSettings()
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
            RefreshUI();
        }

        public void CloseSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        public void ToggleSound()
        {
            GameSettings.SetSound(!GameSettings.SoundEnabled);
            RefreshUI();
        }

        public void ToggleFullscreen()
        {
            GameSettings.SetFullscreen(!GameSettings.FullscreenEnabled);
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (soundLabel != null)
                soundLabel.text = "Sound: " + (GameSettings.SoundEnabled ? "On" : "Off");
            if (fullscreenLabel != null)
                fullscreenLabel.text = "Fullscreen: " + (GameSettings.FullscreenEnabled ? "On" : "Off");

            if (page != Page.LevelSelect || levelButtons == null) return;
            // A later level replaces a completed slot without moving other papers.
            int nextLevel = visibleLevels.Length + 1;
            for (int i = 0; i < levelButtons.Length; i++)
            {
                int level = i + 1;
                if (GameSceneManager.IsCompleted(level))
                {
                    while (nextLevel <= GameSceneManager.LevelCount && GameSceneManager.IsCompleted(nextLevel)) nextLevel++;
                    level = nextLevel++;
                }
                bool visible = i < visibleLevels.Length && level <= GameSceneManager.LevelCount;
                if (i < visibleLevels.Length) visibleLevels[i] = visible ? level : 0;
                bool unlocked = GameSceneManager.IsUnlocked(level);
                if (levelButtons[i] != null)
                {
                    levelButtons[i].gameObject.SetActive(visible);
                    levelButtons[i].interactable = unlocked;
                }
                if (levelLabels != null && i < levelLabels.Length && levelLabels[i] != null)
                {
                    levelLabels[i].text = string.Empty;
                }
            }
        }
    }
}
