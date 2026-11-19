using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Settings;
using SettingsService = UltimateCarParking.Settings.SettingsService;
using UltimateCarParking.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Phase 4 optimisation check: managed (GC) allocation per frame while driving in a real level with the touch
    /// controls, HUD, speedometer, parking sensors, rear camera and chase camera all active, compared with the same
    /// test harness idling in an empty scene. Steady per-frame garbage causes GC hitches on phones.
    /// Editor measurement (not a device measurement).
    /// </summary>
    public class FrameAllocationTests
    {
        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() { Time.captureFramerate = 0; yield return TestScenes.ResetToEmpty(); }

        private static IEnumerator Measure(int frames, Action<long> done)
        {
            for (int i = 0; i < 30; i++) yield return null;   // warm up
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < frames; i++) yield return null;
            done(GC.GetAllocatedBytesForCurrentThread() - start);
        }

        [UnityTest]
        public IEnumerator Driving_With_Every_Hud_System_Allocates_Almost_Nothing_Per_Frame()
        {
            const int frames = 300;
            Time.captureFramerate = 60;
            long idle = 0, driving = 0;
            yield return Measure(frames, b => idle = b);

#if UNITY_EDITOR
            var cat = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/Test/TestCatalog.asset");
            LevelSelection.Request(cat.Get(0), cat);
#endif
            SettingsService.SetAssistMode(GameSettings.AssistBeginner);
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while (LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) { t += Time.unscaledDeltaTime; if (t > 8f) Assert.Fail("level"); yield return null; }
            var car = LevelLoader.Instance.Vehicle;
            car.Input.KeyboardEnabled = false;
            var controls = UnityEngine.Object.FindAnyObjectByType<MobileControls>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);   // something near, so the sensors and beeps work
            wall.transform.SetPositionAndRotation(car.transform.TransformPoint(new Vector3(0f, 0.6f, -3.2f)), car.transform.rotation);
            wall.transform.localScale = new Vector3(4f, 1.2f, 0.2f);
            controls.Wheel.SetHeld(true);
            controls.Wheel.Turn(60f);
            car.Input.SetThrottle(-0.4f);    // reversing: rear camera and rear sensors active
            yield return Measure(frames, b => driving = b);
            car.Input.SetThrottle(0f);

            float idlePerFrame = idle / (float)frames, drivePerFrame = driving / (float)frames;
            float game = Mathf.Max(0f, drivePerFrame - idlePerFrame);
            Debug.Log($"[Perf] GC allocation per frame: empty-scene harness {idlePerFrame:F0} B, driving level {drivePerFrame:F0} B → game ≈ {game:F0} B/frame ({game * 60f / 1024f:F1} KB/s at 60 fps)");
            if (idle == 0 && driving == 0)
                Assert.Inconclusive("GC.GetAllocatedBytesForCurrentThread reports 0 in this Unity runtime, so per-frame garbage cannot be measured here (use the Profiler on a device build).");
            Assert.Less(game, 1024f, "under 1 KB of garbage per frame while driving");
        }
    }
}
