using NUnit.Framework;
using UnityEditor;
using UltimateCarParking.Levels;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;

namespace UltimateCarParking.Tests
{
    public class ProgressionServiceTests
    {
        private LevelCatalog catalog;
        private SaveData data;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
            data = new SaveData();
        }

        private LevelResult Win(LevelData l, int stars, float time = 30f) => new LevelResult(l.LevelId, true, FailReason.None, stars, time, 0, l.CoinsFor(stars));

        [Test]
        public void Only_Level_1_Is_Open_On_A_Fresh_Save()
        {
            Assert.AreEqual(LevelLockState.Unlocked, ProgressionService.StateOf(data, catalog, catalog.Get(0)));
            for (int i = 1; i < catalog.Count; i++)
                Assert.AreEqual(LevelLockState.Locked, ProgressionService.StateOf(data, catalog, catalog.Get(i)), $"level {i + 1}");
            Assert.AreEqual(0, ProgressionService.FurthestUnlockedIndex(data, catalog));
        }

        [Test]
        public void Completing_A_Level_Unlocks_The_Next_Only()
        {
            ProgressionService.RecordCompletion(data, catalog.Get(0), Win(catalog.Get(0), 2));
            Assert.AreEqual(LevelLockState.Completed, ProgressionService.StateOf(data, catalog, catalog.Get(0)));
            Assert.AreEqual(LevelLockState.Unlocked, ProgressionService.StateOf(data, catalog, catalog.Get(1)));
            Assert.AreEqual(LevelLockState.Locked, ProgressionService.StateOf(data, catalog, catalog.Get(2)));
            Assert.AreEqual(1, ProgressionService.FurthestUnlockedIndex(data, catalog));
        }

        [Test]
        public void First_Clear_Pays_Full_Reward()
        {
            LevelData l = catalog.Get(0);
            LevelReward r = ProgressionService.RecordCompletion(data, l, Win(l, 2));
            Assert.IsTrue(r.FirstCompletion);
            Assert.AreEqual(l.BaseCoins + 2 * l.CoinsPerStar, r.Coins);
            Assert.AreEqual(r.Coins, data.coins);
        }

        [Test]
        public void Replays_Pay_New_Stars_Plus_A_Small_Bonus_And_Keep_Bests()
        {
            LevelData l = catalog.Get(0);
            ProgressionService.RecordCompletion(data, l, Win(l, 1, 50f));
            int afterFirst = data.coins;
            int bonus = UnityEngine.Mathf.RoundToInt(l.BaseCoins * ProgressionService.ReplayBaseFraction);

            LevelReward better = ProgressionService.RecordCompletion(data, l, Win(l, 3, 20f));
            Assert.IsFalse(better.FirstCompletion);
            Assert.IsTrue(better.NewBest);
            Assert.AreEqual(2 * l.CoinsPerStar + bonus, better.Coins);

            LevelReward worse = ProgressionService.RecordCompletion(data, l, Win(l, 1, 70f));
            Assert.AreEqual(bonus, worse.Coins, "no star coins for a worse run");
            Assert.AreEqual(afterFirst + better.Coins + worse.Coins, data.coins);
            Assert.AreEqual(3, ProgressionService.BestStars(data, l), "best stars kept");
            Assert.AreEqual(20f, ProgressionService.BestTime(data, l), 1e-4f, "best time kept");
            Assert.AreEqual(3, data.FindLevel(l.LevelId).completions);
        }

        [Test]
        public void Failures_Record_Nothing()
        {
            LevelData l = catalog.Get(0);
            var fail = new LevelResult(l.LevelId, false, FailReason.TimeUp, 0, 90f, 0, 0);
            LevelReward r = ProgressionService.RecordCompletion(data, l, fail);
            Assert.AreEqual(0, r.Coins);
            Assert.AreEqual(0, data.coins);
            Assert.IsNull(data.FindLevel(l.LevelId));
        }

        [Test]
        public void Star_Totals_Per_Range()
        {
            ProgressionService.RecordCompletion(data, catalog.Get(0), Win(catalog.Get(0), 3));
            ProgressionService.RecordCompletion(data, catalog.Get(1), Win(catalog.Get(1), 2));
            ProgressionService.RecordCompletion(data, catalog.Get(10), Win(catalog.Get(10), 1));
            Assert.AreEqual(6, ProgressionService.TotalStars(data, catalog));
            Assert.AreEqual(5, ProgressionService.TotalStars(data, catalog, 0, 10));
            Assert.AreEqual(1, ProgressionService.TotalStars(data, catalog, 10, 20));
        }
    }
}
