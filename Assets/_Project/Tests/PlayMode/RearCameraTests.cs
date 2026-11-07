using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.UI;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Backup camera and VIEW toggle in the real Gameplay scene (level 2, Driving School reverse bay): the rear view
    /// appears when reversing and hides after driving forward, measures the gap to the wall behind, its guide path
    /// bends with the steering, and the VIEW button switches the main camera to a high top-down view.
    /// </summary>
    public class RearCameraTests
    {
        private static LevelManager Manager => LevelManager.Instance;
        private static VehicleController Car => LevelLoader.Instance.Vehicle;
        private static RearCameraView View => Object.FindAnyObjectByType<RearCameraView>();

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.ResetToEmpty();

        private static IEnumerator Load()
        {
#if UNITY_EDITOR
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
            LevelSelection.Request(catalog.Get(1), catalog);
#endif
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while ((Manager == null || Manager.State != LevelState.Playing) && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(LevelState.Playing, Manager.State);
            Car.Input.KeyboardEnabled = false;
            yield return new WaitForSeconds(0.3f);
        }

        private static IEnumerator Place(Vector3 pos, float yaw, float settle = 0.5f)
        {
            Car.ResetVehicle(pos + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            yield return new WaitForSeconds(settle);
        }

        [UnityTest]
        public IEnumerator Rear_View_Shows_While_Reversing_And_Hides_After_Driving_Forward()
        {
            yield return Load();
            RearCameraView view = View;
            Assert.IsNotNull(view, "HUD has the backup camera panel");
            Assert.IsFalse(view.Showing, "hidden while parked / driving forward");
            var rev = (ReverseParkObjective)Manager.Mission.Current;
            yield return Place(new Vector3(rev.Position.x, 0f, 0.5f), 180f);
            Car.Input.SetThrottle(-0.6f);
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(Car.IsReversing, "car reversing");
            RearViewCamera rear = view.RearCamera;
            Assert.IsTrue(rear.Active);
            Assert.IsTrue(view.Showing, "panel visible while reversing");
            Assert.IsNotNull(rear.Texture);
            Assert.IsTrue(rear.Camera.enabled, "rear camera renders only while needed");
            Assert.Less(Vector3.Angle(rear.Camera.transform.forward, -Car.transform.forward), 40f, "looks backwards");
            Assert.IsNotEmpty(view.DistanceText);
            SaveRearImage(rear, "rear_camera_reversing.png");

            Car.Input.SetThrottle(0.6f);
            yield return new WaitForSeconds(3.5f);
            Car.Input.SetThrottle(0f);
            Assert.IsFalse(rear.Active, "off again after driving forward (linger 1.5 s)");
            Assert.IsFalse(rear.Camera.enabled, "camera stops rendering");
            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(view.Showing);
        }

        [UnityTest]
        public IEnumerator Rear_Distance_Measures_The_Wall_And_Guides_Bend_With_Steering()
        {
            yield return Load();
            var rev = (ReverseParkObjective)Manager.Mission.Current;
            RearViewCamera rear = View.RearCamera;
            rear.ForceOn = true;
            float backLine = rev.Position.z + rev.Size.y * 0.5f;
            // Find the wall behind the bay: first collider hit going +Z along the bay centre, at bumper height.
            Assert.IsTrue(Physics.Raycast(new Vector3(rev.Position.x, 0.5f, rev.Position.z), Vector3.forward, out RaycastHit _, 10f), "back wall exists");
            yield return Place(new Vector3(rev.Position.x, 0f, rev.Position.z), 180f, 0.6f);   // facing out, rear towards the wall
            yield return null;
            yield return new WaitForSeconds(0.2f);
            float measured = rear.RearDistance;
            // Independent check: ray from the car's actual rear-most point.
            Vector3 rearPoint = Car.transform.TransformPoint(new Vector3(0f, 0.5f, rear.PathPoint(0f, 0f).z));
            Assert.IsTrue(Physics.Raycast(rearPoint + Car.transform.forward * 0.02f, -Car.transform.forward, out RaycastHit check, 6f));
            Debug.Log($"[Test] rear distance {measured:F2} m, raycast from bumper {check.distance:F2} m, back line at z {backLine:F2}");
            Assert.AreEqual(check.distance, measured, 0.35f, "rear sensor distance");

            // Guide path: straight with no steering, bends to the steering side.
            Car.Input.SetSteering(0f);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(0f, rear.PathPoint(3f, 0f).x, 0.15f, "straight back");
            Car.Input.SetSteering(1f);
            yield return new WaitForSeconds(0.8f);
            float right = rear.PathPoint(3f, 0f).x;
            Car.Input.SetSteering(-1f);
            yield return new WaitForSeconds(1.2f);
            float left = rear.PathPoint(3f, 0f).x;
            Car.Input.SetSteering(0f);
            Debug.Log($"[Test] guide path 3 m behind: steer right x={right:F2}, steer left x={left:F2}");
            Assert.Greater(right, 0.5f, "steering right swings the rear right");
            Assert.Less(left, -0.5f, "steering left swings the rear left");
            Assert.Greater(rear.PathPoint(3f, 1f).x, rear.PathPoint(3f, -1f).x, "right track is right of the left track");
        }

        [UnityTest]
        public IEnumerator View_Button_Switches_To_Top_Down_And_Back()
        {
            yield return Load();
            var hud = Object.FindAnyObjectByType<GameplayHud>();
            var cam = Object.FindAnyObjectByType<VehicleCamera>();
            Assert.AreEqual("VIEW: CHASE", hud.ViewText);
            float chaseHeight = cam.transform.position.y - Car.transform.position.y;
            hud.ToggleView();
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(cam.TopDown);
            Assert.AreEqual("VIEW: TOP", hud.ViewText);
            float topHeight = cam.transform.position.y - Car.transform.position.y;
            Debug.Log($"[Test] camera height chase {chaseHeight:F1} m, top-down {topHeight:F1} m, pitch {cam.transform.eulerAngles.x:F0}");
            Assert.Greater(topHeight, 10f, "high overhead");
            Assert.Greater(cam.transform.eulerAngles.x, 60f, "looking down");
            hud.ToggleView();
            yield return new WaitForSeconds(1.5f);
            Assert.IsFalse(cam.TopDown);
            Assert.Less(cam.transform.position.y - Car.transform.position.y, 6f, "back to the chase view");
        }

        private static void SaveRearImage(RearViewCamera rear, string file)
        {
            rear.Camera.Render();
            RenderTexture.active = rear.Texture;
            var tex = new Texture2D(rear.Texture.width, rear.Texture.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            RenderTexture.active = null;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../UltimateCarParking3D_logs/camera"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
