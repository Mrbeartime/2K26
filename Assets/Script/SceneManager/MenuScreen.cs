using System.Collections.Generic;
using ProjectD.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectD.Menus
{
    /// <summary>
    /// Menu entry points and screen lifecycle.
    /// Keep this component on an always-active object outside the panels it opens.
    /// </summary>
    public partial class MenuScreen : MonoBehaviour
    {
        public enum Page { Start, LevelSelect }
        [SerializeField] private Page page;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private TMP_Text soundLabel;
        [SerializeField] private TMP_Text fullscreenLabel;

        private readonly List<(Button button, UnityAction action)> bindings = new List<(Button, UnityAction)>();

        private void Awake()
        {
            bool isSettingsScene = IsSettingsScene;
            // The old Settings scene has a temporary script that loads Start on Esc.
            // MenuScreen now owns closing this screen and restoring the previous input.
            if (isSettingsScene)
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                    foreach (var legacy in root.GetComponentsInChildren<TEMPORARYMENUFIXER>(true))
                        legacy.enabled = false;
            }

            if (!GameSceneManager.IsInGameSettingsOpen && TurnGameManager.Instance == null) Time.timeScale = 1f;
            if (mainPanel != null) mainPanel.SetActive(true);
            HideSettingsPanel();
            if (paperPanel != null) paperPanel.SetActive(false);
            ButtonAudioFeedback.BindMenu(settingsPanel);
            if (page == Page.Start && !isSettingsScene) ButtonAudioFeedback.BindMenu(mainPanel);
            if (page == Page.LevelSelect) SetupLevelSelection();
            if (!isSettingsScene || settingsPanel == null) RefreshSettingsLabels();
            if (isSettingsScene) OpenSettings();
        }

        private void Start()
        {
            if (page == Page.LevelSelect) PlayMenuSound();
        }

        private void Update()
        {
            if (GameSceneManager.IsChangingScene) return;
            // The menu behind additive Settings must not consume Escape first.
            if (GameSceneManager.IsInGameSettingsOpen && !IsSettingsScene) return;
            if (page == Page.LevelSelect) CheckProgressChanges();
            bool backPressed = Keyboard.current?.escapeKey.wasPressedThisFrame == true || SettingsCancelPressed;
            if (backPressed && MenuInput.ConsumeBack()) GoBack();
        }

        public void GoBack()
        {
            if (IsSettingsVisible || IsSettingsScene)
            {
                CloseSettings();
                return;
            }
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
            bindings.Add((button, action));
        }

        private void OnDestroy()
        {
            foreach (var binding in bindings)
                if (binding.button != null) binding.button.onClick.RemoveListener(binding.action);
        }

        private static void PlayMenuSound() => GameAudioManager.Instance?.PlayUI(GameAudioManager.TimingEvent(AudioCue.Pause));
        public void OpenLevelSelect() => GameSceneManager.OpenLevelSelect();
        public void OpenStart()
        {
            if (page == Page.LevelSelect) PlayMenuSound();
            GameSceneManager.OpenStart();
        }

        public void OpenGuide() => GameSceneManager.OpenGuide();
        public void OpenInGameSettings() => GameSceneManager.OpenInGameSettings();
        public void ReloadCurrentScene() => GameSceneManager.ReloadCurrentScene();
        public void ExitGame() => GameSceneManager.ExitGame();
    }
}
