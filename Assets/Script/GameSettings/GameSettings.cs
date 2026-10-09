using UnityEngine;

namespace ProjectD.Menus
{
    public static class GameSettings
    {
        public static bool SoundEnabled => PlayerPrefs.GetInt("Settings.Sound", 1) != 0;
        public static bool FullscreenEnabled => PlayerPrefs.GetInt("Settings.Fullscreen", Screen.fullScreen ? 1 : 0) != 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedSettings()
        {
            AudioListener.volume = SoundEnabled ? 1f : 0f;
            if (PlayerPrefs.HasKey("Settings.Fullscreen")) Screen.fullScreen = FullscreenEnabled;
        }

        public static void SetSound(bool enabled)
        {
            AudioListener.volume = enabled ? 1f : 0f;
            PlayerPrefs.SetInt("Settings.Sound", enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetFullscreen(bool enabled)
        {
            Screen.fullScreen = enabled;
            PlayerPrefs.SetInt("Settings.Fullscreen", enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
