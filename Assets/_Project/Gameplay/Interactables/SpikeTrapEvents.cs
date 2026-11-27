using System;
using UnityEngine;

/// <summary>Commands for spike traps. No gameplay condition publishes this yet.</summary>
public static class SpikeTrapEvents
{
    public static event Action<TurnSpikeTrap> LowerRequested;

    // A future switch/puzzle can call this with the specific trap it controls.
    public static void RequestLower(TurnSpikeTrap trap)
    {
        if (trap != null) LowerRequested?.Invoke(trap);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents() => LowerRequested = null;
}
