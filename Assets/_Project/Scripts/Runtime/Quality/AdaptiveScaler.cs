using System;

namespace UltimateCarParking.Quality
{
    /// <summary>
    /// Decides the render scale from measured frame times (pure logic, no Unity calls, injectable clock via the time passed in).
    /// Drops a 0.05 step when frames run slow, and only climbs back when it is safe: GPU headroom is reported, or (without GPU timing) a
    /// stable stretch passes. Every drop that follows a climb doubles the wait before the next climb, so it cannot flap.
    /// </summary>
    public sealed class AdaptiveScaler
    {
        public const float Step = 0.05f;
        public const float EvalSeconds = 2f;
        public const float SlowFactor = 1.12f;
        public const float HeadroomFactor = 0.70f;
        public const float BaseLockSeconds = 20f;
        public const float MaxLockSeconds = 240f;
        public const float ProbeSeconds = 40f;

        private float minScale, maxScale;
        private float budget;            // seconds per frame at the target rate
        private float windowTime, windowSum, gpuSum, cpuSum;
        private int windowFrames, slowFrames, gpuSamples, upStreak;
        private float now, lockUntil, lastUpAt = -1000f, lockSeconds = BaseLockSeconds, stableSince;

        public float Scale { get; private set; }
        public float TargetFps { get; private set; }

        public AdaptiveScaler(float startScale, float minScale, float maxScale, float targetFps)
        {
            Configure(startScale, minScale, maxScale, targetFps);
        }

        public void Configure(float startScale, float minScale, float maxScale, float targetFps)
        {
            this.minScale = minScale; this.maxScale = Math.Max(minScale, maxScale);
            Scale = Clamp(startScale);
            TargetFps = Math.Max(10f, targetFps);
            budget = 1f / TargetFps;
            windowTime = windowSum = gpuSum = cpuSum = 0f; windowFrames = slowFrames = gpuSamples = upStreak = 0;
            lockUntil = now; lastUpAt = -1000f; lockSeconds = BaseLockSeconds; stableSince = now;
        }

        private float Clamp(float s) => Math.Min(maxScale, Math.Max(minScale, (float)Math.Round(s / Step) * Step));

        /// <summary>Forces a lower ceiling (thermal throttling). The scale is pulled down to it at once.</summary>
        public void SetMax(float newMax)
        {
            maxScale = Math.Max(minScale, newMax);
            if (Scale > maxScale) Scale = Clamp(maxScale);
        }

        /// <summary>
        /// Updates the frame target (e.g. the display's real refresh rate, which can be lower than the requested rate). A frame
        /// slower than the budget only counts as slow if the GPU or CPU is actually busy for most of it.
        /// </summary>
        public void SetTargetFps(float fps)
        {
            fps = Math.Max(10f, fps);
            if (Math.Abs(fps - TargetFps) < 0.5f) return;
            TargetFps = fps; budget = 1f / fps;
            windowTime = windowSum = gpuSum = cpuSum = 0f; windowFrames = slowFrames = gpuSamples = 0;
        }

        /// <summary>Feeds one frame. Returns true when <see cref="Scale"/> changed. Frames over 0.25 s (loads, pauses) are ignored.</summary>
        public bool Tick(float dt, float gpuMs = -1f, float cpuMs = -1f)
        {
            if (dt <= 0f || dt > 0.25f) return false;
            now += dt;
            windowTime += dt; windowSum += dt; windowFrames++;
            if (dt > budget * 1.3f) slowFrames++;
            if (gpuMs > 0f) { gpuSum += gpuMs; gpuSamples++; }
            if (cpuMs > 0f) cpuSum += cpuMs;
            if (windowTime < EvalSeconds) return false;

            float avg = windowSum / windowFrames;
            float slowFraction = (float)slowFrames / windowFrames;
            bool slow = avg > budget * SlowFactor || slowFraction > 0.15f;
            float gpuAvgMs = gpuSamples > 0 ? gpuSum / gpuSamples : -1f;
            float cpuAvgMs = gpuSamples > 0 && cpuSum > 0f ? cpuSum / gpuSamples : -1f;
            // Slow frames with an idle GPU and CPU are waiting on something else (a display locked below the requested rate):
            // lowering the render scale would not help, so it does not count as slow.
            if (slow && gpuAvgMs > 0f)
            {
                float busyMs = Math.Max(gpuAvgMs, cpuAvgMs);
                if (busyMs < budget * 1000f * 0.85f) slow = false;
            }
            bool headroom = gpuAvgMs > 0f && gpuAvgMs < budget * 1000f * HeadroomFactor && !slow;
            windowTime = windowSum = gpuSum = cpuSum = 0f; windowFrames = slowFrames = gpuSamples = 0;

            bool changed = false;
            if (slow)
            {
                upStreak = 0;
                stableSince = now;
                if (Scale > minScale + 1e-4f)
                {
                    Scale = Clamp(Scale - Step);
                    changed = true;
                    // Dropping soon after a climb means that climb was too ambitious: wait longer next time.
                    if (now - lastUpAt < 15f) lockSeconds = Math.Min(MaxLockSeconds, lockSeconds * 2f);
                    lockUntil = now + lockSeconds;
                }
            }
            else
            {
                bool canClimb = Scale < maxScale - 1e-4f && now >= lockUntil;
                if (headroom) upStreak++; else upStreak = 0;
                bool evidence = upStreak >= 5;                                   // 10 s of GPU headroom
                bool probe = gpuAvgMs <= 0f && now - stableSince >= ProbeSeconds; // no GPU timing: careful periodic probe
                if (canClimb && (evidence || probe))
                {
                    Scale = Clamp(Scale + Step);
                    changed = true;
                    lastUpAt = now;
                    upStreak = 0;
                    stableSince = now;
                }
            }
            return changed;
        }
    }
}
