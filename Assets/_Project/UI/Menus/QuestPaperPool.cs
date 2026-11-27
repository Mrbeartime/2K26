using System.Collections.Generic;
using UnityEngine;

namespace ProjectD.Menus
{
    /// <summary>
    /// Creates and reuses QuestPaper prefab instances (SceneCatalog.questPaperPrefab).
    /// It only spawns papers; MenuScreen decides which level goes on which paper.
    /// </summary>
    public sealed class QuestPaperPool
    {
        private readonly List<QuestPaperLevelCard> papers = new List<QuestPaperLevelCard>();

        /// <summary>Paper number 'index' inside 'area'. Created on first use; null if there is no area or prefab.</summary>
        public QuestPaperLevelCard Get(RectTransform area, int index)
        {
            if (area == null) return null;
            if (index < papers.Count) return papers[index];

            var prefab = GameSceneManager.Catalog?.questPaperPrefab;
            if (prefab == null) return null;

            var instance = Object.Instantiate(prefab, area);
            if (!instance.TryGetComponent<QuestPaperLevelCard>(out var card))
                card = instance.gameObject.AddComponent<QuestPaperLevelCard>();
            papers.Add(card);
            return card;
        }

        /// <summary>Hides every paper from 'visibleCount' onward (they stay pooled for reuse).</summary>
        public void HideFrom(int visibleCount)
        {
            for (int i = visibleCount; i < papers.Count; i++)
                papers[i].gameObject.SetActive(false);
        }
    }
}
