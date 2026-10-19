using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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