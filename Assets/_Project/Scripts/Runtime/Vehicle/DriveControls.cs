using UnityEngine;

namespace UltimateCarParking.Vehicle
{
    /// <summary>The selected gear on the on-screen gear selector.</summary>
    public enum DriveGear { Park, Reverse, Drive }

    /// <summary>What the pedals send to <see cref="VehicleInput"/> for one frame.</summary>
    public struct PedalCommand
    {
        public float Throttle;   // -1..1 (negative = reverse drive)
        public float Brake;      // 0..1 foot brake
        public bool Handbrake;
    }

    /// <summary>
    /// Pure pedal and gear rules for the on-screen controls (no MonoBehaviour, so they are unit tested directly).
    /// Two modes:
    /// <list type="bullet">
    /// <item>Gears (default): separate gas, foot brake and handbrake plus a D / R / P selector. The brake never reverses;
    /// gas follows the selected gear; gears change only when the car is nearly stopped.</item>
    /// <item>Classic: the original combined Brake/Reverse button. Safer than before: if the button was pressed while the
    /// car was still rolling forward, it only brakes (even after the stop) until it is released and pressed again.</item>
    /// </list>
    /// </summary>
    public static class GearRules
    {
        /// <summary>Highest speed (km/h, either direction) at which the selector accepts a gear change.</summary>
        public const float ShiftSpeedKmh = 1.5f;

        public static bool CanShift(float forwardSpeedKmh) => Mathf.Abs(forwardSpeedKmh) <= ShiftSpeedKmh;

        public static string Letter(DriveGear g) => g == DriveGear.Park ? "P" : g == DriveGear.Reverse ? "R" : "D";

        /// <summary>Gears mode: separate pedals. Park holds the car with the foot brake and handbrake.</summary>
        public static PedalCommand Gears(DriveGear gear, bool gas, bool brake, bool handbrake)
        {
            if (gear == DriveGear.Park) return new PedalCommand { Throttle = 0f, Brake = 1f, Handbrake = true };
            float g = gas ? 1f : 0f;
            return new PedalCommand
            {
                Throttle = gear == DriveGear.Reverse ? -g : g,
                Brake = brake ? 1f : 0f,
                Handbrake = handbrake,
            };
        }
    }

    /// <summary>
    /// Classic combined Brake/Reverse button with the accidental-reverse guard. Feed it every frame.
    /// </summary>
    public sealed class ClassicBrakeReverse
    {
        /// <summary>Forward speed (km/h) above which a press is treated as "brake only" for the whole hold.</summary>
        public const float RollingKmh = 0.8f;

        private bool wasHeld;
        private bool brakeOnlyHold;

        /// <summary>True while the current hold only brakes (it started while the car was rolling forward).</summary>
        public bool BrakeOnly => brakeOnlyHold;

        public PedalCommand Step(bool gas, bool brakeReverse, bool handbrake, float forwardSpeedKmh)
        {
            if (brakeReverse && !wasHeld) brakeOnlyHold = forwardSpeedKmh > RollingKmh;
            if (!brakeReverse) brakeOnlyHold = false;
            wasHeld = brakeReverse;

            float throttle = gas ? 1f : 0f, brake = 0f;
            if (brakeReverse)
            {
                if (brakeOnlyHold) brake = 1f;          // stops and holds; release and press again to reverse
                else throttle -= 1f;                    // pressed from a stop: reverse (the controller brakes first if needed)
            }
            return new PedalCommand { Throttle = Mathf.Clamp(throttle, -1f, 1f), Brake = brake, Handbrake = handbrake };
        }

        public void Reset() { wasHeld = false; brakeOnlyHold = false; }
    }

    /// <summary>
    /// Tilt steering: device roll → steering with a calibrated centre, a dead zone and exponential smoothing.
    /// </summary>
    public sealed class TiltSteer
    {
        public const float DeadZoneDeg = 2.5f;
        /// <summary>Roll (degrees past the dead zone) that gives full lock at sensitivity 1.</summary>
        public const float FullLockDeg = 26f;
        public const float Smoothing = 12f;

        private float value;
        public float Value => value;

        public void Reset() => value = 0f;

        /// <summary>Raw target for a roll angle (degrees, positive = rotated clockwise) relative to the calibrated zero.</summary>
        public static float Target(float rollDeg, float zeroDeg, float sensitivity)
        {
            float d = Mathf.DeltaAngle(zeroDeg, rollDeg);
            float past = Mathf.Max(0f, Mathf.Abs(d) - DeadZoneDeg);
            float lockDeg = FullLockDeg / Mathf.Max(0.1f, sensitivity);
            return Mathf.Clamp(Mathf.Sign(d) * past / lockDeg, -1f, 1f);
        }

        public float Step(float rollDeg, float zeroDeg, float sensitivity, float dt)
        {
            float t = Target(rollDeg, zeroDeg, sensitivity);
            value = Mathf.Lerp(value, t, 1f - Mathf.Exp(-Smoothing * Mathf.Max(0f, dt)));
            if (Mathf.Abs(value - t) < 1e-4f) value = t;
            return value;
        }

        /// <summary>
        /// Roll of the phone like a steering wheel (degrees, clockwise positive) from the gravity vector in device
        /// axes (portrait natural orientation: x right, y up, z out of the screen), for the current screen orientation.
        /// </summary>
        public static float RollFromGravity(Vector3 g, ScreenOrientation orientation)
        {
            switch (orientation)
            {
                case ScreenOrientation.LandscapeLeft: return Mathf.Atan2(-g.y, -g.x) * Mathf.Rad2Deg;
                case ScreenOrientation.LandscapeRight: return Mathf.Atan2(g.y, g.x) * Mathf.Rad2Deg;
                case ScreenOrientation.PortraitUpsideDown: return Mathf.Atan2(-g.x, g.y) * Mathf.Rad2Deg;
                default: return Mathf.Atan2(g.x, -g.y) * Mathf.Rad2Deg;
            }
        }
    }

    /// <summary>Steering-wheel maths: pointer rotation → wheel angle → steering value.</summary>
    public static class WheelSteer
    {
        /// <summary>Most the wheel can be turned either way (degrees), like a real wheel's lock-to-lock / 2.</summary>
        public const float MaxRotationDeg = 450f;
        /// <summary>Wheel rotation that gives full steering lock at sensitivity 1.</summary>
        public const float FullLockDeg = 270f;
        /// <summary>Self-centring speed when released (degrees per second).</summary>
        public const float ReturnDegPerSec = 900f;

        /// <summary>Clockwise rotation between two pointer directions around the wheel centre (degrees, signed).</summary>
        public static float ClockwiseDelta(Vector2 fromDir, Vector2 toDir)
        {
            float a = Mathf.Atan2(fromDir.y, fromDir.x) * Mathf.Rad2Deg;
            float b = Mathf.Atan2(toDir.y, toDir.x) * Mathf.Rad2Deg;
            return -Mathf.DeltaAngle(a, b);
        }

        public static float Output(float wheelDeg, float sensitivity) =>
            Mathf.Clamp(wheelDeg / (FullLockDeg / Mathf.Max(0.1f, sensitivity)), -1f, 1f);

        /// <summary>Rotation that is still useful (past full lock the wheel stops, so it never "winds up" invisibly).</summary>
        public static float Limit(float sensitivity) => Mathf.Min(MaxRotationDeg, FullLockDeg / Mathf.Max(0.1f, sensitivity));
    }
}
