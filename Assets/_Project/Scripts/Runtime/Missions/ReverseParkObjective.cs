using System;
using UnityEngine;
using UnityEngine.Scripting;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Reverse parking: the car must end facing out of the bay AND be backed in until its rear bumper is within
    /// <see cref="maxRearGap"/> of the back line. A rear parking sensor (gauge + beeps) is the main feedback.
    /// </summary>
    [Serializable, Preserve]
    public sealed class ReverseParkObjective : ParkObjective
    {
        [Tooltip("Largest allowed gap between the rear bumper and the bay's back line (m).")]
        [SerializeField, Min(0.05f)] private float maxRearGap = 0.6f;

        [NonSerialized] private ParkingSensor sensor;

        public ReverseParkObjective() => direction = ParkDirection.ReverseIn;

        public ReverseParkObjective(string title, Vector3 position, float bayYaw, Vector2 size, float maxRearGap, ParkingRules rules = null)
            : base(title, position, bayYaw, size, ParkDirection.ReverseIn, rules)
        {
            this.maxRearGap = maxRearGap;
        }

        public float MaxRearGap => maxRearGap;

        /// <summary>Gap between the rear bumper and the back line as of the last physics step (m).</summary>
        public float RearGap { get; private set; } = float.PositiveInfinity;

        protected override bool HasCondition => true;

        public override void Begin(MissionContext ctx)
        {
            direction = ParkDirection.ReverseIn;
            base.Begin(ctx);
            sensor = new ParkingSensor(rear: true);
            sensor.Calibrate(ctx.Validator.LocalFootprint);
            RearGap = float.PositiveInfinity;
        }

        public override ObjectiveState Tick(MissionContext ctx, float deltaTime)
        {
            float d = sensor.Measure(ctx.Vehicle);
            bool movingForward = ctx.Vehicle.ForwardSpeedKmh > 0.5f;
            ctx.SensorAudio?.SetDistance(movingForward ? float.PositiveInfinity : d);
            bool far = float.IsPositiveInfinity(d);
            ctx.Gauge.Show("REAR SENSOR", far ? "CLEAR" : $"{d:0.0} m", far ? 0f : 1f - d / ParkingSensor.Range,
                d < 0.3f ? GaugeSeverity.Danger : d < 0.9f ? GaugeSeverity.Warn : GaugeSeverity.Good);
            return base.Tick(ctx, deltaTime);
        }

        public override void End(MissionContext ctx)
        {
            ctx.SensorAudio?.Silence();
            base.End(ctx);
        }

        public override string Hint(MissionContext ctx)
        {
            ParkingValidator v = ctx.Validator;
            if (v.Status == ParkingStatus.Outside) return "REVERSE INTO THE BAY";
            if ((v.Status == ParkingStatus.Misaligned || v.Status == ParkingStatus.PartiallyInside) && v.HeadingErrorDegrees > 135f)
                return "WRONG WAY - BACK IN WITH REVERSE";
            return base.Hint(ctx);
        }

        public override bool IsMet(ParkingValidator validator, out string hint)
        {
            // Zone forward = the way the car faces (out of the bay), so the back line is at local z = -length/2.
            float minZ = float.MaxValue;
            foreach (Vector3 c in validator.WorldFootprint) minZ = Mathf.Min(minZ, ToZoneLocal(c).z);
            RearGap = minZ + size.y * 0.5f;
            if (RearGap <= maxRearGap) { hint = ""; return true; }
            hint = $"BACK IN FURTHER  {RearGap:0.0} m TO THE LINE";
            return false;
        }
    }
}
