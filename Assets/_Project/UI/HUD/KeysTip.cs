using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeysTip : MonoBehaviour
{
    [SerializeField] private RectTransform resetTip;
    [SerializeField] private RectTransform pauseTip;
    [SerializeField] private RectTransform toggleTipsTip;
    [SerializeField] private RectTransform showPositionsTip;
    [SerializeField] private RectTransform chooseActionTip;
    [SerializeField] private RectTransform returnTip;
    [SerializeField] private RectTransform navigateSelectionTip;
    [SerializeField] private RectTransform confirmSelectionTip;
    [SerializeField] private RectTransform cameraPanRotateTip;
    [Tooltip("Extra tips that are always visible, listed top to bottom below the built-in tips. Used by scenes without the in-game HUD, such as the Guide.")]
    [SerializeField] private RectTransform[] alwaysTips;

    [SerializeField, Min(0f)] private float rowSpacing = 70f;

    private enum Visibility { Always, Camera, CameraPanRotate, Character, ActionOrEndTurn, Selection, Confirm }

    private sealed class TipRow
    {
        public RectTransform Row;
        public TMP_Text Caption;
        public Vector2 CaptionPosition;
        public string MeasuredText;
        public TMP_FontAsset MeasuredFont;
        public float MeasuredFontSize;
        public float Bottom;
        public float Right;
        public Visibility Visibility;
    }

    private TipRow[] rows;
    private (bool CameraFree, bool CanPanRotate, bool CanSelect, bool ActionOrEndTurnFocused, bool CanConfirm)? displayedState;
    private Sprite resetFillSprite;
    internal Image ResetFill { get; private set; }

    private void Awake()
    {
        // List the tips from top to bottom.
        var tips = new System.Collections.Generic.List<(RectTransform Prefab, Visibility Visibility)>
        {
            (cameraPanRotateTip, Visibility.CameraPanRotate),
            (navigateSelectionTip, Visibility.Selection),
            (chooseActionTip, Visibility.Character),
            (returnTip, Visibility.ActionOrEndTurn),
            (confirmSelectionTip, Visibility.Confirm),
            (showPositionsTip, Visibility.Camera),
            (resetTip, Visibility.Always),
            (pauseTip, Visibility.Always),
            (toggleTipsTip, Visibility.Always)
        };
        if (alwaysTips != null)
            foreach (RectTransform tip in alwaysTips)
                tips.Add((tip, Visibility.Always));

        rows = new TipRow[tips.Count];
        for (int i = 0; i < tips.Count; i++)
        {
            (RectTransform prefab, Visibility visibility) = tips[i];
            if (prefab == null) continue;
            RectTransform row = Instantiate(prefab, transform, false);
            row.name = prefab.name;
            row.anchorMin = row.anchorMax = new Vector2(1f, 0f);

            // Use the authored graphics so the margins exclude the empty part of the prefab root.
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (Graphic graphic in row.GetComponentsInChildren<Graphic>())
                bounds.Encapsulate(RectTransformUtility.CalculateRelativeRectTransformBounds(row, graphic.rectTransform));
            TMP_Text caption = row.Find("Name")?.GetComponent<TMP_Text>();
            // Convert row-local measurements to anchoredPosition units.
            Vector3 scale = row.localScale;
            rows[i] = new TipRow
            {
                Row = row,
                Caption = caption,
                CaptionPosition = caption != null ? caption.rectTransform.anchoredPosition : Vector2.zero,
                Bottom = bounds.min.y * scale.y,
                Right = bounds.max.x * scale.x,
                Visibility = visibility
            };
            FitCaption(rows[i]);
            if (prefab == resetTip) ResetFill = CreateResetFill(row);
            row.gameObject.SetActive(false);
        }
    }

    private Image CreateResetFill(RectTransform row)
    {
        Transform key = row.Find("Key");
        Image fill = new GameObject("Hold Fill", typeof(RectTransform)) { layer = row.gameObject.layer }.AddComponent<Image>();
        RectTransform rect = fill.rectTransform;
        rect.SetParent(key, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Texture2D white = Texture2D.whiteTexture;
        resetFillSprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f));
        fill.sprite = resetFillSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillAmount = 0f;
        fill.raycastTarget = false;
        return fill;
    }

    private static void FitCaption(TipRow tip)
    {
        TMP_Text caption = tip.Caption;
        if (caption == null) return;

        RectTransform rect = caption.rectTransform;
        // Always start from the authored gap, avoiding drift on repeated language changes.
        rect.anchoredPosition = tip.CaptionPosition;
        caption.enableAutoSizing = false;
        caption.textWrappingMode = TextWrappingModes.NoWrap;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            Mathf.Max(1f, caption.GetPreferredValues(caption.text, Mathf.Infinity, Mathf.Infinity).x) + 2f);
        caption.ForceMeshUpdate(ignoreActiveState: true);
        tip.MeasuredText = caption.text;
        tip.MeasuredFont = caption.font;
        tip.MeasuredFontSize = caption.fontSize;
        if (caption.textInfo.characterCount == 0) return;

        // Keep the first visible glyph next to the key and align the visible right edge.
        rect.anchoredPosition -= new Vector2(caption.textBounds.min.x - rect.rect.xMin, 0f);
        float right = tip.Row.InverseTransformPoint(
            rect.TransformPoint(new Vector3(caption.textBounds.max.x, 0f))).x;
        tip.Right = right * tip.Row.localScale.x;
    }

    private void OnEnable() => Refresh();

    private void OnDestroy() => Destroy(resetFillSprite);

    private void LateUpdate() => Refresh();

    public void ToggleVisibility()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }

    private void Refresh()
    {
        if (rows == null) return;
        // Localization can finish asynchronously, after the locale changed event.
        // Watch the actual caption so late translations and re-enabled tips also relayout.
        foreach (TipRow tip in rows)
        {
            if (tip?.Caption == null) continue;
            if (tip.MeasuredText == tip.Caption.text && tip.MeasuredFont == tip.Caption.font &&
                Mathf.Approximately(tip.MeasuredFontSize, tip.Caption.fontSize)) continue;
            FitCaption(tip);
            displayedState = null;
        }
        InGameUIManager hud = InGameUIManager.Instance;
        CameraFocusManager camera = CameraFocusManager.Instance;
        bool cameraFree = camera?.CanAcceptManualInput == true;
        bool canPanRotate = camera != null && camera.CanRotateCamera && camera.CanPanCamera;
        bool canSelect = hud != null && hud.CanNavigateSelection;
        bool actionOrEndTurnFocused = canSelect && (hud.IsChoosingAction || hud.IsEndTurnFocused);
        bool canConfirm = hud != null && hud.CanUseSpacebar;
        var state = (cameraFree, canPanRotate, canSelect, actionOrEndTurnFocused, canConfirm);
        if (displayedState == state) return;
        displayedState = state;

        float y = 0f;
        bool firstVisibleRow = true;
        for (int i = rows.Length - 1; i >= 0; i--)
        {
            TipRow tip = rows[i];
            if (tip?.Row == null) continue;
            RectTransform row = tip.Row;
            bool visible = tip.Visibility switch
            {
                Visibility.Camera => cameraFree,
                Visibility.CameraPanRotate => canPanRotate,
                Visibility.Character => canSelect && !actionOrEndTurnFocused,
                Visibility.ActionOrEndTurn => actionOrEndTurnFocused,
                Visibility.Selection => canSelect,
                Visibility.Confirm => canConfirm,
                _ => true
            };
            if (visible)
            {
                if (firstVisibleRow)
                {
                    y = -tip.Bottom;
                    firstVisibleRow = false;
                }
                row.anchoredPosition = new Vector2(-tip.Right, y);
                y += rowSpacing;
            }
            row.gameObject.SetActive(visible);
        }
    }
}
