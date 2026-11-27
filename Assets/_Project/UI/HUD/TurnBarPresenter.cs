using System.Collections;
using System.Collections.Generic;
using ProjectD.Gameplay.Turns;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.UI
{
    internal sealed class TurnBarPresenter
    {
        private readonly MonoBehaviour owner;
        private readonly RectTransform turnProgressTrack;
        private readonly Image turnBarPrefab;
        private readonly float turnBarGap;
        private readonly Color turnBarEmptyColor;
        private readonly float turnProgressDuration;
        private readonly List<TurnBarSlot> turnBarSlots = new();
        private int pendingTurnBarRound;

        internal TurnBarPresenter(MonoBehaviour owner, RectTransform track, Image prefab,
            float gap, Color emptyColor, float duration)
        {
            this.owner = owner;
            turnProgressTrack = track;
            turnBarPrefab = prefab;
            turnBarGap = gap;
            turnBarEmptyColor = emptyColor;
            turnProgressDuration = duration;
        }

        private sealed class TurnBarSlot
        {
            public RectTransform Background;
            public RectTransform Fill;
        }

        internal void Refresh(IReadOnlyTurnState gameManager, int turnsLeft, int maximumTurns)
        {
            if (turnProgressTrack == null || turnBarPrefab == null) return;

            int completedRounds = Mathf.Clamp(maximumTurns - turnsLeft, 0, maximumTurns);
            if (pendingTurnBarRound <= completedRounds || gameManager.IsGameComplete)
                pendingTurnBarRound = 0;

            EnsureTurnBarSlots(maximumTurns);
            for (int round = 1; round <= turnBarSlots.Count; round++)
            {
                TurnBarSlot slot = turnBarSlots[round - 1];
                if (round <= completedRounds)
                {
                    SetTurnBarProgress(slot, round, 1f);
                }
                else if (round != pendingTurnBarRound && slot.Fill != null)
                {
                    RemoveGraphic(slot.Fill);
                    slot.Fill = null;
                }
            }
        }

        internal IEnumerator Animate(IReadOnlyTurnState gameManager)
        {
            if (owner == null || !owner.isActiveAndEnabled || turnProgressTrack == null || turnBarPrefab == null) yield break;

            int round = gameManager.RoundNumber;
            if (round < 1 || round > turnBarSlots.Count) yield break;

            pendingTurnBarRound = round;
            TurnBarSlot slot = turnBarSlots[round - 1];
            RectTransform fill = SetTurnBarProgress(slot, round, 0f);
            float duration = turnProgressDuration;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                if (owner == null || !owner.isActiveAndEnabled || fill == null || gameManager.IsGameComplete)
                    yield break;

                elapsed += Time.deltaTime;
                SetTurnBarFill(fill, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
            }
            SetTurnBarFill(fill, 1f);
        }

        private void EnsureTurnBarSlots(int maximumTurns)
        {
            if (turnBarSlots.Count == maximumTurns) return;

            ClearTurnBarSlots();
            float inset = turnBarGap * 0.5f;
            for (int i = 0; i < maximumTurns; i++)
            {
                Image background = Object.Instantiate(turnBarPrefab, turnProgressTrack, false);
                background.name = $"Turn {i + 1} Slot";
                background.color = turnBarEmptyColor;
                RectTransform rect = background.rectTransform;
                rect.SetSiblingIndex(i);
                rect.anchorMin = new Vector2((i + inset) / maximumTurns, 0f);
                rect.anchorMax = new Vector2((i + 1f - inset) / maximumTurns, 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                turnBarSlots.Add(new TurnBarSlot { Background = rect });
            }
        }

        private RectTransform SetTurnBarProgress(TurnBarSlot slot, int round, float progress)
        {
            if (slot.Fill == null)
            {
                Image image = Object.Instantiate(turnBarPrefab, slot.Background, false);
                image.name = $"Bar (Turn {round})";
                slot.Fill = image.rectTransform;
                slot.Fill.anchorMin = Vector2.zero;
                slot.Fill.offsetMin = slot.Fill.offsetMax = Vector2.zero;
            }
            SetTurnBarFill(slot.Fill, progress);
            return slot.Fill;
        }

        private static void SetTurnBarFill(RectTransform fill, float progress)
        {
            fill.anchorMax = new Vector2(progress, 1f);
        }

        internal void ClearTurnBarSlots()
        {
            foreach (TurnBarSlot slot in turnBarSlots)
                RemoveGraphic(slot.Background);
            turnBarSlots.Clear();
        }

        private static void RemoveGraphic(Transform graphic)
        {
            if (graphic == null) return;
            graphic.gameObject.SetActive(false);
            Object.Destroy(graphic.gameObject);
        }
    }
}
