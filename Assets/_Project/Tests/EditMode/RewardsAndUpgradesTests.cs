using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Cars;
using UltimateCarParking.Levels;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.Tests
{
    /// <summary>Country completion rewards, car upgrades and the v3 → v4 save migration.</summary>
    public class RewardsAndUpgradesTests
    {
        private static CountryData India =>
            AssetDatabase.LoadAssetAtPath<CountryCatalog>("Assets/_Project/WorldTour/CountryCatalog.asset").FindById("in");
        private static CarData Car(string id) => AssetDatabase.LoadAssetAtPath<CarData>($"Assets/_Project/Cars/Car_{id}.asset");

        private static void Complete(SaveData save, LevelData level, int stars)
        {
            var r = new LevelResult(level.LevelId, true, FailReason.None, stars, 30f, 0, level.CoinsFor(stars));
            ProgressionService.RecordCompletion(save, level, r);
        }

        [Test]
        public void Country_Bonus_Is_Paid_Once_On_Completion_And_Once_For_All_Stars()
        {
            CountryData c = India;
            Assert.IsNotNull(c);
            Assert.AreEqual(3, c.MissionCount);
            var save = new SaveData();
            Complete(save, c.GetLevel(0), 3);
            Complete(save, c.GetLevel(1), 2);
            Assert.IsFalse(CountryProgressManager.ClaimRewards(save, c).Any, "nothing before every mission is done");
            Assert.AreEqual(67, CountryProgressManager.CompletionPercent(save, c));

            Complete(save, c.GetLevel(2), 3);
            int before = save.coins;
            CountryReward r = CountryProgressManager.ClaimRewards(save, c);
            Assert.AreEqual(c.CompletionCoins, r.CompletionCoins);
            Assert.AreEqual(0, r.PerfectCoins, "not all three stars yet");
            Assert.AreEqual(before + c.CompletionCoins, save.coins);
            Assert.AreEqual(100, CountryProgressManager.CompletionPercent(save, c));
            Assert.IsFalse(CountryProgressManager.ClaimRewards(save, c).Any, "completion bonus is paid once");

            Complete(save, c.GetLevel(1), 3);
            r = CountryProgressManager.ClaimRewards(save, c);
            Assert.AreEqual(0, r.CompletionCoins);
            Assert.AreEqual(c.PerfectCoins, r.PerfectCoins, "all-stars bonus");
            Assert.IsFalse(CountryProgressManager.ClaimRewards(save, c).Any, "all-stars bonus is paid once");
        }

        [Test]
        public void Upgrades_Need_The_Car_And_Coins_And_Stop_At_Max()
        {
            CarData sedan = Car("Sedan"), sport = Car("Sport");
            var save = new SaveData { coins = 5000 };
            GarageService.TryBuy(save, sedan);   // may already be owned by default
            if (!GarageService.IsOwned(save, sedan)) save.ownedCarIds.Add(sedan.CarId);
            Assume.That(!sport.OwnedByDefault, "the sport car must be a paid car");
            Assert.IsFalse(CarUpgrades.TryUpgrade(save, sport, UpgradeKind.Engine), "cannot upgrade a car you do not own");

            int coins = save.coins;
            Assert.AreEqual(250, CarUpgrades.NextPrice(save, sedan, UpgradeKind.Steering));
            for (int i = 0; i < 3; i++) Assert.IsTrue(CarUpgrades.TryUpgrade(save, sedan, UpgradeKind.Steering));
            Assert.AreEqual(coins - 250 - 500 - 900, save.coins);
            Assert.AreEqual(3, CarUpgrades.Level(save, sedan, UpgradeKind.Steering));
            Assert.IsFalse(CarUpgrades.TryUpgrade(save, sedan, UpgradeKind.Steering), "max level");
            Assert.AreEqual(0, CarUpgrades.NextPrice(save, sedan, UpgradeKind.Steering));

            save.coins = 100;
            Assert.IsFalse(CarUpgrades.TryUpgrade(save, sedan, UpgradeKind.Brakes), "not enough coins");
            Assert.AreEqual(100, save.coins);
        }

        [Test]
        public void Upgraded_Handling_Improves_Only_The_Bought_Stats_And_Leaves_The_Asset_Alone()
        {
            CarData sedan = Car("Sedan");
            float torque = sedan.Handling.motorTorque, steer = sedan.Handling.maxSteerAngle, brake = sedan.Handling.brakeTorque;
            var h = CarUpgrades.Upgraded(sedan.Handling, 2, 0, 3);
            Assert.AreEqual(torque * 1.2f, h.motorTorque, 0.01f);
            Assert.AreEqual(brake, h.brakeTorque, 0.01f);
            Assert.AreEqual(Mathf.Min(45f, steer + 4.5f), h.maxSteerAngle, 0.01f, "tighter steering lock");
            Assert.AreEqual(torque, sedan.Handling.motorTorque, "asset untouched");
            Assert.Greater(CarStats.From(h).Handling, CarStats.From(sedan.Handling).Handling);
            var stock = CarUpgrades.Upgraded(new SaveData(), sedan);
            Assert.AreEqual(torque, stock.motorTorque, "stock car = its own handling");
        }

        [Test]
        public void Version_3_Save_Migrates_To_4_Keeping_Progress()
        {
            string json = "{\"version\":3,\"coins\":1234,\"selectedCarId\":\"sedan\",\"ownedCarIds\":[\"sedan\"],\"levels\":[{\"levelId\":\"IN1\",\"bestStars\":2,\"bestTimeSeconds\":40,\"completions\":1}],\"selectedCountryId\":\"in\",\"unlockedCountryIds\":[\"in\"]}";
            var data = JsonUtility.FromJson<SaveData>(json);
            SaveManager.Migrate(data);
            Assert.AreEqual(4, data.version);
            Assert.AreEqual(1234, data.coins);
            Assert.AreEqual(2, data.FindLevel("IN1").bestStars);
            Assert.IsNotNull(data.countryRewardIds);
            Assert.IsNotNull(data.carUpgrades);
            Assert.AreEqual("in", data.selectedCountryId);
        }
    
        [Test]
        public void Testing_Switch_Unlocks_Everything_Without_Writing_Unlocks_To_The_Save()
        {
            var tour = AssetDatabase.LoadAssetAtPath<CountryCatalog>("Assets/_Project/WorldTour/CountryCatalog.asset");
            var save = new SaveData();
            try
            {
                UltimateCarParking.Core.TestingUnlocks.UnlockAll = true;
                for (int i = 0; i < tour.Count; i++)
                {
                    Assert.IsTrue(CountryUnlockManager.IsUnlocked(save, tour, tour.Get(i)), tour.Get(i).DisplayName);
                    for (int m = 0; m < tour.Get(i).MissionCount; m++) Assert.IsTrue(CountryProgressManager.IsMissionUnlocked(save, tour.Get(i), m));
                }
                var campaign = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
                Assert.IsTrue(ProgressionService.IsUnlocked(save, campaign, campaign.Get(campaign.Count - 1)), "last campaign level");
                CountryUnlockManager.RecordUnlocks(save, tour);
                CollectionAssert.AreEquivalent(new[] { "in" }, save.unlockedCountryIds, "only genuinely unlocked countries are saved");
            }
            finally { UltimateCarParking.Core.TestingUnlocks.UnlockAll = false; }
            Assert.IsFalse(CountryUnlockManager.IsUnlocked(save, tour, tour.Get(1)), "locked again once the switch is off");
        }
    }
}
