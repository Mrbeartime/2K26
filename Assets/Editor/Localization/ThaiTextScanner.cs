using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Read-only tool: finds Thai TextMeshPro labels (the "m_text" property) in prefabs and scenes and writes them to
/// Assets/Localize/ThaiTextExport.csv with an empty English column. Text files, code and other properties are ignored.
/// It does not modify any asset.
/// </summary>
public static class ThaiTextScanner
{
    private const string OutputPath = "Assets/Localize/ThaiTextExport.csv";
    private static readonly string[] Extensions = { ".prefab", ".unity" };
    private static readonly string[] SkipFolders =
    {
        "Assets/Localize/", "Assets/AddressableAssetsData/", "Assets/TextMesh Pro/", "Assets/Editor/Localization/",
        "Assets/Plugins/ThaiTextCare/"   // Thai word dictionary, not game text
    };

    // Unity YAML stores non-ASCII text as \uXXXX, so Thai (U+0E00-U+0E7F) appears as \u0E..
    private static readonly Regex EscapedThai = new Regex(@"\\u0E[0-7][0-9A-Fa-f]");
    private static readonly Regex UnicodeEscape = new Regex(@"\\u([0-9A-Fa-f]{4})");

    [MenuItem("Tools/Localization/1. Scan Thai Text (read-only)")]
    public static void Scan()
    {
        var rows = new List<string[]>();
        string[] files = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories);
        try
        {
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (Array.IndexOf(Extensions, ext) < 0 || ShouldSkip(path)) continue;
                if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("Scanning Thai text", path, i / (float)files.Length))
                    return;
                ScanFile(path, ext, rows);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        var sb = new StringBuilder();
        sb.AppendLine("Source,FileID,Property,Line,Thai,English");
        var unique = new HashSet<string>();
        foreach (string[] row in rows)
        {
            unique.Add(row[4]);
            sb.AppendLine(string.Join(",", Array.ConvertAll(row, Quote)));
        }
        // UTF-8 with BOM so Excel opens Thai correctly.
        File.WriteAllText(OutputPath, sb.ToString(), new UTF8Encoding(true));
        AssetDatabase.Refresh();
        Debug.Log($"[ThaiTextScanner] {rows.Count} Thai strings ({unique.Count} unique) written to {OutputPath}");
        EditorUtility.DisplayDialog("Thai text scan",
            $"Found {rows.Count} Thai strings ({unique.Count} unique).\n\nSaved to {OutputPath}", "OK");
    }

    private static bool ShouldSkip(string path)
    {
        foreach (string folder in SkipFolders)
            if (path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void ScanFile(string path, string ext, List<string[]> rows)
    {
        var info = new FileInfo(path);
        if (info.Length > 20 * 1024 * 1024) return;
        string[] lines;
        try { lines = File.ReadAllLines(path, Encoding.UTF8); }
        catch (Exception) { return; }

        string fileId = "";
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.StartsWith("--- !u!"))
            {
                int amp = line.IndexOf('&');
                if (amp >= 0) fileId = line.Substring(amp + 1).Split(' ')[0];
                continue;
            }

            // Only TextMeshPro labels: lines of the form "m_text: ..." that contain Thai.
            if (!line.TrimStart().StartsWith("m_text:")) continue;
            if (!EscapedThai.IsMatch(line) && !HasThai(line)) continue;
            string value = line.Substring(line.IndexOf(':') + 1).Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[value.Length - 1] == value[0])
                value = value.Substring(1, value.Length - 2);
            rows.Add(new[] { path, fileId, "m_text", (i + 1).ToString(), Unescape(value), "" });
        }
    }

    private static bool HasThai(string s)
    {
        foreach (char c in s)
            if (c >= '฀' && c <= '๿') return true;
        return false;
    }

    private static string Unescape(string s) =>
        UnicodeEscape.Replace(s, m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

    private static string Quote(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
}
