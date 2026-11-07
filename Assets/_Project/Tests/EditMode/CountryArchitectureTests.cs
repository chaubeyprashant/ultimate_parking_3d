using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Levels;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Country arena architecture: data shape, lazy arena references, unlock rules, progress, selection persistence,
    /// save migration, weather presets and road directions.
    /// </summary>
    public class CountryArchitectureTests
    {
        private const string TestCatalogPath = "Assets/_Project/WorldTour/Test/TestCountryCatalog.asset";
        private const string TourPath = "Assets/_Project/WorldTour/CountryCatalog.asset";
        private string savePath;

        private static CountryCatalog TestTour => AssetDatabase.LoadAssetAtPath<CountryCatalog>(TestCatalogPath);
        private static CountryCatalog Tour => AssetDatabase.LoadAssetAtPath<CountryCatalog>(TourPath);
        private static CountryData A => TestTour.FindById("ta");
        private static CountryData B => TestTour.FindById("tb");

        [SetUp]
        public void SetUp()
        {
            savePath = Path.Combine(Application.temporaryCachePath, $"ucp_country_{System.Guid.NewGuid():N}.json");
            SaveManager.Initialize(savePath);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string f in new[] { savePath, savePath + ".bak" }) if (File.Exists(f)) File.Delete(f);
        }

        private static void Complete(SaveData save, LevelData level, int stars) =>
            ProgressionService.RecordCompletion(save, level, new LevelResult(level.LevelId, true, FailReason.None, stars, 20f, 0, 0));

        [Test]
        public void Two_Temporary_Countries_Are_Fully_Configured()
        {
            Assert.AreEqual(2, TestTour.Count);
            foreach (CountryData c in new[] { A, B })
            {
                Assert.IsFalse(string.IsNullOrEmpty(c.CountryId));
                Assert.IsFalse(string.IsNullOrEmpty(c.DisplayName));
                Assert.IsNotNull(c.Flag, c.CountryId + " flag");
                Assert.IsNotNull(c.Preview, c.CountryId + " preview");
                Assert.IsTrue(c.Arena.IsSet, c.CountryId + " arena");
                Assert.Greater(c.MissionCount, 0, c.CountryId + " missions");
                Assert.IsNotNull(c.Unlock);
                Assert.IsNotNull(c.Traffic);
                Assert.IsNotNull(c.Weather);
            }
            Assert.AreEqual(ArenaSource.ResourcesPrefab, A.Arena.source);
            Assert.IsNotNull(Resources.Load<GameObject>(A.Arena.key), "A's arena prefab resolves from Resources");
            Assert.AreEqual(ArenaSource.Scene, B.Arena.source);
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && Path.GetFileNameWithoutExtension(s.path) == B.Arena.key), "B's arena scene is in Build Settings");
            Assert.AreEqual(RoadDirection.LeftHand, A.RoadDirection);
            Assert.AreEqual(RoadDirection.RightHand, B.RoadDirection);
            Assert.AreEqual(WeatherPreset.Overcast, A.Weather.preset);
            Assert.AreEqual(WeatherPreset.Rain, B.Weather.preset);
            Assert.IsTrue(B.Traffic.movingTraffic);
            Assert.IsNull(CountryArenaLoader.Validate(A));
            Assert.IsNull(CountryArenaLoader.Validate(B));
        }

        [Test]
        public void Country_Data_Never_References_An_Arena_Directly()
        {
            // Loading the country list must not load arenas: no arena prefab / scene among any country's dependencies.
            string arenaPrefab = AssetDatabase.GetAssetPath(Resources.Load<GameObject>(A.Arena.key));
            var countryPaths = new[] { AssetDatabase.GetAssetPath(A), AssetDatabase.GetAssetPath(B) }
                .Concat(Enumerable.Range(0, Tour.Count).Select(i => AssetDatabase.GetAssetPath(Tour.Get(i))));
            foreach (string path in countryPaths)
            {
                string[] deps = AssetDatabase.GetDependencies(path, true);
                Assert.IsFalse(deps.Contains(arenaPrefab), $"{path} pulls in {arenaPrefab}");
                Assert.IsFalse(deps.Any(d => d.EndsWith(".unity")), $"{path} references a scene");
            }
        }

        [Test]
        public void Unlock_Requirements_And_Reasons()
        {
            SaveData save = SaveManager.Current.Data;
            Assert.IsTrue(CountryUnlockManager.IsUnlocked(save, TestTour, A), "A is open by default");
            UnlockCheck b = CountryUnlockManager.Evaluate(save, TestTour, B);
            Assert.IsFalse(b.Unlocked);
            Assert.AreEqual("FINISH TEST COUNTRY A TO UNLOCK", b.Reason);

            Complete(save, A.GetLevel(0), 1);
            b = CountryUnlockManager.Evaluate(save, TestTour, B);
            Assert.IsFalse(b.Unlocked, "A done but only 1 of 2 required stars");
            Assert.AreEqual("EARN 1 MORE STARS TO UNLOCK", b.Reason);

            Complete(save, A.GetLevel(0), 3);
            Assert.IsTrue(CountryUnlockManager.IsUnlocked(save, TestTour, B));
        }

        [Test]
        public void Unlocks_Are_Sticky_Once_Recorded()
        {
            SaveData save = SaveManager.Current.Data;
            var opened = CountryUnlockManager.RecordUnlocks(save, TestTour);
            CollectionAssert.AreEqual(new[] { "ta" }, opened.Select(c => c.CountryId).ToArray());
            Assert.AreEqual(0, CountryUnlockManager.RecordUnlocks(save, TestTour).Count, "nothing new the second time");

            save.unlockedCountryIds.Add("tb");   // e.g. opened under older, looser rules
            Assert.IsTrue(CountryUnlockManager.IsUnlocked(save, TestTour, B), "a recorded unlock stays open");
        }

        [Test]
        public void Tour_Chain_Unlocks_Match_The_Campaign()
        {
            SaveData save = SaveManager.Current.Data;
            LevelCatalog campaign = Tour.Campaign;
            // Countries after an arena country (India → Japan) wait for that country's own missions; from the
            // third stop on, the chain follows the campaign levels exactly.
            for (int step = 1; step < 4; step++)
            {
                for (int i = 2; i < Tour.Count; i++)
                    if (Tour.Get(i).MissionCatalog == null && Tour.Get(i - 1).MissionCatalog == null)
                    Assert.AreEqual(ProgressionService.IsUnlocked(save, campaign, campaign.Get(i)),
                        CountryUnlockManager.IsUnlocked(save, Tour, Tour.Get(i)), $"step {step}, country {i + 1}");
                Complete(save, campaign.Get(step), 2);
            }
            Assert.AreEqual("FINISH THAILAND TO UNLOCK", CountryUnlockManager.Evaluate(save, Tour, Tour.Get(6)).Reason);
        }

        [Test]
        public void Progress_Comes_From_Existing_Level_Records()
        {
            SaveData save = SaveManager.Current.Data;
            Assert.AreEqual(0, CountryProgressManager.CompletedMissions(save, A));
            Assert.AreEqual(0f, CountryProgressManager.CompletionFraction(save, A));
            Complete(save, A.GetLevel(0), 2);
            Assert.AreEqual(1, CountryProgressManager.CompletedMissions(save, A));
            Assert.AreEqual(2, CountryProgressManager.Stars(save, A));
            Assert.AreEqual(3, CountryProgressManager.MaxStars(A));
            Assert.IsTrue(CountryProgressManager.IsCompleted(save, A));
            Assert.AreEqual(1, CountryProgressManager.CompletedCountries(save, TestTour));
            Assert.AreEqual(2, CountryProgressManager.TourStars(save, TestTour));
            Assert.AreEqual(2, save.FindLevel(A.GetLevel(0).LevelId).bestStars, "stored in the ordinary level record");
        }

        [Test]
        public void Selection_Persists_And_Rejects_Locked_Countries()
        {
            Assert.IsFalse(CountrySelectionManager.Select(TestTour, B), "locked B cannot be selected");
            Assert.AreEqual("", CountrySelectionManager.SelectedId);
            Assert.AreSame(A, CountrySelectionManager.SelectedOrCurrent(TestTour), "falls back to the current stop");

            CountryData changed = null;
            void OnChanged(CountryData c) => changed = c;
            CountrySelectionManager.SelectionChanged += OnChanged;
            try { Assert.IsTrue(CountrySelectionManager.Select(TestTour, A)); }
            finally { CountrySelectionManager.SelectionChanged -= OnChanged; }
            Assert.AreSame(A, changed);
            Assert.AreSame(A, CountrySelectionManager.Selected(TestTour));

            var reloaded = new SaveManager(savePath);   // a restart
            Assert.AreEqual("ta", reloaded.Data.selectedCountryId);
        }

        [Test]
        public void Version_2_Save_Migrates_To_3_Keeping_Progress()
        {
            File.WriteAllText(savePath, "{\"version\":2,\"coins\":120,\"levels\":[{\"levelId\":\"L01\",\"bestStars\":3,\"bestTimeSeconds\":30,\"completions\":1},{\"levelId\":\"L02\",\"bestStars\":2,\"bestTimeSeconds\":40,\"completions\":1},{\"levelId\":\"L15\",\"bestStars\":1,\"bestTimeSeconds\":50,\"completions\":1}]}");
            var save = new SaveManager(savePath);
            Assert.AreEqual(SaveData.CurrentVersion, save.Data.version, "migrated to the current version");
            Assert.AreEqual(120, save.Data.coins);
            Assert.AreEqual(3, save.Data.FindLevel("L01").bestStars);
            Assert.AreEqual("", save.Data.selectedCountryId);
            Assert.IsNotNull(save.Data.unlockedCountryIds);
            // Every country now has its own arena missions, so old campaign records no longer unlock anything:
            // Russia (index 15) stays locked by an old L25 record until the previous country is completed in its arena.
            Assert.AreEqual("ru", Tour.Get(15).CountryId);
            Assert.IsFalse(CountryUnlockManager.IsUnlocked(save.Data, Tour, Tour.Get(15)), "old campaign records do not unlock arena countries");
        }

        [Test]
        public void Weather_Presets_Apply_Lighting_And_Fog()
        {
            WeatherSettings rain = WeatherSettings.ForPreset(WeatherPreset.Rain);
            Assert.IsTrue(rain.fog);
            Assert.Greater(rain.roadWetness, 0.5f);
            Assert.Greater(rain.precipitation, 0f);
            Assert.Less(WeatherSettings.ForPreset(WeatherPreset.Night).sunIntensity, 0.3f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            try
            {
                rain.Apply(sun);
                Assert.IsTrue(RenderSettings.fog);
                Assert.AreEqual(rain.fogDensity, RenderSettings.fogDensity, 1e-5f);
                Assert.AreEqual(rain.sunIntensity, sun.intensity, 1e-5f);
                Assert.AreEqual(rain.ambientSky, RenderSettings.ambientSkyColor);
                WeatherSettings.ForPreset(WeatherPreset.Clear).Apply(null);   // null sun is allowed
                Assert.IsFalse(RenderSettings.fog);
            }
            finally { Object.DestroyImmediate(sun.gameObject); }
        }

        [Test]
        public void Tour_Road_Directions_Are_Correct()
        {
            var left = new[] { "in", "jp", "th", "sg", "my", "id", "gb", "za", "au", "nz" };
            for (int i = 0; i < Tour.Count; i++)
            {
                CountryData c = Tour.Get(i);
                Assert.AreEqual(left.Contains(c.CountryId), c.DrivesOnLeft, c.DisplayName);
                Assert.IsFalse(c.Arena.IsSet, $"{c.DisplayName}: no dedicated arena yet");
                Assert.AreEqual(i == 0, c.Unlock.unlockedByDefault, c.DisplayName);
            }
        }

        [Test]
        public void Validate_Reports_Missing_Arenas()
        {
            Assert.AreEqual("No country selected.", CountryArenaLoader.Validate(null));
            Assert.That(CountryArenaLoader.Validate(Tour.Get(0)), Does.Contain("no arena configured"));
            var ghost = ScriptableObject.CreateInstance<CountryData>();
            try
            {
                ghost.Arena.source = ArenaSource.Scene;
                ghost.Arena.key = "Arena_DoesNotExist";
                Assert.That(CountryArenaLoader.Validate(ghost), Does.Contain("not in Build Settings"));
            }
            finally { Object.DestroyImmediate(ghost); }
        }
    }
}
