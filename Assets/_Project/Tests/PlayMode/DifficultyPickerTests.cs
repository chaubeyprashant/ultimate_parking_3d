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
    /// <summary>EASY / MEDIUM / HARD can be picked on every screen that starts a mission, and all pickers share one setting.</summary>
    public class DifficultyPickerTests
    {
        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SettingsService.SetPlayDifficulty(1);
            yield return TestScenes.ResetToEmpty();
        }

        private static IEnumerator Load(string scene)
        {
            SceneManager.LoadScene(scene);
            float t = 0f;
            while (Object.FindAnyObjectByType<DifficultyPicker>() == null && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return null; yield return null;
        }

        private static Rect World(RectTransform r)
        {
            var c = new Vector3[4]; r.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        [UnityTest]
        public IEnumerator Main_Menu_Missions_And_World_Tour_Each_Have_A_Working_Picker()
        {
            foreach (string scene in new[] { SceneNames.MainMenu, SceneNames.LevelSelect, SceneNames.WorldTour })
            {
                yield return Load(scene);
                var picker = Object.FindAnyObjectByType<DifficultyPicker>();
                Assert.IsNotNull(picker, scene + ": picker");
                Assert.AreEqual(3, picker.ButtonCount, scene);
                Button[] b = picker.GetComponentsInChildren<Button>();
                CollectionAssert.AreEqual(new[] { "EASY", "MEDIUM", "HARD" }, b.Select(x => x.GetComponentInChildren<TMP_Text>().text).ToArray(), scene);
                b[2].onClick.Invoke();
                Assert.AreEqual(Difficulty.Hard, PlayDifficulty.Current, scene + ": HARD picked");
                Assert.AreEqual(UiPalette.Lime, b[2].GetComponent<Image>().color, scene + ": HARD highlighted");
                b[0].onClick.Invoke();
                Assert.AreEqual(Difficulty.Easy, PlayDifficulty.Current, scene + ": EASY picked");
                Assert.AreNotEqual(UiPalette.Lime, b[2].GetComponent<Image>().color, scene + ": HARD no longer highlighted");
            }
        }

        [UnityTest]
        public IEnumerator The_Choice_Carries_From_The_Main_Menu_To_The_Next_Screen()
        {
            yield return Load(SceneNames.MainMenu);
            Object.FindAnyObjectByType<DifficultyPicker>().Pick(Difficulty.Hard);
            yield return Load(SceneNames.LevelSelect);
            Button hard = Object.FindAnyObjectByType<DifficultyPicker>().GetComponentsInChildren<Button>()[2];
            Assert.AreEqual(UiPalette.Lime, hard.GetComponent<Image>().color, "missions screen shows HARD");
        }

        [UnityTest]
        public IEnumerator Main_Menu_Picker_Does_Not_Cover_The_Car_Name_Or_The_World_Tour_Card()
        {
            yield return Load(SceneNames.MainMenu);
            Rect picker = World((RectTransform)Object.FindAnyObjectByType<DifficultyPicker>().transform);
            foreach (string name in new[] { "CarLabel", "VehicleKicker", "WorldTourButton" })
                Assert.IsFalse(picker.Overlaps(World(GameObject.Find(name).GetComponent<RectTransform>())), "picker overlaps " + name);
        }
    
        [UnityTest]
        public IEnumerator World_Tour_Picker_Sits_Clear_Of_The_Card_Contents()
        {
            yield return Load(SceneNames.WorldTour);
            Rect picker = World((RectTransform)Object.FindAnyObjectByType<DifficultyPicker>().transform);
            foreach (string name in new[] { "Preview", "Description", "Mission1", "MissionsStat", "PlayButton" })
            {
                GameObject go = GameObject.Find(name);
                if (go == null || !go.activeInHierarchy) continue;
                Assert.IsFalse(picker.Overlaps(World(go.GetComponent<RectTransform>())), "picker overlaps " + name);
            }
        }
    }
}
