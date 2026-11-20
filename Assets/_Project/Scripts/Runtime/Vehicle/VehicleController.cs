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
        [Tooltip("Idle (no pedal) coast brake fades out between these speeds (km/h): wheels lock below ~3.5 km/h, which clamps the car at tyre-grip limit.")]
        [SerializeField, Min(0.5f)] private float coastFadeStartKmh = 4.5f;
        [SerializeField, Min(1f)] private float coastFadeEndKmh = 7f;
        [Tooltip("Gentle stopping deceleration (m/s²) applied to the body below the fade-end speed with no pedal; tapers to 0 at a standstill.")]
        [SerializeField, Min(0f)] private float rollOutDecel = 1.5f;
        [Tooltip("Speed (km/h) at which the roll-out force has fully tapered in; below it the force fades to zero.")]
        [SerializeField, Min(0.1f)] private float rollOutTaperKmh = 1.5f;
        [Tooltip("Below this speed (km/h) the full standstill hold applies.")]
        [SerializeField, Min(0.05f)] private float holdBelowKmh = 0.35f;
        [Tooltip("Most the idle brake may change per second (N·m/s): keeps stops soft.")]
        [SerializeField, Min(100f)] private float idleBrakeRamp = 2500f;
        [Tooltip("Steepest grade (rise / run) the standstill hold keeps the car still on; heavier cars and bigger wheels get a stronger hold.")]
        [SerializeField, Range(0f, 0.6f)] private float holdGrade = 0.35f;

        [Header("Pedal feel and driver aids")]
        [Tooltip("Pedal → torque curve: 1 = linear, higher = gentler at part pedal for fine creeping (full pedal is unchanged).")]
        [SerializeField, Range(1f, 2.5f)] private float throttleCurve = 1.5f;
        [Tooltip("Anti-lock brakes: eases a wheel's brake when it starts to lock, so wet and icy stops stay straight and shorter.")]
        [SerializeField] private bool antiLockBrakes = true;
        [SerializeField, Range(0.05f, 1f)] private float absSlip = 0.2f;
        [Tooltip("Traction control: trims a driven wheel's torque when it spins (wet roads, monster trucks, launches).")]
        [SerializeField] private bool tractionControl = true;
        [SerializeField, Range(0.05f, 1f)] private float tractionSlip = 0.5f;
        [Tooltip("Strongest acceleration (m/s²) below parking speed; the limit fades out between 6 and 15 km/h. 0 = off.")]
        [SerializeField, Min(0f)] private float launchAccel = 3.2f;
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

        /// <summary>Engine running (start / stop button). Off: no drive torque; brakes, steering and handbrake still work.</summary>
        public bool EngineOn { get; set; } = true;

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
                throttle = EngineOn ? input.Throttle : 0f;
                steering = input.Steering;
                brake = input.Brake;
                handbrake = input.Handbrake;
            }

            ApplySteering(steering);
            ApplyDriveAndBrakes(throttle, brake, handbrake);
            ApplyRollOutAssist(Mathf.Abs(throttle) > InputDeadzone || brake > 0.01f || handbrake);
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
            ConfigureChassis(h);
        }

        /// <summary>
        /// Per-car suspension, tyres and roll stiffness. Springs come from a natural frequency and damping ratio on
        /// each wheel's share of the mass, so a heavy truck and a light sports car both ride as intended. Zero values
        /// keep what the prefab has.
        /// </summary>
        private void ConfigureChassis(VehicleHandling h)
        {
            if (h.antiRoll > 0f) antiRollStiffness = h.antiRoll;
            if (h.steerReturnSpeed > 0f) steerReturnSpeed = h.steerReturnSpeed;
            if (h.launchAccel > 0f) launchAccel = h.launchAccel;
            WheelCollider[] ws = Wheels();
            if (ws[0] == null) return;
            float corner = Mathf.Max(50f, h.mass * 0.25f);
            foreach (WheelCollider w in ws)
            {
                if (h.suspensionTravel > 0f) w.suspensionDistance = h.suspensionTravel;
                if (h.springHz > 0f)
                {
                    JointSpring sp = w.suspensionSpring;
                    float omega = 2f * Mathf.PI * h.springHz;
                    sp.spring = corner * omega * omega;
                    float zeta = h.dampingRatio > 0f ? h.dampingRatio : 0.6f;
                    sp.damper = 2f * zeta * Mathf.Sqrt(sp.spring * corner);
                    sp.targetPosition = 0.5f;
                    w.suspensionSpring = sp;
                }
                if (h.tireGrip > 0f) { WheelFrictionCurve f = w.forwardFriction; f.stiffness = h.tireGrip; w.forwardFriction = f; }
                if (h.sideGrip > 0f) { WheelFrictionCurve f = w.sidewaysFriction; f.stiffness = h.sideGrip; w.sidewaysFriction = f; }
            }
            // Wet / icy grip scales from the new stock values.
            baseForwardStiff = null;
            if (!Mathf.Approximately(surfaceGrip, 1f)) SetSurfaceGrip(surfaceGrip);
        }

        public float SuspensionTravelMeters => frontLeft != null ? frontLeft.suspensionDistance : 0f;
        public float AntiRollStiffness => antiRollStiffness;
        public float HoldBrakeTorque => HoldTorque;

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
                    w.brakeTorque = HoldTorque;
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
            // While a pedal is down, the eased idle brake restarts from the light coast brake (never from a stale standstill hold).
            if (throttle > InputDeadzone || throttle < -InputDeadzone) idleBrake = coastBrakeTorque;

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
                    motor = Shape(throttle) * motorTorque * SpeedLimitFactor(ForwardSpeedKmh, maxForwardSpeedKmh);
                    brake = Mathf.Max(brake, OverspeedBrake(ForwardSpeedKmh, maxForwardSpeedKmh));
                }
            }
            else if (throttle < -InputDeadzone)
            {
                if (ForwardSpeedKmh > directionChangeSpeedKmh)
                    brake = Mathf.Max(brake, -throttle * brakeTorque);          // rolling forwards: brake first
                else
                {
                    motor = -Shape(-throttle) * reverseTorque * SpeedLimitFactor(-ForwardSpeedKmh, maxReverseSpeedKmh);
                    brake = Mathf.Max(brake, OverspeedBrake(-ForwardSpeedKmh, maxReverseSpeedKmh));
                }
            }
            else
            {
                brake = Mathf.Max(brake, EasedIdleBrake());
            }

            int driven = driveType == DriveType.AllWheelDrive ? 4 : 2;
            float perWheel = LimitLaunch(motor) / driven;
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
            ApplyDriverAids(frontLeft, false);
            ApplyDriverAids(frontRight, false);
            ApplyDriverAids(rearLeft, handbrake);
            ApplyDriverAids(rearRight, handbrake);
        }

        /// <summary>
        /// Caps drive torque below parking speed to what gives <see cref="launchAccel"/> on this car, so a short tap of the
        /// pedal moves a powerful car about as far as a modest one. The cap rises steeply from 6 km/h and is gone by 15 km/h.
        /// </summary>
        private float LimitLaunch(float motor)
        {
            if (launchAccel <= 0f || body == null || frontLeft == null) return motor;
            float v = SpeedKmh;
            if (v >= 15f) return motor;
            float cap = body.mass * launchAccel * frontLeft.radius * (1f + 4f * Mathf.InverseLerp(6f, 15f, v));
            return Mathf.Clamp(motor, -cap, cap);
        }

        /// <summary>Progressive pedal: gentler at part pedal (fine creeping), unchanged at full pedal.</summary>
        private float Shape(float pedal) => Mathf.Pow(Mathf.Clamp01(pedal), throttleCurve);

        /// <summary>Brake that keeps the stopped car still: the configured hold, or more for heavy cars on big wheels.</summary>
        private float HoldTorque
        {
            get
            {
                if (body == null || frontLeft == null) return standstillBrakeTorque;
                // Per-wheel torque to hold the car on a holdGrade slope, with a 30 % margin.
                float needed = body.mass * 9.81f * holdGrade * frontLeft.radius * 0.25f * 1.3f;
                return Mathf.Max(standstillBrakeTorque, needed);
            }
        }

        /// <summary>ABS and traction control for one wheel, from its measured longitudinal slip.</summary>
        private void ApplyDriverAids(WheelCollider w, bool handbrakeLocked)
        {
            if (!antiLockBrakes && !tractionControl) return;
            if (!w.GetGroundHit(out WheelHit hit)) return;
            float slip = Mathf.Abs(hit.forwardSlip);
            if (antiLockBrakes && !handbrakeLocked && SpeedKmh > 3f && w.brakeTorque > coastBrakeTorque * 2f && slip > absSlip)
                w.brakeTorque *= 0.45f;
            if (tractionControl && Mathf.Abs(w.motorTorque) > 1f && w.brakeTorque <= coastBrakeTorque * 2f && slip > tractionSlip)
                w.motorTorque *= 0.75f;   // gentle trim just past peak grip (extremum slip 0.4), not a cut
        }

        private float idleBrake;
        private float[] baseForwardStiff, baseSideStiff;
        private float surfaceGrip = 1f;

        /// <summary>Current tyre grip multiplier (1 = dry road; rain, snow and ice use less).</summary>
        public float SurfaceGrip => surfaceGrip;

        /// <summary>
        /// Scales the forward and sideways tyre grip for a wet or icy road. 1 restores the stock handling. Works from the wheels'
        /// original stiffness, so calling it repeatedly never compounds.
        /// </summary>
        public void SetSurfaceGrip(float scale)
        {
            scale = Mathf.Clamp(scale, 0.1f, 1.5f);
            WheelCollider[] ws = Wheels();
            if (ws[0] == null) return;
            if (baseForwardStiff == null)
            {
                baseForwardStiff = new float[ws.Length]; baseSideStiff = new float[ws.Length];
                for (int i = 0; i < ws.Length; i++) { baseForwardStiff[i] = ws[i].forwardFriction.stiffness; baseSideStiff[i] = ws[i].sidewaysFriction.stiffness; }
            }
            surfaceGrip = scale;
            for (int i = 0; i < ws.Length; i++)
            {
                WheelFrictionCurve f = ws[i].forwardFriction; f.stiffness = baseForwardStiff[i] * scale; ws[i].forwardFriction = f;
                WheelFrictionCurve sd = ws[i].sidewaysFriction; sd.stiffness = baseSideStiff[i] * scale; ws[i].sidewaysFriction = sd;
            }
        }

        /// <summary>
        /// The brake held while no pedal is pressed: the light coast brake at speed, faded out as the car slows (so the
        /// wheels never lock at parking speed), then the full standstill hold once it has essentially stopped. Rate-limited.
        /// </summary>
        private float EasedIdleBrake()
        {
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coastFadeStartKmh, coastFadeEndKmh, SpeedKmh));
            float hold = HoldTorque;
            float target = SpeedKmh < holdBelowKmh ? hold : coastBrakeTorque * fade;
            // Applying brake is rate-limited (soft); releasing it is immediate so it can never linger into the speed range where the wheels lock.
            // The ramp scales with the hold so a heavy truck reaches its stronger hold as quickly as a light car.
            float ramp = idleBrakeRamp * Mathf.Max(1f, hold / Mathf.Max(1f, standstillBrakeTorque));
            idleBrake = target < idleBrake ? target : Mathf.MoveTowards(idleBrake, target, ramp * Time.fixedDeltaTime);
            return idleBrake;
        }

        /// <summary>
        /// Soft stop with no pedal: below the fade speed the wheel brake is off (it locks the wheels), so a small body
        /// force along the travel direction slows the car at <see cref="rollOutDecel"/>, tapering to zero as it stops.
        /// </summary>
        private void ApplyRollOutAssist(bool pedalActive)
        {
            if (pedalActive || !ControlsEnabled) return;
            float speed = SpeedKmh;
            if (speed >= coastFadeEndKmh || speed < holdBelowKmh) return;
            float taper = Mathf.SmoothStep(0f, 1f, speed / rollOutTaperKmh);
            // Fade the assist in over the last 2 km/h above its start speed so it joins the wheel-brake coast without a step.
            taper *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coastFadeEndKmh, coastFadeEndKmh - 2f, speed));
            Vector3 along = transform.forward * Mathf.Sign(ForwardSpeedKmh);
            body.AddForce(-along * (body.mass * rollOutDecel * taper), ForceMode.Force);
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
