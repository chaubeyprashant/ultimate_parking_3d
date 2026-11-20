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
        public static void SetGraphicsMode(int v) => Apply(s => s.graphicsMode = v);
        public static void SetFrameRateMode(int v) => Apply(s => s.frameRateMode = v);
        public static void SetSteeringMode(int v) => Apply(s => s.steeringMode = v);
        public static void SetPedalMode(int v) => Apply(s => s.pedalMode = v);
        public static void SetControlScale(float v) => Apply(s => s.controlScale = v);
        public static void SetControlOpacity(float v) => Apply(s => s.controlOpacity = v);
        public static void SetControlsRaise(float v) => Apply(s => s.controlsRaise = v);
        public static void SetLeftHanded(bool v) => Apply(s => s.leftHanded = v);
        public static void SetHudScale(float v) => Apply(s => s.hudScale = v);
        public static void SetTiltZero(float v) => Apply(s => s.tiltZero = v);
        public static void ResetControls() => Apply(s => s.ResetControls());
        public static void SetCameraView(int v) => Apply(s => s.cameraView = v);
        public static void SetChaseDistance(float v) => Apply(s => s.chaseDistance = v);
        public static void SetAssistMode(int v) => Apply(s => s.assistMode = v);
        public static void SetAssistSounds(bool v) => Apply(s => s.assistSounds = v);
        public static void SetPlayDifficulty(int v) => Apply(s => s.playDifficulty = v);

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
