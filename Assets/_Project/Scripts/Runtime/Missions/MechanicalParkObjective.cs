using System;
using UnityEngine;
using UnityEngine.Scripting;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// Mechanical parking: park precisely on the machine's platform (the bay), hold still, then the
    /// <see cref="MechanicalParkingSystem"/> with <see cref="systemId"/> runs its sequence with the car on board.
    /// Moving off during the safety check aborts it; the player re-parks and it starts again. Completes when locked.
    /// </summary>
    [Serializable, Preserve]
    public sealed class MechanicalParkObjective : ParkObjective
    {
        [SerializeField] private string systemId = "lift";
        [Tooltip("Aborted sequences allowed before the objective fails (0 = unlimited).")]
        [SerializeField, Min(0)] private int maxAborts = 3;

        [NonSerialized] private MechanicalParkingSystem system;
        [NonSerialized] private bool running, finished;
        [NonSerialized] private int aborts;

        public MechanicalParkObjective() { }

        public MechanicalParkObjective(string title, Vector3 position, float bayYaw, Vector2 size, string systemId, ParkingRules rules = null)
            : base(title, position, bayYaw, size, ParkDirection.NoseIn, rules)
        {
            this.systemId = systemId;
        }

        public string SystemId => systemId;
        public MechanicalParkingSystem System => system;
        public int Aborts => aborts;
        public override string FailDetail => "MACHINE ABORTED TOO MANY TIMES";

        public override void Begin(MissionContext ctx)
        {
            base.Begin(ctx);
            running = finished = false;
            aborts = 0;
            system = null;
            foreach (MechanicalParkingSystem s in UnityEngine.Object.FindObjectsByType<MechanicalParkingSystem>())
                if (s.SystemId == systemId) { system = s; break; }
            if (system == null) Debug.LogError($"[Mission] No MechanicalParkingSystem '{systemId}' in the scene.");
            else system.ResetMachine();
        }

        public override ObjectiveState Tick(MissionContext ctx, float deltaTime)
        {
            if (finished) return ObjectiveState.Completed;
            if (system == null) return ObjectiveState.Running;
            if (maxAborts > 0 && aborts > maxAborts) return ObjectiveState.Failed;
            if (!running && ctx.Validator.Status == ParkingStatus.Parked)
            {
                running = true;
                ctx.Validator.SetArmed(false);
                ctx.Notify("MECHANICAL PARKING STARTED", false);
                system.StartCoroutine(system.Run(ctx.Vehicle, ok =>
                {
                    running = false;
                    if (ok) { finished = true; return; }
                    aborts++;
                    ctx.Vehicle.ControlsEnabled = true;
                    ctx.Notify($"SEQUENCE ABORTED {aborts}/{maxAborts} - PARK AGAIN", true);
                    system.ResetMachine();
                    ctx.Validator.SetArmed(true);
                }));
            }
            if (running) ctx.Gauge.Show(system.Label(system.Current), $"{Mathf.RoundToInt(system.StepProgress * 100f)}%", system.StepProgress, GaugeSeverity.Good);
            return ObjectiveState.Running;
        }

        public override float Progress(MissionContext ctx) =>
            running && system != null ? system.StepProgress : base.Progress(ctx);

        public override string Hint(MissionContext ctx) =>
            running && system != null ? system.Label(system.Current)
            : ctx.Validator.Status == ParkingStatus.Outside ? "DRIVE ONTO THE LIFT PLATFORM" : base.Hint(ctx);
    }
}
