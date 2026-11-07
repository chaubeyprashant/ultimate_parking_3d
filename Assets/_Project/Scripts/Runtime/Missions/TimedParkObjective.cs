using System;
using UnityEngine;
using UnityEngine.Scripting;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Timed parking: the objective's own countdown starts when it begins (on top of any level limit) and running out
    /// fails the mission. Each hit costs <see cref="hitPenaltySeconds"/> off the clock. The score is the share of the
    /// clock left when parked, so a fast clean park earns three stars in a <see cref="Levels.ScoreMode.Quality"/> level.
    /// </summary>
    [Serializable, Preserve]
    public sealed class TimedParkObjective : ParkObjective, IScoredObjective
    {
        [SerializeField, Min(3f)] private float seconds = 30f;
        [Tooltip("Seconds taken off the clock for every collision.")]
        [SerializeField, Min(0f)] private float hitPenaltySeconds = 3f;

        [NonSerialized] private float remaining;
        [NonSerialized] private float finalShare = -1f;
        [NonSerialized] private MissionContext context;

        public TimedParkObjective() { }

        public TimedParkObjective(string title, Vector3 position, float bayYaw, Vector2 size, ParkDirection direction,
            float seconds, float hitPenaltySeconds = 3f, ParkingRules rules = null)
            : base(title, position, bayYaw, size, direction, rules)
        {
            this.seconds = seconds;
            this.hitPenaltySeconds = hitPenaltySeconds;
        }

        public float Seconds => seconds;
        public float Remaining => remaining;
        public float Quality => finalShare >= 0f ? finalShare : Mathf.Clamp01(remaining / seconds);
        public string ScoreLabel => $"TIME LEFT {Mathf.Max(0f, remaining):0.0}s";
        public override string FailDetail => "OUT OF TIME";

        public override void Begin(MissionContext ctx)
        {
            context = ctx;
            remaining = seconds;
            finalShare = -1f;
            base.Begin(ctx);
            if (ctx.Collisions != null) ctx.Collisions.PenaltyRegistered += OnHit;
            ctx.Notify($"BEAT THE CLOCK: {seconds:0}s", false);
        }

        private void OnHit(Vehicle.CollisionHandler.Penalty p)
        {
            if (hitPenaltySeconds <= 0f || finalShare >= 0f) return;
            remaining -= hitPenaltySeconds;
            context?.Notify($"-{hitPenaltySeconds:0}s", true);
        }

        public override ObjectiveState Tick(MissionContext ctx, float deltaTime)
        {
            ObjectiveState s = base.Tick(ctx, deltaTime);
            if (s == ObjectiveState.Completed) { finalShare = Mathf.Clamp01(remaining / seconds); return s; }
            // The clock stops while holding still in the bay (the hold is the validator's, not the player's time).
            if (ctx.Validator.Status != ParkingStatus.Holding) remaining -= deltaTime;
            float share = Mathf.Clamp01(remaining / seconds);
            ctx.Gauge.Show("CLOCK", $"{Mathf.Max(0f, remaining):0.0}s", share,
                share > 0.4f ? GaugeSeverity.Good : share > 0.2f ? GaugeSeverity.Warn : GaugeSeverity.Danger);
            return remaining <= 0f ? ObjectiveState.Failed : ObjectiveState.Running;
        }

        public override void End(MissionContext ctx)
        {
            if (ctx.Collisions != null) ctx.Collisions.PenaltyRegistered -= OnHit;
            base.End(ctx);
        }

        public override string Hint(MissionContext ctx) =>
            ctx.Validator.Status == ParkingStatus.Outside && remaining < seconds * 0.25f ? "HURRY - DRIVE TO THE BAY" : base.Hint(ctx);
    }
}
