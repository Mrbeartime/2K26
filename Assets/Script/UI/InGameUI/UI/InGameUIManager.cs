using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// HUD �ͧ��: �ͤ͹����Ф�/Hazard, ��¡�� Action (Move / Interact / Signature), ��ǹѺ���� ����к� Pause
/// �����Ѵ�Թ��ԡ����ͧ � ���觤����价�� TurnGameManager �����ʴ��ŵ��ʶҹз�����Ѻ��
///
/// �ӴѺ region: Fields -> Unity Lifecycle -> Setup -> Turn Manager Sync -> Icons
///               -> Action List (�ʴ���) -> Action Selection (��á����͡/�׹�ѹ)
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

    // ---- �к���¹͡ ----
    private ProjectDInputAction _inputAction;
    private TurnGameManager turnGameManager;
    private HoldButton turnLimitButton;

    // ---- ⿡�ʵ���Ф� ----
    private PlayerMove previousCharacter;           // ���Ǩ��ҵ�Ƿ�������������¹�����ѧ (����¹ -> ����⿡�� action)
    private int CurrentFocusCharacterIndex = 0;     // index � CharacterList

    // ---- Action ----
    // id �ͧ action (����� index): Move = 0, Interact = 1, Signature = 2
    // ��ҵ�ͧ�ç�Ѻ enum actionType � CharacterActionList
    private const int ActionMove = 0, ActionInteract = 1, ActionSignature = 2;
    private const int NoAction = -1;

    private readonly List<int> availableActions = new();    // action �����������͡��͹��� (���§�������ʴ�����)
    private int CurrentFocusActionIndex = 0;                // index � availableActions (����� id)
    private int selectedAction = NoAction;                  // action ��� "���͡���/��������" ���׹�ѹ (NoAction = �����)

    // action ����ͧ����� (Space ���ͤ�ԡ�ա����) �֧�зӨ�ԧ � ���� action �������ͧ�׹�ѹ������
    private static bool NeedsSpaceConfirm(int actionId) => actionId == ActionInteract;

    private bool changingFocus;     // true �����ҧ FocusAndUseAction (�ѹ RefreshActionList ���١���¡��͹���С�����͡)
    private int armedFrame = -1;    // ������ action �Ѩ�غѹ�١ arm (��ԡ�׹�ѹ�������ǡѹ���Ѻ)
    private int lastSelectFrame = -1;           // �ѹ SelectAction �١���¡����������ǡѹ
    private int lastSelectId = NoAction;        //   (������ onClick 2 ���: BindActionButton + CharacterActionList)

    private readonly List<(Button button, UnityEngine.Events.UnityAction callback)> actionButtons = new();
    private readonly HashSet<PlayerMove> warnedSignatureEntry = new();    // ��͹ icon ���ú��������ǵ�͵���Ф�

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

        // Holding for skip: Started ������� / Performed ����ҧ���ú���� / Canceled ����¡�͹�ú
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
            Debug.LogError("ไม่พบ TurnGameManager ใน Scene", this);
            return;
        }

        // �ش�������ѡ: �ء���駷��ʶҹ�������¹ HUD �����ê��������ҹ RefreshFromTurnManager
        turnGameManager.StateChanged += RefreshFromTurnManager;
        turnGameManager.GameEnded += OnGameEnded;

        SetupTurnLimitButton();
        SetUpActionList();
        RefreshFromTurnManager();
    }

    #endregion

    #region Setup (�١����)

    // ��������ҧ���͢�������: HoldCompleted = �������침�ԧ, HoldStarted/Canceled = ���Ҿ������ŧ/¡���
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

        // -= ��͹ += �ѹ subscribe ���
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

    #region Turn Manager Sync (�� -> HUD)

    // �١���¡�ء���駷�� TurnGameManager.StateChanged �ԧ
    // �����˵�: CancelMoveTargeting/CancelSkill �Ҩ�ԧ StateChanged "��͹" �����ҧ�����ҡ��ѧ�Ѵ��ä�ԡ
    //           (�� changingFocus � Action Selection)
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
            // ����¹����Ф� -> �����⿡�� action �á �����ҧ��觷�����͡/arm ��ҧ���
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

    // ��������¹ -> �ѻവ����Ţ ������������ԡ�� UITurnCounter ��ع (͹����ѹ 1 ��)
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

    // ���ҧ�ͤ͹����Ф� (����, �����͡��) ��� Hazard (���, ���������) �ҡ CharacterList / HazardList
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

    // ��Ѻ������ҧ�ͤ͹������: 30 = ���Шҡ�ͺ��, 130 = ������ҧ�����ҧ�ͤ͹, -20 = ���Шҡ�ͺ��
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

    // ���ŵ��ͤ͹�ͧ��Ƿ��������� ��з�����Ƿ����/��蹨�����/������������ �������
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

    // ---- ���Ң����ŵ���Ф�� CharacterList ----

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

    #region Action List (�ʴ�����¡�� action)

    // ���ê��¡�� action ���������ʶҹ��� � "�ش����ռŵ�����ҡ����ش�ͧ��� UI"
    //  1) ���ҧ availableActions �ҡ CanChooseMove / CanChooseReact / CanChooseSkill
    //  2) action ���⿡������: Interact -> arm ���׹�ѹ, action ��� -> �׹�ѹ (�ӷѹ��)
    //  3) �Ѵ���˹觻�������
    //  4) ��������������� Move/Skill ���� ���⿡��������͡ action ������ç�Ѻ��
    public void RefreshActionList(PlayerMove current)
    {
        availableActions.Clear();
        MoveAction.SetActive(false);
        InteractAction.SetActive(false);
        SignatureAction.SetActive(false);

        // ��͹ action �����������: ����ռ����� / ���� / ���ѵ�� / ���ѧ�͵���Ф��Թ
        bool show = current != null && !turnGameManager.IsGameComplete && !turnGameManager.IsEnemyPhase &&
            !turnGameManager.IsWaitingForMovement;

        ActionUI.SetActive(show);
        if (!show) { selectedAction = NoAction; return; }

        // �ӴѺ��� add ����� = �ӴѺ����ʴ����� (��ҧ��鹺�)
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

            // ��ҡ��ѧ����� FocusAndUseAction (Cancel* �ԧ StateChanged ��͹�����)
            // ������¡�еѴ�Թ�ͧ��Ҩ� arm/�׹�ѹ���� -> �ͺ��������С�����͡
            if (!changingFocus)
            {
                if (NeedsSpaceConfirm(focusId)) Arm(focusId);   // Interact ������ŵ����� = arm: �� Space ���ͤ�ԡ�ա����
                else ConfirmAction(focusId);                    // Move/Signature = �ӷѹ��
            }
        }

        if (turnGameManager.CurrentPlayer != current || turnGameManager.IsEnemyPhase) return;
        UpdateSignatureUI(current);

        // ������ҧ���� action: startY = ������ҧ�ش, gapY = ������ҧ�����ҧ����, posX = ���Шҡ�ͺ����
        // (��Ҵ�͹���͡������� CharacterActionList ���� localScale �֧����з� gapY)
        const float startY = 35f, gapY = 84f, posX = 32f;
        for (int i = 0; i < availableActions.Count; i++)
        {
            GameObject go = GetActionObject(availableActions[i]);
            go.SetActive(true);
            go.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, startY + i * gapY);
        }

        // sync �Ѻ��: ����������������� ��� UI ��� action ��� (����͵鹩�Ѻ�ͧʶҹ�)
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

    // ���ŵ� action ���⿡������ (�������� Unselected) � �Ҿ�͹���͡ (����/���˹�/sprite) ����� CharacterActionList
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

    // ---- Signature (���� + �ͤ͹੾�е���Ф�) ----

    // ��駪���/�ͤ͹�ͧ���� Signature �������Ф÷��������� (�������Ҩҡ CharacterEntry � Inspector)
    // ��ͧ�� SetSprites ��ҹ�� � CharacterActionList.Update() ��¹ sprite �Ѻ�ء��� ���� set image.sprite �ç�
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

    // ���� skill ��駵������������͡ skillName � Inspector � ��������Ф������������ case �����
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

    #region Action Selection (��á����͡/�����/�׹�ѹ/¡��ԡ)

    // ��ػ��ԡ�:
    //  - A/W/S/D  : ����͹⿡�� + �� action ��鹷ѹ�� (¡��ԡ action/������������͹)
    //  - Space    : �׹�ѹ action ���⿡������
    //  - ��ԡ���� : SelectAction() -> action ������� = ���͡/�����, action ��鹶١������������ = �׹�ѹ
    //  - Interact �� action ����ͧ�׹�ѹ�ա�� (NeedsSpaceConfirm): ���͡���� "arm" �� Space/��ԡ�ա����
    //  - Move/Signature �ӷѹ�շ�����͡ (���������秪�ͧ) ¡��ԡ���¡�����͡ action ���/GoBack

    // ���͡/��ԡ action (���¡�ҡ��������) � �ش�Ѵ�Թ��� "�����" ���� "�Ө�ԧ"
    public void SelectAction(int actionId)
    {
        if (turnGameManager == null || !availableActions.Contains(actionId)) return;

        // ������ onClick 2 ��� ��ԡ�����Ҩ����� 2 ������������
        // ������ѹ ���駷�� 2 �ж١�Ѻ�� "��ԡ����ͧ" ���� Interact �ӧҹ�ѹ��
        if (Time.frameCount == lastSelectFrame && actionId == lastSelectId) return;
        lastSelectFrame = Time.frameCount;
        lastSelectId = actionId;

        // �׹�ѹ���������� action ��� arm ��� "������͹˹��" ����ѧ⿡��������ҹ��
        if (selectedAction == actionId && Time.frameCount > armedFrame &&
            availableActions[CurrentFocusActionIndex] == actionId)
        {
            ConfirmAction(actionId);
            return;
        }
        FocusAndUseAction(availableActions.IndexOf(actionId));
    }

    // ����⿡��� action ����: ¡��ԡ�ͧ��� -> Interact: arm �� / ����: �ӷѹ��
    private void FocusAndUseAction(int newIndex)
    {
        // �����ҧ��ǧ��� Cancel* �ԧ StateChanged ��͹ -> RefreshActionList ��ͧ��� arm/�׹�ѹ�ͧ
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
            selectedAction = NoAction;   // �ѧ�Ѻ arm ���� ������ refresh ��͹�е���������
            Arm(id);
            UpdateActionFocusUI();
            return;
        }
        UpdateActionFocusUI();
        ConfirmAction(id);
    }

    // "�����" action: ����� selectedAction �������׹�ѹ (�ѧ������¡��)
    private void Arm(int id)
    {
        if (selectedAction != id) armedFrame = Time.frameCount;
        selectedAction = id;
    }

    // ===== �ش����觤������� TurnGameManager ��ԧ (�ռŵ�����µç) =====
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

    // �� Interact ��ԧ (���¡ TurnGameManager.ChooseReact)
    private void ConfirmInteract()
    {
        turnGameManager.ChooseReact();
    }

    // ��ҧ������͡ ����͡�ҡ������� Move/Skill ����ҧ���� (�Ҩ�ԧ StateChanged ��͹)
    private void CancelPendingAction()
    {
        selectedAction = NoAction;
        if (turnGameManager == null) return;
        if (turnGameManager.IsMoveTargeting) turnGameManager.CancelMoveTargeting();
        else if (turnGameManager.IsSkillTargeting) turnGameManager.CancelSkill();
    }

    // ������͹��Ѻ/¡��ԡ: �͡�ҡ������� ��������������������ҧ��� arm ���
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

    // ---- ���͡ Action: A/W/S/D ����͹⿡��, Space �׹�ѹ ----
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

    // ---- ���͡����Ф�: Tab = ��ǶѴ价�����͡��, �Ţ 1-9 = ��Ƿ�� n � CharacterList ----
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
                if (TryFocusCharacter(i)) break;    // ������Ƿ�����͡����� (���/��蹨�����)
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

    // ---- �������� (����ҧ) ----
    // Started = ������ŧ (�Ҿ) / Performed = ����ҧ�ú���� -> �������침�ԧ / Canceled = ����� -> ����¡���
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

    // ---- �������카�������� (HoldButton): �����Ҿ���� ��ǹ��â�����ԧ�Ҩҡ HoldCompleted ----
    private void OnMouseSkipStart()
    {
        if (TurnCounter != null) TurnCounter.PressButton();
    }

    private void OnMouseSkipCancel()
    {
        if (TurnCounter != null) TurnCounter.ReleaseButton();
    }

    #endregion

    #region UI Callbacks (������/�ͤ͹���¡�ҡ Inspector ���������)

    // �����침�ԧ (�ռŵ�����µç)
    public void OnClickEndTurn() { if (turnGameManager != null) turnGameManager.ChooseEndTurn(); }

    // ��ԡ�ͤ͹����Ф� -> ��Ѻ���蹵�ǹ�� (������͡��)
    public void OnCharacterIconClicked(GameObject target)
    {
        if (turnGameManager == null || target == null) return;
        PlayerMove p = target.GetComponent<PlayerMove>();
        if (p != null) turnGameManager.TrySelectPlayer(p);
    }

    // ��ԡ�ͤ͹ Hazard -> ����� PlayerController ��� (�١��١Ѻ�ѡ�����ѵ��)
    // ������Դ Hazard �������ԡ�����������������
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

    // ��ش/��蹵��: UI �����衴����� (blocksRaycasts) ����� SetActive(false) �֧�ѧ�ͧ�������
    // ��á�������촷ء���� class ����� IsPaused ����
    public void SetPaused(bool paused)
    {
        if (IsPaused == paused) return;
        IsPaused = paused;

        // �ѹ���� skip ��ҧ�������Ҿ��ŧ
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

    // ࿴ blackScreen (价�� pauseDimAlpha) ��� PauseUI ������ѹ �� unscaledDeltaTime (�ӧҹ����� timeScale = 0)
    // ��Ѻ��������/�����״���� fadeTime ��� pauseDimAlpha � Inspector
    private IEnumerator PauseFade(bool paused)
    {
        if (paused)
        {
            if (inGameGroup != null) inGameGroup.blocksRaycasts = false;   // �ͧ��� �衴�����
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

        // ��蹵��: �Դ PauseUI/�ʹ� ��ѧ࿴�� ���Ǥ������ UI ���ԡ����ա����
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
