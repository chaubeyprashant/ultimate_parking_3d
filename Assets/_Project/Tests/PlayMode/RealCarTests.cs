using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UltimateCarParking.Parking;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Phase C: the real-model sedan prefab (imported Generic passenger car pack, CC BY 4.0) driven with the unchanged
    /// VehicleController. Also renders previews to ../UltimateCarParking3D_logs/realcar/prefab_*.png.
    /// </summary>
    public class RealCarTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Vehicles/Car_RealSedan.prefab";
        private GameObject ground, car, listener, sun;
        private VehicleController controller;
        private VehicleInput input;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 3f;
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.localScale = new Vector3(1000f, 0.2f, 1000f);
            ground.transform.position = new Vector3(0f, -0.1f, 0f);
            listener = new GameObject("Listener", typeof(AudioListener));
            sun = new GameObject("Sun");
            var l = sun.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = 1.3f; sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
#if UNITY_EDITOR
            car = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), new Vector3(0f, 0.3f, 0f), Quaternion.identity);
#endif
            Assert.IsNotNull(car, "prefab exists");
            controller = car.GetComponent<VehicleController>();
            input = car.GetComponent<VehicleInput>();
            input.KeyboardEnabled = false;
            yield return Run(0f, 0f, 1.5f);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            foreach (var go in new[] { car, ground, listener, sun }) if (go != null) Object.Destroy(go);
        }

        private IEnumerator Run(float throttle, float steer, float seconds, System.Action each = null)
        {
            input.SetThrottle(throttle);
            input.SetSteering(steer);
            yield return null;
            for (float t = 0f; t < seconds; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                each?.Invoke();
            }
            yield return null;
        }

        private Transform Wheel(string n) => car.transform.Find("WheelMeshes/" + n + "Mesh");

        [UnityTest]
        public IEnumerator Prefab_Has_Real_Body_Wheels_And_Simple_Colliders()
        {
            Assert.AreEqual(4, car.GetComponentsInChildren<WheelCollider>().Length);
            Assert.AreEqual(2, car.GetComponentsInChildren<BoxCollider>().Length, "simplified collision: lower body + cabin");
            Assert.AreEqual(0, car.GetComponentsInChildren<MeshCollider>().Length, "no mesh colliders");
            foreach (string part in new[] { "Model/Body", "Model/Glass", "Model/Optics" })
                Assert.IsNotNull(car.transform.Find(part), part);
            foreach (string w in new[] { "FrontLeft", "FrontRight", "RearLeft", "RearRight" })
                Assert.IsNotNull(Wheel(w).GetComponentInChildren<MeshRenderer>(), $"{w} wheel mesh");
            Assert.That(car.transform.Find("Model/Glass").GetComponent<Renderer>().sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo(3000), "glass is transparent");
            int tris = car.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
            Debug.Log($"[Test] Real sedan: {tris} triangles in body+wheels, mass {controller.Body.mass} kg");
            Assert.Less(tris, 20000, "mobile budget");
            yield break;
        }

        [UnityTest]
        public IEnumerator Rests_On_Its_Wheels()
        {
            yield return Run(0f, 0f, 1.5f);
            foreach (string w in new[] { "FrontLeft", "FrontRight", "RearLeft", "RearRight" })
                Assert.That(Wheel(w).position.y, Is.InRange(0.26f, 0.36f), $"{w} wheel centre ~ one radius above ground");
            Assert.That(car.transform.position.y, Is.InRange(-0.08f, 0.08f), "body pivot at ground level (not floating or sunk)");
            Assert.Greater(car.transform.up.y, 0.995f, "level");
        }

        [UnityTest]
        public IEnumerator Drives_Forward_Along_Its_Nose_Reverses_And_Brakes()
        {
            Vector3 start = car.transform.position;
            yield return Run(1f, 0f, 4f);
            float forward = car.transform.position.z - start.z;
            float speed = controller.ForwardSpeedKmh;
            yield return Run(0f, 0f, 0f);
            input.SetBrake(1f);
            yield return Run(0f, 0f, 4f);
            input.SetBrake(0f);
            float stopped = controller.SpeedKmh;
            Vector3 mid = car.transform.position;
            yield return Run(-1f, 0f, 4f);
            float back = car.transform.position.z - mid.z;
            Debug.Log($"[Test] Real sedan: forward {forward:F1} m ({speed:F0} km/h) along +Z, braked to {stopped:F1} km/h, reversed {back:F1} m");
            Assert.Greater(forward, 10f, "throttle moves the car toward its nose (+Z)");
            Assert.Less(stopped, 0.5f);
            Assert.Less(back, -4f);
        }

        [UnityTest]
        public IEnumerator Steers_And_Wheel_Meshes_Spin_And_Turn()
        {
            float yaw0 = car.transform.eulerAngles.y;
            yield return Run(0.6f, 1f, 1.2f);
            Quaternion inv = Quaternion.Inverse(car.transform.rotation);
            float frontYaw = Vector3.SignedAngle(Vector3.right, inv * Wheel("FrontLeft").right, Vector3.up);
            float rearYaw = Vector3.SignedAngle(Vector3.right, inv * Wheel("RearLeft").right, Vector3.up);
            Quaternion spin0 = Wheel("RearLeft").localRotation;
            // Accumulate per step: a tight car can turn past 180° in 2.5 s, which a single DeltaAngle would wrap.
            float turned = Mathf.DeltaAngle(yaw0, car.transform.eulerAngles.y), lastYaw = car.transform.eulerAngles.y;
            yield return Run(0.6f, 1f, 2.5f, () => { turned += Mathf.DeltaAngle(lastYaw, car.transform.eulerAngles.y); lastYaw = car.transform.eulerAngles.y; });
            Debug.Log($"[Test] Real sedan: front wheel yaw {frontYaw:F1}°, rear {rearYaw:F1}°, car turned {turned:F0}° right");
            Assert.Greater(frontYaw, 10f, "front wheel meshes steer");
            Assert.Less(Mathf.Abs(rearYaw), 1f, "rear wheel meshes do not steer");
            Assert.AreNotEqual(spin0, Wheel("RearLeft").localRotation, "wheel meshes spin");
            Assert.Greater(turned, 30f, "steering right turns right");
        }

        [UnityTest]
        public IEnumerator Stays_Upright_In_Hard_Driving()
        {
            float minUp = 1f;
            void Track() => minUp = Mathf.Min(minUp, car.transform.up.y);
            yield return Run(1f, 0f, 8f, Track);
            for (int i = 0; i < 6; i++) yield return Run(1f, i % 2 == 0 ? 1f : -1f, 1.2f, Track);
            input.SetHandbrake(true);
            yield return Run(0f, 1f, 2f, Track);
            input.SetHandbrake(false);
            float tilt = Mathf.Acos(minUp) * Mathf.Rad2Deg;
            Debug.Log($"[Test] Real sedan: max tilt {tilt:F1}° through slalom + handbrake turn");
            Assert.Less(tilt, 30f);
        }

        [UnityTest]
        public IEnumerator Footprint_Fits_The_Tightest_Bay()
        {
            var zone = new GameObject("Zone").AddComponent<ParkingZone>();
            zone.transform.position = new Vector3(0f, 0f, 30f);
            zone.Configure(new Vector2(2.65f, 5.6f), null);
            var validator = new GameObject("Validator").AddComponent<ParkingValidator>();
            validator.Bind(zone, controller);
            Vector3[] fp = validator.LocalFootprint;
            float w = fp[1].x - fp[0].x, l = fp[2].z - fp[1].z;
            Debug.Log($"[Test] Real sedan footprint {w:F2} x {l:F2} m");
            controller.ResetVehicle(new Vector3(0f, 0.05f, 30f), Quaternion.identity);
            validator.SetArmed(true);
            yield return Run(0f, 0f, 0.6f);
            Assert.AreEqual(4, validator.CornersInside, "all corners inside a 2.65 x 5.6 bay");
            Assert.That(w, Is.InRange(1.8f, 2.0f));
            Assert.That(l, Is.InRange(4.2f, 4.5f));
            Object.Destroy(zone.gameObject);
            Object.Destroy(validator.gameObject);
        }

        [UnityTest]
        public IEnumerator Render_Previews()
        {
            yield return Run(0f, 0f, 1f);
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../UltimateCarParking3D_logs/realcar"));
            Directory.CreateDirectory(dir);
            var cam = new GameObject("PreviewCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.22f, 0.25f);
            cam.fieldOfView = 30f;
            Vector3 c = car.transform.position + Vector3.up * 0.6f;
            void Shot(string name, Vector3 offset)
            {
                cam.transform.SetPositionAndRotation(c + offset, Quaternion.LookRotation(-offset));
                var rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                File.WriteAllBytes(Path.Combine(dir, $"prefab_{name}.png"), tex.EncodeToPNG());
                RenderTexture.active = null; cam.targetTexture = null;
                Object.Destroy(rt); Object.Destroy(tex);
            }
            Shot("front_three_quarter", new Vector3(5f, 2.2f, 7f));
            Shot("side", new Vector3(10f, 0.6f, 0f));
            Shot("rear_three_quarter", new Vector3(-5f, 2.2f, -7f));
            Object.Destroy(cam.gameObject);
            Assert.IsTrue(File.Exists(Path.Combine(dir, "prefab_side.png")));
        }
    }
}
