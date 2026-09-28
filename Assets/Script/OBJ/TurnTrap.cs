using System.Collections;
using UnityEngine;

/// <summary>Resolved by TurnGameManager after enemies, before the round advances.</summary>
public abstract class TurnTrap : MonoBehaviour
{
    public abstract int IntervalTurns { get; }
    public int LastTriggeredTurn { get; private set; }

    public bool IsDue(int turn) => turn > 0 && turn % Mathf.Max(1, IntervalTurns) == 0;

    public IEnumerator ResolveTurn(int turn)
    {
        if (!isActiveAndEnabled || !IsDue(turn) || LastTriggeredTurn == turn) yield break;
        LastTriggeredTurn = turn;
        Debug.Log($"Turn {turn}: {name} activated.", this);
        yield return Activate();
    }

    protected abstract IEnumerator Activate();
}
