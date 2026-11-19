using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UltimateCarParking.Core;

namespace UltimateCarParking.Quality
{
    /// <summary>
    /// Reacts to Android's low-memory warning using <see cref="MemoryPolicy"/>: frees unused assets (never mid-drive: it waits for the next
    /// scene change), and after repeated warnings drops to half-size textures. Created on device at startup; does nothing in the editor.
    /// </summary>
    public sealed class MemoryGuard : MonoBehaviour
    {
        /// <summary>Extra texture mip limit forced by memory pressure (0 normally).</summary>
        public static int ExtraMipLimit { get; private set; }
        public static int Warnings { get; private set; }

        private readonly MemoryPolicy policy = new MemoryPolicy();
        private bool cleanupPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { ExtraMipLimit = 0; Warnings = 0; }

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("MemoryGuard");
            DontDestroyOnLoad(go);
            go.AddComponent<MemoryGuard>();
        }
#endif

        private void OnEnable()
        {
            Application.lowMemory += OnLowMemory;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            Application.lowMemory -= OnLowMemory;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private static bool Driving() => Levels.LevelManager.Instance != null && Levels.LevelManager.Instance.State == Levels.LevelState.Playing;

        private void OnLowMemory()
        {
            Warnings++;
            MemoryAction a = policy.OnLowMemory(Time.realtimeSinceStartupAsDouble, Driving());
            Debug.LogWarning($"[Memory] low-memory warning #{Warnings}: {a}");
            Apply(a);
        }

        private void Apply(MemoryAction a)
        {
            switch (a)
            {
                case MemoryAction.CleanUpNow:
                    Resources.UnloadUnusedAssets();
                    GC.Collect();
                    break;
                case MemoryAction.CleanUpWhenSafe:
                    cleanupPending = true;
                    break;
                case MemoryAction.HalveTextures:
                    ExtraMipLimit = 1;
                    QualitySettings.globalTextureMipmapLimit = Mathf.Max(QualitySettings.globalTextureMipmapLimit, 1);
                    Resources.UnloadUnusedAssets();
                    GC.Collect();
                    break;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (policy.TakeDeferredCleanup(ref cleanupPending)) Apply(MemoryAction.CleanUpNow);
        }
    }
}
