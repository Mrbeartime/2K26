using System;
using UnityEngine;

/// <summary>A deterministic schedule only. Implementations keep control of execution.</summary>
public interface IFixedTurnScheduledObject
{
    string ScheduledActionName { get; }
    bool IsScheduledOnTurn(int roundNumber, int maximumRounds);
}

[Serializable]
public sealed class FixedTurnScheduleEntry
{
    [SerializeField] private int roundNumber;
    [SerializeField] private MonoBehaviour source;
    [SerializeField] private string objectPath;
    [SerializeField] private string actionName;
    [SerializeField] private bool enabledWhenCaptured;

    public int RoundNumber => roundNumber;
    public MonoBehaviour Source => source;
    public string ObjectPath => objectPath;
    public string ActionName => actionName;
    public bool EnabledWhenCaptured => enabledWhenCaptured;

    public FixedTurnScheduleEntry(int round, MonoBehaviour target, string path, string action, bool enabled)
    {
        roundNumber = round;
        source = target;
        objectPath = path;
        actionName = action;
        enabledWhenCaptured = enabled;
    }
}
