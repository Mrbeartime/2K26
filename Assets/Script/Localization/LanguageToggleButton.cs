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
    [Tooltip("Optional. Shows \"ภาษา: ไทย\" or \"Language: English\". Leave empty if the label is set some other way.")]
    [SerializeField] private TMP_Text label;

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
    }
}
