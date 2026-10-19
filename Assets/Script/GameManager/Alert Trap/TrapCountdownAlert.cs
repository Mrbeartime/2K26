using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation only: observes a TurnTrap without changing its schedule.</summary>
[DisallowMultipleComponent]
public class TrapCountdownAlert : MonoBehaviour
{
    [Header("Choose a trap instance from the Hierarchy")]
    [Tooltip("Optional prefab instance/root containing a TurnTrap. Use one object per countdown.")]
    [SerializeField] private GameObject targetObject;
    [SerializeField] private TurnTrap targetTrap;
    [SerializeField] private TurnGameManager turnManager;

    [Header("UI objects stored in the prefab")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Text countdownText;

    [Header("Placement")]
    [Tooltip("Optional point on the trap model, e.g. the barrel or Fire Point.")]
    [SerializeField] private Transform positionAnchor;
    [SerializeField] private bool followTarget = true;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.65f, 0f);
    [SerializeField] private Camera viewCamera;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool hideWhenGameEnds = true;

    [Header("Appearance")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.7f, 0.15f, 1f);
    [SerializeField] private Color dangerColor = new Color(1f, 0.2f, 0.15f, 1f);

    public TurnTrap TargetTrap => targetTrap;
    public int TurnsRemaining { get; private set; }

    private int lastDisplayedTurns = int.MinValue;
    private GameObject lastTargetObject;

    private void OnEnable()
    {
        FindReferences();
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
            // A prefab asset has no live turn state: select its placed instance.
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

    private void LateUpdate()
    {
        // Polling also handles inspector changes, trap disable/re-enable, and spikes
        // lowered by SpikeTrapEvents without relying on a UI-only event sequence.
        Refresh();
        if (visualRoot == null || !visualRoot.gameObject.activeSelf || targetTrap == null) return;
        if (followTarget)
            visualRoot.position = (positionAnchor != null ? positionAnchor.position : targetTrap.transform.position) + worldOffset;
        if (faceCamera && viewCamera != null)
            visualRoot.rotation = Quaternion.LookRotation(viewCamera.transform.forward, viewCamera.transform.up);
    }

    private void Refresh()
    {
        FindReferences();
        bool active = targetTrap != null && (targetTrap.IsResolving ||
            (targetTrap is TurnSpikeTrap spike && spike.IsRaised));
        bool visible = !active && targetTrap != null && targetTrap.isActiveAndEnabled && turnManager != null &&
            turnManager.CurrentTurn > 0 && (!hideWhenGameEnds || !turnManager.IsGameComplete);
        if (visualRoot != null) visualRoot.gameObject.SetActive(visible);
        if (!visible)
        {
            TurnsRemaining = 0;
            lastDisplayedTurns = int.MinValue;
            return;
        }

        int round = Mathf.Clamp(turnManager.maximumTurns - turnManager.CurrentTurn + 1, 1, Mathf.Max(1, turnManager.maximumTurns));
        TurnsRemaining = TrapCountdownSchedule.TurnsUntilActivation(
            targetTrap.IntervalTurns, round, targetTrap.LastTriggeredTurn);
        if (countdownText != null)
            countdownText.color = TurnsRemaining == 1 ? dangerColor : TurnsRemaining == 2 ? warningColor : normalColor;
        if (lastDisplayedTurns == TurnsRemaining) return;
        lastDisplayedTurns = TurnsRemaining;
        if (countdownText != null) countdownText.text = TurnsRemaining.ToString();
    }

    private void OnDisable()
    {
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
    }
}
