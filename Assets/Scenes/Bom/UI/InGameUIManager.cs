using JetBrains.Annotations;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;

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
    [SerializeField] private GameObject[] CharacterList;
    [SerializeField] private GameObject[] HazardList;

    [Header("Icon Spawning")]
    [SerializeField] private GameObject IconPrefab; // ลาก Prefab ของ Icon มาใส่(มีอันเดียวมั้งตอนนี้)

    [Header("Input Actions")]
    private ProjectDInputAction _inputAction;
    private int MaxCharacters;
    private int CurrentFocusCharacterIndex = 0;
    private int CurrentFocusActionIndex = 0;
    

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if(_inputAction == null)
        {
            _inputAction = new ProjectDInputAction();
        }
    }

    private void OnEnable()
    {
        _inputAction.Enable();

        _inputAction.Gameplay_keyboard.SelectCharacter.performed += OnChangeCharacter;
        _inputAction.Gameplay_keyboard.SelectAction.performed += OnChangeAction;
        _inputAction.Gameplay_keyboard.ActionControl.performed += OnActionControl;
    }

    private void OnDisable()
    {
        _inputAction.Gameplay_keyboard.SelectCharacter.performed -= OnChangeCharacter;
        _inputAction.Gameplay_keyboard.SelectAction.performed -= OnChangeAction;
        _inputAction.Gameplay_keyboard.ActionControl.performed -= OnActionControl;

        _inputAction.Disable();
    }

    private void Start()
    {
        GetInformation();
        CreateCharacterandHazardIcon();
    }

    private void Update()
    {
        CurrentCharacterSelected();
    }

    public void GetInformation()
    {
        // โค้ดดึงข้อมูลว่ามี GameObject ตัวละครและอุปสรรคอะไรบ้างในเกมจาก GameManager ลงใน CharacterList และ HazardList

    }
    #region Setup UI and active UI
    private void CreateCharacterandHazardIcon()
    {
        if (CharacterList != null)
        {
            int index = 0;
            foreach (GameObject character in CharacterList)
            {
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
                    iconScript.Setup(character);
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
                    iconScript.Setup(hazard);
                }
                index++;
            }
        }
    }
    
    public void ShowActionList(GameObject character)
    {
            MoveAction.GetComponent<CharacterActionList>().Unselected();
            InteractAction.GetComponent<CharacterActionList>().Unselected();
            SignatureAction.GetComponent<CharacterActionList>().Unselected();

            //ใช้ Scriptable Object ไปใส่ตัวละคร จะได้อ่านชื่อ Action เฉพาะตัวได้ ไม่ก็หาทางอื่น

            //if Character is Walkable
            bool canMove = true;
        //if Character can Interact
        bool canInteract = true;
        //ถาม Character what is signature move named.
        bool canSignature = true;
        //mockup

        SignatureText.text = "Wait for Character System";


        //เรียงปุ่ม น่าจะไม่พลาด
        MoveAction.SetActive(canMove);
        InteractAction.SetActive(canInteract);
        SignatureAction.SetActive(canSignature);

        int index = 0;
        float startY = 35f;
        float gapY = 65f;
        float posX = 25f;
        bool haveAction = false;

        if (canMove)
        {
            MoveAction.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, startY + (index * gapY));
            index++;
            if (!haveAction)
            {
                MoveAction.GetComponent<CharacterActionList>().Selected();
                haveAction = true;
            }
        }

        if (canInteract)
        {
            InteractAction.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, startY + (index * gapY));
            index++;
            if (!haveAction)
            {
                InteractAction.GetComponent<CharacterActionList>().Selected();
                haveAction = true;
            }
        }

        if (canSignature)
        {
            SignatureAction.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, startY + (index * gapY));
            index++;
            if (!haveAction)
            {
                SignatureAction.GetComponent<CharacterActionList>().Selected(); 
                haveAction = true;
            }
        }

    }

    public void ShowTurnLimit()
    {
        // TurnLimitUI.SetActive(true);
        // การเปิดปิด TextMeshPro ต้องทำผ่าน gameObject
        // TurnLimitText.gameObject.SetActive(true); 
    }

    /*
    public void SelectCharacter(GameObject character)
    {
        // รอ GameManager บอกว่าผู้เล่นเลือก Character ตัวไหน

        //Gemini ดูอันนี้ : ถ้าจะเลือก Icon ที่เชื่อมกับ Character ตัวนั้นให้ทำยังไง
    }
    */
    public void SelectCharacter(GameObject character)
    {
        //ยังน่าจะใช้ไม่ได้จนกว่าจะมี GM
        ObjectIcon[] allIcons = CharacterUI.GetComponentsInChildren<ObjectIcon>();

        foreach (ObjectIcon icon in allIcons)
        {
            if (icon.TargetObject == character)
            {
                icon.Selected();
            }
            else
            {
                icon.Unselected();
            }
        }

        ShowActionList(character);
    }
    public void SelectAction(int actionIndex)
    {
        if (CharacterList == null || CharacterList.Length == 0) return;

        GameObject currentCharacter = CharacterList[CurrentFocusCharacterIndex];

        switch (actionIndex)
        {
            case 0:
                //Move
                break;

            case 1:
                //Interaction
                break;

            case 2:
                //Signature Action
                break;
        }
    }
    public void UpdateTurnCounter()
    {
        int currentTurn;
        //ขอเลขเทิร์นปัจจุบันจาก GameManager
        //TurnLimitText.text = currentTurn.ToString();
    }

    public void CurrentCharacterSelected()
    {
        // โค้ดตรวจสอบการเลือกตัวละคร
    }
    #endregion

    #region InputAction
    private void OnChangeCharacter(InputAction.CallbackContext context)
    {
            string key = context.control.name.ToLower();

            switch (key)
            {
                case "1":
                case "2":
                case "3":
                case "4":
                case "5":
                    int targetIndex = int.Parse(key) - 1;
                    if (CharacterList != null && targetIndex < CharacterList.Length)
                    {
                        CurrentFocusCharacterIndex = targetIndex;
                        UpdateCharacterFocusUI();
                    }
                    break;
                case "tab":
                    if (CharacterList == null || CharacterList.Length == 0) break;
                    CurrentFocusCharacterIndex++;
                    if (CurrentFocusCharacterIndex >= CharacterList.Length)
                        CurrentFocusCharacterIndex = 0;
                    UpdateCharacterFocusUI();
                    break;
            }
        }
    

    private void OnChangeAction(InputAction.CallbackContext context)
    {
            string key = context.control.name.ToLower();
            int maxActions = 3;

            if (key == "d" || key == "s")
            {
                CurrentFocusActionIndex--;
                if (CurrentFocusActionIndex < 0) CurrentFocusActionIndex = maxActions - 1;
                UpdateActionFocusUI();
            }
            else if (key == "a" || key == "w")
            {
                CurrentFocusActionIndex++;
                if (CurrentFocusActionIndex >= maxActions) CurrentFocusActionIndex = 0;
                UpdateActionFocusUI();
            }
            else if (key == "space")
            {
                SelectAction(CurrentFocusActionIndex);
        }
        }
    

    private void OnActionControl(InputAction.CallbackContext context)
    {
        return;//รอไปก่อน
    }
    private void UpdateCharacterFocusUI()
    {
        if (CharacterList == null || CharacterList.Length == 0) return;

        GameObject targetCharacter = CharacterList[CurrentFocusCharacterIndex];

        SelectCharacter(targetCharacter);
    }
    private void UpdateActionFocusUI()
    {
        // 1. Clear all selections first
        MoveAction.GetComponent<CharacterActionList>().Unselected();
        InteractAction.GetComponent<CharacterActionList>().Unselected();
        SignatureAction.GetComponent<CharacterActionList>().Unselected();

        // 2. Highlight the currently focused action
        // Assuming Index 0 = Move, 1 = Interact, 2 = Signature
        switch (CurrentFocusActionIndex)
        {
            case 0:
                MoveAction.GetComponent<CharacterActionList>().Selected();
                break;
            case 1:
                InteractAction.GetComponent<CharacterActionList>().Selected();
                break;
            case 2:
                SignatureAction.GetComponent<CharacterActionList>().Selected();
                break;
        }
    }
    #endregion
}