namespace ProjectD.Menus
{
    public partial class MenuScreen
    {
        private bool IsSettingsScene => gameObject.scene.path == GameSceneManager.SettingsScene;
        private bool IsSettingsVisible => settingsPanel != null && settingsPanel.activeInHierarchy;
        private OverlayUI SettingsOverlay => settingsPanel != null ? settingsPanel.GetComponentInParent<OverlayUI>(true) : null;
        private bool SettingsCancelPressed => IsSettingsVisible && SettingsOverlay?.CancelPressedThisFrame == true;

        public void OpenSettings()
        {
            if (LoadingScreen.IsLoading && !IsSettingsScene) return;
            if (settingsPanel == null)
            {
                GameSceneManager.OpenInGameSettings();
                return;
            }
            if (mainPanel != null) mainPanel.SetActive(false);
            SettingsOverlay?.ActivateOverlay();
            settingsPanel.SetActive(true);
            RefreshSettingsLabels();
        }

        public void CloseSettings()
        {
            // Always release the overlay's raycast background and restore its UI action map.
            HideSettingsPanel();
            if (GameSceneManager.IsInGameSettingsOpen)
            {
                GameSceneManager.CloseInGameSettings();
                return;
            }
            if (IsSettingsScene)
            {
                GameSceneManager.OpenStart();
                return;
            }
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        private void HideSettingsPanel()
        {
            if (settingsPanel == null) return;
            SettingsOverlay?.DeactivateOverlay();
            settingsPanel.SetActive(false);
        }

        public void ToggleSound() { GameSettings.SetSound(!GameSettings.SoundEnabled); RefreshSettingsLabels(); }
        public void ToggleFullscreen() { GameSettings.SetFullscreen(!GameSettings.FullscreenEnabled); RefreshSettingsLabels(); }

        private void RefreshSettingsLabels()
        {
            if (soundLabel != null) soundLabel.text = "Sound: " + (GameSettings.SoundEnabled ? "On" : "Off");
            if (fullscreenLabel != null) fullscreenLabel.text = "Fullscreen: " + (GameSettings.FullscreenEnabled ? "On" : "Off");
        }
    }
}
