using UnityEditor;
using UnityEngine;

/// <summary>Sets the saved game language from the Editor so you can test English without the Settings toggle.</summary>
public static class LanguageTestMenu
{
    private const string PrefKey = "Settings.Language"; // same key as GameLanguage; 0 = Thai, 1 = English

    [MenuItem("Tools/Localization/Language/Use English on next Play")]
    private static void English() => Set(1, "English");

    [MenuItem("Tools/Localization/Language/Use Thai on next Play")]
    private static void Thai() => Set(0, "Thai");

    [MenuItem("Tools/Localization/Language/Forget saved language (follow system)")]
    private static void Forget()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
        Debug.Log("[Language] Saved language cleared. The game follows the system language again.");
    }

    private static void Set(int value, string name)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[Language] Stop Play mode first. Use the in-game toggle while playing.");
            return;
        }
        PlayerPrefs.SetInt(PrefKey, value);
        PlayerPrefs.Save();
        Debug.Log($"[Language] Saved language = {name}. Press Play to test.");
    }
}
