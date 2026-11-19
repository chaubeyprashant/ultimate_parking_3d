using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Career;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;
using UltimateCarParking.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>Phase 6: the career screen, chapter rewards on screen, play / replay from the career, and the academy lessons played for real.</summary>
    public class CareerScreenTests
    {
        private static CareerCatalog Career()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<CareerCatalog>("Assets/_Project/Levels/Career/CareerCatalog.asset");
#else
            return null;
#endif
        }

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() { yield return TestScenes.ResetToEmpty(); }

        private static IEnumerator WaitFor(System.Func<bool> c, float timeout, string what)
        {
            float t = 0f;
            while (!c()) { t += Time.unscaledDeltaTime; if (t > timeout) Assert.Fail("timed out: " + what); yield return null; }
        }

        [UnityTest]
        public IEnumerator Career_Screen_Shows_Chapters_Locks_And_Pays_An_Earned_Chapter_Reward_Once()
        {
            SaveData s = SaveManager.Current.Data;
            CareerChapter ch1 = Career().Chapter(0);
            for (int i = 0; i < ch1.Count; i++) { var p = s.GetOrAddLevel(ch1.Get(i).LevelId); p.bestStars = 2; p.completions = 1; p.bestTimeSeconds = 40f; }
            s.coins = 100;
            SaveManager.Current.Save();

            SceneManager.LoadScene(SceneNames.Career);
            yield return null; yield return null;
            var ctrl = Object.FindAnyObjectByType<CareerController>();
            Assert.IsNotNull(ctrl);
            StringAssert.Contains("BEGINNER ACADEMY COMPLETE", ctrl.RewardText, "reward pop-up for the chapter an old save already finished");
            Assert.AreEqual(100 + ch1.completionCoins, SaveManager.Current.Data.coins);

            ctrl.SelectChapter(0);
            Assert.AreEqual(8, ctrl.MissionCardCount);
            ctrl.SelectMission(0);
            Assert.AreEqual("REPLAY", ctrl.PlayText, "completed missions can be replayed");
            StringAssert.Contains("BEST TIME", ctrl.DetailBestText);
            ctrl.SelectChapter(2);
            Assert.AreEqual("LOCKED", ctrl.PlayText, "chapter 3 is locked until chapter 2 progresses");
            ctrl.SelectChapter(-1);
            Assert.AreEqual(7, ctrl.MissionCardCount, "academy lessons");

            SceneManager.LoadScene(SceneNames.Career);   // reopen: nothing paid twice
            yield return null; yield return null;
            Assert.AreEqual(100 + ch1.completionCoins, SaveManager.Current.Data.coins);
        }

        [UnityTest]
        public IEnumerator Playing_From_The_Career_Returns_To_The_Career_And_Next_Stays_In_The_Chapter()
        {
            SceneManager.LoadScene(SceneNames.Career);
            yield return null; yield return null;
            var ctrl = Object.FindAnyObjectByType<CareerController>();
            ctrl.SelectChapter(0);
            ctrl.SelectMission(0);
            ctrl.Play();
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 10f, "mission");
            Assert.AreEqual(SceneNames.Career, LevelSelection.ReturnScene);
            Assert.AreEqual(Career().Chapter(0).Get(0), LevelManager.Instance.Level);
            Assert.AreSame(Career().Chapter(0).missions, LevelLoader.Instance.Catalog, "NEXT plays the chapter's next mission");
        }

        // ---------------------------------------------------------------- academy lessons, played

        private static IEnumerator LoadLesson(int i)
        {
            LevelCatalog a = Career().Academy;
            LevelSelection.Request(a.Get(i), a);
            SceneManager.LoadScene(SceneNames.Gameplay);
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 10f, "lesson");
            LevelLoader.Instance.Vehicle.Input.KeyboardEnabled = false;
            yield return new WaitForSeconds(0.3f);
        }

        private static Vehicle.VehicleInput In => LevelLoader.Instance.Vehicle.Input;

        [UnityTest]
        public IEnumerator Academy_Steering_Pedals_And_Camera_Lessons_Complete_By_Doing_Them()
        {
            yield return LoadLesson(0);   // A01 steering
            StringAssert.Contains("LEFT", LevelManager.Instance.Mission.Hint);
            In.SetSteering(-1f); yield return new WaitForSeconds(0.3f);
            StringAssert.Contains("RIGHT", LevelManager.Instance.Mission.Hint);
            In.SetSteering(1f);
            yield return WaitFor(() => LevelManager.Instance.State == LevelState.Completed, 4f, "steering lesson");
            In.SetSteering(0f);

            yield return LoadLesson(1);   // A02 gas and brake
            In.SetThrottle(1f);
            yield return WaitFor(() => LevelManager.Instance.Mission.CurrentIndex == 1, 8f, "reach 12 km/h");
            In.SetThrottle(0f); In.SetBrake(1f);
            yield return WaitFor(() => LevelManager.Instance.State == LevelState.Completed, 8f, "brake to a stop");
            In.SetBrake(0f);

            yield return LoadLesson(6);   // A07 cameras
            var hud = Object.FindAnyObjectByType<GameplayHud>();
            hud.ToggleView(); yield return null; hud.ToggleView(); yield return null;
            yield return WaitFor(() => LevelManager.Instance.State == LevelState.Completed, 3f, "camera lesson");
            Assert.Greater(ProgressionService.BestStars(SaveManager.Current.Data, Career().Academy.Get(6)), 0, "lesson recorded");
        }

        [UnityTest]
        public IEnumerator Academy_Reverse_And_Sensor_Lessons_Complete_By_Doing_Them()
        {
            yield return LoadLesson(2);   // A03 gears and reverse
            In.SetThrottle(1f);
            yield return WaitFor(() => LevelManager.Instance.Mission.CurrentIndex == 1, 6f, "drive forward");
            In.SetThrottle(0f); In.SetBrake(1f);
            yield return WaitFor(() => LevelManager.Instance.Mission.CurrentIndex == 2, 6f, "stop");
            In.SetBrake(0f); In.SetThrottle(-1f);
            yield return WaitFor(() => LevelManager.Instance.State == LevelState.Completed, 8f, "back up 3 m");
            In.SetThrottle(0f);

            yield return LoadLesson(5);   // A06 parking sensors: reverse toward the wall, stop under 1 m
            var lesson = (TutorialObjective)LevelManager.Instance.Mission.Current;
            Assert.AreEqual(TutorialAction.BackUpToObstacle, lesson.Action);
            In.SetThrottle(-0.5f);
            yield return WaitFor(() => LevelManager.Instance.Mission.Gauge.Visible && LevelManager.Instance.Mission.Gauge.Fill > 0.7f, 15f, "sensor near the wall");
            In.SetThrottle(0f); In.SetBrake(1f);
            yield return WaitFor(() => LevelManager.Instance.State == LevelState.Completed, 6f, "sensor lesson");
            In.SetBrake(0f);
        }
    }
}
