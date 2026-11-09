using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System.Collections.Generic;

namespace ProjectD.Menus
{
    /// <summary>
    /// Menu entry points and screen lifecycle.
    /// Settings are in MenuScreen.Settings.cs; level cards are in MenuScreen.LevelSelect.cs.
    /// Keep this component on an always-active object outside the panels it opens.
    /// </summary>
    public partial class MenuScreen : MonoBehaviour
    {
        public enum Page { Start, LevelSelect }
        [SerializeField] private Page page;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Text soundLabel;
        [SerializeField] private Text fullscreenLabel;

        private readonly List<KeyValuePair<Button, UnityAction>> bindings = new List<KeyValuePair<Button, UnityAction>>();

        private void Awake()
        {
            // The old Settings scene has a temporary script that loads Start on Esc.
            // MenuScreen now owns closing this screen and restoring the previous input.
            if (gameObject.scene.path == GameSceneManager.SettingsScene)
                foreach (var root in gameObject.scene.GetRootGameObjects())
                    foreach (var legacy in root.GetComponentsInChildren<TEMPORARYMENUFIXER>(true))
                        legacy.enabled = false;
            if (!GameSceneManager.IsInGameSettingsOpen && TurnGameManager.Instance == null) Time.timeScale = 1f;
            if (mainPanel != null) mainPanel.SetActive(true);
            HideSettingsPanel();
            if (paperPanel != null) paperPanel.SetActive(false);
            ProjectD.Audio.ButtonAudioFeedback.BindMenu(settingsPanel);
            if (page == Page.LevelSelect) SetupLevelSelection();
            RefreshSettingsLabels();
            if (gameObject.scene.path == GameSceneManager.SettingsScene) OpenSettings();
        }

        private void Start()
        {
            if (page == Page.LevelSelect) PlayMenuSound();
        }

        private void Update()
        {
            if (GameSceneManager.IsChangingScene) return;
            // The menu behind additive Settings must not consume Escape first.
            if (GameSceneManager.IsInGameSettingsOpen && gameObject.scene.path != GameSceneManager.SettingsScene) return;
            if (page == Page.LevelSelect) CheckProgressChanges();
            var overlay = settingsPanel != null ? settingsPanel.GetComponentInParent<OverlayUI>(true) : null;
            bool backPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (IsSettingsVisible && overlay != null && overlay.CancelPressedThisFrame);
            if (backPressed && MenuInput.ConsumeBack()) GoBack();
        }

        public void GoBack()
        {
            if (IsSettingsVisible || gameObject.scene.path == GameSceneManager.SettingsScene) { CloseSettings(); return; }
            if (GameSceneManager.IsInGameSettingsOpen) return;
            if (paperView == PaperView.Details && detailReturnView != PaperView.Closed)
                OpenCollection(detailReturnView, false);
            else if (paperView != PaperView.Closed) ClosePaper();
            else if (page == Page.LevelSelect) OpenStart();
        }

        private void BindButton(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.AddListener(action);
            bindings.Add(new KeyValuePair<Button, UnityAction>(button, action));
        }

        private void OnDestroy()
        {
            foreach (var binding in bindings)
                if (binding.Key != null) binding.Key.onClick.RemoveListener(binding.Value);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && page == Page.LevelSelect) ReloadProgressFromJson();
        }

        private static void PlayMenuSound() => ProjectD.Audio.GameAudioManager.Instance?.PlayUI(ProjectD.Audio.AudioCue.Pause);
        public void OpenLevelSelect() => GameSceneManager.OpenLevelSelect();
        public void OpenStart() { if (page == Page.LevelSelect) PlayMenuSound(); GameSceneManager.OpenStart(); }
        public void OpenGuide() => GameSceneManager.OpenGuide();
        public void OpenInGameSettings() => GameSceneManager.OpenInGameSettings();
        public void ReloadCurrentScene() => GameSceneManager.ReloadCurrentScene();
        public void ExitGame() => GameSceneManager.ExitGame();

        // Generic Inspector callback for any future level ID.
        public void OpenLevel(int levelId) => ShowLevelDetails(levelId);
        // Preserve existing scene/prefab OnClick bindings.
        public void OpenLevel1() => OpenLevel(1);
        public void OpenLevel2() => OpenLevel(2);
        public void OpenLevel3() => OpenLevel(3);
        public void OpenLevel4() => OpenLevel(4);
        public void OpenLevel5() => OpenLevel(5);
    }
}
