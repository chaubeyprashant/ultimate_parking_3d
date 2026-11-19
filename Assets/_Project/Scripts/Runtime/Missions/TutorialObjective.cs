using System;
using UnityEngine;
using UnityEngine.Scripting;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Settings;

namespace UltimateCarParking.Missions
{
    public enum TutorialAction { SteerBothWays, Accelerate, BrakeToStop, Reverse, BackUpToObstacle, SwitchCamera }

    /// <summary>
    /// Driving Academy step: one short instruction on the HUD, completed by actually doing it (no text screens).
    /// The wording follows the player's pedal setting (Simple: hold BRAKE/REVERSE; Gears: tap R).
    /// </summary>
    [Serializable, Preserve]
    public sealed class TutorialObjective : MissionObjective
    {
        [SerializeField] private TutorialAction action;
        [Tooltip("Speed (Accelerate), metres (Reverse) or distance to the obstacle (BackUpToObstacle).")]
        [SerializeField] private float amount = 10f;

        [NonSerialized] private bool leftDone, rightDone, wasMoving;
        [NonSerialized] private Vector3 start;
        [NonSerialized] private int viewChanges;
        [NonSerialized] private CameraView lastView;
        [NonSerialized] private VehicleCamera cam;
        [NonSerialized] private ParkingSensor sensor;
        [NonSerialized] private float lastDistance = float.PositiveInfinity;

        public TutorialObjective() { }
        public TutorialObjective(string title, TutorialAction action, float amount = 10f) { this.title = title; this.action = action; this.amount = amount; }

        public TutorialAction Action => action;

        public override void Begin(MissionContext ctx)
        {
            leftDone = rightDone = wasMoving = false;
            viewChanges = 0;
            start = ctx.Vehicle.transform.position;
            ctx.Validator.SetArmed(false);
            if (action == TutorialAction.SwitchCamera)
            {
                cam = UnityEngine.Object.FindAnyObjectByType<VehicleCamera>();
                lastView = cam != null ? cam.View : CameraView.Chase;
            }
            if (action == TutorialAction.BackUpToObstacle)
            {
                sensor = new ParkingSensor(rear: true);
                ctx.Validator.EnsureFootprint(ctx.Vehicle);
                sensor.Calibrate(ctx.Validator.LocalFootprint);
            }
        }

        public override ObjectiveState Tick(MissionContext ctx, float deltaTime)
        {
            var car = ctx.Vehicle;
            float steer = car.Input != null ? car.Input.Steering : 0f;
            switch (action)
            {
                case TutorialAction.SteerBothWays:
                    if (steer <= -0.8f) leftDone = true;
                    if (leftDone && steer >= 0.8f) rightDone = true;
                    return leftDone && rightDone ? ObjectiveState.Completed : ObjectiveState.Running;
                case TutorialAction.Accelerate:
                    return car.ForwardSpeedKmh >= amount ? ObjectiveState.Completed : ObjectiveState.Running;
                case TutorialAction.BrakeToStop:
                    if (car.SpeedKmh > 3f) wasMoving = true;
                    return wasMoving && car.SpeedKmh < 0.3f ? ObjectiveState.Completed : ObjectiveState.Running;
                case TutorialAction.Reverse:
                    float back = -Vector3.Dot(car.transform.position - start, car.transform.forward);
                    return back >= amount ? ObjectiveState.Completed : ObjectiveState.Running;
                case TutorialAction.BackUpToObstacle:
                    lastDistance = sensor.Measure(car);
                    if (!float.IsInfinity(lastDistance))
                        ctx.Gauge.Show("REAR SENSOR", $"{lastDistance:0.0} m", 1f - Mathf.Clamp01(lastDistance / ParkingSensor.Range),
                            lastDistance > 1.2f ? GaugeSeverity.Good : lastDistance > amount ? GaugeSeverity.Warn : GaugeSeverity.Danger);
                    return lastDistance <= amount && car.SpeedKmh < 0.5f ? ObjectiveState.Completed : ObjectiveState.Running;
                case TutorialAction.SwitchCamera:
                    if (cam != null && cam.View != lastView) { viewChanges++; lastView = cam.View; }
                    return viewChanges >= 2 ? ObjectiveState.Completed : ObjectiveState.Running;
            }
            return ObjectiveState.Running;
        }

        public override void End(MissionContext ctx) => ctx.Gauge.Hide();

        public override float Progress(MissionContext ctx) => action switch
        {
            TutorialAction.SteerBothWays => (leftDone ? 0.5f : 0f) + (rightDone ? 0.5f : 0f),
            TutorialAction.SwitchCamera => viewChanges / 2f,
            _ => 0f,
        };

        public override string Hint(MissionContext ctx)
        {
            bool gears = SettingsService.Current.pedalMode == GameSettings.PedalsGears;
            return action switch
            {
                TutorialAction.SteerBothWays => !leftDone ? "TURN THE WHEEL FULLY LEFT" : "NOW FULLY RIGHT",
                TutorialAction.Accelerate => $"HOLD GAS TO REACH {amount:0} KM/H",
                TutorialAction.BrakeToStop => "PRESS BRAKE TO STOP",
                TutorialAction.Reverse => gears ? $"STOP, TAP R, THEN GAS: BACK UP {amount:0} M" : $"STOP, THEN HOLD BRAKE/REVERSE: BACK UP {amount:0} M",
                TutorialAction.BackUpToObstacle => $"REVERSE SLOWLY: STOP WHEN THE SENSOR READS UNDER {amount:0.0} M",
                TutorialAction.SwitchCamera => "TAP VIEW TWICE TO TRY THE CAMERAS",
                _ => "",
            };
        }
    }
}
