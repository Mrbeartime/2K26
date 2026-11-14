using System;
using UnityEngine;

namespace ProjectD.Audio
{
    public enum AudioBus { Master, Music, SFX, UI }
    public enum AudioCue { Footstep, SwordSlash, BowShot, DoorOpen, ChestOpen, Death, LevelBGM, MenuBGM, ButtonClick, ButtonHover, Pause, CharacterSelect, Lever, TurnChange, ClockTick, SpikeTrap, BellHover, PlayerShatter }

    [Serializable]
    public sealed class AudioCueSlot
    {
        public AudioCue cue;
        public AudioBus bus = AudioBus.SFX;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        public Vector2 volume = new Vector2(.9f, 1f);
        public Vector2 pitch = new Vector2(.95f, 1.05f);
        [Min(1)] public int maxSimultaneous = 3;
        [Min(0f)] public float minimumInterval;
        [NonSerialized] internal float nextPlayTime;
        [Min(.01f)] public float minDistance = 1f;
        [Min(.01f)] public float maxDistance = 20f;

        public AudioCueSlot(AudioCue cue, AudioBus bus)
        {
            this.cue = cue; this.bus = bus;
            if (bus == AudioBus.Music) pitch = Vector2.one;
        }

        public AudioClip PickClip()
        {
            if (clips == null) return null;
            int count = 0;
            for (int i = 0; i < clips.Length; i++) if (clips[i] != null) count++;
            if (count == 0) return null;
            int choice = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < clips.Length; i++) if (clips[i] != null && choice-- == 0) return clips[i];
            return null;
        }
    }
}
