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

    [SerializeField, Min(0f)] private float rowSpacing = 70f;

    private (RectTransform Row, float Bottom, float Right)[] rows;
    private (bool CameraFree, bool CanSelect, bool ActionOrEndTurnFocused)? displayedState;
    private Sprite resetFillSprite;
    internal Image ResetFill { get; private set; }

    private void Awake()
    {
        // List the tips from top to bottom, in the same order as the visibility flags.
        RectTransform[] prefabs =
        {
            navigateSelectionTip, chooseActionTip, returnTip, confirmSelectionTip,
            showPositionsTip, resetTip, pauseTip, toggleTipsTip
        };

        rows = new (RectTransform, float, float)[prefabs.Length];
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null) continue;
            RectTransform row = Instantiate(prefabs[i], transform, false);
            row.name = prefabs[i].name;
            row.anchorMin = row.anchorMax = new Vector2(1f, 0f);

            // Use the authored graphics so the margins exclude the empty part of the prefab root.
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (Graphic graphic in row.GetComponentsInChildren<Graphic>())
                bounds.Encapsulate(RectTransformUtility.CalculateRelativeRectTransformBounds(row, graphic.rectTransform));
            TMP_Text caption = row.Find("Name")?.GetComponent<TMP_Text>();
            float right = GetCaptionRightEdge(row, caption, bounds.max.x);
            // Convert row-local measurements to anchoredPosition units.
            Vector3 scale = row.localScale;
            rows[i] = (row, bounds.min.y * scale.y, right * scale.x);
            if (prefabs[i] == resetTip) ResetFill = CreateResetFill(row);
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

    private static float GetCaptionRightEdge(RectTransform row, TMP_Text caption, float fallback)
    {
        if (caption == null) return fallback;

        caption.ForceMeshUpdate();
        TMP_TextInfo text = caption.textInfo;
        if (text.lineCount == 0 || text.characterCount == 0) return fallback;

        TMP_LineInfo lastLine = text.lineInfo[text.lineCount - 1];
        TMP_CharacterInfo lastCharacter = text.characterInfo[lastLine.lastVisibleCharacterIndex];
        if (!lastCharacter.isVisible) return fallback;

        RectTransform rect = caption.rectTransform;
        // Trim the empty space before the caption so the key stays next to the text.
        rect.anchoredPosition -= new Vector2(caption.textBounds.min.x - rect.rect.xMin, 0f);

        var glyph = lastCharacter.textElement.glyph.metrics;
        float right = lastCharacter.origin + (glyph.horizontalBearingX + glyph.width) * lastCharacter.scale;
        return row.InverseTransformPoint(rect.TransformPoint(new Vector3(right, 0f))).x;
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
        NewInGameUIManager hud = NewInGameUIManager.Instance;
        bool cameraFree = CameraFocusManager.Instance?.CanAcceptManualInput == true;
        bool canSelect = hud != null && hud.CanNavigateSelection;
        bool actionOrEndTurnFocused = canSelect && (hud.IsChoosingAction || hud.IsEndTurnFocused);
        var state = (cameraFree, canSelect, actionOrEndTurnFocused);
        if (displayedState == state) return;
        displayedState = state;

        bool[] visible =
        {
            canSelect, canSelect && !actionOrEndTurnFocused, actionOrEndTurnFocused, actionOrEndTurnFocused,
            cameraFree, true, true, true
        };

        float y = 0f;
        bool firstVisibleRow = true;
        for (int i = rows.Length - 1; i >= 0; i--)
        {
            (RectTransform row, float bottom, float right) = rows[i];
            if (row == null) continue;
            if (visible[i])
            {
                if (firstVisibleRow)
                {
                    y = -bottom;
                    firstVisibleRow = false;
                }
                row.anchoredPosition = new Vector2(-right, y);
                y += rowSpacing;
            }
            row.gameObject.SetActive(visible[i]);
        }
    }
}
