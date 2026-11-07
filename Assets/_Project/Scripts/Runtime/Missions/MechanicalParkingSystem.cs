using System;
using System.Collections;
using UnityEngine;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Missions
{
    /// <summary>
    /// A working mechanical parking machine (stacker / lift). The visible platform, gate and rack come from imported
    /// models; this component moves them. <see cref="Run"/> takes a parked car through the sequence:
    /// safety check (car still, on the platform) → gate closes → platform lowers → platform shifts into the rack slot
    /// → lock. The car's body is made kinematic and carried by the platform, so it really moves with the machine.
    /// </summary>
    public sealed class MechanicalParkingSystem : MonoBehaviour
    {
        public enum Step { Idle, SafetyCheck, GateClosing, Lowering, Shifting, Locked, Aborted }

        [SerializeField] private string systemId = "lift";
        [Tooltip("Moving deck the car parks on (child visuals from the imported lift model).")]
        [SerializeField] private Transform platform;
        [Tooltip("Optional barrier arm / gate that swings down before the platform moves.")]
        [SerializeField] private Transform gate;
        [SerializeField] private Vector3 gateClosedEuler = new Vector3(0f, 0f, 0f);
        [SerializeField] private Vector3 gateOpenEuler = new Vector3(0f, 0f, 80f);
        [Tooltip("How far the platform drops (metres) and then shifts sideways into its rack slot (local to the system).")]
        [SerializeField] private float lowerDepth = 2.4f;
        [SerializeField] private Vector3 shiftOffset = new Vector3(0f, 0f, 3.2f);
        [SerializeField, Min(0.1f)] private float safetySeconds = 2f;
        [SerializeField, Min(0.1f)] private float gateSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float lowerSeconds = 3f;
        [SerializeField, Min(0.1f)] private float shiftSeconds = 2.5f;
        [Tooltip("Platform footprint (local x = width, local z = length) used for the on-platform check.")]
        [SerializeField] private Vector2 deckSize = new Vector2(2.6f, 5.4f);

        private Vector3 platformHome;
        private Quaternion gateHome;

        public string SystemId => systemId;
        public Transform Platform => platform;
        public Step Current { get; private set; } = Step.Idle;
        public float StepProgress { get; private set; }
        public float TotalSeconds => safetySeconds + gateSeconds + lowerSeconds + shiftSeconds;
        public event Action<Step> StepChanged;

        public static string Describe(Step s) => s switch
        {
            Step.SafetyCheck => "SAFETY CHECK - KEEP STILL",
            Step.GateClosing => "GATE CLOSING",
            Step.Lowering => "PLATFORM LOWERING",
            Step.Shifting => "MOVING INTO THE RACK",
            Step.Locked => "PARKED - SYSTEM LOCKED",
            Step.Aborted => "SEQUENCE ABORTED",
            _ => "DRIVE ONTO THE PLATFORM",
        };

        /// <summary>True for a stacker that lifts the car (negative depth) instead of lowering it into a pit.</summary>
        public bool Raises => lowerDepth < 0f;

        /// <summary>Step caption for this machine (a stacker rises; one without a sideways shift just locks).</summary>
        public string Label(Step s) => s switch
        {
            Step.Lowering => Raises ? "PLATFORM RISING" : "PLATFORM LOWERING",
            Step.Shifting => shiftOffset.sqrMagnitude < 0.01f ? "LOCKING THE PLATFORM" : "MOVING INTO THE RACK",
            _ => Describe(s),
        };

        private void Awake()
        {
            if (platform != null) platformHome = platform.localPosition;
            if (gate != null) gateHome = gate.localRotation;
        }

        public void Configure(Transform platformTransform, Transform gateTransform, Vector2 deck, float depth, Vector3 shift)
        {
            platform = platformTransform;
            gate = gateTransform;
            deckSize = deck;
            lowerDepth = depth;
            shiftOffset = shift;
            Awake();
        }

        /// <summary>Is the car's centre over the platform deck (and roughly at deck height)?</summary>
        public bool CarOnPlatform(Transform car)
        {
            if (platform == null || car == null) return false;
            Vector3 local = platform.InverseTransformPoint(car.position);
            return Mathf.Abs(local.x) <= deckSize.x * 0.5f && Mathf.Abs(local.z) <= deckSize.y * 0.5f && Mathf.Abs(local.y) < 1.2f;
        }

        public void ResetMachine()
        {
            StopAllCoroutines();
            if (platform != null) platform.localPosition = platformHome;
            if (gate != null) gate.localRotation = gateHome;
            SetStep(Step.Idle, 0f);
        }

        /// <summary>
        /// Runs the sequence on a car that is parked on the platform. Aborts (and returns the car's controls) if the car
        /// moves or leaves the deck during the safety check. <paramref name="done"/> receives true on success.
        /// </summary>
        public IEnumerator Run(VehicleController car, Action<bool> done)
        {
            Rigidbody body = car.Body;
            // 1. Safety check: car must stay still on the deck.
            SetStep(Step.SafetyCheck, 0f);
            for (float t = 0f; t < safetySeconds; t += Time.deltaTime)
            {
                if (!CarOnPlatform(car.transform) || body.linearVelocity.magnitude > 0.6f)
                {
                    SetStep(Step.Aborted, 0f);
                    done?.Invoke(false);
                    yield break;
                }
                SetStep(Step.SafetyCheck, t / safetySeconds);
                yield return null;
            }

            // Lock the car to the platform for the mechanical part.
            car.ControlsEnabled = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            RigidbodyInterpolation interpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation.None;   // an interpolated body would snap back to its own pose
            Vector3 localPos = platform.InverseTransformPoint(body.position);
            Quaternion localRot = Quaternion.Inverse(platform.rotation) * body.rotation;
            void Carry()
            {
                Vector3 p = platform.TransformPoint(localPos);
                Quaternion r = platform.rotation * localRot;
                body.position = p; body.rotation = r;
                car.transform.SetPositionAndRotation(p, r);
            }

            // 2. Gate closes.
            if (gate != null)
                yield return Animate(Step.GateClosing, gateSeconds, k => { gate.localRotation = Quaternion.Slerp(Quaternion.Euler(gateOpenEuler), Quaternion.Euler(gateClosedEuler), k); Carry(); });
            else yield return Animate(Step.GateClosing, gateSeconds, _ => Carry());

            // 3. Platform lowers, 4. shifts into the rack slot.
            Vector3 start = platform.localPosition, down = start + Vector3.down * lowerDepth, slot = down + shiftOffset;
            yield return Animate(Step.Lowering, lowerSeconds, k => { platform.localPosition = Vector3.Lerp(start, down, Smooth(k)); Carry(); });
            yield return Animate(Step.Shifting, shiftSeconds, k => { platform.localPosition = Vector3.Lerp(down, slot, Smooth(k)); Carry(); });
            Carry();
            body.interpolation = interpolation;
            SetStep(Step.Locked, 1f);
            done?.Invoke(true);
        }

        private IEnumerator Animate(Step step, float seconds, Action<float> apply)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / seconds);
                apply(k);
                SetStep(step, k);
                yield return null;
            }
            apply(1f);
            SetStep(step, 1f);
        }

        private static float Smooth(float k) => k * k * (3f - 2f * k);

        private void SetStep(Step s, float progress)
        {
            bool changed = s != Current;
            Current = s;
            StepProgress = progress;
            if (changed) StepChanged?.Invoke(s);
        }
    }
}
