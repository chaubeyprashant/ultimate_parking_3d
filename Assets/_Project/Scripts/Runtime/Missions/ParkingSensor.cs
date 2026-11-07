using UnityEngine;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Bumper distance sensor: a short box cast from the rear (or front) bumper along the car's axis, at bumper
    /// height, ignoring the car itself, triggers and anything lower than a kerb. Plain class owned by an objective.
    /// </summary>
    public sealed class ParkingSensor
    {
        public const float Range = 3f;
        private readonly RaycastHit[] hits = new RaycastHit[12];
        private readonly bool rear;
        private float halfWidth = 0.9f, bumperZ = -2.2f;

        public ParkingSensor(bool rear) => this.rear = rear;

        /// <summary>Distance in metres to the nearest obstacle, or +∞ beyond <see cref="Range"/>.</summary>
        public float Distance { get; private set; } = float.PositiveInfinity;

        /// <summary>Takes the bumper position and width from the car's measured footprint (local corners).</summary>
        public void Calibrate(Vector3[] localFootprint)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector3 c in localFootprint)
            {
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z);
            }
            halfWidth = Mathf.Max(0.3f, (maxX - minX) * 0.5f - 0.05f);
            bumperZ = rear ? minZ : maxZ;
        }

        public float Measure(VehicleController car)
        {
            Transform t = car.transform;
            Vector3 dir = rear ? -t.forward : t.forward;
            Vector3 origin = t.TransformPoint(new Vector3(0f, 0.55f, bumperZ)) - dir * 0.1f;
            int n = Physics.BoxCastNonAlloc(origin, new Vector3(halfWidth, 0.3f, 0.05f), dir, hits, t.rotation, Range + 0.1f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = hits[i];
                if (h.rigidbody != null && h.rigidbody == car.Body) continue;
                if (h.distance <= 0f) continue;   // started overlapping: not a forward obstacle
                best = Mathf.Min(best, Mathf.Max(0f, h.distance - 0.1f));
            }
            Distance = best;
            return best;
        }
    }
}
