using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// In-game HUD: character/hazard icons, action list (Move / Interact / Signature), turn counter, and pause system.
/// Does not decide game rules itself: it sends commands to TurnGameManager and only displays the state it reports back.
///
/// Region order: Fields -> Unity Lifecycle -> Setup -> Turn Manager Sync -> Icons
///               -> Action List (display) -> Action Selection (select/confirm)
///               -> Keyboard Input -> UI Callbacks -> Pause
/// </summary>
public class InGameUIManager : MonoBehaviour
{
    public static InGameUIManager Instance { get; private set; }

    #region Fields - Inspector

    [Header("UI References")]
    public GameObject InGameUI;
    public GameObject CharacterUI;
    public GameObject ActionUI;
    public GameObject HazardUI;
    public GameObject TurnLimitUI;
    public TextMeshProUGUI TurnLimitText;
    public UITurnCounter TurnCounter;
    public Canvas PauseUI;

    [Header("Pause Manager")]
    [SerializeField] private float fadeTime = 0.2f;
    [Tooltip("Full-screen black Image. Fades to Pause Dim Alpha while paused.")]
    public Image blackScreen;
    [Range(0f, 1f)]
    [SerializeField] private float pauseDimAlpha = 0.6f;

    // ★ ADDED: turn number text animation (used by ClockMovingOff / ClockMovingBack)
    [Header("Turn Text Animation")]
    [Tooltip("How far (posX) the old number slides to the right before it disappears")]
    [SerializeField] private float turnTextMoveDistance = 80f;
    [Tooltip("The new number starts this far to the LEFT of its resting position, then slides right back to rest")]
    [SerializeField] private float turnTextBackStartOffset = 80f;
    [SerializeField] private float turnTextOffDuration = 0.15f;
    [SerializeField] private float turnTextBackDuration = 0.2f;
    // ★ ADDED: editable easing graphs (click the curve in the Inspector). X = time 0..1, Y = progress 0..1
    //   Y may go above 1 for an overshoot/bounce. Default = ease in-out (same as the old SmoothStep).
    [Tooltip("Easing of the old number sliding out")]
    [SerializeField] private AnimationCurve turnTextOffEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Easing of the new number sliding in")]
    [SerializeField] private AnimationCurve turnTextBackEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

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
        [Tooltip("HUD icon prefab of this character (needs ObjectIcon)")]
        public GameObject iconPrefab;
        [Tooltip("Optional. Leave empty to use the default name (Lockpick / Bow Shot / Slash)")]
        public string skillName;
        [Tooltip("Signature icon, normal state (sprites[0])")]
        public Sprite signatureIcon;
        [Tooltip("Optional. Signature icon while focused (sprites[1]). Empty = same as Signature Icon")]
        public Sprite signatureIconSelected;
    }

    [Serializable]
    public class HazardEntry
    {
        [Tooltip("The hazard in the scene (enemy, trap, ...)")]
        public GameObject hazard;
        [Tooltip("HUD icon prefab of this hazard (needs ObjectIcon)")]
        public GameObject iconPrefab;
    }

    [Header("Objects List")]
    [SerializeField] private CharacterEntry[] CharacterList;
    [SerializeField] private HazardEntry[] HazardList;

    #endregion

    #region Fields - Runtime State

    // ---- External systems ----
    private ProjectDInputAction _inputAction;
    private TurnGameManager turnGameManager;
    private HoldButton turnLimitButton;

    // ---- Character focus ----
    private PlayerMove previousCharacter;           // Used to detect when the current character changes (changed -> reset action focus)
    private int CurrentFocusCharacterIndex = 0;     // Index into CharacterList

    // ---- Actions ----
    // Action ids (not indices): Move = 0, Interact = 1, Signature = 2
    // Must match the actionType enum in CharacterActionList
    private const int ActionMove = 0, ActionInteract = 1, ActionSignature = 2;
    private const int NoAction = -1;

    private readonly List<int> availableActions = new();    // Actions usable right now (sorted in display order)
    private int CurrentFocusActionIndex = 0;                // Index into availableActions (not the id)
    private int selectedAction = NoAction;                  // Action that is "chosen/armed" and awaiting confirmation (NoAction = none)

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

    // ★ ADDED: turn text animation state
    private Coroutine turnTextRoutine;
    private float turnTextRestX;
    private bool turnTextRestCaptured;
    private int lastShownTurn = int.MinValue;

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
        _inputAction.Enable();

        _inputAction.Gameplay_keyboard.SelectCharacter.performed += OnChangeCharacter;
        _inputAction.Gameplay_keyboard.SelectAction.performed += OnChangeAction;
        _inputAction.Gameplay_keyboard.Pause.performed += OnShowPauseUI;

        // Hold to skip: Started = begin hold / Performed = hold completed / Canceled = released early
        _inputAction.Gameplay_keyboard.Skip.started += OnSkipTurnStart;
        _inputAction.Gameplay_keyboard.Skip.performed += OnSkipTurnPerform;
        _inputAction.Gameplay_keyboard.Skip.canceled += OnSkipTurnCancel;
    }

    private void OnDisable()
    {
        if (_inputAction == null) return;
        _inputAction.Gameplay_keyboard.SelectCharacter.performed -= OnChangeCharacter;
        _inputAction.Gameplay_keyboard.SelectAction.performed -= OnChangeAction;
        _inputAction.Gameplay_keyboard.Pause.performed -= OnShowPauseUI;

        _inputAction.Gameplay_keyboard.Skip.started -= OnSkipTurnStart;
        _inputAction.Gameplay_keyboard.Skip.performed -= OnSkipTurnPerform;
        _inputAction.Gameplay_keyboard.Skip.canceled -= OnSkipTurnCancel;

        _inputAction.Disable();
    }

    private void OnDestroy()
    {
        foreach (var binding in actionButtons)
            if (binding.button != null) binding.button.onClick.RemoveListener(binding.callback);
        actionButtons.Clear();
        if (turnGameManager == null) return;
        turnGameManager.StateChanged -= RefreshFromTurnManager;
        turnGameManager.GameEnded -= OnGameEnded;
    }

    private void Start()
    {
        SetupPause();
        CreateCharacterandHazardIcon();

        turnGameManager = TurnGameManager.Instance;
        if (turnGameManager == null)
        {
            Debug.LogError("TurnGameManager not found in the scene", this);
            return;
        }

        // Main hook: every time the state changes, the HUD is redrawn through RefreshFromTurnManager
        turnGameManager.StateChanged += RefreshFromTurnManager;
        turnGameManager.GameEnded += OnGameEnded;

        SetupTurnLimitButton();
        SetUpActionList();
        RefreshFromTurnManager();
    }

    #endregion

    #region Setup (run once)

    // Hold-to-end-turn button: HoldCompleted = hold finished (ends turn), HoldStarted/Canceled = press/release animation
    private void SetupTurnLimitButton()
    {
        if (TurnLimitUI == null) return;

        Button button = TurnLimitUI.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            Debug.LogWarning("TurnLimitUI has no Button component.", TurnLimitUI);
            return;
        }

        turnLimitButton = button.GetComponent<HoldButton>();
        if (turnLimitButton == null) turnLimitButton = button.gameObject.AddComponent<HoldButton>();

        // -= before += to prevent subscribing twice
        turnLimitButton.HoldCompleted -= OnClickEndTurn;
        turnLimitButton.HoldCompleted += OnClickEndTurn;

        turnLimitButton.HoldStarted -= OnMouseSkipStart;
        turnLimitButton.HoldStarted += OnMouseSkipStart;

        turnLimitButton.HoldCanceled -= OnMouseSkipCancel;
        turnLimitButton.HoldCanceled += OnMouseSkipCancel;
    }

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

        PlayerMove current = turnGameManager.CurrentPlayer;
        if (current != previousCharacter)
        {
            // Character changed -> reset focus to the first action and clear any pending selection/arm
            CurrentFocusActionIndex = 0;
            previousCharacter = current;
            selectedAction = NoAction;
        }
        if (current != null && CharacterList != null)
        {
            int idx = FindCharacterIndex(current);
            if (idx >= 0) CurrentFocusCharacterIndex = idx;
        }

        UpdateCharacterIcons(current);
        RefreshActionList(current);
    }

    // When the turn changes -> update the number and play the UITurnCounter animation (once per turn)
    public void UpdateTurnCounter()
    {
        if (TurnGameManager.Instance == null) return;
        int turn = TurnGameManager.Instance.CurrentTurn;

        // ★ CHANGED: the text now animates (off -> change number -> back) only when the turn number really changes.
        //   RefreshFromTurnManager calls this on every state change, so unchanged turns must not replay it.
        if (TurnLimitText != null && turn != lastShownTurn)
        {
            bool firstTime = lastShownTurn == int.MinValue;
            lastShownTurn = turn;

            CaptureTurnTextRest();
            if (turnTextRoutine != null) StopCoroutine(turnTextRoutine);
            SetTurnTextX(turnTextRestX);   // restart from a clean position if a previous animation was cut off

            if (firstTime || !isActiveAndEnabled) TurnLimitText.text = $"{turn}";   // no animation on first draw
            else turnTextRoutine = StartCoroutine(TurnTextRoutine(turn));
        }

        if (TurnCounter != null) TurnCounter.LegMoving(turn);
    }

    // ★ ADDED: full sequence = ClockMovingOff (slide out) -> set new number -> ClockMovingBack (slide in)
    private IEnumerator TurnTextRoutine(int turn)
    {
        yield return ClockMovingOff();
        TurnLimitText.text = $"{turn}";
        yield return ClockMovingBack();
        turnTextRoutine = null;
    }

    // ★ ADDED: old number slides left -> right by turnTextMoveDistance (80)
    private IEnumerator ClockMovingOff()
    {
        yield return MoveTurnTextX(turnTextRestX, turnTextRestX + turnTextMoveDistance, turnTextOffDuration, turnTextOffEase);
    }

    // ★ ADDED: new number is placed to the left (rest - turnTextBackStartOffset) and slides left -> right back to rest
    private IEnumerator ClockMovingBack()
    {
        float startX = turnTextRestX - turnTextBackStartOffset;
        SetTurnTextX(startX);
        yield return MoveTurnTextX(startX, turnTextRestX, turnTextBackDuration, turnTextBackEase);
    }

    private IEnumerator MoveTurnTextX(float from, float to, float duration, AnimationCurve ease)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;   // keeps working while paused (timeScale = 0)
            SetTurnTextX(Mathf.LerpUnclamped(from, to, ease.Evaluate(Mathf.Clamp01(t / duration))));   // ★ CHANGED: curve instead of SmoothStep
            yield return null;
        }
        SetTurnTextX(to);
    }

    // The resting position is read from the scene once, so you place the text where you want it in the editor
    private void CaptureTurnTextRest()
    {
        if (turnTextRestCaptured || TurnLimitText == null) return;
        turnTextRestX = TurnLimitText.rectTransform.anchoredPosition.x;
        turnTextRestCaptured = true;
    }

    private void SetTurnTextX(float x)
    {
        if (TurnLimitText == null) return;
        Vector2 pos = TurnLimitText.rectTransform.anchoredPosition;
        pos.x = x;
        TurnLimitText.rectTransform.anchoredPosition = pos;
    }

    private void OnGameEnded(bool won, string reason)
    {
        ActionUI.SetActive(false);

        Debug.Log(won ? "VICTORY: " + reason : "GAME OVER: " + reason);
    }

    #endregion

    #region Character & Hazard Icons

    // Creates character icons (left, selectable) and hazard icons (right, display only) from CharacterList / HazardList
    private void CreateCharacterandHazardIcon()
    {
        int characterCount = CharacterList != null ? CharacterList.Length : 0;
        int hazardCount = HazardList != null ? HazardList.Length : 0;

        int slot = 0;
        for (int i = 0; i < characterCount; i++)
        {
            CharacterEntry entry = CharacterList[i];
            if (entry == null || entry.character == null)
            {
                Debug.LogWarning($"CharacterList[{i}] has no character assigned.", this);
                continue;
            }
            if (entry.iconPrefab == null)
            {
                Debug.LogWarning($"CharacterList[{i}] ({entry.character.name}) has no iconPrefab assigned.", this);
                continue;
            }
            SpawnIcon(entry.iconPrefab, entry.character.gameObject, CharacterUI.transform, false, slot++, true);
        }

        int hazardSlot = 0;
        for (int i = 0; i < hazardCount; i++)
        {
            HazardEntry entry = HazardList[i];
            if (entry == null || entry.hazard == null)
            {
                Debug.LogWarning($"HazardList[{i}] has no hazard assigned.", this);
                continue;
            }
            if (entry.iconPrefab == null)
            {
                Debug.LogWarning($"HazardList[{i}] ({entry.hazard.name}) has no iconPrefab assigned.", this);
                continue;
            }
            SpawnIcon(entry.iconPrefab, entry.hazard, HazardUI.transform, true, hazardSlot++, false);
        }
    }

    // Adjust icon layout here: 30 = offset from the edge, 130 = spacing between icons, -12 = offset from the top
    private void SpawnIcon(GameObject prefab, GameObject target, Transform parent, bool rightAligned, int index, bool selectable)
    {
        if (prefab == null || target == null) return;

        GameObject newIcon = Instantiate(prefab, parent);
        RectTransform rect = newIcon.GetComponent<RectTransform>();

        float side = rightAligned ? 1f : 0f;
        float direction = rightAligned ? -1f : 1f;
        rect.anchorMin = new Vector2(side, 1);
        rect.anchorMax = new Vector2(side, 1);
        rect.pivot = new Vector2(side, 1);
        rect.anchoredPosition = new Vector2(direction * (30f + index * 130f), -12f);

        ObjectIcon iconScript = newIcon.GetComponent<ObjectIcon>();
        if (iconScript != null) iconScript.Setup(target, selectable);
        else Debug.LogWarning($"{prefab.name} has no ObjectIcon component.", newIcon);
    }

    // Updates each character icon: highlights the current character, and greys out characters that are finished / dead / unavailable
    private void UpdateCharacterIcons(PlayerMove current)
    {
        foreach (ObjectIcon icon in CharacterUI.GetComponentsInChildren<ObjectIcon>())
        {
            PlayerMove p = icon.TargetObject != null ? icon.TargetObject.GetComponent<PlayerMove>() : null;

            if (p != null && p == current) icon.Selected();
            else icon.Unselected();

            bool finished = p != null && turnGameManager.IsPlayerFinished(p);
            bool dead = p != null && p.TryGetComponent(out Character c) && c.IsDead;
            bool unavailable = p == null || finished || dead;

            if (unavailable)
                Debug.Log($"[Icon] {icon.name} disabled -> targetIsNull={p == null}, finished={finished}, dead={dead}", icon);

            icon.SetDisabled(unavailable);
        }
    }

    // ---- Look up character data in CharacterList ----

    private int FindCharacterIndex(PlayerMove player)
    {
        if (player == null || CharacterList == null) return -1;
        for (int i = 0; i < CharacterList.Length; i++)
            if (CharacterList[i] != null && CharacterList[i].character == player) return i;
        return -1;
    }

    private CharacterEntry FindCharacterEntry(PlayerMove player)
    {
        int idx = FindCharacterIndex(player);
        return idx >= 0 ? CharacterList[idx] : null;
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
        CharacterEntry entry = FindCharacterEntry(player);

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
    //  - A/W/S/D  : move focus + enter that action's mode immediately (cancels the previous action / mode first)
    //  - Space    : confirm the focused action
    //  - Click    : SelectAction() -> a different action = select/arm, the already-armed action = confirm
    //  - Interact needs an extra confirm (NeedsSpaceConfirm): selecting only "arms" it; press Space/click again to confirm
    //  - Move/Signature run immediately on selection (and show target highlights); cancel by selecting another action or GoBack

    // Select/click an action (called from buttons) - the decision point between "arm" and "really execute"
    public void SelectAction(int actionId)
    {
        if (turnGameManager == null || !availableActions.Contains(actionId)) return;

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

    #region Keyboard Input

    // ---- Choose action: A/W/S/D moves focus, Space confirms ----
    private void OnChangeAction(InputAction.CallbackContext context)
    {
        if (IsPaused) return;
        int count = availableActions.Count;
        if (count <= 0) return;

        switch (context.control.name.ToLower())
        {
            case "d":
            case "s":
                FocusAndUseAction((CurrentFocusActionIndex - 1 + count) % count);
                break;
            case "a":
            case "w":
                FocusAndUseAction((CurrentFocusActionIndex + 1) % count);
                break;
            case "space":
                ConfirmAction(availableActions[CurrentFocusActionIndex]);
                break;
        }
    }

    // ---- Choose character: Tab = next selectable one, number keys 1-9 = nth character in CharacterList ----
    private void OnChangeCharacter(InputAction.CallbackContext context)
    {
        if (IsPaused) return;
        if (turnGameManager == null || CharacterList == null || CharacterList.Length == 0) return;
        string key = context.control.name.ToLower();

        if (key == "tab")
        {
            int start = turnGameManager.CurrentPlayer == null ? 0 : CurrentFocusCharacterIndex + 1;
            for (int step = 0; step < CharacterList.Length; step++)
            {
                int i = (start + step) % CharacterList.Length;
                if (TryFocusCharacter(i)) break;    // Stop at the first character that can be selected (alive / not finished)
            }
        }
        else if (int.TryParse(key, out int number))
        {
            TryFocusCharacter(number - 1);
        }
    }

    private bool TryFocusCharacter(int index)
    {
        if (turnGameManager == null || CharacterList == null) return false;
        if (index < 0 || index >= CharacterList.Length) return false;
        CharacterEntry entry = CharacterList[index];
        if (entry == null || entry.character == null) return false;
        return turnGameManager.TrySelectPlayer(entry.character);
    }

    // ---- Skip turn (hold) ----
    // Started = press begins (animation) / Performed = held long enough -> end turn for real / Canceled = released early -> cancel animation
    private void OnSkipTurnStart(InputAction.CallbackContext context)
    {
        if (IsPaused) return;
        if (TurnCounter != null) TurnCounter.PressButton();
    }

    private void OnSkipTurnPerform(InputAction.CallbackContext context)
    {
        if (IsPaused) return;
        OnClickEndTurn();
    }

    private void OnSkipTurnCancel(InputAction.CallbackContext context)
    {
        if (TurnCounter != null) TurnCounter.ReleaseButton();
    }

    // ---- Skip turn with the mouse (HoldButton): animation only; the actual end turn comes from HoldCompleted ----
    private void OnMouseSkipStart()
    {
        if (TurnCounter != null) TurnCounter.PressButton();
    }

    private void OnMouseSkipCancel()
    {
        if (TurnCounter != null) TurnCounter.ReleaseButton();
    }

    #endregion

    #region UI Callbacks (buttons/icons call these, e.g. from the Inspector)

    // End the turn for real (has direct gameplay effects)
    public void OnClickEndTurn() { if (turnGameManager != null) turnGameManager.ChooseEndTurn(); }

    // Click a character icon -> switch to that player (if selectable)
    public void OnCharacterIconClicked(GameObject target)
    {
        if (turnGameManager == null || target == null) return;
        PlayerMove p = target.GetComponent<PlayerMove>();
        if (p != null) turnGameManager.TrySelectPlayer(p);
    }

    // Click a hazard icon -> tell PlayerController to select it (so it can be targeted/inspected)
    // Decides which kind of hazard it is and forwards to the matching select method
    public void OnEnemyIconClicked(GameObject target)
    {
        if (target == null)
            return;

        TurnArrowTrap trap = target.GetComponent<TurnArrowTrap>();

        if (trap != null)
        {
            PlayerController.Instance?.SelectArrowTrap(trap);
            return;
        }

        Enemy enemy = target.GetComponent<Enemy>();

        if (enemy != null)
        {
            PlayerController.Instance?.SelectEnemy(enemy);
            return;
        }
    }

    #endregion

    #region Pause

    public void CallPause() => SetPaused(!IsPaused);

    private void OnShowPauseUI(InputAction.CallbackContext context)
    {
        // ถ้าเกมจบแล้ว ห้ามกด ESC เพื่อเปิด Pause
        if (turnGameManager != null && turnGameManager.IsGameComplete)
            return;

        SetPaused(!IsPaused);
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
        IsPaused = paused;

        // Release any skip hold that is still showing its press animation
        if (paused && TurnCounter != null) TurnCounter.ReleaseButton();

        if (pauseRoutine != null) StopCoroutine(pauseRoutine);
        pauseRoutine = StartCoroutine(PauseFade(paused));
    }

    private void SetupPause()
    {
        if (InGameUI != null) inGameGroup = GetOrAddCanvasGroup(InGameUI);

        if (PauseUI != null)
        {
            pauseGroup = GetOrAddCanvasGroup(PauseUI.gameObject);
            pauseGroup.alpha = 0f;
            PauseUI.gameObject.SetActive(false);
        }
        else Debug.LogWarning("PauseUI is not assigned.", this);

        if (blackScreen != null)
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