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
    private Animator focusAnimator;
    private static readonly int SelectedParameter = Animator.StringToHash("selected");

    private void Awake()
    {
        image = GetComponent<Image>();
        focusAnimator = GetComponent<Animator>();
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
    private void OnEnable()
    {
        UpdateFocusVisuals();
    }

    public void Update()
    {
        UpdateFocusVisuals();
    }

    private void UpdateFocusVisuals()
    {
        bool focused = isSelected || isArmed;
        if (focusAnimator != null && focusAnimator.runtimeAnimatorController != null)
            focusAnimator.SetBool(SelectedParameter, focused);

        if (image != null && sprites != null && sprites.Length > 0)
        {
            int index = isArmed && sprites.Length > 2 ? 2
                : focused && sprites.Length > 1 ? 1 : 0;
            image.sprite = sprites[index];
        }
        if (actionName != null)
            actionName.fontStyle = focused ? FontStyles.Bold : FontStyles.Normal;
        // Animator owns size, horizontal offsets and font size. The manager owns Y layout.
    }
}