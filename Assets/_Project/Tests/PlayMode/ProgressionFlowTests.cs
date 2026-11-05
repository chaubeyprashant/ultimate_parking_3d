using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Cars;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;
using UltimateCarParking.UI;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Phase 3 acceptance through the real scenes: rewards and stars saved on completion, everything restored after
    /// a restart (a new SaveManager reading the same file), garage purchase/selection, level select states, menus.
    /// </summary>
    public class ProgressionFlowTests
    {
        private string savePath;

        [SetUp]
        public void SetUp() => savePath = TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.ResetToEmpty();

        private static LevelCatalog Levels()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
#else
            return null;
#endif
        }

        private static CarCatalog Cars()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");
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

        private static IEnumerator Open(string scene)
        {
            SceneManager.LoadScene(scene);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == scene, 5f, scene);
            yield return null;
            yield return null;
        }

        private static void Complete(SaveData data, int levelNumber, int stars)
        {
            LevelData l = Levels().Get(levelNumber - 1);
            ProgressionService.RecordCompletion(data, l, new LevelResult(l.LevelId, true, FailReason.None, stars, 20f, 0, 0));
        }

        [UnityTest]
        public IEnumerator Clearing_A_Level_Saves_Stars_And_Coins_That_Survive_A_Restart()
        {
            LevelCatalog catalog = Levels();
            LevelData l1 = catalog.Get(0);
            LevelSelection.Request(l1, catalog);
            yield return Open(SceneNames.Gameplay);
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 5f, "L01 playing");
            VehicleController car = LevelLoader.Instance.Vehicle;
            car.ResetVehicle(l1.TargetPosition + Vector3.up * 0.05f, l1.TargetRotation);
            yield return WaitFor(() => LevelManager.Instance.State != LevelState.Playing, 8f, "L01 ends");
            LevelResult result = LevelManager.Instance.LastResult.Value;
            Assert.AreEqual(3, result.Stars);
            int expectedCoins = l1.CoinsFor(3);

            // "Restart the app": throw away the in-memory save and read the file again.
            SaveManager restarted = SaveManager.Initialize(savePath);
            Debug.Log($"[Test] After clearing L01 and restarting: coins {restarted.Data.coins}, L01 stars {ProgressionService.BestStars(restarted.Data, l1)}, L02 {ProgressionService.StateOf(restarted.Data, catalog, catalog.Get(1))}");
            Assert.AreEqual(expectedCoins, restarted.Data.coins);
            Assert.AreEqual(3, ProgressionService.BestStars(restarted.Data, l1));
            Assert.AreEqual(LevelLockState.Unlocked, ProgressionService.StateOf(restarted.Data, catalog, catalog.Get(1)), "next level unlocked");
            Assert.AreEqual(LevelLockState.Locked, ProgressionService.StateOf(restarted.Data, catalog, catalog.Get(2)));
        }

        [UnityTest]
        public IEnumerator Garage_Buy_And_Select_Persist_And_Drive_The_New_Car()
        {
            SaveManager.Current.Data.coins = 1000;
            SaveManager.Current.Save();
            yield return Open(SceneNames.Garage);
            var garage = GarageManager.Instance;
            var ui = Object.FindAnyObjectByType<GarageController>();
            Assert.IsNotNull(garage);
            Assert.AreEqual("sedan", garage.SelectedCar.CarId);
            Assert.AreEqual("SELECTED", ui.ActionText);

            ui.Step(1);   // → Volt Hatch, 750
            yield return null;
            CarData hatch = ui.Shown;
            Assert.AreEqual("hatch", hatch.CarId);
            StringAssert.StartsWith("BUY", ui.ActionText);
            Assert.AreEqual(PurchaseResult.Purchased, garage.Buy(hatch));
            Assert.IsTrue(garage.Select(hatch));
            yield return null;
            Assert.AreEqual("SELECTED", ui.ActionText);
            Assert.AreEqual(250, garage.Coins);

            ui.Step(1);   // → Apex GT, 2000: unaffordable
            yield return null;
            Assert.AreEqual(PurchaseResult.NotEnoughCoins, garage.Buy(ui.Shown));
            Assert.AreEqual(250, garage.Coins);

            SaveManager restarted = SaveManager.Initialize(savePath);
            Assert.AreEqual(250, restarted.Data.coins);
            Assert.AreEqual("hatch", restarted.Data.selectedCarId);
            CollectionAssert.Contains(restarted.Data.ownedCarIds, "hatch");
            CollectionAssert.DoesNotContain(restarted.Data.ownedCarIds, "sport");

            LevelSelection.Request(Levels().Get(0), Levels());
            yield return Open(SceneNames.Gameplay);
            yield return WaitFor(() => LevelLoader.Instance != null && LevelLoader.Instance.Vehicle != null, 5f, "car spawned");
            GameObject spawned = LevelLoader.Instance.Vehicle.gameObject;
            Debug.Log($"[Test] Garage: bought + selected {hatch.DisplayName}, 250 coins left; after restart Gameplay spawned '{spawned.name}' ({LevelLoader.Instance.Vehicle.Body.mass} kg)");
            Assert.AreEqual(hatch.ModelPrefab.name, spawned.name);
            Assert.AreEqual(hatch.Handling.mass, LevelLoader.Instance.Vehicle.Body.mass, 0.01f);
            Assert.AreSame(hatch.PaintMaterial, spawned.GetComponent<CarPaint>().CurrentPaint);
        }

        [UnityTest]
        public IEnumerator Level_Select_Shows_Locked_Unlocked_And_Completed()
        {
            SaveData data = SaveManager.Current.Data;
            Complete(data, 1, 3);
            Complete(data, 2, 2);
            Complete(data, 3, 1);
            SaveManager.Current.Save();

            yield return Open(SceneNames.LevelSelect);
            var ui = Object.FindAnyObjectByType<LevelSelectController>();
            Assert.AreEqual(3, ui.SectionCount, "three theme tabs");
            Assert.AreEqual(0, ui.CurrentTab, "opens on the tab with the furthest unlocked level");
            var cards = ui.Cards.Where(c => c.gameObject.activeSelf).ToList();
            Assert.AreEqual(10, cards.Count);
            Assert.AreEqual(LevelLockState.Completed, cards[0].State);
            Assert.AreEqual(3, cards[0].Stars);
            Assert.AreEqual(2, cards[1].Stars);
            Assert.AreEqual(LevelLockState.Completed, cards[2].State);
            Assert.AreEqual(LevelLockState.Unlocked, cards[3].State);
            for (int i = 4; i < 10; i++) Assert.AreEqual(LevelLockState.Locked, cards[i].State, $"card {i + 1}");

            ui.ShowTab(1);
            yield return null;
            Assert.IsTrue(ui.Cards.Where(c => c.gameObject.activeSelf).All(c => c.State == LevelLockState.Locked), "garage levels locked");
            Assert.AreEqual(11, ui.Cards[0].Level.LevelNumber);

            ui.ShowTab(0);
            yield return null;
            ui.Cards[6].Button.onClick.Invoke();   // locked: shakes, stays
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(SceneNames.LevelSelect, SceneManager.GetActiveScene().name, "locked level does not start");

            ui.Cards[3].Button.onClick.Invoke();   // level 4: unlocked
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.Level != null, 6f, "gameplay opens");
            Assert.AreEqual("L04", LevelManager.Instance.Level.LevelId);
            yield return TestScenes.WaitForTransition();
        }

        [UnityTest]
        public IEnumerator Main_Menu_Buttons_Navigate_And_Show_Progress()
        {
            Complete(SaveManager.Current.Data, 1, 3);
            SaveManager.Current.Data.coins = 777;
            SaveManager.Current.Save();
            yield return Open(SceneNames.MainMenu);
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsNotNull(menu);
            var labels = Object.FindObjectsByType<TMPro.TMP_Text>().Select(t => t.text).ToList();
            Assert.IsTrue(labels.Any(t => t.Contains("1/30 LEVELS")), "progress shown");
            yield return new WaitForSecondsRealtime(1f);   // coin counter rolls
            Assert.IsTrue(Object.FindObjectsByType<TMPro.TMP_Text>().Any(t => t.text == "777"), "coins shown");
            Assert.IsNotNull(Object.FindAnyObjectByType<CarShowroom>().CurrentPreview, "selected car on show");

            GameObject.Find("PlayButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.LevelSelect, 5f, "level select");
            yield return TestScenes.WaitForTransition();
            GameObject.Find("BackButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.MainMenu, 5f, "back to menu");
            yield return TestScenes.WaitForTransition();
            GameObject.Find("GarageButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Garage, 5f, "garage");
            yield return TestScenes.WaitForTransition();
        }

        [UnityTest]
        public IEnumerator Duplicate_Garage_Managers_Remove_Themselves()
        {
            yield return Open(SceneNames.Garage);
            GarageManager original = GarageManager.Instance;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate"));
            var extra = new GameObject("ExtraGarage").AddComponent<GarageManager>();
            yield return null;
            Assert.AreSame(original, GarageManager.Instance);
            Assert.IsTrue(extra == null);
        }
    }
}
