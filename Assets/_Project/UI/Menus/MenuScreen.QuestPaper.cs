using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.Menus
{
    internal static class QuestDepthText
    {
        internal static string Resolve(string saved, int depth, TextAsset thaiSource)
        {
            if (string.IsNullOrWhiteSpace(saved)) return null;

            var thaiOptions = ReadOptions(thaiSource, depth);
            int index = saved.StartsWith("#") && int.TryParse(saved.Substring(1), out int number)
                ? number
                : thaiOptions.IndexOf(saved);
            if (index < 0) return saved.StartsWith("#") ? null : saved;

            var options = ReadOptions(GameLanguage.LoadVariant(thaiSource), depth);
            if (index < options.Count) return options[index];
            return index < thaiOptions.Count ? thaiOptions[index] : null;
        }

        internal static List<string> ReadOptions(TextAsset source, int depth)
        {
            var options = new List<string>();
            if (source == null) return options;

            int targetDepth = Mathf.Clamp(depth, 0, 10);
            foreach (var line in source.text.Split('\n'))
            {
                int separator = line.IndexOf('|');
                if (separator <= 0 || !int.TryParse(line.Substring(0, separator), out int optionDepth) || optionDepth != targetDepth)
                    continue;

                string text = line.Substring(separator + 1).Trim();
                if (text.Length > 0) options.Add(text);
            }
            return options;
        }
    }

    public partial class MenuScreen
    {
        private readonly QuestPaperPool boardPapers = new QuestPaperPool();
        private readonly QuestPaperPool collectionPapers = new QuestPaperPool();
        private readonly List<(Image Outline, Image Portrait)> detailCharacterIcons = new();
        private static readonly System.Random depthDescriptionRandom = new System.Random();

        private static void EnsureTextCanRender(Transform slot)
        {
            // Some existing UI containers have Z scale 0. Images tolerate this,
            // but TMP's signed-distance-field shader needs a nonzero normal.
            // Preserve the authored X/Y size and position of every slot.
            for (var parent = slot; parent is RectTransform; parent = parent.parent)
            {
                var scale = parent.localScale;
                if (Mathf.Approximately(scale.z, 0f))
                {
                    scale.z = 1f;
                    parent.localScale = scale;
                }
            }
        }

        private void ShowDetails(string title, string description, Sprite[] characterIcons, int depth, string questId)
        {
            paperView = PaperView.Details;
            SetPaperVisible();
            ShowPaperList(false);
            if (paperTitle != null) paperTitle.text = title;
            if (paperDescription != null) paperDescription.text = description;
            if (paperMetadata != null) paperMetadata.SetActive(true);
            if (paperDepth != null)
            {
                string saved = LevelProgressStore.GetOrCreateQuestDepthDescription(questId, () => PickDepthDescription(depth));
                paperDepth.text = (saved == null ? null : QuestDepthText.Resolve(saved, depth, Catalog?.questDepthDescriptions))
                    ?? GameLanguage.Pick("ยังไม่มีรายงานความยากของภารกิจนี้", "No difficulty report for this quest yet.");
            }
            RefreshDetailCharacterIcons(characterIcons);
            if (collectionScroll != null) collectionScroll.gameObject.SetActive(false);
            if (playButton != null) playButton.gameObject.SetActive(true);
        }

        // The saved value is "#<number>": the position of the chosen line among the lines of that depth. The Thai file
        // (QuestDepthDescriptions.txt) defines the numbers; QuestDepthDescriptions_en.txt must keep the same line order.
        // Older saves hold the Thai text itself, which is matched back to its number.
        private string PickDepthDescription(int depth)
        {
            var options = QuestDepthText.ReadOptions(Catalog?.questDepthDescriptions, depth);
            return options.Count == 0 ? null : "#" + depthDescriptionRandom.Next(options.Count);
        }

        private void RefreshDetailCharacterIcons(Sprite[] sprites)
        {
            if (paperCharacterIcons == null) return;

            int count = 0;
            foreach (var sprite in sprites ?? Array.Empty<Sprite>())
            {
                if (sprite == null) continue;

                if (count == detailCharacterIcons.Count)
                {
                    var icon = new GameObject("Character Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    icon.layer = paperCharacterIcons.gameObject.layer;
                    icon.transform.SetParent(paperCharacterIcons, false);
                    var outline = icon.GetComponent<Image>();
                    outline.preserveAspect = true;
                    outline.raycastTarget = false;
                    var outlineMaterial = Catalog?.questCharacterIconMaterial;
                    outline.material = outlineMaterial;
                    outline.enabled = outlineMaterial != null;
                    icon.GetComponent<LayoutElement>().preferredWidth = 100f;
                    icon.AddComponent<SpriteOutlinePadding>();

                    var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    portrait.gameObject.layer = icon.layer;
                    var rect = portrait.rectTransform;
                    rect.SetParent(icon.transform, false);
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    portrait.preserveAspect = true;
                    portrait.raycastTarget = false;
                    detailCharacterIcons.Add((outline, portrait));
                }

                var (iconOutline, iconPortrait) = detailCharacterIcons[count++];
                iconOutline.sprite = iconPortrait.sprite = sprite;
                iconOutline.gameObject.SetActive(true);
            }

            for (int i = count; i < detailCharacterIcons.Count; i++)
                detailCharacterIcons[i].Outline.gameObject.SetActive(false);
        }
    }
}
