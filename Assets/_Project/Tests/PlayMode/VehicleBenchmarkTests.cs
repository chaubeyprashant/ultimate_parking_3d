using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UltimateCarParking.Cars;
using UltimateCarParking.Conditions;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Phase 2 handling benchmark for every garage car on a flat test pad, driven through the same VehicleInput the
    /// touch controls use. Measures: 3.5 s acceleration, 0–20 km/h, dry / wet / snow braking from 40 km/h,
    /// minimum turning circle, 3 s reverse, low-speed creep precision and standstill drift. Runs at a fixed capture
    /// frame rate so results are repeatable; a second check compares 30 and 120 fps rendering.
    /// Writes ../UltimateCarParking3D_logs/vehicle_benchmark.csv when run from the project tools.
    /// </summary>
    public class VehicleBenchmarkTests
    {
        private const float BrakeFromKmh = 40f;
        private GameObject pad;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "BenchPad";
            pad.transform.position = new Vector3(0f, -0.5f, 0f);
            pad.transform.localScale = new Vector3(600f, 1f, 600f);
            Time.captureFramerate = 60;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureFramerate = 0;
            if (pad != null) Object.Destroy(pad);
            yield return null;
        }

        private struct Rig { public GameObject go; public VehicleController vc; public VehicleInput input; public Rigidbody rb; }

        private static IEnumerator Frames(float seconds)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Mathf.Max(1, Time.captureFramerate)));
            for (int i = 0; i < n; i++) yield return null;
        }

        private static Rig Spawn(CarData data, float grip = 1f)
        {
            GameObject car = Object.Instantiate(data.ModelPrefab, new Vector3(0f, 0.6f, -200f), Quaternion.identity);
            data.ApplyTo(car);
            var r = new Rig { go = car, vc = car.GetComponent<VehicleController>(), input = car.GetComponent<VehicleInput>(), rb = car.GetComponent<Rigidbody>() };
            r.input.KeyboardEnabled = false;
            r.vc.ControlsEnabled = true;
            if (!Mathf.Approximately(grip, 1f)) r.vc.SetSurfaceGrip(grip);
            return r;
        }

        private static float Fwd(Rig r, Vector3 from) => Vector3.Dot(r.rb.position - from, r.go.transform.forward);

        public struct Result
        {
            public string id;
            public float accelM, accelKmh, t0to20, brakeT, brakeM, wetM, snowM, radiusM, revM, revKmh, creepM, holdDriftMm, wetSlip;
        }

        private static IEnumerator Brake(Rig r, float fromKmh, System.Action<float, float> done)
        {
            float t = 0f;
            r.input.SetThrottle(1f);
            while (r.vc.ForwardSpeedKmh < fromKmh && t < 12f) { t += Time.deltaTime; yield return null; }
            r.input.SetThrottle(0f);
            r.input.ResetMobileInput();
            r.input.SetBrake(1f);
            Vector3 p0 = r.rb.position;
            float bt = 0f;
            while (r.vc.SpeedKmh > 0.5f && bt < 10f) { bt += Time.deltaTime; yield return null; }
            done(bt, Vector3.Distance(p0, r.rb.position));
            r.input.SetBrake(0f);
        }

        public static IEnumerator Measure(CarData data, System.Action<Result> report)
        {
            var res = new Result { id = data.CarId };
            float target = Mathf.Min(BrakeFromKmh, data.Handling.maxForwardSpeedKmh * 0.85f);

            // 1) acceleration and 0-20 km/h
            Rig r = Spawn(data);
            yield return Frames(1.2f);
            Vector3 start = r.rb.position;
            r.input.SetThrottle(1f);
            float t = 0f; res.t0to20 = -1f;
            while (t < 3.5f)
            {
                yield return null; t += Time.deltaTime;
                if (res.t0to20 < 0f && r.vc.ForwardSpeedKmh >= 20f) res.t0to20 = t;
            }
            res.accelM = Fwd(r, start); res.accelKmh = r.vc.SpeedKmh;
            Object.Destroy(r.go); yield return null;

            // 2) dry, wet and snow braking from the same speed
            r = Spawn(data); yield return Frames(1.2f);
            yield return Brake(r, target, (bt, bd) => { res.brakeT = bt; res.brakeM = bd; });
            Object.Destroy(r.go); yield return null;
            r = Spawn(data, ConditionProfiles.For(RoadCondition.Rain).Grip); yield return Frames(1.2f);
            yield return Brake(r, target, (bt, bd) => res.wetM = bd);
            Object.Destroy(r.go); yield return null;
            r = Spawn(data, ConditionProfiles.For(RoadCondition.Snow).Grip); yield return Frames(1.2f);
            yield return Brake(r, target, (bt, bd) => res.snowM = bd);
            Object.Destroy(r.go); yield return null;

            // 3) minimum turning circle at walking pace, full lock (centre of the car)
            r = Spawn(data); yield return Frames(1.2f);
            r.input.SetSteering(1f);
            float yawRate = 0f, speed = 0f; int samples = 0;
            for (t = 0f; t < 9f; t += Time.deltaTime)
            {
                float v = r.vc.SpeedKmh;
                r.input.SetThrottle(Mathf.Clamp((7f - v) * 0.4f, 0f, 1f));
                r.input.SetBrake(v > 9f ? 0.3f : 0f);
                yield return null;
                if (t > 6f) { yawRate += Mathf.Abs(r.rb.angularVelocity.y); speed += r.rb.linearVelocity.magnitude; samples++; }
            }
            res.radiusM = samples > 0 && yawRate > 1e-3f ? (speed / samples) / (yawRate / samples) : -1f;
            Object.Destroy(r.go); yield return null;

            // 4) reverse
            r = Spawn(data); yield return Frames(1.2f);
            start = r.rb.position;
            r.input.SetThrottle(-1f);
            yield return Frames(3f);
            res.revM = -Fwd(r, start); res.revKmh = r.vc.SpeedKmh;
            Object.Destroy(r.go); yield return null;

            // 5) low-speed precision: a 0.5 s touch of the pedal, then release; how far it creeps and whether it holds still
            r = Spawn(data); yield return Frames(1.2f);
            start = r.rb.position;
            r.input.SetThrottle(1f);
            yield return Frames(0.5f);
            r.input.SetThrottle(0f);
            for (t = 0f; t < 6f && (t < 0.6f || r.vc.SpeedKmh > 0.05f); t += Time.deltaTime) yield return null;
            res.creepM = Fwd(r, start);
            Vector3 rest = r.rb.position;
            yield return Frames(2f);
            res.holdDriftMm = Vector3.Distance(rest, r.rb.position) * 1000f;
            Object.Destroy(r.go); yield return null;

            report(res);
        }

        public static string Header => "car,accel_m_3.5s,kmh_3.5s,t_0_20_s,brake_from_kmh,brake_s,brake_m,wet_brake_m,snow_brake_m,turn_radius_m,reverse_m_3s,reverse_kmh,creep_m,hold_drift_mm";

        public static string Csv(Result r, float from) => string.Format(CultureInfo.InvariantCulture,
            "{0},{1:F1},{2:F0},{3:F2},{4:F0},{5:F2},{6:F1},{7:F1},{8:F1},{9:F2},{10:F1},{11:F0},{12:F2},{13:F1}",
            r.id, r.accelM, r.accelKmh, r.t0to20, from, r.brakeT, r.brakeM, r.wetM, r.snowM, r.radiusM, r.revM, r.revKmh, r.creepM, r.holdDriftMm);

        [UnityTest, Timeout(1800000)]
        public IEnumerator Benchmark_Every_Garage_Car()
        {
#if UNITY_EDITOR
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");
            var csv = new StringBuilder(Header).AppendLine();
            var problems = new List<string>();
            for (int i = 0; i < catalog.Count; i++)
            {
                CarData data = catalog.Get(i);
                Result res = default;
                yield return Measure(data, x => res = x);
                float from = Mathf.Min(BrakeFromKmh, data.Handling.maxForwardSpeedKmh * 0.85f);
                string line = Csv(res, from);
                csv.AppendLine(line);
                Debug.Log("[Bench] " + line);
                if (float.IsNaN(res.accelM) || float.IsNaN(res.radiusM)) problems.Add($"{res.id}: NaN");
                if (res.brakeT >= 10f) problems.Add($"{res.id}: did not stop on a dry road");
                if (res.snowM <= 0f) problems.Add($"{res.id}: snow braking not measured");
            }
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "UltimateCarParking3D_logs"));
            if (Directory.Exists(dir)) File.WriteAllText(Path.Combine(dir, "vehicle_benchmark.csv"), csv.ToString());
            Assert.IsEmpty(problems, string.Join("\n", problems));
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Results_Do_Not_Depend_On_The_Render_Frame_Rate()
        {
#if UNITY_EDITOR
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");
            foreach (string id in new[] { "sedan", "f1_velocity", "monster_goliath" })
            {
                CarData data = catalog.FindById(id);
                var dist = new float[2];
                int[] fps = { 30, 120 };
                for (int k = 0; k < 2; k++)
                {
                    Time.captureFramerate = fps[k];
                    Rig r = Spawn(data);
                    yield return Frames(1.2f);
                    Vector3 s0 = r.rb.position;
                    r.input.SetThrottle(1f);
                    yield return Frames(3f);
                    dist[k] = Fwd(r, s0);
                    Object.Destroy(r.go);
                    yield return null;
                }
                Time.captureFramerate = 60;
                Debug.Log($"[Bench] fps-independence {id}: 3 s at 30 fps {dist[0]:F2} m, at 120 fps {dist[1]:F2} m");
                Assert.AreEqual(dist[0], dist[1], Mathf.Max(0.5f, dist[0] * 0.04f), $"{id} drives the same at 30 and 120 fps");
            }
#else
            yield break;
#endif
        }
    }
}
