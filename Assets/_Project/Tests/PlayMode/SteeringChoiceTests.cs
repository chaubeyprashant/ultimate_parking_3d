using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Settings;
using UltimateCarParking.UI;

namespace UltimateCarParking.Tests
{
    /// <summary>Driving Controls: WHEEL / LEFT / RIGHT / TILT is a clear choice, and the panel's rows do not overlap.</summary>
    public class SteeringChoiceTests
    {
        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SettingsService.SetSteeringMode(GameSettings.SteerWheel);
            yield return TestScenes.ResetToEmpty();
        }

        private static IEnumerator OpenControls()
        {
            SceneManager.LoadScene(SceneNames.MainMenu);
            float t = 0f;
            while (Object.FindAnyObjectByType<ControlsPanel>(FindObjectsInactive.Include) == null && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return null;
            Object.FindAnyObjectByType<ControlsPanel>(FindObjectsInactive.Include).Open();
            yield return null;
        }

        private static Button Option(string name) => GameObject.Find("Steering" + name).GetComponent<Button>();

        [UnityTest]
        public IEnumerator Each_Steering_Option_Selects_Its_Mode_And_Lights_Up()
        {
            yield return OpenControls();
            Assert.AreEqual("LEFT / RIGHT", Option("LEFTRIGHT").GetComponentInChildren<TMP_Text>().text);
            Option("LEFTRIGHT").onClick.Invoke(); yield return null;
            Assert.AreEqual(GameSettings.SteerButtons, SettingsService.Current.steeringMode, "simple left / right arrows");
            Assert.AreEqual(UiPalette.Lime, Option("LEFTRIGHT").GetComponent<Image>().color);
            Assert.AreNotEqual(UiPalette.Lime, Option("WHEEL").GetComponent<Image>().color);
            Option("TILT").onClick.Invoke(); yield return null;
            Assert.AreEqual(GameSettings.SteerTilt, SettingsService.Current.steeringMode);
            Option("WHEEL").onClick.Invoke(); yield return null;
            Assert.AreEqual(GameSettings.SteerWheel, SettingsService.Current.steeringMode);
            Assert.AreEqual(UiPalette.Lime, Option("WHEEL").GetComponent<Image>().color);
        }

        [UnityTest]
        public IEnumerator Controls_Panel_Rows_Do_Not_Overlap()
        {
            yield return OpenControls();
            var panel = Object.FindAnyObjectByType<ControlsPanel>();
            RectTransform body = panel.transform.Find("Body") as RectTransform;
            var items = body.Cast<Transform>().Select(t => (RectTransform)t).Where(r => r.gameObject.activeSelf && r.name != "Edge" && r.name != "Accent").ToList();
            const float slack = 12f;
            for (int a = 0; a < items.Count; a++)
                for (int b = a + 1; b < items.Count; b++)
                {
                    UnityEngine.Rect ra = Local(body, items[a]), rb = Local(body, items[b]);
                    bool overlap = ra.xMin + slack < rb.xMax && rb.xMin + slack < ra.xMax && ra.yMin + slack < rb.yMax && rb.yMin + slack < ra.yMax;
                    Assert.IsFalse(overlap, $"{items[a].name} overlaps {items[b].name}");
                }
        }

        [UnityTest]
        public IEnumerator Pause_Screen_Switches_Steering_And_The_Controls_Follow()
        {
#if UNITY_EDITOR
            var cat = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
            LevelSelection.Request(cat.Get(0), cat);
#endif
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while ((LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
            var ui = Object.FindAnyObjectByType<GameplayUiController>();
            ui.Pause();
            yield return null;
            var picker = Object.FindAnyObjectByType<SteeringPicker>();
            Assert.IsNotNull(picker, "steering choice on the pause screen");
            Assert.AreEqual(3, picker.ButtonCount);
            Button[] b = picker.GetComponentsInChildren<Button>().Where(x => x.name.StartsWith("Steering")).ToArray();
            Assert.AreEqual(3, b.Length);
            b[1].onClick.Invoke(); yield return null;   // LEFT / RIGHT
            Assert.AreEqual(GameSettings.SteerButtons, SettingsService.Current.steeringMode);
            Assert.AreEqual(UiPalette.Lime, b[1].GetComponent<Image>().color);
            Assert.AreEqual(GameSettings.SteerButtons, Object.FindAnyObjectByType<MobileControls>(FindObjectsInactive.Include).SteeringMode, "on-screen controls follow");
            b[0].onClick.Invoke(); yield return null;   // WHEEL
            Assert.AreEqual(GameSettings.SteerWheel, SettingsService.Current.steeringMode);
            ui.Resume();
        }

        [UnityTest]
        public IEnumerator Car_Control_Buttons_Are_Big_Enough_To_Read_And_Tap()
        {
#if UNITY_EDITOR
            var cat = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
            LevelSelection.Request(cat.Get(0), cat);
#endif
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while ((LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return null;
            var cluster = (RectTransform)Object.FindAnyObjectByType<VehicleButtons>().transform;
            Assert.GreaterOrEqual(cluster.localScale.x, 0.69f, "cluster not shrunk below the smallest HUD size (0.85, × 0.82 on 4:3 tablets)");
            foreach (string name in new[] { "Headlights", "IndicatorLeft", "Hazard", "IndicatorRight", "Engine", "Horn" })
            {
                var r = (RectTransform)cluster.Find(name);
                Assert.GreaterOrEqual(r.rect.height, 76f, name + " tap height");
                TMP_Text label = r.GetComponentInChildren<TMP_Text>();
                if (label != null && label.text.Length > 0) Assert.GreaterOrEqual(label.fontSize, 24f, name + " text size");
            }
        }

        private static UnityEngine.Rect Local(RectTransform space, RectTransform r)
        {
            var c = new Vector3[4]; r.GetWorldCorners(c);
            Vector3 min = space.InverseTransformPoint(c[0]), max = space.InverseTransformPoint(c[2]);
            return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
