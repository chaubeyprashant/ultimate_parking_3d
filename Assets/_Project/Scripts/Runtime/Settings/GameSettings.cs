using System;
using UnityEngine;

namespace UltimateCarParking.Settings
{
    /// <summary>Player options, stored inside the save file (SaveData version 2+).</summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const float MinSensitivity = 0.5f, MaxSensitivity = 2f;

        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [Range(0f, 1f)] public float soundVolume = 0.9f;
        [Tooltip("Steering button ramp speed multiplier (0.5 = gentle, 2 = twitchy).")]
        [Range(MinSensitivity, MaxSensitivity)] public float steeringSensitivity = 1f;

        [Tooltip("0 Auto, 1 Low, 2 Medium, 3 High (see QualityModes).")]
        public int graphicsMode;
        [Tooltip("0 Auto, 1 = 30, 2 = 60, 3 = Max (see QualityModes).")]
        public int frameRateMode;

        public void Clamp()
        {
            graphicsMode = Mathf.Clamp(graphicsMode, 0, 3);
            frameRateMode = Mathf.Clamp(frameRateMode, 0, 3);
            musicVolume = Mathf.Clamp01(musicVolume);
            soundVolume = Mathf.Clamp01(soundVolume);
            steeringSensitivity = Mathf.Clamp(steeringSensitivity <= 0f ? 1f : steeringSensitivity, MinSensitivity, MaxSensitivity);
        }
    }
}
