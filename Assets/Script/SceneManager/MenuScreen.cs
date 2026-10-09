using UnityEngine;
using UnityEngine.UI;

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

        private void Awake()
        {
            Time.timeScale = 1f;
            if (mainPanel != null) mainPanel.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            RefreshUI();
        }

        public void OpenLevelSelect() => GameSceneManager.OpenLevelSelect();
        public void OpenStart() => GameSceneManager.OpenStart();
        public void ExitGame() => GameSceneManager.ExitGame();
        public void OpenLevel1() => GameSceneManager.OpenLevel(1);
        public void OpenLevel2() => GameSceneManager.OpenLevel(2);
        public void OpenLevel3() => GameSceneManager.OpenLevel(3);
        public void OpenLevel4() => GameSceneManager.OpenLevel(4);
        public void OpenLevel5() => GameSceneManager.OpenLevel(5);

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
            for (int i = 0; i < levelButtons.Length; i++)
            {
                int level = i + 1;
                bool unlocked = GameSceneManager.IsUnlocked(level);
                if (levelButtons[i] != null) levelButtons[i].interactable = unlocked;
                if (levelLabels != null && i < levelLabels.Length && levelLabels[i] != null)
                {
                    string suffix = !unlocked ? " (Locked)" : GameSceneManager.IsCompleted(level) ? " (Completed)" : "";
                    levelLabels[i].text = "Level " + level + suffix;
                }
            }
        }
    }
}
