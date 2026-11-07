using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Cars;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
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
        public IEnumerator Garage_Upgrade_Persists_And_Changes_The_Spawned_Car()
        {
            SaveManager.Current.Data.coins = 1000;
            SaveManager.Current.Save();
            yield return Open(SceneNames.Garage);
            var garage = GarageManager.Instance;
            var ui = Object.FindAnyObjectByType<GarageController>();
            CarData sedan = garage.SelectedCar;
            Assert.AreEqual("sedan", sedan.CarId);
            StringAssert.Contains("LV 0/3", ui.UpgradeText((int)UpgradeKind.Steering));
            StringAssert.Contains("250", ui.UpgradeText((int)UpgradeKind.Steering));
            ui.OnUpgrade(UpgradeKind.Steering);
            ui.OnUpgrade(UpgradeKind.Steering);
            yield return null;
            Assert.AreEqual(250, garage.Coins, "250 + 500 spent");
            StringAssert.Contains("LV 2/3", ui.UpgradeText((int)UpgradeKind.Steering));
            StringAssert.Contains("900", ui.UpgradeText((int)UpgradeKind.Steering));
            ui.OnUpgrade(UpgradeKind.Steering);   // 900: cannot afford
            Assert.AreEqual(250, garage.Coins);

            SaveManager restarted = SaveManager.Initialize(savePath);
            Assert.AreEqual(2, restarted.Data.FindUpgrades("sedan").steering, "upgrade saved");
            LevelSelection.Request(Levels().Get(0), Levels());
            yield return Open(SceneNames.Gameplay);
            yield return WaitFor(() => LevelLoader.Instance != null && LevelLoader.Instance.Vehicle != null, 5f, "car spawned");
            float lockAngle = LevelLoader.Instance.Vehicle.MaxSteerAngle;
            Debug.Log($"[Test] Upgrade: steering x2 -> spawned lock {lockAngle:F1} deg (stock {sedan.Handling.maxSteerAngle:F1})");
            Assert.AreEqual(Mathf.Min(45f, sedan.Handling.maxSteerAngle + 3f), lockAngle, 0.01f, "upgraded steering lock on the spawned car");
        }

        [UnityTest]
        public IEnumerator Finishing_A_Country_Pays_The_Bonus_And_Shows_It()
        {
#if UNITY_EDITOR
            var tour = AssetDatabase.LoadAssetAtPath<UltimateCarParking.WorldTour.CountryCatalog>("Assets/_Project/WorldTour/CountryCatalog.asset");
            UltimateCarParking.WorldTour.CountryData india = tour.FindById("in");
            SaveData data = SaveManager.Current.Data;
            foreach (int i in new[] { 0, 1 })
            {
                LevelData m = india.GetLevel(i);
                ProgressionService.RecordCompletion(data, m, new LevelResult(m.LevelId, true, FailReason.None, 3, 20f, 0, 0));
            }
            SaveManager.Current.Save();
            int coinsBefore = data.coins;
            LevelData last = india.GetLevel(2);
            LevelSelection.Country = india;
            LevelSelection.Request(last, india.MissionCatalog);
            yield return Open(SceneNames.Gameplay);
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 10f, "IN3 playing");
            var runner = LevelManager.Instance.Mission;
            var car = LevelLoader.Instance.Vehicle;
            while (LevelManager.Instance.State == LevelState.Playing && runner.Current != null)
            {
                int idx = runner.CurrentIndex;
                MissionObjective o = runner.Current;
                Quaternion rot = o is ParkObjective p ? p.ZoneRotation : car.transform.rotation;
                car.ResetVehicle(o.TargetPoint + Vector3.up * 0.06f, rot);
                yield return WaitFor(() => LevelManager.Instance.State != LevelState.Playing || runner.CurrentIndex != idx, 8f, o.Title);
            }
            Assert.AreEqual(LevelState.Completed, LevelManager.Instance.State);
            LevelResult r = LevelManager.Instance.LastResult.Value;
            StringAssert.StartsWith("ACCURACY", r.ScoreLabel, "IN3 is precision-scored");
            var recorder = Object.FindAnyObjectByType<ProgressionRecorder>();
            Assert.IsTrue(recorder.LastCountryReward.HasValue, "country bonus paid");
            Assert.AreEqual(india.CompletionCoins, recorder.LastCountryReward.Value.CompletionCoins);
            yield return new WaitForSecondsRealtime(2f);
            var badge = GameObject.Find("Badge")?.GetComponent<TMPro.TMP_Text>();
            Assert.IsNotNull(badge);
            Debug.Log($"[Test] Country bonus: {r} | badge '{badge.text}' | coins {coinsBefore} -> {SaveManager.Current.Data.coins}");
            StringAssert.Contains("INDIA", badge.text);
            Assert.AreEqual(coinsBefore + recorder.LastReward.Value.Coins + recorder.LastCountryReward.Value.Coins, SaveManager.Initialize(savePath).Data.coins, "saved");
            yield return TestScenes.WaitForTransition();
#else
            yield break;
#endif
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
            if (hatch.PaintMaterial != null) Assert.AreSame(hatch.PaintMaterial, spawned.GetComponent<CarPaint>().CurrentPaint);
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
            Assert.IsTrue(labels.Any(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"\b1\s*/\s*30\s+LEVELS")), "progress shown");
            yield return new WaitForSecondsRealtime(1f);   // coin counter rolls
            Assert.IsTrue(Object.FindObjectsByType<TMPro.TMP_Text>().Any(t => t.text == "777"), "coins shown");
            Assert.IsNotNull(Object.FindAnyObjectByType<CarShowroom>().CurrentPreview, "selected car on show");

            // Settings: the grid button opens the same panel as the gear.
            var settings = Object.FindAnyObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            GameObject.Find("SettingsMenuButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(settings.gameObject.activeInHierarchy && settings.IsOpen, "settings opened from the menu grid");
            settings.Close();
            yield return new WaitForSecondsRealtime(0.4f);

            // Missions = the level list.
            GameObject.Find("MissionsButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.LevelSelect, 5f, "level select");
            yield return TestScenes.WaitForTransition();
            GameObject.Find("BackButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.MainMenu, 5f, "back to menu");
            yield return TestScenes.WaitForTransition();

            GameObject.Find("WorldTourButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.WorldTour, 5f, "world tour");
            yield return TestScenes.WaitForTransition();
            GameObject.Find("BackButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.MainMenu, 5f, "back to menu from tour");
            yield return TestScenes.WaitForTransition();

            GameObject.Find("GarageButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Garage, 5f, "garage");
            yield return TestScenes.WaitForTransition();
            GameObject.Find("BackButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.MainMenu, 5f, "back to menu from garage");
            yield return TestScenes.WaitForTransition();

            // Play continues with the next level to beat (level 2 after clearing level 1) and exits back to the menu.
            Assert.AreEqual(Levels().Get(1), Object.FindAnyObjectByType<MainMenuController>().NextLevel());
            GameObject.Find("PlayButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.Gameplay, 5f, "gameplay");
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.Level != null, 5f, "level loaded");
            Assert.AreEqual(Levels().Get(1).LevelId, LevelManager.Instance.Level.LevelId, "Play starts the next level");
            Assert.AreEqual(SceneNames.MainMenu, LevelSelection.ReturnScene);
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
