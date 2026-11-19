using System;
using UnityEngine;

namespace UltimateCarParking.Cars
{
    /// <summary>
    /// A car's spec sheet for the garage, measured, not estimated: acceleration, braking and turning come from the
    /// automated handling benchmark (VehicleBenchmarkTests → Documentation/vehicle_specs.csv), size from the car's
    /// collision box. Imported into each CarData when the project is built. Zero = not measured yet.
    /// </summary>
    [Serializable]
    public sealed class CarSpecs
    {
        [Tooltip("Seconds from standstill to 20 km/h, full pedal.")]
        public float zeroTo20;
        [Tooltip("Metres to stop from 40 km/h on a dry road.")]
        public float stopFrom40;
        [Tooltip("Metres to stop from 40 km/h on a wet road.")]
        public float wetStopFrom40;
        [Tooltip("Turning radius at full lock, centre of the car (m).")]
        public float turnRadius;
        [Tooltip("Speed after 3.5 s of full pedal (km/h).")]
        public float speedAfter35;
        public float length;
        public float width;
        [Tooltip("1 (easy) .. 5 (hard) to park: from size and turning circle.")]
        [Range(0, 5)] public int parkingDifficulty;
        [Tooltip("What this car is best at, one line.")]
        public string strength = "";

        public bool Measured => zeroTo20 > 0f && stopFrom40 > 0f;

        public static string DifficultyName(int d) => d <= 1 ? "EASY" : d == 2 ? "FAIR" : d == 3 ? "MEDIUM" : d == 4 ? "HARD" : "EXPERT";

        /// <summary>Difficulty from footprint and turning circle (a long, wide, wide-turning car is harder to park).</summary>
        public static int Difficulty(float length, float width, float turnRadius)
        {
            float score = 1f;
            score += Mathf.InverseLerp(4.0f, 5.4f, length) * 1.6f;
            score += Mathf.InverseLerp(1.75f, 2.1f, width) * 1.2f;
            score += Mathf.InverseLerp(3.8f, 6.0f, turnRadius) * 1.4f;
            return Mathf.Clamp(Mathf.RoundToInt(score), 1, 5);
        }
    }
}
