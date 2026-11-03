using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UITurnCounter : MonoBehaviour
{
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
    [SerializeField, Min(1)] private float eventIconSize = 24f;
    private RectTransform scheduleDial;
    private readonly List<Image> roundTicks = new();

    public void BuildScheduleDial(TurnScheduleData schedule, InGameUIManager.HazardEntry[] hazards)
    {
        if (schedule == null || !Init()) return;
        if (scheduleDial != null)
        {
            scheduleDial.gameObject.SetActive(false);
            Destroy(scheduleDial.gameObject);
        }
        roundTicks.Clear();
        var hand = leg.rectTransform;
        scheduleDial = new GameObject("Schedule Dial", typeof(RectTransform)).GetComponent<RectTransform>();
        scheduleDial.SetParent(hand.parent, false);
        scheduleDial.anchorMin = hand.anchorMin;
        scheduleDial.anchorMax = hand.anchorMax;
        scheduleDial.pivot = hand.pivot;
        scheduleDial.sizeDelta = hand.sizeDelta;
        scheduleDial.anchoredPosition = hand.anchoredPosition;
        scheduleDial.localScale = hand.localScale;
        scheduleDial.SetSiblingIndex(hand.GetSiblingIndex());
        for (int round = 1; round <= schedule.MaximumRounds; round++)
        {
            float angle = (counterClockwise ? 1 : -1) * (round - 1) * 360f / schedule.MaximumRounds;
            Vector2 direction = new Vector2(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
            var rect = DialElement("Round " + round, direction * dialRadius, new Vector2(3, 12));
            rect.localEulerAngles = new Vector3(0, 0, angle);
            var tick = rect.gameObject.AddComponent<Image>();
            tick.raycastTarget = false;
            roundTicks.Add(tick);
            var label = DialElement("Number", direction * (dialRadius - 20), new Vector2(30, 20)).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = round.ToString();
            label.fontSize = 14;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            int index = 0;
            if (hazards == null) continue;
            foreach (var entry in hazards)
            {
                if (entry == null || entry.hazard == null || entry.iconPrefab == null) continue;
                var trap = entry.hazard.GetComponent<TurnTrap>();
                if (trap == null || schedule.GetState(round, trap) != 1) continue;
                var source = entry.iconPrefab.GetComponent<ObjectIcon>();
                Sprite portrait = source != null && source.sprites != null && source.sprites.Length > 0 ? source.sprites[0] : null;
                if (portrait == null)
                    foreach (var img in entry.iconPrefab.GetComponentsInChildren<Image>(true))
                        if (img.sprite != null && (source == null || img != source.bgEffectImage))
                        { portrait = img.sprite; break; }
                var icon = DialElement(entry.hazard.name,
                    direction * (dialRadius + 20 + index++ * (eventIconSize + 4)), Vector2.one * eventIconSize).gameObject.AddComponent<Image>();
                icon.sprite = portrait;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
        }
    }

    private RectTransform DialElement(string elementName, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(elementName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(scheduleDial, false);
        rect.anchorMin = rect.anchorMax = scheduleDial.pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public void HighlightScheduleRound(int round, bool complete)
    {
        for (int i = 0; i < roundTicks.Count; i++)
            roundTicks[i].color = complete || i + 1 < round ? Color.gray
                : i + 1 == round ? new Color(1f, 0.75f, 0.2f) : Color.white;
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
