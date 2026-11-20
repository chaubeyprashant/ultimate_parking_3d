using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Career;
using UltimateCarParking.EditorTools;
using UltimateCarParking.Levels;
using UltimateCarParking.Save;

namespace UltimateCarParking.Tests
{
    /// <summary>Phase 6 career: chapters cover the campaign once, unlock rules, one-off rewards, save migration, economy.</summary>
    public class CareerTests
    {
        private static CareerCatalog Career => AssetDatabase.LoadAssetAtPath<CareerCatalog>(CareerBuilder.CatalogPath);
        private static LevelCatalog Campaign => AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath);

        private static void Complete(SaveData s, LevelData l, int stars)
        {
            LevelProgress p = s.GetOrAddLevel(l.LevelId);
            p.bestStars = stars; p.completions = 1; p.bestTimeSeconds = 30f;
        }

        [Test]
        public void Five_Chapters_Cover_Every_Campaign_Mission_Exactly_Once()
        {
            Assert.AreEqual(5, Career.ChapterCount);
            var seen = new List<string>();
            for (int i = 0; i < Career.ChapterCount; i++)
                for (int m = 0; m < Career.Chapter(i).Count; m++) seen.Add(Career.Chapter(i).Get(m).LevelId);
            Assert.AreEqual(Campaign.Count, seen.Count);
            Assert.AreEqual(seen.Count, seen.Distinct().Count(), "no mission twice");
            for (int i = 0; i < Campaign.Count; i++) Assert.Contains(Campaign.Get(i).LevelId, seen);
            Assert.AreEqual("BEGINNER ACADEMY", Career.Chapter(0).title);
            Assert.AreEqual("PARKING MASTER", Career.Chapter(4).title);
            Assert.AreEqual(7, Career.Academy.Count, "seven academy lessons");
            Assert.AreEqual(42, Campaign.Count, "campaign catalog itself unchanged by the career");
        }

        [Test]
        public void Chapters_Unlock_Through_Mission_Completion_And_Missions_In_Order()
        {
            var s = new SaveData();
            CareerChapter ch1 = Career.Chapter(0);
            Assert.AreEqual(ChapterState.Open, CareerService.State(s, Career, 0));
            Assert.AreEqual(ChapterState.Locked, CareerService.State(s, Career, 1));
            Assert.IsTrue(CareerService.IsMissionOpen(s, Career, 0, 0));
            Assert.IsFalse(CareerService.IsMissionOpen(s, Career, 0, 1), "missions open in order");
            for (int i = 0; i < ch1.unlockAfter + 5 && i < 5; i++) Complete(s, ch1.Get(i), 2);
            Assert.IsTrue(CareerService.IsMissionOpen(s, Career, 0, 5), "next after the last completed");
            Assert.IsTrue(CareerService.IsMissionOpen(s, Career, 0, 2), "completed missions stay open for replay");
            Assert.AreEqual(ChapterState.Locked, CareerService.State(s, Career, 1), "5 of 8 is not enough");
            Complete(s, ch1.Get(5), 1);
            Assert.AreEqual(ChapterState.Open, CareerService.State(s, Career, 1), "6 of 8 opens chapter 2");
            Assert.AreEqual(ChapterState.Locked, CareerService.State(s, Career, 2));
        }

        [Test]
        public void Chapter_And_Mastery_Rewards_Are_Paid_Exactly_Once()
        {
            var s = new SaveData { coins = 1000 };
            CareerChapter ch1 = Career.Chapter(0);
            for (int i = 0; i < ch1.Count; i++) Complete(s, ch1.Get(i), 2);
            List<CareerReward> paid = CareerService.ClaimRewards(s, Career);
            Assert.AreEqual(1, paid.Count);
            Assert.AreEqual(1000 + ch1.completionCoins, s.coins);
            Assert.AreEqual(ChapterState.Completed, CareerService.State(s, Career, 0));
            Assert.AreEqual(0, CareerService.ClaimRewards(s, Career).Count, "not paid twice");
            for (int i = 0; i < ch1.Count; i++) Complete(s, ch1.Get(i), 3);
            paid = CareerService.ClaimRewards(s, Career);
            Assert.AreEqual(1, paid.Count); Assert.IsTrue(paid[0].Mastery);
            Assert.AreEqual(1000 + ch1.completionCoins + ch1.masteryCoins, s.coins);
            Assert.AreEqual(ChapterState.Mastered, CareerService.State(s, Career, 0));
            StringAssert.Contains("MASTERED", paid[0].Label);
        }

        [Test]
        public void A_Version_4_Save_Migrates_With_Coins_Cars_And_Stars_Intact()
        {
            string path = Path.Combine(Application.temporaryCachePath, $"career_mig_{System.Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(path, "{\"version\":4,\"coins\":5432,\"selectedCarId\":\"suv\",\"ownedCarIds\":[\"sedan\",\"suv\"]," +
                    "\"levels\":[{\"levelId\":\"L01\",\"bestStars\":3,\"bestTimeSeconds\":31.5,\"completions\":2},{\"levelId\":\"L02\",\"bestStars\":2,\"bestTimeSeconds\":50,\"completions\":1}]," +
                    "\"countryRewardIds\":[\"in\"]}");
                SaveData d = new SaveManager(path).Data;
                Assert.AreEqual(SaveData.CurrentVersion, d.version);
                Assert.GreaterOrEqual(d.version, 5, "migrates through version 5 (and on to the current version)");
                Assert.AreEqual(5432, d.coins);
                CollectionAssert.AreEqual(new[] { "sedan", "suv" }, d.ownedCarIds);
                Assert.AreEqual("suv", d.selectedCarId);
                Assert.AreEqual(3, d.FindLevel("L01").bestStars);
                Assert.AreEqual(31.5f, d.FindLevel("L01").bestTimeSeconds, 1e-3f);
                Assert.IsNotNull(d.careerRewardIds);
                Assert.AreEqual(0f, d.FindLevel("L01").bestQuality, "no score recorded yet");
                CollectionAssert.Contains(d.countryRewardIds, "in");
                Assert.IsTrue(CareerService.IsMissionOpen(d, Career, 0, 2), "career progress comes from the old stars");
            }
            finally { foreach (string f in new[] { path, path + ".bak" }) if (File.Exists(f)) File.Delete(f); }
        }

        [Test]
        public void Rewards_Are_Balanced_Against_Car_Prices()
        {
            int chapters = 0;
            for (int i = 0; i < Career.ChapterCount; i++) chapters += Career.Chapter(i).completionCoins + Career.Chapter(i).masteryCoins;
            var cars = AssetDatabase.LoadAssetAtPath<UltimateCarParking.Cars.CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");
            int garage = 0; for (int i = 0; i < cars.Count; i++) garage += cars.Get(i).Price;
            int missions = 0; for (int i = 0; i < Campaign.Count; i++) missions += Campaign.Get(i).CoinsFor(3);
            Debug.Log($"[Career] economy: campaign 3★ {missions:N0}, chapter rewards {chapters:N0}, all cars {garage:N0}");
            Assert.Less(chapters, garage * 0.15f, "chapter rewards do not flood the economy");
            Assert.Greater(missions + chapters, Career.Chapter(1).completionCoins * 10, "playing the career pays enough to buy several cars");
            for (int i = 1; i < Career.ChapterCount; i++)
                Assert.Greater(Career.Chapter(i).completionCoins, Career.Chapter(i - 1).completionCoins, "later chapters pay more");
        }
    }
}
