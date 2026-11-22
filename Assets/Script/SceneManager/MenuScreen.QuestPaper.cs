using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.Menus
{
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
            if (paperTitle != null) paperTitle.text = title;
            if (paperDescription != null) paperDescription.text = description;
            if (paperMetadata != null) paperMetadata.SetActive(true);
            if (paperDepth != null)
                paperDepth.text = LevelProgressStore.GetOrCreateQuestDepthDescription(questId, () => PickDepthDescription(depth))
                    ?? "ยังไม่มีรายงานความยากของภารกิจนี้";
            RefreshDetailCharacterIcons(characterIcons);
            if (collectionScroll != null) collectionScroll.gameObject.SetActive(false);
            if (playButton != null) playButton.gameObject.SetActive(true);
        }

        private string PickDepthDescription(int depth)
        {
            var source = Catalog?.questDepthDescriptions;
            if (source == null) return null;

            int targetDepth = Mathf.Clamp(depth, 0, 10);
            var options = new List<string>();
            foreach (var line in source.text.Split('\n'))
            {
                int separator = line.IndexOf('|');
                if (separator <= 0 || !int.TryParse(line.Substring(0, separator), out int optionDepth) || optionDepth != targetDepth)
                    continue;

                string text = line.Substring(separator + 1).Trim();
                if (text.Length > 0) options.Add(text);
            }
            return options.Count == 0 ? null : options[depthDescriptionRandom.Next(options.Count)];
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
