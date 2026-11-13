using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation only: observes a TurnTrap without changing its schedule.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class TrapCountdownAlert : MonoBehaviour
{
    private enum AlertState { Normal, Warning, Active }

    [Serializable]
    private struct StateSprite
    {
        public AlertState state;
        public Sprite sprite;
    }

    [Header("Choose a trap instance from the Hierarchy")]
    [Tooltip("Optional prefab instance/root containing a TurnTrap. Use one object per countdown.")]
    [SerializeField] private GameObject targetObject;
    [SerializeField] private TurnTrap targetTrap;

    [SerializeField] private TurnGameManager turnManager;

    [Header("UI objects stored in the prefab")]
    // Countdown Canvas อยู่ในฉาก 3D และใช้ CanvasGroup ทำให้ทั้งรูปและข้อความจางได้
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private CanvasGroup visualGroup;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private Image countdownImage;

    [Header("Placement")]
    // เว้นว่างเพื่อยึดเหนือกึ่งกลางของโมเดล หรือเลือก Transform ที่ใช้เป็นจุดอ้างอิงเอง
    [Tooltip("Optional anchor. If empty, the label is centered above the linked trap's visible model.")]
    [SerializeField] private Transform positionAnchor;
    // ปิดได้หากต้องการจัดตำแหน่ง Countdown Canvas เองใน Hierarchy
    [SerializeField] private bool followTarget = true;
    [Tooltip("Vertical gap in rendered pixels between the anchor and the bottom of the label box.")]
    [SerializeField, Min(0f)] private float screenGap = 6f;
    [Tooltip("Rendered pixels per Canvas unit. Increase to make the number and sprites larger.")]
    [SerializeField, Min(0.01f)] private float pixelScale = 0.62f;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool hideWhenGameEnds = true;

    [Header("Appearance")]
    [Tooltip("Optional TMP Font Asset. If empty, use the font asset on Countdown Text.")]
    [SerializeField] private TMP_FontAsset fontOverride;
    [Tooltip("One sprite per state. Normal: more than 1 turn left. Warning: 1 turn left. Active: firing or raising. Empty slots use the countdown number.")]
    [SerializeField] private List<StateSprite> stateSprites = new()
    {
        new StateSprite { state = AlertState.Normal },
        new StateSprite { state = AlertState.Warning },
        new StateSprite { state = AlertState.Active }
    };
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.7f, 0.15f, 1f);
    [SerializeField] private Color dangerColor = new Color(1f, 0.2f, 0.15f, 1f);

    [Header("Activation Animation")]
    [SerializeField, Min(0.01f)] private float activationDuration = 0.35f;
    [SerializeField, Min(1f)] private float activationScale = 1.6f;

    public TurnTrap TargetTrap => targetTrap;
    public int TurnsRemaining { get; private set; }

    private TMP_FontAsset defaultFont;
    private int lastDisplayedTurns = int.MinValue;
    private GameObject lastTargetObject;
    private TurnTrap observedTrap;
    private Renderer[] trapRenderers = Array.Empty<Renderer>();
    private int observedTriggeredTurn;
    private float activationStartedAt = -1f;

    private void Awake()
    {
        if (countdownText != null) defaultFont = countdownText.font;
    }

    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>Can also bind the same prefab from another UI/controller.</summary>
    public void Bind(TurnTrap trap)
    {
        targetObject = null;
        lastTargetObject = null;
        targetTrap = trap;
        lastDisplayedTurns = int.MinValue;
        Refresh();
    }

    private void FindReferences()
    {
        if (targetObject != lastTargetObject)
        {
            targetTrap = null;
            lastTargetObject = targetObject;
        }
        if (targetObject != null && targetTrap == null)
        {
            if (targetObject.scene.IsValid())
                targetTrap = targetObject.GetComponent<TurnTrap>() ??
                    targetObject.GetComponentInParent<TurnTrap>() ??
                    targetObject.GetComponentInChildren<TurnTrap>(true);
        }
        if (targetTrap != null && !targetTrap.gameObject.scene.IsValid()) targetTrap = null;
        if (targetObject != null && targetTrap == null) return;
        if (targetTrap == null) targetTrap = GetComponentInParent<TurnTrap>();
        if (turnManager == null) turnManager = TurnGameManager.Instance;
        if (viewCamera == null) viewCamera = Camera.main;
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        FindReferences();
        if (observedTrap != targetTrap)
        {
            // เริ่มติดตามจากสถานะปัจจุบันเมื่อเปลี่ยนกับดัก จึงไม่เล่นเอฟเฟกต์ของเทิร์นที่ผ่านไปแล้ว
            observedTrap = targetTrap;
            observedTriggeredTurn = targetTrap != null ? targetTrap.LastTriggeredTurn : 0;
            trapRenderers = targetTrap != null ? targetTrap.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            activationStartedAt = -1f;
        }

        TurnsRemaining = 0;
        if (targetTrap == null || !targetTrap.isActiveAndEnabled || turnManager == null)
        {
            activationStartedAt = -1f;
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
            return;
        }

        // LastTriggeredTurn เปลี่ยนครั้งเดียวต่อการทำงาน จึงจับได้แม้หน้าไม้ยิงจบภายในเฟรมเดียว
        if (targetTrap.LastTriggeredTurn != observedTriggeredTurn)
        {
            observedTriggeredTurn = targetTrap.LastTriggeredTurn;
            activationStartedAt = Time.unscaledTime;
        }

        float progress = activationStartedAt < 0f ? 1f :
            Mathf.Clamp01((Time.unscaledTime - activationStartedAt) / Mathf.Max(0.01f, activationDuration));
        bool animating = progress < 1f;
        bool trapActive = targetTrap.IsResolving || (targetTrap is TurnSpikeTrap spike && spike.IsRaised);
        bool counting = !animating && !trapActive && turnManager.CurrentTurn > 0 &&
            (!hideWhenGameEnds || !turnManager.IsGameComplete);
        if (counting)
            TurnsRemaining = TrapCountdownSchedule.TurnsUntilActivation(
                targetTrap.IntervalTurns, turnManager.RoundNumber, targetTrap.LastTriggeredTurn);

        // เล่นเอฟเฟกต์ให้จบแม้กับดักทำงานเสร็จก่อน จากนั้นรอให้กับดักพร้อมนับรอบใหม่
        if (visualRoot == null) return;
        float scale = animating ? Mathf.Lerp(1f, activationScale, progress) : 1f;
        bool visible = (animating || counting) && UpdatePlacement(scale);
        if (visible)
        {
            if (visualGroup != null) visualGroup.alpha = animating ? 1f - progress : 1f;
            AlertState state = animating ? AlertState.Active : TurnsRemaining == 1 ? AlertState.Warning : AlertState.Normal;
            UpdateAppearance(state, animating ? 1 : TurnsRemaining);
        }
        visualRoot.gameObject.SetActive(visible);
    }

    private bool UpdatePlacement(float scale)
    {
        if (viewCamera == null) return false;

        Vector3 screenPoint = followTarget ? GetScreenAnchor() : viewCamera.WorldToScreenPoint(visualRoot.position);
        if (screenPoint.z <= viewCamera.nearClipPlane || screenPoint.z >= viewCamera.farClipPlane) return false;

        // เว้นช่องจากขอบล่างของกล่องป้ายเป็นพิกเซล ใช้ขนาดก่อนขยายเพื่อให้เอฟเฟกต์ขยายจากกึ่งกลางเดิม
        if (followTarget)
            screenPoint.y += screenGap + visualRoot.rect.height * pixelScale * 0.5f;

        Vector3 worldPoint = viewCamera.ScreenToWorldPoint(screenPoint);
        if (followTarget) visualRoot.position = worldPoint;
        if (faceCamera) visualRoot.rotation = viewCamera.transform.rotation;

        // แปลง 1 พิกเซลเป็นระยะในฉากที่ความลึกของป้าย จึงชดเชยได้ทั้ง Resolution และการซูม
        float worldUnitsPerPixel = Vector3.Distance(worldPoint, viewCamera.ScreenToWorldPoint(screenPoint + Vector3.up));
        Vector3 localScale = Vector3.one * (worldUnitsPerPixel * pixelScale * scale);
        Transform parent = visualRoot.parent;
        if (parent != null)
        {
            // ชดเชย Scale ที่สืบทอดจากกับดัก เพื่อให้แต่ละป้ายมีขนาดเท่ากันบนจอ
            Quaternion localRotation = visualRoot.localRotation;
            localScale.x /= Mathf.Max(0.0001f, parent.TransformVector(localRotation * Vector3.right).magnitude);
            localScale.y /= Mathf.Max(0.0001f, parent.TransformVector(localRotation * Vector3.up).magnitude);
            localScale.z /= Mathf.Max(0.0001f, parent.TransformVector(localRotation * Vector3.forward).magnitude);
        }
        visualRoot.localScale = localScale;
        return true;
    }

    private Vector3 GetScreenAnchor()
    {
        if (positionAnchor != null) return viewCamera.WorldToScreenPoint(positionAnchor.position);

        // ใช้ขอบเขตโมเดลที่แสดงอยู่เพื่อจัดกึ่งกลาง แม้ Pivot ของกับดักจะเยื้องจากตัวโมเดล
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (Renderer renderer in trapRenderers)
        {
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy) continue;
            if (hasBounds) bounds.Encapsulate(renderer.bounds);
            else bounds = renderer.bounds;
            hasBounds = true;
        }
        if (!hasBounds) return viewCamera.WorldToScreenPoint(targetTrap.transform.position);

        // ฉายมุมทั้งแปดของกรอบโมเดลลงบนจอ ใช้กึ่งกลางแนวนอนและขอบบนของกรอบที่ฉายได้
        Vector3 screen = viewCamera.WorldToScreenPoint(bounds.center);
        if (screen.z <= viewCamera.nearClipPlane) return screen;
        float left = screen.x;
        float right = screen.x;
        float top = screen.y;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = new((corner & 1) == 0 ? min.x : max.x,
                (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
            Vector3 projected = viewCamera.WorldToScreenPoint(point);
            if (projected.z <= viewCamera.nearClipPlane) continue;
            left = Mathf.Min(left, projected.x);
            right = Mathf.Max(right, projected.x);
            top = Mathf.Max(top, projected.y);
        }
        return new Vector3((left + right) * 0.5f, top, screen.z);
    }

    private void UpdateAppearance(AlertState state, int turns)
    {
        Sprite sprite = null;
        if (countdownImage != null)
        {
            foreach (StateSprite entry in stateSprites)
            {
                if (entry.state != state || entry.sprite == null) continue;
                sprite = entry.sprite;
                break;
            }
            countdownImage.sprite = sprite;
            countdownImage.enabled = sprite != null;
        }

        // ถ้าช่องรูปของสถานะนี้ว่าง ให้ใช้ตัวเลขและสีเดิม โดยช่วงเริ่มทำงานใช้เลข 1
        if (countdownText == null) return;
        countdownText.enabled = sprite == null;
        if (sprite != null) return;
        countdownText.font = fontOverride != null ? fontOverride : defaultFont;
        countdownText.color = turns == 1 ? dangerColor : turns == 2 ? warningColor : normalColor;
        if (lastDisplayedTurns == turns) return;
        lastDisplayedTurns = turns;
        countdownText.text = turns.ToString();
    }

    private void OnDisable()
    {
        observedTrap = null;
        activationStartedAt = -1f;
        TurnsRemaining = 0;
        // ปิด Canvas ด้วยเมื่อคอมโพเนนต์หยุดทำงาน เพื่อไม่ให้ป้ายค้างบนฉาก
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
    }
}
