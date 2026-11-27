using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class GuideTextDatabase
{
    // One dictionary per language so switching language never needs a re-parse.
    private static readonly Dictionary<GameLanguage.Id, Dictionary<string, string>> cache = new();

    public static string Get(string key)
    {
        if (TextsFor(GameLanguage.Current).TryGetValue(key, out string value)) return value;
        // A key that is not translated yet falls back to Thai instead of showing nothing.
        if (GameLanguage.Current != GameLanguage.Id.Thai &&
            TextsFor(GameLanguage.Id.Thai).TryGetValue(key, out value)) return value;
        Debug.LogWarning($"[GuideText] Key not found: {key}");
        return null;
    }

    public static void Reload() => cache.Clear();

    private static Dictionary<string, string> TextsFor(GameLanguage.Id language)
    {
        if (!cache.TryGetValue(language, out var texts))
            cache[language] = texts = Load(language);
        return texts;
    }

    private static Dictionary<string, string> Load(GameLanguage.Id language)
    {
        var texts = new Dictionary<string, string>();
        // Resources/GuideText.txt is Thai, Resources/GuideText_en.txt is English (same [key] sections).
        bool english = language == GameLanguage.Id.English;
        string fileName = english ? "GuideText_en" : "GuideText";
        TextAsset file = Resources.Load<TextAsset>(fileName);
        if (file == null)
        {
            if (english) Debug.LogWarning("[GuideText] Resources/GuideText_en.txt not found, using Thai text");
            else Debug.LogError("[GuideText] Resources/GuideText.txt not found");
            return texts;
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
        return texts;
    }
}
