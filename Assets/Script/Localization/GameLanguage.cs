using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// The player's chosen language (Thai or English). It is saved, applied to Unity Localization's selected locale,
/// and announced through <see cref="Changed"/> so code that reads text files can reload.
/// </summary>
public static class GameLanguage
{
    public enum Id { Thai, English }

    public static event Action Changed;

    private const string PrefKey = "Settings.Language";
    private const string ThaiCode = "th-TH";
    private const string EnglishCode = "en";
    private static Id? current;
    // Locale changes are ignored until the saved language has been applied once, so Localization's own startup
    // locale choice cannot overwrite the saved language.
    private static bool synced;

    public static Id Current
    {
        get
        {
            if (!current.HasValue)
            {
                // No saved choice yet: follow the system language, English for anything that is not Thai.
                int saved = PlayerPrefs.GetInt(PrefKey, -1);
                current = saved == (int)Id.Thai || saved == (int)Id.English
                    ? (Id)saved
                    : Application.systemLanguage == SystemLanguage.Thai ? Id.Thai : Id.English;
            }
            return current.Value;
        }
    }

    public static bool IsEnglish => Current == Id.English;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        current = null;
        Changed = null;
        synced = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySaved()
    {
        // Unity Localization is the other way to change language (locale dropdown, Game View selector, other scripts),
        // so the text files follow it too.
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        ApplyLocale();
    }

    private static void OnLocaleChanged(Locale locale)
    {
        if (!synced || locale == null) return;
        Id language = locale.Identifier.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? Id.English : Id.Thai;
        if (current.HasValue && current.Value == language) return;
        current = language;
        PlayerPrefs.SetInt(PrefKey, (int)language);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void Set(Id language)
    {
        if (current.HasValue && current.Value == language) return;
        current = language;
        PlayerPrefs.SetInt(PrefKey, (int)language);
        PlayerPrefs.Save();
        ApplyLocale();
        Changed?.Invoke();
    }

    public static void Toggle() => Set(IsEnglish ? Id.Thai : Id.English);

    /// <summary>For text written directly in code: returns the Thai or English version.</summary>
    public static string Pick(string thai, string english) => IsEnglish ? english : thai;

    /// <summary>
    /// Returns the English version of a Resources text file (name + "_en") when English is selected and that file
    /// exists. Otherwise returns the original Thai file.
    /// </summary>
    public static TextAsset LoadVariant(TextAsset thai)
    {
        if (thai == null) return null;
        if (IsEnglish)
        {
            var english = Resources.Load<TextAsset>(thai.name + "_en");
            if (english != null) return english;
        }
        return thai;
    }

    /// <summary>
    /// A per-language companion of a Resources text file: name + "_en" for English, name + "_th" for Thai.
    /// Returns null when that file does not exist.
    /// </summary>
    public static TextAsset LoadOverlay(TextAsset baseAsset) =>
        baseAsset == null ? null : Resources.Load<TextAsset>(baseAsset.name + (IsEnglish ? "_en" : "_th"));

    private static void ApplyLocale()
    {
        var init = LocalizationSettings.InitializationOperation;
        if (init.IsDone) SelectLocale();
        else init.Completed += _ => SelectLocale();
    }

    private static void SelectLocale()
    {
        // Read Current again: the player may have switched while Localization was still starting.
        var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(IsEnglish ? EnglishCode : ThaiCode));
        synced = true;
        if (locale != null) LocalizationSettings.SelectedLocale = locale;
    }
}
