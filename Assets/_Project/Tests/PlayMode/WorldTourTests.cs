using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;
using UltimateCarParking.UI;
using UltimateCarParking.WorldTour;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// World Tour through the real scene: pins and locks from the save, country selection and the detail card,
    /// region chips, drag and pinch, playing a country and coming back, and the back button.
    /// </summary>
    public class WorldTourTests
    {
        [SetUp]
        public void SetUp()
        {
            TestScenes.UseTemporarySave();
            WorldTourController.ForgetSelection();
        }

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.ResetToEmpty();

        private static LevelCatalog Campaign()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
#else
            return null;
#endif
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeout, string what)
        {
            float t = 0f;
            while (!condition())
            {
                t += Time.unscaledDeltaTime;
                if (t > timeout) Assert.Fail($"timed out: {what}");
                yield return null;
            }
        }

        private static IEnumerator OpenTour()
        {
            SceneManager.LoadScene(SceneNames.WorldTour);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.WorldTour, 5f, "world tour");
            yield return WaitFor(() => Tour() != null && Tour().Markers.Count == 30 && Tour().SelectedMarker != null, 5f, "pins");
            yield return new WaitForSecondsRealtime(0.7f);   // region glide
        }

        private static WorldTourController Tour() => Object.FindAnyObjectByType<WorldTourController>();

        private static void CompleteCountry(CountryData c, int stars)
        {
            for (int m = 0; m < c.MissionCount; m++)
            {
                LevelData l = c.GetLevel(m);
                ProgressionService.RecordCompletion(SaveManager.Current.Data, l, new LevelResult(l.LevelId, true, FailReason.None, stars, 20f, 0, 0));
            }
            SaveManager.Current.Save();
        }

        private static CountryData India()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<CountryCatalog>("Assets/_Project/WorldTour/CountryCatalog.asset").Get(0);
#else
            return null;
#endif
        }

        private static void Complete(int levelNumber, int stars)
        {
            LevelData l = Campaign().Get(levelNumber - 1);
            ProgressionService.RecordCompletion(SaveManager.Current.Data, l, new LevelResult(l.LevelId, true, FailReason.None, stars, 20f, 0, 0));
            SaveManager.Current.Save();
        }

        [UnityTest]
        public IEnumerator Fresh_Save_Shows_30_Pins_With_Only_India_Open()
        {
            yield return OpenTour();
            WorldTourController ui = Tour();
            Assert.AreEqual(30, ui.Markers.Count);
            Assert.AreEqual(CountryState.Unlocked, ui.Markers[0].State);
            Assert.IsTrue(ui.Markers.Skip(1).All(m => m.State == CountryState.Locked), "the other 29 are locked");
            Assert.AreEqual("in", ui.SelectedCountry.CountryId, "starts on the current stop");
            Assert.AreEqual("INDIA", ui.DetailName);
            Assert.AreEqual("UNLOCKED", ui.DetailStatus);
            Assert.AreEqual($"0/{ui.SelectedCountry.MissionCount}", ui.DetailMissions);
            Assert.IsNotNull(ui.DetailFlag);
            Assert.IsNotNull(ui.DetailPreview);
            StringAssert.Contains("COUNTRY BONUS", ui.BonusText, "country reward shown on the card");
            StringAssert.Contains(ui.SelectedCountry.CompletionCoins.ToString(), ui.BonusText);
            Assert.IsTrue(ui.PlayAvailable);
            Assert.AreEqual(WorldTourController.ChipOf(WorldRegion.Asia), ui.Region, "opens on India's region");
            Assert.Greater(ui.MapView.RelativeZoom, 1.5f, "zoomed into the region");
            Assert.IsTrue(ui.Markers[0].NameVisible, "selected pin shows its name");
            Assert.IsTrue(Object.FindObjectsByType<TMPro.TMP_Text>().Any(t => t.text.StartsWith("0 / 30")), "tour progress shown");
        }

        [UnityTest]
        public IEnumerator Tapping_A_Locked_Country_Shows_It_But_Play_Is_Blocked()
        {
            yield return OpenTour();
            WorldTourController ui = Tour();
            CountryMarker japan = ui.Markers[1];
            japan.Button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreSame(japan, ui.SelectedMarker);
            Assert.IsTrue(japan.Selected && !ui.Markers[0].Selected, "selection moved");
            Assert.AreEqual("JAPAN", ui.DetailName);
            Assert.AreEqual("LOCKED", ui.DetailStatus);
            Assert.AreEqual("FINISH INDIA TO UNLOCK", ui.DetailLockHint);
            Assert.IsFalse(ui.PlayAvailable);
            ui.PlaySelected();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(SceneNames.WorldTour, SceneManager.GetActiveScene().name, "a locked country does not start");
        }

        [UnityTest]
        public IEnumerator Progress_Unlocks_The_Next_Country_And_Marks_The_Finished_One()
        {
            CompleteCountry(India(), 3);
            yield return OpenTour();
            WorldTourController ui = Tour();
            Assert.AreEqual(CountryState.Completed, ui.Markers[0].State);
            Assert.AreEqual(CountryState.Unlocked, ui.Markers[1].State);
            Assert.AreEqual(CountryState.Locked, ui.Markers[2].State);
            Assert.AreEqual("jp", ui.SelectedCountry.CountryId, "opens on the next stop");
            ui.Select(0);
            yield return null;
            Assert.AreEqual("COMPLETED", ui.DetailStatus);
            Assert.AreEqual($"{ui.SelectedCountry.MissionCount}/{ui.SelectedCountry.MissionCount}", ui.DetailMissions);
            Assert.IsTrue(Object.FindObjectsByType<TMPro.TMP_Text>().Any(t => t.text.StartsWith("1 / 30")), "1 country completed");
        }

        [UnityTest]
        public IEnumerator Region_Chips_Zoom_To_A_Region_And_Spread_Its_Pins()
        {
            yield return OpenTour();
            WorldTourController ui = Tour();
            WorldMapView view = ui.MapView;
            ui.ShowRegion(0);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(1f, view.RelativeZoom, 0.01f, "WORLD fits the whole map");

            int europe = WorldTourController.ChipOf(WorldRegion.Europe);
            ui.ShowRegion(europe);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(europe, ui.Region);
            Assert.Greater(view.RelativeZoom, 3f, "Europe is zoomed in");
            var viewport = (RectTransform)view.transform;
            var pins = ui.Markers.Where(m => m.Country.Region == WorldRegion.Europe).ToList();
            Assert.AreEqual(10, pins.Count);
            foreach (CountryMarker m in pins)
            {
                Vector2 p = view.MapToViewport(WorldMapProjection.ToNormalized(m.Country.LatLon));
                Assert.IsTrue(viewport.rect.Contains(p), $"{m.Country.DisplayName} on screen in the Europe view");
            }
            // Pins (104 px frames at full scale) must not pile up: centres at least ~half a pin apart at this screen size.
            float pinWidth = 104f * view.MarkerScreenScale;
            float closest = float.MaxValue;
            string pair = "";
            for (int i = 0; i < pins.Count; i++)
                for (int j = i + 1; j < pins.Count; j++)
                {
                    float d = Vector2.Distance(view.MapToViewport(WorldMapProjection.ToNormalized(pins[i].Country.LatLon)),
                                               view.MapToViewport(WorldMapProjection.ToNormalized(pins[j].Country.LatLon)));
                    if (d < closest) { closest = d; pair = pins[i].Country.DisplayName + " / " + pins[j].Country.DisplayName; }
                }
            Debug.Log($"[Test] Europe view: viewport {viewport.rect.size}, zoom x{view.RelativeZoom:F2}, pin {pinWidth:F0} px, closest pins {pair} {closest:F0} px");
            Assert.Greater(closest, pinWidth * 0.55f, $"closest pins {pair}");
        }

        [UnityTest]
        public IEnumerator Drag_Pans_And_Pinch_Zooms_The_Map()
        {
            yield return OpenTour();
            WorldMapView view = Tour().MapView;
            view.SetView(view.FitZoom * 2.5f, Vector2.zero);
            var es = EventSystem.current;
            var viewport = (RectTransform)view.transform;
            Vector2 centre = RectTransformUtility.WorldToScreenPoint(null, viewport.TransformPoint(viewport.rect.center));

            // One finger: drag 120 px to the right.
            var a = new PointerEventData(es) { pointerId = 1, position = centre };
            view.OnPointerDown(a);
            view.OnBeginDrag(a);
            Vector2 before = ((RectTransform)viewport.GetChild(0)).anchoredPosition;
            a.position = centre + new Vector2(120f, 0f);
            view.OnDrag(a);
            view.OnEndDrag(a);
            view.OnPointerUp(a);
            Vector2 after = ((RectTransform)viewport.GetChild(0)).anchoredPosition;
            Assert.Greater(after.x - before.x, 20f, "dragging right moves the map right");
            Assert.IsTrue(view.Interacted);

            // Two fingers: spread them apart → zoom in.
            float z0 = view.Zoom;
            var f1 = new PointerEventData(es) { pointerId = 11, position = centre + new Vector2(-60f, 0f) };
            var f2 = new PointerEventData(es) { pointerId = 12, position = centre + new Vector2(60f, 0f) };
            view.OnPointerDown(f1);
            view.OnPointerDown(f2);
            f2.position = centre + new Vector2(140f, 0f);
            view.OnDrag(f2);
            Assert.Greater(view.Zoom, z0 * 1.3f, "pinch out zooms in");
            view.OnPointerUp(f1);
            view.OnPointerUp(f2);

            // Zoom is clamped: never smaller than the whole-map fit.
            view.ZoomAbout(Vector2.zero, view.FitZoom * 0.2f, Vector2.zero);
            Assert.AreEqual(view.FitZoom, view.Zoom, 1e-4f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Playing_A_Country_Loads_Its_Mission_And_Returns_To_The_Tour()
        {
            yield return OpenTour();
            WorldTourController ui = Tour();
            ui.Select(0);
            ui.PlaySelected();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Gameplay, 6f, "gameplay");
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.Level != null, 6f, "level loaded");
            Assert.AreEqual(India().GetLevel(0).LevelId, LevelManager.Instance.Level.LevelId, "India starts with its first mission");
            Assert.AreEqual(SceneNames.WorldTour, LevelSelection.ReturnScene);
            yield return TestScenes.WaitForTransition();

            Object.FindAnyObjectByType<GameplayUiController>().ToLevels();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.WorldTour, 6f, "back to the tour");
            yield return WaitFor(() => Tour() != null && Tour().SelectedMarker != null, 5f, "tour ready");
            Assert.AreEqual("in", Tour().SelectedCountry.CountryId, "the played country stays selected");
            yield return TestScenes.WaitForTransition();
        }

        [UnityTest]
        public IEnumerator Mission_Picker_Locks_Later_Missions_And_Plays_The_Chosen_One()
        {
            yield return OpenTour();
            WorldTourController ui = Tour();
            ui.Select(0);
            yield return null;
            CountryData india = ui.SelectedCountry;
            Assert.GreaterOrEqual(india.MissionCount, 3, "India has its Mumbai missions");
            Assert.AreEqual(0, ui.SelectedMission);
            StringAssert.Contains("MISSION 1", ui.DetailDescription);
            ui.SelectMission(1);
            yield return null;
            Assert.IsFalse(ui.PlayAvailable, "mission 2 locked until mission 1 is done");

            LevelData m1 = india.GetLevel(0);
            ProgressionService.RecordCompletion(SaveManager.Current.Data, m1, new LevelResult(m1.LevelId, true, FailReason.None, 3, 20f, 0, 0));
            ui.Refresh();
            ui.SelectMission(1);
            yield return null;
            Assert.IsTrue(ui.PlayAvailable, "mission 2 open after mission 1");
            StringAssert.Contains("MISSION 2", ui.DetailDescription);
            ui.PlaySelected();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Gameplay, 6f, "gameplay");
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.Level != null, 8f, "level loaded");
            Assert.AreSame(india.GetLevel(1), LevelManager.Instance.Level, "the chosen mission loads");
            Assert.IsTrue(LevelManager.Instance.HasNextLevel, "next mission follows inside the country");
            yield return TestScenes.WaitForTransition();
        }

        [UnityTest]
        public IEnumerator Back_Returns_To_The_Main_Menu()
        {
            yield return OpenTour();
            yield return TestScenes.WaitForTransition();
            GameObject.Find("BackButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.MainMenu, 5f, "main menu");
            yield return TestScenes.WaitForTransition();
        }
    }
}
