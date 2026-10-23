using System.Collections;
using UnityEngine;

/// <summary>Resolved by TurnGameManager after enemies, before the round advances.</summary>
public abstract class TurnTrap : MonoBehaviour, IFixedTurnScheduledObject
{
    public abstract int IntervalTurns { get; }
    public int LastTriggeredTurn { get; private set; }
    public bool IsResolving { get; private set; }
    public string ScheduledActionName => GetType().Name + " resolution, every " + Mathf.Max(1, IntervalTurns) + " rounds (end of round)";

    public bool IsScheduledOnTurn(int roundNumber, int maximumRounds) =>
        roundNumber <= maximumRounds && IsDue(roundNumber);

    public bool IsDue(int turn) => turn > 0 && turn % Mathf.Max(1, IntervalTurns) == 0;

    public IEnumerator ResolveTurn(int turn)
    {
        if (!isActiveAndEnabled || !IsDue(turn) || LastTriggeredTurn == turn) yield break;
        LastTriggeredTurn = turn;
        if (TurnGameManager.Instance == null || !TurnGameManager.Instance.RecordFixedTurnExecution(
            this, turn, "Trap resolution started (effect depends on trap state)"))
            Debug.Log($"Turn {turn}: {name} activated.", this);
        IsResolving = true;
        try
        {
            yield return Activate();
        }
        finally
        {
            IsResolving = false;
        }
    }

    protected abstract IEnumerator Activate();
}
