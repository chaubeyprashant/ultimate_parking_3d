using UnityEngine;

namespace UltimateCarParking.Parking
{
    /// <summary>Pure geometry used by <see cref="ParkingValidator"/>. No scene state, so it is unit-tested directly.</summary>
    public static class ParkingGeometry
    {
        /// <summary>
        /// Counts how many world-space points lie inside a bay rectangle on the ground plane.
        /// The bay is centred on <paramref name="bayCenter"/>, rotated by <paramref name="bayRotation"/>;
        /// <paramref name="baySize"/> is (width along local X, length along local Z). Height is ignored.
        /// </summary>
        public static int CountPointsInside(Vector3 bayCenter, Quaternion bayRotation, Vector2 baySize, Vector3[] worldPoints, float margin)
        {
            if (worldPoints == null) return 0;
            float halfWidth = baySize.x * 0.5f - margin;
            float halfLength = baySize.y * 0.5f - margin;
            if (halfWidth <= 0f || halfLength <= 0f) return 0;

            Quaternion toLocal = Quaternion.Inverse(bayRotation);
            int inside = 0;
            foreach (Vector3 p in worldPoints)
            {
                Vector3 local = toLocal * (p - bayCenter);
                if (Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.z) <= halfLength) inside++;
            }
            return inside;
        }

        /// <summary>
        /// Angle in degrees between the car's heading and the bay's long axis, on the ground plane.
        /// With <paramref name="eitherDirection"/> a car facing out of the bay scores the same as one facing in.
        /// </summary>
        public static float HeadingError(Vector3 vehicleForward, Vector3 bayForward, bool eitherDirection)
        {
            vehicleForward.y = 0f;
            bayForward.y = 0f;
            if (vehicleForward.sqrMagnitude < 1e-6f || bayForward.sqrMagnitude < 1e-6f) return 180f;
            float angle = Vector3.Angle(vehicleForward, bayForward);
            return eitherDirection ? Mathf.Min(angle, 180f - angle) : angle;
        }
    }
}
