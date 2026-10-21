using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public partial class TurnGameManager
{
    [Header("Fixed Turn Logging (rounds count UP from 1)")]
    [SerializeField] private bool logFixedTurnsToConsole = true;
    [SerializeField] private bool saveFixedTurnLogToFile = true;
    [SerializeField, TextArea(4, 16)] private string fixedTurnScheduleSummary;
    [SerializeField] private List<FixedTurnScheduleEntry> fixedTurnSchedule = new();
    [SerializeField] private List<string> fixedTurnLog = new();
    [SerializeField] private string fixedTurnLogFilePath;

    private bool fixedTurnFileLoggingFailed;

    public int RoundNumber => ElapsedTurn;
    public IReadOnlyList<FixedTurnScheduleEntry> FixedTurnSchedule => fixedTurnSchedule;
    public IReadOnlyList<string> FixedTurnLog => fixedTurnLog;
    public string FixedTurnScheduleSummary => fixedTurnScheduleSummary;
    public string FixedTurnLogFilePath => fixedTurnLogFilePath;

    private void InitializeFixedTurnLogging()
    {
        fixedTurnLog.Clear();
        fixedTurnLogFilePath = string.Empty;
        fixedTurnFileLoggingFailed = false;
        RebuildFixedTurnSchedule();
    }

    /// <summary>Refresh explicitly after adding objects or changing their configured schedule.</summary>
    [ContextMenu("Fixed Turns/Rebuild And Log Schedule")]
    public void RebuildFixedTurnSchedule()
    {
        fixedTurnSchedule.Clear();
        foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (component == null || component.gameObject.scene != gameObject.scene ||
                !(component is IFixedTurnScheduledObject scheduled)) continue;
            string path = GetFixedTurnObjectPath(component.transform);
            for (int round = 1; round <= maximumTurns; round++)
            {
                if (!scheduled.IsScheduledOnTurn(round, maximumTurns)) continue;
                fixedTurnSchedule.Add(new FixedTurnScheduleEntry(round, component, path,
                    scheduled.ScheduledActionName, component.isActiveAndEnabled));
            }
        }
        fixedTurnSchedule.Sort((a, b) =>
        {
            int order = a.RoundNumber.CompareTo(b.RoundNumber);
            if (order == 0) order = string.CompareOrdinal(a.ObjectPath, b.ObjectPath);
            if (order == 0) order = string.CompareOrdinal(a.ActionName, b.ActionName);
            return order;
        });

        var summary = new StringBuilder();
        summary.Append("[FixedTurn][Plan] Scene: ").Append(gameObject.scene.name)
            .Append(" | Rounds 1..").Append(maximumTurns)
            .AppendLine(" (counting UP, not TurnsLeft)");
        summary.AppendLine("Configured schedule, not a guarantee: disabled/destroyed objects and game end can prevent execution.");
        for (int round = 1; round <= maximumTurns; round++)
        {
            summary.Append("Round ").Append(round).Append(": ");
            bool found = false;
            foreach (FixedTurnScheduleEntry entry in fixedTurnSchedule)
            {
                if (entry.RoundNumber != round) continue;
                if (found) summary.Append("; ");
                found = true;
                summary.Append(entry.ObjectPath).Append(" -> ").Append(entry.ActionName);
                if (!entry.EnabledWhenCaptured) summary.Append(" [inactive when captured]");
            }
            if (!found) summary.Append("(none)");
            summary.AppendLine();
        }
        fixedTurnScheduleSummary = summary.ToString().TrimEnd();
        AppendFixedTurnLog(fixedTurnScheduleSummary, this);
    }

    public IEnumerable<FixedTurnScheduleEntry> GetScheduledObjectsForTurn(int roundNumber)
    {
        foreach (FixedTurnScheduleEntry entry in fixedTurnSchedule)
            if (entry.RoundNumber == roundNumber) yield return entry;
    }

    private void LogFixedTurnRoundStart()
    {
        var message = new StringBuilder("[FixedTurn][RoundStart] Round ");
        message.Append(RoundNumber).Append('/').Append(maximumTurns)
            .Append(" | TurnsLeft: ").Append(CurrentTurn).Append(" | Scheduled: ");
        bool found = false;
        foreach (FixedTurnScheduleEntry entry in GetScheduledObjectsForTurn(RoundNumber))
        {
            if (found) message.Append("; ");
            found = true;
            message.Append(entry.ObjectPath).Append(" -> ").Append(entry.ActionName);
            if (entry.Source == null) message.Append(" [destroyed]");
            else if (!entry.Source.isActiveAndEnabled) message.Append(" [inactive]");
        }
        if (!found) message.Append("(none)");
        AppendFixedTurnLog(message.ToString(), this);
    }

    /// <summary>Called at the real execution point, not inferred from a planned entry.</summary>
    public bool RecordFixedTurnExecution(MonoBehaviour source, int roundNumber, string detail)
    {
        if (roundNumber < 1 || roundNumber > maximumTurns || source == null || source.gameObject.scene != gameObject.scene ||
            !(source is IFixedTurnScheduledObject)) return false;
        AppendFixedTurnLog($"[FixedTurn][Execution] Round {roundNumber}/{maximumTurns} | " +
            $"TurnsLeft: {CurrentTurn} | {GetFixedTurnObjectPath(source.transform)} -> {detail}", source);
        return true;
    }

    [ContextMenu("Fixed Turns/Print Stored Log")]
    public void PrintFixedTurnLog()
    {
        // Reprinting must not add duplicate history entries or append to the file.
        foreach (string message in fixedTurnLog) Debug.Log(message, this);
    }

    private void AppendFixedTurnLog(string message, UnityEngine.Object context)
    {
        fixedTurnLog.Add(message);
        if (logFixedTurnsToConsole) Debug.Log(message, context);
        if (!saveFixedTurnLogToFile || fixedTurnFileLoggingFailed) return;
        try
        {
            bool createdFile = false;
            if (string.IsNullOrEmpty(fixedTurnLogFilePath))
            {
                string folder = Path.Combine(Application.persistentDataPath, "TurnLogs");
                Directory.CreateDirectory(folder);
                string sceneName = gameObject.scene.name;
                foreach (char invalid in Path.GetInvalidFileNameChars()) sceneName = sceneName.Replace(invalid, '_');
                string session = Guid.NewGuid().ToString("N").Substring(0, 8);
                fixedTurnLogFilePath = Path.Combine(folder,
                    $"FixedTurns_{sceneName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{session}.log");
                createdFile = true;
            }
            File.AppendAllText(fixedTurnLogFilePath, message + Environment.NewLine, new UTF8Encoding(false));
            if (createdFile && logFixedTurnsToConsole) Debug.Log("[FixedTurn][LogFile] " + fixedTurnLogFilePath, this);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
            exception is ArgumentException || exception is NotSupportedException || exception is System.Security.SecurityException)
        {
            fixedTurnFileLoggingFailed = true;
            Debug.LogWarning("Fixed turn log file could not be written; in-memory log remains available. " + exception.Message, this);
        }
    }

    private static string GetFixedTurnObjectPath(Transform target)
    {
        string path = target.name;
        for (Transform parent = target.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;
        return target.gameObject.scene.name + "/" + path;
    }
}
