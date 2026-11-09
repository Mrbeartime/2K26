using UnityEngine;

namespace ProjectD.Menus
{
    public partial class MenuScreen
    {
        private bool IsSettingsVisible => settingsPanel != null && settingsPanel.activeInHierarchy;

        public void OpenSettings()
        {
            if (LoadingScreen.IsLoading && gameObject.scene.path != GameSceneManager.SettingsScene) return;
            if (settingsPanel == null)
            {
                GameSceneManager.OpenInGameSettings();
                return;
            }
            if (mainPanel != null) mainPanel.SetActive(false);
            settingsPanel.GetComponentInParent<OverlayUI>(true)?.ActivateOverlay();
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
            if (gameObject.scene.path == GameSceneManager.SettingsScene)
            {
                GameSceneManager.OpenStart();
                return;
            }
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        private void HideSettingsPanel()
        {
            if (settingsPanel == null) return;
            settingsPanel.GetComponentInParent<OverlayUI>(true)?.DeactivateOverlay();
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
