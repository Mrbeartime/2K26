using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectD.Menus;

/// <summary>
/// Shows a victory-only overlay when TurnGameManager raises GameEnded(true, ...).
/// Attach this script to an always-active object, NOT to the panel that it hides.
/// Level progress is recorded by TurnGameManager.ShowVictory(), not this UI.
/// </summary>
public class LevelCompleteUI : MonoBehaviour
{
    [Header("Level Complete UI")]
    [SerializeField] private GameObject completePanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Buttons")]
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button levelSelectButton;

    [Header("Messages")]
    [SerializeField] private string completeTitle = "LEVEL COMPLETE!";
    [SerializeField] private string completeMessage = "You reached the exit!";

    private TurnGameManager turnManager;
    private bool hasShown;

    private void Awake()
    {
        // Keep the controller object active; hide only its separate panel.
        if (completePanel != null)
            completePanel.SetActive(false);

        // Inspector OnClick bindings are unnecessary; this script wires them.
        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(OpenNextLevel);
        if (retryButton != null)
            retryButton.onClick.AddListener(RetryLevel);
        if (levelSelectButton != null)
            levelSelectButton.onClick.AddListener(OpenLevelSelect);
    }

    private void Start()
    {
        turnManager = TurnGameManager.Instance;
        if (turnManager == null)
        {
            Debug.LogError("LevelCompleteUI: TurnGameManager was not found.", this);
            return;
        }

        turnManager.GameEnded += OnGameEnded;
    }

    private void OnDestroy()
    {
        if (turnManager != null)
            turnManager.GameEnded -= OnGameEnded;

        if (nextLevelButton != null)
            nextLevelButton.onClick.RemoveListener(OpenNextLevel);
        if (retryButton != null)
            retryButton.onClick.RemoveListener(RetryLevel);
        if (levelSelectButton != null)
            levelSelectButton.onClick.RemoveListener(OpenLevelSelect);
    }

    private void OnGameEnded(bool won, string reason)
    {
        // Only show the victory panel; defeat UI can be handled separately.
        if (!won || hasShown)
            return;

        if (completePanel == null)
        {
            Debug.LogError("LevelCompleteUI: Complete Panel is not assigned.", this);
            return;
        }

        hasShown = true;
        int level = GameSceneManager.ActiveLevel;

        if (titleText != null)
            titleText.text = completeTitle;

        if (descriptionText != null)
            descriptionText.text = level > 0
                ? $"Level {level} completed!"
                : completeMessage;

        // The final level and non-standard scenes don't have a Next Level button.

        // TODO: เปิดระบบ Next Level เมื่อ Scene พร้อม

        // bool hasNext = level >= 1 && level < GameSceneManager.LevelCount;
        // if (nextLevelButton != null)
        //     nextLevelButton.gameObject.SetActive(hasNext);

        completePanel.SetActive(true);
    }

    public void OpenNextLevel()
    {
        // TODO: เปิดด่านถัดไปเมื่อสร้าง Scene เสร็จ เผื่อทำ

        // int level = GameSceneManager.ActiveLevel;
        // if (level < 1 || level >= GameSceneManager.LevelCount)
        //     return;

        // GameSceneManager.OpenLevel(level + 1);

        Debug.Log("Next Level - Not implemented yet");
    }

    public void RetryLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OpenLevelSelect()
    {
        // TODO: กลับหน้าเลือกด่านเมื่อสร้าง Scene เสร็จ

        // GameSceneManager.OpenLevelSelect();

        Debug.Log("Level Select - Not implemented yet");
    }
}
