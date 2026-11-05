using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using UnityEngine;

/// <summary>A deterministic plan only; objects control their own execution.</summary>
public interface IFixedTurnScheduledObject
{
    string ScheduledActionName { get; }
    bool IsScheduledOnTurn(int roundNumber, int maximumRounds);
}

/// <summary>Independent doc for clocks/UI: round -> object ID -> 1 (scheduled) or 0.</summary>
[DisallowMultipleComponent]
public sealed class TurnScheduleData : MonoBehaviour
{
    [SerializeField, TextArea(4, 12)] private string turnSummary;
    [SerializeField, Min(1)] private int maximumRounds = 1;
    [SerializeField] private int currentRound = 1;
    [SerializeField, TextArea(2, 5)] private string currentTurnSummary;
    [SerializeField, HideInInspector] private List<ScheduledObject> objects = new();
    [SerializeField, HideInInspector] private List<SavedRound> savedStates = new();

    [Serializable]
    private sealed class ScheduledObject
    {
        public string id;
        public string name;
        public MonoBehaviour source;
    }

    [Serializable]
    private sealed class SavedRound
    {
        public int round;
        public List<int> values = new();
    }

    private readonly Dictionary<int, IReadOnlyDictionary<string, int>> statesByTurn = new();
    private readonly Dictionary<string, string> objectNames = new();
    private readonly Dictionary<MonoBehaviour, string> sourceIds = new();
    private ReadOnlyDictionary<int, IReadOnlyDictionary<string, int>> dictionaryView;
    private ReadOnlyDictionary<string, string> namesView;
    private bool initialized;
    private static readonly IReadOnlyDictionary<string, int> EmptyStates =
        new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());

    public int MaximumRounds => maximumRounds;
    public int CurrentRound => currentRound;
    public string TurnSummary => turnSummary;
    public string CurrentTurnSummary => currentTurnSummary;
    public event Action ScheduleChanged;
    public event Action CurrentRoundChanged;

    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, int>> StatesByTurn
    {
        get
        {
            EnsureDictionary();
            return dictionaryView ??= new ReadOnlyDictionary<int, IReadOnlyDictionary<string, int>>(statesByTurn);
        }
    }
    public IReadOnlyDictionary<string, string> ObjectNames
    {
        get
        {
            EnsureDictionary();
            return namesView ??= new ReadOnlyDictionary<string, string>(objectNames);
        }
    }
    public IReadOnlyDictionary<string, int> CurrentStates => GetStates(currentRound);

    public IReadOnlyDictionary<string, int> GetStates(int round)
    {
        EnsureDictionary();
        return statesByTurn.TryGetValue(round, out var values) ? values : EmptyStates;
    }
    public int GetState(int round, string objectId) =>
        objectId != null && GetStates(round).TryGetValue(objectId, out int value) ? value : 0;

    public int GetState(int round, MonoBehaviour source)
    {
        EnsureDictionary();
        return source != null && sourceIds.TryGetValue(source, out string id) ? GetState(round, id) : 0;
    }

    private void OnEnable()
    {
        EnsureDictionary();
        UpdateSummaries();
    }

    private void EnsureDictionary()
    {
        if (initialized) return;
        initialized = true;
        statesByTurn.Clear();
        objectNames.Clear();
        sourceIds.Clear();
        foreach (var item in objects)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) continue;
            objectNames[item.id] = item.name;
            if (item.source != null) sourceIds[item.source] = item.id;
        }
        for (int round = 1; round <= Mathf.Max(1, maximumRounds); round++)
        {
            var values = new Dictionary<string, int>();
            foreach (var item in objectNames) values[item.Key] = 0;
            foreach (var row in savedStates)
            {
                if (row == null || row.round != round || row.values == null) continue;
                for (int i = 0; i < objects.Count && i < row.values.Count; i++)
                    if (objects[i] != null && values.ContainsKey(objects[i].id))
                        values[objects[i].id] = row.values[i] > 0 ? 1 : 0;
            }
            statesByTurn.Add(round, new ReadOnlyDictionary<string, int>(values));
        }
    }

    [ContextMenu("Schedule/Refresh From Scene")]
    public void RefreshFromScene()
    {
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;
        var sources = new List<MonoBehaviour>();
        foreach (var source in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            if (source != null && source.gameObject.scene == gameObject.scene && source is IFixedTurnScheduledObject)
                sources.Add(source);
        sources.Sort((a, b) => string.CompareOrdinal(ObjectId(a), ObjectId(b)));
        objects.Clear();
        foreach (var source in sources)
            objects.Add(new ScheduledObject { id = ObjectId(source), name = source.name, source = source });
        savedStates.Clear();
        for (int round = 1; round <= Mathf.Max(1, maximumRounds); round++)
        {
            var row = new SavedRound { round = round };
            foreach (var source in sources)
                row.values.Add(((IFixedTurnScheduledObject)source).IsScheduledOnTurn(round, maximumRounds) ? 1 : 0);
            savedStates.Add(row);
        }
        int previous = currentRound;
        currentRound = Mathf.Clamp(currentRound, 1, Mathf.Max(1, maximumRounds));
        initialized = false;
        EnsureDictionary();
        UpdateSummaries();
        ScheduleChanged?.Invoke();
        if (previous != currentRound) CurrentRoundChanged?.Invoke();
    }

    public void RefreshFromScene(int rounds)
    {
        maximumRounds = Mathf.Max(1, rounds);
        RefreshFromScene();
    }

    public void SetCurrentRound(int round)
    {
        int next = Mathf.Clamp(round, 1, Mathf.Max(1, maximumRounds));
        if (next == currentRound) return;
        currentRound = next;
        currentTurnSummary = FormatRound(currentRound);
        CurrentRoundChanged?.Invoke();
    }

    private static string ObjectId(MonoBehaviour source)
    {
        string path = string.Empty;
        for (Transform item = source.transform; item != null; item = item.parent)
            path = "/" + item.name + "[" + item.GetSiblingIndex() + "]" + path;
        return path + ":" + source.GetType().Name + "[" + Array.IndexOf(source.GetComponents<MonoBehaviour>(), source) + "]";
    }

    private string FormatRound(int round)
    {
        var parts = new List<string>();
        foreach (var item in GetStates(round)) parts.Add(objectNames[item.Key] + " = " + item.Value);
        return "Round " + round + ": " + (parts.Count == 0 ? "(none)" : string.Join(", ", parts));
    }

    private void UpdateSummaries()
    {
        var text = new StringBuilder();
        for (int round = 1; round <= Mathf.Max(1, maximumRounds); round++)
        {
            if (text.Length > 0) text.AppendLine();
            text.Append(FormatRound(round));
        }
        turnSummary = text.ToString();
        currentTurnSummary = FormatRound(currentRound);
    }
}
