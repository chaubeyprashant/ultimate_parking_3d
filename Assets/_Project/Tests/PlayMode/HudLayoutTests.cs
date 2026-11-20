using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Settings;
using SettingsService = UltimateCarParking.Settings.SettingsService;
using UltimateCarParking.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Phase 8 HUD layout in the real Gameplay scene, emulating phone screens in canvas units (1080 high): 16:9, 20:9,
    /// 19.5:9 with a 90-unit notch cut-out, 16:10 and 4:3; right- and left-handed; HUD and control sizes at both extremes.
    /// Every essential HUD element must sit inside the safe area and not overlap any other, with the rear camera up.
    /// (Editor layout check; not a substitute for looking at a real phone.)
    /// </summary>
    public class HudLayoutTests
    {
        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() { yield return TestScenes.ResetToEmpty(); }

        private static readonly string[] Essentials =
        {
            "PauseButton", "LevelBlock", "TimerPill", "HitsPill", "ViewButton", "ZoomButton", "ObjectiveProgress", "Objectives/Objective1",
            "Guide", "RearCamera", "Speedometer", "ParkAssist", "SteerZone", "PedalZone", "VehicleButtons",
        };

        private static Rect CanvasRect(RectTransform r, RectTransform canvas)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            Vector2 a = canvas.InverseTransformPoint(c[0]), b = canvas.InverseTransformPoint(c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>The control zones' real footprint: the union of their visible children (zones are larger than their controls).</summary>
        private static Rect ZoneRect(RectTransform zone, RectTransform canvas)
        {
            Rect u = default; bool any = false;
            foreach (RectTransform c in zone)
            {
                if (!c.gameObject.activeInHierarchy) continue;
                Rect r = CanvasRect(c, canvas);
                u = any ? Rect.MinMaxRect(Mathf.Min(u.xMin, r.xMin), Mathf.Min(u.yMin, r.yMin), Mathf.Max(u.xMax, r.xMax), Mathf.Max(u.yMax, r.yMax)) : r;
                any = true;
            }
            return u;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Essential_Hud_Elements_Fit_And_Never_Overlap_On_Common_Phone_Screens()
        {
#if UNITY_EDITOR
            var cat = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/Career/Chapter_ch3.asset");   // multi-objective missions live here
            LevelData level = null;
            for (int i = 0; i < cat.Count; i++) if (cat.Get(i).CreateObjectives().Length > 1) { level = cat.Get(i); break; }
            LevelSelection.Request(level ?? cat.Get(0), cat);
#endif
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while (LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) { t += Time.unscaledDeltaTime; if (t > 10f) Assert.Fail("level"); yield return null; }
            LevelLoader.Instance.Vehicle.Input.KeyboardEnabled = false;

            Canvas canvas = Object.FindObjectsByType<Canvas>().First(c => c.name == "GameplayCanvas");
            var rootRt = (RectTransform)canvas.transform;
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); if (scaler != null) scaler.enabled = false;
            var safe = (RectTransform)rootRt.Find("SafeArea");
            var fitter = safe.GetComponent<SafeAreaFitter>(); if (fitter != null) fitter.enabled = false;
            var hud = safe.Find("Hud");
            var layout = hud.GetComponent<HudLayout>();
            var rear = Object.FindAnyObjectByType<RearCameraView>();
            rear.RearCamera.ForceOn = true;                       // rear camera up, as when reversing
            yield return new WaitForSeconds(0.4f);

            var screens = new (string name, float aspect, float notch)[] { ("16:9", 16f / 9f, 0f), ("20:9", 20f / 9f, 0f), ("19.5:9 notch", 19.5f / 9f, 90f), ("16:10", 1.6f, 0f), ("4:3", 4f / 3f, 0f) };
            var problems = new List<string>();
            foreach (var sc in screens)
                foreach (bool lefty in new[] { false, true })
                    foreach ((float hudS, float ctrlS) in new[] { (1f, 1f), (GameSettings.MaxHudScale, GameSettings.MaxControlScale), (GameSettings.MinHudScale, GameSettings.MinControlScale) })
                    {
                        SettingsService.SetLeftHanded(lefty);
                        SettingsService.SetHudScale(hudS);
                        SettingsService.SetControlScale(ctrlS);
                        rootRt.sizeDelta = new Vector2(1080f * sc.aspect, 1080f);
                        safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
                        safe.offsetMin = new Vector2(sc.notch, 0f); safe.offsetMax = Vector2.zero;   // notch on the left edge
                        Canvas.ForceUpdateCanvases();
                        layout.Apply();
                        Canvas.ForceUpdateCanvases();
                        yield return null;
                        Rect area = CanvasRect(safe, rootRt);
                        var rects = new List<(string, Rect)>();
                        foreach (string path in Essentials)
                        {
                            Transform tr = hud.Find(path) ?? hud.Find("Controls/" + path);
                            if (tr == null || !tr.gameObject.activeInHierarchy) continue;
                            Rect r = path.EndsWith("Zone") ? ZoneRect((RectTransform)tr, rootRt) : CanvasRect((RectTransform)tr, rootRt);
                            rects.Add((path, r));
                            if (r.xMin < area.xMin - 1f || r.xMax > area.xMax + 1f || r.yMin < area.yMin - 1f || r.yMax > area.yMax + 1f)
                                problems.Add($"{sc.name} {(lefty ? "L" : "R")} hud {hudS} ctrl {ctrlS}: {path} outside the safe area");
                        }
                        for (int i = 0; i < rects.Count; i++)
                            for (int j = i + 1; j < rects.Count; j++)
                            {
                                Rect a = rects[i].Item2, b = rects[j].Item2;
                                float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                                if (ox > 2f && oy > 2f) problems.Add($"{sc.name} {(lefty ? "L" : "R")} hud {hudS} ctrl {ctrlS}: {rects[i].Item1} overlaps {rects[j].Item1} ({ox:F0}×{oy:F0})");
                            }
                    }
            Debug.Log($"[HUD] {screens.Length * 6} layouts checked, {problems.Count} problems" + (problems.Count > 0 ? ":\n" + string.Join("\n", problems.Take(30)) : ""));
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(30)));
        }

        [UnityTest]
        public IEnumerator Objectives_Start_Compact_Expand_On_Tap_And_Collapse_While_Reversing()
        {
#if UNITY_EDITOR
            var cat = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/Career/Chapter_ch3.asset");
            LevelData level = null;
            for (int i = 0; i < cat.Count; i++) if (cat.Get(i).CreateObjectives().Length > 1) { level = cat.Get(i); break; }
            Assert.IsNotNull(level, "a multi-objective mission");
            LevelSelection.Request(level, cat);
#endif
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while (LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) { t += Time.unscaledDeltaTime; if (t > 10f) Assert.Fail("level"); yield return null; }
            var hud = Object.FindAnyObjectByType<GameplayHud>();
            yield return null;
            Assert.AreEqual(1, hud.VisibleObjectiveRows, "compact: only the current objective");
            StringAssert.Contains("SHOW ALL", hud.ObjectiveProgressText);
            hud.ToggleObjectives();
            Assert.Greater(hud.VisibleObjectiveRows, 1, "expanded on tap");
            Object.FindAnyObjectByType<RearCameraView>().RearCamera.ForceOn = true;
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1, hud.VisibleObjectiveRows, "collapses while the rear camera is up");
            Object.FindAnyObjectByType<RearCameraView>().RearCamera.ForceOn = false;
            yield return new WaitForSeconds(2.2f);
            Assert.Greater(hud.VisibleObjectiveRows, 1, "comes back after reversing");
            hud.ToggleObjectives();
        }

        [Test]
        public void Hud_Scale_Is_Clamped_Saved_And_Reset()
        {
            SettingsService.SetHudScale(3f);
            Assert.AreEqual(GameSettings.MaxHudScale, SettingsService.Current.hudScale);
            SettingsService.SetHudScale(1.1f);
            Assert.AreEqual(1.1f, SettingsService.Current.hudScale, 1e-4f);
            SettingsService.ResetControls();
            Assert.AreEqual(1f, SettingsService.Current.hudScale);
        }
    }
}
