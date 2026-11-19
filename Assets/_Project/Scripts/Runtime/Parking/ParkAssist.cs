using UnityEngine;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Parking
{
    /// <summary>Parking assistance level (Settings → Controls).</summary>
    public enum AssistLevel
    {
        /// <summary>Sensors, beeps, bay guide, rear-camera guide lines and alignment hints.</summary>
        Beginner = 0,
        /// <summary>Sensors, beeps, bay guide and rear-camera guide lines.</summary>
        Standard = 1,
        /// <summary>No sensors, no bay chevron, rear camera image only.</summary>
        Expert = 2,
    }

    /// <summary>
    /// Proximity sensors around the car: three zones (left, centre, right) at the front and three at the rear, each a
    /// short box cast at bumper height from the car's measured footprint. Sampled 10 times a second (six casts per
    /// sample, no allocations). Distances are clear space from the bumper in metres (+∞ beyond <see cref="Range"/>).
    /// </summary>
    public sealed class ParkAssist
    {
        public const float Range = 2.5f;
        public const float SampleInterval = 0.1f;
        public const int Zones = 3;

        private readonly RaycastHit[] hits = new RaycastHit[12];
        private readonly float[] front = new float[Zones];
        private readonly float[] rear = new float[Zones];
        private float halfWidth = 0.9f, frontZ = 2.2f, rearZ = -2.2f, nextSample;

        public ParkAssist() { Clear(); }

        public float Front(int zone) => front[zone];
        public float Rear(int zone) => rear[zone];
        public float FrontMin => Mathf.Min(front[0], Mathf.Min(front[1], front[2]));
        public float RearMin => Mathf.Min(rear[0], Mathf.Min(rear[1], rear[2]));
        public int CastsPerSample => Zones * 2;

        public void Clear()
        {
            for (int i = 0; i < Zones; i++) { front[i] = float.PositiveInfinity; rear[i] = float.PositiveInfinity; }
        }

        /// <summary>Bumper positions and width from the car's local footprint corners (ParkingValidator.LocalFootprint).</summary>
        public void Calibrate(Vector3[] localFootprint)
        {
            if (localFootprint == null || localFootprint.Length == 0) return;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector3 c in localFootprint)
            {
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z);
            }
            halfWidth = Mathf.Max(0.4f, (maxX - minX) * 0.5f);
            frontZ = maxZ; rearZ = minZ;
        }

        /// <summary>Samples the sensors if the interval has passed (or <paramref name="force"/>). Returns true when it sampled.</summary>
        public bool Tick(VehicleController car, float now, bool force = false)
        {
            if (car == null) return false;
            if (!force && now < nextSample) return false;
            nextSample = now + SampleInterval;
            float zoneHalf = halfWidth / Zones;
            for (int z = 0; z < Zones; z++)
            {
                float x = -halfWidth + zoneHalf * (2 * z + 1);
                front[z] = Cast(car, x, zoneHalf, frontZ, true);
                rear[z] = Cast(car, x, zoneHalf, rearZ, false);
            }
            return true;
        }

        private float Cast(VehicleController car, float x, float zoneHalf, float bumperZ, bool forward)
        {
            Transform t = car.transform;
            Vector3 dir = forward ? t.forward : -t.forward;
            // Beam from 0.35 m to 0.9 m above the ground: bumpers, walls and cars, not kerbs or the road.
            Vector3 origin = t.TransformPoint(new Vector3(x, 0.625f, bumperZ)) - dir * 0.1f;
            int n = Physics.BoxCastNonAlloc(origin, new Vector3(zoneHalf * 0.95f, 0.275f, 0.05f), dir, hits, t.rotation, Range + 0.1f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = hits[i];
                if (h.rigidbody != null && h.rigidbody == car.Body) continue;
                if (h.distance <= 0f) continue;
                best = Mathf.Min(best, Mathf.Max(0f, h.distance - 0.1f));
            }
            return best > Range ? float.PositiveInfinity : best;
        }

        /// <summary>Warning band for a distance: 0 clear (&gt; 1.5 m), 1 caution, 2 close (&lt; 0.8 m), 3 stop (&lt; 0.3 m).</summary>
        public static int Band(float d) => float.IsInfinity(d) || d > 1.5f ? 0 : d > 0.8f ? 1 : d > 0.3f ? 2 : 3;

        /// <summary>Seconds between beeps for a distance (0 = continuous tone, -1 = silent).</summary>
        public static float BeepInterval(float d) =>
            float.IsInfinity(d) || d > 1.5f ? -1f : d <= 0.3f ? 0f : Mathf.Lerp(0.12f, 0.7f, Mathf.InverseLerp(0.3f, 1.5f, d));
    }
}
