using System;
using UnityEngine;

namespace UltimateCarParking.Vehicle
{
    /// <summary>
    /// Tunable handling values a car model can override on <see cref="VehicleController"/> (see CarData).
    /// Defaults equal the controller's own defaults, so applying a default set changes nothing.
    /// </summary>
    [Serializable]
    public sealed class VehicleHandling
    {
        public VehicleController.DriveType driveType = VehicleController.DriveType.FrontWheelDrive;
        [Min(300f)] public float mass = 1300f;
        [Tooltip("Local centre of mass. Keep it near axle height.")]
        public Vector3 centerOfMass = new Vector3(0f, 0.3f, 0.05f);
        [Min(0f)] public float motorTorque = 1600f;
        [Min(0f)] public float reverseTorque = 1100f;
        [Min(1f)] public float maxForwardSpeedKmh = 60f;
        [Min(1f)] public float maxReverseSpeedKmh = 20f;
        [Min(0f)] public float brakeTorque = 2500f;
        [Min(0f)] public float handbrakeTorque = 4000f;
        [Range(5f, 50f)] public float maxSteerAngle = 35f;
        [Range(1f, 50f)] public float highSpeedSteerAngle = 12f;
        [Min(1f)] public float steerSpeed = 110f;

        [Header("Chassis (0 = keep the car prefab's own value)")]
        [Tooltip("Suspension travel (m). Monster trucks long, the F1 car short.")]
        [Min(0f)] public float suspensionTravel;
        [Tooltip("Suspension natural frequency (Hz): ~1 soft truck, ~1.5 comfortable car, ~2+ sports car.")]
        [Min(0f)] public float springHz;
        [Tooltip("Suspension damping ratio (0.5 floaty .. 0.75 tied down).")]
        [Min(0f)] public float dampingRatio;
        [Tooltip("Tyre grip along the rolling direction (WheelCollider forward stiffness).")]
        [Min(0f)] public float tireGrip;
        [Tooltip("Tyre grip sideways (WheelCollider sideways stiffness).")]
        [Min(0f)] public float sideGrip;
        [Tooltip("Anti-roll bar stiffness per axle (N per unit of travel difference).")]
        [Min(0f)] public float antiRoll;
        [Tooltip("Degrees per second the front wheels return toward straight.")]
        [Min(0f)] public float steerReturnSpeed;
        [Tooltip("Strongest acceleration (m/s²) allowed below parking speed, so a tap of the pedal moves the car a controllable amount. Fades out by 15 km/h.")]
        [Min(0f)] public float launchAccel;

        public VehicleHandling Clone() => (VehicleHandling)MemberwiseClone();
    }
}
