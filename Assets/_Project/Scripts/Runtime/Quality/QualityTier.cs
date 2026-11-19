using UnityEngine;

namespace UltimateCarParking.Quality
{
    public enum QualityTier { Low = 0, Mid = 1, High = 2 }

    /// <summary>One tier's rendering settings. All values are applied to a runtime copy of the URP asset, never to the asset on disk.</summary>
    public readonly struct QualityPreset
    {
        public readonly QualityTier Tier;
        public readonly float RenderScale, MinScale, MaxScale;
        /// <summary>MSAA sample count: 1 (off), 2 or 4.</summary>
        public readonly int Msaa;
        public readonly float ShadowDistance;
        public readonly int ShadowResolution;
        public readonly float LodBias;
        public readonly int TargetFps;
        /// <summary>0 = full-size textures, 1 = half-size (a quarter of the texture memory). Low tier uses half-size.</summary>
        public readonly int TextureMipLimit;

        public QualityPreset(QualityTier tier, float scale, float min, float max, int msaa, float shadowDistance, int shadowResolution, float lodBias, int fps, int textureMipLimit = 0)
        {
            TextureMipLimit = textureMipLimit;
            Tier = tier; RenderScale = scale; MinScale = min; MaxScale = max; Msaa = msaa;
            ShadowDistance = shadowDistance; ShadowResolution = shadowResolution; LodBias = lodBias; TargetFps = fps;
        }
    }

    public static class QualityPresets
    {
        // Low: 30 FPS, lighter everything. Mid: 60 FPS with no MSAA and a smaller render scale (the GPU, not the CPU, is the
        // limit on mid phones: see Documentation/PHASE1_PERF_BASELINE.md). High: the original look at 60 FPS (up to 120 with "Max").
        public static QualityPreset For(QualityTier tier) => tier switch
        {
            QualityTier.Low => new QualityPreset(QualityTier.Low, 0.70f, 0.55f, 0.80f, 1, 15f, 512, 0.6f, 30, textureMipLimit: 1),
            QualityTier.Mid => new QualityPreset(QualityTier.Mid, 0.85f, 0.65f, 1.00f, 1, 25f, 1024, 0.8f, 60),
            _ => new QualityPreset(QualityTier.High, 1.00f, 0.75f, 1.00f, 2, 45f, 1024, 1.0f, 60),
        };
    }

    /// <summary>User-facing choices stored in the save (0 = Auto everywhere, so old saves keep working).</summary>
    public static class QualityModes
    {
        public const int GraphicsAuto = 0, GraphicsLow = 1, GraphicsMedium = 2, GraphicsHigh = 3;
        public const int FpsAuto = 0, Fps30 = 1, Fps60 = 2, FpsMax = 3;

        public static int NextGraphics(int mode) => (Mathf.Clamp(mode, 0, 3) + 1) % 4;
        public static int NextFps(int mode) => (Mathf.Clamp(mode, 0, 3) + 1) % 4;
        public static string GraphicsName(int mode) => mode switch { GraphicsLow => "LOW", GraphicsMedium => "MEDIUM", GraphicsHigh => "HIGH", _ => "AUTO" };
        public static string FpsName(int mode) => mode switch { Fps30 => "30", Fps60 => "60", FpsMax => "MAX", _ => "AUTO" };
    }
}
