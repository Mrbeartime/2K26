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
    public UITurnCounter TurnCounter;   // drag the object with UITurnCounter here
    public GameObject PauseUI;


    [Header("Action Lists")]
    public GameObject MoveAction;
    public GameObject InteractAction;
    public GameObject SignatureAction;
    public TextMeshProUGUI SignatureText;

    [Header("Objects List")]
    [SerializeField] private PlayerMove[] CharacterList;
    [SerializeField] private GameObject[] HazardList;

    [Header("Icon Spawning")]
    [Tooltip("Enter Icon list in order of CharacterList and HazardList simultaneously")]
    [SerializeField] private GameObject[] IconPrefab;

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
            selectedAction = NoAction;   // เปลี่ยนตัว/จบ action = ล้างสถานะที่เลือกค้างไว้
        }
        if (current != null && CharacterList != null)
        {
            int idx = Array.IndexOf(CharacterList, current);
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

        // action ที่เลือกค้างไว้ใช้ไม่ได้แล้ว (เช่น Move ไปแล้ว) -> ล้าง
        if (selectedAction != NoAction && !availableActions.Contains(selectedAction)) selectedAction = NoAction;

        if (availableActions.Count > 0)
        {
            CurrentFocusActionIndex = Mathf.Clamp(CurrentFocusActionIndex, 0, availableActions.Count - 1);
            int focusId = availableActions[CurrentFocusActionIndex];
            if (!NeedsSpaceConfirm(focusId)) ConfirmAction(focusId);
            if (turnGameManager.CurrentPlayer != current || turnGameManager.IsEnemyPhase) return;
        }

        if (SignatureText != null)
        {
            Character character = current.GetComponent<Character>();
            string count = character != null && character.SkillLimit >= 0
                ? " (" + character.RemainingSkills + "/" + character.SkillLimit + ")" : " (Unlimited)";
            SignatureText.text = GetSignatureName(current) + count;
        }

        const float startY = 35f, gapY = 65f, posX = 25f;
        for (int i = 0; i < availableActions.Count; i++)
        {
            GameObject go = GetActionObject(availableActions[i]);
            go.SetActive(true);
            go.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, startY + i * gapY);
        }

        //เวลาเลือก action ใหม่ให้ยกเลิก action ที่ทำอยู่
        int modeAction = turnGameManager.IsMoveTargeting ? ActionMove
               : turnGameManager.IsSkillTargeting ? ActionSignature : -1;
        int modePos = availableActions.IndexOf(modeAction);
        if (modePos >= 0) CurrentFocusActionIndex = modePos;
        if (modeAction != NoAction) selectedAction = modeAction;   // manager อยู่ในโหมดเลือกช่องอยู่ = action นี้ถูกยืนยันแล้ว

        CurrentFocusActionIndex = Mathf.Clamp(CurrentFocusActionIndex, 0, Mathf.Max(0, availableActions.Count - 1));
        UpdateActionFocusUI();
    }

    private GameObject GetActionObject(int id) => id switch
    {
        ActionMove => MoveAction,
        ActionInteract => InteractAction,
        _ => SignatureAction
    };

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

        //Mock-up รอระบบเปลี่ยน scene
        Debug.Log(won ? "VICTORY: " + reason : "GAME OVER: " + reason);


        //บอกผลชนะ/แพ้
    }

    public void CancelCurrentAction() => GoBack();   // เผื่อผูกกับปุ่ม uGUI
    public void OnClickEndTurn() { if (turnGameManager != null) turnGameManager.ChooseEndTurn(); }
    public void OnCharacterIconClicked(GameObject target)
    {
        if (turnGameManager == null || target == null) return;
        PlayerMove p = target.GetComponent<PlayerMove>();
        if (p != null) turnGameManager.TrySelectPlayer(p);
    }

    //ของ Enemy ที่ไม่ส่ง Enemy เข้า TurnGameManager เพราะ Enemy Icon แค่เอาไว้ดู Range เฉยๆ
    public void OnEnemyIconClicked(GameObject target)
    {
        if (target == null)
            return;

        //หน้าไม้
        TurnArrowTrap trap = target.GetComponent<TurnArrowTrap>();

        if (trap != null)
        {
            PlayerController.Instance?.SelectArrowTrap(trap);
            return;
        }

        //Enemy
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

        int iconCount = IconPrefab != null ? IconPrefab.Length : 0;
        if (iconCount != characterCount + hazardCount)
        {
            Debug.LogWarning(
                $"IconPrefab length ({iconCount}) does not match CharacterList + HazardList " +
                $"({characterCount} + {hazardCount}). Order must be characters first, then hazards.", this);
        }

        int slot = 0;
        for (int i = 0; i < characterCount; i++)
        {
            PlayerMove character = CharacterList[i];
            if (character == null)
            {
                Debug.LogWarning($"CharacterList[{i}] is not assigned.", this);
                continue;
            }
            SpawnIcon(GetIconPrefab(i), character.gameObject, CharacterUI.transform, false, slot++, true);
        }

        // Hazards: prefab index = characterCount + i
        int hazardSlot = 0;
        for (int i = 0; i < hazardCount; i++)
        {
            GameObject hazard = HazardList[i];
            if (hazard == null)
            {
                Debug.LogWarning($"HazardList[{i}] is not assigned.", this);
                continue;
            }
            SpawnIcon(GetIconPrefab(characterCount + i), hazard, HazardUI.transform, true, hazardSlot++, false);
        }
    }

    private GameObject GetIconPrefab(int prefabIndex)
    {
        if (IconPrefab == null || prefabIndex < 0 || prefabIndex >= IconPrefab.Length || IconPrefab[prefabIndex] == null)
        {
            Debug.LogWarning($"No IconPrefab assigned at index {prefabIndex}.", this);
            return null;
        }
        return IconPrefab[prefabIndex];
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
        CurrentFocusActionIndex = newIndex;   // set first
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

        // Already the active/selected action -> confirm it (Interact: second click = Space)
        if (selectedAction == actionId)
        {
            ConfirmAction(actionId);
            return;
        }

        // Same behaviour as pressing W/S: cancel the old action, focus the new one,
        // and use it immediately (Interact waits for confirmation)
        FocusAndUseAction(availableActions.IndexOf(actionId));
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
                if (!turnGameManager.IsMoveTargeting) turnGameManager.ChooseMove();       // -> เลือกช่องเดิน
                break;
            case ActionInteract:
                ConfirmInteract();
                break;
            case ActionSignature:
                if (!turnGameManager.IsSkillTargeting) turnGameManager.ChooseSkill();     // -> เลือกเป้าหมายสกิล
                break;
        }
    }

    private void ConfirmInteract()
    {
        //Claude ลองแก้ตรงนี้
        turnGameManager.ChooseReact();
    }

    private void CancelPendingAction()
    {
        selectedAction = NoAction;
        if (turnGameManager == null) return;
        if (turnGameManager.IsMoveTargeting) turnGameManager.CancelMoveTargeting();
        else if (turnGameManager.IsSkillTargeting) turnGameManager.CancelSkill();
    }

    // ย้อนกลับทีละขั้น: เลือกช่อง -> รอยืนยัน -> เลือก Action
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
            // ยังไม่ได้เลือกตัวไหน (เช่น เพิ่งเริ่มเทิร์น หรือตัวก่อนหน้าจบ action ไปแล้ว) -> เริ่มหาจากตัวแรก
            // มีตัวที่เลือกอยู่ -> ไปตัวถัดไปจากตัวนั้น
            int start = turnGameManager.CurrentPlayer == null ? 0 : CurrentFocusCharacterIndex + 1;
            for (int step = 0; step < CharacterList.Length; step++)
            {
                int i = (start + step) % CharacterList.Length;
                if (TryFocusCharacter(i)) break;   // ข้ามตัวที่ทำ action ไปแล้ว/ตายแล้วให้เอง
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
        if (index < 0 || index >= CharacterList.Length || CharacterList[index] == null) return false;
        return turnGameManager.TrySelectPlayer(CharacterList[index]);
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