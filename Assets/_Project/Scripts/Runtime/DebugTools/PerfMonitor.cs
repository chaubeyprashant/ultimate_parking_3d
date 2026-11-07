using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace UltimateCarParking.DebugTools
{
    /// <summary>
    /// On-device performance sampling for the Android performance report. Off unless the file
    /// <c>Application.persistentDataPath/perfmon</c> exists (create it with
    /// <c>adb shell touch /sdcard/Android/data/om.ultimateparking.carparking3d/files/perfmon</c>). Then every
    /// <see cref="WindowSeconds"/> it logs to logcat: scene, average FPS, 1% low FPS, worst frame, Unity heap and
    /// graphics memory. Read with <c>adb logcat -s Unity | grep PerfMon</c>.
    /// </summary>
    public sealed class PerfMonitor : MonoBehaviour
    {
        public const float WindowSeconds = 10f;
        private readonly float[] frames = new float[2048];
        private int count;
        private float elapsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!File.Exists(Path.Combine(Application.persistentDataPath, "perfmon"))) return;
            var go = new GameObject("PerfMonitor");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfMonitor>();
            Debug.Log($"[PerfMon] enabled: {SystemInfo.deviceModel}, {SystemInfo.graphicsDeviceType}, {SystemInfo.processorCount} cores, " +
                      $"{SystemInfo.systemMemorySize} MB RAM, {Screen.width}x{Screen.height}, target {Application.targetFrameRate} fps");
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (count < frames.Length) frames[count++] = dt;
            elapsed += dt;
            if (elapsed < WindowSeconds) return;
            Report();
            elapsed = 0f;
            count = 0;
        }

        private void Report()
        {
            if (count == 0) return;
            float sum = 0f;
            for (int i = 0; i < count; i++) sum += frames[i];
            System.Array.Sort(frames, 0, count);
            int p99 = Mathf.Clamp(Mathf.FloorToInt(count * 0.99f), 0, count - 1);
            Debug.Log($"[PerfMon] {SceneManager.GetActiveScene().name}: avg {count / sum:F1} fps, 1% low {1f / frames[p99]:F1} fps, " +
                      $"worst {frames[count - 1] * 1000f:F0} ms, heap {Profiler.GetTotalAllocatedMemoryLong() / 1048576} MB, " +
                      $"reserved {Profiler.GetTotalReservedMemoryLong() / 1048576} MB, gfx {Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576} MB");
        }
    }
}
