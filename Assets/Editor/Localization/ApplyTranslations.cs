using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

/// <summary>
/// Reads Assets/Localize/ThaiTextExport.csv (with the English column filled in) and
///  1. puts every translated text into the "UI" string table (Thai + English), and
///  2. adds a LocalizeStringEvent to each TextMeshPro text that shows that Thai text.
/// Use the preview command first: it only logs what would change.
/// Only the "m_text" rows (TextMeshPro labels) are applied. Other rows are listed in the Console.
/// </summary>
public static class ApplyTranslations
{
    private const string CsvPath = "Assets/Localize/ThaiTextExport.csv";
    private const string TableName = "UI";
    private const string TableFolder = "Assets/Localize";
    private const string ThaiCode = "th-TH";
    private const string EnglishCode = "en";
    private static readonly string[] SkipFolders =
    {
        "Assets/Localize/", "Assets/AddressableAssetsData/", "Assets/TextMesh Pro/", "Assets/Plugins/ThaiTextCare/"
    };

    private sealed class Entry
    {
        public string Thai, English, Key;
    }

    private sealed class Report
    {
        public int Wired, AlreadyWired, SkippedNestedInstance;
        public readonly List<string> Untranslated = new List<string>();
    }

    [MenuItem("Tools/Localization/2. Apply Translations (preview only)")]
    private static void Preview() => Run(false);

    [MenuItem("Tools/Localization/3. Apply Translations (write changes)")]
    private static void Write()
    {
        if (!EditorUtility.DisplayDialog("Apply translations",
            "This edits the UI string table and adds components to prefabs and scenes in bulk.\n\n" +
            "Back up the project or commit it to git first.\n\nContinue?", "Apply", "Cancel")) return;
        Run(true);
    }

    private static void Run(bool write)
    {
        if (!File.Exists(CsvPath))
        {
            EditorUtility.DisplayDialog("Apply translations", $"{CsvPath} not found. Run the scan first.", "OK");
            return;
        }
        if (write && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var entries = ReadEntries(out int ignoredRows, out var otherProperties);
        if (entries.Count == 0)
        {
            EditorUtility.DisplayDialog("Apply translations", "No TextMeshPro rows with an English translation were found.", "OK");
            return;
        }

        var report = new Report();
        try
        {
            if (write) FillTable(entries);
            ProcessPrefabs(entries, write, report);
            ProcessScenes(entries, write, report);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (write)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        var log = new StringBuilder();
        log.AppendLine(write ? "[ApplyTranslations] DONE" : "[ApplyTranslations] PREVIEW (nothing was changed)");
        log.AppendLine($"Translated texts: {entries.Count}");
        log.AppendLine($"{(write ? "Components added" : "Components that would be added")}: {report.Wired}");
        log.AppendLine($"Already localized (skipped): {report.AlreadyWired}");
        log.AppendLine($"Inside nested prefab instances (handled through their own prefab): {report.SkippedNestedInstance}");
        log.AppendLine($"Rows without English or not TextMeshPro: {ignoredRows}");
        foreach (var pair in otherProperties)
            log.AppendLine($"  Not applied automatically: {pair.Value} row(s) of property '{pair.Key}' (set by code or ScriptableObject)");
        if (report.Untranslated.Count > 0)
        {
            log.AppendLine($"Thai TextMeshPro texts with no translation in the CSV: {report.Untranslated.Count}");
            for (int i = 0; i < report.Untranslated.Count && i < 50; i++) log.AppendLine("  " + report.Untranslated[i]);
            if (report.Untranslated.Count > 50) log.AppendLine("  ...");
        }
        Debug.Log(log.ToString());
        EditorUtility.DisplayDialog("Apply translations",
            $"{(write ? "Added" : "Would add")} {report.Wired} LocalizeStringEvent component(s).\n" +
            $"{report.Untranslated.Count} Thai text(s) still have no translation.\n\nDetails are in the Console.", "OK");
    }

    // ---------- CSV ----------

    private static Dictionary<string, Entry> ReadEntries(out int ignoredRows, out Dictionary<string, int> otherProperties)
    {
        ignoredRows = 0;
        otherProperties = new Dictionary<string, int>();
        var byThai = new Dictionary<string, Entry>();
        var usedKeys = new HashSet<string>();
        List<List<string>> table = ParseCsv(File.ReadAllText(CsvPath, Encoding.UTF8));
        // Columns: Source, FileID, Property, Line, Thai, English
        for (int i = 1; i < table.Count; i++)
        {
            List<string> row = table[i];
            if (row.Count < 6) continue;
            string property = row[2], thai = row[4], english = row[5];
            if (string.IsNullOrWhiteSpace(english)) { ignoredRows++; continue; }
            if (property != "m_text")
            {
                otherProperties.TryGetValue(property, out int count);
                otherProperties[property] = count + 1;
                continue;
            }

            string normalized = Normalize(thai);
            if (byThai.TryGetValue(normalized, out var existing))
            {
                if (existing.English != english.Trim())
                    Debug.LogWarning($"[ApplyTranslations] Same Thai text has two different translations, using the first: \"{normalized}\"");
                continue;
            }
            byThai[normalized] = new Entry { Thai = normalized, English = english.Replace("\\n", "\n").Trim(), Key = MakeKey(english, usedKeys) };
        }
        return byThai;
    }

    private static string MakeKey(string english, HashSet<string> used)
    {
        var sb = new StringBuilder();
        foreach (char c in english)
        {
            if (sb.Length >= 40) break;
            if (char.IsLetterOrDigit(c) && c < 128) sb.Append(char.ToLowerInvariant(c));
            else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
        }
        string baseKey = sb.ToString().Trim('_');
        if (baseKey.Length == 0) baseKey = "text";
        string key = baseKey;
        for (int n = 2; !used.Add(key); n++) key = baseKey + "_" + n;
        return key;
    }

    private static string Normalize(string text) => (text ?? "").Replace("\r", "").Replace("\\n", "\n").Trim();

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = false;
                }
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                rows.Add(row); row = new List<string>();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    // ---------- String table ----------

    private static void FillTable(Dictionary<string, Entry> entries)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(TableName)
            ?? LocalizationEditorSettings.CreateStringTableCollection(TableName, TableFolder);
        var thai = GetOrAddTable(collection, ThaiCode);
        var english = GetOrAddTable(collection, EnglishCode);
        foreach (Entry entry in entries.Values)
        {
            thai.AddEntry(entry.Key, entry.Thai);
            english.AddEntry(entry.Key, entry.English);
        }
        EditorUtility.SetDirty(thai);
        EditorUtility.SetDirty(english);
        EditorUtility.SetDirty(collection.SharedData);
    }

    private static StringTable GetOrAddTable(StringTableCollection collection, string code)
    {
        var id = new LocaleIdentifier(code);
        var table = collection.GetTable(id) as StringTable;
        if (table == null) table = collection.AddNewTable(id) as StringTable;
        return table;
    }

    // ---------- Prefabs and scenes ----------

    private static bool Skipped(string path)
    {
        foreach (string folder in SkipFolders)
            if (path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void ProcessPrefabs(Dictionary<string, Entry> entries, bool write, Report report)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Skipped(path)) continue;
            if (i % 10 == 0) EditorUtility.DisplayProgressBar("Prefabs", path, i / (float)guids.Length);

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int before = report.Wired;
                Wire(root, path, entries, write, report);
                if (write && report.Wired > before) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void ProcessScenes(Dictionary<string, Entry> entries, bool write, Report report)
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Skipped(path)) continue;
            EditorUtility.DisplayProgressBar("Scenes", path, i / (float)guids.Length);

            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                int before = report.Wired;
                foreach (GameObject root in scene.GetRootGameObjects())
                    Wire(root, path, entries, write, report);
                if (write && report.Wired > before)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void Wire(GameObject root, string source, Dictionary<string, Entry> entries, bool write, Report report)
    {
        foreach (TMP_Text tmp in root.GetComponentsInChildren<TMP_Text>(true))
        {
            string text = Normalize(tmp.text);
            if (!HasThai(text)) continue;

            // Objects of a nested prefab instance are localized in their own prefab.
            if (PrefabUtility.IsPartOfPrefabInstance(tmp)) { report.SkippedNestedInstance++; continue; }

            if (!entries.TryGetValue(text, out Entry entry))
            {
                report.Untranslated.Add($"{source} : {GetPath(tmp.transform)} : {text.Replace("\n", "\\n")}");
                continue;
            }
            if (tmp.GetComponent<LocalizeStringEvent>() != null) { report.AlreadyWired++; continue; }

            report.Wired++;
            if (write) AddEvent(tmp, entry.Key);
        }
    }

    private static void AddEvent(TMP_Text tmp, string key)
    {
        var localize = tmp.gameObject.AddComponent<LocalizeStringEvent>();
        localize.StringReference = new LocalizedString { TableReference = TableName, TableEntryReference = key };
        // Same wiring the Localization package's own "Localize" menu produces: the string is sent to TMP_Text.text.
        var setter = typeof(TMP_Text).GetProperty("text").GetSetMethod();
        var action = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), tmp, setter);
        UnityEventTools.AddPersistentListener(localize.OnUpdateString, action);
        localize.OnUpdateString.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
        EditorUtility.SetDirty(tmp.gameObject);
    }

    private static bool HasThai(string s)
    {
        foreach (char c in s)
            if (c >= '฀' && c <= '๿') return true;
        return false;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
