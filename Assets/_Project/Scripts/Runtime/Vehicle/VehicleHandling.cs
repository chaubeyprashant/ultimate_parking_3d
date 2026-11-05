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

        public VehicleHandling Clone() => (VehicleHandling)MemberwiseClone();
    }
}
