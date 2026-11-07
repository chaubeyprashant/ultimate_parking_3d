using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UltimateCarParking.UI;
using Debug = UnityEngine.Debug;

namespace UltimateCarParking.EditorTools
{
    /// <summary>App icon, Android static splash art, and Unity splash screen settings.</summary>
    public static class BrandingBuilder
    {
        public const string BrandingDir = "Assets/_Project/Art/Branding";
        public const string IconPath = BrandingDir + "/AppIcon.png";
        public const string PlayStoreIconPath = BrandingDir + "/PlayStoreIcon.png";
        public const string PlayStoreExportPath = "StoreAssets/GooglePlay/icon-512.png";
        public const string AndroidSplashPath = BrandingDir + "/AndroidSplash.png";

        [MenuItem("Tools/Ultimate Car Parking/Generate Play Store Icon (512)")]
        public static void GeneratePlayStoreIconMenu()
        {
            GeneratePlayStoreIcon();
            ConfigureTextures();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            EditorUtility.RevealInFinder(Path.GetFullPath(PlayStoreExportPath));
        }

        public static void GeneratePlayStoreIcon()
        {
            Directory.CreateDirectory(BrandingDir);
            Directory.CreateDirectory(Path.GetDirectoryName(PlayStoreExportPath)!);
            RunPythonIconGenerator();
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(PlayStoreIconPath, ImportAssetOptions.ForceUpdate);
        }

        public static void ApplyAll()
        {
            Directory.CreateDirectory(BrandingDir);
            GeneratePlayStoreIcon();
            WriteSplash(1920, 1080, AndroidSplashPath);
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(AndroidSplashPath, ImportAssetOptions.ForceUpdate);
            ConfigureTextures();
            ApplyPlayerSettings();
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = UiPalette.Background;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
                PlayerSettings.SetIcons(NamedBuildTarget.Android, new[] { icon }, IconKind.Application);

            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            var so = new SerializedObject(ps);
            so.FindProperty("m_ShowUnitySplashScreen").boolValue = false;
            so.FindProperty("m_ShowUnitySplashLogo").boolValue = false;
            var splash = AssetDatabase.LoadAssetAtPath<Texture2D>(AndroidSplashPath);
            if (splash != null)
            {
                so.FindProperty("AndroidSplashScreenScale").intValue = (int)AndroidSplashScreenScale.ScaleToFit;
                so.FindProperty("androidSplashScreen").objectReferenceValue = splash;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RunPythonIconGenerator()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath)!;
            string script = Path.Combine(projectRoot, "Tools/generate_play_store_icon.py");
            if (!File.Exists(script))
            {
                Debug.LogWarning("[Branding] Missing Tools/generate_play_store_icon.py — using fallback icon.");
                WriteIconFallback(512, IconPath);
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"\"{script}\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return;
            p.WaitForExit(120_000);
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(stdout)) Debug.Log("[Branding] " + stdout.Trim());
            if (p.ExitCode != 0)
                Debug.LogError("[Branding] Icon generator failed: " + stderr);
        }

        private static void ConfigureTextures()
        {
            Configure(IconPath, 512, true);
            Configure(PlayStoreIconPath, 512, true);
            Configure(AndroidSplashPath, 2048, false);
        }

        private static void Configure(string path, int maxSize, bool isIcon)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = isIcon ? TextureImporterType.Default : TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = maxSize;
            ti.alphaIsTransparency = true;
            if (isIcon)
            {
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.androidETC2FallbackOverride = AndroidETC2FallbackOverride.Quality32Bit;
            }
            ti.SaveAndReimport();
        }

        private static void WriteIconFallback(int size, string path)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color bg = UiPalette.Background;
            Color fg = UiPalette.TextPrimary;
            Color accent = UiPalette.Lime;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, bg);

            float cx = size * 0.5f, cy = size * 0.52f;
            // Simple top-down car silhouette + bay lines (reads at launcher size).
            FillRoundRect(tex, size * 0.22f, size * 0.30f, size * 0.56f, size * 0.38f, 0.08f, fg);
            FillRoundRect(tex, size * 0.30f, size * 0.42f, size * 0.40f, size * 0.22f, 0.06f, fg);
            for (int i = 0; i < 3; i++)
            {
                float lx = size * (0.18f + i * 0.22f);
                FillRect(tex, lx, size * 0.18f, size * 0.04f, size * 0.62f, accent * 0.85f);
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void WriteSplash(int w, int h, string path)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color bg = UiPalette.Background;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, bg);

            Color fg = UiPalette.TextPrimary;
            Color dim = UiPalette.TextSecondary;
            // Title block (approximate main-menu typography).
            DrawTextBlock(tex, w, h, fg, dim);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void DrawTextBlock(Texture2D tex, int w, int h, Color fg, Color dim)
        {
            int cx = w / 2;
            int cy = h / 2;
            FillRect(tex, cx - 36, cy - 8, 72, 4, fg);
            // "ULTIMATE" bar
            FillRect(tex, cx - 140, cy + 36, 280, 6, dim * 0.9f);
            // "CAR PARKING" wide bar
            FillRect(tex, cx - 260, cy - 10, 520, 28, fg * 0.95f);
            FillRect(tex, cx + 120, cy - 6, 120, 18, dim);
        }

        private static void FillRect(Texture2D t, float x, float y, float w, float h, Color c)
        {
            int x0 = Mathf.RoundToInt(x), y0 = Mathf.RoundToInt(y);
            int x1 = Mathf.RoundToInt(x + w), y1 = Mathf.RoundToInt(y + h);
            for (int py = y0; py < y1; py++)
            for (int px = x0; px < x1; px++)
                if (px >= 0 && py >= 0 && px < t.width && py < t.height) t.SetPixel(px, py, c);
        }

        private static void FillRoundRect(Texture2D t, float x, float y, float w, float h, float radius, Color c)
        {
            int x0 = Mathf.RoundToInt(x), y0 = Mathf.RoundToInt(y);
            int x1 = Mathf.RoundToInt(x + w), y1 = Mathf.RoundToInt(y + h);
            float r = radius * Mathf.Min(w, h);
            for (int py = y0; py < y1; py++)
            for (int px = x0; px < x1; px++)
            {
                float dx = Mathf.Max(0, Mathf.Max(x0 + r - px, px - (x1 - r)));
                float dy = Mathf.Max(0, Mathf.Max(y0 + r - py, py - (y1 - r)));
                if (dx * dx + dy * dy <= r * r && px >= 0 && py >= 0 && px < t.width && py < t.height)
                    t.SetPixel(px, py, c);
            }
        }
    }
}
