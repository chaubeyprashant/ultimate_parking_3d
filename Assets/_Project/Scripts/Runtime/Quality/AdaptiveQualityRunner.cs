using System;
using UnityEngine;

namespace UltimateCarParking.Quality
{
    /// <summary>
    /// Feeds frame times to the <see cref="AdaptiveScaler"/> (one light Update, no allocation) and applies its render scale. Polls the
    /// Android thermal status every few seconds and caps the frame rate and scale while the phone is hot. Created once by
    /// <see cref="QualityService"/> on device; not used when the player fixes the graphics level by hand.
    /// </summary>
    public sealed class AdaptiveQualityRunner : MonoBehaviour
    {
        private const float ThermalPollSeconds = 5f;
        private AdaptiveScaler scaler;
        private float nextThermalPoll, nextRefreshPoll;
        private int hotStatus;
        private float coolSince;
        private readonly FrameTiming[] timing = new FrameTiming[1];

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!QualityService.Initialized || FindAnyObjectByType<AdaptiveQualityRunner>() != null) return;
            var go = new GameObject("AdaptiveQuality");
            DontDestroyOnLoad(go);
            go.AddComponent<AdaptiveQualityRunner>();
        }
#endif

        private void OnEnable()
        {
            QualityService.Applied += Reconfigure;
            Reconfigure();
        }

        private void OnDisable() => QualityService.Applied -= Reconfigure;

        private void Reconfigure()
        {
            QualityPreset p = QualityService.Preset;
            float start = Mathf.Clamp(QualityService.RenderScale, p.MinScale, p.MaxScale);
            if (scaler == null) scaler = new AdaptiveScaler(start, p.MinScale, p.MaxScale, QualityService.TargetFps);
            else scaler.Configure(start, p.MinScale, p.MaxScale, QualityService.TargetFps);
        }

        private void Update()
        {
            if (!QualityService.Initialized || scaler == null) return;
            PollThermal();
            if (Time.unscaledTime >= nextRefreshPoll)
            {
                nextRefreshPoll = Time.unscaledTime + 3f;
                // The display may run below the requested rate (power saving, a fixed refresh mode): budget against what it really does.
                float hz = (float)Screen.currentResolution.refreshRateRatio.value;
                scaler.SetTargetFps(hz > 20f ? Mathf.Min(QualityService.TargetFps, hz + 1f) : QualityService.TargetFps);
            }
            if (!QualityService.AdaptiveEnabled) return;

            float gpu = -1f, cpu = -1f;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0)
            {
                gpu = (float)timing[0].gpuFrameTime;
                cpu = (float)Math.Max(timing[0].cpuMainThreadFrameTime, timing[0].cpuRenderThreadFrameTime);
            }
            if (scaler.Tick(Time.unscaledDeltaTime, gpu, cpu)) QualityService.SetRenderScale(scaler.Scale);
        }

        private void PollThermal()
        {
            if (Time.unscaledTime < nextThermalPoll) return;
            nextThermalPoll = Time.unscaledTime + ThermalPollSeconds;
            int status = ReadThermalStatus();
            if (status < 0) return;
            if (status >= 3)               // severe or worse: cap to 30 fps and a smaller image until it cools
            {
                hotStatus = status;
                coolSince = Time.unscaledTime;
                QualityService.SetThermalCaps(30f, 0.7f);
            }
            else if (hotStatus >= 3 && status <= 1 && Time.unscaledTime - coolSince > 60f)
            {
                hotStatus = 0;
                QualityService.SetThermalCaps(0f, 0f);
            }
        }

        private static int ReadThermalStatus()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var power = activity.Call<AndroidJavaObject>("getSystemService", "power");
                return power.Call<int>("getCurrentThermalStatus");
            }
            catch (Exception) { return -1; }
#else
            return -1;
#endif
        }
    }
}
