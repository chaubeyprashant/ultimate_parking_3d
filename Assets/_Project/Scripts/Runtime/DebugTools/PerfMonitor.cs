using System;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UltimateCarParking.Levels;

namespace UltimateCarParking.DebugTools
{
    /// <summary>
    /// On-device performance recorder (Phase 1 baseline). Off unless the file <c>perfmon</c> exists in
    /// <c>Application.persistentDataPath</c>:
    /// <c>adb shell touch /sdcard/Android/data/om.ultimateparking.carparking3d/files/perfmon</c>.
    /// Every <see cref="WindowSeconds"/> it logs one line to logcat (tag PerfMon) and appends a row to
    /// <c>perf_log.csv</c> next to that file: FPS (avg and 1% low), frame-time p95 and worst, hitch counts, CPU main/render
    /// thread and GPU frame time (when the device reports them), draw calls, SetPass calls, triangles, GC allocation per
    /// frame and collections, Unity and system memory, thermal status and battery. It also logs scene and level load times
    /// and low-memory warnings. Counters the device cannot provide are written as -1 (never guessed).
    /// </summary>
    public sealed class PerfMonitor : MonoBehaviour
    {
        public const float WindowSeconds = 10f;
        public const string CsvHeader =
            "time_s,scene,frames,avg_fps,low1pct_fps,avg_ms,p95_ms,worst_ms,over33ms,over50ms,cpu_main_ms,cpu_render_ms,gpu_ms," +
            "draw_calls,setpass_calls,triangles,gc_bytes_per_frame,gc_collections,unity_mem_mb,system_mem_mb,gfx_mem_mb,thermal,battery_pct";

        private readonly PerfWindow window = new PerfWindow();
        private float elapsed;
        private float sceneLoadedAt;
        private bool levelStartReported;
        private string csvPath;
        private int lastGcCount;
        private int lowMemoryEvents;
        private double gcBytesInWindow;

        private ProfilerRecorder drawCalls, setPassCalls, triangles, gcAllocFrame, systemMem, totalMem;
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private double cpuMainSum, cpuRenderSum, gpuSum;
        private int timingSamples;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!File.Exists(Path.Combine(Application.persistentDataPath, "perfmon"))) return;
            PerfLog.Enabled = true;
            var go = new GameObject("PerfMonitor");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfMonitor>();
            string flag = File.ReadAllText(Path.Combine(Application.persistentDataPath, "perfmon")).Trim();
            if (flag.StartsWith("bench:")) go.AddComponent<PerfBenchmark>().Configure(flag.Substring(6));
        }

        private void OnEnable()
        {
            csvPath = Path.Combine(Application.persistentDataPath, "perf_log.csv");
            if (!File.Exists(csvPath)) File.WriteAllText(csvPath, CsvHeader + "\n");
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            gcAllocFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            systemMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            totalMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            lastGcCount = GC.CollectionCount(0);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.lowMemory += OnLowMemory;
            sceneLoadedAt = Time.realtimeSinceStartup;
            Debug.Log($"[PerfMon] enabled: {SystemInfo.deviceModel}, {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}), " +
                      $"{SystemInfo.processorCount} cores, {SystemInfo.systemMemorySize} MB RAM, {Screen.width}x{Screen.height}, " +
                      $"{Screen.currentResolution.refreshRateRatio.value:F0} Hz, target {Application.targetFrameRate} fps, " +
                      $"quality '{QualitySettings.names[QualitySettings.GetQualityLevel()]}' [{(UltimateCarParking.Quality.QualityService.Initialized ? UltimateCarParking.Quality.QualityService.Describe() : "quality service off")}], frame timing {(FrameTimingManager.IsFeatureEnabled() ? "on" : "off")}");
        }

        private void OnDisable()
        {
            drawCalls.Dispose(); setPassCalls.Dispose(); triangles.Dispose(); gcAllocFrame.Dispose(); systemMem.Dispose(); totalMem.Dispose();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.lowMemory -= OnLowMemory;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            sceneLoadedAt = Time.realtimeSinceStartup;
            levelStartReported = false;
            window.Reset(); elapsed = 0f; ResetWindowSums();
            Debug.Log($"[PerfMon] scene '{scene.name}' loaded at t={sceneLoadedAt:F1}s");
        }

        private void OnLowMemory()
        {
            lowMemoryEvents++;
            Debug.LogWarning($"[PerfMon] LOW MEMORY warning #{lowMemoryEvents} in '{SceneManager.GetActiveScene().name}' (unity {Profiler.GetTotalAllocatedMemoryLong() / 1048576} MB)");
        }

        private void ResetWindowSums()
        {
            cpuMainSum = cpuRenderSum = gpuSum = 0; timingSamples = 0; gcBytesInWindow = 0;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            window.Add(dt);
            elapsed += dt;
            if (gcAllocFrame.Valid) gcBytesInWindow += gcAllocFrame.LastValue;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                cpuMainSum += timing[0].cpuMainThreadFrameTime;
                cpuRenderSum += timing[0].cpuRenderThreadFrameTime;
                gpuSum += timing[0].gpuFrameTime;
                timingSamples++;
            }

            if (!levelStartReported && LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing)
            {
                levelStartReported = true;
                Debug.Log($"[PerfMon] level '{LevelManager.Instance.Level.LevelId}' playable {(Time.realtimeSinceStartup - sceneLoadedAt) * 1000f:F0} ms after its scene loaded");
            }

            if (elapsed >= WindowSeconds) Report();
        }

        private static double Mb(long bytes) => bytes / 1048576.0;

        private void Report()
        {
            PerfStats s = window.Compute();
            if (s.Frames == 0) { elapsed = 0f; return; }
            int gc = GC.CollectionCount(0);
            int gcDelta = gc - lastGcCount;
            lastGcCount = gc;
            double gcPerFrame = gcAllocFrame.Valid ? gcBytesInWindow / s.Frames : -1;
            double cpuMain = timingSamples > 0 ? cpuMainSum / timingSamples : -1;
            double cpuRender = timingSamples > 0 ? cpuRenderSum / timingSamples : -1;
            double gpu = timingSamples > 0 && gpuSum > 0 ? gpuSum / timingSamples : -1;
            long dc = drawCalls.Valid ? drawCalls.LastValue : -1;
            long sp = setPassCalls.Valid ? setPassCalls.LastValue : -1;
            long tri = triangles.Valid ? triangles.LastValue : -1;
            double unityMb = Mb(Profiler.GetTotalAllocatedMemoryLong());
            double sysMb = systemMem.Valid ? Mb(systemMem.LastValue) : -1;
            double gfxMb = Mb(Profiler.GetAllocatedMemoryForGraphicsDriver());
            int thermal = ThermalStatus();
            int battery = SystemInfo.batteryLevel < 0 ? -1 : Mathf.RoundToInt(SystemInfo.batteryLevel * 100f);
            string scene = SceneManager.GetActiveScene().name;

            Debug.Log($"[PerfMon] {scene}: {s.AvgFps:F1} fps (1% low {s.OnePercentLowFps:F1}), frame avg {s.AvgMs:F1} / p95 {s.P95Ms:F1} / worst {s.WorstMs:F0} ms, " +
                      $">33ms {s.Over33Ms}, >50ms {s.Over50Ms}, cpu main {cpuMain:F1} render {cpuRender:F1} gpu {gpu:F1} ms, draws {dc}, setpass {sp}, tris {tri}, " +
                      $"gc {gcPerFrame:F0} B/frame ({gcDelta} collections), unity {unityMb:F0} MB, system {sysMb:F0} MB, gfx {gfxMb:F0} MB, thermal {thermal}, battery {battery}%, lowMem {lowMemoryEvents}, renderScale {UltimateCarParking.Quality.QualityService.RenderScale:F2}");

            var sb = new StringBuilder(256);
            sb.Append(Time.realtimeSinceStartup.ToString("F1")).Append(',').Append(scene).Append(',').Append(s.Frames).Append(',')
              .Append(s.AvgFps.ToString("F1")).Append(',').Append(s.OnePercentLowFps.ToString("F1")).Append(',').Append(s.AvgMs.ToString("F2")).Append(',')
              .Append(s.P95Ms.ToString("F2")).Append(',').Append(s.WorstMs.ToString("F1")).Append(',').Append(s.Over33Ms).Append(',').Append(s.Over50Ms).Append(',')
              .Append(cpuMain.ToString("F2")).Append(',').Append(cpuRender.ToString("F2")).Append(',').Append(gpu.ToString("F2")).Append(',')
              .Append(dc).Append(',').Append(sp).Append(',').Append(tri).Append(',').Append(gcPerFrame.ToString("F0")).Append(',').Append(gcDelta).Append(',')
              .Append(unityMb.ToString("F0")).Append(',').Append(sysMb.ToString("F0")).Append(',').Append(gfxMb.ToString("F0")).Append(',')
              .Append(thermal).Append(',').Append(battery).Append('\n');
            try { File.AppendAllText(csvPath, sb.ToString()); } catch (Exception e) { Debug.LogWarning("[PerfMon] csv write failed: " + e.Message); }

            window.Reset();
            elapsed = 0f;
            ResetWindowSums();
        }

        // ------------------------------------------------------------ thermal (Android 10+)

        private int ThermalStatus()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var power = activity.Call<AndroidJavaObject>("getSystemService", "power");
                return power.Call<int>("getCurrentThermalStatus");   // 0 none, 1 light, 2 moderate, 3 severe, 4 critical, 5 emergency, 6 shutdown
            }
            catch (Exception) { return -1; }
#else
            return -1;
#endif
        }
    }
}
