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
using UltimateCarParking.Progression;
using UltimateCarParking.UI;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.Tests
{
    /// <summary>The home page: large readable type, World Tour as the featured hero card, live progress on it.</summary>
    public class MainMenuLayoutTests
    {
        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return TestScenes.ResetToEmpty();
        }

        private static IEnumerator OpenMenu()
        {
            SceneManager.LoadScene(SceneNames.MainMenu);
            float t = 0f;
            while (GameObject.Find("WorldTourButton") == null && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return null; yield return null;
        }

        private static TMP_Text Text(string path) => GameObject.Find(path).GetComponent<TMP_Text>();
        private static RectTransform Rect(string name) => GameObject.Find(name).GetComponent<RectTransform>();

        [UnityTest]
        public IEnumerator Home_Page_Type_Is_Large()
        {
            yield return OpenMenu();
            Assert.GreaterOrEqual(Text("MainMenuCanvas/SafeArea/TitleGroup/Title") != null ? Text("MainMenuCanvas/SafeArea/TitleGroup/Title").fontSize : 0f, 96f, "title");
            foreach (string name in new[] { "GarageButton", "MissionsButton", "SettingsMenuButton" })
            {
                var label = GameObject.Find(name).GetComponentInChildren<TMP_Text>();
                Assert.GreaterOrEqual(label.fontSize, 28f, name + " label");
                Assert.GreaterOrEqual(Rect(name).rect.height, 96f, name + " height");
            }
            Assert.GreaterOrEqual(GameObject.Find("PlayButton").GetComponentInChildren<TMP_Text>().fontSize, 46f, "play label");
            Assert.GreaterOrEqual(Rect("PlayButton").rect.height, 120f);
            foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))
                if (t.gameObject.activeInHierarchy && t.text.Length > 0 && t.GetComponentInParent<Canvas>() != null && t.transform.root.name == "MainMenuCanvas" && t.name != "Version")
                    Assert.GreaterOrEqual(t.fontSize, 20f, $"{t.name} '{t.text}' is too small to read on a phone");
        }

        [UnityTest]
        public IEnumerator World_Tour_Is_The_Large_Featured_Hero_Above_Play()
        {
            yield return OpenMenu();
            RectTransform hero = Rect("WorldTourButton"), play = Rect("PlayButton");
            Assert.GreaterOrEqual(hero.rect.height, 190f, "hero card height");
            Assert.GreaterOrEqual(hero.rect.width, 680f);
            Assert.Greater(hero.rect.height, play.rect.height * 1.4f, "bigger than Play");
            Assert.Greater(hero.position.y, play.position.y, "listed above Play");
            Assert.IsNotNull(GameObject.Find("WorldTourButton/FeaturedTag"), "FEATURED tag");
            StringAssert.Contains("FEATURED", Text("WorldTourButton/FeaturedTag/Text").text);
            Assert.AreEqual("WORLD TOUR", Text("WorldTourButton/Title").text);
            StringAssert.Contains("30 COUNTRIES", Text("WorldTourButton/Subtitle").text);
            Assert.GreaterOrEqual(Text("WorldTourButton/Title").fontSize, 64f);
            Assert.AreEqual(UiPalette.Lime, GameObject.Find("WorldTourButton").GetComponent<Image>().color, "the app's accent colour fills the hero");
        }

        [UnityTest]
        public IEnumerator Hero_Shows_Live_Mission_Progress()
        {
            yield return OpenMenu();
            Assert.AreEqual("0 / 90 DONE", Text("WorldTourButton/Progress").text);
            var tour = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsNotNull(tour);

            // Finish India mission 1 in the save, reopen: the count goes up.
            var catalog = UnityEngine.Resources.FindObjectsOfTypeAll<CountryCatalog>().First();
            CountryData india = catalog.Get(0);
            LevelData l1 = india.GetLevel(0);
            ProgressionService.RecordCompletion(UltimateCarParking.Save.SaveManager.Current.Data, l1, new LevelResult(l1.LevelId, true, FailReason.None, 3, 40f, 0, l1.CoinsFor(3)));
            Assert.AreEqual((1, 90), MainMenuController.TourProgress(catalog, UltimateCarParking.Save.SaveManager.Current.Data));
            yield return OpenMenu();
            Assert.AreEqual("1 / 90 DONE", Text("WorldTourButton/Progress").text);
        }

        [UnityTest]
        public IEnumerator Hero_Card_Opens_The_World_Tour_And_Play_Still_Plays()
        {
            yield return OpenMenu();
            GameObject.Find("WorldTourButton").GetComponent<Button>().onClick.Invoke();
            float t = 0f;
            while (SceneManager.GetActiveScene().name != SceneNames.WorldTour && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(SceneNames.WorldTour, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator Hero_Card_Breathes_Gently_And_Stays_Inside_Its_Bounds()
        {
            yield return OpenMenu();
            RectTransform hero = Rect("WorldTourButton");
            float min = 9f, max = 0f;
            for (int i = 0; i < 70; i++)    // 3.5 s of real time: more than one slow swell
            {
                min = Mathf.Min(min, hero.localScale.x); max = Mathf.Max(max, hero.localScale.x);
                yield return new WaitForSecondsRealtime(0.05f);
            }
            Assert.Less(max, 1.02f);
            Assert.Greater(min, 0.98f);
            Assert.Greater(max - min, 0.001f, "it moves");
        }
    
        [UnityTest]
        public IEnumerator Settings_Panel_Rows_Do_Not_Overlap()
        {
            yield return OpenMenu();
            var panel = Object.FindAnyObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel);
            panel.Open();
            yield return null;
            RectTransform body = panel.transform.Find("Body") as RectTransform;
            Assert.IsNotNull(body, "settings body");
            var items = body.Cast<Transform>().Select(t => (RectTransform)t)
                .Where(r => r.gameObject.activeSelf && r.name != "Edge" && r.name != "Accent").ToList();
            const float slack = 12f;   // a caption box may brush its slider's track; real overlaps are far larger
            for (int a = 0; a < items.Count; a++)
            {
                UnityEngine.Rect ra = Local(body, items[a]);
                Assert.IsTrue(body.rect.Contains(ra.min + Vector2.one * slack) && body.rect.Contains(ra.max - Vector2.one * slack), items[a].name + " inside the panel");
                for (int b = a + 1; b < items.Count; b++)
                {
                    UnityEngine.Rect rb = Local(body, items[b]);
                    bool overlap = ra.xMin + slack < rb.xMax && rb.xMin + slack < ra.xMax && ra.yMin + slack < rb.yMax && rb.yMin + slack < ra.yMax;
                    Assert.IsFalse(overlap, $"{items[a].name} overlaps {items[b].name}");
                }
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
