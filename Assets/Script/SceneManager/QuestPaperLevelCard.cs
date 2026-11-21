using System;
using ProjectD.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.Menus
{
    /// <summary>Connects a QuestPaper prefab instance to one catalog entry.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button), typeof(QuestPaperDecoration), typeof(CanvasGroup))]
    public class QuestPaperLevelCard : MonoBehaviour
    {
        private QuestPaperDecoration decoration;
        private TMP_Text title;
        private TMP_FontAsset inkTitleFont;
        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 originalSize;
        private Quaternion originalRotation;
        private Button button;
        private Action onSelected;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (button != null) return;
            decoration = GetComponent<QuestPaperDecoration>();
            button = GetComponent<Button>();
            rect = (RectTransform)transform;
            originalSize = rect.sizeDelta;
            originalRotation = rect.localRotation;
            title = decoration.Title;
            if (title != null) inkTitleFont = title.font;
            group = GetComponent<CanvasGroup>();
            button.onClick.AddListener(Select);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Select);
        }

        private void Select()
        {
            if (button.IsActive() && button.IsInteractable()) onSelected?.Invoke();
        }

        public void BindLevel(LevelDefinition level, bool unlocked, bool completed, Action select)
        {
            Bind(level.Title, level.description, level.paperSprite, unlocked, completed, select);
            ButtonAudioFeedback.BindLevelHover(button);
        }

        public void BindSecret(SecretLevelDefinition level, Action select)
        {
            Bind(level.title, level.description, level.paperSprite, true, LevelProgressStore.IsSecretCompleted(level.id), select);
            ButtonAudioFeedback.BindLevelHover(button, false);
        }

        private void Bind(string label, string description, Sprite paperSprite, bool unlocked, bool completed, Action select)
        {
            Initialize();
            onSelected = select;
            gameObject.name = "QuestPaper - " + label;
            // Retain the prefab's ink decoration, paper styles and hover animation.
            decoration.ApplyDecoration(label?.Length ?? 0, description?.Length ?? 0, paperSprite);
            // Unfinished quests keep the prefab's decorative ink. Reveal names after completion.
            if (title != null)
            {
                var readableFont = GameSceneManager.Catalog?.questTitleFont;
                title.font = completed && readableFont != null ? readableFont : inkTitleFont;
                if (completed) title.text = label ?? string.Empty;
            }
            button.interactable = unlocked;
            group.interactable = unlocked;
            group.alpha = unlocked ? 1f : 0.45f;
            gameObject.SetActive(true);
        }

        /// <summary>Plays the Animator's 'PopIn' state. Call after Bind, when the paper is active.</summary>
        internal void PlayPopIn()
        {
            if (!TryGetComponent(out Animator animator) || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) return;
            animator.Update(0f);   // use up the Normal trigger the Button sends when the paper is enabled
            animator.Play("PopIn", 0, 0f);
        }

        internal void PlaceIn(RectTransform area, int slot, int capacity)
        {
            Initialize();
            if (originalSize.x <= 0 || originalSize.y <= 0) return;
            float width = area.rect.width / capacity * 0.86f;
            float scale = Mathf.Min(width / originalSize.x, area.rect.height / originalSize.y);
            Place(new Vector2((slot + 0.5f) / capacity, 0.5f), Mathf.Max(0.001f, scale), originalRotation);
        }

        internal void PlaceOnBoard(RectTransform area, QuestPaperPlacement placement, int capacity)
        {
            Initialize();
            int columns = QuestPaperPlacement.ColumnsFor(capacity);
            float slotWidth = area.rect.width / columns;
            float areaHeight = area.rect.height;
            if (originalSize.x <= 0f || originalSize.y <= 0f || slotWidth <= 0f || areaHeight <= 0f) return;

            // The hover animation scales the paper artwork by about 6%.
            const float hoverScale = 1.06f;
            float radians = placement.rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Abs(Mathf.Sin(radians));
            float rotatedWidth = (originalSize.x * cos + originalSize.y * sin) * hoverScale;
            float rotatedHeight = (originalSize.x * sin + originalSize.y * cos) * hoverScale;
            float scale = Mathf.Min(slotWidth * 0.82f / rotatedWidth, areaHeight * 0.50f / rotatedHeight);
            float halfWidth = rotatedWidth * scale / (2f * slotWidth);
            float halfHeight = rotatedHeight * scale / (2f * areaHeight);

            bool centerColumn = placement.slot == columns / 2;
            float horizontalSample = centerColumn ? Mathf.Lerp(0.43f, 0.57f, placement.x) : placement.x;
            float xInSlot = Mathf.Lerp(0.04f + halfWidth, 0.96f - halfWidth, horizontalSample);

            var canvas = area.GetComponentInParent<Canvas>(true)?.rootCanvas;
            var canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            float screenCenterY = canvasRect != null
                ? area.InverseTransformPoint(canvasRect.TransformPoint(canvasRect.rect.center)).y
                : area.rect.center.y;
            float centerYInArea = (screenCenterY - area.rect.yMin) / areaHeight;
            float verticalVariation = centerColumn ? 0.06f : 0.20f;
            // Keep each rotated footprint in its own column and clear of the controls below.
            float yInArea = Mathf.Clamp(centerYInArea + (placement.y - 0.5f) * verticalVariation,
                0.25f + halfHeight, 0.96f - halfHeight);
            Place(new Vector2((placement.slot + xInSlot) / columns, yInArea), scale, Quaternion.Euler(0f, 0f, placement.rotation));
        }

        private void Place(Vector2 anchor, float scale, Quaternion rotation)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = originalSize;
            rect.localScale = Vector3.one * scale;
            rect.localRotation = rotation;
        }
    }
}
