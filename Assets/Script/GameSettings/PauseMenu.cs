using ProjectD.Menus;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pauseButton;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Text soundLabel;
    [SerializeField] private Text fullscreenLabel;

    private bool isPaused;

    private void Awake()
    {
        if (pauseButton != null) pauseButton.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        RefreshSettings();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (settingsPanel != null && settingsPanel.activeSelf) CloseSettings();
        else if (isPaused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        if (TurnGameManager.Instance != null && TurnGameManager.Instance.IsGameComplete) return;
        isPaused = true;
        Time.timeScale = 0f;
        if (pauseButton != null) pauseButton.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);
    }

    public void OpenSettings()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
        RefreshSettings();
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void ToggleSound()
    {
        GameSettings.SetSound(!GameSettings.SoundEnabled);
        RefreshSettings();
    }

    public void ToggleFullscreen()
    {
        GameSettings.SetFullscreen(!GameSettings.FullscreenEnabled);
        RefreshSettings();
    }

    public void ReturnToLevelSelect()
    {
        isPaused = false;
        Time.timeScale = 1f;
        GameSceneManager.OpenLevelSelect();
    }

    private void OnDestroy()
    {
        if (isPaused) Time.timeScale = 1f;
    }

    private void RefreshSettings()
    {
        if (soundLabel != null)
            soundLabel.text = "Sound: " + (GameSettings.SoundEnabled ? "On" : "Off");
        if (fullscreenLabel != null)
            fullscreenLabel.text = "Fullscreen: " + (GameSettings.FullscreenEnabled ? "On" : "Off");
    }
}
