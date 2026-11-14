using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.Menus
{
    public partial class MenuScreen
    {
        private QuestPaperLevelCard[] boardPapers;
        private QuestPaperLevelCard[] collectionPapers;

        private void SetupQuestPapers()
        {
            if (Catalog == null || Catalog.questPaperPrefab == null) return;
            // Existing buttons become layout slots editable in the scene Hierarchy.
            // Only the visual/button inside each slot is replaced by the shared prefab.
            boardPapers = ReplaceWithQuestPapers(levelButtons);
            collectionPapers = ReplaceWithQuestPapers(paperLevelButtons);
        }

        private QuestPaperLevelCard[] ReplaceWithQuestPapers(Button[] slots)
        {
            var papers = new QuestPaperLevelCard[slots == null ? 0 : slots.Length];
            for (int i = 0; i < papers.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;
                EnsureTextCanRender(slot.transform);
                // Disable the old visuals before adding the new paper beneath this slot.
                foreach (var graphic in slot.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
                var oldAnimator = slot.GetComponent<Animator>();
                if (oldAnimator != null) oldAnimator.enabled = false;
                slot.enabled = false;
                slot.gameObject.SetActive(true);

                var paper = Instantiate(Catalog.questPaperPrefab, slot.transform);
                paper.name = "QuestPaper";
                var card = paper.gameObject.AddComponent<QuestPaperLevelCard>();
                card.Initialize(paper);
                papers[i] = card;
                slots[i] = card.Button;
            }
            return papers;
        }

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

        private void RefreshBoardPaper(int slot, int id)
        {
            if (boardPapers == null || slot >= boardPapers.Length || boardPapers[slot] == null || id == 0) return;
            boardPapers[slot].BindLevel(Catalog.FindLevel(id), GameSceneManager.IsUnlocked(id));
        }

        private void RefreshCollectionPaper(int slot, int entry, bool completed)
        {
            if (collectionPapers == null || slot >= collectionPapers.Length || collectionPapers[slot] == null) return;
            if (completed) collectionPapers[slot].BindLevel(Catalog.FindLevel(entry), GameSceneManager.IsUnlocked(entry));
            else collectionPapers[slot].BindSecret(Catalog.SecretLevels[entry]);
        }
    }
}
