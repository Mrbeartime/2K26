using System.Collections.Generic;
using ProjectD.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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
            if (page == Page.Start && !isSettingsScene && mainPanel != null)
            {
                ButtonAudioFeedback.BindMenu(mainPanel);
                foreach (var b in mainPanel.GetComponentsInChildren<Button>(true))
                    if (b.GetComponent<SelectedButtonBold>() == null) b.gameObject.AddComponent<SelectedButtonBold>();
                // Hold buttons (Exit) quit once the fill completes.
                foreach (var hold in mainPanel.GetComponentsInChildren<HoldButton>(true))
                    hold.HoldCompleted += ExitGame;
                BindSpaceSubmit();
            }
            if (page == Page.LevelSelect)
            {
                SetupLevelSelection();
                BindSpaceSubmit();
            }
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

        // Nothing selected: a navigation key / Space selects the first button (after the EventSystem has handled the key).
        // Start page: the top button. Level select: the leftmost QuestPaper / Complete / Secret button.
        private void LateUpdate()
        {
            if (uiInput == null || GameSceneManager.IsInGameSettingsOpen || EventSystem.current == null) return;
            var root = page == Page.LevelSelect && paperPanel != null && paperPanel.activeInHierarchy ? paperPanel : mainPanel;
            if (root == null || !root.activeInHierarchy) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy) return;   // a hidden panel leaves a stale selection
            var k = Keyboard.current;
            if (k == null || !(k.spaceKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame
                || k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame
                || k.wKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame)) return;
            Button first = null;
            foreach (var b in root.GetComponentsInChildren<Button>(false))
            {
                if (!b.IsInteractable()) continue;
                if (first == null || (page == Page.LevelSelect && b.transform.position.x < first.transform.position.x)) first = b;
            }
            if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
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

        // Same input as the Pause overlay: its UI map has Space as Submit (the scene's default asset does not).
        private ProjectDInputAction uiInput;

        private void BindSpaceSubmit()
        {
            var module = FindFirstObjectByType<InputSystemUIInputModule>();
            if (module == null) return;
            uiInput = new ProjectDInputAction();
            uiInput.UI.Enable();
            module.actionsAsset = uiInput.asset;
            uiInput.UI.Submit.started += OnSubmitStarted;
            uiInput.UI.Submit.canceled += OnSubmitCanceled;
        }

        // Space/Enter on the selected Hold button (Exit) starts the hold; release cancels it.
        private void OnSubmitStarted(InputAction.CallbackContext _) =>
            EventSystem.current?.currentSelectedGameObject?.GetComponent<HoldButton>()?.BeginKeyboardHold();

        private void OnSubmitCanceled(InputAction.CallbackContext _) =>
            EventSystem.current?.currentSelectedGameObject?.GetComponent<HoldButton>()?.CancelKeyboardHold();

        private void BindButton(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.AddListener(action);
            bindings.Add((button, action));
        }

        private void OnDestroy()
        {
            uiInput?.Dispose();
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
