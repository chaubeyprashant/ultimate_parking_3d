using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Narrow alley: drive (or reverse) from the alley's mouth to its end between walls barely wider than the car.
    /// Side sensors measure the gap to the walls on both sides; the tightest gap kept is the score. Touching a wall
    /// inside the alley is a scrape, and more than <see cref="maxScrapes"/> fails the mission. With
    /// <see cref="reverseThrough"/> the car must arrive facing back towards the mouth (reversed the whole way).
    /// </summary>
    [Serializable, Preserve]
    public sealed class NarrowAlleyObjective : MissionObjective, IScoredObjective
    {
        [SerializeField] private Vector3 mouth;
        [SerializeField] private Vector3 end;
        [Tooltip("Wall-to-wall width, metres (for the gauge and the finish area).")]
        [SerializeField, Min(1.5f)] private float width = 3f;
        [Tooltip("Side gap that scores full marks, metres.")]
        [SerializeField, Min(0.05f)] private float goodClearance = 0.3f;
        [Tooltip("Wall scrapes allowed; one more fails. -1 = unlimited.")]
        [SerializeField] private int maxScrapes = 2;
        [SerializeField] private bool reverseThrough;

        [NonSerialized] private MissionContext context;
        [NonSerialized] private Parking.ParkingZone zone;
        [NonSerialized] private float minClearance;
        [NonSerialized] private float final = -1f;
        [NonSerialized] private bool wrongWayWarned;

        public NarrowAlleyObjective() { title = "Through the alley"; }

        public NarrowAlleyObjective(string title, Vector3 mouth, Vector3 end, float width, int maxScrapes = 2, bool reverseThrough = false, float goodClearance = 0.3f)
        {
            this.title = title;
            this.mouth = mouth;
            this.end = end;
            this.width = width;
            this.maxScrapes = maxScrapes;
            this.reverseThrough = reverseThrough;
            this.goodClearance = goodClearance;
        }

        public Vector3 Mouth => mouth;
        public Vector3 Exit => end;
        public bool ReverseThrough => reverseThrough;
        public override Vector3 TargetPoint => end;
        public int Scrapes { get; private set; }
        public float LeftGap { get; private set; } = float.PositiveInfinity;
        public float RightGap { get; private set; } = float.PositiveInfinity;
        /// <summary>Tightest side gap seen inside the alley (∞ before entering).</summary>
        public float MinClearance => minClearance;
        public float Quality => final >= 0f ? final : Grade();
        public string ScoreLabel => float.IsInfinity(minClearance) ? "CLEAN RUN" : $"TIGHTEST GAP {Mathf.RoundToInt(minClearance * 100f)}cm";
        public override string FailDetail => $"SCRAPED THE WALLS {Scrapes} TIMES";

        private Vector3 Axis => (end - mouth).WithY0().normalized;
        private float Length => (end - mouth).WithY0().magnitude;
        private Quaternion Frame => Quaternion.LookRotation(Axis, Vector3.up);

        public override void Begin(MissionContext ctx)
        {
            context = ctx;
            Scrapes = 0;
            minClearance = float.PositiveInfinity;
            final = -1f;
            wrongWayWarned = false;
            zone = ctx.CreateZone("Alley_" + title, end - Axis * 1.5f, Frame, new Vector2(width - 0.2f, 3f));
            ctx.Validator.SetArmed(false);
            ctx.Validator.EnsureFootprint(ctx.Vehicle);
            ctx.SetActiveZone(zone);
            if (ctx.Collisions != null) ctx.Collisions.PenaltyRegistered += OnHit;
        }

        private float Along(Vector3 world) => Vector3.Dot((world - mouth).WithY0(), Axis);
        private bool InAlley(Vector3 world) => Along(world) > -0.5f && Along(world) < Length + 0.5f &&
            Mathf.Abs(Vector3.Dot((world - mouth).WithY0(), Vector3.Cross(Vector3.up, Axis))) < width * 0.5f + 0.5f;

        private void OnHit(Vehicle.CollisionHandler.Penalty p)
        {
            if (context == null || !InAlley(context.Vehicle.transform.position)) return;
            Scrapes++;
            context.Notify(maxScrapes >= 0 ? $"WALL SCRAPE {Scrapes}/{maxScrapes}" : "WALL SCRAPE", true);
        }

        public override ObjectiveState Tick(MissionContext ctx, float deltaTime)
        {
            if (maxScrapes >= 0 && Scrapes > maxScrapes) return ObjectiveState.Failed;
            Transform car = ctx.Vehicle.transform;
            if (InAlley(car.position))
            {
                MeasureSides(ctx);
                float gap = Mathf.Min(LeftGap, RightGap);
                if (!float.IsInfinity(gap)) minClearance = Mathf.Min(minClearance, gap);
                ctx.Gauge.Show("SIDE GAP", $"L {Fmt(LeftGap)}  R {Fmt(RightGap)}", Mathf.Clamp01(gap / (goodClearance * 2f)),
                    gap >= goodClearance ? GaugeSeverity.Good : gap >= goodClearance * 0.4f ? GaugeSeverity.Warn : GaugeSeverity.Danger);
                if (reverseThrough && ctx.Vehicle.ForwardSpeedKmh > 2f && Along(car.position) > 1f && !wrongWayWarned)
                {
                    wrongWayWarned = true;
                    ctx.Notify("REVERSE THROUGH THE ALLEY", true);
                }
            }
            else ctx.Gauge.Hide();

            bool atEnd = Along(car.position) >= Length - 3f && InAlley(car.position);
            if (!atEnd) return ObjectiveState.Running;
            bool facingBack = Vector3.Dot(car.forward.WithY0().normalized, Axis) < -0.7f;
            if (reverseThrough && !facingBack) return ObjectiveState.Running;
            final = Grade();
            return ObjectiveState.Completed;
        }

        private float Grade()
        {
            float clearance = float.IsInfinity(minClearance) ? 1f : Mathf.Clamp01(minClearance / goodClearance);
            return Mathf.Clamp01(clearance * (Scrapes == 0 ? 1f : Scrapes == 1 ? 0.6f : 0.35f));
        }

        /// <summary>Casts sideways from the car's flanks (front, middle, rear) to the nearest wall.</summary>
        private void MeasureSides(MissionContext ctx)
        {
            Vector3[] f = ctx.Validator.LocalFootprint;
            Transform car = ctx.Vehicle.transform;
            if (f == null || f.Length < 4) return;
            float halfW = 0f, halfL = 0f;
            foreach (Vector3 c in f) { halfW = Mathf.Max(halfW, Mathf.Abs(c.x)); halfL = Mathf.Max(halfL, Mathf.Abs(c.z)); }
            LeftGap = Cast(car, -1f, halfW, halfL, ctx.Vehicle.Body);
            RightGap = Cast(car, 1f, halfW, halfL, ctx.Vehicle.Body);
        }

        private static float Cast(Transform car, float side, float halfW, float halfL, Rigidbody self)
        {
            float best = float.PositiveInfinity;
            foreach (float z in new[] { -halfL * 0.8f, 0f, halfL * 0.8f })
            {
                Vector3 origin = car.TransformPoint(new Vector3(side * (halfW - 0.05f), 0.5f, z));
                Vector3 dir = car.right * side;
                foreach (RaycastHit h in Physics.RaycastAll(origin, dir, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                    if (h.rigidbody != self && h.distance - 0.05f < best) best = Mathf.Max(0f, h.distance - 0.05f);
            }
            return best;
        }

        private static string Fmt(float m) => float.IsInfinity(m) ? "--" : $"{Mathf.RoundToInt(m * 100f)}cm";

        public override void End(MissionContext ctx)
        {
            if (ctx.Collisions != null) ctx.Collisions.PenaltyRegistered -= OnHit;
            if (zone != null) zone.gameObject.SetActive(false);
            ctx.Gauge.Hide();
        }

        public override string Hint(MissionContext ctx)
        {
            if (context == null) return "";
            Vector3 p = ctx.Vehicle.transform.position;
            if (!InAlley(p)) return reverseThrough ? "LINE UP AND REVERSE INTO THE ALLEY" : "DRIVE INTO THE ALLEY";
            if (Along(p) >= Length - 3f && reverseThrough && Vector3.Dot(ctx.Vehicle.transform.forward.WithY0().normalized, Axis) > -0.7f)
                return "WRONG WAY - REVERSE IN";
            float gap = Mathf.Min(LeftGap, RightGap);
            if (gap < goodClearance * 0.4f) return LeftGap < RightGap ? "STEER AWAY FROM THE LEFT WALL" : "STEER AWAY FROM THE RIGHT WALL";
            return reverseThrough ? "KEEP REVERSING - STAY CENTRED" : "KEEP GOING - STAY CENTRED";
        }
    }

    internal static class AlleyVectorExt
    {
        public static Vector3 WithY0(this Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
