using System;
using UnityEngine;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Parking
{
    /// <summary>
    /// Decides when the car is parked: every footprint corner inside the bay, heading within the angle
    /// tolerance, speed under the threshold, all held continuously for the hold duration.
    /// Bound to one zone and one car by the LevelLoader; armed and disarmed by the LevelManager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParkingValidator : MonoBehaviour
    {
        [Tooltip("Thresholds used when the level does not override them.")]
        [SerializeField] private ParkingRules defaultRules = new ParkingRules();

        [Tooltip("Footprint used if the car has no BoxColliders to measure (width, length in metres).")]
        [SerializeField] private Vector2 fallbackFootprint = new Vector2(1.8f, 4.3f);

        private const float MsToKmh = 3.6f;

        private readonly Vector3[] localCorners = new Vector3[4];
        private readonly Vector3[] worldCorners = new Vector3[4];
        private ParkingZone zone;
        private VehicleController vehicle;
        private Rigidbody body;
        private ParkingRules rules;
        private IParkingCondition condition;
        private float holdTimer;
        private bool armed;

        public event Action<ParkingStatus> StatusChanged;
        public event Action Parked;

        public ParkingStatus Status { get; private set; } = ParkingStatus.Inactive;
        public ParkingRules ActiveRules => rules ?? defaultRules;
        public ParkingRules DefaultRules => defaultRules;
        public int CornersInside { get; private set; }
        public float HeadingErrorDegrees { get; private set; }
        public float SpeedKmh { get; private set; }
        public bool IsArmed => armed;
        public ParkingZone Zone => zone;
        public VehicleController Vehicle => vehicle;

        /// <summary>The car's footprint corners in world space, as of the last evaluation.</summary>
        public Vector3[] WorldFootprint => worldCorners;

        /// <summary>Why the extra condition is unmet (status <see cref="ParkingStatus.Unpositioned"/>), else empty.</summary>
        public string ConditionHint { get; private set; } = "";

        /// <summary>0..1 progress of the hold timer (1 once parked).</summary>
        public float HoldProgress =>
            Status == ParkingStatus.Parked ? 1f :
            ActiveRules.holdDuration <= 0f ? 0f : Mathf.Clamp01(holdTimer / ActiveRules.holdDuration);

        /// <summary>The car's footprint corners in its local space (ground plane), measured at bind time.</summary>
        public Vector3[] LocalFootprint => localCorners;

        /// <summary>
        /// Connects the validator to a bay and a car. Pass null rules to use the Inspector defaults. An optional
        /// <paramref name="extraCondition"/> (rear depth, kerb gap…) must also hold before the hold timer runs.
        /// </summary>
        public void Bind(ParkingZone parkingZone, VehicleController car, ParkingRules levelRules = null, IParkingCondition extraCondition = null)
        {
            condition = extraCondition;
            ConditionHint = "";
            zone = parkingZone;
            vehicle = car;
            body = car != null ? car.Body != null ? car.Body : car.GetComponent<Rigidbody>() : null;
            rules = levelRules != null ? levelRules.Clone() : defaultRules.Clone();
            armed = false;
            holdTimer = 0f;

            if (zone == null) Debug.LogError("[ParkingValidator] Bind: no ParkingZone.", this);
            if (vehicle == null) Debug.LogError("[ParkingValidator] Bind: no VehicleController.", this);
            if (zone != null && body != null) zone.Track(body);
            if (vehicle != null) MeasureFootprint(vehicle.transform);
            SetStatus(ParkingStatus.Inactive);
        }

        /// <summary>Measures the car's footprint without binding a bay (side sensors on route objectives).</summary>
        public void EnsureFootprint(VehicleController car)
        {
            if (car != null && (localCorners[0] == Vector3.zero || vehicle != car)) MeasureFootprint(car.transform);
        }

        /// <summary>Armed = the level is running and parking can succeed.</summary>
        public void SetArmed(bool value)
        {
            armed = value && zone != null && vehicle != null;
            holdTimer = 0f;
            if (armed) SetStatus(Evaluate());   // report the real status at once; the hold timer starts next physics step
            else if (Status != ParkingStatus.Parked) SetStatus(ParkingStatus.Inactive);
        }

        private void FixedUpdate()
        {
            if (!armed || zone == null || vehicle == null) return;

            ParkingStatus next = Evaluate();
            if (next == ParkingStatus.Holding)
            {
                holdTimer += Time.fixedDeltaTime;
                if (holdTimer >= ActiveRules.holdDuration)
                {
                    armed = false;
                    SetStatus(ParkingStatus.Parked);
                    Parked?.Invoke();
                    return;
                }
            }
            else
            {
                holdTimer = 0f;
            }
            SetStatus(next);
        }

        /// <summary>Current status from the car's pose and speed, ignoring the hold timer.</summary>
        public ParkingStatus Evaluate()
        {
            if (zone == null || vehicle == null) return ParkingStatus.Inactive;
            ParkingRules r = ActiveRules;

            Transform car = vehicle.transform;
            for (int i = 0; i < 4; i++) worldCorners[i] = car.TransformPoint(localCorners[i]);
            CornersInside = ParkingGeometry.CountPointsInside(zone.Center, zone.Rotation, zone.Size, worldCorners, r.edgeMargin);
            HeadingErrorDegrees = ParkingGeometry.HeadingError(car.forward, zone.Forward, r.allowEitherDirection);
            SpeedKmh = body != null ? body.linearVelocity.magnitude * MsToKmh : vehicle.SpeedKmh;

            if (!zone.IsOccupied && CornersInside == 0) return ParkingStatus.Outside;
            if (CornersInside < 4) return CornersInside == 0 ? ParkingStatus.Outside : ParkingStatus.PartiallyInside;
            if (HeadingErrorDegrees > r.angleTolerance) return ParkingStatus.Misaligned;
            if (condition != null && !condition.IsMet(this, out string hint)) { ConditionHint = hint ?? ""; return ParkingStatus.Unpositioned; }
            ConditionHint = "";
            if (SpeedKmh > r.maxSpeedKmh) return ParkingStatus.TooFast;
            return ParkingStatus.Holding;
        }

        private void SetStatus(ParkingStatus status)
        {
            if (zone != null) zone.ShowStatus(status);
            if (status == Status) return;
            Status = status;
            StatusChanged?.Invoke(status);
        }

        /// <summary>
        /// Footprint = the ground-plane rectangle around all non-trigger BoxColliders of the car, in its local space.
        /// Measured once per bind, not per frame.
        /// </summary>
        private void MeasureFootprint(Transform car)
        {
            bool any = false;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (BoxCollider box in car.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger) continue;
                Vector3 c = box.center, e = box.size * 0.5f;
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 p = car.InverseTransformPoint(box.transform.TransformPoint(c + new Vector3(sx * e.x, 0f, sz * e.z)));
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                    any = true;
                }
            }
            if (!any)
            {
                Debug.LogWarning($"[ParkingValidator] {car.name} has no BoxColliders; using the fallback footprint {fallbackFootprint}.", this);
                minX = -fallbackFootprint.x * 0.5f; maxX = -minX;
                minZ = -fallbackFootprint.y * 0.5f; maxZ = -minZ;
            }
            localCorners[0] = new Vector3(minX, 0f, minZ);
            localCorners[1] = new Vector3(maxX, 0f, minZ);
            localCorners[2] = new Vector3(maxX, 0f, maxZ);
            localCorners[3] = new Vector3(minX, 0f, maxZ);
        }

        private void OnDrawGizmosSelected()
        {
            if (vehicle == null) return;
            Gizmos.color = Status == ParkingStatus.Holding || Status == ParkingStatus.Parked ? Color.green : Color.red;
            for (int i = 0; i < 4; i++)
                Gizmos.DrawLine(vehicle.transform.TransformPoint(localCorners[i]), vehicle.transform.TransformPoint(localCorners[(i + 1) % 4]));
        }
    }
}
