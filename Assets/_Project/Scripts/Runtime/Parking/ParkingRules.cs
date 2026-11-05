using System;
using UnityEngine;

namespace UltimateCarParking.Parking
{
    /// <summary>
    /// Thresholds a parked car must meet. The ParkingValidator holds the default set in the Inspector;
    /// a LevelData can supply its own set to make one level stricter or looser.
    /// </summary>
    [Serializable]
    public sealed class ParkingRules
    {
        [Tooltip("Every corner of the car's footprint must be at least this far inside the bay lines (m). 0 = touching the line is allowed.")]
        [Min(0f)] public float edgeMargin = 0.05f;

        [Tooltip("Largest allowed angle (degrees) between the car and the bay's long axis.")]
        [Range(0f, 45f)] public float angleTolerance = 10f;

        [Tooltip("Accept the car facing either way along the bay (nose-in or reversed in).")]
        public bool allowEitherDirection = true;

        [Tooltip("The car counts as stopped below this speed (km/h).")]
        [Min(0f)] public float maxSpeedKmh = 2f;

        [Tooltip("Seconds the car must stay inside, aligned and stopped before the level is complete.")]
        [Min(0f)] public float holdDuration = 1.5f;

        public ParkingRules Clone() => (ParkingRules)MemberwiseClone();
    }
}
