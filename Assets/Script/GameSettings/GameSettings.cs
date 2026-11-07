using UnityEngine;
using ProjectD.Audio;

namespace ProjectD.Menus
{
    public static class GameSettings
    {
        public static event System.Action AudioChanged;
        private static readonly string[] VolumeKeys = { "Settings.Audio.Master", "Settings.Audio.Music", "Settings.Audio.SFX", "Settings.Audio.UI" };
        private static float[] audioDefaults = { 100f, 80f, 100f, 100f };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAudioState()
        {
            AudioChanged = null;
            audioDefaults = new[] { 100f, 80f, 100f, 100f };
        }

        public static void ConfigureAudioDefaults(float[] values)
        {
            if (values == null || values.Length != VolumeKeys.Length) return;
            for (int i = 0; i < values.Length; i++) audioDefaults[i] = SafePercent(values[i]);
        }

        private static float SafePercent(float value) => float.IsFinite(value) ? Mathf.Clamp(value, 0f, 100f) : 0f;
        public static float GetVolume(AudioBus bus) => SafePercent(PlayerPrefs.GetFloat(VolumeKeys[(int)bus], audioDefaults[(int)bus]));
        public static void SetVolume(AudioBus bus, float percent)
        {
            PlayerPrefs.SetFloat(VolumeKeys[(int)bus], SafePercent(percent));
            AudioChanged?.Invoke();
        }
        public static void SaveAudioSettings() => PlayerPrefs.Save();
        public static void ResetAudioDefaults()
        {
            for (int i = 0; i < VolumeKeys.Length; i++) PlayerPrefs.SetFloat(VolumeKeys[i], audioDefaults[i]);
            SetSound(true);
        }
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
            AudioChanged?.Invoke();
        }

        public static void SetFullscreen(bool enabled)
        {
            Screen.fullScreen = enabled;
            PlayerPrefs.SetInt("Settings.Fullscreen", enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
