using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UITurnCounter : MonoBehaviour
{
    public enum TrapEventType { ArrowTrap, SpikeTrap }

    [System.Serializable]
    public class EventIconEntry
    {
        public TrapEventType trapType;
        public Sprite icon;
    }

    [Header("Dial Event Icons")]
    [Tooltip("Simplified dial sprites, independent of the Hazard HUD icons. One entry per trap type.")]
    [SerializeField] private EventIconEntry[] eventIcons =
    {
        new EventIconEntry { trapType = TrapEventType.ArrowTrap },
        new EventIconEntry { trapType = TrapEventType.SpikeTrap }
    };
    private readonly HashSet<string> warnedMissingEventIcons = new();

    [SerializeField] public Image button;
    [SerializeField] public Image leg;   // the hand that rotates around the counter

    [Header("Rotation")]
    [Tooltip("Unity: +Z rotation = counter-clockwise on screen. Untick for clockwise.")]
    [SerializeField] private bool counterClockwise = true;

    [Tooltip("Leg rotates around its RectTransform Pivot. Tick this to force the pivot below " +
             "(the leg keeps its current look, only the rotation centre moves).")]
    [SerializeField] private bool overridePivot = false;
    [Tooltip("(0.5, 0) = middle of the bottom edge, (0.5, 0.5) = centre of the image.")]
    [SerializeField] private Vector2 legPivot = new Vector2(0.5f, 0f);

    [Header("Leg Animation")]
    [SerializeField] private float moveDuration = 1f;
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Button Press")]
    [Tooltip("anchoredPosition.y of the button while held down")]
    [SerializeField] private float pressedY = -200f;
    [Tooltip("anchoredPosition.y of the button when released")]
    [SerializeField] private float releasedY = -190f;
    [SerializeField] private float buttonMoveDuration = 0.1f;

    [Header("Schedule Dial")]
    [SerializeField, Min(1)] private float dialRadius = 90f;
    [Tooltip("Optional override. Defaults to Resources/UI/Dial/DialTick.")]
    [SerializeField] private Image dialTickPrefab;
    [Tooltip("Optional override. Defaults to Resources/UI/Dial/DialEventIcon.")]
    [SerializeField] private Image dialEventIconPrefab;
    [Tooltip("Distance inward from the tick ring to the first event icon.")]
    [SerializeField, Min(0)] private float eventOffset = 20f;
    [SerializeField, Min(0)] private float eventSpacing = 4f;
    [Tooltip("Base tick color for rounds with a scheduled trap. Current/past tints still apply.")]
    [SerializeField] private Color hazardTickColor = new Color(1f, 0.35f, 0.2f);
    [SerializeField] private Color currentTickTint = new Color(1f, 0.75f, 0.2f);
    [SerializeField] private Color pastTickTint = Color.gray;
    private RectTransform scheduleDial;
    private readonly List<Image> roundTicks = new();
    private readonly List<Color> tickColors = new();
    private readonly List<bool> hazardRounds = new();
    private readonly List<(int round, Image image)> dialEventImages = new();
    private readonly List<(int round, Graphic graphic, float alpha)> dialGraphics = new();

    public void BuildScheduleDial(TurnScheduleData schedule,
        Image tickOverride = null, Image eventIconOverride = null)
    {
        if (schedule == null || !Init()) return;
        if (tickOverride != null) dialTickPrefab = tickOverride;
        if (eventIconOverride != null) dialEventIconPrefab = eventIconOverride;
        if (dialTickPrefab == null) dialTickPrefab = Resources.Load<GameObject>("UI/Dial/DialTick")?.GetComponent<Image>();
        if (dialEventIconPrefab == null) dialEventIconPrefab = Resources.Load<GameObject>("UI/Dial/DialEventIcon")?.GetComponent<Image>();
        if (dialTickPrefab == null || dialEventIconPrefab == null)
        {
            Debug.LogError("Assign the dial tick and event icon prefabs on UITurnCounter.", this);
            return;
        }
        if (scheduleDial != null)
        {
            scheduleDial.gameObject.SetActive(false);
            Destroy(scheduleDial.gameObject);
        }
        roundTicks.Clear();
        tickColors.Clear();
        hazardRounds.Clear();
        dialGraphics.Clear();
        dialEventImages.Clear();
        var hand = leg.rectTransform;
        scheduleDial = new GameObject("Schedule Dial", typeof(RectTransform)).GetComponent<RectTransform>();
        scheduleDial.SetParent(hand.parent, false);
        scheduleDial.anchorMin = hand.anchorMin;
        scheduleDial.anchorMax = hand.anchorMax;
        scheduleDial.pivot = hand.pivot;
        scheduleDial.sizeDelta = hand.sizeDelta;
        scheduleDial.anchoredPosition = hand.anchoredPosition;
        // The hand sprite is scaled down independently. Dial dimensions use
        // parent UI units so its ticks and portraits remain readable.
        scheduleDial.localScale = Vector3.one;
        scheduleDial.SetSiblingIndex(hand.GetSiblingIndex());
        for (int round = 1; round <= schedule.MaximumRounds; round++)
        {
            float angle = (counterClockwise ? 1 : -1) * (round - 1) * 360f / schedule.MaximumRounds;
            Vector2 direction = new Vector2(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
            var tick = DialElement(dialTickPrefab, "Round " + round, direction * dialRadius);
            tick.rectTransform.localRotation = Quaternion.Euler(0, 0, angle) * dialTickPrefab.rectTransform.localRotation;
            roundTicks.Add(tick);
            tickColors.Add(tick.color);
            TrackDialGraphics(tick, round);
            var scheduledTypes = schedule.GetTrapTypes(round);
            hazardRounds.Add(scheduledTypes.Count > 0);
            int index = 0;
            foreach (string trapType in scheduledTypes)
            {
                Sprite portrait = GetEventIcon(trapType);
                if (portrait == null)
                {
                    if (warnedMissingEventIcons.Add(trapType))
                        Debug.LogWarning($"UITurnCounter: assign a Dial Event Icon for {trapType}.", this);
                    continue;
                }
                var iconRect = dialEventIconPrefab.rectTransform;
                float iconSize = Mathf.Max(iconRect.rect.width * Mathf.Abs(iconRect.localScale.x),
                    iconRect.rect.height * Mathf.Abs(iconRect.localScale.y));
                var icon = DialElement(dialEventIconPrefab, trapType,
                    direction * Mathf.Max(0f, dialRadius - eventOffset - index++ * (iconSize + eventSpacing)));
                icon.sprite = portrait;
                dialEventImages.Add((round, icon));
                TrackDialGraphics(icon, round);
            }
        }
    }

    private Sprite GetEventIcon(string scheduledType)
    {
        if (eventIcons == null) return null;
        foreach (var entry in eventIcons)
        {
            if (entry == null || entry.icon == null) continue;
            string typeName = entry.trapType switch
            {
                TrapEventType.ArrowTrap => typeof(TurnArrowTrap).FullName,
                TrapEventType.SpikeTrap => typeof(TurnSpikeTrap).FullName,
                _ => null
            };
            if (typeName == scheduledType) return entry.icon;
        }
        return null;
    }

    private void TrackDialGraphics(Image element, int round)
    {
        foreach (var graphic in element.GetComponentsInChildren<Graphic>(true))
            dialGraphics.Add((round, graphic, graphic.color.a));
    }

    private Image DialElement(Image prefab, string elementName, Vector2 position)
    {
        var element = Instantiate(prefab, scheduleDial, false);
        element.name = elementName;
        var rect = element.rectTransform;
        rect.anchorMin = rect.anchorMax = scheduleDial.pivot;
        rect.anchoredPosition = position;
        foreach (var graphic in element.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        return element;
    }

    public void HighlightScheduleRound(int round, bool complete)
    {
        for (int i = 0; i < roundTicks.Count; i++)
            roundTicks[i].color = (hazardRounds[i] ? hazardTickColor : tickColors[i]) * (i + 1 < round ? pastTickTint
                : i + 1 == round ? currentTickTint : Color.white);
        // Only earlier rounds fade, including after game end. Never accumulate alpha.
        foreach (var item in dialGraphics)
        {
            if (item.graphic == null) continue;
            Color color = item.graphic.color;
            color.a = item.round < round ? 0.5f : item.alpha;
            item.graphic.color = color;
        }
        // Match the corresponding hazard tick, including its current/past tint and opacity.
        foreach (var item in dialEventImages)
            if (item.image != null)
                item.image.color = roundTicks[item.round - 1].color;
    }

    private int maxTurn;
    private int currentTurn;
    private float legMovingDegree;
    private float currentAngle;
    private bool initialized;
    private Coroutine moveRoutine;
    private Coroutine buttonRoutine;

    private void Start()
    {
        Init();
    }

    private void OnDisable()
    {
        moveRoutine = null;
        buttonRoutine = null;

        if (initialized)
        {
            currentAngle = Mathf.Repeat(AngleForTurn(currentTurn), 360f);
            ApplyAngle(currentAngle);
        }
        SetButtonY(releasedY);
    }

    private bool Init()
    {
        if (initialized) return true;

        TurnGameManager gameManager = TurnGameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("TurnGameManager doesn't exist.", this);
            return false;
        }
        if (leg == null)
        {
            Debug.LogError("UITurnCounter: 'leg' is not assigned.", this);
            return false;
        }

        maxTurn = Mathf.Max(1, gameManager.maximumTurns);
        legMovingDegree = 360f / maxTurn;

        if (overridePivot) ApplyPivotKeepingPosition(legPivot);

        currentTurn = ClampTurn(gameManager.CurrentTurn);
        currentAngle = AngleForTurn(currentTurn);
        ApplyAngle(currentAngle);

        initialized = true;
        return true;
    }

    public void LegMoving(int newTurn)
    {
        if (!Init()) return;

        newTurn = ClampTurn(newTurn);
        if (newTurn == currentTurn) return;

        bool turnDecreased = newTurn < currentTurn;
        currentTurn = newTurn;
        float target = AngleForTurn(newTurn);

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        if (!turnDecreased || moveDuration <= 0f || !isActiveAndEnabled)
        {
            currentAngle = Mathf.Repeat(target, 360f);
            ApplyAngle(currentAngle);
            return;
        }

        moveRoutine = StartCoroutine(AnimateLeg(currentAngle, target));
    }

    private IEnumerator AnimateLeg(float from, float to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / moveDuration;
            float angle = Mathf.Lerp(from, to, easing.Evaluate(Mathf.Clamp01(t)));
            ApplyAngle(angle);
            yield return null;
        }

        currentAngle = Mathf.Repeat(to, 360f);
        ApplyAngle(currentAngle);
        moveRoutine = null;
    }

    private int ClampTurn(int turn) => Mathf.Clamp(turn, 0, maxTurn);

    private float AngleForTurn(int turn)
    {
        float direction = counterClockwise ? 1f : -1f;
        return direction * (maxTurn - turn) * legMovingDegree;
    }

    private void ApplyAngle(float angle)
    {
        if (leg == null) return;
        leg.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    private void ApplyPivotKeepingPosition(Vector2 newPivot)
    {
        RectTransform rt = leg.rectTransform;
        rt.localEulerAngles = Vector3.zero;

        Vector2 delta = Vector2.Scale(newPivot - rt.pivot, rt.rect.size);
        delta = Vector2.Scale(delta, rt.localScale);

        rt.pivot = newPivot;
        rt.anchoredPosition += delta;
    }

    public void PressButton()
    {
        MoveButton(pressedY);
    }

    public void ReleaseButton()
    {
        MoveButton(releasedY);
    }

    private void MoveButton(float targetY)
    {
        if (button == null) return;


        Debug.Log("button move");
        if (buttonRoutine != null)
        {
            StopCoroutine(buttonRoutine);
            buttonRoutine = null;
        }

        if (buttonMoveDuration <= 0f || !isActiveAndEnabled)
        {
            SetButtonY(targetY);
            return;
        }

        buttonRoutine = StartCoroutine(AnimateButton(button.rectTransform.anchoredPosition.y, targetY));
    }

    private IEnumerator AnimateButton(float fromY, float toY)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / buttonMoveDuration;
            SetButtonY(Mathf.Lerp(fromY, toY, Mathf.Clamp01(t)));
            yield return null;
        }

        SetButtonY(toY);
        buttonRoutine = null;
    }

    private void SetButtonY(float y)
    {
        if (button == null) return;
        Vector2 pos = button.rectTransform.anchoredPosition;
        pos.y = y;
        button.rectTransform.anchoredPosition = pos;
    }
}
