using System;
using UltimateCarParking.Save;

namespace UltimateCarParking.Settings
{
    /// <summary>Reads and writes the player's settings in the save file and notifies listeners (audio, controls).</summary>
    public static class SettingsService
    {
        public static event Action<GameSettings> Changed;

        public static GameSettings Current => SaveManager.Current.Data.settings;

        public static void SetMusicVolume(float v) => Apply(s => s.musicVolume = v);
        public static void SetSoundVolume(float v) => Apply(s => s.soundVolume = v);
        public static void SetSteeringSensitivity(float v) => Apply(s => s.steeringSensitivity = v);

        /// <summary>Writes the save file (call when a settings screen closes, not on every slider tick).</summary>
        public static void Commit() => SaveManager.Current.Save();

        private static void Apply(Action<GameSettings> change)
        {
            GameSettings s = Current;
            change(s);
            s.Clamp();
            Changed?.Invoke(s);
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Changed = null;
    }
}
