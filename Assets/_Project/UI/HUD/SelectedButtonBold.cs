using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectD.Menus
{
    /// <summary>
    /// Selected-state feedback for a button (keyboard / controller selection):
    /// optionally bolds its TMP label and/or smoothly scales it up, then back to its original size.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectedButtonBold : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public bool bold = true;
        public float selectedScale = 1f;   // e.g. 1.08 = a little bigger while selected
        private const float ScaleSpeed = 14f;

        private TMP_Text label;
        private FontStyles baseStyle;
        private Vector3 baseScale;
        private bool selected;
        private bool scaling;

        private void Awake()
        {
            label = GetComponentInChildren<TMP_Text>(true);
            if (label != null) baseStyle = label.fontStyle;
        }

        public void OnSelect(BaseEventData data)
        {
            if (selected) return;
            selected = true;
            if (bold && label != null) label.fontStyle = baseStyle | FontStyles.Bold;
            if (selectedScale != 1f)
            {
                baseScale = transform.localScale;   // the size it has while not selected
                scaling = true;
            }
        }

        public void OnDeselect(BaseEventData data)
        {
            selected = false;
            if (bold && label != null) label.fontStyle = baseStyle;
            if (selectedScale != 1f) scaling = true;   // animate back to the original size
        }

        private void Update()
        {
            if (!scaling) return;
            Vector3 target = selected ? baseScale * selectedScale : baseScale;
            transform.localScale = Vector3.Lerp(transform.localScale, target, 1f - Mathf.Exp(-ScaleSpeed * Time.unscaledDeltaTime));
            if ((transform.localScale - target).sqrMagnitude < 1e-8f)
            {
                transform.localScale = target;
                scaling = false;
            }
        }

        private void OnDisable()
        {
            if (selectedScale != 1f && (selected || scaling)) transform.localScale = baseScale;
            selected = false;
            scaling = false;
            if (bold && label != null) label.fontStyle = baseStyle;
        }
    }
}
