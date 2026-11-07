using System;
using UnityEngine;
using UnityEngine.Scripting;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Precision parking: being inside the lines is not enough. Accuracy is graded from how far the car's centre sits
    /// off the bay's centre line and how far its heading is off the bay axis; the car only counts as parked once
    /// accuracy reaches <see cref="minAccuracy"/>, and the final accuracy is the mission's score (stars in
    /// <see cref="Levels.ScoreMode.Quality"/> levels). Live "ACCURACY" gauge while in the bay.
    /// </summary>
    [Serializable, Preserve]
    public sealed class PrecisionParkObjective : ParkObjective, IScoredObjective
    {
        [Tooltip("Sideways offset from the centre line that scores zero for position, metres.")]
        [SerializeField, Min(0.05f)] private float maxOffset = 0.35f;
        [Tooltip("Front/back offset from the bay centre that scores zero for depth, metres.")]
        [SerializeField, Min(0.05f)] private float maxDepthOffset = 0.8f;
        [Tooltip("Heading error that scores zero for angle, degrees.")]
        [SerializeField, Min(0.5f)] private float maxAngle = 5f;
        [Tooltip("Accuracy needed before the car counts as parked.")]
        [SerializeField, Range(0f, 1f)] private float minAccuracy = 0.55f;

        [NonSerialized] private float finalAccuracy = -1f;
        [NonSerialized] private MissionContext context;

        public PrecisionParkObjective() { }

        public PrecisionParkObjective(string title, Vector3 position, float bayYaw, Vector2 size, ParkDirection direction,
            float maxOffset = 0.35f, float maxAngle = 5f, float minAccuracy = 0.55f, ParkingRules rules = null)
            : base(title, position, bayYaw, size, direction, rules)
        {
            this.maxOffset = maxOffset;
            this.maxAngle = maxAngle;
            this.minAccuracy = minAccuracy;
        }

        public float MinAccuracy => minAccuracy;
        public float Accuracy { get; private set; }
        public float Quality => finalAccuracy >= 0f ? finalAccuracy : Accuracy;
        public string ScoreLabel => $"ACCURACY {Mathf.RoundToInt(Quality * 100f)}%";
        protected override bool HasCondition => true;

        public override void Begin(MissionContext ctx)
        {
            context = ctx;
            finalAccuracy = -1f;
            Accuracy = 0f;
            base.Begin(ctx);
        }

        public override ObjectiveState Tick(MissionContext ctx, float deltaTime)
        {
            Accuracy = Measure(ctx.Validator);
            if (ctx.Validator.CornersInside >= 2)
            {
                GaugeSeverity sev = Accuracy >= 0.85f ? GaugeSeverity.Good : Accuracy >= minAccuracy ? GaugeSeverity.Warn : GaugeSeverity.Danger;
                ctx.Gauge.Show("ACCURACY", $"{Mathf.RoundToInt(Accuracy * 100f)}%", Accuracy, sev);
            }
            else ctx.Gauge.Hide();
            ObjectiveState s = base.Tick(ctx, deltaTime);
            if (s == ObjectiveState.Completed) finalAccuracy = Accuracy;
            return s;
        }

        public override bool IsMet(ParkingValidator validator, out string hint)
        {
            float a = Measure(validator);
            if (a >= minAccuracy) { hint = ""; return true; }
            Vector3 off = CentreOffset(validator);
            hint = Mathf.Abs(off.x) / maxOffset > Mathf.Abs(off.z) / maxDepthOffset
                ? "CENTRE THE CAR BETWEEN THE LINES"
                : (off.z > 0f ? "TOO DEEP - BACK OUT A LITTLE" : "NOT DEEP ENOUGH - PULL IN");
            return false;
        }

        /// <summary>0..1: 50% sideways centring, 20% depth, 30% heading.</summary>
        public float Measure(ParkingValidator validator)
        {
            if (validator == null || validator.WorldFootprint == null || validator.WorldFootprint.Length == 0) return 0f;
            Vector3 off = CentreOffset(validator);
            float side = 1f - Mathf.Clamp01(Mathf.Abs(off.x) / maxOffset);
            float depth = 1f - Mathf.Clamp01(Mathf.Abs(off.z) / maxDepthOffset);
            float angle = 1f - Mathf.Clamp01(validator.HeadingErrorDegrees / maxAngle);
            return Mathf.Clamp01(side * 0.5f + depth * 0.2f + angle * 0.3f);
        }

        /// <summary>The car footprint's centre in the bay frame (x right of the centre line, z deeper into the bay).</summary>
        private Vector3 CentreOffset(ParkingValidator validator)
        {
            Vector3 c = Vector3.zero;
            Vector3[] f = validator.WorldFootprint;
            foreach (Vector3 p in f) c += p;
            c /= f.Length;
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, bayYaw, 0f)) * (c - position);
            return new Vector3(local.x, 0f, local.z);
        }

        public override string Hint(MissionContext ctx) =>
            ctx.Validator.Status == ParkingStatus.Holding ? $"HOLD IT... ACCURACY {Mathf.RoundToInt(Accuracy * 100f)}%" : base.Hint(ctx);
    }
}
