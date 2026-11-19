using System;
using System.Text.RegularExpressions;

namespace UltimateCarParking.Quality
{
    /// <summary>What the quality system knows about the phone. Plain data so the classifier is unit-testable.</summary>
    public readonly struct DeviceInfo
    {
        public readonly int RamMb, Cores, AndroidApi, MaxRefreshHz;
        public readonly string GpuName;

        public DeviceInfo(int ramMb, int cores, string gpuName, int androidApi, int maxRefreshHz)
        {
            RamMb = ramMb; Cores = cores; GpuName = gpuName ?? ""; AndroidApi = androidApi; MaxRefreshHz = maxRefreshHz;
        }
    }

    /// <summary>
    /// Picks a <see cref="QualityTier"/> from the GPU family (the main driver of how many pixels a phone can fill), then caps it by
    /// RAM and Android version. Unknown GPUs default to Mid, never High: an unknown phone should not be pushed hard.
    /// </summary>
    public static class DeviceProfile
    {
        public static QualityTier Classify(DeviceInfo d)
        {
            QualityTier byGpu = ByGpu(d.GpuName);
            QualityTier cap = QualityTier.High;
            if (d.RamMb > 0 && d.RamMb < 3500) cap = QualityTier.Low;
            else if (d.RamMb > 0 && d.RamMb < 5000) cap = QualityTier.Mid;
            if (d.AndroidApi > 0 && d.AndroidApi < 28) cap = QualityTier.Low;
            if (d.Cores > 0 && d.Cores <= 4) cap = QualityTier.Low;
            return (QualityTier)Math.Min((int)byGpu, (int)cap);
        }

        private static readonly Regex Number = new Regex(@"(\d{3,4})", RegexOptions.Compiled);
        private static readonly Regex MaliG = new Regex(@"g\s*-?\s*(\d{2,3})", RegexOptions.Compiled);

        public static QualityTier ByGpu(string gpuName)
        {
            string g = (gpuName ?? "").ToLowerInvariant();
            if (g.Length == 0) return QualityTier.Mid;

            if (g.Contains("adreno"))
            {
                Match m = Number.Match(g);
                if (!m.Success) return QualityTier.Mid;
                int n = int.Parse(m.Groups[1].Value);
                if (n >= 640) return QualityTier.High;     // 640, 650, 660, 730, 740 ...
                if (n >= 610) return QualityTier.Mid;      // 610 .. 630
                return QualityTier.Low;                    // 5xx and older, 504-508 ...
            }
            if (g.Contains("immortalis")) return QualityTier.High;
            if (g.Contains("mali"))
            {
                Match m = MaliG.Match(g);
                if (!m.Success) return QualityTier.Low;    // old Mali-T / Mali-4xx
                int n = int.Parse(m.Groups[1].Value);
                if (n >= 77) return QualityTier.High;      // G77, G78, G710, G715 ...
                if (n >= 57) return QualityTier.Mid;       // G57, G68, G76
                return QualityTier.Low;                    // G31, G51, G52
            }
            if (g.Contains("powervr")) return QualityTier.Low;
            if (g.Contains("xclipse")) return QualityTier.High;
            if (g.Contains("apple")) return QualityTier.High;
            return QualityTier.Mid;
        }
    }
}
