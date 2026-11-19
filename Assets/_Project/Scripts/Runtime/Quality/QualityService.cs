using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UltimateCarParking.Settings;

namespace UltimateCarParking.Quality
{
    /// <summary>
    /// Applies the quality tier to the running game. The URP asset is copied once at startup and the copy is what gets changed, so
    /// nothing is ever written to the asset in the project. Auto picks a tier from the device; Low/Medium/High fix it. The frame
    /// target is set explicitly with vSync off, because with vSync on Unity ignores <c>targetFrameRate</c> and the display decides.
    /// Off in the editor (tests drive it explicitly).
    /// </summary>
    public static class QualityService
    {
        public static bool Initialized { get; private set; }
        public static QualityTier DetectedTier { get; private set; } = QualityTier.Mid;
        public static QualityTier ActiveTier { get; private set; } = QualityTier.Mid;
        public static QualityPreset Preset { get; private set; }
        public static DeviceInfo Device { get; private set; }
        public static int MaxRefreshHz { get; private set; } = 60;
        public static float RenderScale => pipeline != null ? pipeline.renderScale : 1f;
        public static int TargetFps { get; private set; } = 60;
        public static bool AdaptiveEnabled { get; private set; }
        /// <summary>Raised after a mode or tier change so the adaptive runner can reconfigure.</summary>
        public static event Action Applied;

        private static UniversalRenderPipelineAsset pipeline;
        private static float thermalFpsCap = 0f, thermalScaleCap = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Initialized = false; pipeline = null; Applied = null; thermalFpsCap = thermalScaleCap = 0f;
            SettingsService.Changed -= OnSettingsChanged;
        }

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() => Initialize(ReadDevice());
#endif

        public static DeviceInfo ReadDevice()
        {
            int api = 0;
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using var v = new AndroidJavaClass("android.os.Build$VERSION"); api = v.GetStatic<int>("SDK_INT"); } catch (Exception) { }
#endif
            int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            foreach (Resolution r in Screen.resolutions) hz = Mathf.Max(hz, Mathf.RoundToInt((float)r.refreshRateRatio.value));
            return new DeviceInfo(SystemInfo.systemMemorySize, SystemInfo.processorCount, SystemInfo.graphicsDeviceName, api, hz);
        }

        public static void Initialize(DeviceInfo device)
        {
            Device = device;
            MaxRefreshHz = Mathf.Max(60, device.MaxRefreshHz);
            DetectedTier = DeviceProfile.Classify(device);
            if (pipeline == null)
            {
                RenderPipelineAsset source = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
                if (source is UniversalRenderPipelineAsset urp)
                {
                    pipeline = UnityEngine.Object.Instantiate(urp);
                    pipeline.name = urp.name + " (runtime)";
                    QualitySettings.renderPipeline = pipeline;
                }
            }
            else if (QualitySettings.renderPipeline != pipeline) QualitySettings.renderPipeline = pipeline;   // keep using the copy
            Initialized = true;
            SettingsService.Changed -= OnSettingsChanged;
            SettingsService.Changed += OnSettingsChanged;
            Apply(SettingsService.Current);
        }

        private static void OnSettingsChanged(GameSettings s) => Apply(s);

        /// <summary>The tier a graphics mode resolves to on this device.</summary>
        public static QualityTier Resolve(int graphicsMode, QualityTier detected) => graphicsMode switch
        {
            QualityModes.GraphicsLow => QualityTier.Low,
            QualityModes.GraphicsMedium => QualityTier.Mid,
            QualityModes.GraphicsHigh => QualityTier.High,
            _ => detected,
        };

        /// <summary>The frame target for a mode, tier and refresh rate (pure).</summary>
        public static int ResolveFps(int fpsMode, QualityTier tier, int presetFps, int maxRefreshHz)
        {
            switch (fpsMode)
            {
                case QualityModes.Fps30: return 30;
                case QualityModes.Fps60: return 60;
                case QualityModes.FpsMax: return tier == QualityTier.High ? Mathf.Clamp(maxRefreshHz, 60, 120) : 60;
                default: return presetFps;
            }
        }

        public static void Apply(GameSettings s)
        {
            if (!Initialized) return;
            ActiveTier = Resolve(s.graphicsMode, DetectedTier);
            Preset = QualityPresets.For(ActiveTier);
            AdaptiveEnabled = s.graphicsMode == QualityModes.GraphicsAuto;

            TargetFps = ResolveFps(s.frameRateMode, ActiveTier, Preset.TargetFps, MaxRefreshHz);
            if (thermalFpsCap > 0f) TargetFps = Mathf.Min(TargetFps, Mathf.RoundToInt(thermalFpsCap));
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;
            QualitySettings.lodBias = Preset.LodBias;
            QualitySettings.globalTextureMipmapLimit = Mathf.Max(Preset.TextureMipLimit, MemoryGuard.ExtraMipLimit);

            if (pipeline != null)
            {
                pipeline.msaaSampleCount = Preset.Msaa;
                pipeline.shadowDistance = Preset.ShadowDistance;
                pipeline.mainLightShadowmapResolution = Preset.ShadowResolution;
                float cap = thermalScaleCap > 0f ? Mathf.Min(Preset.MaxScale, thermalScaleCap) : Preset.MaxScale;
                pipeline.renderScale = Mathf.Min(Preset.RenderScale, cap);
            }
            Applied?.Invoke();
        }

        /// <summary>Sets the render scale chosen by the adaptive scaler.</summary>
        public static void SetRenderScale(float scale)
        {
            if (pipeline != null && !Mathf.Approximately(pipeline.renderScale, scale)) pipeline.renderScale = scale;
        }

        /// <summary>Thermal throttling: caps the frame rate and render scale (0 = no cap) and re-applies.</summary>
        public static void SetThermalCaps(float fpsCap, float scaleCap)
        {
            if (Mathf.Approximately(fpsCap, thermalFpsCap) && Mathf.Approximately(scaleCap, thermalScaleCap)) return;
            thermalFpsCap = fpsCap; thermalScaleCap = scaleCap;
            if (Initialized) Apply(SettingsService.Current);
        }

        public static string Describe() =>
            $"{(SettingsService.Current.graphicsMode == QualityModes.GraphicsAuto ? "auto" : "manual")} {ActiveTier} (device {DetectedTier}), scale {RenderScale:F2}, msaa {(pipeline != null ? pipeline.msaaSampleCount : 0)}, {TargetFps} fps target, {MaxRefreshHz} Hz max";
    }
}
