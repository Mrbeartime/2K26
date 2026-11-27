using System.Collections.Generic;
using ProjectD.Gameplay.Turns;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.UI
{
    internal sealed class HazardTimeline
    {
        private readonly RectTransform turnProgressTrack, hazardEventPrefab;
        private readonly Sprite arrowTrapIcon, spikeTrapIcon;
        private readonly TMP_FontAsset hazardTurnFont;
        private readonly float turnBarGap;
        private readonly System.Func<bool> canAcceptInput;
        private readonly List<RectTransform> hazardEvents = new();
        private TurnScheduleData hazardSchedule;
        private IReadOnlyTurnState turnGameManager;

        internal HazardTimeline(RectTransform track, RectTransform prefab, Sprite arrow, Sprite spike,
            TMP_FontAsset font, float gap, System.Func<bool> canAcceptInput)
        {
            turnProgressTrack = track;
            hazardEventPrefab = prefab;
            arrowTrapIcon = arrow;
            spikeTrapIcon = spike;
            hazardTurnFont = font;
            turnBarGap = gap;
            this.canAcceptInput = canAcceptInput;
        }

        internal void Dispose()
        {
            if (hazardSchedule != null) hazardSchedule.ScheduleChanged -= RebuildHazardEvents;
            ClearHazardEvents();
        }

        internal void Bind(IReadOnlyTurnState state)
        {
            turnGameManager = state;
            if (hazardSchedule == state.ScheduleData) return;

            if (hazardSchedule != null) hazardSchedule.ScheduleChanged -= RebuildHazardEvents;
            hazardSchedule = turnGameManager.ScheduleData;
            if (hazardSchedule != null) hazardSchedule.ScheduleChanged += RebuildHazardEvents;
            RebuildHazardEvents();
        }

        private void RebuildHazardEvents()
        {
            ClearHazardEvents();
            if (hazardSchedule == null || hazardEventPrefab == null || turnProgressTrack == null) return;

            int maximumTurns = Mathf.Max(1, turnGameManager.MaximumTurns);
            int rounds = Mathf.Min(hazardSchedule.MaximumRounds, maximumTurns);
            float halfGap = turnBarGap * 0.5f;
            for (int round = 1; round <= rounds; round++)
            {
                var trapTypes = hazardSchedule.GetTrapTypes(round);
                if (trapTypes.Count == 0) continue;

                RectTransform marker = Object.Instantiate(hazardEventPrefab, turnProgressTrack, false);
                marker.name = $"HazardEvent (Turn {round})";
                // Center the prefab in the gap while keeping its authored width.
                float gapStart = (round - halfGap) / maximumTurns;
                float gapEnd = Mathf.Min(1f, (round + halfGap) / maximumTurns);
                marker.anchorMin = marker.anchorMax = new Vector2((gapStart + gapEnd) * 0.5f, 0.5f);
                marker.anchoredPosition = Vector2.zero;
                hazardEvents.Add(marker);
                CreateHazardTurnLabel(marker, round);

                var icons = new List<(string trapType, Sprite sprite)>(trapTypes.Count);
                foreach (string trapType in trapTypes)
                {
                    Sprite sprite = trapType == typeof(TurnArrowTrap).FullName ? arrowTrapIcon :
                        trapType == typeof(TurnSpikeTrap).FullName ? spikeTrapIcon : null;
                    if (sprite != null) icons.Add((trapType, sprite));
                }
                for (int i = 0; i < icons.Count; i++)
                {
                    var (trapType, sprite) = icons[i];
                    CreateHazardIcon(marker, sprite, i - (icons.Count - 1) * 0.5f, round, trapType);
                }
            }
        }

        private void CreateHazardTurnLabel(RectTransform marker, int round)
        {
            var labelObject = new GameObject("Turn Number", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.layer = marker.gameObject.layer;

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset font = hazardTurnFont;
            if (font != null) label.font = font;
            label.text = round.ToString();
            label.fontSize = 37f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            RectTransform rect = label.rectTransform;
            rect.SetParent(marker, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(56f, 32f);
            rect.anchoredPosition = new Vector2(0f, 13f);
        }

        private void CreateHazardIcon(RectTransform marker, Sprite sprite, float column, int round, string trapType)
        {
            const float iconSize = 54f;
            const float spacing = 10f;
            var iconObject = new GameObject(sprite.name, typeof(RectTransform), typeof(Image), typeof(Button));
            iconObject.layer = marker.gameObject.layer;
            var icon = iconObject.GetComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = true;

            RectTransform rect = icon.rectTransform;
            rect.SetParent(marker, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(iconSize, iconSize);
            rect.anchoredPosition = new Vector2(column * (iconSize + spacing), -spacing);

            var button = iconObject.GetComponent<Button>();
            button.targetGraphic = icon;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            TurnTrap lastFocusedTrap = null;
            button.onClick.AddListener(() =>
            {
                CameraFocusManager camera = CameraFocusManager.Instance;
                if (!canAcceptInput() || hazardSchedule == null ||
                    turnGameManager == null || turnGameManager.IsIntroPlaying || turnGameManager.IsGameComplete ||
                    Time.timeScale == 0f || camera == null || !camera.isActiveAndEnabled)
                    return;

                List<TurnTrap> traps = hazardSchedule.GetActiveTraps(round, trapType);
                if (traps.Count == 0) return;
                int next = (traps.IndexOf(lastFocusedTrap) + 1) % traps.Count;
                lastFocusedTrap = traps[next];
                camera.FocusObject(lastFocusedTrap.transform);
            });
        }

        private void ClearHazardEvents()
        {
            foreach (RectTransform marker in hazardEvents)
            {
                if (marker == null) continue;
                marker.gameObject.SetActive(false);
                Object.Destroy(marker.gameObject);
            }
            hazardEvents.Clear();
        }
    }
}
