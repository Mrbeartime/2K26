using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestPaperDecoration : MonoBehaviour
{
    private const int MaxTitleCharacters = 18;
    private const int MaxCharactersPerLine = 24;
    private const string InkLetters = "abcdefghijklmnopqrstuvwxyz";
    private static readonly System.Random InkRandom = new();

    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text description;
    [SerializeField] private Image paperImage;
    [SerializeField, Tooltip("The first entry with a Sprite is the default paper.")]
    private List<PaperStyle> paperStyles = new();

    /// <summary>
    /// Apply the default paper and draw ink using the supplied character counts.
    /// </summary>
    public void ApplyDecoration(int titleLength, int descriptionLength)
    {
        ApplyDecoration(titleLength, descriptionLength, null);
    }

    /// <summary>
    /// Apply the paper Sprite selected by the quest data and draw its ink.
    /// A missing or unlisted Sprite uses the default paper.
    /// </summary>
    public void ApplyDecoration(int titleLength, int descriptionLength, Sprite paperSprite)
    {
        ApplyPaperStyle(paperSprite);

        int lineCount = descriptionLength switch
        {
            <= 0 => 0,
            <= 20 => 1,
            <= 50 => 2,
            <= 80 => 3,
            <= 120 => 4,
            _ => 5
        };

        if (title != null)
        {
            var ink = new StringBuilder(MaxTitleCharacters);
            AppendInkLine(ink, Mathf.Clamp(titleLength, 0, MaxTitleCharacters));
            title.text = ink.ToString();
        }

        if (description != null)
        {
            int remaining = Mathf.Clamp(descriptionLength, 0, lineCount * MaxCharactersPerLine);
            var ink = new StringBuilder(remaining + lineCount);
            for (int linesLeft = lineCount; linesLeft > 0; linesLeft--)
            {
                if (linesLeft < lineCount)
                    ink.Append('\n');

                int lineLength = (remaining + linesLeft - 1) / linesLeft;
                AppendInkLine(ink, lineLength);
                remaining -= lineLength;
            }

            description.text = ink.ToString();
        }
    }

    private void ApplyPaperStyle(Sprite paperSprite)
    {
        PaperStyle selected = null;
        foreach (PaperStyle style in paperStyles)
        {
            if (style?.sprite == null)
                continue;

            selected ??= style;
            if (style.sprite == paperSprite)
            {
                selected = style;
                break;
            }
        }

        if (selected == null)
            return;

        if (paperImage != null)
            paperImage.sprite = selected.sprite;

        ApplyTextLayout(title, selected.titleArea, selected.minFontSize, selected.titleMaxFontSize);
        ApplyTextLayout(description, selected.descriptionArea, selected.minFontSize, selected.descriptionMaxFontSize);
    }

    private static void ApplyTextLayout(TMP_Text text, Rect area, float minFontSize, float maxFontSize)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = area.min;
        rect.anchorMax = area.max;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        text.enableAutoSizing = true;
        text.fontSizeMax = Mathf.Max(1, maxFontSize);
        text.fontSizeMin = Mathf.Clamp(minFontSize, 1, text.fontSizeMax);
    }

    private static void AppendInkLine(StringBuilder ink, int remaining)
    {
        while (remaining > 0)
        {
            int wordLength = Mathf.Min(remaining, InkRandom.Next(3, 8));
            for (int i = 0; i < wordLength; i++)
                ink.Append(InkLetters[InkRandom.Next(InkLetters.Length)]);

            remaining -= wordLength;
            if (remaining > 2)
            {
                ink.Append(' ');
                remaining--;
            }
        }
    }

    [System.Serializable]
    private sealed class PaperStyle
    {
        public Sprite sprite;
        [Tooltip("Title area in normalized paper coordinates, from the bottom left.")]
        public Rect titleArea = new(0.12f, 0.72f, 0.76f, 0.12f);
        [Tooltip("Description area in normalized paper coordinates, from the bottom left.")]
        public Rect descriptionArea = new(0.12f, 0.25f, 0.76f, 0.44f);
        [Min(1)] public float minFontSize = 18;
        [Min(1)] public float titleMaxFontSize = 140;
        [Min(1)] public float descriptionMaxFontSize = 88;
    }
}
