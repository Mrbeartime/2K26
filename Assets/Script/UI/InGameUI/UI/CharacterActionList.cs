using UnityEngine;
using UnityEngine.UI;

public class CharacterActionList : MonoBehaviour
{

    [SerializeField] private bool isSelected = false;
    [SerializeField] private bool isArmed = false;   // เลือกไว้แล้ว รอกด Space ยืนยัน
    [Tooltip("สีย้อมตอนรอยืนยัน (ใช้เมื่อ sprites มีไม่ถึง 3 ช่อง)")]
    [SerializeField] private CharacterActionType actionType;

    public enum CharacterActionType
    {
        Move,
        Interact,
        Sig,
        Other
    }
    public Sprite[] sprites;

    private Image image;
    private Color baseColor = Color.white;

    private void Awake()
    {
        Button button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnIconClicked);

        image = GetComponent<Image>();
        if (image != null) baseColor = image.color;
        if (image != null && sprites != null && sprites.Length > 0)
            image.sprite = sprites[0];
    }

    public void Setup(GameObject target)
    {
        return;
    }

    public void OnIconClicked()
    {
        // ค่า enum ต้องตรงกับ id ใน InGameUIManager: Move = 0, Interact = 1, Sig = 2
        // การไฮไลต์ให้ InGameUIManager เป็นคนคุมทั้งหมด (ห้ามสลับ isSelected เองที่นี่)
        if (InGameUIManager.Instance == null) return;
        InGameUIManager.Instance.SelectAction((int)actionType);
    }
    public void Selected()
    {
        isSelected = true;
    }
    public void Unselected()
    {
        isSelected = false;
    }
    public void Update()
    {
        if (image == null) return;

        bool hasArmedSprite = sprites != null && sprites.Length > 2;

        // เลือก sprite: รอยืนยัน (sprites[2]) > โฟกัส (sprites[1]) > ปกติ (sprites[0])
        if (sprites != null && sprites.Length > 1)
        {
            if (isArmed && hasArmedSprite) image.sprite = sprites[2];
            else if (isSelected || isArmed) image.sprite = sprites[1];
            else image.sprite = sprites[0];
        }

    }
}