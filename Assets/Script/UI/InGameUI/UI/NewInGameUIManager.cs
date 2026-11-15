using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Updates the in-game HUD and routes its selection and overlay input.
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

    [Header("Character Icons and Signatures")]
    [SerializeField] private RectTransform characterIconContainer;
    [SerializeField, Tooltip("Character slots ordered from top to bottom.")]
    private Image[] characterIconReferences;
    [SerializeField] private NewCharacterIcon characterIconPrefab;
    [SerializeField, Min(0f)] private float characterIconSpacing = 105f;
    [SerializeField, Min(0f)] private float characterConnectorWidth = 3f;
    [SerializeField] private CharacterEntry[] CharacterList;

    [Header("Action List")]
    [SerializeField, Tooltip("Center of the Action menu for the middle and bottom character rows.")]
    private RectTransform actionPositionReference;
    [SerializeField, Tooltip("Top, middle, and bottom Action slots.")]
    private Image[] actionIconReferences;
    [SerializeField] private NewCharacterIcon actionIconPrefab;
    [SerializeField] private Sprite moveActionIcon;
    [SerializeField] private Sprite interactActionIcon;
    [SerializeField] private Vector2 actionOuterOffset = new Vector2(-40f, 65f);

    [Header("Action Menu Animation")]
    [SerializeField, Min(0f)] private float actionOpeningDuration = 0.25f;
    [SerializeField, Tooltip("Movement from the character icon (0) to the Action slot (1). Runs backward when closing.")]
    private AnimationCurve actionOpeningMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Tooltip("Opacity from transparent (0) to opaque (1). Runs backward when closing.")]
    private AnimationCurve actionOpeningOpacityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Serializable]
    public class CharacterEntry
    {
        [Tooltip("Optional scene character. A direct match takes priority over the character type.")]
        public PlayerMove character;
        [Tooltip("Character type used when there is no direct scene character match.")]
        public CharacterType characterType;
        public Sprite portrait;
        [Tooltip("Optional. Leave empty to use the default name (Lockpick / Bow Shot / Slash)")]
        public string skillName;
        [Tooltip("Signature icon in the normal state.")]
        public Sprite signatureIcon;
        [Tooltip("Optional. Uses the normal Signature icon when empty.")]
        public Sprite signatureIconSelected;
    }

    public enum CharacterType
    {
        Unspecified,
        Sword,
        Archer,
        Rogue
    }

    #endregion

    #region Fields - Runtime State

    private ProjectDInputAction inputActions;
    private TurnGameManager turnGameManager;
    private int pendingTurnBarRound;
    private readonly List<TurnBarSlot> turnBarSlots = new();
    private TurnScheduleData hazardSchedule;
    private readonly List<RectTransform> hazardEvents = new();

    private const int MaximumCharacterIcons = 5;
    private readonly List<(PlayerMove player, NewCharacterIcon icon, CanvasGroup group)> characterIcons = new();
    private readonly List<RectTransform> characterConnectors = new();

    private sealed class TurnBarSlot
    {
        public RectTransform Background;
        public RectTransform Fill;
    }

    internal ProjectDInputAction.GameplayControl_NewActions? GameplayInput =>
        isActiveAndEnabled ? inputActions?.GameplayControl_New : null;

    internal bool BlocksGameplayInput => isActiveAndEnabled &&
        (IsPaused || HasOpenChildOverlay);

    private bool HasOpenChildOverlay =>
        (guideOverlay != null && guideOverlay.gameObject.activeInHierarchy) ||
        (settingsOverlay != null && settingsOverlay.gameObject.activeInHierarchy);

    private PlayerMove previousCharacter;

    // Action IDs used by SelectAction and the action icon array.
    private const int ActionMove = 0, ActionInteract = 1, ActionSignature = 2;
    private const int NoAction = -1;

    private readonly List<int> availableActions = new();
    private int focusedActionIndex;
    private int FocusedAction => focusedActionIndex < availableActions.Count
        ? availableActions[focusedActionIndex]
        : NoAction;
    private int selectedAction = NoAction;
    private bool choosingAction;
    // World clicks may confirm Interact only while this action is selected in the HUD.
    public bool IsInteractMode => isActiveAndEnabled && choosingAction && !BlocksGameplayInput && !changingFocus &&
        ActionUI != null && ActionUI.activeInHierarchy && selectedAction == ActionInteract &&
        FocusedAction == ActionInteract &&
        turnGameManager != null && turnGameManager.CanChooseReact &&
        !turnGameManager.IsMoveTargeting && !turnGameManager.IsSkillTargeting;

    private readonly NewCharacterIcon[] actionIcons = new NewCharacterIcon[3];
    private readonly RectTransform[] actionIconSlots = new RectTransform[3];
    private float actionListRestY;
    private CanvasGroup actionListGroup;
    private float? actionTransitionStartedAt;
    private bool actionListClosing;
    private Sprite defaultSignatureIcon;
    private Sprite focusedSignatureIcon;

    private bool changingFocus;
    private int armedFrame = -1;
    private int lastSelectFrame = -1;
    private int lastSelectId = NoAction;

    public bool IsPaused { get; private set; }
    private OverlayUI pauseOverlay;
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

        HideIconReferences(characterIconReferences);
        HideIconReferences(actionIconReferences);

        // Keep these groups alive when the entrance cutscene removes its temporary groups.
        if (InGameUI != null) inGameGroup = GetOrAddCanvasGroup(InGameUI);
        if (PauseUI != null) pauseGroup = GetOrAddCanvasGroup(PauseUI.gameObject);
        if (ActionUI != null)
        {
            actionListGroup = GetOrAddCanvasGroup(ActionUI);
            ActionUI.SetActive(false);
        }
        inputActions = new ProjectDInputAction();
    }

    private void OnEnable()
    {
        if (inputActions == null) return;

        var gameplay = inputActions.GameplayControl_New;
        gameplay.CharacterChange.performed += OnChangeSelectionMode;
        gameplay.ActionChange.performed += OnNavigateSelection;
        gameplay.ActionConfirm.performed += OnConfirmAction;
        gameplay.Pause.performed += OnShowPauseUI;
        gameplay.Reset.performed += OnReset;
        gameplay.Tips.performed += OnToggleTips;
        gameplay.Enable();
    }

    private void OnDisable()
    {
        if (inputActions == null) return;

        var gameplay = inputActions.GameplayControl_New;
        gameplay.CharacterChange.performed -= OnChangeSelectionMode;
        gameplay.ActionChange.performed -= OnNavigateSelection;
        gameplay.ActionConfirm.performed -= OnConfirmAction;
        gameplay.Pause.performed -= OnShowPauseUI;
        gameplay.Reset.performed -= OnReset;
        gameplay.Tips.performed -= OnToggleTips;
        gameplay.Disable();
    }

    private void OnDestroy()
    {
        foreach (RectTransform slot in actionIconSlots)
            if (slot != null) Destroy(slot.gameObject);
        inputActions?.Dispose();
        if (Instance == this) Instance = null;
        if (hazardSchedule != null) hazardSchedule.ScheduleChanged -= RebuildHazardEvents;
        ClearHazardEvents();
        ClearTurnBarSlots();
        ClearCharacterIcons();
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

        turnGameManager.StateChanged += RefreshFromTurnManager;
        turnGameManager.GameEnded += OnGameEnded;

        SetUpActionList();
        RefreshFromTurnManager();
    }

    #endregion

    #region Setup (run once)

    private void SetUpActionList()
    {
        if (ActionUI == null || actionIconPrefab == null) return;

        if (ActionUI.transform is RectTransform actionList)
            actionListRestY = actionList.anchoredPosition.y;

        CreateActionIcon(ActionMove, "Move", moveActionIcon);
        CreateActionIcon(ActionInteract, "Interact", interactActionIcon);
        CreateActionIcon(ActionSignature, "Signature", null);
    }

    private static void HideIconReferences(Image[] references)
    {
        if (references == null) return;
        foreach (Image reference in references)
            if (reference != null) reference.enabled = false;
    }

    private static void AlignToIconReference(RectTransform icon, Image[] references, int index)
    {
        if (references == null || index >= references.Length || references[index] == null) return;

        RectTransform reference = references[index].rectTransform;
        if (reference.parent == icon.parent)
        {
            icon.anchoredPosition += (Vector2)(GetCenterInParent(reference) - GetCenterInParent(icon));
            return;
        }

        Vector3 center = reference.TransformPoint(reference.rect.center);
        Vector3 iconCenter = icon.TransformPoint(icon.rect.center);
        icon.anchoredPosition += (Vector2)icon.parent.InverseTransformVector(center - iconCenter);
    }

    private void CreateActionIcon(int actionId, string title, Sprite sprite)
    {
        var slotObject = new GameObject(title + " Slot", typeof(RectTransform));
        slotObject.layer = ActionUI.layer;
        var slot = (RectTransform)slotObject.transform;
        slot.SetParent(ActionUI.transform, false);
        slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.5f);
        slot.sizeDelta = Vector2.zero;
        actionIconSlots[actionId] = slot;

        NewCharacterIcon icon = Instantiate(actionIconPrefab, slot, false);
        var rect = (RectTransform)icon.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        icon.name = title;
        icon.SetLabel(title);
        icon.SetPortrait(sprite);
        icon.BindClick(() => SelectAction(actionId));
        actionIcons[actionId] = icon;
    }

    #endregion

    #region Character Icons and HUD Updates

    private void RefreshFromTurnManager()
    {
        if (turnGameManager == null) return;

        UpdateTurnCounter();
        RefreshHazardSchedule();

        PlayerMove current = turnGameManager.CurrentPlayer;
        RefreshCharacterIcons(current);
        if (current != previousCharacter)
        {
            previousCharacter = current;
            CloseActionList();
        }
        RefreshActionList(current);
    }

    private void RefreshCharacterIcons(PlayerMove selectedPlayer)
    {
        if (characterIconContainer == null || characterIconPrefab == null) return;

        IReadOnlyList<PlayerMove> players = turnGameManager.Players;
        int count = Mathf.Min(players.Count, MaximumCharacterIcons);
        if (!CharacterIconsMatch(players, count)) BuildCharacterIcons(players, count);

        UpdateCharacterSelection(selectedPlayer);
    }

    private void UpdateCharacterSelection(PlayerMove selectedPlayer)
    {
        bool canSelectCharacters = !turnGameManager.IsGameComplete &&
            !turnGameManager.IsEnemyPhase && !turnGameManager.IsWaitingForMovement;
        bool actionsFocused = choosingAction && ActionUI != null && ActionUI.activeSelf;
        foreach (var entry in characterIcons)
        {
            bool selected = entry.player == selectedPlayer;
            bool selectable = canSelectCharacters && !turnGameManager.IsPlayerFinished(entry.player);
            entry.icon.SetSelected(selected);
            entry.icon.SetFocused(selected && selectable && !actionsFocused);
            entry.group.alpha = selectable ? 1f : 0.5f;
        }
    }

    private void UpdateCharacterConnectors()
    {
        for (int i = 0; i < characterConnectors.Count; i++)
        {
            RectTransform upper = (RectTransform)characterIcons[i].icon.transform;
            RectTransform lower = (RectTransform)characterIcons[i + 1].icon.transform;
            Vector2 upperEdge = upper.anchoredPosition + Vector2.up * (upper.rect.yMin * upper.localScale.y);
            Vector2 lowerEdge = lower.anchoredPosition + Vector2.up * (lower.rect.yMax * lower.localScale.y);
            Vector2 gap = upperEdge - lowerEdge;
            RectTransform connector = characterConnectors[i];
            connector.anchoredPosition = (upperEdge + lowerEdge) * 0.5f;
            connector.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, gap));
            connector.sizeDelta = new Vector2(characterConnectorWidth, gap.y > 0f ? gap.magnitude : 0f);
        }
    }

    private bool CharacterIconsMatch(IReadOnlyList<PlayerMove> players, int count)
    {
        if (characterIcons.Count != count) return false;
        for (int i = 0; i < count; i++)
            if (characterIcons[i].player != players[i]) return false;
        return true;
    }

    private void BuildCharacterIcons(IReadOnlyList<PlayerMove> players, int count)
    {
        ClearCharacterIcons();
        for (int i = 1; i < count; i++)
        {
            var line = new GameObject("Character Connector", typeof(RectTransform), typeof(Image));
            line.layer = characterIconContainer.gameObject.layer;
            Image image = line.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.SetParent(characterIconContainer, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            characterConnectors.Add(rect);
        }

        for (int i = 0; i < count; i++)
        {
            PlayerMove player = players[i];
            NewCharacterIcon icon = Instantiate(characterIconPrefab, characterIconContainer, false);
            icon.name = $"Character Icon ({player.name})";
            CharacterEntry entry = FindCharacterEntry(player, player.GetComponent<Character>());
            icon.SetPortrait(entry?.portrait);

            RectTransform rect = (RectTransform)icon.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, ((count - 1) * 0.5f - i) * characterIconSpacing);
            int referenceIndex = (MaximumCharacterIcons - count) / 2 + i;
            AlignToIconReference(rect, characterIconReferences, referenceIndex);
            characterIcons.Add((player, icon, GetOrAddCanvasGroup(icon.gameObject)));

            icon.BindClick(() =>
            {
                if (!BlocksGameplayInput && isActiveAndEnabled && turnGameManager != null)
                    turnGameManager.TrySelectPlayer(player);
            });
        }
    }

    private void ClearCharacterIcons()
    {
        foreach (var entry in characterIcons)
            if (entry.icon != null) RemoveGraphic(entry.icon.transform);
        foreach (RectTransform connector in characterConnectors)
            RemoveGraphic(connector);
        characterIcons.Clear();
        characterConnectors.Clear();
    }

    private static void RemoveGraphic(Transform graphic)
    {
        if (graphic == null) return;
        graphic.gameObject.SetActive(false);
        Destroy(graphic.gameObject);
    }

    private void OnGameEnded(bool won, string reason)
    {
        CloseActionList();
        Debug.Log(won ? "VICTORY: " + reason : "GAME OVER: " + reason);
    }

    #endregion

    #region Turn Progress and Hazard Schedule

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
        for (int round = 1; round <= turnBarSlots.Count; round++)
        {
            TurnBarSlot slot = turnBarSlots[round - 1];
            if (round <= completedRounds)
            {
                SetTurnBarProgress(slot, round, 1f);
            }
            else if (round != pendingTurnBarRound && slot.Fill != null)
            {
                RemoveGraphic(slot.Fill);
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
        RectTransform fill = SetTurnBarProgress(slot, round, 0f);
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

    private RectTransform SetTurnBarProgress(TurnBarSlot slot, int round, float progress)
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
            RemoveGraphic(slot.Background);
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
            // Center the prefab in the gap while keeping its authored width.
            float gapStart = (round - halfGap) / maximumTurns;
            float gapEnd = Mathf.Min(1f, (round + halfGap) / maximumTurns);
            marker.anchorMin = marker.anchorMax = new Vector2((gapStart + gapEnd) * 0.5f, 0.5f);
            marker.anchoredPosition = Vector2.zero;
            hazardEvents.Add(marker);
            CreateHazardTurnLabel(marker, round);

            var icons = new List<(string trapType, Sprite sprite)>(trapTypes.Count);
            foreach (string trapType in trapTypes)
            {
                Sprite sprite = trapType == typeof(TurnArrowTrap).FullName ? arrowTrapIcon :
                    trapType == typeof(TurnSpikeTrap).FullName ? spikeTrapIcon : null;
                if (sprite != null) icons.Add((trapType, sprite));
            }
            for (int i = 0; i < icons.Count; i++)
            {
                var (trapType, sprite) = icons[i];
                CreateHazardIcon(marker, sprite, i - (icons.Count - 1) * 0.5f, round, trapType);
            }
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

    private void CreateHazardIcon(RectTransform marker, Sprite sprite, float column, int round, string trapType)
    {
        const float iconSize = 54f;
        const float spacing = 10f;
        var iconObject = new GameObject(sprite.name, typeof(RectTransform), typeof(Image), typeof(Button));
        iconObject.layer = marker.gameObject.layer;
        var icon = iconObject.GetComponent<Image>();
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.raycastTarget = true;

        RectTransform rect = icon.rectTransform;
        rect.SetParent(marker, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(iconSize, iconSize);
        rect.anchoredPosition = new Vector2(column * (iconSize + spacing), -spacing);

        var button = iconObject.GetComponent<Button>();
        button.targetGraphic = icon;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        TurnTrap lastFocusedTrap = null;
        button.onClick.AddListener(() =>
        {
            CameraFocusManager camera = CameraFocusManager.Instance;
            if (!isActiveAndEnabled || BlocksGameplayInput || hazardSchedule == null ||
                turnGameManager == null || turnGameManager.IsIntroPlaying || turnGameManager.IsGameComplete ||
                Time.timeScale == 0f || camera == null || !camera.isActiveAndEnabled)
                return;

            List<TurnTrap> traps = hazardSchedule.GetActiveTraps(round, trapType);
            if (traps.Count == 0) return;
            int next = (traps.IndexOf(lastFocusedTrap) + 1) % traps.Count;
            lastFocusedTrap = traps[next];
            camera.FocusObject(lastFocusedTrap.transform);
        });
    }

    private void ClearHazardEvents()
    {
        foreach (RectTransform marker in hazardEvents)
            RemoveGraphic(marker);
        hazardEvents.Clear();
    }

    #endregion

    #region Action List

    public void RefreshActionList(PlayerMove current)
    {
        if (!choosingAction || ActionUI == null || actionIcons[ActionMove] == null) return;

        int previousFocus = FocusedAction;
        availableActions.Clear();

        bool show = current != null && !turnGameManager.IsGameComplete && !turnGameManager.IsEnemyPhase &&
            !turnGameManager.IsWaitingForMovement;
        if (show)
        {
            if (turnGameManager.CanChooseReact) availableActions.Add(ActionInteract);
            if (turnGameManager.CanChooseSkill) availableActions.Add(ActionSignature);
            if (turnGameManager.CanChooseMove) availableActions.Add(ActionMove);
        }
        if (availableActions.Count == 0)
        {
            selectedAction = NoAction;
            actionTransitionStartedAt = null;
            SetActionListVisible(false);
            if (show)
            {
                CloseActionList();
                PlayerController.Instance?.ClearHighlights();
                turnGameManager.SkipExhaustedSkill();
            }
            return;
        }

        if (selectedAction != NoAction && !availableActions.Contains(selectedAction)) selectedAction = NoAction;

        int modeAction = turnGameManager.IsMoveTargeting ? ActionMove :
            turnGameManager.IsSkillTargeting ? ActionSignature : NoAction;
        int focus = availableActions.IndexOf(modeAction);
        if (focus < 0) focus = availableActions.IndexOf(previousFocus);
        if (focus < 0) focus = availableActions.IndexOf(ActionMove);
        focusedActionIndex = Mathf.Max(0, focus);

        if (availableActions.Contains(ActionSignature)) UpdateSignatureUI(current);
        PositionActionList(current);
        if (actionListClosing || !ActionUI.activeSelf)
        {
            float progress = actionListClosing ? GetActionListProgress() : 0f;
            actionListClosing = false;
            actionTransitionStartedAt = Time.unscaledTime - progress * actionOpeningDuration;
            SetActionListVisible(true);
        }
        UpdateActionFocusUI();

        if (changingFocus) return;
        int action = FocusedAction;
        if (action == ActionInteract) Arm(action);
        else
        {
            if (modeAction == action) selectedAction = action;
            ConfirmAction(action);
        }
    }

    private void PositionActionList(PlayerMove current)
    {
        if (ActionUI.transform is not RectTransform actionList || actionList.parent == null) return;

        Transform parent = actionList.parent;
        Vector2 position = actionList.anchoredPosition;
        position.y = actionListRestY;
        if (actionPositionReference != null)
        {
            Vector3 offset = actionPositionReference.parent == parent
                ? GetCenterInParent(actionPositionReference) - GetCenterInParent(actionList)
                : parent.InverseTransformVector(actionPositionReference.TransformPoint(actionPositionReference.rect.center) -
                    actionList.TransformPoint(actionList.rect.center));
            position = actionList.anchoredPosition + (Vector2)offset;
        }

        // Keep the bottom character's menu at the reference to leave space below it.
        if (characterIcons.Count > 0 && characterIcons[^1].player == current)
        {
            actionList.anchoredPosition = position;
            return;
        }

        RectTransform characterIcon = FindCharacterIcon(current);
        if (characterIcon != null)
        {
            Vector3 offset = GetCenterInParent(characterIcon) - (Vector3)characterIconContainer.rect.center;
            Vector3 parentOffset = characterIconContainer.parent == parent
                ? characterIconContainer.localRotation * Vector3.Scale(offset, characterIconContainer.localScale)
                : parent.InverseTransformVector(characterIconContainer.TransformVector(offset));
            position += (Vector2)parentOffset;
        }
        actionList.anchoredPosition = position;
    }

    private RectTransform FindCharacterIcon(PlayerMove player)
    {
        foreach (var entry in characterIcons)
            if (entry.player == player && entry.icon != null) return (RectTransform)entry.icon.transform;
        return null;
    }

    private static Vector3 GetCenterInParent(RectTransform rect)
    {
        return rect.localPosition + rect.localRotation * Vector3.Scale(rect.rect.center, rect.localScale);
    }

    private void UpdateActionFocusUI()
    {
        for (int actionId = 0; actionId < actionIcons.Length; actionId++)
        {
            NewCharacterIcon icon = actionIcons[actionId];
            int index = availableActions.IndexOf(actionId);
            actionIconSlots[actionId].gameObject.SetActive(index >= 0);
            if (index < 0) continue;

            bool selected = index == focusedActionIndex;
            icon.SetSelected(selected);
            icon.SetFocused(selected);
            if (actionId == ActionSignature)
                icon.SetPortrait(selected ? focusedSignatureIcon : defaultSignatureIcon);
        }
        UpdateActionIconLayout();
    }

    private void UpdateActionIconLayout()
    {
        float progress = GetActionListProgress();
        if (ActionUI != null && ActionUI.transform is RectTransform actionList)
            ApplyActionIconLayout(actionList, progress);

        if (actionListClosing && progress <= 0f)
            CloseActionList();
        else if (!actionListClosing && progress >= 1f)
            actionTransitionStartedAt = null;
    }

    private float GetActionListProgress()
    {
        float elapsed = actionTransitionStartedAt is float startedAt && actionOpeningDuration > 0f
            ? Mathf.Clamp01((Time.unscaledTime - startedAt) / actionOpeningDuration)
            : 1f;
        return actionListClosing ? 1f - elapsed : elapsed;
    }

    private void ApplyActionIconLayout(RectTransform actionList, float progress)
    {
        float movement = EvaluateActionOpeningCurve(actionOpeningMoveCurve, progress);
        if (actionListGroup != null)
            actionListGroup.alpha = Mathf.Clamp01(EvaluateActionOpeningCurve(actionOpeningOpacityCurve, progress));

        Vector2 origin = Vector2.zero;
        if (progress < 1f)
        {
            RectTransform characterIcon = FindCharacterIcon(turnGameManager.CurrentPlayer);
            if (characterIcon != null)
            {
                Vector3 offset = characterIconContainer.parent == actionList.parent
                    ? characterIconContainer.localPosition + characterIconContainer.localRotation *
                        Vector3.Scale(GetCenterInParent(characterIcon), characterIconContainer.localScale) -
                        GetCenterInParent(actionList)
                    : actionList.parent.InverseTransformVector(characterIcon.TransformPoint(characterIcon.rect.center) -
                        actionList.TransformPoint(actionList.rect.center));
                origin = (Vector2)ParentOffsetToActionSpace(actionList, offset);
            }
        }

        for (int i = 0; i < availableActions.Count; i++)
        {
            // Assign the top slot only when all three actions are available.
            int slot = availableActions.Count == 3 ? i - 1 : i;
            Vector2 target = GetActionSlotPosition(actionList, slot);
            actionIconSlots[availableActions[i]].anchoredPosition = Vector2.LerpUnclamped(origin, target, movement);
        }
    }

    private static float EvaluateActionOpeningCurve(AnimationCurve curve, float time)
    {
        // Always start at the icon and finish at the slot, regardless of the curve's endpoints.
        return time <= 0f || time >= 1f || curve == null ? time : curve.Evaluate(time);
    }

    private Vector2 GetActionSlotPosition(RectTransform actionList, int slot)
    {
        int index = slot + 1;
        Vector2 fallback = slot == 0 ? Vector2.zero : new Vector2(actionOuterOffset.x, -slot * actionOuterOffset.y);
        if (actionPositionReference == null || actionIconReferences == null ||
            index >= actionIconReferences.Length || actionIconReferences[index] == null)
            return fallback;

        RectTransform reference = actionIconReferences[index].rectTransform;
        if (slot == 0 && reference == actionPositionReference) return Vector2.zero;

        Vector2 position;
        if (reference.parent == actionPositionReference)
        {
            // The top and bottom guides are centered children of the middle guide.
            position = reference.anchoredPosition;
        }
        else
        {
            Vector3 center = reference.TransformPoint(reference.rect.center);
            Vector3 origin = actionPositionReference.TransformPoint(actionPositionReference.rect.center);
            position = actionList.InverseTransformVector(center - origin);
        }

        return slot != 0 && position == Vector2.zero ? fallback : position;
    }

    private static Vector3 ParentOffsetToActionSpace(RectTransform actionList, Vector3 offset)
    {
        return Matrix4x4.TRS(Vector3.zero, actionList.localRotation, actionList.localScale)
            .inverse.MultiplyVector(offset);
    }

    private void UpdateSignatureUI(PlayerMove player)
    {
        Character character = player.GetComponent<Character>();
        CharacterEntry entry = FindCharacterEntry(player, character);
        string title = !string.IsNullOrEmpty(entry?.skillName) ? entry.skillName : GetSignatureName(character);
        string count = character != null && character.SkillLimit >= 0
            ? $" ({character.RemainingSkills}/{character.SkillLimit})" : " (Unlimited)";
        actionIcons[ActionSignature].SetLabel(title + count);

        defaultSignatureIcon = entry?.signatureIcon;
        focusedSignatureIcon = entry?.signatureIconSelected;
        if (focusedSignatureIcon == null) focusedSignatureIcon = defaultSignatureIcon;
    }

    private CharacterEntry FindCharacterEntry(PlayerMove player, Character character)
    {
        if (CharacterList == null) return null;

        CharacterType characterType = character switch
        {
            SwordMan => CharacterType.Sword,
            ArcherMan => CharacterType.Archer,
            Rogue => CharacterType.Rogue,
            _ => CharacterType.Unspecified
        };

        CharacterEntry typeMatch = null;
        foreach (CharacterEntry entry in CharacterList)
        {
            if (entry == null) continue;
            if (entry.character == player) return entry;
            if (typeMatch == null && characterType != CharacterType.Unspecified &&
                entry.characterType == characterType)
                typeMatch = entry;
        }
        return typeMatch;
    }

    private static string GetSignatureName(Character character)
    {
        return character switch
        {
            Rogue => "Lockpick",
            ArcherMan => "Bow Shot",
            SwordMan => "Slash",
            _ => "Skill"
        };
    }

    #endregion

    #region Action Selection

    public void SelectAction(int actionId)
    {
        if (!choosingAction || BlocksGameplayInput || turnGameManager == null) return;
        int index = availableActions.IndexOf(actionId);
        if (index < 0) return;

        // A duplicate callback in the same frame must not confirm Interact.
        if (Time.frameCount == lastSelectFrame && actionId == lastSelectId) return;
        lastSelectFrame = Time.frameCount;
        lastSelectId = actionId;

        if (selectedAction == actionId && Time.frameCount > armedFrame &&
            focusedActionIndex == index)
        {
            ConfirmAction(actionId);
            return;
        }
        FocusAndUseAction(index);
    }

    private void FocusAndUseAction(int newIndex)
    {
        // Canceling targeting can refresh the list before the new action has focus.
        changingFocus = true;
        int id = availableActions[newIndex];
        try
        {
            focusedActionIndex = newIndex;
            CancelPendingAction();
        }
        finally { changingFocus = false; }

        int focus = availableActions.IndexOf(id);
        if (focus < 0) return;
        focusedActionIndex = focus;

        if (id == ActionInteract) Arm(id);
        UpdateActionFocusUI();
        if (id != ActionInteract) ConfirmAction(id);
    }

    private void Arm(int id)
    {
        if (selectedAction != id) armedFrame = Time.frameCount;
        selectedAction = id;
    }

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
                turnGameManager.ChooseReact();
                break;
            case ActionSignature:
                if (!turnGameManager.IsSkillTargeting) turnGameManager.ChooseSkill();
                break;
        }
    }

    private void CancelPendingAction()
    {
        selectedAction = NoAction;
        if (turnGameManager == null) return;
        if (turnGameManager.IsMoveTargeting) turnGameManager.CancelMoveTargeting();
        else if (turnGameManager.IsSkillTargeting) turnGameManager.CancelSkill();
    }

    public void GoBack()
    {
        if (!choosingAction) return;

        // Give focus back before canceling targeting, which can raise StateChanged.
        if (ActionUI != null && ActionUI.activeSelf && availableActions.Count > 0 && actionOpeningDuration > 0f)
        {
            float progress = GetActionListProgress();
            choosingAction = false;
            actionListClosing = true;
            actionTransitionStartedAt = Time.unscaledTime - (1f - progress) * actionOpeningDuration;
            actionIcons[FocusedAction].SetFocused(false);
            if (actionListGroup != null) actionListGroup.blocksRaycasts = false;
            UpdateCharacterSelection(turnGameManager.CurrentPlayer);
            UpdateActionIconLayout();
        }
        else
        {
            CloseActionList();
        }
        CancelPendingAction();
    }

    private void CloseActionList()
    {
        choosingAction = false;
        actionTransitionStartedAt = null;
        actionListClosing = false;
        focusedActionIndex = 0;
        selectedAction = NoAction;
        availableActions.Clear();
        SetActionListVisible(false);
    }

    private void SetActionListVisible(bool visible)
    {
        if (ActionUI == null) return;

        ActionUI.SetActive(visible);
        if (actionListGroup != null) actionListGroup.blocksRaycasts = true;
        if (turnGameManager != null) UpdateCharacterSelection(turnGameManager.CurrentPlayer);
    }

    public void CancelCurrentAction() => GoBack();

    #endregion

    #region Gameplay Input

    private void OnNavigateSelection(InputAction.CallbackContext context)
    {
        if (BlocksGameplayInput || turnGameManager == null) return;

        float direction = context.ReadValue<float>();
        if (direction == 0f) return;

        if (!choosingAction)
        {
            turnGameManager.CyclePlayer(direction > 0f);
            return;
        }

        int count = availableActions.Count;
        if (count < 2) return;
        int step = direction < 0f ? -1 : 1;
        FocusAndUseAction((focusedActionIndex + step + count) % count);
    }

    private void OnConfirmAction(InputAction.CallbackContext context)
    {
        if (!choosingAction || BlocksGameplayInput) return;
        int action = FocusedAction;
        if (action != NoAction) ConfirmAction(action);
    }

    private void OnChangeSelectionMode(InputAction.CallbackContext context)
    {
        if (BlocksGameplayInput || turnGameManager == null) return;

        float direction = context.ReadValue<float>();
        if (direction < 0f)
        {
            GoBack();
            return;
        }

        PlayerMove current = turnGameManager.CurrentPlayer;
        if (direction == 0f || choosingAction || current == null || actionIcons[ActionMove] == null ||
            turnGameManager.IsGameComplete || turnGameManager.IsEnemyPhase || turnGameManager.IsWaitingForMovement)
            return;

        choosingAction = true;
        RefreshActionList(current);
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
        UpdateCharacterConnectors();
        if (actionTransitionStartedAt.HasValue) UpdateActionIconLayout();

        if (!IsPaused || pauseGroup == null) return;
        pauseGroup.interactable = !HasOpenChildOverlay;
        pauseGroup.blocksRaycasts = pauseGroup.interactable;
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

    public void SetPaused(bool paused)
    {
        if (IsPaused == paused || (paused && turnGameManager != null && turnGameManager.IsGameComplete)) return;
        ProjectD.Audio.GameAudioManager.Instance?.PlayUI(ProjectD.Audio.AudioCue.Pause);
        IsPaused = paused;

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
        if (PauseUI != null)
        {
            ProjectD.Audio.ButtonAudioFeedback.BindMenu(PauseUI.gameObject);
            pauseOverlay = PauseUI.GetComponent<OverlayUI>();
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
