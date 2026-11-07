using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UltimateCarParking.WorldTour
{
    /// <summary>
    /// Loads the selected country's arena — and only that one. Loading a country first unloads the previous arena
    /// (destroys a prefab instance or unloads an additive scene, then releases unused assets), then resolves the
    /// country's <see cref="CountryArenaReference"/> asynchronously. A newer request supersedes an older one still in
    /// flight (the older result is discarded). Missing prefabs, scenes absent from Build Settings and instantiation
    /// errors end in <see cref="LoadState.Failed"/> with <see cref="LastError"/> set and <see cref="Failed"/> raised —
    /// never an exception. Uses only Resources and SceneManager, both available on Android.
    /// </summary>
    public sealed class CountryArenaLoader : MonoBehaviour
    {
        public enum LoadState { Idle, Loading, Loaded, Failed }

        [Tooltip("Parent for prefab arenas (defaults to this object).")]
        [SerializeField] private Transform arenaParent;
        [Tooltip("Sun to tint with the country's weather (defaults to RenderSettings.sun).")]
        [SerializeField] private Light sun;
        [SerializeField] private bool applyWeather = true;
        [Tooltip("Call Resources.UnloadUnusedAssets after unloading an arena (frees its textures and meshes).")]
        [SerializeField] private bool releaseUnusedAssets = true;

        private int request;

        public LoadState State { get; private set; } = LoadState.Idle;
        public CountryData Current { get; private set; }
        public GameObject ArenaInstance { get; private set; }
        public Scene ArenaScene { get; private set; }
        public CountryArenaRoot ArenaRoot { get; private set; }
        public string LastError { get; private set; } = "";
        public bool IsLoading => State == LoadState.Loading;

        public event Action<CountryData> Loaded;
        public event Action<CountryData, string> Failed;
        public event Action<CountryData> Unloaded;

        /// <summary>Starts loading a country's arena (replacing the current one).</summary>
        public Coroutine Load(CountryData country) => StartCoroutine(LoadRoutine(country));

        /// <summary>Starts unloading the current arena.</summary>
        public Coroutine Unload()
        {
            request++;   // cancels any load in flight
            State = LoadState.Idle;
            return StartCoroutine(UnloadRoutine());
        }

        public IEnumerator LoadRoutine(CountryData country)
        {
            int id = ++request;
            if (country != null && country == Current && State == LoadState.Loaded) { Loaded?.Invoke(country); yield break; }
            State = LoadState.Loading;
            LastError = "";

            // Validate cheaply before touching the current arena's memory.
            string problem = Validate(country);
            yield return UnloadRoutine();
            if (id != request) yield break;                       // superseded while unloading
            if (problem != null) { Fail(country, problem); yield break; }

            State = LoadState.Loading;
            CountryArenaReference arena = country.Arena;
            if (arena.source == ArenaSource.ResourcesPrefab)
            {
                ResourceRequest req = Resources.LoadAsync<GameObject>(arena.key);
                yield return req;
                if (id != request) yield break;                   // superseded: the asset is never instantiated
                var prefab = req.asset as GameObject;
                if (prefab == null) { Fail(country, $"Arena prefab not found at Resources/{arena.key} ({country.CountryId})."); yield break; }
                try
                {
                    ArenaInstance = Instantiate(prefab, arenaParent != null ? arenaParent : transform);
                    ArenaInstance.name = prefab.name;
                }
                catch (Exception e)
                {
                    Fail(country, $"Arena prefab Resources/{arena.key} failed to instantiate: {e.Message}");
                    yield break;
                }
                ArenaRoot = ArenaInstance.GetComponentInChildren<CountryArenaRoot>(true);
            }
            else
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(arena.key, LoadSceneMode.Additive);
                if (op == null) { Fail(country, $"Arena scene '{arena.key}' could not be loaded ({country.CountryId})."); yield break; }
                yield return op;
                Scene scene = SceneManager.GetSceneByName(arena.key);
                if (id != request)                                // superseded: drop the scene we just loaded
                {
                    if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
                    yield break;
                }
                if (!scene.IsValid() || !scene.isLoaded) { Fail(country, $"Arena scene '{arena.key}' did not finish loading."); yield break; }
                ArenaScene = scene;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    ArenaRoot = root.GetComponentInChildren<CountryArenaRoot>(true);
                    if (ArenaRoot != null) break;
                }
            }

            if (ArenaRoot == null) Debug.LogWarning($"[Arena] {arena} has no CountryArenaRoot; loaded without named anchors.");
            Current = country;
            State = LoadState.Loaded;
            if (applyWeather && country.Weather != null) country.Weather.Apply(sun != null ? sun : RenderSettings.sun);
            Loaded?.Invoke(country);
        }

        public IEnumerator UnloadRoutine()
        {
            CountryData previous = Current;
            bool hadSomething = ArenaInstance != null || (ArenaScene.IsValid() && ArenaScene.isLoaded);
            if (ArenaInstance != null)
            {
                Destroy(ArenaInstance);
                ArenaInstance = null;
            }
            if (ArenaScene.IsValid() && ArenaScene.isLoaded)
            {
                AsyncOperation op = SceneManager.UnloadSceneAsync(ArenaScene);
                ArenaScene = default;
                if (op != null) yield return op;
            }
            ArenaScene = default;
            ArenaRoot = null;
            Current = null;
            if (State != LoadState.Loading) State = LoadState.Idle;
            if (hadSomething)
            {
                yield return null;                                // let Destroy complete before releasing assets
                if (releaseUnusedAssets) yield return Resources.UnloadUnusedAssets();
                if (previous != null) Unloaded?.Invoke(previous);
            }
        }

        /// <summary>Why a country's arena cannot be loaded, or null when it looks loadable.</summary>
        public static string Validate(CountryData country)
        {
            if (country == null) return "No country selected.";
            CountryArenaReference a = country.Arena;
            if (a == null || !a.IsSet) return $"{country.DisplayName} has no arena configured.";
            if (a.source == ArenaSource.Scene && !SceneInBuild(a.key))
                return $"Arena scene '{a.key}' is not in Build Settings ({country.CountryId}).";
            return null;
        }

        private static bool SceneInBuild(string sceneName)
        {
#if UNITY_EDITOR
            // Outside Play mode the runtime scene list is not refreshed; read Build Settings directly.
            if (!Application.isPlaying)
            {
                foreach (var s in UnityEditor.EditorBuildSettings.scenes)
                    if (s.enabled && System.IO.Path.GetFileNameWithoutExtension(s.path) == sceneName) return true;
                return false;
            }
#endif
            return Application.CanStreamedLevelBeLoaded(sceneName);
        }

        private void Fail(CountryData country, string message)
        {
            State = LoadState.Failed;
            LastError = message;
            Debug.LogError("[Arena] " + message);
            Failed?.Invoke(country, message);
        }

        private void OnDestroy()
        {
            request++;
            if (ArenaScene.IsValid() && ArenaScene.isLoaded) SceneManager.UnloadSceneAsync(ArenaScene);
        }
    }
}
