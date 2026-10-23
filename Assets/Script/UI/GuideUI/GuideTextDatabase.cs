using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class GuideTextDatabase
{
    private static Dictionary<string, string> texts;

    public static string Get(string key)
    {
        if (texts == null) Load();
        if (texts.TryGetValue(key, out string value)) return value;
        Debug.LogWarning($"[GuideText] Key not found: {key}");
        return null;
    }

    public static void Reload() => texts = null;

    private static void Load()
    {
        texts = new Dictionary<string, string>();
        TextAsset file = Resources.Load<TextAsset>("GuideText");   // Resources/GuideText.txt
        if (file == null)
        {
            Debug.LogError("[GuideText] Resources/GuideText.txt not found");
            return;
        }

        string key = null;
        StringBuilder body = new StringBuilder();

        void Flush()
        {
            if (key != null) texts[key] = body.ToString().TrimEnd('\n');
            body.Clear();
        }

        foreach (string raw in file.text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith("//")) continue;

            if (line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']')
            {
                Flush();
                key = line.Substring(1, line.Length - 2).Trim();
            }
            else if (key != null)
            {
                body.Append(line).Append('\n');
            }
        }
        Flush();
    }
}