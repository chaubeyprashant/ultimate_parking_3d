using System;
using UnityEngine;
using UnityEngine.Scripting;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Valet parking in two phases: pull up at the valet stand and hold still for the guest handover, then park the
    /// guest's car in its assigned bay. A guest rating starts at 100% and drops for speeding over
    /// <see cref="speedLimitKmh"/>, harsh acceleration or braking, and every hit; reaching zero fails ("THE GUEST
    /// COMPLAINED"). The final rating is the score.
    /// </summary>
    [Serializable, Preserve]
    public sealed class ValetParkObjective : ParkObjective, IScoredObjective
    {
        [SerializeField] private Vector3 standPosition;
        [SerializeField] private float standYaw;
        [SerializeField] private Vector2 standSize = new Vector2(3.2f, 6.5f);
        [SerializeField, Min(0.2f)] private float handoverSeconds = 2f;
        [SerializeField, Min(5f)] private float speedLimitKmh = 18f;
        [Tooltip("Rating lost per second over the speed limit.")]
        [SerializeField, Min(0f)] private float speedingPerSecond = 10f;
        [Tooltip("Acceleration (m/s²) above which driving counts as harsh.")]
        [SerializeField, Min(1f)] private float harshAccel = 4.5f;
        [SerializeField, Min(0f)] private float harshPerSecond = 15f;
        [SerializeField, Min(0f)] private float perHit = 20f;

        [NonSerialized] private bool handedOver;
        [NonSerialized] private float hold;
        [NonSerialized] private float rating;
        [NonSerialized] private float final = -1f;
        [NonSerialized] private ParkingZone stand;
        [NonSerialized] private MissionContext context;
        [NonSerialized] private float lastSpeed;
        [NonSerialized] private float warnCooldown;

        public ValetParkObjective() { }

        public ValetParkObjective(string title, Vector3 standPosition, float standYaw, Vector3 bayPosition, float bayYaw, Vector2 baySize,
            ParkDirection direction, float speedLimitKmh = 18f, ParkingRules rules = null)
            : base(title, bayPosition, bayYaw, baySize, direction, rules)
        {
            this.standPosition = standPosition;
            this.standYaw = standYaw;
            this.speedLimitKmh = speedLimitKmh;
        }

        public Vector3 StandPosition => standPosition;
        public Quaternion StandRotation => Quaternion.Euler(0f, standYaw, 0f);
        public bool HandedOver => handedOver;
        public float Rating => rating;
        public float SpeedLimitKmh => speedLimitKmh;
        public override Vector3 TargetPoint => handedOver ? position : standPosition;
        public float Quality => final >= 0f ? final : rating / 100f;
        public string ScoreLabel => $"GUEST RATING {Mathf.RoundToInt(Quality * 100f)}%";
        public override string FailDetail => "THE GUEST COMPLAINED";

        public override void Begin(MissionContext ctx)
        {
            context = ctx;
            handedOver = false;
            hold = 0f;
            rating = 100f;
            final = -1f;
            lastSpeed = 0f;
            stand = ctx.CreateZone("ValetStand_" + title, standPosition, StandRotation, standSize);
            ctx.Validator.SetArmed(false);
            ctx.SetActiveZone(stand);
            if (ctx.Collisions != null) ctx.Collisions.PenaltyRegistered += OnHit;
            ctx.Notify($"VALET: KEEP UNDER {speedLimitKmh:0} KM/H", false);
        }

        private void OnHit(Vehicle.CollisionHandler.Penalty p)
        {
            if (final >= 0f) return;
            rating -= perHit;
            context?.Notify($"GUEST RATING -{perHit:0}%", true);
        }

        public override ObjectiveState Tick(MissionContext ctx, float deltaTime)
        {
            UpdateRating(ctx, deltaTime);
            if (rating <= 0f) { rating = 0f; return ObjectiveState.Failed; }
            ctx.Gauge.Show("GUEST RATING", $"{Mathf.RoundToInt(rating)}%", rating / 100f,
                rating >= 80f ? GaugeSeverity.Good : rating >= 50f ? GaugeSeverity.Warn : GaugeSeverity.Danger);

            if (!handedOver)
            {
                bool inStand = InStand(ctx.Vehicle.transform.position) && ctx.Vehicle.SpeedKmh < 1.5f;
                hold = inStand ? hold + deltaTime : 0f;
                if (hold >= handoverSeconds)
                {
                    handedOver = true;
                    if (stand != null) stand.gameObject.SetActive(false);
                    ctx.Notify("KEYS RECEIVED - PARK THE GUEST'S CAR", false);
                    base.Begin(ctx);
                }
                return ObjectiveState.Running;
            }
            ObjectiveState s = base.Tick(ctx, deltaTime);
            if (s == ObjectiveState.Completed) final = rating / 100f;
            return s;
        }

        private void UpdateRating(MissionContext ctx, float dt)
        {
            if (dt <= 0f) return;
            float speed = ctx.Vehicle.Body != null ? ctx.Vehicle.Body.linearVelocity.magnitude : ctx.Vehicle.SpeedKmh / 3.6f;
            float accel = Mathf.Abs(speed - lastSpeed) / dt;
            lastSpeed = speed;
            warnCooldown -= dt;
            if (ctx.Vehicle.SpeedKmh > speedLimitKmh)
            {
                rating -= speedingPerSecond * dt;
                if (warnCooldown <= 0f) { ctx.Notify("TOO FAST FOR THE GUEST", true); warnCooldown = 2.5f; }
            }
            if (accel > harshAccel && accel < 60f)   // >60 m/s² is a collision/teleport, handled elsewhere
            {
                rating -= harshPerSecond * dt;
                if (warnCooldown <= 0f) { ctx.Notify("SMOOTHLY!", true); warnCooldown = 2.5f; }
            }
        }

        public bool InStand(Vector3 world)
        {
            Vector3 local = Quaternion.Inverse(StandRotation) * (world - standPosition);
            return Mathf.Abs(local.x) <= standSize.x * 0.5f && Mathf.Abs(local.z) <= standSize.y * 0.5f && Mathf.Abs(local.y) < 1.6f;
        }

        public override float Progress(MissionContext ctx) => handedOver ? base.Progress(ctx) : Mathf.Clamp01(hold / handoverSeconds);

        public override void End(MissionContext ctx)
        {
            if (ctx.Collisions != null) ctx.Collisions.PenaltyRegistered -= OnHit;
            if (stand != null) stand.gameObject.SetActive(false);
            base.End(ctx);
        }

        public override string Hint(MissionContext ctx)
        {
            if (!handedOver)
                return hold > 0f ? "HOLD STILL - HANDING OVER THE KEYS" : "PULL UP AT THE VALET STAND";
            return base.Hint(ctx);
        }
    }
}
