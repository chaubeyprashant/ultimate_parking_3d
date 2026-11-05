using System;
using System.Collections;
using System.Text.RegularExpressions;
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
    /// Phase 2 acceptance tests in the real Gameplay scene: the LevelLoader builds the level from LevelData and
    /// the LevelManager runs it. The car is placed with VehicleController.ResetVehicle or driven through the
    /// mobile input methods. Measured values are logged with a "[Test]" prefix.
    /// </summary>
    public class ParkingLevelTests
    {
        private const string CatalogPath = "Assets/_Project/Levels/Test/TestCatalog.asset";   // the Phase 2 fixture levels

        private LevelManager Manager => LevelManager.Instance;
        private LevelLoader Loader => LevelLoader.Instance;
        private VehicleController Car => Loader.Vehicle;
        private ParkingValidator Validator => Manager.Validator;

        [SetUp]
        public void UseTemporarySave() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.ResetToEmpty();
        }

        // ---------------------------------------------------------------- helpers

        private static LevelCatalog Catalog()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
#else
            return null;
#endif
        }

        /// <summary>A runtime copy of level 1 with some fields changed (assets on disk are untouched).</summary>
        private static LevelData Variant(params (string field, object value)[] edits)
        {
            LevelData copy = UnityEngine.Object.Instantiate(Catalog().Get(0));
#if UNITY_EDITOR
            var so = new SerializedObject(copy);
            foreach (var (field, value) in edits)
            {
                SerializedProperty p = so.FindProperty(field);
                switch (value)
                {
                    case float f: p.floatValue = f; break;
                    case int i: p.intValue = i; break;
                    case bool b: p.boolValue = b; break;
                    case Vector3 v: p.vector3Value = v; break;
                    default: throw new ArgumentException(field);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
            return copy;
        }

        private IEnumerator Load(LevelData level)
        {
            if (level == null) LevelSelection.Request(Catalog().Get(0), Catalog());
            else LevelSelection.Request(level);
            SceneManager.LoadScene(SceneNames.Gameplay);
            yield return WaitUntil(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 5f, "level reaches Playing");
            Car.Input.KeyboardEnabled = false;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeout, string what)
        {
            float t = 0f;
            while (!condition())
            {
                t += Time.unscaledDeltaTime;
                if (t > timeout) Assert.Fail($"timed out waiting for: {what}");
                yield return null;
            }
        }

        private static IEnumerator Seconds(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.deltaTime; yield return null; }
        }

        /// <summary>Teleports the car into the target bay, offset in the bay's local frame.</summary>
        private void PlaceInBay(Vector3 localOffset, float yawOffset)
        {
            ParkingZone zone = Loader.Zone;
            Quaternion rot = zone.Rotation * Quaternion.Euler(0f, yawOffset, 0f);
            Car.ResetVehicle(zone.Center + zone.Rotation * localOffset + Vector3.up * 0.05f, rot);
        }

        private void Drive(float throttle, float steer = 0f) { Car.Input.SetThrottle(throttle); Car.Input.SetSteering(steer); }

        // ---------------------------------------------------------------- loading

        [UnityTest]
        public IEnumerator Level_Loads_World_Car_Bay_And_Camera()
        {
            yield return Load(null);
            LevelData level = Catalog().Get(0);
            Assert.AreEqual(level.LevelId, Manager.Level.LevelId, "default level is catalog[0]");
            Assert.IsNotNull(Loader.Environment, "environment spawned");
            Assert.Less(Vector3.Distance(Car.transform.position, level.SpawnPosition), 1f, "car at the spawn point");
            Assert.Less(Quaternion.Angle(Car.transform.rotation, level.SpawnRotation), 2f);
            Assert.Less(Vector3.Distance(Loader.Zone.Center, level.TargetPosition), 0.01f, "bay at the target");
            Assert.AreEqual(level.TargetSize, Loader.Zone.Size);
            Assert.AreSame(Car.transform, UnityEngine.Object.FindAnyObjectByType<VehicleCamera>().Target);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<LevelManager>().Length);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<VehicleController>().Length);
            Assert.AreEqual(ParkingStatus.Outside, Validator.Status);
            Assert.IsTrue(Car.ControlsEnabled);
        }

        // ---------------------------------------------------------------- validation

        [UnityTest]
        public IEnumerator Centred_And_Stopped_Completes_With_Three_Stars()
        {
            yield return Load(null);
            PlaceInBay(Vector3.zero, 0f);
            float placedAt = Manager.ElapsedSeconds;
            yield return WaitUntil(() => Manager.State != LevelState.Playing, 5f, "level ends");
            float holdTime = Manager.ElapsedSeconds - placedAt;
            LevelResult r = Manager.LastResult.Value;
            Debug.Log($"[Test] Parked centred: {r}; completed {holdTime:F2}s after placing (hold {Validator.ActiveRules.holdDuration}s)");
            Assert.AreEqual(LevelState.Completed, Manager.State);
            Assert.AreEqual(ParkingStatus.Parked, Validator.Status);
            Assert.AreEqual(3, r.Stars);
            Assert.AreEqual(Manager.Level.CoinsFor(3), r.Coins);
            Assert.GreaterOrEqual(holdTime, Validator.ActiveRules.holdDuration - 0.05f, "must hold for the full duration");
            Assert.IsFalse(Car.ControlsEnabled, "car is frozen after completion");
        }

        [UnityTest]
        public IEnumerator Reversed_In_Also_Completes()
        {
            yield return Load(null);
            PlaceInBay(Vector3.zero, 180f);
            yield return WaitUntil(() => Manager.State != LevelState.Playing, 5f, "level ends");
            Assert.AreEqual(LevelState.Completed, Manager.State);
        }

        [UnityTest]
        public IEnumerator Partially_Inside_Does_Not_Complete()
        {
            yield return Load(null);
            PlaceInBay(new Vector3(0f, 0f, 1.6f), 0f);   // nose 1 m past the front line
            yield return Seconds(3f);
            Debug.Log($"[Test] Overhanging: {Validator.Status}, {Validator.CornersInside}/4 corners inside");
            Assert.AreEqual(LevelState.Playing, Manager.State);
            Assert.AreEqual(ParkingStatus.PartiallyInside, Validator.Status);
            Assert.AreEqual(2, Validator.CornersInside);
        }

        [UnityTest]
        public IEnumerator Over_The_Side_Line_Does_Not_Complete()
        {
            yield return Load(null);
            PlaceInBay(new Vector3(0.6f, 0f, 0f), 0f);   // right side 0.1 m over the line
            yield return Seconds(3f);
            Assert.AreEqual(LevelState.Playing, Manager.State);
            Assert.AreEqual(ParkingStatus.PartiallyInside, Validator.Status);
        }

        [UnityTest]
        public IEnumerator Misaligned_Does_Not_Complete()
        {
            yield return Load(null);
            PlaceInBay(Vector3.zero, 11f);   // fits inside the lines, but 11° > 10° tolerance
            yield return Seconds(3f);
            Debug.Log($"[Test] Angled 11°: {Validator.Status}, heading error {Validator.HeadingErrorDegrees:F1}°, corners {Validator.CornersInside}/4");
            Assert.AreEqual(LevelState.Playing, Manager.State);
            Assert.AreEqual(ParkingStatus.Misaligned, Validator.Status);
            Assert.AreEqual(4, Validator.CornersInside);
        }

        [UnityTest]
        public IEnumerator Within_Angle_Tolerance_Completes()
        {
            yield return Load(null);
            PlaceInBay(Vector3.zero, 7f);
            yield return WaitUntil(() => Manager.State != LevelState.Playing, 5f, "level ends");
            Assert.AreEqual(LevelState.Completed, Manager.State);
        }

        [UnityTest]
        public IEnumerator Moving_Too_Fast_Is_Not_Parked()
        {
            yield return Load(null);
            PlaceInBay(new Vector3(0f, 0f, -0.5f), 0f);
            yield return new WaitForFixedUpdate();
            Car.Body.linearVelocity = Car.transform.forward * 1.5f;   // 5.4 km/h, threshold 2 km/h
            yield return new WaitForFixedUpdate();
            Debug.Log($"[Test] Rolling in the bay: {Validator.Status} at {Validator.SpeedKmh:F1} km/h");
            Assert.AreEqual(ParkingStatus.TooFast, Validator.Status);
            Assert.AreEqual(0f, Validator.HoldProgress);
        }

        [UnityTest]
        public IEnumerator Hold_Timer_Restarts_When_The_Car_Leaves()
        {
            yield return Load(null);
            float hold = Validator.ActiveRules.holdDuration;
            PlaceInBay(Vector3.zero, 0f);
            yield return Seconds(hold * 0.6f);
            float progress = Validator.HoldProgress;
            Car.ResetVehicle(Manager.Level.SpawnPosition + Vector3.up * 0.1f, Manager.Level.SpawnRotation);
            yield return Seconds(0.3f);
            Assert.AreEqual(0f, Validator.HoldProgress, "leaving resets the hold");
            PlaceInBay(Vector3.zero, 0f);
            yield return Seconds(hold * 0.6f);
            Assert.AreEqual(LevelState.Playing, Manager.State, "needs a full hold again");
            yield return WaitUntil(() => Manager.State != LevelState.Playing, 5f, "level ends");
            Debug.Log($"[Test] Hold: {progress * 100f:F0}% before leaving → reset → completed after a full second hold");
            Assert.Greater(progress, 0.3f);
            Assert.AreEqual(LevelState.Completed, Manager.State);
        }

        // ---------------------------------------------------------------- failure

        [UnityTest]
        public IEnumerator Timer_Expiry_Fails_The_Level()
        {
            yield return Load(Variant(("timeLimitSeconds", 1.5f)));
            yield return WaitUntil(() => Manager.State != LevelState.Playing, 5f, "level ends");
            Debug.Log($"[Test] Time limit 1.5s: {Manager.LastResult}");
            Assert.AreEqual(LevelState.Failed, Manager.State);
            Assert.AreEqual(FailReason.TimeUp, Manager.LastResult.Value.FailReason);
            Assert.AreEqual(0, Manager.LastResult.Value.Stars);
            Assert.AreEqual(0f, Manager.RemainingSeconds);
            Assert.IsFalse(Car.ControlsEnabled);
        }

        [UnityTest]
        public IEnumerator Exceeding_The_Collision_Limit_Fails_The_Level()
        {
            // Facing the north wall with no collisions allowed: the first hit exceeds the limit.
            yield return Load(Variant(("collisionLimit", 0), ("spawnPosition", new Vector3(0f, 0f, 22f)), ("spawnYaw", 0f)));
            Drive(1f);
            yield return WaitUntil(() => Manager.State != LevelState.Playing, 8f, "level ends");
            Debug.Log($"[Test] Collision limit 0: {Manager.LastResult}");
            Assert.AreEqual(LevelState.Failed, Manager.State);
            Assert.AreEqual(FailReason.TooManyCollisions, Manager.LastResult.Value.FailReason);
            Assert.AreEqual(1, Manager.Collisions);
        }

        [UnityTest]
        public IEnumerator Collisions_Under_The_Limit_Count_Once_And_Cost_Stars()
        {
            yield return Load(Variant(("spawnPosition", new Vector3(0f, 0f, 22f)), ("spawnYaw", 0f)));
            Drive(1f);
            yield return WaitUntil(() => Manager.Collisions > 0, 8f, "hits the wall");
            yield return Seconds(1.5f);                                     // keep pushing into the wall
            Drive(0f);
            int count = Manager.Collisions;
            Assert.AreEqual(LevelState.Playing, Manager.State, "1 collision is within the limit of 3");
            PlaceInBay(Vector3.zero, 0f);
            yield return WaitUntil(() => Manager.State != LevelState.Playing, 5f, "level ends");
            Debug.Log($"[Test] Wall hit then parked: collisions {count}, {Manager.LastResult}");
            Assert.AreEqual(1, count, "pushing against the same wall counts once");
            Assert.AreEqual(LevelState.Completed, Manager.State);
            Assert.AreEqual(2, Manager.LastResult.Value.Stars, "one collision drops to two stars");
        }

        [UnityTest]
        public IEnumerator Driving_Over_The_Kerb_Strip_Is_Not_A_Collision()
        {
            yield return Load(null);                    // spawn x = -20 facing +X; kerb strip at x = -28
            Drive(-1f);
            yield return WaitUntil(() => Car.transform.position.x < -30f, 8f, "reverses over the kerb");
            Drive(0f);
            Car.Input.SetBrake(1f);
            yield return Seconds(1f);
            Debug.Log($"[Test] Reversed over the kerb strip to x={Car.transform.position.x:F1}: {Manager.Collisions} collisions");
            Assert.AreEqual(0, Manager.Collisions);
        }

        // ---------------------------------------------------------------- flow

        [UnityTest]
        public IEnumerator Next_Level_Then_Restart_Then_Last_Level_Has_No_Next()
        {
            yield return Load(null);
            Assert.IsTrue(Manager.HasNextLevel);
            LevelData second = Catalog().Get(1);
            Assert.IsTrue(Manager.NextLevel());
            yield return WaitUntil(() => Manager != null && Manager.Level == second && Manager.State == LevelState.Playing, 5f, "level 2 starts");
            Assert.Less(Vector3.Distance(Loader.Vehicle.transform.position, second.SpawnPosition), 1f);
            Assert.AreEqual(7f, Validator.ActiveRules.angleTolerance, "level 2 uses its own rules");

            Loader.Vehicle.ResetVehicle(new Vector3(0f, 0.1f, 0f), Quaternion.identity);
            yield return Seconds(0.5f);
            Manager.RestartLevel();
            yield return WaitUntil(() => Manager != null && Manager.Level == second && Manager.State == LevelState.Playing, 5f, "level 2 restarts");
            Assert.Less(Manager.ElapsedSeconds, 1f, "timer restarted");
            Assert.Less(Vector3.Distance(Loader.Vehicle.transform.position, second.SpawnPosition), 1f, "car back at the spawn");

            Assert.IsFalse(Manager.HasNextLevel);
            LogAssert.Expect(LogType.Warning, new Regex("no next level"));
            Assert.IsFalse(Manager.NextLevel());
        }

        [UnityTest]
        public IEnumerator Duplicate_Managers_Remove_Themselves()
        {
            yield return Load(null);
            LevelManager original = Manager;
            LogAssert.Expect(LogType.Warning, new Regex("A LevelManager already exists"));
            LogAssert.Expect(LogType.Warning, new Regex("A LevelLoader already exists"));
            var extra = new GameObject("ExtraSystems");
            extra.AddComponent<ParkingValidator>();
            extra.AddComponent<LevelManager>();
            extra.AddComponent<LevelLoader>();
            yield return null;
            Assert.AreSame(original, LevelManager.Instance);
            Assert.IsNull(extra.GetComponent<LevelManager>());
            Assert.IsNull(extra.GetComponent<LevelLoader>());
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<LevelManager>().Length);
            UnityEngine.Object.Destroy(extra);
        }

        [UnityTest]
        public IEnumerator Missing_References_Log_Instead_Of_Throwing()
        {
            yield return Load(null);
            LogAssert.Expect(LogType.Error, new Regex("Load: level is null"));
            Assert.IsFalse(Loader.Load(null));
            LogAssert.Expect(LogType.Error, new Regex("BeginLevel needs a level and a vehicle"));
            Manager.BeginLevel(null, null, null, null);

            var go = new GameObject("LooseValidator");
            var v = go.AddComponent<ParkingValidator>();
            LogAssert.Expect(LogType.Error, new Regex("Bind: no ParkingZone"));
            LogAssert.Expect(LogType.Error, new Regex("Bind: no VehicleController"));
            v.Bind(null, null);
            v.SetArmed(true);
            Assert.IsFalse(v.IsArmed, "cannot arm without a zone and a car");
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(ParkingStatus.Inactive, v.Status);
            UnityEngine.Object.Destroy(go);
            Assert.AreEqual(LevelState.Playing, Manager.State, "the running level is unaffected");
        }
    }

    /// <summary>Leaves the test runner in a fresh empty scene so later tests don't run inside Gameplay.</summary>
    internal static class TestScenes
    {
        private static int counter;

        /// <summary>Points SaveManager at a fresh temp file so tests never touch (or depend on) real progress.</summary>
        public static string UseTemporarySave()
        {
            string path = System.IO.Path.Combine(Application.temporaryCachePath, $"ucp_test_{System.Guid.NewGuid():N}.json");
            UltimateCarParking.Save.SaveManager.Initialize(path);
            return path;
        }

        public static IEnumerator WaitForTransition()
        {
            float t = 0f;
            while (UltimateCarParking.UI.ScreenTransition.IsTransitioning && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
        }

        public static IEnumerator ResetToEmpty()
        {
            yield return WaitForTransition();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            // The persistent AudioManager would keep playing into the next test's (listener-less) scene.
            foreach (var am in UnityEngine.Object.FindObjectsByType<UltimateCarParking.Audio.AudioManager>()) UnityEngine.Object.Destroy(am.gameObject);
            Scene empty = SceneManager.CreateScene($"TestEmpty{++counter}");
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                // Never unload the test runner's own scene: it hosts the coroutine running this teardown.
                if (s != empty && s.isLoaded && !s.name.StartsWith("InitTestScene")) yield return SceneManager.UnloadSceneAsync(s);
            }
        }
    }
}
