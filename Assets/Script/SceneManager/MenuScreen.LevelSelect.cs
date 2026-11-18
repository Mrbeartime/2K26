using System.Collections.Generic;
using ProjectD.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.Menus
{
    public partial class MenuScreen
    {
        [Header("QuestPaper layout")]
        [SerializeField] private RectTransform boardPaperArea;
        [SerializeField, Min(1)] private int boardSlotCapacity = 4;
        [SerializeField] private bool saveBoardPaperPlacements;
        [SerializeField] private ScrollRect collectionScroll;
        [Header("Level progress source (runtime)")]
        [SerializeField] private string activeProgressJsonPath;
        [SerializeField] private bool firstLevelCompleted;
        [Header("Paper panel (Hierarchy objects)")]
        [SerializeField] private GameObject paperPanel;
        [SerializeField] private TMP_Text paperTitle;
        [SerializeField] private TMP_Text paperDescription;
        [SerializeField] private GameObject paperMetadata;
        [SerializeField] private TMP_Text paperDepth;
        [SerializeField] private RectTransform paperCharacterIcons;
        [SerializeField] private Button playButton;

        private enum PaperView { Closed, Completed, Secret, Details }
        private PaperView paperView;
        private PaperView detailReturnView;
        private readonly List<LevelDefinition> boardLevels = new List<LevelDefinition>();
        private readonly List<LevelDefinition> completedLevels = new List<LevelDefinition>();
        private readonly List<SecretLevelDefinition> unlockedSecrets = new List<SecretLevelDefinition>();
        private int selectedLevel;
        private SecretLevelDefinition selectedSecret;
        private float nextProgressCheck;
        private int appliedProgressRevision = -1;
        private int appliedCatalogRevision = -1;
        private SceneCatalog Catalog => GameSceneManager.Catalog;
        private bool IsPaperCollection => paperView == PaperView.Completed || paperView == PaperView.Secret;
        private const int CollectionVisibleSlots = 4;

        private void SetupLevelSelection()
        {
            BindButton(playButton, PlaySelectedLevel);
            if (mainPanel != null)
            {
                foreach (var button in mainPanel.GetComponentsInChildren<Button>(true))
                {
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        if (button.onClick.GetPersistentTarget(i) != this) continue;
                        switch (button.onClick.GetPersistentMethodName(i))
                        {
                            case nameof(OpenCompleted): ButtonAudioFeedback.BindLevelHover(button); break;
                            case nameof(OpenSecret): ButtonAudioFeedback.BindBellHover(button); break;
                        }
                    }
                }
            }
            EnsureTextCanRender(boardPaperArea);
            if (collectionScroll != null) EnsureTextCanRender(collectionScroll.transform);
            ReloadProgressFromJson();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) ReloadProgressFromJson();
        }

        private void CheckProgressChanges()
        {
            if (Time.unscaledTime < nextProgressCheck) return;
            nextProgressCheck = Time.unscaledTime + 0.25f;
            if (LevelProgressStore.ReloadIfFileChanged() || appliedProgressRevision != LevelProgressStore.Revision ||
                (Catalog != null && appliedCatalogRevision != Catalog.Revision))
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
            if (IsPaperCollection) OpenCollection(paperView, false);
            else if (paperView == PaperView.Details &&
                (selectedSecret == null ? !GameSceneManager.IsUnlocked(selectedLevel) : !IsSecretUnlocked(selectedSecret))) ClosePaper();
            appliedProgressRevision = LevelProgressStore.Revision;
            appliedCatalogRevision = Catalog != null ? Catalog.Revision : -1;
        }

#if UNITY_EDITOR
        [ContextMenu("Progress/Open Active JSON")]
        public void OpenActiveProgressJson()
        {
            string path = UnityEditor.FileUtil.GetProjectRelativePath(LevelProgressStore.SaveFilePath);
            UnityEditor.AssetDatabase.ImportAsset(path);
            UnityEditor.AssetDatabase.OpenAsset(UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path));
        }
#endif

        private void RefreshBoard()
        {
            boardLevels.Clear();
            var catalog = Catalog;
            bool useSavedPlacements = saveBoardPaperPlacements && Application.isPlaying &&
                boardPaperArea != null && catalog != null;
            if (catalog != null)
                foreach (var level in catalog.Levels)
                    if (level != null && !GameSceneManager.IsCompleted(level.id) &&
                        (!useSavedPlacements || GameSceneManager.IsUnlocked(level.id))) boardLevels.Add(level);

            int capacity = Mathf.Max(boardSlotCapacity, boardLevels.Count);
            var placements = useSavedPlacements
                ? LevelProgressStore.GetOrCreateQuestPaperPlacements(boardLevels, capacity)
                : null;

            int visibleCount = 0;
            foreach (var level in boardLevels)
            {
                var paper = GetPaper(boardPapers, boardPaperArea, visibleCount);
                if (paper == null) break;

                if (placements == null) paper.PlaceIn(boardPaperArea, visibleCount, capacity);
                else paper.PlaceOnBoard(boardPaperArea, placements[level.id], capacity);
                paper.BindLevel(level, useSavedPlacements || GameSceneManager.IsUnlocked(level.id), false, () => OpenLevel(level.id));
                visibleCount++;
            }

            HidePapers(boardPapers, visibleCount);
        }

        public void OpenLevel(int levelId)
        {
            var level = Catalog?.FindLevel(levelId);
            if (level == null || !GameSceneManager.IsUnlocked(levelId)) return;
            detailReturnView = paperView == PaperView.Completed ? PaperView.Completed : PaperView.Closed;
            selectedLevel = levelId;
            selectedSecret = null;
            ShowDetails(level.Title, level.description, level.characterAvailable, level.depth, $"level:{level.id}");
        }

        private void PlaySelectedLevel()
        {
            if (paperView != PaperView.Details) return;
            if (selectedSecret != null && !IsSecretUnlocked(selectedSecret)) return;
            PlayMenuSound();
            if (selectedSecret == null) GameSceneManager.OpenLevelFromSelect(selectedLevel);
            else GameSceneManager.OpenSecretFromSelect(selectedSecret.scene.Path);
        }

        private static bool IsSecretUnlocked(SecretLevelDefinition level) => level != null && GameSceneManager.IsSecretUnlocked(level.id);

        public void OpenCompleted() => OpenCollection(PaperView.Completed, true);
        public void OpenSecret() => OpenCollection(PaperView.Secret, true);

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

        private void OpenCollection(PaperView view, bool resetScroll)
        {
            var catalog = Catalog;
            if (catalog == null) return;
            bool completed = view == PaperView.Completed;
            paperView = view;
            if (completed)
            {
                completedLevels.Clear();
                foreach (var level in catalog.Levels)
                    if (level != null && GameSceneManager.IsCompleted(level.id)) completedLevels.Add(level);
            }
            else
            {
                unlockedSecrets.Clear();
                foreach (var secret in catalog.SecretLevels)
                    if (IsSecretUnlocked(secret)) unlockedSecrets.Add(secret);
            }

            SetPaperVisible();
            RefreshPaperCollection(resetScroll);
        }

        private void RefreshPaperCollection(bool resetScroll)
        {
            bool completed = paperView == PaperView.Completed;
            int entryCount = completed ? completedLevels.Count : unlockedSecrets.Count;
            if (paperTitle != null) paperTitle.text = completed ? "Complete" : "Secret";
            if (paperDescription != null) paperDescription.text = entryCount == 0
                ? (completed ? "No completed levels yet." : "No secret levels unlocked yet.") : "Select a paper to view its details.";
            if (paperMetadata != null) paperMetadata.SetActive(false);
            if (playButton != null) playButton.gameObject.SetActive(false);

            if (collectionScroll == null || collectionScroll.content == null || collectionScroll.viewport == null) return;
            collectionScroll.gameObject.SetActive(true);
            collectionScroll.StopMovement();
            var content = collectionScroll.content;
            float viewportWidth = collectionScroll.viewport.rect.width;
            int slots = Mathf.Max(CollectionVisibleSlots, entryCount);
            float contentWidth = viewportWidth * slots / CollectionVisibleSlots;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
            float left = resetScroll ? 0f : Mathf.Clamp(content.anchoredPosition.x, viewportWidth - contentWidth, 0f);
            content.anchoredPosition = new Vector2(left, 0f);

            int visibleCount = 0;
            for (; visibleCount < entryCount; visibleCount++)
            {
                var paper = GetPaper(collectionPapers, content, visibleCount);
                if (paper == null) break;
                paper.PlaceIn(content, visibleCount, slots);
                if (completed)
                {
                    var level = completedLevels[visibleCount];
                    paper.BindLevel(level, GameSceneManager.IsUnlocked(level.id), true, () => OpenLevel(level.id));
                }
                else
                {
                    var level = unlockedSecrets[visibleCount];
                    paper.BindSecret(level, () => OpenSecretDetails(level));
                }
            }
            HidePapers(collectionPapers, visibleCount);
        }

        private void OpenSecretDetails(SecretLevelDefinition level)
        {
            if (!IsSecretUnlocked(level)) return;
            selectedSecret = level;
            detailReturnView = PaperView.Secret;
            ShowDetails(level.title, level.description, level.characterAvailable, level.depth, $"secret:{level.id}");
        }
    }
}
