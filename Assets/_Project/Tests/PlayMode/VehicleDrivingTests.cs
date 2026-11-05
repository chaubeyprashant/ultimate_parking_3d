using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Phase 1 acceptance tests: the real PlayerCar prefab on a flat 1 km test ground, driven through
    /// VehicleInput's public (mobile) methods. Measured values are logged with a "[Test]" prefix.
    /// </summary>
    public class VehicleDrivingTests
    {
        private const string CarPrefabPath = "Assets/_Project/Prefabs/Vehicles/PlayerCar.prefab";
        private const float TimeScale = 3f;

        private GameObject ground;
        private AudioListener listener;
        private GameObject car;
        private VehicleController controller;
        private VehicleInput input;
        private WheelVisuals visuals;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = TimeScale;
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "TestGround";
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            listener = new GameObject("TestListener").AddComponent<AudioListener>();   // as a real scene's camera (cars carry engine audio)

            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
#endif
            Assert.IsNotNull(prefab, $"Car prefab missing at {CarPrefabPath}; run Tools > Ultimate Car Parking > Bootstrap Project.");
            car = Object.Instantiate(prefab, new Vector3(0f, 0.3f, 0f), Quaternion.identity);
            controller = car.GetComponent<VehicleController>();
            input = car.GetComponent<VehicleInput>();
            visuals = car.GetComponent<WheelVisuals>();
            input.KeyboardEnabled = false;
            yield return Simulate(1.5f);   // let the suspension settle
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            foreach (var go in new[] { car, ground, listener != null ? listener.gameObject : null }) if (go != null) Object.Destroy(go);
            foreach (var cam in Object.FindObjectsByType<VehicleCamera>()) Object.Destroy(cam.gameObject);
        }

        // ---------------------------------------------------------------- helpers

        private void Set(float throttle, float steering = 0f, float brake = 0f, bool handbrake = false)
        {
            input.SetThrottle(throttle);
            input.SetSteering(steering);
            input.SetBrake(brake);
            input.SetHandbrake(handbrake);
        }

        /// <summary>Runs physics for the given simulated seconds, calling onStep after each physics step.</summary>
        private static IEnumerator Simulate(float seconds, System.Action onStep = null)
        {
            int steps = Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                onStep?.Invoke();
            }
            yield return null;   // let Update/LateUpdate (input, visuals, camera) run on the latest state
        }

        private float Yaw => car.transform.eulerAngles.y;

        // ---------------------------------------------------------------- acceptance

        [UnityTest]
        public IEnumerator Accelerates_Forward()
        {
            Vector3 start = car.transform.position;
            Set(1f);
            yield return Simulate(4f);
            float travelled = Vector3.Dot(car.transform.position - start, Vector3.forward);
            Debug.Log($"[Test] Accelerate 4 s: {controller.ForwardSpeedKmh:F1} km/h, {travelled:F1} m");
            Assert.Greater(controller.ForwardSpeedKmh, 25f);
            Assert.Greater(travelled, 10f);
        }

        [UnityTest]
        public IEnumerator Forward_Speed_Is_Limited()
        {
            float peak = 0f;
            Set(1f);
            yield return Simulate(16f, () => peak = Mathf.Max(peak, controller.ForwardSpeedKmh));
            Debug.Log($"[Test] Top speed: final {controller.ForwardSpeedKmh:F1} km/h, peak {peak:F1} (limit {controller.MaxForwardSpeedKmh})");
            Assert.LessOrEqual(peak, controller.MaxForwardSpeedKmh + 1f);
            Assert.Greater(controller.ForwardSpeedKmh, controller.MaxForwardSpeedKmh - 10f);
        }

        [UnityTest]
        public IEnumerator Brakes_To_A_Stop()
        {
            Set(1f);
            yield return Simulate(5f);
            float speedBefore = controller.ForwardSpeedKmh;
            Vector3 brakeStart = car.transform.position;
            float stopTime = -1f, t = 0f;
            Set(0f, 0f, 1f);
            yield return Simulate(5f, () =>
            {
                t += Time.fixedDeltaTime;
                if (stopTime < 0f && controller.SpeedKmh < 0.5f) stopTime = t;
            });
            float distance = Vector3.Distance(car.transform.position, brakeStart);
            Debug.Log($"[Test] Brake from {speedBefore:F1} km/h: stopped in {stopTime:F2} s over {distance:F1} m");
            Assert.Greater(speedBefore, 30f);
            Assert.That(stopTime, Is.InRange(0f, 4f), "car should stop within 4 s");
            Assert.Less(controller.SpeedKmh, 0.5f);
        }

        [UnityTest]
        public IEnumerator Reverse_Throttle_Brakes_Then_Reverses()
        {
            Set(1f);
            yield return Simulate(3f);
            float forwardSpeed = controller.ForwardSpeedKmh;
            Set(-1f);
            bool wentNegative = false;
            float minWhileForward = forwardSpeed;
            yield return Simulate(8f, () =>
            {
                if (controller.ForwardSpeedKmh < -1f) wentNegative = true;
                minWhileForward = Mathf.Min(minWhileForward, controller.ForwardSpeedKmh);
            });
            Debug.Log($"[Test] S/back held from {forwardSpeed:F1} km/h → now {controller.ForwardSpeedKmh:F1} km/h (reverse limit {controller.MaxReverseSpeedKmh})");
            Assert.IsTrue(wentNegative, "car should end up reversing");
            Assert.Less(controller.ForwardSpeedKmh, -10f);
            Assert.GreaterOrEqual(controller.ForwardSpeedKmh, -(controller.MaxReverseSpeedKmh + 1f), "reverse speed limit");
        }

        [UnityTest]
        public IEnumerator Reverses_From_Standstill()
        {
            Vector3 start = car.transform.position;
            Set(-1f);
            yield return Simulate(4f);
            float travelled = Vector3.Dot(car.transform.position - start, Vector3.forward);
            Debug.Log($"[Test] Reverse 4 s: {controller.ForwardSpeedKmh:F1} km/h, {travelled:F1} m, IsReversing={controller.IsReversing}");
            Assert.Less(travelled, -5f);
            Assert.IsTrue(controller.IsReversing);
        }

        [UnityTest]
        public IEnumerator Steers_Left_And_Right()
        {
            float startYaw = Yaw;
            Set(0.6f, 1f);
            yield return Simulate(4f);
            float right = Mathf.DeltaAngle(startYaw, Yaw);

            Set(0f, 0f, 1f);
            yield return Simulate(3f);
            float midYaw = Yaw;
            Set(0.6f, -1f);
            yield return Simulate(4f);
            float left = Mathf.DeltaAngle(midYaw, Yaw);
            Debug.Log($"[Test] Steering: right turn {right:F0}°, left turn {left:F0}° (4 s each)");
            Assert.Greater(right, 45f);
            Assert.Less(left, -45f);
        }

        [UnityTest]
        public IEnumerator Steering_Is_Smoothed_And_Speed_Sensitive()
        {
            Set(0f, 1f);
            yield return null;   // VehicleInput latches mobile values in Update
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            float afterOneStep = controller.SteerAngle;
            yield return Simulate(1f);
            float lowSpeedLock = controller.SteerAngle;

            Set(1f, 0f);
            yield return Simulate(14f);
            Set(1f, 1f);
            yield return Simulate(1f);
            float highSpeedLock = controller.SteerAngle;
            Debug.Log($"[Test] Steer angle: 2 steps {afterOneStep:F1}°, settled {lowSpeedLock:F1}° at rest, {highSpeedLock:F1}° at {controller.SpeedKmh:F0} km/h");
            Assert.Less(afterOneStep, lowSpeedLock * 0.5f, "steering should ramp, not snap");
            Assert.Greater(lowSpeedLock, 30f);
            Assert.Less(highSpeedLock, lowSpeedLock * 0.6f, "lock should shrink with speed");
        }

        [UnityTest]
        public IEnumerator Wheel_Meshes_Spin_And_Steer()
        {
            Transform fl = car.transform.Find("WheelMeshes/FrontLeftMesh");
            Transform rl = car.transform.Find("WheelMeshes/RearLeftMesh");
            Assert.IsNotNull(fl);
            Assert.IsNotNull(rl);

            Set(0.5f, 1f);
            yield return Simulate(1.5f);
            float spinA = visuals.GetSpinDegrees(0);
            Quaternion flRot = fl.rotation;
            yield return Simulate(0.2f);
            float spinB = visuals.GetSpinDegrees(0);

            float rearYaw = Mathf.DeltaAngle(0f, (Quaternion.Inverse(car.transform.rotation) * rl.rotation * Vector3.right).ToYaw() - 90f);
            float frontAxleYaw = Mathf.DeltaAngle(0f, (Quaternion.Inverse(car.transform.rotation) * fl.rotation * Vector3.right).ToYaw() - 90f);
            Debug.Log($"[Test] Wheel visuals: spin {spinA:F0}° → {spinB:F0}°, front axle yaw {frontAxleYaw:F1}°, rear axle yaw {rearYaw:F1}°, collider steer {controller.SteerAngle:F1}°");
            Assert.AreNotEqual(spinA, spinB, "wheel mesh should spin while driving");
            Assert.AreNotEqual(flRot, fl.rotation);
            Assert.Greater(frontAxleYaw, 15f, "front wheel meshes should turn with the steering");
            Assert.Less(Mathf.Abs(rearYaw), 1f, "rear wheel meshes should not steer");

            // The wheel mesh sits on the ground: its centre is about one radius above the plane.
            Assert.That(fl.position.y, Is.InRange(0.25f, 0.45f));
        }

        [UnityTest]
        public IEnumerator Stays_Upright_In_Hard_Driving()
        {
            float minUp = 1f;
            void Track() => minUp = Mathf.Min(minUp, car.transform.up.y);

            Set(1f);
            yield return Simulate(10f, Track);                     // top speed
            for (int i = 0; i < 8; i++)                            // full-lock slalom at speed
            {
                Set(1f, i % 2 == 0 ? 1f : -1f);
                yield return Simulate(1.2f, Track);
            }
            Set(1f, 1f);
            yield return Simulate(4f, Track);                      // full-lock circle at speed
            Set(0f, -1f, 1f, true);
            yield return Simulate(3f, Track);                      // panic brake + handbrake while turning
            Set(-1f, 1f);
            yield return Simulate(4f, Track);                      // reverse at full lock
            Set(0f, 0f, 1f);
            yield return Simulate(3f, Track);

            float maxTilt = Mathf.Acos(Mathf.Clamp(minUp, -1f, 1f)) * Mathf.Rad2Deg;
            Debug.Log($"[Test] Upright: max tilt {maxTilt:F1}° over slalom / circle / handbrake turn / reverse");
            Assert.Greater(minUp, Mathf.Cos(30f * Mathf.Deg2Rad), "body tilt should stay under 30°");
            Assert.Greater(car.transform.up.y, 0.98f, "car should settle on its wheels");
        }

        [UnityTest]
        public IEnumerator Holds_Still_With_No_Input()
        {
            Set(1f);
            yield return Simulate(2f);
            Set(0f);
            yield return Simulate(8f);
            Vector3 p = car.transform.position;
            yield return Simulate(3f);
            float creep = Vector3.Distance(p, car.transform.position);
            Debug.Log($"[Test] Coast to stop then hold: speed {controller.SpeedKmh:F2} km/h, creep {creep * 100f:F1} cm in 3 s");
            Assert.Less(controller.SpeedKmh, 0.5f);
            Assert.Less(creep, 0.05f);
        }

        [UnityTest]
        public IEnumerator Controls_Disabled_Ignores_Input()
        {
            controller.ControlsEnabled = false;
            Vector3 p = car.transform.position;
            Set(1f, 1f);
            yield return Simulate(3f);
            controller.ControlsEnabled = true;
            Assert.Less(Vector3.Distance(p, car.transform.position), 0.1f);
        }

        [UnityTest]
        public IEnumerator Camera_Follows_Smoothly()
        {
            // Real time at a phone-like 60 fps: batch mode otherwise runs thousands of frames per second,
            // where per-frame finite differences measure physics-step quantisation, not the camera.
            Time.timeScale = 1f;
            int previousFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = 60;

            var camGo = new GameObject("TestCamera");
            camGo.AddComponent<Camera>();
            var follow = camGo.AddComponent<VehicleCamera>();
            follow.SetTarget(car.transform);
            yield return null;

            float minDist = float.MaxValue, maxDist = 0f, maxAngle = 0f, maxAccel = 0f, worstStepRatio = 0f;
            Vector3 lastCam = camGo.transform.position, lastCar = car.transform.position, lastVel = Vector3.zero;
            int frames = 0;
            float time = 0f;
            var samples = new System.Collections.Generic.List<(float t, Vector3 p)>();
            Set(1f);
            while (time < 8f)
            {
                yield return null;
                float dt = Time.deltaTime;
                if (dt <= 0f) continue;
                time += dt;
                if (time > 3f) Set(0.8f, Mathf.Sin(time * 1.5f) > 0f ? 0.7f : -0.7f);
                frames++;

                Vector3 cam = camGo.transform.position, carPos = car.transform.position;
                Vector3 toCar = carPos + Vector3.up - cam;
                minDist = Mathf.Min(minDist, toCar.magnitude);
                maxDist = Mathf.Max(maxDist, toCar.magnitude);
                maxAngle = Mathf.Max(maxAngle, Vector3.Angle(camGo.transform.forward, toCar));

                // No snapping: the camera never moves much further in a frame than the car does.
                float camStep = (cam - lastCam).magnitude, carStep = (carPos - lastCar).magnitude;
                if (frames > 2) worstStepRatio = Mathf.Max(worstStepRatio, camStep / (carStep + 0.02f));
                samples.Add((time, cam));
                lastCam = cam;
                lastCar = carPos;
            }
            Application.targetFrameRate = previousFrameRate;

            // Smoothness: velocity averaged over 0.1 s windows, acceleration between consecutive windows. A snap or
            // jump shows up as a huge spike; per-frame timing jitter of the test runner averages out.
            var windowVel = new System.Collections.Generic.List<(float t, Vector3 v)>();
            for (int i = 0, j = 0; i < samples.Count; i = j)
            {
                j = i + 1;
                while (j < samples.Count && samples[j].t - samples[i].t < 0.1f) j++;
                if (j >= samples.Count) break;
                windowVel.Add(((samples[i].t + samples[j].t) * 0.5f, (samples[j].p - samples[i].p) / (samples[j].t - samples[i].t)));
            }
            for (int i = 2; i < windowVel.Count; i++)
                maxAccel = Mathf.Max(maxAccel, (windowVel[i].v - windowVel[i - 1].v).magnitude / (windowVel[i].t - windowVel[i - 1].t));
            Debug.Log($"[Test] Camera over {frames} frames ({frames / time:F0} fps): distance {minDist:F1}–{maxDist:F1} m, " +
                      $"max off-centre {maxAngle:F1}°, peak accel {maxAccel:F1} m/s², worst cam/car step ratio {worstStepRatio:F2}");
            Assert.That(minDist, Is.GreaterThan(3f));
            Assert.That(maxDist, Is.LessThan(16f));
            Assert.Less(maxAngle, 15f, "camera keeps the car framed");
            Assert.Less(worstStepRatio, 1.6f, "camera never jumps ahead of the car's own motion");
            Assert.Less(maxAccel, 60f, "camera acceleration stays smooth");
        }

        [UnityTest]
        public IEnumerator Camera_Distance_Is_Adjustable_And_Clamped()
        {
            var camGo = new GameObject("TestCamera");
            camGo.AddComponent<Camera>();
            var follow = camGo.AddComponent<VehicleCamera>();
            follow.SetTarget(car.transform);
            follow.Distance = 10f;
            yield return Simulate(2f);
            float far = Vector3.Distance(camGo.transform.position, car.transform.position);
            follow.Zoom(-100f);
            yield return Simulate(2f);
            float near = Vector3.Distance(camGo.transform.position, car.transform.position);
            Debug.Log($"[Test] Camera distance: set 10 → {far:F1} m; zoom fully in → Distance {follow.Distance:F1}, {near:F1} m");
            Assert.Greater(far, near + 3f);
            Assert.Greater(follow.Distance, 0.4f, "zoom is clamped at the minimum");
        }

        // ---------------------------------------------------------------- null safety

        [UnityTest]
        public IEnumerator Controller_Without_Wheels_Disables_Itself()
        {
            LogAssert.Expect(LogType.Warning, new Regex("no VehicleInput found"));
            LogAssert.Expect(LogType.Error, new Regex("assign all four WheelColliders"));
            var go = new GameObject("BrokenCar");
            go.AddComponent<Rigidbody>();
            var broken = go.AddComponent<VehicleController>();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(broken.enabled);
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Camera_And_Visuals_Tolerate_Missing_References()
        {
            Object.Destroy(car);
            yield return null;
            var camGo = new GameObject("LonelyCamera");
            camGo.AddComponent<Camera>();
            camGo.AddComponent<VehicleCamera>();
            LogAssert.Expect(LogType.Warning, new Regex("no Rigidbody found"));
            var v = new GameObject("EmptyVisuals").AddComponent<WheelVisuals>();
            yield return Simulate(0.5f);
            Object.Destroy(v.gameObject);
            LogAssert.NoUnexpectedReceived();
        }
    }

    internal static class VectorExt
    {
        public static float ToYaw(this Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
    }
}
