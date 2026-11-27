using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[RequireComponent(typeof(Animator))]
public sealed class NewCharacterIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image portrait;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image focusGlow;

    private static readonly int SelectedParameter = Animator.StringToHash("selected");
    private Animator selectionAnimator;
    private UnityAction pointerEnter;
    private UnityAction pointerExit;

    private void Awake()
    {
        selectionAnimator = GetComponent<Animator>();
    }

    internal void SetSelected(bool selected)
    {
        selectionAnimator.SetBool(SelectedParameter, selected);
    }

    internal void SetFocused(bool focused)
    {
        if (focusGlow != null) focusGlow.enabled = focused;
    }

    internal void SetPortrait(Sprite sprite)
    {
        portrait.overrideSprite = sprite;
    }

    internal void SetLabel(string text)
    {
        if (label != null) label.text = text;
    }

    internal void BindClick(UnityAction callback)
    {
        if (!TryGetComponent(out Button button)) button = gameObject.AddComponent<Button>();
        button.targetGraphic = GetComponent<Image>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(callback);
    }

    internal void BindHover(UnityAction onEnter, UnityAction onExit)
    {
        pointerEnter = onEnter;
        pointerExit = onExit;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsMouse(eventData)) pointerEnter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (IsMouse(eventData)) pointerExit?.Invoke();
    }

    private static bool IsMouse(PointerEventData eventData) =>
        eventData is ExtendedPointerEventData input ? input.device is Mouse : eventData.pointerId == -1;
}
