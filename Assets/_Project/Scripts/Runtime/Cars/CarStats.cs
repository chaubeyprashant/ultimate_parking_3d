using UnityEngine;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Cars
{
    /// <summary>0..1 ratings derived from handling values, for the garage stat bars.</summary>
    public readonly struct CarStats
    {
        public readonly float TopSpeed;
        public readonly float Acceleration;
        public readonly float Handling;
        public readonly float Braking;

        public CarStats(float topSpeed, float acceleration, float handling, float braking)
        {
            TopSpeed = topSpeed;
            Acceleration = acceleration;
            Handling = handling;
            Braking = braking;
        }

        public static CarStats From(VehicleHandling h)
        {
            if (h == null) return default;
            float mass = Mathf.Max(300f, h.mass);
            return new CarStats(
                Mathf.InverseLerp(40f, 80f, h.maxForwardSpeedKmh),
                Mathf.InverseLerp(0.8f, 2.0f, h.motorTorque / mass),       // N·m per kg
                Mathf.InverseLerp(28f, 40f, h.maxSteerAngle) * 0.7f + Mathf.InverseLerp(80f, 140f, h.steerSpeed) * 0.3f,
                Mathf.InverseLerp(1.2f, 2.6f, h.brakeTorque / mass));
        }
    }
}
