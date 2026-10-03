using UnityEngine;
using UnityEngine.UI;

public class ObjectIcon : MonoBehaviour
{
    [SerializeField] private bool isSelected = false;
    [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.4f;

    public GameObject TargetObject { get; private set; }

    // [0] = สถานะปกติ, [1] = สถานะถูกเลือก
    public Sprite[] sprites;

    private Image image;
    private CanvasGroup canvasGroup;
    private bool selectable = true;

    private void Awake()
    {
        image = GetComponent<Image>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Button button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnIconClicked);

        ApplyVisual();
    }

    // selectable = true สำหรับตัวละคร (กดเพื่อเลือกได้), false สำหรับ Hazard
    public void Setup(GameObject target, bool selectable = true)
    {
        TargetObject = target;
        this.selectable = selectable;
    }

    public void OnIconClicked()
    {
        //if (!selectable || TargetObject == null || InGameUIManager.Instance == null) return;
        //InGameUIManager.Instance.OnCharacterIconClicked(TargetObject); //ของเก่า

        if (TargetObject == null ||
        InGameUIManager.Instance == null)
            return;

        // =========================
        // Player Icon
        // =========================
        if (TargetObject.TryGetComponent<PlayerMove>(out _))
        {
            if (!selectable)
                return;

            InGameUIManager.Instance
                .OnCharacterIconClicked(TargetObject);

            return;
        }

        // =========================
        // Enemy Icon
        // =========================
        if (TargetObject.TryGetComponent<Enemy>(out _))
        {
            InGameUIManager.Instance
                .OnEnemyIconClicked(TargetObject);

            return;
        }
    }

    public void Selected()
    {
        isSelected = true;
        ApplyVisual();
    }

    public void Unselected()
    {
        isSelected = false;
        ApplyVisual();
    }

    // จางลง = ทำ action ไปแล้ว / ตายแล้ว
    public void SetDisabled(bool disabled)
    {
        if (canvasGroup != null) canvasGroup.alpha = disabled ? disabledAlpha : 1f;
    }

    private void ApplyVisual()
    {
        if (image == null || sprites == null || sprites.Length == 0) return;
        image.sprite = (isSelected && sprites.Length > 1) ? sprites[1] : sprites[0];
    }
}