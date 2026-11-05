using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectD.Menus;

/// <summary>
/// Shows the scene-authored victory or defeat overlay after a confirmed game result.
/// Attach this script to an always-active object, NOT to the panel that it hides.
/// Level progress is recorded by TurnGameManager.ShowVictory(), not this UI.
/// </summary>
public class LevelCompleteUI : MonoBehaviour
{
    [Header("Level Complete UI")]
    [SerializeField] private GameObject completePanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private GameObject clearStamp;
    [SerializeField] private GameResultController resultController;

    [Header("Buttons")]
    [SerializeField, InspectorName("Return To Guild Button")] private Button nextLevelButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button levelSelectButton;

    [Header("Messages")]
    [SerializeField] private string completeTitle = "LEVEL COMPLETE!";
    [SerializeField] private string completeMessage = "You reached the exit!";
    [SerializeField] private string incompleteTitle = "Quest Incomplete";

    [Header("Victory Timing")]
    [Tooltip("Wait for active treasure chests in this scene to finish opening.")]
    [SerializeField] private bool waitForChestAnimation = true;
    [Tooltip("Extra pause after the chest opens, before showing the victory panel (seconds).")]
    [SerializeField, Min(0f)] private float delayAfterChestOpen = 0.35f;

    private TurnGameManager turnManager;
    private bool hasShown;

    private void Awake()
    {
        if (resultController == null) resultController = GetComponent<GameResultController>();
        if (resultController == null) resultController = gameObject.AddComponent<GameResultController>();
        BindExistingPanel();
        // Keep the controller object active; hide only its separate panel.
        if (completePanel != null)
            completePanel.SetActive(false);

        // Inspector OnClick bindings are unnecessary; this script wires them.
        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(ReturnToGuild);
        if (retryButton != null)
            retryButton.onClick.AddListener(RetryLevel);
        if (levelSelectButton != null)
            levelSelectButton.onClick.AddListener(OpenLevelSelect);
    }

    private void Start()
    {
        turnManager = resultController.TurnManager;
        if (turnManager == null)
        {
            Debug.LogError("LevelCompleteUI: TurnGameManager was not found.", this);
            return;
        }

        turnManager.GameEnded += OnGameEnded;
        if (turnManager.IsGameComplete)
            OnGameEnded(turnManager.PlayerWon, turnManager.GameOverReason);
    }

    private void OnDestroy()
    {
        if (turnManager != null)
            turnManager.GameEnded -= OnGameEnded;

        if (nextLevelButton != null)
            nextLevelButton.onClick.RemoveListener(ReturnToGuild);
        if (retryButton != null)
            retryButton.onClick.RemoveListener(RetryLevel);
        if (levelSelectButton != null)
            levelSelectButton.onClick.RemoveListener(OpenLevelSelect);
    }

    private void OnGameEnded(bool won, string reason)
    {
        if (hasShown)
            return;

        if (completePanel == null)
        {
            Debug.LogError("LevelCompleteUI: Complete Panel is not assigned.", this);
            return;
        }

        hasShown = true;
        if (won) StartCoroutine(ShowAfterChestOpens());
        else ShowResult(false, reason);
    }

    private IEnumerator ShowAfterChestOpens()
    {
        if (waitForChestAnimation)
        {
            var chests = FindObjectsByType<TreasureChestAnimation>();
            // Start all chests before waiting; event subscriber order must not matter.
            foreach (var chest in chests)
                if (chest.isActiveAndEnabled && chest.gameObject.scene == gameObject.scene)
                    chest.PlayOpen();

            foreach (var chest in chests)
                if (chest != null && chest.isActiveAndEnabled && chest.gameObject.scene == gameObject.scene)
                    yield return chest.WaitForOpenCompleted();
        }

        if (delayAfterChestOpen > 0f)
            yield return new WaitForSecondsRealtime(delayAfterChestOpen);
        if (completePanel == null) yield break;

        ShowResult(true, string.Empty);
    }

    private void ShowResult(bool won, string reason)
    {
        if (titleText != null) titleText.text = won ? completeTitle : incompleteTitle;
        if (descriptionText != null)
            descriptionText.text = won
                ? (resultController.Level > 0 ? $"Level {resultController.Level} completed!" : completeMessage)
                : reason;
        if (clearStamp != null) clearStamp.SetActive(won);
        if (nextLevelButton != null)
            nextLevelButton.gameObject.SetActive(won);
        if (retryButton != null) retryButton.gameObject.SetActive(!won);
        completePanel.SetActive(true);
    }

    // Resolve only missing references in older scene instances; keep Inspector assignments.
    private void BindExistingPanel()
    {
        if (completePanel == null) return;
        foreach (var child in completePanel.GetComponentsInChildren<Transform>(true))
        {
            if (clearStamp == null && child.name == "ClearStamp") clearStamp = child.gameObject;
            if (titleText == null && child.name == "Quest Complete") titleText = child.GetComponent<TMP_Text>();
            if (descriptionText == null && child.name == "descriptionText") descriptionText = child.GetComponent<TMP_Text>();
            if (nextLevelButton == null && (child.name == "Return to Guild" || child.name == "Next level"))
                nextLevelButton = child.GetComponent<Button>();
            if (retryButton == null && child.name == "RETRY") retryButton = child.GetComponent<Button>();
            if (levelSelectButton == null && child.name == "Abandon") levelSelectButton = child.GetComponent<Button>();
        }
    }

    public void ReturnToGuild() => resultController.ReturnToGuild();
    // Keep existing Inspector callbacks pointing at this method working.
    public void OpenNextLevel() => ReturnToGuild();
    public void RetryLevel() => resultController.RetryLevel();
    public void OpenLevelSelect() => resultController.OpenLevelSelect();
}
