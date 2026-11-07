using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Parking;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Phase D: Garage B1 — the real-model level — loaded through the normal Gameplay flow. Checks the car, the bay,
    /// reachability, the ramp, camera clearance under the ceiling, and renders views to
    /// ../UltimateCarParking3D_logs/garage/*.png for visual review.
    /// </summary>
    public class RealGarageLevelTests
    {
        private const string LevelPath = "Assets/_Project/Levels/Real/Level_RG01.asset";
        private const string CatalogPath = "Assets/_Project/Levels/Real/RealCatalog.asset";
        private LevelData level;

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.ResetToEmpty();

        private IEnumerator Load()
        {
#if UNITY_EDITOR
            level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
            LevelSelection.Request(level, AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath));
#endif
            Assert.IsNotNull(level, "level asset");
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while ((LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(LevelState.Playing, LevelManager.Instance.State);
            LevelLoader.Instance.Vehicle.Input.KeyboardEnabled = false;
            yield return new WaitForSeconds(0.5f);
        }

        private static VehicleController Car => LevelLoader.Instance.Vehicle;

        [UnityTest]
        public IEnumerator Loads_With_The_Real_Sedan_And_Completes_When_Parked()
        {
            yield return Load();
            Assert.AreEqual("Car_RealSedan", Car.gameObject.name, "the forced real car is spawned");
            Assert.IsNotNull(LevelLoader.Instance.Environment, "environment prefab spawned");
            Assert.AreEqual(0, LevelManager.Instance.Collisions, "spawn clear");
            Car.ResetVehicle(level.TargetPosition + Vector3.up * 0.05f, level.TargetRotation);
            float t = 0f;
            while (LevelManager.Instance.State == LevelState.Playing && t < 6f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(LevelState.Completed, LevelManager.Instance.State, $"parks in the target bay ({LevelManager.Instance.Validator.Status})");
        }

        [UnityTest]
        public IEnumerator Target_Bay_Is_Clear_And_Reachable()
        {
            yield return Load();
            Collider[] inBay = Physics.OverlapBox(level.TargetPosition + Vector3.up * 0.95f,
                new Vector3(level.TargetSize.x * 0.5f - 0.1f, 0.6f, level.TargetSize.y * 0.5f - 0.1f), level.TargetRotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider c in inBay) Assert.AreSame(Car.Body, c.attachedRigidbody, $"bay blocked by {c.name}");

            // Coarse reachability: a 1.9 m-wide box can travel from the spawn down the aisle to the bay mouth, then in.
            Vector3 from = Car.transform.position, mouth = new Vector3(level.TargetPosition.x, 0f, 0f);
            Assert.IsTrue(SweepClear(from, new Vector3(mouth.x, 0f, from.z), Car.Body), "aisle from the entry to the bay is clear");
            Assert.IsTrue(SweepClear(new Vector3(mouth.x, 0f, from.z), level.TargetPosition, Car.Body), "bay mouth to bay centre is clear");
        }

        private static bool SweepClear(Vector3 a, Vector3 b, Rigidbody self)
        {
            Vector3 d = b - a; d.y = 0f;
            foreach (RaycastHit h in Physics.BoxCastAll(a + Vector3.up * 0.95f, new Vector3(0.95f, 0.55f, 0.95f), d.normalized, Quaternion.identity, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (h.rigidbody != self && h.distance > 0f) { Debug.Log($"[Test] blocked by {h.collider.name} at {h.point}"); return false; }
            return true;
        }

        [UnityTest]
        public IEnumerator Ramp_Is_Drivable_And_Camera_Stays_Under_The_Ceiling()
        {
            yield return Load();
            var cam = Object.FindAnyObjectByType<VehicleCamera>();
            float maxCamY = 0f;
            // Drive down the aisle (straight), camera must stay below the beams (3.38 m) inside.
            Car.Input.SetThrottle(0.7f);
            float t = 0f;
            while (Car.transform.position.x < 6f && t < 15f) { t += Time.deltaTime; maxCamY = Mathf.Max(maxCamY, cam.transform.position.y); yield return null; }
            // Then teleport to the ramp foot and drive up.
            Car.ResetVehicle(new Vector3(11f, 0.1f, 0f), Quaternion.Euler(0f, 90f, 0f));
            yield return new WaitForSeconds(0.3f);
            Car.Input.SetThrottle(1f);
            t = 0f;
            while (Car.transform.position.x < 28f && t < 12f) { t += Time.deltaTime; yield return null; }
            Car.Input.SetThrottle(0f);
            Car.Input.SetBrake(1f);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[Test] Garage: camera max height in hall {maxCamY:F2} m; on the ramp landing the car is at y {Car.transform.position.y:F2} (x {Car.transform.position.x:F1})");
            Assert.Less(maxCamY, 3.3f, "camera below the beams");
            Assert.Greater(Car.transform.position.y, 2.0f, "drove up the ramp");
            Assert.Greater(Car.transform.up.y, 0.95f);
        }

        [UnityTest]
        public IEnumerator Render_Views()
        {
            yield return Load();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../UltimateCarParking3D_logs/garage"));
            Directory.CreateDirectory(dir);
            Camera main = Camera.main;
            Capture(main, Path.Combine(dir, "chase_spawn.png"));

            var cam = new GameObject("ShotCam").AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.enabled = false;
            void View(string name, Vector3 from, Vector3 at, float fov = 60f)
            {
                cam.fieldOfView = fov;
                cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
                Capture(cam, Path.Combine(dir, name + ".png"));
            }
            View("hall_from_entry", new Vector3(-12f, 1.7f, -2f), new Vector3(8f, 1.0f, 3f));
            View("hall_bays_north", new Vector3(0f, 1.6f, -2.5f), new Vector3(1.7f, 0.8f, 10f));
            View("ramp", new Vector3(6f, 1.8f, 1.5f), new Vector3(24f, 2.0f, 0f));
            View("entry", new Vector3(-9f, 1.7f, 2f), new Vector3(-27f, 1.2f, -1f));
            // Plan: orthographic, looking down from just under the ceiling (slab culled by the near plane).
            cam.orthographic = true;
            cam.orthographicSize = 17f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 10f;
            cam.transform.SetPositionAndRotation(new Vector3(-6f, 3.3f, 0f), Quaternion.Euler(90f, 0f, 0f));
            Capture(cam, Path.Combine(dir, "plan.png"));

            Car.ResetVehicle(level.TargetPosition + Vector3.up * 0.05f, level.TargetRotation);
            yield return new WaitForSeconds(1.5f);
            Capture(main, Path.Combine(dir, "chase_parked.png"));
            Object.Destroy(cam.gameObject);
            Assert.IsTrue(File.Exists(Path.Combine(dir, "plan.png")));
        }

        private static void Capture(Camera cam, string path)
        {
            var rt = new RenderTexture(1600, 720, 24);
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 720), 0, 0);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(rt);
            Object.Destroy(tex);
        }
    }
}
