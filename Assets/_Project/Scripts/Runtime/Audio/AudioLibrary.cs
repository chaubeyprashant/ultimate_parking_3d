using UnityEngine;

namespace UltimateCarParking.Audio
{
    /// <summary>Every game sound in one asset (Resources/AudioLibrary). Clips are synthesised by Tools/audio/gen_audio.py.</summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Ultimate Car Parking/Audio Library", order = 30)]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Header("Vehicle")]
        public AudioClip engineLoop;
        public AudioClip brakeSqueal;
        public AudioClip collision;
        public AudioClip collisionWarning;
        public AudioClip horn;
        public AudioClip indicatorTick;
        public AudioClip engineStart;
        public AudioClip tireSqueal;

        [Header("Ambience")]
        public AudioClip ambienceRain;
        public AudioClip ambienceWind;
        public AudioClip ambienceCity;

        [Header("UI and rewards")]
        public AudioClip buttonClick;
        public AudioClip rewardStar;
        public AudioClip rewardCoin;
        public AudioClip levelSuccess;
        public AudioClip levelFail;

        [Header("Music")]
        public AudioClip musicLoop;
        [Range(0f, 1f)] public float gameplayMusicDuck = 0.45f;
    }
}
