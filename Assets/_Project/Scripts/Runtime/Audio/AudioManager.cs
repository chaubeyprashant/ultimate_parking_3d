using UnityEngine;
using UltimateCarParking.Settings;

namespace UltimateCarParking.Audio
{
    public enum Sfx { Click, Star, Coin, Success, Fail, Collision, CollisionWarning, Brake }

    /// <summary>
    /// App-wide 2D audio: one music source and a small pool of one-shot sources, volumes from the player's settings.
    /// Created on first use, kept across scenes (exactly one). No per-frame work.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int PoolSize = 6;
        private static AudioManager instance;

        private AudioLibrary library;
        private AudioSource music;
        private AudioSource[] pool;
        private int next;
        private float musicDuck = 1f;

        public static AudioManager Instance
        {
            get
            {
                if (instance == null) Create();
                return instance;
            }
        }

        public AudioLibrary Library => library;
        public float SoundVolume => SettingsService.Current.soundVolume;
        public AudioSource MusicSource => music;

        /// <summary>Number of one-shots started (for tests and diagnostics).</summary>
        public int PlayedCount { get; private set; }
        public Sfx? LastPlayed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private static void Create()
        {
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AudioManager>();
            instance.library = Resources.Load<AudioLibrary>("AudioLibrary");
            if (instance.library == null) Debug.LogError("[Audio] Resources/AudioLibrary not found; the game will be silent.");
            instance.music = go.AddComponent<AudioSource>();
            instance.music.loop = true;
            instance.music.playOnAwake = false;
            instance.music.ignoreListenerPause = true;
            instance.pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                instance.pool[i] = go.AddComponent<AudioSource>();
                instance.pool[i].playOnAwake = false;
            }
            SettingsService.Changed += instance.OnSettingsChanged;
            instance.ApplyVolumes();
        }

        private void OnDestroy()
        {
            SettingsService.Changed -= OnSettingsChanged;
            if (instance == this) instance = null;
        }

        private void OnSettingsChanged(GameSettings s) => ApplyVolumes();

        private void ApplyVolumes()
        {
            if (music != null) music.volume = SettingsService.Current.musicVolume * musicDuck;
        }

        /// <summary>Starts the music loop (no-op if already playing). Gameplay plays it ducked under the engine.</summary>
        public void PlayMusic(bool ducked)
        {
            musicDuck = ducked && library != null ? library.gameplayMusicDuck : 1f;
            ApplyVolumes();
            if (library == null || library.musicLoop == null || music.isPlaying) return;
            music.clip = library.musicLoop;
            music.Play();
        }

        public void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
        {
            AudioClip clip = ClipFor(sfx);
            if (clip == null) return;
            AudioSource src = pool[next];
            next = (next + 1) % pool.Length;
            src.ignoreListenerPause = sfx == Sfx.Click || sfx == Sfx.Star || sfx == Sfx.Coin;   // menus work while paused
            src.pitch = pitch;
            src.PlayOneShot(clip, Mathf.Clamp01(volume) * SoundVolume);
            PlayedCount++;
            LastPlayed = sfx;
        }

        public static void Click()
        {
            if (Application.isPlaying) Instance.Play(Sfx.Click, 0.6f);
        }

        private AudioClip ClipFor(Sfx sfx)
        {
            if (library == null) return null;
            return sfx switch
            {
                Sfx.Click => library.buttonClick,
                Sfx.Star => library.rewardStar,
                Sfx.Coin => library.rewardCoin,
                Sfx.Success => library.levelSuccess,
                Sfx.Fail => library.levelFail,
                Sfx.Collision => library.collision,
                Sfx.CollisionWarning => library.collisionWarning,
                Sfx.Brake => library.brakeSqueal,
                _ => null,
            };
        }
    }
}
