using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UltimateCarParking.Levels;
using UltimateCarParking.Settings;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// On-screen driving controls → the car's <see cref="VehicleInput"/> mobile methods. The keyboard still works
    /// alongside (VehicleInput sums both).
    ///
    /// Steering (Settings → Controls): steering wheel (default), Left/Right buttons, or device tilt.
    /// Pedals: Gears (default: Gas, Brake, Handbrake and a P / R / D selector; the brake never reverses and gears change
    /// only when the car is nearly stopped) or Classic (the original Brake/Reverse button, now guarded so a press made
    /// while rolling forward only brakes until it is released).
    /// Every control tracks its own finger, so steering, pedals and gear buttons work together (multi-touch).
    /// </summary>
    public sealed class MobileControls : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [Header("Steering")]
        [SerializeField] private HoldButton left;
        [SerializeField] private HoldButton right;
        [SerializeField] private SteeringWheel wheel;
        [Tooltip("Tilt mode: sets the current phone angle as straight ahead.")]
        [SerializeField] private Button tiltCentreButton;
        [SerializeField] private RectTransform tiltIndicator;
        [Header("Pedals")]
        [SerializeField] private HoldButton gas;
        [SerializeField] private HoldButton brake;
        [SerializeField] private HoldButton handbrake;
        [Tooltip("Classic mode only: brake, then reverse from a stop.")]
        [SerializeField] private HoldButton brakeReverse;
        [Header("Gear selector (P / R / D)")]
        [SerializeField] private Button gearPark;
        [SerializeField] private Button gearReverse;
        [SerializeField] private Button gearDrive;
        [Tooltip("Steering change per second at sensitivity 1 for the Left/Right buttons (full lock in 1/rate seconds).")]
        [SerializeField, Min(0.1f)] private float steerRate = 2.2f;
        [Tooltip("Return-to-centre speed relative to the steer rate (buttons).")]
        [SerializeField, Min(0.1f)] private float returnMultiplier = 1.6f;

        private VehicleInput input;
        private float steer;
        private readonly ClassicBrakeReverse classic = new ClassicBrakeReverse();
        private readonly TiltSteer tilt = new TiltSteer();
        private float lastRoll;
        private bool hasRoll;

        /// <summary>Raised with the new gear after a successful change.</summary>
        public event Action<DriveGear> GearChanged;
        /// <summary>Raised when a gear change is refused because the car is still moving.</summary>
        public event Action ShiftRefused;

        public float Steering => steer;
        public VehicleInput Target => input;
        public DriveGear Gear { get; private set; } = DriveGear.Drive;
        public int SteeringMode => SettingsService.Current.steeringMode;
        public bool UsesGears => SettingsService.Current.pedalMode == GameSettings.PedalsGears;
        public SteeringWheel Wheel => wheel;
        /// <summary>Latest phone roll (degrees, clockwise positive) when tilt steering has a reading.</summary>
        public float CurrentRoll => lastRoll;
        public bool HasTiltReading => hasRoll;

        private VehicleController Car => levelManager != null ? levelManager.Vehicle : null;

        private void Awake()
        {
            if (gearPark != null) gearPark.onClick.AddListener(() => RequestGear(DriveGear.Park));
            if (gearReverse != null) gearReverse.onClick.AddListener(() => RequestGear(DriveGear.Reverse));
            if (gearDrive != null) gearDrive.onClick.AddListener(() => RequestGear(DriveGear.Drive));
            if (tiltCentreButton != null) tiltCentreButton.onClick.AddListener(CalibrateTilt);
            if (levelManager == null) Debug.LogError("[MobileControls] No LevelManager assigned.", this);
            else
            {
                levelManager.LevelStarted += OnLevelStarted;
                if (levelManager.Vehicle != null) Bind(levelManager.Vehicle.Input);
            }
            RenderGears();
        }

        private void OnDestroy()
        {
            if (levelManager != null) levelManager.LevelStarted -= OnLevelStarted;
            SetAccelerometer(false);
        }

        private void OnLevelStarted(LevelData _) => Bind(Car != null ? Car.Input : null);

        /// <summary>Points the controls at a car (called automatically when a level starts). Starts in D.</summary>
        public void Bind(VehicleInput target)
        {
            input = target;
            steer = 0f;
            classic.Reset();
            tilt.Reset();
            if (wheel != null) wheel.ResetWheel();
            Gear = DriveGear.Drive;
            RenderGears();
            SendAll();
        }

        /// <summary>Selects a gear. Refused (returns false, raises ShiftRefused) unless the car is nearly stopped.</summary>
        public bool RequestGear(DriveGear gear)
        {
            if (gear == Gear) return true;
            float speed = Car != null ? Car.ForwardSpeedKmh : 0f;
            if (!GearRules.CanShift(speed))
            {
                ShiftRefused?.Invoke();
                return false;
            }
            Gear = gear;
            RenderGears();
            GearChanged?.Invoke(gear);
            return true;
        }

        /// <summary>Tilt mode: the current phone angle becomes straight ahead (saved with the settings).</summary>
        public void CalibrateTilt()
        {
            TiltInput.Calibrate();
            tilt.Reset();
        }

        private void Update()
        {
            GameSettings s = SettingsService.Current;
            float dt = Time.unscaledDeltaTime;

            // ---- steering
            int mode = s.steeringMode;
            SetAccelerometer(mode == GameSettings.SteerTilt);
            if (mode == GameSettings.SteerWheel && wheel != null)
            {
                wheel.Sensitivity = s.steeringSensitivity;
                steer = wheel.Output;
            }
            else if (mode == GameSettings.SteerTilt)
            {
                hasRoll = TryReadRoll(out float roll);
                if (hasRoll) lastRoll = roll;
                steer = hasRoll ? tilt.Step(roll, s.tiltZero, s.steeringSensitivity, dt) : Mathf.MoveTowards(steer, 0f, dt * 4f);
                if (tiltIndicator != null) tiltIndicator.localEulerAngles = new Vector3(0f, 0f, -steer * 90f);
            }
            else
            {
                float target = (right != null && right.IsHeld ? 1f : 0f) - (left != null && left.IsHeld ? 1f : 0f);
                float rate = steerRate * s.steeringSensitivity;
                bool returning = Mathf.Abs(target) < Mathf.Abs(steer) || (target != 0f && Mathf.Sign(target) != Mathf.Sign(steer));
                steer = Mathf.MoveTowards(steer, target, rate * (returning ? returnMultiplier : 1f) * dt);
            }

            // ---- pedals
            bool g = gas != null && gas.IsHeld, hb = handbrake != null && handbrake.IsHeld;
            PedalCommand cmd = s.pedalMode == GameSettings.PedalsGears
                ? GearRules.Gears(Gear, g, brake != null && brake.IsHeld, hb)
                : classic.Step(g, brakeReverse != null && brakeReverse.IsHeld, hb, Car != null ? Car.ForwardSpeedKmh : 0f);

            if (input == null) return;
            // Write only when something changed, so scripts and tests that drive VehicleInput directly are not overridden
            // every frame by idle touch controls (the old controls were event-driven for the same reason).
            if (steer != sentSteer) { sentSteer = steer; input.SetSteering(steer); }
            if (cmd.Throttle != sent.Throttle || cmd.Brake != sent.Brake || cmd.Handbrake != sent.Handbrake)
            {
                sent = cmd;
                input.SetThrottle(cmd.Throttle);
                input.SetBrake(cmd.Brake);
                input.SetHandbrake(cmd.Handbrake);
            }
        }

        private float sentSteer;
        private PedalCommand sent;

        /// <summary>Sends the current controls once (binding a new car, after a reset).</summary>
        private void SendAll()
        {
            if (input == null) return;
            sentSteer = steer; sent = default;
            input.SetSteering(0f); input.SetThrottle(0f); input.SetBrake(0f); input.SetHandbrake(false);
        }

        /// <summary>Releases everything (pause, level end). The selected gear is kept.</summary>
        public void ReleaseAll()
        {
            foreach (HoldButton b in new[] { left, right, gas, brake, handbrake, brakeReverse }) if (b != null) b.SetHeld(false);
            if (wheel != null) wheel.ResetWheel();
            steer = 0f;
            classic.Reset();
            tilt.Reset();
            if (input != null) input.ResetMobileInput();
            sentSteer = 0f; sent = default;
        }

        private void RenderGears()
        {
            Paint(gearPark, Gear == DriveGear.Park);
            Paint(gearReverse, Gear == DriveGear.Reverse);
            Paint(gearDrive, Gear == DriveGear.Drive);
        }

        private static void Paint(Button b, bool selected)
        {
            if (b == null) return;
            if (b.targetGraphic != null) b.targetGraphic.color = selected ? UiPalette.TextPrimary : UiPalette.Surface.WithAlpha(0.55f);
            TMP_Text t = b.GetComponentInChildren<TMP_Text>();
            if (t != null) t.color = selected ? UiPalette.Background : UiPalette.TextSecondary;
        }

        private void SetAccelerometer(bool on) => TiltInput.Enable(on);
        private static bool TryReadRoll(out float roll) => TiltInput.TryReadRoll(out roll);
    }
}

namespace UltimateCarParking.UI
{
    /// <summary>Phone accelerometer for tilt steering: switched on only while tilt steering is in use.</summary>
    public static class TiltInput
    {
        private static bool on;

        public static bool Enabled => on;

        public static void Enable(bool enable)
        {
            if (enable == on) return;
            Accelerometer acc = Accelerometer.current;
            if (acc != null) { if (enable) InputSystem.EnableDevice(acc); else InputSystem.DisableDevice(acc); }
            on = enable;
        }

        /// <summary>Phone roll like a steering wheel (degrees, clockwise positive). False with no accelerometer reading.</summary>
        public static bool TryReadRoll(out float roll)
        {
            roll = 0f;
            Accelerometer acc = Accelerometer.current;
            if (acc == null || !acc.enabled) return false;
            Vector3 g = acc.acceleration.ReadValue();
            if (g.sqrMagnitude < 0.04f) return false;
            ScreenOrientation o = InputSystem.settings.compensateForScreenOrientation ? ScreenOrientation.Portrait : Screen.orientation;
            roll = TiltSteer.RollFromGravity(g, o);
            return true;
        }

        /// <summary>Saves the current phone angle as straight ahead. False if the phone gives no reading.</summary>
        public static bool Calibrate()
        {
            Enable(true);
            if (!TryReadRoll(out float roll)) return false;
            SettingsService.SetTiltZero(roll);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => on = false;
    }
}
