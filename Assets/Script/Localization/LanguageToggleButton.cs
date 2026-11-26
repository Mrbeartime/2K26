using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put this on a Button or a Toggle (for example a row in the Settings panel) to switch between Thai and English.
/// The choice is saved by <see cref="GameLanguage"/>.
/// Toggle: on = English, off = Thai. Button: each click switches language.
/// The label is optional: when set, it shows the current language.
/// </summary>
[DisallowMultipleComponent]
public sealed class LanguageToggleButton : MonoBehaviour
{
    private const float FlagWidth = 46f;
    private const float GapAfterFlag = 10f;
    private const float HorizontalPadding = 18f;

    [Tooltip("Optional. Shows \"ภาษา: ไทย\" or \"Language: English\". Leave empty if the label is set some other way.")]
    [SerializeField] private TMP_Text label;
    [Tooltip("Optional flag image showing the currently selected language.")]
    [SerializeField] private Image flagImage;
    [SerializeField] private Sprite thaiFlag;
    [SerializeField] private Sprite englishFlag;

    private Toggle toggle;

    private void Awake()
    {
        if (TryGetComponent(out Button button))
        {
            button.onClick.AddListener(GameLanguage.Toggle);
        }
        else if (TryGetComponent(out toggle))
        {
            toggle.onValueChanged.AddListener(on => GameLanguage.Set(on ? GameLanguage.Id.English : GameLanguage.Id.Thai));
        }
        else
        {
            Debug.LogWarning("LanguageToggleButton needs a Button or a Toggle on the same object.", this);
        }
    }

    private void OnEnable()
    {
        GameLanguage.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => GameLanguage.Changed -= Refresh;

    private void Refresh()
    {
        if (toggle != null) toggle.SetIsOnWithoutNotify(GameLanguage.IsEnglish);
        if (label != null) label.text = GameLanguage.IsEnglish ? "Language: English" : "ภาษา: ไทย";
        if (flagImage != null)
        {
            flagImage.sprite = GameLanguage.IsEnglish ? englishFlag : thaiFlag;
            flagImage.enabled = flagImage.sprite != null;
        }

        FitButtonToContent();
    }

    private void FitButtonToContent()
    {
        if (label == null) return;

        RectTransform labelRect = label.rectTransform;
        float textWidth = label.GetPreferredValues(label.text).x;
        float contentStart = flagImage != null ? FlagWidth + GapAfterFlag : 0f;
        float buttonWidth = contentStart + textWidth + HorizontalPadding * 2f;

        // Keep the right edge anchored while the button grows or shrinks with the selected language.
        RectTransform buttonRect = (RectTransform)transform;
        buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, buttonWidth);

        // Lay out the label after the flag and use its measured width to avoid excess blank space.
        labelRect.anchorMin = new Vector2(0f, labelRect.anchorMin.y);
        labelRect.anchorMax = new Vector2(0f, labelRect.anchorMax.y);
        labelRect.pivot = new Vector2(0f, labelRect.pivot.y);
        labelRect.anchoredPosition = new Vector2(HorizontalPadding + contentStart, labelRect.anchoredPosition.y);
        labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);

        if (flagImage != null)
        {
            RectTransform flagRect = flagImage.rectTransform;
            flagRect.anchoredPosition = new Vector2(HorizontalPadding + FlagWidth * 0.5f, flagRect.anchoredPosition.y);
        }
    }
}
