using ProjectD.Menus;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioSettingsUI : MonoBehaviour
    {
        [Tooltip("Root containing only Settings UI buttons. Reassign when replacing the UI prefab.")]
        [SerializeField] private GameObject settingsAudioRoot;
        [SerializeField] private Slider masterVolume, musicVolume, sfxVolume, uiVolume;
        [SerializeField] private Toggle muteAll;
        [Tooltip("Existing Sound enabled toggle; checked means sound ON, inverse of Mute All.")]
        [SerializeField] private Toggle soundEnabled;
        [SerializeField] private Button resetDefaults;
        private void OnEnable()
        {
            ButtonAudioFeedback.BindMenu(settingsAudioRoot != null ? settingsAudioRoot : gameObject);
            Bind(masterVolume, SetMaster); Bind(musicVolume, SetMusic); Bind(sfxVolume, SetSFX);
            Bind(uiVolume, SetUI);
            if (muteAll != null) muteAll.onValueChanged.AddListener(SetMuted);
            if (soundEnabled != null) soundEnabled.onValueChanged.AddListener(SetSound);
            if (resetDefaults != null) resetDefaults.onClick.AddListener(ResetDefaults);
            GameSettings.AudioChanged += Refresh;
            Refresh();
        }
        private static void Bind(Slider slider, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider == null) return;
            slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(callback);
        }
        private void OnDisable()
        {
            if (masterVolume != null) masterVolume.onValueChanged.RemoveListener(SetMaster);
            if (musicVolume != null) musicVolume.onValueChanged.RemoveListener(SetMusic);
            if (sfxVolume != null) sfxVolume.onValueChanged.RemoveListener(SetSFX);
            if (uiVolume != null) uiVolume.onValueChanged.RemoveListener(SetUI);
            if (muteAll != null) muteAll.onValueChanged.RemoveListener(SetMuted);
            if (soundEnabled != null) soundEnabled.onValueChanged.RemoveListener(SetSound);
            if (resetDefaults != null) resetDefaults.onClick.RemoveListener(ResetDefaults);
            GameSettings.AudioChanged -= Refresh;
            GameSettings.SaveAudioSettings();
        }
        private void Refresh()
        {
            if (masterVolume != null) masterVolume.SetValueWithoutNotify(GameSettings.GetVolume(AudioBus.Master));
            if (musicVolume != null) musicVolume.SetValueWithoutNotify(GameSettings.GetVolume(AudioBus.Music));
            if (sfxVolume != null) sfxVolume.SetValueWithoutNotify(GameSettings.GetVolume(AudioBus.SFX));
            if (uiVolume != null) uiVolume.SetValueWithoutNotify(GameSettings.GetVolume(AudioBus.UI));
            if (muteAll != null) muteAll.SetIsOnWithoutNotify(!GameSettings.SoundEnabled);
            if (soundEnabled != null) soundEnabled.SetIsOnWithoutNotify(GameSettings.SoundEnabled);
        }
        public void SetMaster(float value) => GameSettings.SetVolume(AudioBus.Master, value);
        public void SetMusic(float value) => GameSettings.SetVolume(AudioBus.Music, value);
        public void SetSFX(float value) => GameSettings.SetVolume(AudioBus.SFX, value);
        public void SetUI(float value) => GameSettings.SetVolume(AudioBus.UI, value);
        public void SetMuted(bool muted) => GameSettings.SetSound(!muted);
        public void SetSound(bool enabled) => GameSettings.SetSound(enabled);
        public void ResetDefaults() => GameSettings.ResetAudioDefaults();
    }
}
