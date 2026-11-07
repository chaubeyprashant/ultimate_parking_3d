using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Get through a gate guarded by a <see cref="MovingBarrier"/> (bollards, sliding gate, swing arm): the area beyond
    /// the barrier completes the objective. The HUD counts down to the next opening and the open window left; every
    /// hit on the barrier is a strike and more than <see cref="maxBarrierHits"/> fails the mission.
    /// </summary>
    [Serializable, Preserve]
    public sealed class BarrierGateObjective : MissionObjective
    {
        [SerializeField] private string barrierId = "gate";
        [Tooltip("Area past the barrier that counts as through.")]
        [SerializeField] private Vector3 position;
        [SerializeField] private float yaw;
        [SerializeField] private Vector2 size = new Vector2(6f, 4f);
        [SerializeField, Min(0.2f)] private float heightTolerance = 1.6f;
        [Tooltip("Barrier hits allowed; one more fails. -1 = unlimited.")]
        [SerializeField] private int maxBarrierHits = 1;

        [NonSerialized] private MovingBarrier barrier;
        [NonSerialized] private Parking.ParkingZone zone;
        [NonSerialized] private MissionContext context;
        [NonSerialized] private MovingBarrier.Phase lastPhase;

        public BarrierGateObjective() { title = "Through the gate"; }

        public BarrierGateObjective(string title, string barrierId, Vector3 position, float yaw, Vector2 size, int maxBarrierHits = 1)
        {
            this.title = title;
            this.barrierId = barrierId;
            this.position = position;
            this.yaw = yaw;
            this.size = size;
            this.maxBarrierHits = maxBarrierHits;
        }

        public string BarrierId => barrierId;
        public Vector3 Position => position;
        public override Vector3 TargetPoint => position;
        public int BarrierHits { get; private set; }
        public MovingBarrier Barrier => barrier;
        public override string FailDetail => BarrierHits == 1 ? "HIT THE BARRIER" : $"HIT THE BARRIER {BarrierHits} TIMES";

        public override void Begin(MissionContext ctx)
        {
            context = ctx;
            BarrierHits = 0;
            barrier = MovingBarrier.Find(barrierId);
            if (barrier == null) Debug.LogError($"[Mission] No MovingBarrier '{barrierId}' in the scene.");
            else lastPhase = barrier.Current;
            zone = ctx.CreateZone("Gate_" + title, position, Quaternion.Euler(0f, yaw, 0f), size);
            ctx.Validator.SetArmed(false);
            ctx.SetActiveZone(zone);
            if (ctx.Collisions != null) ctx.Collisions.PenaltyRegistered += OnHit;
        }

        private void OnHit(Vehicle.CollisionHandler.Penalty p)
        {
            if (barrier == null || !barrier.Owns(p.Other)) return;
            BarrierHits++;
            context?.Notify(maxBarrierHits >= 0 ? $"BARRIER HIT {BarrierHits}/{maxBarrierHits}" : "BARRIER HIT", true);
        }

        public override ObjectiveState Tick(MissionContext ctx, float deltaTime)
        {
            if (maxBarrierHits >= 0 && BarrierHits > maxBarrierHits) return ObjectiveState.Failed;
            if (barrier != null)
            {
                if (barrier.Current != lastPhase)
                {
                    lastPhase = barrier.Current;
                    if (lastPhase == MovingBarrier.Phase.Open) ctx.Notify("BARRIER OPEN - GO!", false);
                    else if (lastPhase == MovingBarrier.Phase.Closing && !barrier.HeldBySensor) ctx.Notify("BARRIER CLOSING", true);
                }
                if (barrier.IsPassable)
                    ctx.Gauge.Show("BARRIER OPEN", $"{barrier.SecondsUntilClosing:0.0}s", Mathf.Clamp01(barrier.SecondsUntilClosing / 4f),
                        barrier.SecondsUntilClosing > 1.5f ? GaugeSeverity.Good : GaugeSeverity.Warn);
                else
                    ctx.Gauge.Show("OPENS IN", $"{barrier.SecondsUntilOpen:0.0}s", barrier.OpenFraction, GaugeSeverity.Danger);
            }
            return Contains(ctx.Vehicle.transform.position) ? ObjectiveState.Completed : ObjectiveState.Running;
        }

        public override void End(MissionContext ctx)
        {
            if (ctx.Collisions != null) ctx.Collisions.PenaltyRegistered -= OnHit;
            if (zone != null) zone.gameObject.SetActive(false);
            ctx.Gauge.Hide();
        }

        public override string Hint(MissionContext ctx)
        {
            if (barrier == null) return "DRIVE THROUGH THE GATE";
            if (barrier.IsPassable) return "GATE OPEN - DRIVE THROUGH";
            return barrier.Current == MovingBarrier.Phase.Closing ? "STOP - BARRIER CLOSING" : "WAIT FOR THE BARRIER";
        }

        public bool Contains(Vector3 world)
        {
            if (Mathf.Abs(world.y - position.y) > heightTolerance) return false;
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * (world - position);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f;
        }
    }
}
