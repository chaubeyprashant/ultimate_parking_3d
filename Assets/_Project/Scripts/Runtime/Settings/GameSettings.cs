using System;
using UnityEngine;

namespace UltimateCarParking.Settings
{
    /// <summary>Player options, stored inside the save file (SaveData version 2+).</summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const float MinSensitivity = 0.5f, MaxSensitivity = 2f;
        public const float MinControlScale = 0.75f, MaxControlScale = 1.3f;
        public const float MinControlOpacity = 0.35f;
        public const float MinHudScale = 0.85f, MaxHudScale = 1.15f;
        public const int SteerButtons = 0, SteerWheel = 1, SteerTilt = 2;
        // Classic is 0 so saves from before this option (and new installs) get the simple pedals.
        public const int PedalsClassic = 0, PedalsGears = 1;
        public const int AssistBeginner = 0, AssistStandard = 1, AssistExpert = 2;
        public const float MinChaseDistance = 4f, MaxChaseDistance = 14f;

        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [Range(0f, 1f)] public float soundVolume = 0.9f;
        [Tooltip("Steering speed multiplier for every steering mode (0.5 = gentle, 2 = quick).")]
        [Range(MinSensitivity, MaxSensitivity)] public float steeringSensitivity = 1f;

        [Tooltip("0 Auto, 1 Low, 2 Medium, 3 High (see QualityModes).")]
        public int graphicsMode;
        [Tooltip("0 Auto, 1 = 30, 2 = 60, 3 = Max (see QualityModes).")]
        public int frameRateMode;

        [Header("Controls")]
        [Tooltip("0 Left/Right buttons, 1 steering wheel, 2 tilt. Saves made before this option default to the wheel.")]
        public int steeringMode = SteerWheel;
        [Tooltip("0 Classic (GAS + BRAKE/REVERSE, the default: simplest for first-time players), 1 Gears (separate brake + P/R/D selector).")]
        public int pedalMode = PedalsClassic;
        [Range(MinControlScale, MaxControlScale)] public float controlScale = 1f;
        [Range(MinControlOpacity, 1f)] public float controlOpacity = 0.9f;
        [Tooltip("0 = lowest, 1 = highest placement of the touch controls.")]
        [Range(0f, 1f)] public float controlsRaise;
        [Tooltip("Steering on the right, pedals on the left.")]
        public bool leftHanded;
        [Tooltip("Size of the HUD panels (speed, timer, hits, objectives, camera buttons).")]
        [Range(MinHudScale, MaxHudScale)] public float hudScale = 1f;
        [Tooltip("Tilt steering: phone roll (degrees) that counts as straight ahead.")]
        public float tiltZero;

        [Header("Camera and parking assistance")]
        [Tooltip("0 Chase, 1 Top-down, 2 Bumper, 3 Cockpit, 4 Orbit (CameraView). Remembered between sessions.")]
        public int cameraView;
        [Tooltip("Chase / orbit camera distance (m), 4–14. Pinch or the ZOOM button change it.")]
        public float chaseDistance = 7f;
        [Tooltip("0 Beginner (sensors, beeps, guides, alignment), 1 Standard (sensors, beeps, guides), 2 Expert (none).")]
        public int assistMode = AssistStandard;
        [Tooltip("Parking sensor beeps.")]
        public bool assistSounds = true;
        [Tooltip("Mission difficulty last picked on a mission card: 0 Easy, 1 Medium (as designed), 2 Hard.")]
        public int playDifficulty = 1;

        public static string AssistName(int mode) => mode == AssistBeginner ? "BEGINNER" : mode == AssistExpert ? "EXPERT" : "STANDARD";

        public static string SteeringName(int mode) => mode == SteerButtons ? "LEFT / RIGHT" : mode == SteerTilt ? "TILT" : "WHEEL";

        /// <summary>Steering choices in the order the Controls panel shows them.</summary>
        public static readonly int[] SteeringChoices = { SteerWheel, SteerButtons, SteerTilt };

        /// <summary>Puts every control option back to its default (audio and graphics are left alone).</summary>
        public void ResetControls()
        {
            steeringSensitivity = 1f;
            steeringMode = SteerWheel;
            pedalMode = PedalsClassic;
            controlScale = 1f;
            controlOpacity = 0.9f;
            controlsRaise = 0f;
            leftHanded = false;
            tiltZero = 0f;
            hudScale = 1f;
            assistMode = AssistStandard;
            assistSounds = true;
        }

        public void Clamp()
        {
            graphicsMode = Mathf.Clamp(graphicsMode, 0, 3);
            frameRateMode = Mathf.Clamp(frameRateMode, 0, 3);
            musicVolume = Mathf.Clamp01(musicVolume);
            soundVolume = Mathf.Clamp01(soundVolume);
            steeringSensitivity = Mathf.Clamp(steeringSensitivity <= 0f ? 1f : steeringSensitivity, MinSensitivity, MaxSensitivity);
            steeringMode = steeringMode < SteerButtons || steeringMode > SteerTilt ? SteerWheel : steeringMode;
            pedalMode = pedalMode == PedalsGears ? PedalsGears : PedalsClassic;
            controlScale = Mathf.Clamp(controlScale <= 0f ? 1f : controlScale, MinControlScale, MaxControlScale);
            controlOpacity = Mathf.Clamp(controlOpacity <= 0f ? 0.9f : controlOpacity, MinControlOpacity, 1f);
            controlsRaise = Mathf.Clamp01(controlsRaise);
            hudScale = Mathf.Clamp(hudScale <= 0f ? 1f : hudScale, MinHudScale, MaxHudScale);
            tiltZero = float.IsNaN(tiltZero) ? 0f : Mathf.Clamp(tiltZero, -60f, 60f);
            cameraView = Mathf.Clamp(cameraView, 0, 4);
            chaseDistance = Mathf.Clamp(chaseDistance <= 0f ? 7f : chaseDistance, MinChaseDistance, MaxChaseDistance);
            assistMode = Mathf.Clamp(assistMode, AssistBeginner, AssistExpert);
            playDifficulty = Mathf.Clamp(playDifficulty, 0, 2);
        }
    }
}
