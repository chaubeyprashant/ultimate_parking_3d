using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;

namespace UltimateCarParking.Tests
{
    /// <summary>Chase view: drag to look around the car (left / right / up / down); it eases back behind after letting go.</summary>
    public class CameraLookTests
    {
        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() { yield return TestScenes.ResetToEmpty(); }

        [UnityTest]
        public IEnumerator Chase_Camera_Looks_Around_The_Car_And_Returns_Behind()
        {
#if UNITY_EDITOR
            var cat = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
            LevelSelection.Request(cat.Get(0), cat);
#endif
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while ((LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
            var cam = Object.FindAnyObjectByType<VehicleCamera>();
            cam.SetView(CameraView.Chase, instant: true);
            LevelLoader.Instance.Vehicle.Input.KeyboardEnabled = false;
            yield return new WaitForSeconds(1f);
            Transform car = cam.Target;
            Vector3 Behind() { Vector3 d = cam.transform.position - car.position; d.y = 0f; return d.normalized; }
            float startAngle = Vector3.SignedAngle(-car.forward, Behind(), Vector3.up);
            float startHeight = cam.transform.position.y;

            for (int i = 0; i < 20; i++) { cam.Look(5f, 0f); yield return null; }   // drag sideways: 100°
            yield return new WaitForSeconds(0.8f);
            float turned = Vector3.SignedAngle(-car.forward, Behind(), Vector3.up) - startAngle;
            Debug.Log($"[Camera] looked {turned:0}° around the car");
            Assert.Greater(Mathf.Abs(turned), 60f, "camera swung round to the side of the car");

            for (int i = 0; i < 10; i++) { cam.Look(0f, 4f); yield return null; }    // drag up: higher view
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(cam.transform.position.y, startHeight + 0.5f, "camera rose to look down on the car");

            yield return new WaitForSeconds(VehicleCamera.LookReturnDelay + 2.5f);
            Assert.Less(Mathf.Abs(cam.LookYaw), 3f, "eased back behind the car");
            Assert.Less(Mathf.Abs(cam.LookPitch), 3f);
        }
    }
}
