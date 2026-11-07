using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.UI;
using Debug = UnityEngine.Debug;

namespace UltimateCarParking.EditorTools
{
    /// <summary>Captures landscape store screenshots (1920×1080) and composes Play Store PNGs.</summary>
    public static class StoreScreenshotBuilder
    {
        public const int Width = 1920;
        public const int Height = 1080;
        public const string RawDir = "StoreAssets/GooglePlay/screenshots/raw";
        public const string OutDir = "StoreAssets/GooglePlay/screenshots";

        [MenuItem("Tools/Ultimate Car Parking/Generate Play Store Screenshots (7)")]
        public static void GenerateAllMenu()
        {
            GenerateAll();
            EditorUtility.RevealInFinder(Path.GetFullPath(OutDir));
        }

        /// <summary>Batch: -executeMethod UltimateCarParking.EditorTools.StoreScreenshotBuilder.GenerateAll</summary>
        public static void GenerateAll()
        {
            Directory.CreateDirectory(RawDir);
            Directory.CreateDirectory(OutDir);
            CaptureUiScene(ProjectBootstrapper.ScenePathOf(SceneNames.MainMenu), "01_main_menu.jpg", TuneMainMenuForStore);
            LevelData in1 = AssetDatabase.LoadAssetAtPath<LevelData>("Assets/_Project/Levels/Countries/Level_IN1.asset");
            LevelData l1 = AssetDatabase.LoadAssetAtPath<LevelData>("Assets/_Project/Levels/Level_01.asset");
            if (in1 != null)
            {
                WorldTourBuilder.ExportLevelView(in1, "target", Path.Combine(RawDir, "02_world_tour_parking.jpg"));
                WorldTourBuilder.ExportLevelView(in1, "overview", Path.Combine(RawDir, "07_world_arena.jpg"));
            }
            if (l1 != null)
                WorldTourBuilder.ExportLevelScreenshot(l1, Path.Combine(RawDir, "03_campaign_parking.jpg"));
            CaptureUiScene(ProjectBootstrapper.ScenePathOf(SceneNames.Garage), "04_garage.jpg");
            CaptureUiScene(ProjectBootstrapper.ScenePathOf(SceneNames.LevelSelect), "05_level_select.jpg");
            CaptureUiScene(ProjectBootstrapper.ScenePathOf(SceneNames.WorldTour), "06_world_tour.jpg");
            RunComposer();
            Debug.Log("[Store] Screenshots → " + Path.GetFullPath(OutDir));
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void TuneMainMenuForStore(Scene scene)
        {
            foreach (SettingsPanel sp in Object.FindObjectsByType<SettingsPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sp.gameObject.SetActive(false);
            foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.gameObject.name == "Progress")
                    t.text = "18 / 30   LEVELS      ·      72 / 90   STARS";
                if (t.gameObject.name == "CarLabel")
                    t.text = "SPORT";
            }
        }

        private static void CaptureUiScene(string scenePath, string fileName, System.Action<Scene> tune = null)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning("[Store] Missing scene " + scenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            tune?.Invoke(scene);
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogWarning("[Store] No camera in " + scenePath);
                return;
            }

            var canvasStates = new System.Collections.Generic.List<(Canvas c, RenderMode mode, Camera wc, float plane)>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvasStates.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 10f;
            }

            Canvas.ForceUpdateCanvases();
            foreach (Graphic g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                g.SetAllDirty();

            string path = Path.Combine(RawDir, fileName);
            RenderCamera(cam, path);

            foreach (var (c, mode, wc, plane) in canvasStates)
            {
                c.renderMode = mode;
                c.worldCamera = wc;
                c.planeDistance = plane;
            }
        }

        private static void RenderCamera(Camera cam, string path)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture prevTarget = cam.targetTexture;
            CameraClearFlags prevClear = cam.clearFlags;
            Color prevBg = cam.backgroundColor;
            try
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.043f, 0.051f, 0.063f);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToJPG(92));
            }
            finally
            {
                cam.targetTexture = prevTarget;
                cam.clearFlags = prevClear;
                cam.backgroundColor = prevBg;
                RenderTexture.active = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
        }

        private static void RunComposer()
        {
            string root = Path.GetDirectoryName(Application.dataPath)!;
            string script = Path.Combine(root, "Tools/compose_play_store_screenshots.py");
            if (!File.Exists(script))
            {
                Debug.LogWarning("[Store] Missing compose script — raw JPGs only.");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"\"{script}\"",
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(120_000);
            if (p == null) return;
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(stdout)) Debug.Log("[Store] " + stdout.Trim());
            if (p.ExitCode != 0) Debug.LogError("[Store] Compose failed: " + stderr);
        }
    }
}
