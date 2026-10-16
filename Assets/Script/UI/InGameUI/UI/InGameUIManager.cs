using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InGameUIManager : MonoBehaviour
{
    public static InGameUIManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject InGameUI;
    public GameObject CharacterUI;
    public GameObject ActionUI;
    public GameObject HazardUI;
    public GameObject TurnLimitUI;
    public TextMeshProUGUI TurnLimitText;
    public UITurnCounter TurnCounter;
    public GameObject PauseUI;


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

    [Header("Objects List")]
    [SerializeField] private CharacterEntry[] CharacterList;

    [Serializable]
    public class HazardEntry
    {
        [Tooltip("The hazard in the scene (enemy, trap, ...)")]
        public GameObject hazard;
        [Tooltip("HUD icon prefab of this hazard (needs ObjectIcon)")]
        public GameObject iconPrefab;
    }

    [SerializeField] private HazardEntry[] HazardList;

    [Header("Input Actions")]
    private ProjectDInputAction _inputAction;
    private int CurrentFocusCharacterIndex = 0;
    private int CurrentFocusActionIndex = 0;


    //System

    private readonly List<(Button button, UnityEngine.Events.UnityAction callback)> actionButtons = new();
    private HoldButton turnLimitButton;
    private TurnGameManager turnGameManager;
    private PlayerMove previousCharacter;
    private readonly List<int> availableActions = new();
    private const int ActionMove = 0, ActionInteract = 1, ActionSignature = 2;
    private const int NoAction = -1;
    private int selectedAction = NoAction;
    private static bool NeedsSpaceConfirm(int actionId) => actionId == ActionInteract;
    private readonly HashSet<PlayerMove> warnedSignatureEntry = new();

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

        //Holding for skip: Start เริ่มกด Performed กดค้างจนพอ Canceled ยกเลิกการกด
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

        //Holding for skip
        _inputAction.Gameplay_keyboard.Skip.started -= OnSkipTurnStart;
        _inputAction.Gameplay_keyboard.Skip.performed -= OnSkipTurnPerform;
        _inputAction.Gameplay_keyboard.Skip.canceled -= OnSkipTurnCancel;

        _inputAction.Disable();
    }

    private void OnDestroy()
    {
        if (turnGameManager == null) return;
        turnGameManager.StateChanged -= RefreshFromTurnManager;
        turnGameManager.GameEnded -= OnGameEnded;
    }

    private void Start()
    {
        CreateCharacterandHazardIcon();

        turnGameManager = TurnGameManager.Instance;
        if (turnGameManager == null)
        {
            Debug.LogError("ไม่พบ TurnGameManager ใน Scene", this);
            return;
        }
        turnGameManager.StateChanged += RefreshFromTurnManager;
        turnGameManager.GameEnded += OnGameEnded;

        SetupTurnLimitButton();
        SetUpActionList();
        RefreshFromTurnManager();

        PauseUI.SetActive(false);
    }

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

        UnityEngine.Events.UnityAction callback = () => SelectAction(actionId);
        button.onClick.AddListener(callback);
        actionButtons.Add((button, callback));
    }

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

    public void UpdateTurnCounter()
    {
        if (TurnGameManager.Instance == null) return;
        if (TurnLimitText != null) TurnLimitText.text = $"{TurnGameManager.Instance.CurrentTurn}";
        if (TurnCounter != null) TurnCounter.LegMoving(TurnGameManager.Instance.CurrentTurn);
    }

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

    public void RefreshActionList(PlayerMove current)
    {
        availableActions.Clear();
        MoveAction.SetActive(false);
        InteractAction.SetActive(false);
        SignatureAction.SetActive(false);

        bool show = current != null && !turnGameManager.IsGameComplete && !turnGameManager.IsEnemyPhase &&
            !turnGameManager.IsWaitingForMovement;

        ActionUI.SetActive(show);
        if (!show) { selectedAction = NoAction; return; }

        if (turnGameManager.CanChooseMove) availableActions.Add(ActionMove);
        if (turnGameManager.CanChooseReact) availableActions.Add(ActionInteract);
        if (turnGameManager.CanChooseSkill) availableActions.Add(ActionSignature);

        if (selectedAction != NoAction && !availableActions.Contains(selectedAction)) selectedAction = NoAction;

        if (availableActions.Count > 0)
        {
            CurrentFocusActionIndex = Mathf.Clamp(CurrentFocusActionIndex, 0, availableActions.Count - 1);
            int focusId = availableActions[CurrentFocusActionIndex];
            if (!NeedsSpaceConfirm(focusId)) ConfirmAction(focusId);
        }

        UpdateSignatureUI(current);

        const float startY = 35f, gapY = 84f, posX = 32f;
        for (int i = 0; i < availableActions.Count; i++)
        {
            GameObject go = GetActionObject(availableActions[i]);
            go.SetActive(true);
            go.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, startY + i * gapY);
        }

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

    // Shows the current character's signature name and gives the Signature button its icons.
    // CharacterActionList.Update() picks normal/focused sprite itself, so only hand the sprites over.
    private void UpdateSignatureUI(PlayerMove player)
    {
        CharacterEntry entry = FindCharacterEntry(player);

        if (SignatureText != null)
        {
            bool hasName = entry != null && !string.IsNullOrEmpty(entry.skillName);
            SignatureText.text = hasName ? entry.skillName : GetSignatureName(player);
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

    private void OnGameEnded(bool won, string reason)
    {
        ActionUI.SetActive(false);

        Debug.Log(won ? "VICTORY: " + reason : "GAME OVER: " + reason);


    }

    public void CancelCurrentAction() => GoBack();
    public void OnClickEndTurn() { if (turnGameManager != null) turnGameManager.ChooseEndTurn(); }
    public void OnCharacterIconClicked(GameObject target)
    {
        if (turnGameManager == null || target == null) return;
        PlayerMove p = target.GetComponent<PlayerMove>();
        if (p != null) turnGameManager.TrySelectPlayer(p);
    }

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

    #region Setup UI and active UI
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
        rect.anchoredPosition = new Vector2(direction * (30f + index * 130f), -20f);

        ObjectIcon iconScript = newIcon.GetComponent<ObjectIcon>();
        if (iconScript != null) iconScript.Setup(target, selectable);
        else Debug.LogWarning($"{prefab.name} has no ObjectIcon component.", newIcon);
    }

    // ---------- ระบบเลือก Action ----------
    // A/W/S/D  = เลื่อนโฟกัส แล้วใช้ action นั้นทันที (ยกเลิก action/โหมดเลือกช่องเดิมก่อน)
    // Space    = ใช้ action ที่โฟกัสอยู่
    // คลิกปุ่ม = SelectAction(): คลิกครั้งแรกเลือก คลิกครั้งที่สองยืนยัน
    private void OnChangeAction(InputAction.CallbackContext context)
    {
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

    private void FocusAndUseAction(int newIndex)
    {
        CurrentFocusActionIndex = newIndex;
        CancelPendingAction();
        UpdateActionFocusUI();

        int id = availableActions[CurrentFocusActionIndex];
        if (NeedsSpaceConfirm(id))
        {
            selectedAction = id;
            return;
        }
        ConfirmAction(id);
    }

    public void SelectAction(int actionId)
    {
        if (turnGameManager == null || !availableActions.Contains(actionId)) return;

        if (selectedAction == actionId)
        {
            ConfirmAction(actionId);
            return;
        }
        FocusAndUseAction(availableActions.IndexOf(actionId));
    }

    private void ConfirmAction(int actionId)
    {
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

    private void ConfirmInteract()
    {
        turnGameManager.ChooseReact();
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
        if (turnGameManager == null) return;
        if (turnGameManager.IsMoveTargeting) turnGameManager.CancelMoveTargeting();
        else if (turnGameManager.IsSkillTargeting) turnGameManager.CancelSkill();
        else selectedAction = NoAction;
        UpdateActionFocusUI();
    }
    #endregion

    #region InputAction
    private void OnChangeCharacter(InputAction.CallbackContext context)
    {
        if (turnGameManager == null || CharacterList == null || CharacterList.Length == 0) return;
        string key = context.control.name.ToLower();

        if (key == "tab")
        {
            int start = turnGameManager.CurrentPlayer == null ? 0 : CurrentFocusCharacterIndex + 1;
            for (int step = 0; step < CharacterList.Length; step++)
            {
                int i = (start + step) % CharacterList.Length;
                if (TryFocusCharacter(i)) break;
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


    private void UpdateActionFocusUI()
    {
        // 1. เคลียร์สถานะของทุกปุ่มก่อน
        foreach (GameObject go in new[] { MoveAction, InteractAction, SignatureAction })
        {
            CharacterActionList item = go.GetComponent<CharacterActionList>();
            item.Unselected();
        }

        // 2. ไฮไลต์ action ที่โฟกัสอยู่
        if (availableActions.Count == 0) return;
        int focusId = availableActions[Mathf.Clamp(CurrentFocusActionIndex, 0, availableActions.Count - 1)];
        CharacterActionList focused = GetActionObject(focusId).GetComponent<CharacterActionList>();
        focused.Selected();
    }

    private void OnSkipTurnStart(InputAction.CallbackContext context)
    {
        if (TurnCounter != null)
        {
            TurnCounter.PressButton();
            Debug.Log("can press");
        }
    }
    private void OnMouseSkipStart()
    {
        if (TurnCounter != null) TurnCounter.PressButton();
    }

    private void OnMouseSkipCancel()
    {
        if (TurnCounter != null) TurnCounter.ReleaseButton();
    }
    private void OnSkipTurnPerform(InputAction.CallbackContext context)
    {
        OnClickEndTurn();
    }

    private void OnSkipTurnCancel(InputAction.CallbackContext context)
    {
        if (TurnCounter != null)
        {
            TurnCounter.ReleaseButton();

            Debug.Log("can release");
        }
    }
    private void OnShowPauseUI(InputAction.CallbackContext context)
    {
        PauseUI.SetActive(!PauseUI.activeSelf);
    }
    #endregion
}