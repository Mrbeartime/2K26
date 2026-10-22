using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterActionList : MonoBehaviour
{

    [SerializeField] private bool isSelected = false;
    [SerializeField] private bool isArmed = false;   // เลือกไว้แล้ว รอกด Space ยืนยัน
    [Tooltip("สีย้อมตอนรอยืนยัน (ใช้เมื่อ sprites มีไม่ถึง 3 ช่อง)")]
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
        image = GetComponent<Image>();
        if (image != null) baseColor = image.color;
        if (image != null && sprites != null && sprites.Length > 0)
            image.sprite = sprites[0];
    }

    public void Setup(GameObject target)
    {
        return;
    }

    public void SetActionType(int id) => actionType = (CharacterActionType)id;

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
        // ��� enum ��ͧ�ç�Ѻ id � InGameUIManager: Move = 0, Interact = 1, Sig = 2
        // InGameUIManager �繽��¤��������͡�������� (�������ǡѺ isSelected �ͧ���ʹ��)
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
            if (isArmed && hasArmedSprite)
            {
                image.sprite = sprites[2];
                image.rectTransform.sizeDelta = new Vector2(86f, 86f);
                SetPosX(17f);   // ปรับ x ของตอน arm ตามต้องการ
            }
            else if (isSelected || isArmed)
            {
                image.sprite = sprites[1];
                image.rectTransform.sizeDelta = new Vector2(86f, 86f);
                SetPosX(17f);
            }
            else
            {
                image.sprite = sprites[0];
                image.rectTransform.sizeDelta = new Vector2(70f, 70f);
                SetPosX(25f);
            }
        }

        // --- �Ѵ�������ͧ����ѡ�� ---
        if (actionName != null)
        {
            if (isSelected || isArmed)
            {
                actionName.fontStyle = FontStyles.Bold;
                actionName.fontSize = 36;
                SetPosXText(98f);
            }
            else
            {
                actionName.fontStyle = FontStyles.Normal;
                actionName.fontSize = 30;
                SetPosXText(90f);
            }
        }
    }
    private void SetPosX(float x)
    {
        Vector2 p = image.rectTransform.anchoredPosition;
        p.x = x;
        image.rectTransform.anchoredPosition = p;
    }
    private void SetPosXText(float x)
    {
        Vector2 p = actionName.rectTransform.anchoredPosition;
        p.x = x;
        actionName.rectTransform.anchoredPosition = p;

    }
}
