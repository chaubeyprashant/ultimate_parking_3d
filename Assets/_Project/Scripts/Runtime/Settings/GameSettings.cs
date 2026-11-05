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

        public void Clamp()
        {
            musicVolume = Mathf.Clamp01(musicVolume);
            soundVolume = Mathf.Clamp01(soundVolume);
            steeringSensitivity = Mathf.Clamp(steeringSensitivity <= 0f ? 1f : steeringSensitivity, MinSensitivity, MaxSensitivity);
        }
    }
}
