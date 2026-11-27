using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(ObjectIcon))]
public class IconTurnIndicator : MonoBehaviour
{
    [SerializeField] private Canvas indicatorBarZone;
    [SerializeField] private Image barTemplate;


    private TurnGameManager turns;
    private readonly List<Image> bars = new();
    private ObjectIcon icon;
    private GameObject target;
    private TurnTrap trap;
    private int laidOutInterval;

    private void Awake()
    {
        turns = TurnGameManager.Instance;
        icon = GetComponent<ObjectIcon>();
        if (barTemplate != null) bars.Add(barTemplate);
    }

    private void LateUpdate()
    {
        if (indicatorBarZone == null || barTemplate == null) return;

        // ObjectIcon receives the trap instance when InGameUIManager creates this icon.
        if (target != icon.TargetObject)
        {
            target = icon.TargetObject;
            trap = target != null ? target.GetComponent<TurnTrap>() : null;
        }


        int interval = trap != null ? trap.IntervalTurns : 0;

        bool show = interval > 1 && trap.isActiveAndEnabled && !trap.IsResolving &&
            !(trap is TurnSpikeTrap spike && spike.IsRaised) && turns != null &&
            turns.CurrentTurn > 0 && !turns.IsGameComplete;
        if (show)
        {
            if (laidOutInterval != interval) LayoutBars(interval);

            // Use the same countdown as the world-space label. The current round
            // counts toward the fill, so all bars show when the label reads 1.
            int remaining = TrapCountdownSchedule.TurnsUntilActivation(interval, turns.RoundNumber, trap.LastTriggeredTurn);
            int filled = interval - remaining + 1;
            for (int i = 0; i < bars.Count; i++)
                bars[i].gameObject.SetActive(i < filled);
        }

        indicatorBarZone.gameObject.SetActive(show);
    }

    private void LayoutBars(int interval)
    {
        while (bars.Count < interval)
            bars.Add(Instantiate(barTemplate, indicatorBarZone.transform));

        // Each segment occupies 90% of its slot. The anchors keep the gaps proportional when the icon scales.
        for (int i = 0; i < interval; i++)
        {
            RectTransform rect = bars[i].rectTransform;
            rect.anchorMin = new Vector2((i + 0.05f) / interval, rect.anchorMin.y);
            rect.anchorMax = new Vector2((i + 0.95f) / interval, rect.anchorMax.y);
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
        }
        laidOutInterval = interval;
    }

    private void OnDisable()
    {
        if (indicatorBarZone != null && gameObject.activeInHierarchy)
            indicatorBarZone.gameObject.SetActive(false);
    }
}
