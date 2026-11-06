using System.Collections;
using ProjectD.Menus;
using UnityEngine;
using UnityEngine.Audio;

namespace ProjectD.Audio
{
    [DisallowMultipleComponent]
    public sealed class GameAudioManager : MonoBehaviour
    {
        public static GameAudioManager Instance { get; private set; }
        [Header("Mixer routing: Master, Music, SFX, UI, Ambient")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup[] groups = new AudioMixerGroup[5];
        [Header("Default volume percentages")]
        [SerializeField] private float[] defaultVolumes = { 100f, 80f, 100f, 100f, 70f };
        [SerializeField, Range(1, 64)] private int poolSize = 24;
        [SerializeField, Min(0f)] private float musicFadeSeconds = .8f;
        [SerializeField] private AudioCueSlot[] cues = {
            new(AudioCue.Footstep, AudioBus.SFX), new(AudioCue.SwordSlash, AudioBus.SFX),
            new(AudioCue.BowShot, AudioBus.SFX), new(AudioCue.DoorOpen, AudioBus.SFX),
            new(AudioCue.ChestOpen, AudioBus.SFX), new(AudioCue.Death, AudioBus.SFX),
            new(AudioCue.LevelBGM, AudioBus.Music), new(AudioCue.MenuBGM, AudioBus.Music),
            new(AudioCue.ButtonClick, AudioBus.UI), new(AudioCue.ButtonHover, AudioBus.UI),
            new(AudioCue.Pause, AudioBus.UI), new(AudioCue.CharacterSelect, AudioBus.UI),
            new(AudioCue.Ambient, AudioBus.Ambient) };
        private static readonly string[] Parameters = { "MasterVolume", "MusicVolume", "SFXVolume", "UIVolume", "AmbientVolume" };
        private struct Voice { public AudioSource source; public AudioCue cue; public AudioBus bus; }
        private Voice[] voices;
        private AudioSource[] music;
        private AudioSource ambient;
        private Coroutine fade;
        private bool started;
        public int PoolCapacity => voices == null ? 0 : voices.Length;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var prefab = Resources.Load<GameAudioManager>("GameAudioManager");
            if (prefab != null) Instantiate(prefab);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            GameSettings.ConfigureAudioDefaults(defaultVolumes);
            voices = new Voice[Mathf.Clamp(poolSize, 1, 64)];
            for (int i = 0; i < voices.Length; i++) voices[i].source = CreateSource("SFX Voice " + i, AudioBus.SFX);
            music = new[] { CreateSource("Music A", AudioBus.Music), CreateSource("Music B", AudioBus.Music) };
            ambient = CreateSource("Ambient", AudioBus.Ambient);
            GameSettings.AudioChanged += ApplySettings;
        }

        private void Start() { started = true; ApplySettings(); }
        private AudioSource CreateSource(string label, AudioBus bus)
        {
            var child = new GameObject(label); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.outputAudioMixerGroup = Group(bus); source.dopplerLevel = 0;
            return source;
        }
        private AudioMixerGroup Group(AudioBus bus) => groups != null && groups.Length > (int)bus ? groups[(int)bus] : null;
        private AudioCueSlot Find(AudioCue cue)
        {
            if (cues != null) for (int i = 0; i < cues.Length; i++) if (cues[i] != null && cues[i].cue == cue) return cues[i];
            return null;
        }
        private bool Muted(AudioBus bus) => !GameSettings.SoundEnabled || GameSettings.GetVolume(AudioBus.Master) <= 0 || GameSettings.GetVolume(bus) <= 0;
        public static float PercentToDecibels(float percent) => !float.IsFinite(percent) || percent <= 0f ? -80f : Mathf.Clamp(20f * Mathf.Log10(Mathf.Clamp(percent, .0001f, 100f) / 100f), -80f, 0f);
        private void ApplySettings()
        {
            if (!started || Instance != this) return;
            if (mixer != null) for (int i = 0; i < Parameters.Length; i++) mixer.SetFloat(Parameters[i], PercentToDecibels(GameSettings.GetVolume((AudioBus)i)));
            for (int i = 0; i < voices.Length; i++) voices[i].source.mute = Muted(voices[i].bus);
            for (int i = 0; i < music.Length; i++) music[i].mute = Muted(AudioBus.Music);
            ambient.mute = Muted(AudioBus.Ambient);
        }
        private void Configure(AudioSource source, AudioCueSlot slot, AudioClip clip, Vector3? position, bool loop)
        {
            source.Stop(); source.clip = clip; source.loop = loop;
            source.outputAudioMixerGroup = Group(slot.bus);
            source.volume = Mathf.Clamp01(Random.Range(Mathf.Min(slot.volume.x, slot.volume.y), Mathf.Max(slot.volume.x, slot.volume.y)));
            source.pitch = Mathf.Clamp(Random.Range(Mathf.Min(slot.pitch.x, slot.pitch.y), Mathf.Max(slot.pitch.x, slot.pitch.y)), .1f, 3f);
            source.spatialBlend = position.HasValue ? 1f : 0f;
            source.transform.position = position.GetValueOrDefault(transform.position);
            source.minDistance = Mathf.Max(.01f, slot.minDistance);
            source.maxDistance = Mathf.Max(source.minDistance, slot.maxDistance);
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.ignoreListenerPause = slot.bus == AudioBus.UI;
            source.mute = Muted(slot.bus);
        }
        public bool PlaySFX(AudioCue cue) => PlayPooled(cue, AudioBus.SFX, null);
        public bool PlaySFXAtPosition(AudioCue cue, Vector3 position) => PlayPooled(cue, AudioBus.SFX, position);
        public bool PlayUI(AudioCue cue) => PlayPooled(cue, AudioBus.UI, null);
        private bool PlayPooled(AudioCue cue, AudioBus bus, Vector3? position)
        {
            if (Instance != this || voices == null) return false;
            var slot = Find(cue);
            if (slot == null || slot.bus != bus) return false;
            var clip = slot.PickClip(); if (clip == null) return false;
            int free = -1, overlapping = 0;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i].source.isPlaying) { if (free < 0) free = i; }
                else if (voices[i].cue == cue) overlapping++;
            }
            if (free < 0 || overlapping >= Mathf.Max(1, slot.maxSimultaneous)) return false;
            voices[free].cue = cue; voices[free].bus = bus;
            Configure(voices[free].source, slot, clip, position, false);
            voices[free].source.Play(); return true;
        }
        public bool PlayMusic(AudioCue cue)
        {
            if (Instance != this || music == null) return false;
            var slot = Find(cue); if (slot == null || slot.bus != AudioBus.Music) return false;
            var clip = slot.PickClip(); if (clip == null) return false;
            if (fade != null) StopCoroutine(fade);
            int old = music[0].isPlaying && (!music[1].isPlaying || music[0].volume >= music[1].volume) ? 0 : 1;
            var next = music[1 - old];
            Configure(next, slot, clip, null, true);
            float target = next.volume; next.volume = 0; next.Play();
            fade = StartCoroutine(FadeMusic(music[old], next, target)); return true;
        }
        public void StopMusic()
        {
            if (Instance != this || music == null) return;
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeMusic(music[0], music[1], 0f));
        }
        private IEnumerator FadeMusic(AudioSource old, AudioSource next, float target)
        {
            float from = old.volume, nextFrom = next.volume, elapsed = 0f;
            float duration = Mathf.Max(0f, musicFadeSeconds);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                old.volume = Mathf.Lerp(from, 0f, t); next.volume = Mathf.Lerp(nextFrom, target, t);
                yield return null;
            }
            old.Stop(); old.clip = null; old.volume = 0;
            next.volume = target;
            if (target <= 0) { next.Stop(); next.clip = null; }
            fade = null;
        }
        public bool PlayAmbient(AudioCue cue, Vector3? position = null)
        {
            if (Instance != this || ambient == null) return false;
            var slot = Find(cue); if (slot == null || slot.bus != AudioBus.Ambient) return false;
            var clip = slot.PickClip(); if (clip == null) return false;
            Configure(ambient, slot, clip, position, true); ambient.Play(); return true;
        }
        public void StopAmbient() { if (ambient != null) { ambient.Stop(); ambient.clip = null; } }
        private void OnApplicationPause(bool paused) { if (paused && Instance == this) GameSettings.SaveAudioSettings(); }
        private void OnApplicationQuit() { if (Instance == this) GameSettings.SaveAudioSettings(); }
        private void OnDestroy()
        {
            if (Instance != this) return;
            GameSettings.AudioChanged -= ApplySettings; Instance = null;
        }
    }
}
