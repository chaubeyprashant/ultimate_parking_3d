using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.UI;

namespace UltimateCarParking.Tests
{
    /// <summary>Country arenas must be released when the level scene is left; the World Tour map prefetches the selected country.</summary>
    public class ArenaMemoryTests
    {
        [SetUp]
        public void SetUp()
        {
            TestScenes.UseTemporarySave();
            TestingUnlocks.UnlockAll = true;   // the test save starts with only India open; these tests visit other countries
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return TestScenes.ResetToEmpty();
        }

        private static IEnumerator WaitFor(System.Func<bool> c, float timeout, string what)
        {
            float t = 0f;
            while (!c()) { t += Time.unscaledDeltaTime; if (t > timeout) Assert.Fail("timed out: " + what); yield return null; }
        }

        private static IEnumerator OpenWorldTour()
        {
            SceneManager.LoadScene(SceneNames.WorldTour);
            yield return WaitFor(() => Object.FindAnyObjectByType<WorldTourController>() != null, 8f, "world tour");
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_Country_Arena_Handle_Is_Held_While_Playing_And_Released_On_Leaving()
        {
            yield return OpenWorldTour();
            var tour = Object.FindAnyObjectByType<WorldTourController>();
            Assert.IsTrue(tour.PlayCountryMission("id", 0), "Indonesia mission 1 starts");
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 20f, "level playing");
            Assert.AreEqual(1, LevelLoader.OpenArenaHandles, "the arena is held while playing");

            SceneManager.LoadScene(SceneNames.WorldTour);
            yield return WaitFor(() => Object.FindAnyObjectByType<WorldTourController>() != null, 8f, "back on the map");
            yield return null;
            Assert.AreEqual(0, LevelLoader.OpenArenaHandles, "leaving the level frees the arena (it used to stay in memory for the whole session)");
        }

        [UnityTest]
        public IEnumerator Visiting_Several_Countries_In_A_Row_Never_Holds_More_Than_One_Arena()
        {
            string[] countries = { "id", "ae", "us", "br" };
            int maxHeld = 0;
            foreach (string c in countries)
            {
                yield return OpenWorldTour();
                var tour = Object.FindAnyObjectByType<WorldTourController>();
                Assert.IsTrue(tour.PlayCountryMission(c, 0), c);
                yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 20f, c + " playing");
                maxHeld = Mathf.Max(maxHeld, LevelLoader.OpenArenaHandles);
            }
            Assert.AreEqual(1, maxHeld, "one arena at a time across four countries");
        }

        [UnityTest]
        public IEnumerator Selecting_A_Country_On_The_Map_Starts_One_Prefetch_Per_Pack()
        {
            yield return OpenWorldTour();
            var tour = Object.FindAnyObjectByType<WorldTourController>();
            int before = ArenaPrefetcher.Started;
            int idx = -1;
            for (int i = 0; i < tour.Markers.Count; i++) if (tour.Markers[i].Country.CountryId == "br") idx = i;
            Assert.GreaterOrEqual(idx, 0);
            tour.Select(idx, focus: false);
            Assert.AreEqual(before + 1, ArenaPrefetcher.Started, "selecting a country requests its pack");
            tour.Select(idx, focus: false);
            Assert.AreEqual(before + 1, ArenaPrefetcher.Started, "the same pack is not requested twice");
        }
    }
}
