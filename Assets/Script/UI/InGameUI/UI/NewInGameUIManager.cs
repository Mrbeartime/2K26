using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Displays the action list and turn counter and handles HUD input and overlays.
/// </summary>
public class NewInGameUIManager : MonoBehaviour
{
    public static NewInGameUIManager Instance { get; private set; }

    #region Fields - Inspector

    [Header("UI References")]
    public GameObject InGameUI;
    public GameObject ActionUI;
    public TextMeshProUGUI TurnLimitText;
    [SerializeField] private RectTransform turnProgressTrack;
    [SerializeField] private Image turnBarPrefab;
    [SerializeField, Range(0f, 0.3f)] private float turnBarGap = 0.05f;
    [SerializeField] private Color turnBarEmptyColor = new Color(1f, 1f, 1f, 0.19607843f);
    [SerializeField, Min(0f)] private float turnProgressDuration = 0.3f;
    public Canvas PauseUI;
    [SerializeField] private OverlayUI guideOverlay;
    [SerializeField] private OverlayUI settingsOverlay;
    private OverlayUI pauseOverlay;

    [Header("Pause Manager")]
    [SerializeField] private float fadeTime = 0.2f;
    [Tooltip("Full-screen black Image. Fades to Pause Dim Alpha while paused.")]
    public Image blackScreen;
    [Range(0f, 1f)]
    [SerializeField] private float pauseDimAlpha = 0.6f;

    [Header("Hazard Schedule")]
    [SerializeField] private RectTransform hazardEventPrefab;
    [SerializeField] private Sprite arrowTrapIcon;
    [SerializeField] private Sprite spikeTrapIcon;
    [SerializeField, Tooltip("Font for the hazard turn number. Uses the Turn Limit Text font when empty.")]
    private TMP_FontAsset hazardTurnFont;

    [Header("Action Lists")]
    public GameObject MoveAction;
    public GameObject InteractAction;
    public GameObject SignatureAction;
    public TextMeshProUGUI SignatureText;

    [Serializable]
    public class CharacterEntry
    {
        [Tooltip("The character in the scene")]
        public PlayerMove character;
        [Tooltip("Optional. Leave empty to use the default name (Lockpick / Bow Shot / Slash)")]
        public string skillName;
        [Tooltip("Signature icon, normal state (sprites[0])")]
        public Sprite signatureIcon;
        [Tooltip("Optional. Signature icon while focused (sprites[1]). Empty = same as Signature Icon")]
        public Sprite signatureIconSelected;
    }

    [Header("Character Signature Settings")]
    [SerializeField] private CharacterEntry[] CharacterList;

    #endregion

    #region Fields - Runtime State

    // ---- External systems ----
    private ProjectDInputAction _inputAction;
    private TurnGameManager turnGameManager;
    private int pendingTurnBarRound;
    private readonly List<TurnBarSlot> turnBarSlots = new();
    private TurnScheduleData hazardSchedule;
    private readonly List<GameObject> hazardEvents = new();
    private HoldButton turnLimitButton;

    private sealed class TurnBarSlot
    {
        public RectTransform Background;
        public RectTransform Fill;
    }

    internal ProjectDInputAction.GameplayControl_NewActions? GameplayInput =>
        isActiveAndEnabled ? _inputAction?.GameplayControl_New : null;

    internal bool BlocksGameplayInput => isActiveAndEnabled &&
        (IsPaused || (guideOverlay != null && guideOverlay.gameObject.activeInHierarchy) ||
        (settingsOverlay != null && settingsOverlay.gameObject.activeInHierarchy));

    // ---- Character focus ----
    private PlayerMove previousCharacter;           // Used to detect when the current character changes (changed -> reset action focus)

    // ---- Actions ----
    // Action ids (not indices): Move = 0, Interact = 1, Signature = 2
    // Must match the actionType enum in CharacterActionList
    private const int ActionMove = 0, ActionInteract = 1, ActionSignature = 2;
    private const int NoAction = -1;

    private readonly List<int> availableActions = new();    // Actions usable right now (sorted in display order)
    private int CurrentFocusActionIndex = 0;                // Index into availableActions (not the id)
    private int selectedAction = NoAction;                  // Action that is "chosen/armed" and awaiting confirmation (NoAction = none)
    // World clicks may confirm Interact only while this action is selected in the HUD.
    public bool IsInteractMode => isActiveAndEnabled && !BlocksGameplayInput && !changingFocus &&
        ActionUI != null && ActionUI.activeInHierarchy && selectedAction == ActionInteract &&
        CurrentFocusActionIndex >= 0 && CurrentFocusActionIndex < availableActions.Count &&
        availableActions[CurrentFocusActionIndex] == ActionInteract &&
        turnGameManager != null && turnGameManager.CanChooseReact &&
        !turnGameManager.IsMoveTargeting && !turnGameManager.IsSkillTargeting;

    // ★ ADDED: action layout values (moved out of RefreshActionList so LayoutActions() can share them)
    //   startY    = bottom offset of the lowest action
    //   gapY      = normal spacing between two actions
    //   focusGapY = spacing used next to the FOCUSED action (keep >= gapY; bigger = more breathing room around it)
    //   posX      = offset from the edge
    private const float startY = 75f, gapY = 84f, posX = 32f, focusGapY = 93f;

    // Actions that need an extra confirm (Space or a second click) before really executing; other actions run immediately
    private static bool NeedsSpaceConfirm(int actionId) => actionId == ActionInteract;

    private bool changingFocus;     // true while inside FocusAndUseAction (prevents RefreshActionList, triggered earlier, from re-selecting)
    private int armedFrame = -1;    // Frame in which the current action was armed (prevents click-confirm in the same frame)
    private int lastSelectFrame = -1;           // Frame in which SelectAction was last called
    private int lastSelectId = NoAction;        //   (guards against onClick firing twice: BindActionButton + CharacterActionList)

    private readonly List<(Button button, UnityEngine.Events.UnityAction callback)> actionButtons = new();
    private readonly HashSet<PlayerMove> warnedSignatureEntry = new();    // Warn about a missing icon only once per character

    // ---- Pause ----
    public bool IsPaused { get; private set; }
    private CanvasGroup inGameGroup;
    private CanvasGroup pauseGroup;
    private Coroutine pauseRoutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _inputAction = new ProjectDInputAction();
    }

    private void OnEnable()
    {
        if (_inputAction == null) return;

        var gameplay = _inputAction.GameplayControl_New;
        gameplay.CharacterChange.performed += OnChangeCharacter;
        gameplay.ActionChange.performed += OnChangeAction;
        gameplay.ActionConfirm.performed += OnConfirmAction;
        gameplay.Pause.performed += OnShowPauseUI;
        gameplay.Reset.performed += OnReset;
        gameplay.Tips.performed += OnToggleTips;
        gameplay.Enable();
    }

    private void OnDisable()
    {
        if (_inputAction == null) return;

        var gameplay = _inputAction.GameplayControl_New;
        gameplay.CharacterChange.performed -= OnChangeCharacter;
        gameplay.ActionChange.performed -= OnChangeAction;
        gameplay.ActionConfirm.performed -= OnConfirmAction;
        gameplay.Pause.performed -= OnShowPauseUI;
        gameplay.Reset.performed -= OnReset;
        gameplay.Tips.performed -= OnToggleTips;
        gameplay.Disable();
    }

    private void OnDestroy()
    {
        foreach (var binding in actionButtons)
            if (binding.button != null) binding.button.onClick.RemoveListener(binding.callback);
        actionButtons.Clear();
        if (turnLimitButton != null)
        {
        }
        _inputAction?.Dispose();
        if (Instance == this) Instance = null;
        if (hazardSchedule != null) hazardSchedule.ScheduleChanged -= RebuildHazardEvents;
        ClearHazardEvents();
        ClearTurnBarSlots();
        if (turnGameManager == null) return;
        turnGameManager.StateChanged -= RefreshFromTurnManager;
        turnGameManager.GameEnded -= OnGameEnded;
    }

    private void Start()
    {
        SetupPause();

        turnGameManager = TurnGameManager.Instance;
        if (turnGameManager == null)
        {
            Debug.LogError("TurnGameManager not found in the scene", this);
            return;
        }

        // Main hook: every time the state changes, the HUD is redrawn through RefreshFromTurnManager
        turnGameManager.StateChanged += RefreshFromTurnManager;
        turnGameManager.GameEnded += OnGameEnded;

        SetUpActionList();
        RefreshFromTurnManager();
    }

    #endregion

    #region Setup (run once)

    private void SetUpActionList()
    {
        BindActionButton(MoveAction, ActionMove);
        BindActionButton(InteractAction, ActionInteract);
        BindActionButton(SignatureAction, ActionSignature);
    }

    private void BindActionButton(GameObject actionObject, int actionId)
    {
        if (actionObject == null) return;

        Button button = actionObject.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            Debug.LogWarning($"{actionObject.name} has no Button component.", actionObject);
            return;
        }

        CharacterActionList item = actionObject.GetComponent<CharacterActionList>();
        if (item != null) item.SetActionType(actionId);
        UnityEngine.Events.UnityAction callback = () => SelectAction(actionId);
        button.onClick.AddListener(callback);
        actionButtons.Add((button, callback));
    }

    #endregion

    #region Turn Manager Sync (game state -> HUD)

    // Called every time TurnGameManager.StateChanged fires
    // Note: CancelMoveTargeting/CancelSkill may fire StateChanged "early" while a click is still being handled
    //       (see changingFocus in Action Selection)
    private void RefreshFromTurnManager()
    {
        if (turnGameManager == null)
        {
            Debug.LogWarning("There is NO TurnGameManager");
            return;
        }

        UpdateTurnCounter();
        RefreshHazardSchedule();

        PlayerMove current = turnGameManager.CurrentPlayer;
        if (current != previousCharacter)
        {
            // Character changed -> reset focus to the first action and clear any pending selection/arm
            CurrentFocusActionIndex = 0;
            previousCharacter = current;
            selectedAction = NoAction;
        }
        RefreshActionList(current);
    }

    public void UpdateTurnCounter()
    {
        TurnGameManager gameManager = TurnGameManager.Instance;
        if (gameManager == null) return;

        int turnsLeft = gameManager.TurnsLeft;
        if (TurnLimitText != null) TurnLimitText.text = turnsLeft.ToString();
        if (turnProgressTrack == null || turnBarPrefab == null) return;

        int maximumTurns = Mathf.Max(1, gameManager.maximumTurns);
        int completedRounds = Mathf.Clamp(maximumTurns - turnsLeft, 0, maximumTurns);
        if (pendingTurnBarRound <= completedRounds || gameManager.IsGameComplete)
            pendingTurnBarRound = 0;

        EnsureTurnBarSlots(maximumTurns);
        for (int i = 0; i < turnBarSlots.Count; i++)
        {
            TurnBarSlot slot = turnBarSlots[i];
            if (i < completedRounds)
            {
                if (slot.Fill == null) CreateTurnBarFill(slot, i + 1, 1f);
                else SetTurnBarFill(slot.Fill, 1f);
            }
            else if (i + 1 != pendingTurnBarRound && slot.Fill != null)
            {
                slot.Fill.gameObject.SetActive(false);
                Destroy(slot.Fill.gameObject);
                slot.Fill = null;
            }
        }
    }

    internal IEnumerator AnimateTurnProgress(TurnGameManager gameManager)
    {
        if (!isActiveAndEnabled || turnProgressTrack == null || turnBarPrefab == null) yield break;

        UpdateTurnCounter();
        int round = gameManager.RoundNumber;
        if (round < 1 || round > turnBarSlots.Count) yield break;

        pendingTurnBarRound = round;
        TurnBarSlot slot = turnBarSlots[round - 1];
        RectTransform fill = CreateTurnBarFill(slot, round, 0f);
        float duration = turnProgressDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            if (this == null || !isActiveAndEnabled || fill == null || gameManager.IsGameComplete)
                yield break;

            elapsed += Time.deltaTime;
            SetTurnBarFill(fill, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
        }
        SetTurnBarFill(fill, 1f);
    }

    private void EnsureTurnBarSlots(int maximumTurns)
    {
        if (turnBarSlots.Count == maximumTurns) return;

        ClearTurnBarSlots();
        float inset = turnBarGap * 0.5f;
        for (int i = 0; i < maximumTurns; i++)
        {
            Image background = Instantiate(turnBarPrefab, turnProgressTrack, false);
            background.name = $"Turn {i + 1} Slot";
            background.color = turnBarEmptyColor;
            RectTransform rect = background.rectTransform;
            rect.SetSiblingIndex(i);
            rect.anchorMin = new Vector2((i + inset) / maximumTurns, 0f);
            rect.anchorMax = new Vector2((i + 1f - inset) / maximumTurns, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            turnBarSlots.Add(new TurnBarSlot { Background = rect });
        }
    }

    private RectTransform CreateTurnBarFill(TurnBarSlot slot, int round, float progress)
    {
        if (slot.Fill == null)
        {
            Image image = Instantiate(turnBarPrefab, slot.Background, false);
            image.name = $"Bar (Turn {round})";
            slot.Fill = image.rectTransform;
            slot.Fill.anchorMin = Vector2.zero;
            slot.Fill.offsetMin = slot.Fill.offsetMax = Vector2.zero;
        }
        SetTurnBarFill(slot.Fill, progress);
        return slot.Fill;
    }

    private static void SetTurnBarFill(RectTransform fill, float progress)
    {
        fill.anchorMax = new Vector2(progress, 1f);
    }

    private void ClearTurnBarSlots()
    {
        foreach (TurnBarSlot slot in turnBarSlots)
        {
            if (slot.Background == null) continue;
            slot.Background.gameObject.SetActive(false);
            Destroy(slot.Background.gameObject);
        }
        turnBarSlots.Clear();
    }

    private void RefreshHazardSchedule()
    {
        if (hazardSchedule == turnGameManager.ScheduleData) return;

        if (hazardSchedule != null) hazardSchedule.ScheduleChanged -= RebuildHazardEvents;
        hazardSchedule = turnGameManager.ScheduleData;
        if (hazardSchedule != null) hazardSchedule.ScheduleChanged += RebuildHazardEvents;
        RebuildHazardEvents();
    }

    private void RebuildHazardEvents()
    {
        ClearHazardEvents();
        if (hazardSchedule == null || hazardEventPrefab == null || turnProgressTrack == null) return;

        int maximumTurns = Mathf.Max(1, turnGameManager.maximumTurns);
        int rounds = Mathf.Min(hazardSchedule.MaximumRounds, maximumTurns);
        float halfGap = turnBarGap * 0.5f;
        for (int round = 1; round <= rounds; round++)
        {
            var trapTypes = hazardSchedule.GetTrapTypes(round);
            if (trapTypes.Count == 0) continue;

            RectTransform marker = Instantiate(hazardEventPrefab, turnProgressTrack, false);
            marker.name = $"HazardEvent (Turn {round})";
            marker.anchorMin = new Vector2((round - halfGap) / maximumTurns, 0.5f);
            marker.anchorMax = new Vector2(Mathf.Min(1f, (round + halfGap) / maximumTurns), 0.5f);
            marker.sizeDelta = new Vector2(0f, marker.sizeDelta.y);
            marker.anchoredPosition = Vector2.zero;
            var group = marker.GetComponent<CanvasGroup>();
            if (group == null) group = marker.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            hazardEvents.Add(marker.gameObject);
            CreateHazardTurnLabel(marker, round);

            var icons = new List<Sprite>(trapTypes.Count);
            foreach (string trapType in trapTypes)
            {
                Sprite sprite = trapType == typeof(TurnArrowTrap).FullName ? arrowTrapIcon :
                    trapType == typeof(TurnSpikeTrap).FullName ? spikeTrapIcon : null;
                if (sprite != null) icons.Add(sprite);
            }
            for (int i = 0; i < icons.Count; i++)
                CreateHazardIcon(marker, icons[i], i - (icons.Count - 1) * 0.5f);
        }
    }

    private void CreateHazardTurnLabel(RectTransform marker, int round)
    {
        var labelObject = new GameObject("Turn Number", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.layer = marker.gameObject.layer;

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = hazardTurnFont;
        if (font == null && TurnLimitText != null) font = TurnLimitText.font;
        if (font != null) label.font = font;
        label.text = round.ToString();
        label.fontSize = 37f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.SetParent(marker, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(56f, 32f);
        rect.anchoredPosition = new Vector2(0f, 13f);
    }

    private static void CreateHazardIcon(RectTransform marker, Sprite sprite, float column)
    {
        const float iconSize = 54f;
        const float spacing = 10f;
        var iconObject = new GameObject(sprite.name, typeof(RectTransform), typeof(Image));
        iconObject.layer = marker.gameObject.layer;
        var icon = iconObject.GetComponent<Image>();
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        RectTransform rect = icon.rectTransform;
        rect.SetParent(marker, false); 
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(iconSize, iconSize);
        rect.anchoredPosition = new Vector2(column * (iconSize + spacing), -spacing);
    }

    private void ClearHazardEvents()
    {
        foreach (GameObject marker in hazardEvents)
        {
            if (marker == null) continue;
            marker.SetActive(false);
            Destroy(marker);
        }
        hazardEvents.Clear();
    }

    private void OnGameEnded(bool won, string reason)
    {
        if (ActionUI != null) ActionUI.SetActive(false);

        Debug.Log(won ? "VICTORY: " + reason : "GAME OVER: " + reason);
    }

    #endregion

    #region Action List (displaying the action list)

    // Rebuilds the action list every time the state changes; this is "the point with the most side effects in the whole UI"
    //  1) Build availableActions from CanChooseMove / CanChooseReact / CanChooseSkill
    //  2) Action under focus: Interact -> arm and wait for confirmation, other actions -> confirm (immediately)
    //  3) Lay out the positions
    //  4) If a Move/Skill mode is already active, move focus to match that action
    public void RefreshActionList(PlayerMove current)
    {
        if (ActionUI == null || MoveAction == null || InteractAction == null || SignatureAction == null) return;

        availableActions.Clear();
        MoveAction.SetActive(false);
        InteractAction.SetActive(false);
        SignatureAction.SetActive(false);

        // Hide all actions when: no current player / game over / enemy phase / waiting for a character to finish moving
        bool show = current != null && !turnGameManager.IsGameComplete && !turnGameManager.IsEnemyPhase &&
            !turnGameManager.IsWaitingForMovement;

        ActionUI.SetActive(show);
        if (!show) { selectedAction = NoAction; return; }

        // The order of Add calls = display order (bottom to top)
        if (turnGameManager.CanChooseMove) availableActions.Add(ActionMove);
        if (turnGameManager.CanChooseReact) availableActions.Add(ActionInteract);
        if (turnGameManager.CanChooseSkill) availableActions.Add(ActionSignature);
        Button skillButton = SignatureAction.GetComponentInChildren<Button>(true);
        if (skillButton != null) skillButton.interactable = turnGameManager.CanChooseSkill;
        if (availableActions.Count == 0)
        {
            selectedAction = NoAction;
            ActionUI.SetActive(false);
            PlayerController.Instance?.ClearHighlights();
            turnGameManager.SkipExhaustedSkill();
            return;
        }

        if (selectedAction != NoAction && !availableActions.Contains(selectedAction)) selectedAction = NoAction;

        if (availableActions.Count > 0)
        {
            CurrentFocusActionIndex = Mathf.Clamp(CurrentFocusActionIndex, 0, availableActions.Count - 1);
            int focusId = availableActions[CurrentFocusActionIndex];

            // If we are inside FocusAndUseAction (Cancel* fired StateChanged early),
            // skip the auto arm/confirm here -> the caller will handle selection itself
            if (!changingFocus)
            {
                if (NeedsSpaceConfirm(focusId)) Arm(focusId);   // Interact: arm it and wait for Space or a second click
                else ConfirmAction(focusId);                    // Move/Signature: confirm immediately
            }
        }

        if (turnGameManager.CurrentPlayer != current || turnGameManager.IsEnemyPhase) return;
        UpdateSignatureUI(current);

        // ★ CHANGED: the layout loop that used to be here now lives in LayoutActions(),
        //   which UpdateActionFocusUI() calls at the end (so the focus gap also updates on keyboard/click focus changes)

        // Sync with the game state: if a mode is already active, make the UI focus that action (state takes precedence)
        int modeAction = turnGameManager.IsMoveTargeting ? ActionMove
               : turnGameManager.IsSkillTargeting ? ActionSignature : -1;
        int modePos = availableActions.IndexOf(modeAction);
        if (modePos >= 0) CurrentFocusActionIndex = modePos;
        if (modeAction != NoAction) selectedAction = modeAction;

        CurrentFocusActionIndex = Mathf.Clamp(CurrentFocusActionIndex, 0, Mathf.Max(0, availableActions.Count - 1));
        UpdateActionFocusUI();
    }

    private GameObject GetActionObject(int id) => id switch
    {
        ActionMove => MoveAction,
        ActionInteract => InteractAction,
        _ => SignatureAction
    };

    // Shows each action as focused (or Unselected otherwise); the visuals (size/position/sprite) are handled by CharacterActionList
    private void UpdateActionFocusUI()
    {
        foreach (GameObject go in new[] { MoveAction, InteractAction, SignatureAction })
        {
            CharacterActionList item = go.GetComponent<CharacterActionList>();
            item.Unselected();
        }

        if (availableActions.Count == 0) return;
        int focusId = availableActions[Mathf.Clamp(CurrentFocusActionIndex, 0, availableActions.Count - 1)];
        CharacterActionList focused = GetActionObject(focusId).GetComponent<CharacterActionList>();
        focused.Selected();

        LayoutActions();   // ★ ADDED: re-space the list every time focus changes
    }

    // ★ ADDED: positions the visible actions bottom -> top.
    //   The gap touching the focused action is focusGapY, every other gap is gapY.
    //   The bottom action stays at startY, so the list only grows upward.
    //   (The focused icon's own size comes from CharacterActionList: selectedIconScale / selectedFontScale)
    private void LayoutActions()
    {
        float y = startY;
        for (int i = 0; i < availableActions.Count; i++)
        {
            if (i > 0)
            {
                bool nearFocus = i == CurrentFocusActionIndex || i - 1 == CurrentFocusActionIndex;
                y += nearFocus ? focusGapY : gapY;
            }

            GameObject go = GetActionObject(availableActions[i]);
            go.SetActive(true);
            go.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, y);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.pivot = new Vector2(rt.pivot.x, 0.5f);
            rt.anchoredPosition = new Vector2(posX, y);
        }
    }

    // ---- Signature (name + per-character icon) ----

    // Sets the name/icon of the Signature action for the current character (data comes from CharacterEntry in the Inspector)
    // Goes through SetSprites because CharacterActionList.Update() rewrites the sprite every frame, so setting image.sprite directly would be overwritten
    private void UpdateSignatureUI(PlayerMove player)
    {
        CharacterEntry entry = CharacterList == null ? null : Array.Find(CharacterList, item => item?.character == player);

        if (SignatureText != null)
        {
            bool hasName = entry != null && !string.IsNullOrEmpty(entry.skillName);
            Character character = player.GetComponent<Character>();
            string count = character != null && character.SkillLimit >= 0
                ? " (" + character.RemainingSkills + "/" + character.SkillLimit + ")" : " (Unlimited)";
            SignatureText.text = (hasName ? entry.skillName : GetSignatureName(player)) + count;
        }

        if (entry != null && entry.signatureIcon != null && SignatureAction != null)
        {
            CharacterActionList signatureUI = SignatureAction.GetComponent<CharacterActionList>();
            if (signatureUI != null) signatureUI.SetSprites(entry.signatureIcon, entry.signatureIconSelected);
        }

        if (player != null && warnedSignatureEntry.Add(player))
        {
            if (entry == null)
                Debug.LogWarning($"[Signature] {player.name} is not in CharacterList, so its signature icon can't be set.", this);
            else if (entry.signatureIcon == null)
                Debug.LogWarning($"[Signature] {player.name} has no Signature Icon assigned in CharacterList.", this);
        }
    }

    // Default skill name, used when skillName is not set in the Inspector; add a case here when adding a new character class
    private static string GetSignatureName(PlayerMove player)
    {
        if (player == null) return string.Empty;
        return player.GetComponent<Character>() switch
        {
            Rogue => "Lockpick",
            ArcherMan => "Bow Shot",
            SwordMan => "Slash",
            _ => "Skill"
        };
    }

    #endregion

    #region Action Selection (select / arm / confirm / cancel)

    // Behavior summary:
    //  - W/S      : move focus and update the active targeting mode
    //  - Space    : confirm the focused action
    //  - Click    : SelectAction() -> a different action = select/arm, the already-armed action = confirm
    //  - Interact needs an extra confirm (NeedsSpaceConfirm): selecting only "arms" it; press Space/click again to confirm
    //  - Move/Signature run immediately on selection (and show target highlights); cancel by selecting another action or GoBack

    // Select/click an action (called from buttons) - the decision point between "arm" and "really execute"
    public void SelectAction(int actionId)
    {
        if (BlocksGameplayInput || turnGameManager == null || !availableActions.Contains(actionId)) return;

        // Guard against onClick firing twice: a single click may call this twice in the same frame
        // Ignore the second call so the click is not counted as a "double click" and Interact fires immediately
        if (Time.frameCount == lastSelectFrame && actionId == lastSelectId) return;
        lastSelectFrame = Time.frameCount;
        lastSelectId = actionId;

        // Confirm only if this action was armed in an earlier frame and focus is still on it
        if (selectedAction == actionId && Time.frameCount > armedFrame &&
            availableActions[CurrentFocusActionIndex] == actionId)
        {
            ConfirmAction(actionId);
            return;
        }
        FocusAndUseAction(availableActions.IndexOf(actionId));
    }

    // Move focus to a new action: cancel the old one -> Interact: arm and wait / others: execute immediately
    private void FocusAndUseAction(int newIndex)
    {
        // During this block, Cancel* may fire StateChanged early -> RefreshActionList must not arm/confirm on its own
        changingFocus = true;
        int id;
        try
        {
            CurrentFocusActionIndex = newIndex;
            CancelPendingAction();
            id = availableActions[CurrentFocusActionIndex];
        }
        finally { changingFocus = false; }

        if (NeedsSpaceConfirm(id))
        {
            selectedAction = NoAction;   // Force a fresh arm so an earlier refresh can't leave stale state
            Arm(id);
            UpdateActionFocusUI();
            return;
        }
        UpdateActionFocusUI();
        ConfirmAction(id);
    }

    // "Arm" an action: set selectedAction so it is ready to confirm (but does not execute yet)
    private void Arm(int id)
    {
        if (selectedAction != id) armedFrame = Time.frameCount;
        selectedAction = id;
    }

    // ===== The point that sends the real command to TurnGameManager (has direct gameplay effects) =====
    private void ConfirmAction(int actionId)
    {
        if (turnGameManager == null) return;
        if (actionId == ActionSignature && !turnGameManager.CanChooseSkill)
        {
            selectedAction = NoAction;
            PlayerController.Instance?.ClearHighlights();
            turnGameManager.SkipExhaustedSkill();
            return;
        }
        switch (actionId)
        {
            case ActionMove:
                if (!turnGameManager.IsMoveTargeting) turnGameManager.ChooseMove();
                break;
            case ActionInteract:
                ConfirmInteract();
                break;
            case ActionSignature:
                if (!turnGameManager.IsSkillTargeting) turnGameManager.ChooseSkill();
                break;
        }
    }

    // Execute Interact for real (calls TurnGameManager.ChooseReact)
    private void ConfirmInteract()
    {
        turnGameManager.ChooseReact();
    }

    // Clears pending selection and exits Move/Skill targeting if active (may fire StateChanged early)
    private void CancelPendingAction()
    {
        selectedAction = NoAction;
        if (turnGameManager == null) return;
        if (turnGameManager.IsMoveTargeting) turnGameManager.CancelMoveTargeting();
        else if (turnGameManager.IsSkillTargeting) turnGameManager.CancelSkill();
    }

    // Go back / cancel: exit the current mode, or if there is none, just clear any armed action
    public void GoBack()
    {
        if (turnGameManager == null) return;
        if (turnGameManager.IsMoveTargeting) turnGameManager.CancelMoveTargeting();
        else if (turnGameManager.IsSkillTargeting) turnGameManager.CancelSkill();
        else selectedAction = NoAction;
        UpdateActionFocusUI();
    }

    public void CancelCurrentAction() => GoBack();

    #endregion

    #region Gameplay Input

    private void OnChangeAction(InputAction.CallbackContext context)
    {
        int count = availableActions.Count;
        if (BlocksGameplayInput || count == 0) return;

        float direction = context.ReadValue<float>();
        if (direction == 0f) return;

        // The list grows upward, so W moves up and S moves down.
        int step = direction < 0f ? 1 : -1;
        FocusAndUseAction((CurrentFocusActionIndex + step + count) % count);
    }

    private void OnConfirmAction(InputAction.CallbackContext context)
    {
        if (BlocksGameplayInput || CurrentFocusActionIndex >= availableActions.Count) return;
        ConfirmAction(availableActions[CurrentFocusActionIndex]);
    }

    private void OnChangeCharacter(InputAction.CallbackContext context)
    {
        if (BlocksGameplayInput || turnGameManager == null) return;

        float direction = context.ReadValue<float>();
        if (direction != 0f) turnGameManager.CyclePlayer(direction > 0f);
    }

    private void OnReset(InputAction.CallbackContext context)
    {
        if (BlocksGameplayInput || turnGameManager == null ||
            ProjectD.Menus.GameSceneManager.IsInGameSettingsOpen) return;

        ProjectD.Menus.GameSceneManager.ReloadCurrentScene();
    }

    private void OnToggleTips(InputAction.CallbackContext context)
    {
        if (guideOverlay == null || ProjectD.Menus.GameSceneManager.IsInGameSettingsOpen ||
            (settingsOverlay != null && settingsOverlay.gameObject.activeInHierarchy)) return;

        if (!TryClosePauseOverlay(guideOverlay)) guideOverlay.ActivateOverlay();
    }

    // HoldButton handles when the turn ends. These callbacks update the button's visuals.

    #endregion

    #region UI Callbacks

    public void OnClickEndTurn()
    {
        if (!BlocksGameplayInput && turnGameManager != null) turnGameManager.ChooseEndTurn();
    }

    #endregion

    #region Pause

    private void LateUpdate()
    {
        if (!IsPaused || pauseGroup == null) return;
        bool childOverlayOpen =
            (guideOverlay != null && guideOverlay.gameObject.activeInHierarchy) ||
            (settingsOverlay != null && settingsOverlay.gameObject.activeInHierarchy);
        pauseGroup.interactable = !childOverlayOpen;
        pauseGroup.blocksRaycasts = !childOverlayOpen;
    }

    public void CallPause() => SetPaused(!IsPaused);

    private void OnShowPauseUI(InputAction.CallbackContext context)
    {
        if (ProjectD.Menus.GameSceneManager.IsInGameSettingsOpen) return;
        if (TryClosePauseOverlay(settingsOverlay) || TryClosePauseOverlay(guideOverlay)) return;
        if (turnGameManager != null && turnGameManager.IsGameComplete)
            return;

        SetPaused(!IsPaused);
    }

    private bool TryClosePauseOverlay(OverlayUI target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        target.DeactivateOverlay();
        if (IsPaused && PauseUI != null)
        {
            if (pauseGroup != null)
            {
                pauseGroup.interactable = true;
                pauseGroup.blocksRaycasts = true;
            }
            foreach (Selectable control in PauseUI.GetComponentsInChildren<Selectable>())
            {
                if (!control.isActiveAndEnabled || !control.IsInteractable()) continue;
                control.Select();
                break;
            }
        }
        return true;
    }

    // Pause/resume: the in-game UI is blocked via CanvasGroup (blocksRaycasts) instead of SetActive(false), so its state is kept
    // Input handlers in this class check IsPaused before doing anything
    public void SetPaused(bool paused)
    {
        // ถ้าเกมจบแล้ว ไม่อนุญาตให้เปิด Pause
        if (paused && turnGameManager != null &&
            turnGameManager.IsGameComplete)
            return;

        if (IsPaused == paused) return;
        ProjectD.Audio.GameAudioManager.Instance?.PlayUI(ProjectD.Audio.AudioCue.Pause);
        IsPaused = paused;

        // Release the end-turn button when the Pause menu opens.

        if (pauseRoutine != null) StopCoroutine(pauseRoutine);
        if (pauseOverlay != null)
        {
            pauseRoutine = null;
            if (inGameGroup != null) inGameGroup.blocksRaycasts = !paused;
            if (pauseGroup != null)
            {
                pauseGroup.interactable = paused;
                pauseGroup.blocksRaycasts = paused;
            }
            if (paused)
                pauseOverlay.ActivateOverlay();
            else
            {
                // Unwind child overlays before restoring the gameplay UI actions.
                if (settingsOverlay != null && settingsOverlay.gameObject.activeSelf)
                    settingsOverlay.DeactivateOverlay();
                if (guideOverlay != null && guideOverlay.gameObject.activeSelf)
                    guideOverlay.DeactivateOverlay();
                pauseOverlay.DeactivateOverlay();
            }
            return;
        }
        pauseRoutine = StartCoroutine(PauseFade(paused));
    }

    private void SetupPause()
    {
        if (InGameUI != null) inGameGroup = GetOrAddCanvasGroup(InGameUI);

        if (PauseUI != null)
        {
            ProjectD.Audio.ButtonAudioFeedback.BindMenu(PauseUI.gameObject);
            pauseOverlay = PauseUI.GetComponent<OverlayUI>();
            pauseGroup = GetOrAddCanvasGroup(PauseUI.gameObject);
            pauseGroup.alpha = pauseOverlay != null ? 1f : 0f;
            PauseUI.gameObject.SetActive(false);
        }
        else Debug.LogWarning("PauseUI is not assigned.", this);

        // OverlayUI owns the background in the shared-overlay version of Pause.
        if (blackScreen != null && pauseOverlay == null)
        {
            SetBlackScreenAlpha(0f);
            blackScreen.gameObject.SetActive(false);
        }
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        if (group == null) group = go.AddComponent<CanvasGroup>();
        return group;
    }

    private void SetBlackScreenAlpha(float alpha)
    {
        if (blackScreen == null) return;
        Color c = blackScreen.color;
        c.a = alpha;
        blackScreen.color = c;
    }

    // Fades blackScreen (to pauseDimAlpha) and PauseUI together, using unscaledDeltaTime (works when timeScale = 0)
    // Adjust the pause/resume fade speed with fadeTime and pauseDimAlpha in the Inspector
    private IEnumerator PauseFade(bool paused)
    {
        if (paused)
        {
            if (inGameGroup != null) inGameGroup.blocksRaycasts = false;   // Block the in-game UI from clicks
            if (PauseUI != null) PauseUI.gameObject.SetActive(true);
            if (pauseGroup != null) { pauseGroup.interactable = true; pauseGroup.blocksRaycasts = true; }
            if (blackScreen != null) blackScreen.gameObject.SetActive(true);
        }
        else if (pauseGroup != null)
        {
            pauseGroup.interactable = false;
            pauseGroup.blocksRaycasts = false;
        }

        float dimFrom = blackScreen != null ? blackScreen.color.a : 0f;
        float dimTo = paused ? pauseDimAlpha : 0f;
        float uiFrom = pauseGroup != null ? pauseGroup.alpha : 0f;
        float uiTo = paused ? 1f : 0f;

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeTime);
            SetBlackScreenAlpha(Mathf.Lerp(dimFrom, dimTo, k));
            if (pauseGroup != null) pauseGroup.alpha = Mathf.Lerp(uiFrom, uiTo, k);
            yield return null;
        }

        SetBlackScreenAlpha(dimTo);
        if (pauseGroup != null) pauseGroup.alpha = uiTo;

        // Resume: hide PauseUI/black screen after the fade, then let the in-game UI receive clicks again
        if (!paused)
        {
            if (PauseUI != null) PauseUI.gameObject.SetActive(false);
            if (blackScreen != null) blackScreen.gameObject.SetActive(false);
            if (inGameGroup != null) inGameGroup.blocksRaycasts = true;
        }
        pauseRoutine = null;
    }

    #endregion
}
