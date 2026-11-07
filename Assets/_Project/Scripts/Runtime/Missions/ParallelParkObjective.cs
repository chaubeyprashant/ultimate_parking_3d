using System;
using UnityEngine;
using UnityEngine.Scripting;
using UltimateCarParking.Environment;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Parallel parking at a kerb: inside the slot and facing the traffic direction, with the kerb-side of the car
    /// within <see cref="maxKerbGap"/> of the kerb. Each time a wheel mounts the kerb (a collider with
    /// <see cref="KerbMarker"/>) is a strike; more than <see cref="kerbStrikeLimit"/> strikes fails the mission.
    /// </summary>
    [Serializable, Preserve]
    public sealed class ParallelParkObjective : ParkObjective
    {
        [Tooltip("+1: the kerb is on the car's right when it faces the slot direction. -1: on its left.")]
        [SerializeField] private int kerbSide = 1;
        [Tooltip("Largest allowed gap between the car's kerb-side edge and the kerb (m).")]
        [SerializeField, Min(0.05f)] private float maxKerbGap = 0.45f;
        [Tooltip("Kerb strikes allowed; one more fails the mission. -1 = unlimited.")]
        [SerializeField, Min(-1)] private int kerbStrikeLimit = 2;
        [Tooltip("Seconds all wheels must be off the kerb before a new strike can count.")]
        [SerializeField, Min(0f)] private float strikeCooldown = 0.8f;

        [NonSerialized] private WheelCollider[] wheels;
        [NonSerialized] private bool onKerb;
        [NonSerialized] private float offKerbTime;

        public ParallelParkObjective() => direction = ParkDirection.NoseIn;

        public ParallelParkObjective(string title, Vector3 position, float slotYaw, Vector2 size, int kerbSide, float maxKerbGap,
            int kerbStrikeLimit, ParkingRules rules = null)
            : base(title, position, slotYaw, size, ParkDirection.NoseIn, rules)
        {
            this.kerbSide = kerbSide >= 0 ? 1 : -1;
            this.maxKerbGap = maxKerbGap;
            this.kerbStrikeLimit = kerbStrikeLimit;
        }

        public int KerbStrikes { get; private set; }
        public int KerbStrikeLimit => kerbStrikeLimit;
        public float MaxKerbGap => maxKerbGap;
        public float KerbGap { get; private set; } = float.PositiveInfinity;

        protected override bool HasCondition => true;

        public override string FailDetail => $"HIT THE KERB {KerbStrikes} TIMES";

        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            wheels = ctx.Vehicle.GetComponentsInChildren<WheelCollider>();
            KerbStrikes = 0;
            onKerb = false;
            offKerbTime = strikeCooldown;
            KerbGap = float.PositiveInfinity;
        }

        public override ObjectiveState Tick(MissionContext ctx, float deltaTime)
        {
            bool touching = false;
            foreach (WheelCollider w in wheels)
                if (w != null && w.GetGroundHit(out WheelHit hit) && hit.collider != null && hit.collider.TryGetComponent(out KerbMarker _))
                    touching = true;

            if (touching && !onKerb && offKerbTime >= strikeCooldown)
            {
                KerbStrikes++;
                bool over = kerbStrikeLimit >= 0 && KerbStrikes > kerbStrikeLimit;
                ctx.Notify(kerbStrikeLimit >= 0 ? $"KERB STRIKE {KerbStrikes}/{kerbStrikeLimit}" : "KERB STRIKE", bad: true);
                if (over) { onKerb = true; return ObjectiveState.Failed; }
            }
            if (touching) offKerbTime = 0f;
            else offKerbTime += deltaTime;
            onKerb = touching;

            UpdateGap(ctx.Validator);
            bool inSlot = ctx.Validator.CornersInside > 0;
            string caption = kerbStrikeLimit >= 0 ? $"KERB GAP   STRIKES {KerbStrikes}/{kerbStrikeLimit}" : "KERB GAP";
            ctx.Gauge.Show(caption, inSlot ? $"{KerbGap:0.00} m" : "--", inSlot ? 1f - KerbGap / 1.5f : 0f,
                !inSlot ? GaugeSeverity.Warn : KerbGap <= maxKerbGap ? GaugeSeverity.Good : GaugeSeverity.Warn);
            return base.Tick(ctx, deltaTime);
        }

        public override string Hint(MissionContext ctx) =>
            ctx.Validator.Status == ParkingStatus.Outside ? "PULL PAST THE GAP, THEN REVERSE IN" : base.Hint(ctx);

        public override bool IsMet(ParkingValidator validator, out string hint)
        {
            UpdateGap(validator);
            if (KerbGap <= maxKerbGap) { hint = ""; return true; }
            hint = $"CLOSER TO THE KERB  {KerbGap:0.00} m";
            return false;
        }

        private void UpdateGap(ParkingValidator validator)
        {
            float maxSide = float.MinValue;
            foreach (Vector3 c in validator.WorldFootprint) maxSide = Mathf.Max(maxSide, ToZoneLocal(c).x * kerbSide);
            KerbGap = Mathf.Max(0f, size.x * 0.5f - maxSide);
        }
    }
}
