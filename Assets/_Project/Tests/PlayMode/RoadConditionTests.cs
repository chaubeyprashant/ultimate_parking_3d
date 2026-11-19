using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Conditions;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.UI;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>Night, rain, snow and fog in the real Gameplay scene: look, grip, effects, and a clean restore.</summary>
    public class RoadConditionTests
    {
        private static LevelCatalog TestCatalog()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/Test/TestCatalog.asset");
#else
            return null;
#endif
        }

        private LevelManager Manager => LevelManager.Instance;
        private VehicleController Car => LevelLoader.Instance.Vehicle;

        [SetUp]
        public void SetUp() { TestScenes.UseTemporarySave(); TestingUnlocks.UnlockAll = true; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return TestScenes.ResetToEmpty();
        }

        private static IEnumerator WaitFor(System.Func<bool> c, float timeout, string what)
        {
            float t = 0f;
            while (!c()) { t += Time.unscaledDeltaTime; if (t > timeout) Assert.Fail("timed out: " + what); yield return null; }
        }

        private IEnumerator Load()
        {
            LevelSelection.Request(TestCatalog().Get(0), TestCatalog());
            SceneManager.LoadScene(SceneNames.Gameplay);
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 8f, "level playing");
            Car.Input.KeyboardEnabled = false;
            yield return null;
        }

        private static ConditionsApplier Applier => LevelLoader.Instance.Conditions != null ? LevelLoader.Instance.Conditions : LevelLoader.Instance.gameObject.AddComponent<ConditionsApplier>();

        [UnityTest]
        public IEnumerator Night_Darkens_The_Scene_Adds_A_Beam_And_Restores_Exactly()
        {
            yield return Load();
            Light sun = RenderSettings.sun;
            float sunBefore = sun.intensity; Color ambBefore = RenderSettings.ambientSkyColor; bool fogBefore = RenderSettings.fog;
            float fogDensityBefore = RenderSettings.fogDensity; Material skyBefore = RenderSettings.skybox;

            Applier.Apply(RoadCondition.Night, Car, null);
            Assert.Less(sun.intensity, sunBefore * 0.2f, "sun is dim");
            Assert.Less(RenderSettings.ambientSkyColor.grayscale, ambBefore.grayscale, "ambient is darker");
            Assert.IsTrue(RenderSettings.fog);
            Assert.IsTrue(Applier.HasHeadlightBeam, "the car has a headlight beam");
            Assert.IsNotNull(Car.transform.Find("HeadlightBeam"));
            Assert.AreEqual(1f, Car.SurfaceGrip, "night does not change grip");

            Applier.Clear();
            yield return null;   // Destroy() takes effect at the end of the frame
            Assert.AreEqual(sunBefore, sun.intensity, 1e-5f);
            Assert.AreEqual(ambBefore, RenderSettings.ambientSkyColor);
            Assert.AreEqual(fogBefore, RenderSettings.fog);
            Assert.AreEqual(fogDensityBefore, RenderSettings.fogDensity, 1e-6f);
            Assert.AreSame(skyBefore, RenderSettings.skybox, "original sky restored");
            Assert.IsFalse(Applier.HasHeadlightBeam);
            Assert.IsNull(Car.transform.Find("HeadlightBeam"));
        }

        [UnityTest]
        public IEnumerator Rain_Adds_Falling_Particles_Lowers_Grip_And_Cleans_Up()
        {
            yield return Load();
            Applier.Apply(RoadCondition.Rain, Car, Camera.main != null ? Camera.main.transform : null);
            Assert.IsNotNull(Applier.Precipitation, "rain particle system exists");
            Assert.IsTrue(Applier.Precipitation.isPlaying);
            Assert.Greater(Applier.Precipitation.emission.rateOverTime.constant, 100f);
            Assert.Less(Applier.Precipitation.main.maxParticles, 2000, "mobile-sized");
            Assert.IsNotNull(Applier.Precipitation.GetComponent<ParticleSystemRenderer>().sharedMaterial, "rain material found in Resources");
            Assert.AreEqual(ConditionProfiles.For(RoadCondition.Rain).Grip, Car.SurfaceGrip, 1e-4f);
            Assert.IsFalse(Applier.HasHeadlightBeam);

            Applier.Clear();
            yield return null;
            Assert.IsNull(Applier.Precipitation);
            Assert.AreEqual(1f, Car.SurfaceGrip, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Snow_Uses_A_Different_Effect_And_Much_Less_Grip()
        {
            yield return Load();
            Applier.Apply(RoadCondition.Snow, Car, null);
            Assert.IsNotNull(Applier.Precipitation);
            Assert.AreEqual(ParticleSystemRenderMode.Billboard, Applier.Precipitation.GetComponent<ParticleSystemRenderer>().renderMode, "flakes, not streaks");
            Assert.AreEqual(ConditionProfiles.For(RoadCondition.Snow).Grip, Car.SurfaceGrip, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Fog_Thickens_The_Fog_Without_Particles_Or_Grip_Change()
        {
            yield return Load();
            float before = RenderSettings.fogDensity;
            Applier.Apply(RoadCondition.Fog, Car, null);
            Assert.Greater(RenderSettings.fogDensity, Mathf.Max(before, 0.04f));
            Assert.IsNull(Applier.Precipitation);
            Assert.IsFalse(Applier.HasHeadlightBeam);
            Assert.AreEqual(1f, Car.SurfaceGrip);
            Applier.Clear();
            Assert.AreEqual(before, RenderSettings.fogDensity, 1e-6f);
        }

        [UnityTest]
        public IEnumerator Applying_Twice_Does_Not_Stack_Or_Leak_Effects()
        {
            yield return Load();
            Applier.Apply(RoadCondition.Rain, Car, null);
            Applier.Apply(RoadCondition.Rain, Car, null);
            Applier.Apply(RoadCondition.Night, Car, null);
            Assert.IsNull(Applier.Precipitation, "rain was removed when night replaced it");
            Assert.AreEqual(1f, Car.SurfaceGrip);
            Assert.AreEqual(1, CountBeams(), "exactly one beam");
            Applier.Clear();
            yield return null;
            int weather = 0;
            foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include)) if (ps.name == "Rain" || ps.name == "Snow") weather++;
            Assert.AreEqual(0, weather, "no rain or snow system left behind");
        }

        private int CountBeams() { int n = 0; foreach (Transform t in Car.GetComponentsInChildren<Transform>(true)) if (t.name == "HeadlightBeam") n++; return n; }

        // ---------------------------------------------------------------- the road itself

        private IEnumerator BrakeDistanceFrom30(float grip, System.Action<float> result)
        {
            yield return Load();
            Car.SetSurfaceGrip(grip);
            Rigidbody rb = Car.GetComponent<Rigidbody>();
            Car.Input.SetThrottle(1f);
            yield return WaitFor(() => Car.SpeedKmh >= 30f, 12f, "reach 30 km/h");
            Car.Input.SetThrottle(0f);
            Car.Input.SetBrake(1f);
            Vector3 start = rb.position;
            yield return WaitFor(() => Car.SpeedKmh < 1f, 12f, "stop");
            result(Vector3.Distance(start, rb.position));
        }

        [UnityTest]
        public IEnumerator Wet_And_Icy_Roads_Need_Longer_To_Stop_Than_Dry()
        {
            float dry = 0f, rain = 0f, snow = 0f;
            yield return BrakeDistanceFrom30(1f, d => dry = d);
            yield return TestScenes.ResetToEmpty();
            yield return BrakeDistanceFrom30(ConditionProfiles.For(RoadCondition.Rain).Grip, d => rain = d);
            yield return TestScenes.ResetToEmpty();
            yield return BrakeDistanceFrom30(ConditionProfiles.For(RoadCondition.Snow).Grip, d => snow = d);
            Debug.Log($"[Test] Braking from 30 km/h: dry {dry:F1} m, rain {rain:F1} m, snow {snow:F1} m");
            Assert.Greater(dry, 1f);
            Assert.Greater(rain, dry * 1.15f, "rain: clearly longer than dry");
            Assert.Greater(snow, rain * 1.2f, "snow: longer again");
        }
    }
}
