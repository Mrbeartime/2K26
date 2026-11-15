using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.Menus
{
    public partial class MenuScreen
    {
        [Header("Board slots (Hierarchy order, no fixed level limit)")]
        [SerializeField] private Button[] levelButtons;
        [SerializeField] private Button[] boardHoverButtons;
        [SerializeField] private Button secretHoverButton;
        [SerializeField] private Text[] levelLabels;
        [Header("Level progress source (runtime)")]
        [SerializeField] private string activeProgressJsonPath;
        [SerializeField] private bool firstLevelCompleted;
        [Header("Paper panel (Hierarchy objects)")]
        [SerializeField] private GameObject paperPanel;
        [SerializeField] private Text paperTitle;
        [SerializeField] private Text paperDescription;
        [SerializeField] private Button playButton;
        [SerializeField] private Button[] paperLevelButtons;
        [SerializeField] private Text[] paperLevelLabels;
        [SerializeField] private GameObject previousPageButton;
        [SerializeField] private GameObject nextPageButton;
        [SerializeField] private Text pageLabel;

        // Keep old serialized data readable; edit level descriptions/secrets in SceneCatalog now.
        [Serializable] public class SecretLevel
        {
            public string id, title, description, scenePath;
            public int requiredCompletedLevel;
        }
        [SerializeField, HideInInspector] private string[] levelDescriptions;
        [SerializeField, HideInInspector] private SecretLevel[] secretLevels;

        private enum PaperView { Closed, Completed, Secret, Details }
        private PaperView paperView;
        private PaperView detailReturnView;
        private readonly List<int> entries = new List<int>();
        private readonly List<int> visibleLevels = new List<int>();
        private int paperPage;
        private int selectedLevel;
        private SecretLevelDefinition selectedSecret;
        private float nextProgressCheck;
        private int appliedProgressRevision = -1;
        private SceneCatalog Catalog => GameSceneManager.Catalog;

        private void SetupLevelSelection()
        {
            BindButton(playButton, PlaySelectedLevel);
            if (boardHoverButtons != null)
                foreach (var button in boardHoverButtons) ProjectD.Audio.ButtonAudioFeedback.BindLevelHover(button);
            ProjectD.Audio.ButtonAudioFeedback.BindBellHover(secretHoverButton);
            for (int i = 0; levelButtons != null && i < levelButtons.Length; i++)
            {
                int slot = i;
                BindButton(levelButtons[i], () => OpenLevelSlot(slot));
                ProjectD.Audio.ButtonAudioFeedback.BindLevelHover(levelButtons[i]);
            }
            for (int i = 0; paperLevelButtons != null && i < paperLevelButtons.Length; i++)
            {
                int slot = i;
                BindButton(paperLevelButtons[i], () => OpenPaperSlot(slot));
            }
            ReloadProgressFromJson();
        }

        private void CheckProgressChanges()
        {
            if (Time.unscaledTime < nextProgressCheck) return;
            nextProgressCheck = Time.unscaledTime + 0.25f;
            if (LevelProgressStore.ReloadIfFileChanged() || appliedProgressRevision != LevelProgressStore.Revision)
                ApplyProgressToUI();
        }

        [ContextMenu("Progress/Reload From JSON")]
        public void ReloadProgressFromJson()
        {
            if (page != Page.LevelSelect) return;
            LevelProgressStore.Reload();
            ApplyProgressToUI();
        }

        private void ApplyProgressToUI()
        {
            activeProgressJsonPath = LevelProgressStore.SaveFilePath;
            firstLevelCompleted = Catalog != null && GameSceneManager.IsCompleted(Catalog.firstLevelId);
            RefreshBoard();
            if (paperView == PaperView.Completed || paperView == PaperView.Secret) OpenCollection(paperView, false);
            else if (paperView == PaperView.Details &&
                (selectedSecret == null ? !GameSceneManager.IsUnlocked(selectedLevel) : !IsSecretUnlocked(selectedSecret))) ClosePaper();
            appliedProgressRevision = LevelProgressStore.Revision;
        }

        [ContextMenu("Progress/Open Active JSON")]
        public void OpenActiveProgressJson()
        {
#if UNITY_EDITOR
            const string path = "Assets/Script/SceneManager/level-progress.json";
            UnityEditor.AssetDatabase.ImportAsset(path);
            UnityEditor.AssetDatabase.OpenAsset(UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path));
#endif
        }

        private void RefreshBoard()
        {
            visibleLevels.Clear();
            if (Catalog != null)
                foreach (var level in Catalog.Levels)
                    if (!GameSceneManager.IsCompleted(level.id)) visibleLevels.Add(level.id);
            for (int i = 0; levelButtons != null && i < levelButtons.Length; i++)
            {
                bool visible = i < visibleLevels.Count;
                int id = visible ? visibleLevels[i] : 0;
                bool unlocked = visible && GameSceneManager.IsUnlocked(id);
                if (levelButtons[i] != null)
                {
                    levelButtons[i].gameObject.SetActive(visible);
                    levelButtons[i].interactable = unlocked;
                    var colors = levelButtons[i].colors;
                    colors.disabledColor = new Color(0.38f, 0.38f, 0.38f, 1f);
                    levelButtons[i].colors = colors;
                }
                if (levelLabels != null && i < levelLabels.Length && levelLabels[i] != null)
                {
                    levelLabels[i].text = visible ? Catalog.FindLevel(id).Title + (unlocked ? string.Empty : "\nLocked") : string.Empty;
                    levelLabels[i].color = unlocked ? new Color(0.19f, 0.14f, 0.08f, 1f) : new Color(0.92f, 0.92f, 0.92f, 1f);
                }
            }
        }

        private void OpenLevelSlot(int slot)
        {
            if (slot >= 0 && slot < visibleLevels.Count) ShowLevelDetails(visibleLevels[slot]);
        }

        private void ShowLevelDetails(int id)
        {
            if (Catalog == null || !GameSceneManager.IsUnlocked(id)) return;
            var level = Catalog.FindLevel(id);
            if (level == null) return;
            detailReturnView = paperView == PaperView.Completed ? PaperView.Completed : PaperView.Closed;
            selectedLevel = id;
            selectedSecret = null;
            ShowDetails(level.Title, level.description);
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
            if (selectedSecret == null) { PlayMenuSound(); GameSceneManager.OpenLevelFromSelect(selectedLevel); }
            else if (IsSecretUnlocked(selectedSecret)) { PlayMenuSound(); GameSceneManager.OpenSecretFromSelect(selectedSecret.scene.Path); }
        }

        private static bool IsSecretUnlocked(SecretLevelDefinition level) => level != null &&
            (GameSceneManager.IsSecretUnlocked(level.id) ||
             (level.requiredCompletedLevel > 0 && GameSceneManager.IsCompleted(level.requiredCompletedLevel)));

        public void OpenCompleted() => OpenCollection(PaperView.Completed, true);
        public void OpenSecret() => OpenCollection(PaperView.Secret, true);
        public void PreviousPaperPage() { paperPage = Mathf.Max(0, paperPage - 1); RefreshPaperCollection(); }
        public void NextPaperPage() { paperPage++; RefreshPaperCollection(); }

        public void ClosePaper()
        {
            if (paperPanel != null && paperPanel.activeSelf) PlayMenuSound();
            paperView = PaperView.Closed;
            if (paperPanel != null) paperPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);
            RefreshBoard();
        }

        private void SetPaperVisible()
        {
            if (paperPanel != null && !paperPanel.activeSelf) PlayMenuSound();
            if (mainPanel != null) mainPanel.SetActive(false);
            if (paperPanel != null) paperPanel.SetActive(true);
        }

        private void OpenCollection(PaperView view, bool resetPage)
        {
            if (Catalog == null) return;
            if (paperLevelButtons != null)
                foreach (var button in paperLevelButtons) ProjectD.Audio.ButtonAudioFeedback.BindLevelHover(button, view == PaperView.Completed);
            paperView = view;
            if (resetPage) paperPage = 0;
            entries.Clear();
            if (view == PaperView.Completed)
            {
                foreach (var level in Catalog.Levels) if (GameSceneManager.IsCompleted(level.id)) entries.Add(level.id);
            }
            else
                for (int i = 0; i < Catalog.SecretLevels.Count; i++) if (IsSecretUnlocked(Catalog.SecretLevels[i])) entries.Add(i);
            SetPaperVisible();
            RefreshPaperCollection();
        }

        private int PaperCapacity => Mathf.Max(1, paperLevelButtons == null ? 0 : paperLevelButtons.Length);
        private void RefreshPaperCollection()
        {
            int pages = Mathf.Max(1, (entries.Count + PaperCapacity - 1) / PaperCapacity);
            paperPage = Mathf.Clamp(paperPage, 0, pages - 1);
            bool completed = paperView == PaperView.Completed;
            if (paperTitle != null) paperTitle.text = completed ? "Complete" : "Secret";
            if (paperDescription != null) paperDescription.text = entries.Count == 0
                ? (completed ? "No completed levels yet." : "No secret levels unlocked yet.") : "Select a paper to view its details.";
            if (playButton != null) playButton.gameObject.SetActive(false);
            SetCollectionControls(true);
            for (int i = 0; paperLevelButtons != null && i < paperLevelButtons.Length; i++)
            {
                int index = paperPage * PaperCapacity + i;
                bool visible = index < entries.Count;
                if (paperLevelButtons[i] != null) paperLevelButtons[i].gameObject.SetActive(visible);
                if (visible && paperLevelLabels != null && i < paperLevelLabels.Length && paperLevelLabels[i] != null)
                    paperLevelLabels[i].text = completed ? Catalog.FindLevel(entries[index]).Title : Catalog.SecretLevels[entries[index]].title;
            }
            if (previousPageButton != null) previousPageButton.SetActive(paperPage > 0);
            if (nextPageButton != null) nextPageButton.SetActive(paperPage + 1 < pages);
            if (pageLabel != null) { pageLabel.gameObject.SetActive(pages > 1); pageLabel.text = (paperPage + 1) + " / " + pages; }
        }

        private void SetCollectionControls(bool visible)
        {
            if (paperLevelButtons != null) foreach (Button button in paperLevelButtons) if (button != null) button.gameObject.SetActive(visible);
            if (previousPageButton != null) previousPageButton.SetActive(visible);
            if (nextPageButton != null) nextPageButton.SetActive(visible);
            if (pageLabel != null) pageLabel.gameObject.SetActive(visible);
        }

        private void OpenPaperSlot(int slot)
        {
            int index = paperPage * PaperCapacity + slot;
            if (slot < 0 || slot >= PaperCapacity || index >= entries.Count) return;
            if (paperView == PaperView.Completed) ShowLevelDetails(entries[index]);
            else if (paperView == PaperView.Secret)
            {
                selectedSecret = Catalog.SecretLevels[entries[index]];
                detailReturnView = PaperView.Secret;
                ShowDetails(selectedSecret.title, selectedSecret.description);
            }
        }
    }
}
