using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Cars;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Save;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>Each of the five cars: drives, respects its own top speed, stays upright, and fits the tightest bays.</summary>
    public class CarFleetTests
    {
        private static CarCatalog Cars()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");
#else
            return null;
#endif
        }

        private static IEnumerable<string> CarIds() => new[] { "sedan", "hatch", "sport", "suv", "pickup" };

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.ResetToEmpty();

        [UnityTest]
        public IEnumerator Car_Drives_And_Stays_Upright([ValueSource(nameof(CarIds))] string carId)
        {
            Time.timeScale = 3f;
            CarData data = Cars().FindById(carId);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            GameObject car = Object.Instantiate(data.ModelPrefab, new Vector3(0f, 0.3f, 0f), Quaternion.identity);
            data.ApplyTo(car);
            var c = car.GetComponent<VehicleController>();
            var input = car.GetComponent<VehicleInput>();
            input.KeyboardEnabled = false;
            Assert.AreEqual(data.Handling.mass, c.Body.mass, 0.01f, "handling applied");
            Assert.AreSame(data.PaintMaterial, car.GetComponent<CarPaint>().CurrentPaint, "paint applied");

            float minUp = 1f, peak = 0f;
            IEnumerator Run(float throttle, float steer, float seconds)
            {
                input.SetThrottle(throttle);
                input.SetSteering(steer);
                for (float t = 0f; t < seconds; t += Time.fixedDeltaTime)
                {
                    yield return new WaitForFixedUpdate();
                    minUp = Mathf.Min(minUp, car.transform.up.y);
                    peak = Mathf.Max(peak, c.ForwardSpeedKmh);
                }
            }
            yield return Run(0f, 0f, 1f);
            yield return Run(1f, 0f, 12f);
            float top = c.ForwardSpeedKmh;
            for (int i = 0; i < 6; i++) yield return Run(1f, i % 2 == 0 ? 1f : -1f, 1.2f);
            yield return Run(0f, 1f, 2f);
            yield return Run(-1f, -1f, 3f);
            float tilt = Mathf.Acos(Mathf.Clamp(minUp, -1f, 1f)) * Mathf.Rad2Deg;
            Debug.Log($"[Test] {data.DisplayName}: top {top:F1}/{data.Handling.maxForwardSpeedKmh} km/h (peak {peak:F1}), max tilt {tilt:F1} deg, {data.Handling.driveType}, {data.Handling.mass} kg");
            Assert.LessOrEqual(peak, data.Handling.maxForwardSpeedKmh + 1.5f);
            Assert.Greater(top, data.Handling.maxForwardSpeedKmh - 12f);
            Assert.Less(tilt, 30f, "stays upright");
            Object.Destroy(car);
            Object.Destroy(ground);
        }

        [UnityTest]
        public IEnumerator Car_Fits_The_Tightest_Bay_And_Parallel_Slot([ValueSource(nameof(CarIds))] string carId)
        {
            Time.timeScale = 3f;
            SaveData save = SaveManager.Current.Data;
            CarData data = Cars().FindById(carId);
            save.ownedCarIds.Add(carId);
            save.selectedCarId = carId;
#if UNITY_EDITOR
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
#else
            LevelCatalog catalog = null;
#endif
            foreach (int n in new[] { 29, 30 })   // narrowest regular bay, tightest parallel slot
            {
                LevelData level = catalog.Get(n - 1);
                LevelSelection.Request(level, catalog);
                SceneManager.LoadScene(SceneNames.Gameplay);
                float t = 0f;
                while ((LevelManager.Instance == null || LevelManager.Instance.Level != level || LevelManager.Instance.State != LevelState.Playing) && t < 8f)
                { t += Time.unscaledDeltaTime; yield return null; }
                VehicleController car = LevelLoader.Instance.Vehicle;
                Assert.AreSame(data, LevelLoader.Instance.CurrentCar, "the selected car is spawned");
                car.ResetVehicle(level.TargetPosition + Vector3.up * 0.05f, level.TargetRotation);
                t = 0f;
                while (LevelManager.Instance.State == LevelState.Playing && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
                Assert.AreEqual(LevelState.Completed, LevelManager.Instance.State, $"{data.DisplayName} parks in {level.LevelId} ({LevelManager.Instance.Validator.Status})");
            }
        }
    }
}
