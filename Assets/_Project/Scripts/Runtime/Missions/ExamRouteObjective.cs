using System;
using UnityEngine;
using UnityEngine.Scripting;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Missions
{
    /// <summary>One gate on an exam route: drive through it, or (stop line) come to a full stop inside it first.</summary>
    [Serializable]
    public struct ExamGate
    {
        public Vector3 position;
        public float yaw;
        public Vector2 size;
        [Tooltip("A stop line: the car must stand still inside the box for the stop time before it counts.")]
        public bool stop;

        public ExamGate(Vector3 position, float yaw, Vector2 size, bool stop = false)
        { this.position = position; this.yaw = yaw; this.size = size; this.stop = stop; }
    }

    /// <summary>
    /// Driving examination route with traffic rules: drive through the gates in order, never above the speed limit, and
    /// come to a full stop at every stop line. Breaking a rule is a fault (speeding is one fault per offence, after a
    /// short grace; rolling through a stop line without stopping is one fault). More than <see cref="maxFaults"/>
    /// faults fails the exam ("EXAM FAILED"). Score = 1 − 0.25 per fault.
    /// </summary>
    [Serializable, Preserve]
    public sealed class ExamRouteObjective : MissionObjective, IScoredObjective
    {
        [SerializeField] private ExamGate[] gates = Array.Empty<ExamGate>();
        [SerializeField, Min(5f)] private float speedLimitKmh = 20f;
        [Tooltip("Seconds over the limit before it counts as a speeding fault.")]
        [SerializeField, Min(0f)] private float speedingGrace = 1f;
        [SerializeField, Min(0.2f)] private float stopSeconds = 1f;
        [SerializeField, Min(0)] private int maxFaults = 2;
        [SerializeField, Min(0.2f)] private float heightTolerance = 1.6f;

        [NonSerialized] private int next;
        [NonSerialized] private int faults;
        [NonSerialized] private float overTime, stillTime;
        [NonSerialized] private bool speeding, stopDone, inStopBox;
        [NonSerialized] private ParkingZone zone;
        [NonSerialized] private string lastFault = "";

        public ExamRouteObjective() { title = "Driving exam"; }

        public ExamRouteObjective(string title, ExamGate[] gates, float speedLimitKmh = 20f, int maxFaults = 2, float stopSeconds = 1f)
        {
            this.title = title;
            this.gates = gates;
            this.speedLimitKmh = speedLimitKmh;
            this.maxFaults = maxFaults;
            this.stopSeconds = stopSeconds;
        }

        public int Faults => faults;
        public int NextGate => next;
        public int GateCount => gates.Length;
        public ExamGate Gate(int i) => gates[i];
        public float SpeedLimitKmh => speedLimitKmh;
        public float Quality => Mathf.Clamp01(1f - 0.25f * faults);
        public string ScoreLabel => faults == 0 ? "EXAM: NO FAULTS" : $"EXAM: {faults} FAULT{(faults == 1 ? "" : "S")}";
        public override string FailDetail => "EXAM FAILED: " + (lastFault.Length > 0 ? lastFault : "TOO MANY FAULTS");
        public override Vector3 TargetPoint => next < gates.Length ? gates[next].position : Vector3.zero;

        public override void Begin(MissionContext ctx)
        {
            next = 0; faults = 0; overTime = 0f; stillTime = 0f; speeding = false; stopDone = false; inStopBox = false; lastFault = "";
            ctx.Validator.SetArmed(false);
            ShowGate(ctx);
        }

        private void ShowGate(MissionContext ctx)
        {
            if (zone != null) zone.gameObject.SetActive(false);
            if (next >= gates.Length) return;
            ExamGate g = gates[next];
            zone = ctx.CreateZone($"ExamGate_{next + 1}", g.position, Quaternion.Euler(0f, g.yaw, 0f), g.size);
            ctx.SetActiveZone(zone);
            stillTime = 0f; stopDone = false; inStopBox = false;
        }

        public override ObjectiveState Tick(MissionContext ctx, float deltaTime)
        {
            float v = ctx.Vehicle.SpeedKmh;

            // Speed limit: one fault per offence once over the limit for longer than the grace.
            if (v > speedLimitKmh + 0.5f)
            {
                overTime += deltaTime;
                if (!speeding && overTime > speedingGrace) { speeding = true; Fault(ctx, $"SPEEDING: OVER {speedLimitKmh:0} KM/H"); }
            }
            else { overTime = 0f; speeding = false; }

            if (next < gates.Length)
            {
                ExamGate g = gates[next];
                bool inside = Contains(g, ctx.Vehicle.transform.position);
                if (g.stop)
                {
                    if (inside)
                    {
                        inStopBox = true;
                        stillTime = v <= 0.5f ? stillTime + deltaTime : 0f;
                        if (stillTime >= stopSeconds && !stopDone)
                        {
                            stopDone = true;
                            ctx.Gauge.Show("STOP", "OK", 1f, GaugeSeverity.Good);
                            next++; ShowGate(ctx);
                        }
                        else if (!stopDone) ctx.Gauge.Show("STOP", $"{Mathf.Min(stillTime, stopSeconds):0.0} s", stillTime / stopSeconds, GaugeSeverity.Warn);
                    }
                    else if (inStopBox && !stopDone)
                    {
                        // Left the stop box without stopping: a fault, and the line counts as passed.
                        Fault(ctx, "DID NOT STOP AT THE STOP LINE");
                        ctx.Gauge.Hide();
                        next++; ShowGate(ctx);
                    }
                }
                else if (inside) { next++; ShowGate(ctx); }
            }

            if (faults > maxFaults) return ObjectiveState.Failed;
            return next >= gates.Length ? ObjectiveState.Completed : ObjectiveState.Running;
        }

        private void Fault(MissionContext ctx, string what)
        {
            faults++;
            lastFault = what;
            ctx.Notify(what, true);
        }

        public override void End(MissionContext ctx)
        {
            if (zone != null) zone.gameObject.SetActive(false);
            ctx.Gauge.Hide();
        }

        public override string Hint(MissionContext ctx)
        {
            if (next >= gates.Length) return "";
            return gates[next].stop ? $"STOP FULLY AT THE LINE  ·  LIMIT {speedLimitKmh:0} KM/H" : $"FOLLOW THE ROUTE  ·  LIMIT {speedLimitKmh:0} KM/H";
        }

        public override float Progress(MissionContext ctx) => gates.Length == 0 ? 1f : next / (float)gates.Length;

        private bool Contains(ExamGate g, Vector3 world)
        {
            if (Mathf.Abs(world.y - g.position.y) > heightTolerance) return false;
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, g.yaw, 0f)) * (world - g.position);
            return Mathf.Abs(local.x) <= g.size.x * 0.5f && Mathf.Abs(local.z) <= g.size.y * 0.5f;
        }
    }
}
