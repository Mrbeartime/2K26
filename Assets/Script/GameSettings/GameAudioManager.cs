using System.Collections;
using ProjectD.Menus;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectD.Audio
{
    [DisallowMultipleComponent]
    public sealed class GameAudioManager : MonoBehaviour
    {
        public static GameAudioManager Instance { get; private set; }
        [Header("Mixer routing: Master, Music, SFX, UI")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup[] groups = new AudioMixerGroup[4];
        [Header("Default volume percentages")]
        [SerializeField] private float[] defaultVolumes = { 100f, 80f, 100f, 100f };
        [SerializeField, Range(1, 64)] private int poolSize = 24;
        [SerializeField, Range(1, 64)] private int hoverVoiceLimit = 4;
        [SerializeField, Min(0f)] private float musicFadeSeconds = .8f;
        [SerializeField] private AudioCueSlot[] cues = {
            new(AudioCue.Footstep, AudioBus.SFX), new(AudioCue.SwordSlash, AudioBus.SFX),
            new(AudioCue.BowShot, AudioBus.SFX), new(AudioCue.DoorOpen, AudioBus.SFX),
            new(AudioCue.ChestOpen, AudioBus.SFX), new(AudioCue.Death, AudioBus.SFX),
            new(AudioCue.LevelBGM, AudioBus.Music), new(AudioCue.MenuBGM, AudioBus.Music),
            new(AudioCue.ButtonClick, AudioBus.UI), new(AudioCue.ButtonHover, AudioBus.UI),
            new(AudioCue.Pause, AudioBus.UI), new(AudioCue.CharacterSelect, AudioBus.UI)};
        private static readonly string[] Parameters = { "MasterVolume", "MusicVolume", "SFXVolume", "UIVolume" };
        private struct Voice { public AudioSource source; public AudioCue cue; public AudioBus bus; public Object owner; public bool hover; }
        private Voice[] voices;
        private AudioSource[] music;
        private Coroutine fade;
        private bool started;
        private AudioCue? currentMusic;
        private int specialUIFrame = -1;
        private int buttonPressFrame = -1;
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
            GameSettings.AudioChanged += ApplySettings;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool level = false, menu = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                level |= root.GetComponentInChildren<TurnGameManager>(true) != null;
                menu |= root.GetComponentInChildren<ProjectD.Menus.MenuScreen>(true) != null;
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                    if (button.GetComponent<ButtonAudioFeedback>() == null) button.gameObject.AddComponent<ButtonAudioFeedback>();
            }
            // Settings and other additive overlays inherit the current score.
            if (mode == LoadSceneMode.Additive && currentMusic.HasValue) return;
            if (level) PlayMusic(AudioCue.LevelBGM);
            else if (menu) PlayMusic(AudioCue.MenuBGM);
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
        }
        private void Configure(AudioSource source, AudioCueSlot slot, AudioClip clip, Vector3? position, bool loop)
        {
            source.Stop(); source.clip = clip; source.loop = loop;
            source.outputAudioMixerGroup = Group(slot.bus);
            source.volume = Mathf.Clamp01(slot.cue == AudioCue.Footstep ? slot.volume.x :
                Random.Range(Mathf.Min(slot.volume.x, slot.volume.y), Mathf.Max(slot.volume.x, slot.volume.y)));
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
        public bool PlayUI(AudioCue cue)
        {
            if (cue == AudioCue.Pause && buttonPressFrame == Time.frameCount) return false;
            if (cue == AudioCue.ButtonClick && specialUIFrame == Time.frameCount) return false;
            if (cue == AudioCue.Pause || cue == AudioCue.CharacterSelect)
            {
                if (specialUIFrame == Time.frameCount) return false;
                specialUIFrame = Time.frameCount;
            }
            return PlayPooled(cue, AudioBus.UI, null);
        }
        public bool PlayFootstep(Object owner, Vector3 position)
        {
            if (owner == null || voices == null) return false;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i].owner == owner && voices[i].cue == AudioCue.Footstep && voices[i].source.isPlaying) return false;
            return PlayPooled(AudioCue.Footstep, AudioBus.SFX, position, owner);
        }
        public void StopFootstep(Object owner)
        {
            if (owner == null || voices == null) return;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i].owner == owner && voices[i].cue == AudioCue.Footstep)
                { voices[i].source.Stop(); voices[i].owner = null; }
        }
        public bool PlayLevelHover() => PlayPooled(AudioCue.Pause, AudioBus.UI, null, null, true);
        public bool PlayLevelClick()
        {
            // Press feedback must never cut a previously started hover/open sound.
            buttonPressFrame = Time.frameCount;
            return PlayPooled(AudioCue.ButtonClick, AudioBus.UI, null);
        }
        private bool PlayPooled(AudioCue cue, AudioBus bus, Vector3? position, Object owner = null, bool hover = false)
        {
            if (Instance != this || voices == null) return false;
            var slot = Find(cue);
            if (slot == null || slot.bus != bus) return false;
            hover |= cue == AudioCue.ButtonHover && bus == AudioBus.UI;
            if (!hover && Time.unscaledTime < slot.nextPlayTime) return false;
            var clip = slot.PickClip(); if (clip == null) return false;
            int free = -1, overlapping = 0;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i].source.isPlaying) { if (free < 0) free = i; }
                else if (hover ? voices[i].hover : !voices[i].hover && voices[i].cue == cue)
                    overlapping++;
            }
            int limit = hover ? Mathf.Clamp(hoverVoiceLimit, 1, voices.Length) : Mathf.Max(1, slot.maxSimultaneous);
            if (free < 0 || overlapping >= limit) return false;
            voices[free].cue = cue; voices[free].bus = bus;
            voices[free].owner = owner;
            voices[free].hover = hover;
            Configure(voices[free].source, slot, clip, position, cue == AudioCue.Footstep && owner != null);
            voices[free].source.Play();
            if (!hover) slot.nextPlayTime = Time.unscaledTime + Mathf.Max(0f, slot.minimumInterval);
            return true;
        }
        public bool PlayMusic(AudioCue cue)
        {
            if (Instance != this || music == null) return false;
            if (currentMusic == cue) return true;
            var slot = Find(cue); if (slot == null || slot.bus != AudioBus.Music) return false;
            var clip = slot.PickClip(); if (clip == null) return false;
            currentMusic = cue;
            if (fade != null) StopCoroutine(fade);
            int old = music[0].isPlaying && (!music[1].isPlaying || music[0].volume >= music[1].volume) ? 0 : 1;
            var next = music[1 - old];
            Configure(next, slot, clip, null, true);
            float target = next.volume; next.volume = 0; next.Play();
            fade = StartCoroutine(FadeMusic(music[old], next, target)); return true;
        }
        public void StopMusic()
        {
            currentMusic = null;
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
        private void OnApplicationPause(bool paused) { if (paused && Instance == this) GameSettings.SaveAudioSettings(); }
        private void OnApplicationQuit() { if (Instance == this) GameSettings.SaveAudioSettings(); }
        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameSettings.AudioChanged -= ApplySettings; Instance = null;
        }
    }
}
