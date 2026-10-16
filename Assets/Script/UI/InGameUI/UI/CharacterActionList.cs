using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterActionList : MonoBehaviour
{

    [SerializeField] private bool isSelected = false;
    [SerializeField] private bool isArmed = false;   // เลือกแล้ว รอกด Space ยืนยัน
    [Tooltip("ต้องตรงกับ id ใน InGameUIManager: Move = 0, Interact = 1, Sig = 2")]
    [SerializeField] private CharacterActionType actionType;

    [SerializeField] private TextMeshProUGUI actionName;
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

    public void SetSprites(Sprite normal, Sprite focused = null, Sprite armed = null)
    {
        if (normal == null) return;
        if (focused == null) focused = normal;
        sprites = armed != null
            ? new[] { normal, focused, armed }
            : new[] { normal, focused };
    }

    public void OnIconClicked()
    {
        // ค่า enum ต้องตรงกับ id ใน InGameUIManager: Move = 0, Interact = 1, Sig = 2
        // InGameUIManager เป็นฝ่ายคุมว่าเลือกอะไรอยู่ (ไม่เกี่ยวกับ isSelected ของคลาสนี้)
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

        // เลือก sprite: ยืนยันแล้ว (sprites[2]) > โฟกัส (sprites[1]) > ปกติ (sprites[0])
        if (sprites != null && sprites.Length > 1)
        {
            if (isArmed && hasArmedSprite) image.sprite = sprites[2];
            else if (isSelected || isArmed) image.sprite = sprites[1];
            else image.sprite = sprites[0];
        }

        // --- จัดการเรื่องตัวอักษร ---
        if (actionName != null)
        {
            if (isSelected || isArmed)
            {
                actionName.fontStyle = FontStyles.Bold;
            }
            else
            {
                actionName.fontStyle = FontStyles.Normal;
            }
        }
    }
}