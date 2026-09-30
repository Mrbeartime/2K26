using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

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
    [SerializeField] private GameObject IconPrefab; // ลาก Prefab ของ Icon มาใส่(มีอันเดียวมั้งตอนนี้)

    [Header("Input Actions")]
    private ProjectDInputAction _inputAction;
    private int MaxCharacters;
    private int CurrentFocusCharacterIndex = 0;
    private int CurrentFocusActionIndex = 0;


    //System
    private TurnGameManager turnGameManager;
    private PlayerMove previousCharacter;
    private readonly List<int> availableActions = new();
    private const int ActionMove = 0, ActionInteract = 1, ActionSignature = 2;
    private const int NoAction = -1;
    private int selectedAction = NoAction;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_inputAction == null)
        {
            _inputAction = new ProjectDInputAction();
        }
    }

    private void OnEnable()
    {
        if (_inputAction == null) return;   // กรณีเป็น Instance ซ้ำที่ถูก Destroy ใน Awake
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

        RefreshFromTurnManager();

        PauseUI.SetActive(false);
    }

    private void RefreshFromTurnManager()
    {
        if (turnGameManager == null)
        {
            Debug.LogWarning("There is NO TurnGameManager");
            return;
        }
        else
        {
            UpdateTurnCounter();
        }

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
        if (TurnGameManager.Instance == null || TurnLimitText == null) return;
        TurnLimitText.text = $"{TurnGameManager.Instance.CurrentTurn}";
    }
    private void UpdateCharacterIcons(PlayerMove current)
    {
        foreach (ObjectIcon icon in CharacterUI.GetComponentsInChildren<ObjectIcon>())
        {
            PlayerMove p = icon.TargetObject != null ? icon.TargetObject.GetComponent<PlayerMove>() : null;

            if (p != null && p == current) icon.Selected();
            else icon.Unselected();

            bool unavailable = p == null || turnGameManager.IsPlayerFinished(p) ||
                               (p.TryGetComponent(out Character c) && c.IsDead);
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

        // action ที่เลือกค้างไว้ใช้ไม่ได้แล้ว (เช่น Move ไปแล้ว) -> ล้าง
        if (selectedAction != NoAction && !availableActions.Contains(selectedAction)) selectedAction = NoAction;

        ConfirmAction(availableActions[CurrentFocusActionIndex]);

        if (SignatureText != null) SignatureText.text = GetSignatureName(current);

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
    #region Setup UI and active UI
    private void CreateCharacterandHazardIcon()
    {
        if (CharacterList != null)
        {
            int index = 0;
            foreach (PlayerMove character in CharacterList)
            {
                if (character == null)
                {
                    Debug.LogWarning("CharacterList ยังไม่ได้ลากตัวละครใส่", this);
                    continue;
                }

                //สร้าง Prefab ของ Icon ตัวละครใน UI
                //Prefabs ของ Icon มีการเชื่อมเกัย ObjectIcon.cs เพื่อให้ Icon สามารถอ้างอิงไปยัง GameObject ตัวละครที่เกี่ยวข้องได้


                GameObject newIcon = Instantiate(IconPrefab, CharacterUI.transform);

                // 1. ดึง RectTransform จาก Icon ที่เพิ่งสร้าง
                RectTransform rect = newIcon.GetComponent<RectTransform>();

                // 2. คำนวณตำแหน่ง X (เริ่มที่ 15 บวกเพิ่มชิ้นละ 100)
                float posX = 30f + (index * 110f);
                float posY = -20f;

                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);

                rect.pivot = new Vector2(0, 1);

                rect.anchoredPosition = new Vector2(posX, posY);

                ObjectIcon iconScript = newIcon.GetComponent<ObjectIcon>();
                if (iconScript != null)
                {
                    iconScript.Setup(character.gameObject, true);
                }

                index++;
            }
        }

        if (HazardList != null)
        {
            int index = 0;
            foreach (GameObject hazard in HazardList)
            {
                GameObject newIcon = Instantiate(IconPrefab, HazardUI.transform);

                RectTransform rect = newIcon.GetComponent<RectTransform>();

                float posX = -30f + (index * -110f);
                float posY = -20f;

                rect.anchorMin = new Vector2(1, 1);
                rect.anchorMax = new Vector2(1, 1);

                rect.pivot = new Vector2(1, 1);

                rect.anchoredPosition = new Vector2(posX, posY);

                ObjectIcon iconScript = newIcon.GetComponent<ObjectIcon>();
                if (iconScript != null)
                {
                    iconScript.Setup(hazard, false);
                }
                index++;
            }
        }
    }

    // ---------- ระบบเลือก Action ----------
    // ขั้น 0 : เลื่อนโฟกัสด้วย A/W/S/D
    // ขั้น 1 : กด Space (หรือคลิกปุ่ม)    = ยืนยันใช้ (Move/Skill เข้าโหมดเลือกช่อง, Interact ทำทันที)
    // การกด A/W/S/D ขณะอยู่ในขั้น 1 ทำให้ยกเลิก action ที่ทำอยู่ทันที
    private void OnChangeAction(InputAction.CallbackContext context)
    {
        int count = availableActions.Count;
        if (count <= 0) return;
        string key = context.control.name.ToLower();

        int newIndex;
        if (key == "d" || key == "s")
        {
            newIndex = (CurrentFocusActionIndex - 1 + count) % count;
            Debug.Log($"Press : {key} , newIndex : {newIndex}");

            CancelPendingAction();
            CurrentFocusActionIndex = newIndex;
            UpdateActionFocusUI();

            ConfirmAction(availableActions[CurrentFocusActionIndex]);
            return;
        }
        else if (key == "a" || key == "w")
        {
            newIndex = (CurrentFocusActionIndex + 1) % count;
            Debug.Log($"Press : {key} , newIndex : {newIndex}");

            CancelPendingAction();
            CurrentFocusActionIndex = newIndex;
            UpdateActionFocusUI();

            ConfirmAction(availableActions[CurrentFocusActionIndex]);
            return;
        }
        else if (key == "space")
        {
            // CurrentFocusActionIndex คือ "ลำดับในรายการที่ใช้ได้" ต้องแปลงเป็น id ของ action ก่อนส่ง
            ConfirmAction(availableActions[CurrentFocusActionIndex]);
            //SelectAction(availableActions[CurrentFocusActionIndex]);
            return;
        }
        else
        {
            return;
        }

        if (newIndex == CurrentFocusActionIndex) return;   // มี action เดียว = ไม่ได้เปลี่ยน จึงไม่ยกเลิก

        // เปลี่ยน Action ระหว่างเลือกช่อง/รอยืนยัน = ยกเลิกของเดิมทั้งหมด
        //CancelPendingAction();
        //CurrentFocusActionIndex = newIndex;
        //UpdateActionFocusUI();
    }

    public void SelectAction(int actionId)
    {
        if (turnGameManager == null || !availableActions.Contains(actionId)) return;

        if (selectedAction == actionId)   // เลือกไว้แล้ว -> ครั้งนี้คือยืนยัน
        {
            ConfirmAction(actionId);
            return;
        }

        // เลือกครั้งแรก หรือเปลี่ยนไปเลือกอันอื่น: ยกเลิกของเดิมทั้งหมดก่อน แล้วค่อย "เลือก" อันใหม่ (ยังไม่ใช้)
        CancelPendingAction();
        selectedAction = actionId;
        CurrentFocusActionIndex = availableActions.IndexOf(actionId);
        UpdateActionFocusUI();
    }

    private void ConfirmAction(int actionId)
    {
        switch (actionId)
        {
            case ActionMove:
                if (!turnGameManager.IsMoveTargeting) turnGameManager.ChooseMove();       // -> เลือกช่องเดิน
                break;
            case ActionInteract:
                turnGameManager.ChooseReact();                                            // -> ทำทันที (ไม่มีเป้าหมายให้เลือก)
                break;
            case ActionSignature:
                if (!turnGameManager.IsSkillTargeting) turnGameManager.ChooseSkill();     // -> เลือกเป้าหมายสกิล
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
            for (int step = 1; step <= CharacterList.Length; step++)
            {
                int i = (CurrentFocusCharacterIndex + step) % CharacterList.Length;
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

        // 3. เลือกไว้แล้วแต่ยังไม่ยืนยัน (ยังไม่เข้าโหมดเลือกช่อง) = สถานะ "รอยืนยัน"
        bool targeting = turnGameManager != null &&
                         (turnGameManager.IsMoveTargeting || turnGameManager.IsSkillTargeting);
    }
    private void OnSkipTurnStart(InputAction.CallbackContext context)
    {
        //รอ Visual เริ่มกดค้าง
    }
    private void OnSkipTurnPerform(InputAction.CallbackContext context)
    {
        OnClickEndTurn();
    }
    private void OnSkipTurnCancel(InputAction.CallbackContext context)
    {
        //รอ Visual ยกเลิกกดค้าง
    }
    private void OnShowPauseUI(InputAction.CallbackContext context)
    {
            PauseUI.SetActive(!PauseUI.activeSelf);
    }
    #endregion
}