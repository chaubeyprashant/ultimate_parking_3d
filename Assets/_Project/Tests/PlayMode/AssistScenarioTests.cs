using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Cars;
using UltimateCarParking.Parking;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Parking-assistance scenarios on a flat pad, with real physics queries: walls, parked cars, kerbs, a narrow
    /// garage, a moving obstacle and irregular vehicles (monster truck, F1). The sensors must never see the player's
    /// own car. Also checks that the reverse guide lines follow the path the car really drives.
    /// </summary>
    public class AssistScenarioTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.transform.position = new Vector3(0f, -0.5f, 0f);
            pad.transform.localScale = new Vector3(120f, 1f, 120f);
            spawned.Add(pad);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject g in spawned) if (g != null) Object.Destroy(g);
            spawned.Clear();
            yield return null;
        }

        private static CarCatalog Cars()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");
#else
            return null;
#endif
        }

        private (VehicleController car, ParkAssist sensors, Vector3[] footprint) Spawn(string id)
        {
            CarData data = Cars().FindById(id);
            GameObject go = Object.Instantiate(data.ModelPrefab, new Vector3(0f, 0.6f, 0f), Quaternion.identity);
            data.ApplyTo(go);
            go.GetComponent<VehicleInput>().KeyboardEnabled = false;
            spawned.Add(go);
            var car = go.GetComponent<VehicleController>();
            Vector3[] fp = Footprint(go.transform);
            var s = new ParkAssist();
            s.Calibrate(fp);
            return (car, s, fp);
        }

        /// <summary>Same footprint rule as ParkingValidator: the ground rectangle around the car's box colliders.</summary>
        private static Vector3[] Footprint(Transform car)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (BoxCollider b in car.GetComponentsInChildren<BoxCollider>(true))
            {
                if (b.isTrigger) continue;
                for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 p = car.InverseTransformPoint(b.transform.TransformPoint(b.center + new Vector3(sx * b.size.x * 0.5f, 0f, sz * b.size.z * 0.5f)));
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                }
            }
            return new[] { new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), new Vector3(maxX, 0, maxZ), new Vector3(minX, 0, maxZ) };
        }

        private GameObject Box(Vector3 pos, Vector3 size, float yaw = 0f)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            b.transform.localScale = size;
            spawned.Add(b);
            return b;
        }

        private static float MinZ(Vector3[] fp) => Mathf.Min(fp[0].z, fp[2].z);
        private static float MaxZ(Vector3[] fp) => Mathf.Max(fp[0].z, fp[2].z);

        [UnityTest]
        public IEnumerator Sensors_Ignore_The_Players_Own_Car_Of_Every_Shape()
        {
            foreach (string id in new[] { "sedan", "monster_mammoth", "f1_velocity", "suv" })
            {
                var (car, s, _) = Spawn(id);
                yield return new WaitForSeconds(0.6f);
                s.Tick(car, Time.time, force: true);
                Assert.IsTrue(float.IsInfinity(s.FrontMin) && float.IsInfinity(s.RearMin), $"{id}: nothing around, sensors clear (front {s.FrontMin}, rear {s.RearMin})");
                Object.Destroy(car.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Walls_And_Parked_Cars_Are_Measured_Front_And_Rear()
        {
            var (car, s, fp) = Spawn("sedan");
            yield return new WaitForSeconds(0.6f);
            Box(new Vector3(0f, 0.6f, MaxZ(fp) + 0.8f + 0.1f), new Vector3(4f, 1.2f, 0.2f));                  // wall 0.8 m ahead
#if UNITY_EDITOR
            GameObject parkedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Real/Parked_sedan.prefab");
            Assert.IsNotNull(parkedPrefab);
            GameObject parked = Object.Instantiate(parkedPrefab);
            spawned.Add(parked);
            // Parked car nose-to-nose behind us, its nearest point 1.5 m behind our rear bumper.
            Bounds pb = default; bool any = false;
            foreach (Collider c in parked.GetComponentsInChildren<Collider>()) { if (!any) { pb = c.bounds; any = true; } else pb.Encapsulate(c.bounds); }
            parked.transform.position = new Vector3(0f, 0f, MinZ(fp) - 1.5f - (pb.max.z - parked.transform.position.z));
#endif
            yield return new WaitForFixedUpdate(); yield return null;
            s.Tick(car, Time.time, force: true);
            Debug.Log($"[Assist] wall 0.80 m ahead → {s.FrontMin:F2}; parked car 1.50 m behind → {s.RearMin:F2}");
            Assert.AreEqual(0.8f, s.FrontMin, 0.15f, "wall ahead");
            Assert.AreEqual(1.5f, s.RearMin, 0.2f, "parked car behind");
            Assert.AreEqual(2, ParkAssist.Band(s.FrontMin) >= 1 ? 2 : 0, "warning shown for the wall");
        }

        [UnityTest]
        public IEnumerator Kerbs_And_Narrow_Garage_Walls_Beside_The_Car_Do_Not_Trigger_Front_Or_Rear()
        {
            var (car, s, fp) = Spawn("sedan");
            yield return new WaitForSeconds(0.6f);
            float halfW = Mathf.Max(fp[1].x, fp[2].x);
            Box(new Vector3(0f, 0.075f, MinZ(fp) - 1.0f), new Vector3(4f, 0.15f, 0.3f));                       // 15 cm kerb 1 m behind
            Box(new Vector3(halfW + 0.35f, 1f, 0f), new Vector3(0.2f, 2f, 8f));                                    // garage walls 0.25 m beside
            Box(new Vector3(-halfW - 0.35f, 1f, 0f), new Vector3(0.2f, 2f, 8f));
            yield return new WaitForFixedUpdate(); yield return null;
            s.Tick(car, Time.time, force: true);
            Debug.Log($"[Assist] kerb + garage side walls: front {s.FrontMin}, rear {s.RearMin}");
            Assert.IsTrue(float.IsInfinity(s.RearMin), "a kerb below bumper height is not an obstacle");
            Assert.IsTrue(float.IsInfinity(s.FrontMin), "side walls are not in front");
            Box(new Vector3(0f, 1f, MaxZ(fp) + 0.6f + 0.1f), new Vector3(halfW * 2f + 0.9f, 2f, 0.2f));          // garage back wall 0.6 m ahead
            yield return new WaitForFixedUpdate();
            s.Tick(car, Time.time, force: true);
            Assert.AreEqual(0.6f, s.FrontMin, 0.15f, "end wall of a narrow garage");
        }

        [UnityTest]
        public IEnumerator A_Moving_Obstacle_Is_Tracked_As_It_Approaches()
        {
            var (car, s, fp) = Spawn("sedan");
            yield return new WaitForSeconds(0.6f);
            GameObject mover = Box(new Vector3(0f, 0.6f, MinZ(fp) - 2.4f), new Vector3(2f, 1.2f, 0.5f));
            var rb = mover.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var readings = new List<float>();
            for (int i = 0; i < 6; i++)
            {
                rb.MovePosition(mover.transform.position + Vector3.forward * 0.3f);   // closing in at 0.3 m per step
                yield return new WaitForFixedUpdate();
                s.Tick(car, Time.time, force: true);
                readings.Add(s.RearMin);
            }
            Debug.Log("[Assist] moving obstacle: " + string.Join(" → ", readings.ConvertAll(r => r.ToString("F2"))));
            for (int i = 1; i < readings.Count; i++) Assert.Less(readings[i], readings[i - 1] + 0.01f, "distance shrinks as it approaches");
            Assert.Less(readings[readings.Count - 1], 1.0f);
        }

        [UnityTest]
        public IEnumerator Monster_Truck_And_F1_Measure_From_Their_Own_Bumpers()
        {
            foreach (string id in new[] { "monster_mammoth", "f1_velocity" })
            {
                var (car, s, fp) = Spawn(id);
                yield return new WaitForSeconds(0.8f);
                GameObject wall = Box(car.transform.TransformPoint(new Vector3(0f, 0.6f, MinZ(fp) - 1.0f - 0.1f)), new Vector3(5f, 1.2f, 0.2f));
                yield return new WaitForFixedUpdate();
                s.Tick(car, Time.time, force: true);
                Debug.Log($"[Assist] {id}: wall 1.00 m behind → {s.RearMin:F2}");
                Assert.AreEqual(1.0f, s.RearMin, 0.2f, id);
                Object.Destroy(wall); Object.Destroy(car.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Reverse_Guide_Lines_Follow_The_Path_The_Car_Actually_Drives()
        {
            var (car, _, _) = Spawn("sedan");
            var host = new GameObject("RearCam");
            spawned.Add(host);
            var rear = host.AddComponent<RearViewCamera>();
            rear.Attach(car);
            car.ControlsEnabled = true;
            yield return new WaitForSeconds(0.8f);
            car.Input.SetSteering(0.6f);
            yield return new WaitForSeconds(1.2f);                       // wheels reach the angle while standing
            float steer = car.SteerAngle;
            // Predicted rear-axle path (car-local at the start) for 0.5 … 3 m behind the bumper.
            var predicted = new List<Vector3>();
            for (float m = 0.5f; m <= 3.01f; m += 0.5f) predicted.Add(car.transform.TransformPoint(rear.PathPoint(m, 0f)));
            WheelCollider[] w = car.GetComponentsInChildren<WheelCollider>();
            System.Func<Vector3> axle = () =>
            {
                Vector3 a = Vector3.zero; int n = 0; float rearZ = float.MaxValue;
                foreach (WheelCollider x in w) rearZ = Mathf.Min(rearZ, car.transform.InverseTransformPoint(x.transform.position).z);
                foreach (WheelCollider x in w) if (Mathf.Abs(car.transform.InverseTransformPoint(x.transform.position).z - rearZ) < 0.2f) { a += x.transform.position; n++; }
                return a / Mathf.Max(1, n);
            };
            var driven = new List<Vector3> { axle() };
            car.Input.SetThrottle(-0.35f);
            for (float t = 0f; t < 8f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                driven.Add(axle());
                if (Vector3.Distance(driven[0], driven[driven.Count - 1]) > 5.5f) break;
            }
            car.Input.SetThrottle(0f);
            float worst = 0f;
            foreach (Vector3 p in predicted)
            {
                float best = float.MaxValue;
                for (int i = 1; i < driven.Count; i++)
                {
                    Vector3 a = driven[i - 1], b = driven[i];
                    Vector3 ab = b - a; ab.y = 0f; Vector3 ap = p - a; ap.y = 0f;
                    float k = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
                    Vector3 c = a + ab * k; c.y = p.y;
                    best = Mathf.Min(best, Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(c.x, 0, c.z)));
                }
                worst = Mathf.Max(worst, best);
            }
            Debug.Log($"[Assist] guide lines vs driven path at {steer:F1}° steer: worst gap {worst:F2} m over 0.5–3 m");
            Assert.Less(worst, 0.3f, "guide lines follow the real reversing path");
        }
    }
}
