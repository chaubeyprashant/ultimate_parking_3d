using UnityEngine;

namespace UltimateCarParking.Vehicle
{
    /// <summary>
    /// Drives a Rigidbody car on four WheelColliders: throttle, braking, automatic switch to reverse,
    /// speed-sensitive smoothed steering (with optional Ackermann geometry), speed limits,
    /// anti-roll bars and a lowered centre of mass so it stays upright in normal driving.
    /// Reads <see cref="VehicleInput"/>; wheel meshes are moved by <see cref="WheelVisuals"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class VehicleController : MonoBehaviour
    {
        public enum DriveType { FrontWheelDrive, RearWheelDrive, AllWheelDrive }

        [Header("References")]
        [Tooltip("Left empty: taken from this GameObject.")]
        [SerializeField] private VehicleInput input;
        [SerializeField] private WheelCollider frontLeft;
        [SerializeField] private WheelCollider frontRight;
        [SerializeField] private WheelCollider rearLeft;
        [SerializeField] private WheelCollider rearRight;

        [Header("Body")]
        [Tooltip("Centre of mass in local space. Keep it low (near axle height) so the car resists rolling over.")]
        [SerializeField] private Vector3 centerOfMass = new Vector3(0f, 0.3f, 0.05f);

        [Header("Engine")]
        [SerializeField] private DriveType driveType = DriveType.FrontWheelDrive;
        [Tooltip("Total forward torque (N·m) shared by the driven wheels.")]
        [SerializeField, Min(0f)] private float motorTorque = 1600f;
        [Tooltip("Total reverse torque (N·m) shared by the driven wheels.")]
        [SerializeField, Min(0f)] private float reverseTorque = 1100f;
        [SerializeField, Min(1f)] private float maxForwardSpeedKmh = 60f;
        [SerializeField, Min(1f)] private float maxReverseSpeedKmh = 20f;
        [Tooltip("Torque fades to zero over this many km/h below the speed limit, so the car settles at the limit instead of bouncing off it.")]
        [SerializeField, Min(0.1f)] private float speedLimitBandKmh = 6f;

        [Header("Brakes")]
        [Tooltip("Foot-brake torque per wheel (N·m).")]
        [SerializeField, Min(0f)] private float brakeTorque = 2500f;
        [Tooltip("Handbrake torque on each rear wheel (N·m).")]
        [SerializeField, Min(0f)] private float handbrakeTorque = 4000f;
        [Tooltip("Rolling resistance / engine braking per wheel when no pedal is pressed.")]
        [SerializeField, Min(0f)] private float coastBrakeTorque = 120f;
        [Tooltip("Brake per wheel that holds the car still once it has stopped with no throttle.")]
        [SerializeField, Min(0f)] private float standstillBrakeTorque = 800f;
        [Tooltip("Below this speed an opposite throttle changes direction instead of braking.")]
        [SerializeField, Min(0f)] private float directionChangeSpeedKmh = 2f;

        [Header("Steering")]
        [SerializeField, Range(5f, 50f)] private float maxSteerAngle = 35f;
        [Tooltip("Steering lock at the top speed; the lock blends from Max Steer Angle down to this with speed.")]
        [SerializeField, Range(1f, 50f)] private float highSpeedSteerAngle = 12f;
        [Tooltip("Degrees per second the front wheels turn toward the requested angle.")]
        [SerializeField, Min(1f)] private float steerSpeed = 110f;
        [Tooltip("Degrees per second the front wheels return toward straight.")]
        [SerializeField, Min(1f)] private float steerReturnSpeed = 160f;
        [Tooltip("The inside front wheel turns more than the outside one, as on a real car.")]
        [SerializeField] private bool useAckermann = true;

        [Header("Stability")]
        [Tooltip("Anti-roll bar stiffness per axle (N per unit of suspension travel difference).")]
        [SerializeField, Min(0f)] private float antiRollStiffness = 7000f;
        [Tooltip("Downforce in N per m/s of speed. Keeps the tyres loaded; small values are enough for a parking game.")]
        [SerializeField, Min(0f)] private float downforce = 25f;

        [Header("WheelCollider sub-stepping")]
        [Tooltip("See WheelCollider.ConfigureVehicleSubsteps. More steps = smoother low-speed handling.")]
        [SerializeField, Min(0.1f)] private float substepSpeedThreshold = 5f;
        [SerializeField, Min(1)] private int substepsBelowThreshold = 12;
        [SerializeField, Min(1)] private int substepsAboveThreshold = 15;

        private const float InputDeadzone = 0.05f;
        private const float MsToKmh = 3.6f;

        private Rigidbody body;
        private bool configured;
        private float wheelbase;
        private float trackWidth;
        private float steerAngle;

        public Rigidbody Body => body;
        public VehicleInput Input => input;

        /// <summary>Signed speed along the car's forward axis; negative while reversing.</summary>
        public float ForwardSpeedKmh { get; private set; }

        public float SpeedKmh => Mathf.Abs(ForwardSpeedKmh);
        public bool IsReversing => ForwardSpeedKmh < -0.5f;

        /// <summary>Centre steering angle in degrees (negative = left).</summary>
        public float SteerAngle => steerAngle;

        public float MaxForwardSpeedKmh => maxForwardSpeedKmh;
        public float MaxReverseSpeedKmh => maxReverseSpeedKmh;

        /// <summary>When false the car ignores input and holds the brakes (e.g. level complete, paused).</summary>
        public bool ControlsEnabled { get; set; } = true;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (input == null) input = GetComponent<VehicleInput>();
            if (input == null)
                Debug.LogWarning($"[VehicleController] {name}: no VehicleInput found; the car will receive no input.", this);

            if (frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
            {
                Debug.LogError($"[VehicleController] {name}: assign all four WheelColliders. Controller disabled.", this);
                enabled = false;
                return;
            }

            body.centerOfMass = centerOfMass;

            Vector3 fl = transform.InverseTransformPoint(frontLeft.transform.position);
            Vector3 fr = transform.InverseTransformPoint(frontRight.transform.position);
            Vector3 rl = transform.InverseTransformPoint(rearLeft.transform.position);
            wheelbase = Mathf.Abs(fl.z - rl.z);
            trackWidth = Mathf.Abs(fr.x - fl.x);

            frontLeft.ConfigureVehicleSubsteps(substepSpeedThreshold, substepsBelowThreshold, substepsAboveThreshold);
            configured = true;
        }

        private void OnValidate()
        {
            highSpeedSteerAngle = Mathf.Min(highSpeedSteerAngle, maxSteerAngle);
            speedLimitBandKmh = Mathf.Min(speedLimitBandKmh, Mathf.Min(maxForwardSpeedKmh, maxReverseSpeedKmh));
        }

        private void FixedUpdate()
        {
            if (!configured) return;

            ForwardSpeedKmh = Vector3.Dot(body.linearVelocity, transform.forward) * MsToKmh;

            float throttle = 0f, steering = 0f, brake = 0f;
            bool handbrake = false;
            if (ControlsEnabled && input != null)
            {
                throttle = input.Throttle;
                steering = input.Steering;
                brake = input.Brake;
                handbrake = input.Handbrake;
            }

            ApplySteering(steering);
            ApplyDriveAndBrakes(throttle, brake, handbrake);
            ApplyAntiRoll(frontLeft, frontRight);
            ApplyAntiRoll(rearLeft, rearRight);

            if (downforce > 0f)
                body.AddForce(-transform.up * (downforce * body.linearVelocity.magnitude));
        }

        /// <summary>
        /// Overrides the tunable handling values (a car model's CarData). Safe to call right after Instantiate.
        /// </summary>
        public void ApplyHandling(VehicleHandling h)
        {
            if (h == null) return;
            driveType = h.driveType;
            centerOfMass = h.centerOfMass;
            motorTorque = h.motorTorque;
            reverseTorque = h.reverseTorque;
            maxForwardSpeedKmh = h.maxForwardSpeedKmh;
            maxReverseSpeedKmh = h.maxReverseSpeedKmh;
            brakeTorque = h.brakeTorque;
            handbrakeTorque = h.handbrakeTorque;
            maxSteerAngle = h.maxSteerAngle;
            highSpeedSteerAngle = Mathf.Min(h.highSpeedSteerAngle, h.maxSteerAngle);
            steerSpeed = h.steerSpeed;
            speedLimitBandKmh = Mathf.Min(speedLimitBandKmh, Mathf.Min(maxForwardSpeedKmh, maxReverseSpeedKmh));

            if (body == null) body = GetComponent<Rigidbody>();
            body.mass = h.mass;
            body.centerOfMass = centerOfMass;
        }

        public DriveType Drive => driveType;
        public float MotorTorque => motorTorque;
        public float BrakeTorque => brakeTorque;
        public float MaxSteerAngle => maxSteerAngle;

        /// <summary>Teleports the car and clears its motion (spawning, or recovering after a crash).</summary>
        public void ResetVehicle(Vector3 position, Quaternion rotation)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            steerAngle = 0f;
            ForwardSpeedKmh = 0f;
            if (configured)
                foreach (WheelCollider w in Wheels())
                {
                    w.motorTorque = 0f;
                    w.brakeTorque = standstillBrakeTorque;
                    w.steerAngle = 0f;
                }
        }

        private void ApplySteering(float steering)
        {
            float speedFactor = Mathf.Clamp01(SpeedKmh / maxForwardSpeedKmh);
            float lockAngle = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speedFactor);
            float target = steering * lockAngle;

            bool returning = Mathf.Abs(target) < Mathf.Abs(steerAngle) || Mathf.Sign(target) != Mathf.Sign(steerAngle);
            float rate = returning ? steerReturnSpeed : steerSpeed;
            steerAngle = Mathf.MoveTowards(steerAngle, target, rate * Time.fixedDeltaTime);

            if (!useAckermann || Mathf.Abs(steerAngle) < 0.01f || wheelbase <= 0f)
            {
                frontLeft.steerAngle = steerAngle;
                frontRight.steerAngle = steerAngle;
                return;
            }

            // Turning radius at the rear-axle centre for the centre angle, then each wheel's own angle.
            float radius = wheelbase / Mathf.Tan(Mathf.Abs(steerAngle) * Mathf.Deg2Rad);
            float inner = Mathf.Atan(wheelbase / Mathf.Max(radius - trackWidth * 0.5f, 0.01f)) * Mathf.Rad2Deg;
            float outer = Mathf.Atan(wheelbase / (radius + trackWidth * 0.5f)) * Mathf.Rad2Deg;
            float sign = Mathf.Sign(steerAngle);
            bool turningRight = sign > 0f;
            frontLeft.steerAngle = sign * (turningRight ? outer : inner);
            frontRight.steerAngle = sign * (turningRight ? inner : outer);
        }

        private void ApplyDriveAndBrakes(float throttle, float brakeInput, bool handbrake)
        {
            float motor = 0f;
            float brake = brakeInput * brakeTorque;

            if (!ControlsEnabled)
            {
                brake = brakeTorque;
            }
            else if (throttle > InputDeadzone)
            {
                if (ForwardSpeedKmh < -directionChangeSpeedKmh)
                    brake = Mathf.Max(brake, throttle * brakeTorque);           // rolling backwards: brake first
                else
                {
                    motor = throttle * motorTorque * SpeedLimitFactor(ForwardSpeedKmh, maxForwardSpeedKmh);
                    brake = Mathf.Max(brake, OverspeedBrake(ForwardSpeedKmh, maxForwardSpeedKmh));
                }
            }
            else if (throttle < -InputDeadzone)
            {
                if (ForwardSpeedKmh > directionChangeSpeedKmh)
                    brake = Mathf.Max(brake, -throttle * brakeTorque);          // rolling forwards: brake first
                else
                {
                    motor = throttle * reverseTorque * SpeedLimitFactor(-ForwardSpeedKmh, maxReverseSpeedKmh);
                    brake = Mathf.Max(brake, OverspeedBrake(-ForwardSpeedKmh, maxReverseSpeedKmh));
                }
            }
            else
            {
                float idle = SpeedKmh < 1f ? standstillBrakeTorque : coastBrakeTorque;
                brake = Mathf.Max(brake, idle);
            }

            int driven = driveType == DriveType.AllWheelDrive ? 4 : 2;
            float perWheel = motor / driven;
            bool frontDriven = driveType != DriveType.RearWheelDrive;
            bool rearDriven = driveType != DriveType.FrontWheelDrive;

            frontLeft.motorTorque = frontDriven ? perWheel : 0f;
            frontRight.motorTorque = frontDriven ? perWheel : 0f;
            rearLeft.motorTorque = rearDriven ? perWheel : 0f;
            rearRight.motorTorque = rearDriven ? perWheel : 0f;

            float rearBrake = handbrake ? Mathf.Max(brake, handbrakeTorque) : brake;
            frontLeft.brakeTorque = brake;
            frontRight.brakeTorque = brake;
            rearLeft.brakeTorque = rearBrake;
            rearRight.brakeTorque = rearBrake;
            if (handbrake)
            {
                rearLeft.motorTorque = 0f;
                rearRight.motorTorque = 0f;
            }
        }

        /// <summary>
        /// Gentle brake once a powerful car has overshot its limit (cutting torque alone lets a high-torque car
        /// coast past it); zero at or below the limit.
        /// </summary>
        private float OverspeedBrake(float speedInDirectionKmh, float limitKmh) =>
            brakeTorque * Mathf.Clamp01((speedInDirectionKmh - limitKmh) / (speedLimitBandKmh * 0.5f));

        /// <summary>1 well below the limit, fading to 0 at it (and 0 above it).</summary>
        private float SpeedLimitFactor(float speedInDirectionKmh, float limitKmh) =>
            Mathf.Clamp01((limitKmh - speedInDirectionKmh) / speedLimitBandKmh);

        /// <summary>Pushes the more-compressed side down less than the extended side, resisting body roll.</summary>
        private void ApplyAntiRoll(WheelCollider left, WheelCollider right)
        {
            if (antiRollStiffness <= 0f) return;

            bool leftGrounded = left.GetGroundHit(out WheelHit leftHit);
            bool rightGrounded = right.GetGroundHit(out WheelHit rightHit);
            float leftTravel = leftGrounded ? SuspensionTravel(left, leftHit) : 1f;
            float rightTravel = rightGrounded ? SuspensionTravel(right, rightHit) : 1f;

            float force = (leftTravel - rightTravel) * antiRollStiffness;
            if (leftGrounded) body.AddForceAtPosition(left.transform.up * -force, left.transform.position);
            if (rightGrounded) body.AddForceAtPosition(right.transform.up * force, right.transform.position);
        }

        /// <summary>0 = fully compressed, 1 = fully extended.</summary>
        private static float SuspensionTravel(WheelCollider wheel, WheelHit hit)
        {
            if (wheel.suspensionDistance <= 0f) return 0f;
            float hitDepth = -wheel.transform.InverseTransformPoint(hit.point).y + wheel.center.y;
            return Mathf.Clamp01((hitDepth - wheel.radius) / wheel.suspensionDistance);
        }

        private WheelCollider[] Wheels() => new[] { frontLeft, frontRight, rearLeft, rearRight };
    }
}
