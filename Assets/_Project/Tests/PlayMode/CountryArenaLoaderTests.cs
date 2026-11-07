using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.WorldTour;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// CountryArenaLoader with the two temporary countries: A (Resources prefab) and B (additive scene).
    /// Only the selected arena is ever loaded; switching unloads the previous one; errors never throw.
    /// </summary>
    public class CountryArenaLoaderTests
    {
        private CountryArenaLoader loader;
        private Light sun;

        private static CountryCatalog TestTour()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<CountryCatalog>("Assets/_Project/WorldTour/Test/TestCountryCatalog.asset");
#else
            return null;
#endif
        }

        private static CountryData A => TestTour().FindById("ta");
        private static CountryData B => TestTour().FindById("tb");

        [SetUp]
        public void SetUp()
        {
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            loader = new GameObject("ArenaLoader").AddComponent<CountryArenaLoader>();
            var so = typeof(CountryArenaLoader).GetField("sun", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            so.SetValue(loader, sun);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (loader != null) yield return loader.Unload();
            if (loader != null) Object.Destroy(loader.gameObject);
            if (sun != null) Object.Destroy(sun.gameObject);
            yield return TestScenes.ResetToEmpty();
        }

        private static int ArenaRootsInScenes() => Object.FindObjectsByType<CountryArenaRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        private static bool SceneLoaded(string name) => SceneManager.GetSceneByName(name).isLoaded;

        [UnityTest]
        public IEnumerator Prefab_Arena_Loads_With_Its_Anchors_And_Weather()
        {
            CountryData loadedEvent = null;
            loader.Loaded += c => loadedEvent = c;
            yield return loader.Load(A);
            Assert.AreEqual(CountryArenaLoader.LoadState.Loaded, loader.State, loader.LastError);
            Assert.AreSame(A, loader.Current);
            Assert.AreSame(A, loadedEvent);
            Assert.IsNotNull(loader.ArenaInstance);
            Assert.IsNotNull(loader.ArenaRoot);
            Assert.AreEqual("ta", loader.ArenaRoot.ArenaId);
            Assert.IsNotNull(loader.ArenaRoot.SpawnPoint);
            Assert.AreEqual(2, loader.ArenaRoot.SlotCount);
            Assert.AreEqual(1, ArenaRootsInScenes());
            Assert.AreEqual(A.Weather.sunIntensity, sun.intensity, 1e-4f, "country weather applied");
            Assert.AreEqual(A.Weather.fogDensity, RenderSettings.fogDensity, 1e-5f);
        }

        [UnityTest]
        public IEnumerator Scene_Arena_Loads_Additively()
        {
            yield return loader.Load(B);
            Assert.AreEqual(CountryArenaLoader.LoadState.Loaded, loader.State, loader.LastError);
            Assert.IsTrue(SceneLoaded("Arena_TestB"));
            Assert.AreEqual("tb", loader.ArenaRoot.ArenaId);
            Assert.IsNull(loader.ArenaInstance, "scene arenas have no prefab instance");
            Assert.AreEqual(B.Weather.sunIntensity, sun.intensity, 1e-4f);
            Assert.IsTrue(RenderSettings.fog, "rain brings fog");
        }

        [UnityTest]
        public IEnumerator Switching_Countries_Unloads_The_Previous_Arena()
        {
            CountryData unloaded = null;
            loader.Unloaded += c => unloaded = c;

            yield return loader.Load(A);
            GameObject aInstance = loader.ArenaInstance;
            yield return loader.Load(B);
            Assert.IsTrue(aInstance == null, "A's instance destroyed");
            Assert.AreSame(A, unloaded);
            Assert.AreEqual(1, ArenaRootsInScenes(), "only B is in memory");
            Assert.AreEqual("tb", Object.FindAnyObjectByType<CountryArenaRoot>().ArenaId);

            yield return loader.Load(A);
            Assert.IsFalse(SceneLoaded("Arena_TestB"), "B's scene unloaded");
            Assert.AreSame(B, unloaded);
            Assert.AreEqual(1, ArenaRootsInScenes());
            Assert.AreEqual("ta", loader.ArenaRoot.ArenaId);

            yield return loader.Load(A);   // same country again: nothing reloaded
            Assert.AreEqual(1, ArenaRootsInScenes());
        }

        [UnityTest]
        public IEnumerator A_Newer_Request_Supersedes_One_In_Flight()
        {
            loader.Load(A);
            loader.Load(B);                 // before A finished
            float t = 0f;
            while ((loader.State != CountryArenaLoader.LoadState.Loaded || loader.Current != B) && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return null;
            Assert.AreSame(B, loader.Current);
            Assert.AreEqual(1, ArenaRootsInScenes(), "A was discarded, never instantiated alongside B");
            Assert.AreEqual("tb", Object.FindAnyObjectByType<CountryArenaRoot>().ArenaId);
        }

        [UnityTest]
        public IEnumerator Unload_Leaves_Nothing_Behind()
        {
            yield return loader.Load(B);
            yield return loader.Unload();
            Assert.AreEqual(CountryArenaLoader.LoadState.Idle, loader.State);
            Assert.IsNull(loader.Current);
            Assert.IsFalse(SceneLoaded("Arena_TestB"));
            Assert.AreEqual(0, ArenaRootsInScenes());
        }

        [UnityTest]
        public IEnumerator Missing_Prefab_Fails_Cleanly_And_Unloads_The_Previous_Arena()
        {
            yield return loader.Load(A);
            var ghost = ScriptableObject.CreateInstance<CountryData>();
            ghost.Arena.source = ArenaSource.ResourcesPrefab;
            ghost.Arena.key = "Arenas/DoesNotExist";
            string failure = null;
            loader.Failed += (c, msg) => failure = msg;
            LogAssert.Expect(LogType.Error, new Regex(@"\[Arena\] Arena prefab not found at Resources/Arenas/DoesNotExist"));
            yield return loader.Load(ghost);
            Assert.AreEqual(CountryArenaLoader.LoadState.Failed, loader.State);
            StringAssert.Contains("Resources/Arenas/DoesNotExist", loader.LastError);
            Assert.AreEqual(loader.LastError, failure);
            Assert.IsNull(loader.Current);
            Assert.AreEqual(0, ArenaRootsInScenes(), "the previous arena does not linger");
            Object.Destroy(ghost);

            yield return loader.Load(A);   // recovers
            Assert.AreEqual(CountryArenaLoader.LoadState.Loaded, loader.State);
        }

        [UnityTest]
        public IEnumerator Missing_Scene_And_Unconfigured_Arena_Fail_Cleanly()
        {
            var ghost = ScriptableObject.CreateInstance<CountryData>();
            ghost.Arena.source = ArenaSource.Scene;
            ghost.Arena.key = "Arena_DoesNotExist";
            LogAssert.Expect(LogType.Error, new Regex(@"not in Build Settings"));
            yield return loader.Load(ghost);
            Assert.AreEqual(CountryArenaLoader.LoadState.Failed, loader.State);
            StringAssert.Contains("Arena_DoesNotExist", loader.LastError);

            var empty = ScriptableObject.CreateInstance<CountryData>();
            LogAssert.Expect(LogType.Error, new Regex(@"no arena configured"));
            yield return loader.Load(empty);
            Assert.AreEqual(CountryArenaLoader.LoadState.Failed, loader.State);

            LogAssert.Expect(LogType.Error, new Regex(@"No country selected"));
            yield return loader.Load(null);
            Assert.AreEqual(CountryArenaLoader.LoadState.Failed, loader.State);
            Object.Destroy(ghost);
            Object.Destroy(empty);
        }
    }
}
