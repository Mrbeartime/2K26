using System;

/// <summary>Counts end-of-round boundaries, including the current unresolved round.</summary>
public static class TrapCountdownSchedule
{
    public static int TurnsUntilActivation(int intervalTurns, int currentRound, int lastTriggeredRound)
    {
        int interval = Math.Max(1, intervalTurns);
        int round = Math.Max(1, currentRound);
        // This round's activation has already started/finished. Show the next cycle.
        if (lastTriggeredRound >= round) return interval;
        return interval - ((round - 1) % interval);
    }
}
