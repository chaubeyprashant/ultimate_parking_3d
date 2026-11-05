using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Tests
{
    /// <summary>The generated scenes as shipped: Bootstrap opens the main menu; Gameplay builds level 1 and the car drives.</summary>
    public class GameplaySceneTests
    {
        [SetUp]
        public void UseTemporarySave() => TestScenes.UseTemporarySave();

        [UnityTest]
        public IEnumerator Bootstrap_Opens_The_Main_Menu()
        {
            SceneManager.LoadScene(SceneNames.Bootstrap);
            float wait = 0f;
            while (SceneManager.GetActiveScene().name != SceneNames.MainMenu && wait < 10f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(SceneNames.MainMenu, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Gameplay_Builds_Level_1_And_The_Car_Drives()
        {
            SceneManager.LoadScene(SceneNames.Gameplay);
            float wait = 0f;
            while ((LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) && wait < 10f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            var car = Object.FindAnyObjectByType<VehicleController>();
            var cam = Object.FindAnyObjectByType<VehicleCamera>();
            Assert.IsNotNull(car, "player car in Gameplay");
            Assert.AreEqual("L01", LevelManager.Instance.Level.LevelId, "default level is campaign level 1");
            Assert.AreSame(car.transform, cam.Target);

            car.Input.KeyboardEnabled = false;
            Vector3 start = car.transform.position;
            car.Input.SetThrottle(1f);
            yield return new WaitForSeconds(3f);
            car.Input.SetThrottle(0f);
            float travelled = Vector3.Dot(car.transform.position - start, car.transform.forward);
            Debug.Log($"[Test] Level 1 lot: drove {travelled:F1} m in 3 s, {car.SpeedKmh:F1} km/h, up.y {car.transform.up.y:F3}, camera {Vector3.Distance(cam.transform.position, car.transform.position):F1} m behind");
            Assert.Greater(travelled, 8f);
            Assert.Greater(car.transform.up.y, 0.95f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.ResetToEmpty();
        }
    }
}
