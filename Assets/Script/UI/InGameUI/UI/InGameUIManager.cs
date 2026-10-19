using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// HUD ของเกม: ไอคอนตัวละคร/Hazard, รายการ Action (Move / Interact / Signature), ตัวนับเทิร์น และระบบ Pause
/// ไม่ได้ตัดสินกติกาเกมเอง — แค่ส่งคำสั่งไปที่ TurnGameManager แล้วแสดงผลตามสถานะที่ได้กลับมา
///
/// ลำดับ region: Fields -> Unity Lifecycle -> Setup -> Turn Manager Sync -> Icons
///               -> Action List (แสดงผล) -> Action Selection (ตรรกะเลือก/ยืนยัน)
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

    // ---- ระบบภายนอก ----
    private ProjectDInputAction _inputAction;
    private TurnGameManager turnGameManager;
    private HoldButton turnLimitButton;

    // ---- โฟกัสตัวละคร ----
    private PlayerMove previousCharacter;           // ใช้ตรวจว่าตัวที่เล่นอยู่เปลี่ยนหรือยัง (เปลี่ยน -> รีเซ็ตโฟกัส action)
    private int CurrentFocusCharacterIndex = 0;     // index ใน CharacterList

    // ---- Action ----
    // id ของ action (ไม่ใช่ index): Move = 0, Interact = 1, Signature = 2
    // ค่าต้องตรงกับ enum actionType ใน CharacterActionList
    private const int ActionMove = 0, ActionInteract = 1, ActionSignature = 2;
    private const int NoAction = -1;

    private readonly List<int> availableActions = new();    // action ที่ผู้เล่นเลือกได้ตอนนี้ (เรียงตามที่แสดงบนจอ)
    private int CurrentFocusActionIndex = 0;                // index ใน availableActions (ไม่ใช่ id)
    private int selectedAction = NoAction;                  // action ที่ "เลือกไว้/เตรียมไว้" รอยืนยัน (NoAction = ไม่มี)

    // action ที่ต้องกดซ้ำ (Space หรือคลิกอีกครั้ง) ถึงจะทำจริง — เพิ่ม action ใหม่ที่ต้องยืนยันได้ที่นี่
    private static bool NeedsSpaceConfirm(int actionId) => actionId == ActionInteract;

    private bool changingFocus;     // true ระหว่าง FocusAndUseAction (กัน RefreshActionList ที่ถูกเรียกซ้อนมาแตะการเลือก)
    private int armedFrame = -1;    // เฟรมที่ action ปัจจุบันถูก arm (คลิกยืนยันในเฟรมเดียวกันไม่นับ)
    private int lastSelectFrame = -1;           // กัน SelectAction ถูกเรียกซ้ำในเฟรมเดียวกัน
    private int lastSelectId = NoAction;        //   (ปุ่มมี onClick 2 ตัว: BindActionButton + CharacterActionList)

    private readonly List<(Button button, UnityEngine.Events.UnityAction callback)> actionButtons = new();
    private readonly HashSet<PlayerMove> warnedSignatureEntry = new();    // เตือน icon ไม่ครบแค่ครั้งเดียวต่อตัวละคร

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

        // Holding for skip: Started เริ่มกด / Performed กดค้างจนครบเวลา / Canceled ปล่อยก่อนครบ
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
            Debug.LogError("ไม่พบ TurnGameManager ใน Scene", this);
            return;
        }

        // จุดเชื่อมหลัก: ทุกครั้งที่สถานะเกมเปลี่ยน HUD จะรีเฟรชทั้งหมดผ่าน RefreshFromTurnManager
        turnGameManager.StateChanged += RefreshFromTurnManager;
        turnGameManager.GameEnded += OnGameEnded;

        SetupTurnLimitButton();
        SetUpActionList();
        RefreshFromTurnManager();
    }

    #endregion

    #region Setup (ผูกปุ่ม)

    // ปุ่มกดค้างเพื่อข้ามเทิร์น: HoldCompleted = ข้ามเทิร์นจริง, HoldStarted/Canceled = แค่ภาพปุ่มกดลง/ยกขึ้น
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

        // -= ก่อน += กัน subscribe ซ้ำ
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

    #endregion

    #region Turn Manager Sync (เกม -> HUD)

    // ถูกเรียกทุกครั้งที่ TurnGameManager.StateChanged ยิง
    // หมายเหตุ: CancelMoveTargeting/CancelSkill อาจยิง StateChanged "ซ้อน" ระหว่างที่เรากำลังจัดการคลิก
    //           (ดู changingFocus ใน Action Selection)
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
            // เปลี่ยนตัวละคร -> เริ่มโฟกัส action แรก และล้างสิ่งที่เลือก/arm ค้างไว้
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

    // เทิร์นเปลี่ยน -> อัปเดตตัวเลข และสั่งเข็มนาฬิกาใน UITurnCounter หมุน (อนิเมชัน 1 วิ)
    public void UpdateTurnCounter()
    {
        if (TurnGameManager.Instance == null) return;
        if (TurnLimitText != null) TurnLimitText.text = $"{TurnGameManager.Instance.CurrentTurn}";
        if (TurnCounter != null) TurnCounter.LegMoving(TurnGameManager.Instance.CurrentTurn);
    }

    private void OnGameEnded(bool won, string reason)
    {
        ActionUI.SetActive(false);

        Debug.Log(won ? "VICTORY: " + reason : "GAME OVER: " + reason);
    }

    #endregion

    #region Character & Hazard Icons

    // สร้างไอคอนตัวละคร (ซ้าย, กดเลือกได้) และ Hazard (ขวา, กดเพื่อเล็ง) จาก CharacterList / HazardList
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

    // ปรับระยะห่างไอคอนได้ที่นี่: 30 = ระยะจากขอบจอ, 130 = ระยะห่างระหว่างไอคอน, -20 = ระยะจากขอบบน
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

    // ไฮไลต์ไอคอนของตัวที่เล่นอยู่ และทำให้ตัวที่ตาย/เล่นจบแล้ว/ไม่มีเป้าหมาย กดไม่ได้
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

    // ---- ค้นหาข้อมูลตัวละครใน CharacterList ----

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

    #region Action List (แสดงผลรายการ action)

    // รีเฟรชรายการ action ทั้งหมดตามสถานะเกม — "จุดที่มีผลต่อเกมมากที่สุดของฝั่ง UI"
    //  1) สร้าง availableActions จาก CanChooseMove / CanChooseReact / CanChooseSkill
    //  2) action ที่โฟกัสอยู่: Interact -> arm รอยืนยัน, action อื่น -> ยืนยัน (ทำทันที)
    //  3) จัดตำแหน่งปุ่มบนจอ
    //  4) ถ้าอยู่ในโหมดเล็ง Move/Skill อยู่ ให้โฟกัสและเลือก action นั้นให้ตรงกับเกม
    public void RefreshActionList(PlayerMove current)
    {
        availableActions.Clear();
        MoveAction.SetActive(false);
        InteractAction.SetActive(false);
        SignatureAction.SetActive(false);

        // ซ่อน action ทั้งหมดเมื่อ: ไม่มีผู้เล่น / เกมจบ / เฟสศัตรู / กำลังรอตัวละครเดิน
        bool show = current != null && !turnGameManager.IsGameComplete && !turnGameManager.IsEnemyPhase &&
            !turnGameManager.IsWaitingForMovement;

        ActionUI.SetActive(show);
        if (!show) { selectedAction = NoAction; return; }

        // ลำดับที่ add ที่นี่ = ลำดับที่แสดงบนจอ (ล่างขึ้นบน)
        if (turnGameManager.CanChooseMove) availableActions.Add(ActionMove);
        if (turnGameManager.CanChooseReact) availableActions.Add(ActionInteract);
        if (turnGameManager.CanChooseSkill) availableActions.Add(ActionSignature);

        if (selectedAction != NoAction && !availableActions.Contains(selectedAction)) selectedAction = NoAction;

        if (availableActions.Count > 0)
        {
            CurrentFocusActionIndex = Mathf.Clamp(CurrentFocusActionIndex, 0, availableActions.Count - 1);
            int focusId = availableActions[CurrentFocusActionIndex];

            // ถ้ากำลังอยู่ใน FocusAndUseAction (Cancel* ยิง StateChanged ซ้อนเข้ามา)
            // ผู้เรียกจะตัดสินเองว่าจะ arm/ยืนยันอะไร -> รอบนี้ห้ามแตะการเลือก
            if (!changingFocus)
            {
                if (NeedsSpaceConfirm(focusId)) Arm(focusId);   // Interact ที่ไฮไลต์อยู่ = arm: รอ Space หรือคลิกอีกครั้ง
                else ConfirmAction(focusId);                    // Move/Signature = ทำทันที
            }
        }

        UpdateSignatureUI(current);

        // ระยะห่างปุ่ม action: startY = ปุ่มล่างสุด, gapY = ระยะห่างระหว่างปุ่ม, posX = ระยะจากขอบซ้าย
        // (ขนาดตอนเลือกที่ขยายใน CharacterActionList เป็นแค่ localScale จึงไม่กระทบ gapY)
        const float startY = 35f, gapY = 84f, posX = 32f;
        for (int i = 0; i < availableActions.Count; i++)
        {
            GameObject go = GetActionObject(availableActions[i]);
            go.SetActive(true);
            go.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, startY + i * gapY);
        }

        // sync กับเกม: ถ้าเกมอยู่ในโหมดเล็ง ให้ UI ชี้ action นั้น (เกมคือต้นฉบับของสถานะ)
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

    // ไฮไลต์ action ที่โฟกัสอยู่ (ที่เหลือ Unselected) — ภาพตอนเลือก (ขยาย/ตัวหนา/sprite) อยู่ใน CharacterActionList
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
    }

    // ---- Signature (ชื่อ + ไอคอนเฉพาะตัวละคร) ----

    // ตั้งชื่อ/ไอคอนของปุ่ม Signature ตามตัวละครที่เล่นอยู่ (ข้อมูลมาจาก CharacterEntry ใน Inspector)
    // ต้องใช้ SetSprites เท่านั้น — CharacterActionList.Update() เขียน sprite ทับทุกเฟรม ห้าม set image.sprite ตรงๆ
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

    // ชื่อ skill ตั้งต้นเมื่อไม่ได้กรอก skillName ใน Inspector — เพิ่มตัวละครใหม่ให้เพิ่ม case ที่นี่
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

    #region Action Selection (ตรรกะเลือก/เตรียม/ยืนยัน/ยกเลิก)

    // สรุปกติกา:
    //  - A/W/S/D  : เลื่อนโฟกัส + ใช้ action นั้นทันที (ยกเลิก action/โหมดเล็งเดิมก่อน)
    //  - Space    : ยืนยัน action ที่โฟกัสอยู่
    //  - คลิกปุ่ม : SelectAction() -> action อื่นอยู่ = เลือก/เตรียม, action นั้นถูกเตรียมไว้แล้ว = ยืนยัน
    //  - Interact เป็น action ที่ต้องยืนยันอีกที (NeedsSpaceConfirm): เลือกแล้ว "arm" รอ Space/คลิกอีกครั้ง
    //  - Move/Signature ทำทันทีที่เลือก (เข้าโหมดเล็งช่อง) ยกเลิกด้วยการเลือก action อื่น/GoBack

    // เลือก/คลิก action (เรียกจากปุ่มบนจอ) — จุดตัดสินว่า "เตรียม" หรือ "ทำจริง"
    public void SelectAction(int actionId)
    {
        if (turnGameManager == null || !availableActions.Contains(actionId)) return;

        // ปุ่มมี onClick 2 ตัว คลิกเดียวอาจเข้ามา 2 ครั้งในเฟรมเดียว
        // ถ้าไม่กัน ครั้งที่ 2 จะถูกนับเป็น "คลิกที่สอง" แล้ว Interact ทำงานทันที
        if (Time.frameCount == lastSelectFrame && actionId == lastSelectId) return;
        lastSelectFrame = Time.frameCount;
        lastSelectId = actionId;

        // ยืนยันได้ก็ต่อเมื่อ action นั้น arm ไว้ "ในเฟรมก่อนหน้า" และยังโฟกัสอยู่เท่านั้น
        if (selectedAction == actionId && Time.frameCount > armedFrame &&
            availableActions[CurrentFocusActionIndex] == actionId)
        {
            ConfirmAction(actionId);
            return;
        }
        FocusAndUseAction(availableActions.IndexOf(actionId));
    }

    // ย้ายโฟกัสไป action ใหม่: ยกเลิกของเดิม -> Interact: arm รอ / อื่นๆ: ทำทันที
    private void FocusAndUseAction(int newIndex)
    {
        // ระหว่างช่วงนี้ Cancel* ยิง StateChanged ซ้อน -> RefreshActionList ต้องไม่ arm/ยืนยันเอง
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
            selectedAction = NoAction;   // บังคับ arm ใหม่ ไม่ว่า refresh ซ้อนจะตั้งอะไรไว้
            Arm(id);
            UpdateActionFocusUI();
            return;
        }
        UpdateActionFocusUI();
        ConfirmAction(id);
    }

    // "เตรียม" action: ตั้งเป็น selectedAction แล้วรอยืนยัน (ยังไม่เรียกเกม)
    private void Arm(int id)
    {
        if (selectedAction != id) armedFrame = Time.frameCount;
        selectedAction = id;
    }

    // ===== จุดที่ส่งคำสั่งเข้า TurnGameManager จริง (มีผลต่อเกมโดยตรง) =====
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

    // ทำ Interact จริง (เรียก TurnGameManager.ChooseReact)
    private void ConfirmInteract()
    {
        turnGameManager.ChooseReact();
    }

    // ล้างการเลือก และออกจากโหมดเล็ง Move/Skill ที่ค้างอยู่ (อาจยิง StateChanged ซ้อน)
    private void CancelPendingAction()
    {
        selectedAction = NoAction;
        if (turnGameManager == null) return;
        if (turnGameManager.IsMoveTargeting) turnGameManager.CancelMoveTargeting();
        else if (turnGameManager.IsSkillTargeting) turnGameManager.CancelSkill();
    }

    // ปุ่มย้อนกลับ/ยกเลิก: ออกจากโหมดเล็ง ถ้าไม่ได้เล็งอยู่ก็แค่ล้างที่ arm ไว้
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

    // ---- เลือก Action: A/W/S/D เลื่อนโฟกัส, Space ยืนยัน ----
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

    // ---- เลือกตัวละคร: Tab = ตัวถัดไปที่เลือกได้, เลข 1-9 = ตัวที่ n ใน CharacterList ----
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
                if (TryFocusCharacter(i)) break;    // ข้ามตัวที่เลือกไม่ได้ (ตาย/เล่นจบแล้ว)
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

    // ---- ข้ามเทิร์น (กดค้าง) ----
    // Started = ปุ่มกดลง (ภาพ) / Performed = กดค้างครบเวลา -> ข้ามเทิร์นจริง / Canceled = ปล่อย -> ปุ่มยกขึ้น
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

    // ---- ข้ามเทิร์นด้วยเมาส์ (HoldButton): ใช้แค่ภาพปุ่ม ส่วนการข้ามจริงมาจาก HoldCompleted ----
    private void OnMouseSkipStart()
    {
        if (TurnCounter != null) TurnCounter.PressButton();
    }

    private void OnMouseSkipCancel()
    {
        if (TurnCounter != null) TurnCounter.ReleaseButton();
    }

    #endregion

    #region UI Callbacks (ให้ปุ่ม/ไอคอนเรียกจาก Inspector หรือโค้ดอื่น)

    // จบเทิร์นจริง (มีผลต่อเกมโดยตรง)
    public void OnClickEndTurn() { if (turnGameManager != null) turnGameManager.ChooseEndTurn(); }

    // คลิกไอคอนตัวละคร -> สลับไปเล่นตัวนั้น (ถ้าเลือกได้)
    public void OnCharacterIconClicked(GameObject target)
    {
        if (turnGameManager == null || target == null) return;
        PlayerMove p = target.GetComponent<PlayerMove>();
        if (p != null) turnGameManager.TrySelectPlayer(p);
    }

    // คลิกไอคอน Hazard -> ส่งให้ PlayerController เล็ง (ลูกธนูกับดักหรือศัตรู)
    // เพิ่มชนิด Hazard ใหม่ที่คลิกเล็งได้ให้เพิ่มที่นี่
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

    private void OnShowPauseUI(InputAction.CallbackContext context) => SetPaused(!IsPaused);

    // หยุด/เล่นต่อ: UI ปกติแค่กดไม่ได้ (blocksRaycasts) ไม่ได้ SetActive(false) จึงยังมองเห็นอยู่
    // การกดคีย์บอร์ดทุกตัวใน class นี้เช็ก IsPaused แล้ว
    public void SetPaused(bool paused)
    {
        if (IsPaused == paused) return;
        IsPaused = paused;

        // กันปุ่ม skip ค้างอยู่ในสภาพกดลง
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

    // เฟด blackScreen (ไปที่ pauseDimAlpha) และ PauseUI พร้อมกัน ใช้ unscaledDeltaTime (ทำงานได้แม้ timeScale = 0)
    // ปรับความเร็ว/ความมืดได้ที่ fadeTime และ pauseDimAlpha ใน Inspector
    private IEnumerator PauseFade(bool paused)
    {
        if (paused)
        {
            if (inGameGroup != null) inGameGroup.blocksRaycasts = false;   // มองเห็น แต่กดไม่ได้
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

        // เล่นต่อ: ปิด PauseUI/จอดำ หลังเฟดจบ แล้วค่อยให้ UI ปกติกดได้อีกครั้ง
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