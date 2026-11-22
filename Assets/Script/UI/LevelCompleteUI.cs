using System.Collections;
using ProjectD.Menus;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shows the scene-authored victory or defeat overlay after a confirmed game result.
/// Attach this script to an always-active object, NOT to the panel that it hides.
/// Level progress is recorded by TurnGameManager.ShowVictory(), not this UI.
/// The panel's own animation (turn count, icons, stamps, PaperSheet) lives in LevelCompleteSequence.
/// A key press during the animation only skips it. Once it has finished, buttons appear:
/// victory shows Level Select; defeat (turns ran out or every character died) shows Retry and Level Select.
/// </summary>
public class LevelCompleteUI : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("The LevelCompletePanel in the scene. Hidden until the game ends.")]
    [SerializeField] private GameObject completePanel;

    [Header("Victory Timing")]
    [Tooltip("Wait for active treasure chests in this scene to finish opening.")]
    [SerializeField] private bool waitForChestAnimation = true;
    [Tooltip("Extra pause after the chest opens, before showing the victory panel (seconds).")]
    [SerializeField, Min(0f)] private float delayAfterChestOpen = 0.35f;

    [Header("Buttons (Canvas holding the buttons)")]
    [Tooltip("Shown after the animation on victory. Hook each button's OnClick to this object's OpenLevelSelect / RetryLevel / ReturnToGuild.")]
    [SerializeField] private Canvas victoryButtons;
    [Tooltip("Shown after the animation on defeat (turns ran out or every character died). Empty = use the victory canvas.")]
    [SerializeField] private Canvas defeatButtons;

    // Found automatically; nothing to assign in the Inspector.
    private GameResultController resultController;
    private LevelCompleteSequence resultSequence;
    private TurnGameManager turnManager;
    private bool hasShown;
    private bool panelShown;
    private bool buttonsShown;
    private bool lost;

    private void Awake()
    {
        resultController = GetComponent<GameResultController>();
        if (resultController == null) resultController = gameObject.AddComponent<GameResultController>();

        // The sequence can sit on this object or anywhere inside the panel.
        resultSequence = GetComponent<LevelCompleteSequence>();

        if (completePanel != null)
        {
            if (resultSequence == null)
                resultSequence = completePanel.GetComponentInChildren<LevelCompleteSequence>(true);
            // Keep the controller object active; hide only its separate panel.
            completePanel.SetActive(false);
        }
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

    private void Update()
    {
        if (!panelShown || buttonsShown) return;
        // Presses during the animation only skip it; the buttons wait until it has finished.
        if (resultSequence != null && !resultSequence.IsFinished) return;
        ShowButtons();
    }

    private void ShowButtons()
    {
        buttonsShown = true;
        var hint = completePanel.transform.Find("Skip Hint");
        if (hint != null) hint.gameObject.SetActive(false);

        var shown = lost && defeatButtons != null ? defeatButtons : victoryButtons;
        if (shown == null)
        {
            Debug.LogWarning("LevelCompleteUI: assign the buttons Canvas, otherwise the player cannot leave.", this);
            return;
        }
        shown.gameObject.SetActive(true);
        var first = shown.GetComponentInChildren<Selectable>();
        if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    /// <summary>Visible for the whole sequence, drawn above the PaperSheet and the other nested canvases.</summary>
    private void ShowSkipHint()
    {
        var hint = completePanel.transform.Find("Skip Hint");
        if (hint == null) return;
        hint.gameObject.SetActive(true);
        if (!hint.TryGetComponent(out Canvas canvas)) canvas = hint.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 200;   // above the panel's own order (100)
    }

    private void OnDestroy()
    {
        if (turnManager != null)
            turnManager.GameEnded -= OnGameEnded;
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
        else ShowResult(false);
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

        ShowResult(true);
    }

    private void ShowResult(bool won)
    {
        completePanel.SetActive(true);
        panelShown = true;
        lost = !won;
        // The button canvases stay hidden until the animation has finished.
        if (victoryButtons != null) victoryButtons.gameObject.SetActive(false);
        if (defeatButtons != null) defeatButtons.gameObject.SetActive(false);
        ShowSkipHint();
        FillPaper();

        var manager = resultController.TurnManager;
        if (resultSequence != null && manager != null)
            resultSequence.Play(manager.maximumTurns, manager.TurnsLeft, won, completePanel.transform);
        else if (resultSequence == null)
            Debug.LogWarning("LevelCompleteUI: add LevelCompleteSequence to this object to play the result animation.", this);
    }

    /// <summary>
    /// Puts the same quest information on the result PaperSheet as Level Select shows for this quest:
    /// title, description, saved depth report and the available characters.
    /// </summary>
    private void FillPaper()
    {
        var catalog = GameSceneManager.Catalog;
        if (catalog == null) return;

        string path = gameObject.scene.path;
        string title, description, questId;
        var level = catalog.FindLevelByScene(path);
        if (level != null)
        {
            title = level.Title;
            description = level.description;
            questId = "level:" + level.id;
        }
        else
        {
            var secret = catalog.FindSecretLevelByScene(path);
            if (secret == null) return;
            title = secret.title;
            description = secret.description;
            questId = "secret:" + secret.id;
        }

        SetText("Paper Title", title);
        SetText("Paper Description", description);
        // Level Select already saved this quest's depth report in level-progress.json; just read it back.
        SetText("Depth", LevelProgressStore.GetOrCreateQuestDepthDescription(questId, () => null)
            ?? "ยังไม่มีรายงานความยากของภารกิจนี้");

        var paperIcons = completePanel.transform.Find("PaperSheet/Quest Details/Character Icons");
        if (paperIcons != null && paperIcons.TryGetComponent(out LevelCompleteCharacterIcons icons)) icons.Prepare();
    }

    private void SetText(string objectName, string value)
    {
        foreach (var text in completePanel.GetComponentsInChildren<TMP_Text>(true))
            if (text.name == objectName) { text.text = value; return; }
    }

    // Same navigation as the rest of the project: GameSceneManager.OpenLevelSelect() through GameResultController.
    // These can also be hooked to a button's OnClick in the Inspector.
    public void OpenLevelSelect() => resultController.OpenLevelSelect();
    public void RetryLevel() => resultController.RetryLevel();
    public void ReturnToGuild() => resultController.ReturnToGuild();
    public void OpenNextLevel() => ReturnToGuild();
}
