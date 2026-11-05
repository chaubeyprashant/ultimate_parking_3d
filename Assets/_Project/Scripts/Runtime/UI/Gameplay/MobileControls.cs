using UnityEngine;
using UltimateCarParking.Levels;
using UltimateCarParking.Settings;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// On-screen driving controls → the car's VehicleInput mobile methods.
    /// Left/Right ramp the steering toward full lock at a rate scaled by the Steering Sensitivity setting and return
    /// to centre when released. Gas = forward throttle. Brake/Reverse = negative throttle: brakes while rolling
    /// forward, then reverses (the controller's own logic). Handbrake is optional.
    /// Runs Update only while a steering button is held or the wheel is returning to centre.
    /// </summary>
    public sealed class MobileControls : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private HoldButton left;
        [SerializeField] private HoldButton right;
        [SerializeField] private HoldButton gas;
        [SerializeField] private HoldButton brakeReverse;
        [SerializeField] private HoldButton handbrake;
        [Tooltip("Steering change per second at sensitivity 1 (full lock in 1/rate seconds).")]
        [SerializeField, Min(0.1f)] private float steerRate = 2.2f;
        [Tooltip("Return-to-centre speed relative to the steer rate.")]
        [SerializeField, Min(0.1f)] private float returnMultiplier = 1.6f;

        private VehicleInput input;
        private float steer;

        public float Steering => steer;
        public VehicleInput Target => input;

        // Event subscriptions live in Awake/OnDestroy: the component disables itself whenever the wheel is centred
        // (no Update cost), and must keep listening for the level start while disabled.
        private void Awake()
        {
            Hook(left, Refresh, Refresh);
            Hook(right, Refresh, Refresh);
            Hook(gas, Pedals, Pedals);
            Hook(brakeReverse, Pedals, Pedals);
            Hook(handbrake, Pedals, Pedals);
            if (levelManager == null) Debug.LogError("[MobileControls] No LevelManager assigned.", this);
            else
            {
                levelManager.LevelStarted += OnLevelStarted;
                if (levelManager.Vehicle != null) Bind(levelManager.Vehicle.Input);
            }
            enabled = false;
        }

        private void OnDestroy()
        {
            if (levelManager != null) levelManager.LevelStarted -= OnLevelStarted;
        }

        private void OnLevelStarted(LevelData _) => Bind(levelManager.Vehicle != null ? levelManager.Vehicle.Input : null);

        /// <summary>Points the controls at a car (called automatically when a level starts).</summary>
        public void Bind(VehicleInput target)
        {
            input = target;
            steer = 0f;
            Pedals();
            Refresh();
        }

        private static void Hook(HoldButton b, System.Action down, System.Action up)
        {
            if (b == null) return;
            b.Pressed += down;
            b.Released += up;
        }

        private float SteerTarget => (right != null && right.IsHeld ? 1f : 0f) - (left != null && left.IsHeld ? 1f : 0f);

        private void Refresh() => enabled = SteerTarget != 0f || steer != 0f;

        private void Pedals()
        {
            if (input == null) return;
            float throttle = (gas != null && gas.IsHeld ? 1f : 0f) - (brakeReverse != null && brakeReverse.IsHeld ? 1f : 0f);
            input.SetThrottle(throttle);
            input.SetHandbrake(handbrake != null && handbrake.IsHeld);
        }

        private void Update()
        {
            float target = SteerTarget;
            float rate = steerRate * SettingsService.Current.steeringSensitivity;
            bool returning = Mathf.Abs(target) < Mathf.Abs(steer) || (target != 0f && Mathf.Sign(target) != Mathf.Sign(steer));
            steer = Mathf.MoveTowards(steer, target, rate * (returning ? returnMultiplier : 1f) * Time.unscaledDeltaTime);
            if (input != null) input.SetSteering(steer);
            if (target == 0f && steer == 0f) enabled = false;
        }

        /// <summary>Releases everything (pause, level end).</summary>
        public void ReleaseAll()
        {
            foreach (HoldButton b in new[] { left, right, gas, brakeReverse, handbrake }) if (b != null) b.SetHeld(false);
            steer = 0f;
            if (input != null) input.ResetMobileInput();
            enabled = false;
        }
    }
}
