using System.Collections;
using UnityEngine;

// Scheduling only. These fields preserve existing scene and prefab settings.
[DisallowMultipleComponent]
public class TurnSpikeTrap : TurnTrap
{
    [SerializeField, Min(1)] private int intervalTurns = 2;
    [SerializeField] private Tile targetTile;
    [SerializeField] private Renderer basePlate;
    [SerializeField] private Transform modelRoot;
    [SerializeField, Min(0f)] private float raiseDuration = 0.2f;
    private SpikeTrap trap;
    private int lastResolvedRound = -1;
    public override int IntervalTurns => Mathf.Max(1, intervalTurns);
    public bool IsRaised => trap != null && trap.IsRaised;

    private void Awake()
    {
        if (GetComponent<SwitchSpikeTrap>() != null)
        {
            Debug.LogError("Use either TurnSpikeTrap or SwitchSpikeTrap on a trap, not both.", this);
            enabled = false;
            return;
        }
        trap = GetComponent<SpikeTrap>();
        if (trap == null)
        {
            trap = gameObject.AddComponent<SpikeTrap>();
            trap.Configure(targetTile, basePlate, modelRoot, raiseDuration);
        }
        else trap.Initialize();
    }

    private void OnEnable()
    {
        SpikeTrapEvents.LowerRequested += OnLowerRequested;
        if (trap != null) trap.enabled = true;
    }
    private void OnDisable()
    {
        SpikeTrapEvents.LowerRequested -= OnLowerRequested;
        if (trap != null) trap.enabled = false;
    }

    public override IEnumerator ResolveTurn(int turn)
    {
        if (!isActiveAndEnabled || trap == null || turn <= 0 || lastResolvedRound == turn) yield break;
        lastResolvedRound = turn;
        if (IsRaised && turn > LastTriggeredTurn)
        {
            yield return trap.MoveTo(false);
            yield break;
        }
        yield return base.ResolveTurn(turn);
    }

    protected override IEnumerator Activate()
    {
        if (!IsRaised && trap != null) yield return trap.MoveTo(true);
    }

    private void OnLowerRequested(TurnSpikeTrap target)
    {
        if (target == this && trap != null) trap.SnapTo(false);
    }
}
