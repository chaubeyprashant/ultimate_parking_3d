using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Cars;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Parking;
using UltimateCarParking.Settings;
using SettingsService = UltimateCarParking.Settings.SettingsService;
using UltimateCarParking.UI;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>Phase 3: every camera view with every garage car, parking sensors, collision incidents, remembered camera.</summary>
    public class CameraAndAssistTests
    {
        private static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureFramerate = 0;
            yield return TestScenes.ResetToEmpty();
        }

        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// <summary>The visible car in its own local space (renderer local bounds).</summary>
        private static Bounds LocalBounds(GameObject car)
        {
            var b = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
            foreach (Renderer r in car.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer)) continue;
                Bounds lb = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 l = car.transform.InverseTransformPoint(r.transform.TransformPoint(new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x, (i & 2) == 0 ? lb.min.y : lb.max.y, (i & 4) == 0 ? lb.min.z : lb.max.z)));
                    if (first) { b = new Bounds(l, Vector3.zero); first = false; } else b.Encapsulate(l);
                }
            }
            return b;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Every_View_Frames_Every_Garage_Car_Without_Clipping()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.transform.position = new Vector3(0f, -0.5f, 0f);
            pad.transform.localScale = new Vector3(200f, 1f, 200f);
            var camGo = new GameObject("TestCamera", typeof(Camera));
            var vcam = camGo.AddComponent<VehicleCamera>();
            var problems = new List<string>();
            var cockpits = new List<string>();
            var chaseBack = new Dictionary<string, float>();
            for (int i = 0; i < catalog.Count; i++)
            {
                CarData data = catalog.Get(i);
                GameObject car = Object.Instantiate(data.ModelPrefab, new Vector3(0f, 0.6f, 0f), Quaternion.identity);
                data.ApplyTo(car);
                car.GetComponent<VehicleInput>().KeyboardEnabled = false;
                yield return Frames(40);
                vcam.SetTarget(car.transform);
                Bounds b = LocalBounds(car);
                if (vcam.CockpitAvailable) cockpits.Add(data.CarId);
                foreach (CameraView v in new[] { CameraView.Chase, CameraView.TopDown, CameraView.Bumper, CameraView.Cockpit, CameraView.Orbit })
                {
                    vcam.SetView(v, instant: true);
                    if (v == CameraView.Orbit) vcam.Orbit(120f, 10f);
                    yield return Frames(30);
                    Vector3 p = camGo.transform.position;
                    Vector3 l = car.transform.InverseTransformPoint(p);
                    if (float.IsNaN(p.x)) { problems.Add($"{data.CarId} {v}: NaN"); continue; }
                    bool inside = b.Contains(l);
                    CameraView actual = vcam.View;
                    if ((actual == CameraView.Chase || actual == CameraView.TopDown || actual == CameraView.Orbit) && inside)
                        problems.Add($"{data.CarId} {actual}: camera inside the car");
                    if (actual == CameraView.Bumper && l.z < b.max.z - 0.15f) problems.Add($"{data.CarId} bumper camera behind the bumper (z {l.z:F2} vs {b.max.z:F2})");
                    if (actual == CameraView.Cockpit && !inside) problems.Add($"{data.CarId} cockpit camera outside the car");
                    if (p.y < 0.2f) problems.Add($"{data.CarId} {actual}: camera at ground level ({p.y:F2})");
                    if (v == CameraView.Chase) chaseBack[data.CarId] = -l.z;
                }
                Object.Destroy(car);
                yield return null;
            }
            Object.Destroy(camGo); Object.Destroy(pad);
            Debug.Log($"[Test] Cockpit view available on {cockpits.Count}/{catalog.Count}: {string.Join(", ", cockpits)}");
            Debug.Log($"[Test] Chase distance behind: hatch {chaseBack["hatch"]:F1} m, monster_mammoth {chaseBack["monster_mammoth"]:F1} m");
            Assert.Greater(chaseBack["monster_mammoth"], chaseBack["hatch"] + 0.5f, "bigger cars are framed from further back");
            Assert.IsEmpty(problems, string.Join("\n", problems));
#else
            yield break;
#endif
        }

        [Test]
        public void Zoom_Stays_In_The_4_To_14_Metre_Range_And_Orbit_Wraps()
        {
            var go = new GameObject("Cam", typeof(Camera));
            var vcam = go.AddComponent<VehicleCamera>();
            vcam.Distance = 2f; Assert.AreEqual(4f, vcam.Distance);
            vcam.Distance = 30f; Assert.AreEqual(14f, vcam.Distance);
            vcam.Orbit(400f, 200f);
            Assert.That(vcam.OrbitYaw, Is.InRange(-180f, 180f));
            Object.DestroyImmediate(go);
        }

        private static IEnumerator LoadT01()
        {
#if UNITY_EDITOR
            var cat = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/Test/TestCatalog.asset");
            LevelSelection.Request(cat.Get(0), cat);
#endif
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while (LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) { t += Time.unscaledDeltaTime; if (t > 6f) Assert.Fail("level"); yield return null; }
            LevelLoader.Instance.Vehicle.Input.KeyboardEnabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Camera_View_And_Distance_Are_Remembered()
        {
            SettingsService.SetCameraView((int)CameraView.Orbit);
            SettingsService.SetChaseDistance(10f);
            yield return LoadT01();
            var cam = Find<VehicleCamera>();
            Assert.AreEqual(CameraView.Orbit, cam.View);
            Assert.AreEqual(10f, cam.Distance, 1e-3f);
            Assert.AreEqual("ORBIT", Find<GameplayHud>().ViewText);
            Find<GameplayHud>().ToggleView();
            Assert.AreEqual((int)CameraView.Chase, SettingsService.Current.cameraView, "cycle wraps to chase and is saved");
        }

        [UnityTest]
        public IEnumerator Rear_Sensors_Measure_An_Obstacle_And_Expert_Hides_Them()
        {
            SettingsService.SetAssistMode(GameSettings.AssistBeginner);
            yield return LoadT01();
            VehicleController car = LevelLoader.Instance.Vehicle;
            var view = Find<ParkAssistView>();
            var validator = Find<ParkingValidator>();
            validator.EnsureFootprint(car);
            float rearZ = float.MaxValue;
            foreach (Vector3 c in validator.LocalFootprint) rearZ = Mathf.Min(rearZ, c.z);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetPositionAndRotation(car.transform.TransformPoint(new Vector3(0f, 0.6f, rearZ - 1.0f - 0.1f)), car.transform.rotation);
            wall.transform.localScale = new Vector3(4f, 1.2f, 0.2f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.3f);
            float d = view.Sensors.RearMin;
            Debug.Log($"[Test] Rear sensor: wall at 1.00 m → {d:F2} m; zones {view.Sensors.Rear(0):F2} / {view.Sensors.Rear(1):F2} / {view.Sensors.Rear(2):F2}; front {view.Sensors.FrontMin}");
            Assert.AreEqual(1.0f, d, 0.15f, "rear distance");
            Assert.IsTrue(float.IsInfinity(view.Sensors.FrontMin), "nothing in front");
            Assert.IsTrue(view.Showing, "sensor widget shown when something is near");
            Assert.AreEqual(2, ParkAssist.Band(0.5f));

            SettingsService.SetAssistMode(GameSettings.AssistExpert);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(view.Showing, "Expert: no sensors");
            Object.Destroy(wall);
        }

        [UnityTest]
        public IEnumerator One_Crash_Into_A_Multi_Part_Obstacle_Counts_Once()
        {
#if UNITY_EDITOR
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.transform.position = new Vector3(0f, -0.5f, 0f);
            pad.transform.localScale = new Vector3(100f, 1f, 100f);
            // A "parked car" made of three touching boxes across the lane.
            for (int i = -1; i <= 1; i++)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.transform.position = new Vector3(i * 0.9f, 0.6f, 14f);
                part.transform.localScale = new Vector3(0.9f, 1.2f, 0.6f);
            }
            CarData data = catalog.FindById("sedan");
            GameObject car = Object.Instantiate(data.ModelPrefab, new Vector3(0f, 0.6f, 0f), Quaternion.identity);
            data.ApplyTo(car);
            var input = car.GetComponent<VehicleInput>();
            input.KeyboardEnabled = false;
            var hits = car.GetComponent<CollisionHandler>();
            var severities = new List<CollisionHandler.HitSeverity>();
            hits.PenaltyRegistered += p => severities.Add(p.Severity);
            yield return new WaitForSeconds(1f);
            input.SetThrottle(1f);
            yield return new WaitForSeconds(3f);
            input.SetThrottle(0f);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[Test] Multi-part crash: counted {hits.CollisionCount} ({string.Join(", ", severities)})");
            Assert.AreEqual(1, hits.CollisionCount, "one crash, one count");
            Assert.AreEqual(CollisionHandler.HitSeverity.Impact, severities[0]);
            Object.Destroy(car);
#else
            yield break;
#endif
        }
    }
}
