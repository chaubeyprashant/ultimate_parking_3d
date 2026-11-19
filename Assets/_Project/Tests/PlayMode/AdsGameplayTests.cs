using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Ads;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Save;
using UltimateCarParking.UI;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Ad flows in the real Gameplay scene with a fake provider: the rewarded "+30 seconds" continue, "double coins",
    /// interstitials only at the break after a win, and nothing for brand-new players.
    /// </summary>
    public class AdsGameplayTests
    {
        private FakeAdProvider fake;

        private static LevelCatalog TestCatalog()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/Test/TestCatalog.asset");
#else
            return null;
#endif
        }

        private LevelManager Manager => LevelManager.Instance;
        private VehicleController Car => LevelLoader.Instance.Vehicle;
        private static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

        private void Seed(int completedLevels)
        {
            TestScenes.UseTemporarySave();
            for (int i = 0; i < completedLevels; i++)
                SaveManager.Current.Data.levels.Add(new LevelProgress { levelId = "done" + i, bestStars = 3 });
            fake = new FakeAdProvider();
            AdService.ResetForTests();
            AdService.Register(fake);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            AdService.ResetForTests();
            yield return TestScenes.ResetToEmpty();
        }

        private static IEnumerator WaitFor(System.Func<bool> c, float timeout, string what)
        {
            float t = 0f;
            while (!c()) { t += Time.unscaledDeltaTime; if (t > timeout) Assert.Fail("timed out: " + what); yield return null; }
        }

        private IEnumerator Load(LevelData level = null)
        {
            LevelSelection.Request(level ?? TestCatalog().Get(0), level == null ? TestCatalog() : null);
            SceneManager.LoadScene(SceneNames.Gameplay);
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 6f, "level playing");
            Car.Input.KeyboardEnabled = false;
            yield return null;
        }

        private static LevelData ShortTime(float seconds)
        {
            LevelData copy = Object.Instantiate(TestCatalog().Get(0));
#if UNITY_EDITOR
            var so = new SerializedObject(copy);
            so.FindProperty("timeLimitSeconds").floatValue = seconds;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
            return copy;
        }

        private static UnityEngine.UI.Button Button(string path) => GameObject.Find(path).GetComponent<UnityEngine.UI.Button>();

        [UnityTest]
        public IEnumerator Time_Up_Offers_One_Rewarded_Continue_That_Adds_Thirty_Seconds()
        {
            Seed(5);
            yield return Load(ShortTime(1.5f));
            var ui = Find<GameplayUiController>();
            yield return WaitFor(() => ui.FailShown, 6f, "fail panel");
            GameObject continueGo = GameObject.Find("FailPanel/Body/Continue");
            Assert.IsNotNull(continueGo);
            Assert.IsTrue(continueGo.activeSelf, "continue offered after a time-up");

            Button("FailPanel/Body/Continue").onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, fake.RewardedShown);
            Assert.AreEqual(LevelState.Playing, Manager.State, "playing again");
            Assert.IsFalse(ui.FailShown, "fail panel closed");
            Assert.IsTrue(Manager.ContinueUsed);
            Assert.Greater(Manager.RemainingSeconds, 28f);
            Assert.IsTrue(Car.ControlsEnabled, "car is drivable again");

            // Time runs out a second time: no second continue.
            Manager.FailLevel(FailReason.TimeUp);
            yield return WaitFor(() => ui.FailShown, 6f, "second fail panel");
            Assert.IsFalse(GameObject.Find("FailPanel/Body/Continue") != null && GameObject.Find("FailPanel/Body/Continue").activeSelf, "only once per attempt");
        }

        [UnityTest]
        public IEnumerator Closing_The_Ad_Early_Does_Not_Continue()
        {
            Seed(5);
            fake.RewardedCompletes = false;
            yield return Load(ShortTime(1.5f));
            var ui = Find<GameplayUiController>();
            yield return WaitFor(() => ui.FailShown, 6f, "fail panel");
            Button("FailPanel/Body/Continue").onClick.Invoke();
            yield return null;
            Assert.AreEqual(LevelState.Failed, Manager.State, "still failed");
            Assert.IsTrue(ui.FailShown);
            Assert.IsFalse(Manager.ContinueUsed);
        }

        [UnityTest]
        public IEnumerator New_Players_Are_Offered_No_Continue()
        {
            Seed(0);
            yield return Load(ShortTime(1.5f));
            var ui = Find<GameplayUiController>();
            yield return WaitFor(() => ui.FailShown, 6f, "fail panel");
            GameObject go = GameObject.Find("FailPanel/Body/Continue");
            Assert.IsTrue(go == null || !go.activeSelf, "no ads in the first levels");
        }

        [UnityTest]
        public IEnumerator Only_Time_Up_Gets_A_Continue_Not_A_Collision_Fail()
        {
            Seed(5);
            yield return Load();
            var ui = Find<GameplayUiController>();
            Manager.FailLevel(FailReason.TooManyCollisions);
            yield return WaitFor(() => ui.FailShown, 6f, "fail panel");
            GameObject go = GameObject.Find("FailPanel/Body/Continue");
            Assert.IsTrue(go == null || !go.activeSelf);
        }

        [UnityTest]
        public IEnumerator Double_Coins_Pays_The_Reward_A_Second_Time_Once()
        {
            Seed(5);
            yield return Load();
            var ui = Find<GameplayUiController>();
            Car.ResetVehicle(Manager.Level.TargetPosition + Vector3.up * 0.05f, Manager.Level.TargetRotation);
            yield return WaitFor(() => ui.CompleteShown, 6f, "complete panel");
            yield return new WaitForSecondsRealtime(2.2f);
            GameObject go = GameObject.Find("CompletePanel/Body/DoubleCoins");
            Assert.IsNotNull(go);
            Assert.IsTrue(go.activeSelf, "offered after the coins rolled up");
            int before = SaveManager.Current.Data.coins;
            int reward = Manager.Level.CoinsFor(3);

            Button("CompletePanel/Body/DoubleCoins").onClick.Invoke();
            yield return null;
            Assert.AreEqual(before + reward, SaveManager.Current.Data.coins, "second payout");
            Assert.IsFalse(go.activeSelf, "button gone, cannot be claimed twice");
        }

        [UnityTest]
        public IEnumerator Retry_After_A_Fail_Never_Shows_An_Interstitial()
        {
            Seed(6);
            SaveManager.Current.Data.ads.levelsSinceInterstitial = 5;
            yield return Load(ShortTime(1.5f));
            var ui = Find<GameplayUiController>();
            yield return WaitFor(() => ui.FailShown, 6f, "fail panel");
            Button("FailPanel/Body/Retry").onClick.Invoke();
            yield return WaitFor(() => Manager != null && Manager.State == LevelState.Playing && Manager.ElapsedSeconds < 1f, 6f, "restarted");
            Assert.AreEqual(0, fake.InterstitialsShown, "no ad on retry after a fail");
            yield return WaitFor(() => Find<GameplayUiController>().FailShown, 6f, "fails again");
            Button("FailPanel/Body/Levels").onClick.Invoke();
            Assert.AreEqual(0, fake.InterstitialsShown, "no ad when leaving after a fail");
        }

        [UnityTest]
        public IEnumerator Interstitial_Plays_Only_At_The_Break_After_A_Win()
        {
            Seed(6);
            SaveManager.Current.Data.ads.levelsSinceInterstitial = 5;
            yield return Load();
            var ui = Find<GameplayUiController>();
            Car.ResetVehicle(Manager.Level.TargetPosition + Vector3.up * 0.05f, Manager.Level.TargetRotation);
            yield return WaitFor(() => ui.CompleteShown, 6f, "complete panel");
            yield return new WaitForSecondsRealtime(0.5f);
            Button("CompletePanel/Body/Retry").onClick.Invoke();
            Assert.AreEqual(0, fake.InterstitialsShown, "no ad on retry after a win");
            yield return WaitFor(() => Manager != null && Manager.State == LevelState.Playing && Manager.ElapsedSeconds < 1f, 6f, "restarted");
            ui = Find<GameplayUiController>();
            Car.ResetVehicle(Manager.Level.TargetPosition + Vector3.up * 0.05f, Manager.Level.TargetRotation);
            yield return WaitFor(() => ui.CompleteShown, 6f, "complete panel 2");
            yield return new WaitForSecondsRealtime(0.5f);
            SaveManager.Current.Data.ads.levelsSinceInterstitial = 5;
            Button("CompletePanel/Body/Levels").onClick.Invoke();
            Assert.AreEqual(1, fake.InterstitialsShown, "one interstitial at the break after the win");
        }
    }
}
