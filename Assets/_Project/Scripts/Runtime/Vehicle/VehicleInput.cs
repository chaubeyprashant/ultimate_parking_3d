using UnityEngine;
using UnityEngine.InputSystem;

namespace UltimateCarParking.Vehicle
{
    /// <summary>
    /// Collects the driver's intent for one vehicle. Two sources are summed and clamped:
    /// the keyboard (Editor / desktop testing) and the public Set* methods, which on-screen
    /// mobile controls call. <see cref="VehicleController"/> reads the result every physics step.
    ///
    /// Keyboard: W/Up = forward, S/Down = brake then reverse, A/D or Left/Right = steer,
    /// X = foot brake, Space = handbrake.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleInput : MonoBehaviour
    {
        [Header("Keyboard (Editor / desktop testing)")]
        [Tooltip("Read the keyboard. Harmless on phones: there is no keyboard device there.")]
        [SerializeField] private bool keyboardEnabled = true;

        [Tooltip("Seconds for a held throttle key to go from 0 to full. Keys are digital; the ramp makes them act like a pedal.")]
        [SerializeField, Min(0f)] private float keyboardThrottleRampTime = 0.3f;

        private float keyboardThrottle;
        private float mobileThrottle;
        private float mobileSteering;
        private float mobileBrake;
        private bool mobileHandbrake;

        /// <summary>-1..1. Positive drives forward; negative brakes while rolling forward, then reverses.</summary>
        public float Throttle { get; private set; }

        /// <summary>-1 (full left) .. 1 (full right). Smoothing to a wheel angle happens in the controller.</summary>
        public float Steering { get; private set; }

        /// <summary>0..1 foot brake.</summary>
        public float Brake { get; private set; }

        /// <summary>Locks the rear wheels.</summary>
        public bool Handbrake { get; private set; }

        public bool KeyboardEnabled
        {
            get => keyboardEnabled;
            set => keyboardEnabled = value;
        }

        // ---- Mobile / UI input. Call these from on-screen buttons, sliders or a steering wheel. ----

        /// <summary>-1..1. For a pedal + gear UI pass <c>reverseGear ? -pedal : pedal</c>.</summary>
        public void SetThrottle(float value) => mobileThrottle = Mathf.Clamp(value, -1f, 1f);

        /// <summary>-1 (left) .. 1 (right).</summary>
        public void SetSteering(float value) => mobileSteering = Mathf.Clamp(value, -1f, 1f);

        /// <summary>0..1.</summary>
        public void SetBrake(float value) => mobileBrake = Mathf.Clamp01(value);

        public void SetHandbrake(bool engaged) => mobileHandbrake = engaged;

        /// <summary>Releases every mobile control (e.g. when the app is paused or a level ends).</summary>
        public void ResetMobileInput()
        {
            mobileThrottle = 0f;
            mobileSteering = 0f;
            mobileBrake = 0f;
            mobileHandbrake = false;
        }

        private void OnDisable()
        {
            keyboardThrottle = 0f;
            ResetMobileInput();
            Throttle = Steering = Brake = 0f;
            Handbrake = false;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) ResetMobileInput();
        }

        private void Update()
        {
            float keyThrottleTarget = 0f, keySteer = 0f, keyBrake = 0f;
            bool keyHandbrake = false;

            Keyboard kb = keyboardEnabled ? Keyboard.current : null;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) keyThrottleTarget += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) keyThrottleTarget -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keySteer += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keySteer -= 1f;
                if (kb.xKey.isPressed) keyBrake = 1f;
                keyHandbrake = kb.spaceKey.isPressed;
            }

            // Releasing the key or switching direction drops the throttle at once; pressing ramps up.
            if (keyThrottleTarget == 0f || (keyboardThrottle != 0f && Mathf.Sign(keyThrottleTarget) != Mathf.Sign(keyboardThrottle)))
                keyboardThrottle = 0f;
            keyboardThrottle = keyboardThrottleRampTime <= 0f
                ? keyThrottleTarget
                : Mathf.MoveTowards(keyboardThrottle, keyThrottleTarget, Time.deltaTime / keyboardThrottleRampTime);

            Throttle = Mathf.Clamp(keyboardThrottle + mobileThrottle, -1f, 1f);
            Steering = Mathf.Clamp(keySteer + mobileSteering, -1f, 1f);
            Brake = Mathf.Clamp01(keyBrake + mobileBrake);
            Handbrake = keyHandbrake || mobileHandbrake;
        }
    }
}
