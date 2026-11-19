using System;

namespace UltimateCarParking.DebugTools
{
    /// <summary>Frame-time statistics of one sampling window.</summary>
    public readonly struct PerfStats
    {
        public readonly int Frames;
        public readonly float AvgMs, P95Ms, WorstMs;
        public readonly float AvgFps, OnePercentLowFps;
        /// <summary>Frames slower than 33.3 ms (below 30 FPS) and slower than 50 ms (clearly visible hitches).</summary>
        public readonly int Over33Ms, Over50Ms;

        public PerfStats(int frames, float avgMs, float p95Ms, float worstMs, float avgFps, float lowFps, int over33, int over50)
        {
            Frames = frames; AvgMs = avgMs; P95Ms = p95Ms; WorstMs = worstMs;
            AvgFps = avgFps; OnePercentLowFps = lowFps; Over33Ms = over33; Over50Ms = over50;
        }
    }

    /// <summary>
    /// Collects frame times (seconds) and summarises them. Pure C# with a fixed buffer (no allocation after
    /// construction), so it is unit-tested and cheap enough to run in a release build.
    /// </summary>
    public sealed class PerfWindow
    {
        private readonly float[] frames;
        private readonly float[] scratch;
        private int count;

        public PerfWindow(int capacity = 4096)
        {
            frames = new float[capacity];
            scratch = new float[capacity];
        }

        public int Count => count;

        public void Add(float seconds)
        {
            if (seconds <= 0f || count >= frames.Length) return;
            frames[count++] = seconds;
        }

        public void Reset() => count = 0;

        public PerfStats Compute()
        {
            if (count == 0) return default;
            float sum = 0f;
            int over33 = 0, over50 = 0;
            for (int i = 0; i < count; i++)
            {
                float f = frames[i];
                sum += f;
                if (f > 1f / 30f + 0.0001f) over33++;
                if (f > 0.05f) over50++;
                scratch[i] = f;
            }
            Array.Sort(scratch, 0, count);
            float avg = sum / count;
            float p95 = scratch[Math.Min(count - 1, (int)(count * 0.95f))];
            float p99 = scratch[Math.Min(count - 1, (int)(count * 0.99f))];
            float worst = scratch[count - 1];
            return new PerfStats(count, avg * 1000f, p95 * 1000f, worst * 1000f, 1f / avg, 1f / p99, over33, over50);
        }
    }
}
