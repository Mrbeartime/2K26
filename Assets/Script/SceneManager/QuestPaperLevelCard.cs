using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.Menus
{
    /// <summary>Connects a QuestPaper prefab instance to one catalog entry.</summary>
    [DisallowMultipleComponent]
    public class QuestPaperLevelCard : MonoBehaviour
    {
        private QuestPaperDecoration decoration;
        private TMP_Text title;
        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 originalSize;
        public Button Button { get; private set; }
        public int LevelId { get; private set; }
        public string SecretId { get; private set; }

        public void Initialize(QuestPaperDecoration paper)
        {
            decoration = paper;
            Button = GetComponent<Button>();
            rect = (RectTransform)transform;
            originalSize = rect.sizeDelta;
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                if (text.name == "Title") { title = text; break; }
            if (title != null && GameSceneManager.Catalog.questTitleFont != null)
                title.font = GameSceneManager.Catalog.questTitleFont;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            FitToSlot();
        }

        public void BindLevel(LevelDefinition level, bool unlocked)
        {
            LevelId = level.id;
            SecretId = null;
            Bind(level.Title, level.description, level.paperSprite, unlocked);
        }

        public void BindSecret(SecretLevelDefinition level)
        {
            LevelId = 0;
            SecretId = level.id;
            Bind(level.title, level.description, level.paperSprite, true);
        }

        private void Bind(string label, string description, Sprite paperSprite, bool unlocked)
        {
            gameObject.name = "QuestPaper - " + label;
            // Retain the prefab's ink decoration, paper styles and hover animation.
            decoration.ApplyDecoration(label?.Length ?? 0, description?.Length ?? 0, paperSprite);
            // A readable title identifies the level; full details open when clicked.
            if (title != null) title.text = label ?? string.Empty;
            Button.interactable = unlocked;
            group.interactable = unlocked;
            group.alpha = unlocked ? 1f : 0.45f;
            FitToSlot();
        }

        private void FitToSlot()
        {
            if (!(transform.parent is RectTransform slot) || originalSize.x <= 0 || originalSize.y <= 0) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = originalSize;
            float scale = Mathf.Min(slot.rect.width / originalSize.x, slot.rect.height / originalSize.y);
            rect.localScale = Vector3.one * Mathf.Max(0.001f, scale);
        }
    }
}
