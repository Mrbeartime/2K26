using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObjectIcon : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.4f;

    public GameObject TargetObject { get; private set; }

    // [0] = normal, [1] = selected
    public Sprite[] sprites;
    public Image bgEffectImage;   // the "Glow" child

    [Tooltip("Image that shows the portrait (the 'Archer' child). Auto-detected if empty.")]
    [SerializeField] private Image iconImage;

    private bool isSelected;
    private bool isDisabled;
    private bool selectable = true;

    private CanvasGroup canvasGroup;
    private Button button;
    private Vector3 originalScale;

    // every image that should be tinted when disabled (everything except the glow)
    private readonly List<Image> tintImages = new();
    private readonly List<Color> tintOriginals = new();

    private void Awake()
    {
        originalScale = transform.localScale;

        // Find images anywhere in the prefab, not just on the root
        foreach (Image img in GetComponentsInChildren<Image>(true))
        {
            if (img == bgEffectImage) continue;
            tintImages.Add(img);
            tintOriginals.Add(img.color);
            if (iconImage == null) iconImage = img;   // first non-glow image = portrait
        }

        if (iconImage == null)
            Debug.LogWarning($"{name}: no Image found for the icon portrait.", this);

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // The Button needs a Graphic on its own object to receive clicks
        button = GetComponent<Button>();
        if (button == null) button = gameObject.AddComponent<Button>();

        if (GetComponent<Graphic>() == null)
        {
            Image hit = gameObject.AddComponent<Image>();
            hit.color = Color.clear;          // invisible, only catches raycasts
            hit.raycastTarget = true;
        }
        if (button.targetGraphic == null) button.targetGraphic = GetComponent<Graphic>();

        button.onClick.RemoveListener(OnIconClicked);
        button.onClick.AddListener(OnIconClicked);

        ApplyVisual();
    }

    public void Setup(GameObject target, bool selectable = true)
    {
        TargetObject = target;
        this.selectable = selectable;
        ApplyVisual();
    }

    public void OnIconClicked()
    {
        if (isDisabled || TargetObject == null || InGameUIManager.Instance == null) return;

        if (TargetObject.TryGetComponent<PlayerMove>(out _))
        {
            if (!selectable) return;
            InGameUIManager.Instance.OnCharacterIconClicked(TargetObject);
            return;
        }

        if (TargetObject.TryGetComponent<Enemy>(out _) || TargetObject.TryGetComponent<TurnArrowTrap>(out _))
            InGameUIManager.Instance.OnEnemyIconClicked(TargetObject);
    }

    public void Selected()
    {
        if (isSelected) return;
        isSelected = true;
        ApplyVisual();
    }

    public void Unselected()
    {
        if (!isSelected) return;
        isSelected = false;
        ApplyVisual();
    }

    public void SetDisabled(bool disabled)
    {
        if (isDisabled == disabled) return;
        isDisabled = disabled;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        bool showSelected = isSelected && !isDisabled;

        if (iconImage != null && sprites != null && sprites.Length > 0)
            iconImage.sprite = (showSelected && sprites.Length > 1) ? sprites[1] : sprites[0];

        if (bgEffectImage != null)
            bgEffectImage.enabled = showSelected;

        transform.localScale = isDisabled ? originalScale * 0.9f : originalScale;

        for (int i = 0; i < tintImages.Count; i++)
            tintImages[i].color = isDisabled ? Color.gray : tintOriginals[i];

        if (canvasGroup != null)
            canvasGroup.alpha = isDisabled ? disabledAlpha : 1f;
    }
}