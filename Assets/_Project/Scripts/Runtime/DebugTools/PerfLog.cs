using System.Diagnostics;
using UnityEngine.Profiling;

namespace UltimateCarParking.DebugTools
{
    /// <summary>
    /// Load-phase timing for the performance work. Does nothing (one bool check) unless <see cref="Enabled"/>, which PerfMonitor
    /// sets when the perfmon flag file exists. Each <see cref="Mark"/> logs the time since the previous mark and the Unity heap.
    /// </summary>
    public static class PerfLog
    {
        public static bool Enabled;
        private static readonly Stopwatch Clock = new Stopwatch();
        private static string scope = "";
        private static long last;

        public static void Begin(string name)
        {
            if (!Enabled) return;
            scope = name; Clock.Restart(); last = 0;
            UnityEngine.Debug.Log($"[PerfMon] phase '{name}' begin");
        }

        public static void Mark(string phase)
        {
            if (!Enabled || !Clock.IsRunning) return;
            long now = Clock.ElapsedMilliseconds;
            UnityEngine.Debug.Log($"[PerfMon] phase '{scope}' {phase}: +{now - last} ms (t={now} ms), heap {Profiler.GetTotalAllocatedMemoryLong() / 1048576} MB");
            last = now;
        }

        public static void End(string phase = "done")
        {
            if (!Enabled || !Clock.IsRunning) return;
            Mark(phase);
            Clock.Stop();
        }
    }
}
