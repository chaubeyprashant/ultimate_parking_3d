using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.EditorTools;
using UltimateCarParking.Levels;
using UltimateCarParking.Parking;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Tests
{
    /// <summary>Easy / Medium / Hard: rule changes, rewards ×0.75 / ×1 / ×1.5, stars kept per difficulty, touch vs collision.</summary>
    public class DifficultyTests
    {
        private static LevelCatalog Campaign => AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath);

        [TearDown]
        public void TearDown() => PlayDifficulty.Override = null;

        private static LevelData LimitedLevel()
        {
            for (int i = 0; i < Campaign.Count; i++)
                if (Campaign.Get(i).HasTimeLimit && Campaign.Get(i).HasCollisionLimit && Campaign.Get(i).CollisionLimit >= 1) return Campaign.Get(i);
            Assert.Fail("no level with a time and hit limit");
            return null;
        }

        [Test]
        public void Medium_Is_The_Mission_As_Designed()
        {
            LevelData l = LimitedLevel();
            PlayDifficulty.Override = Difficulty.Medium;
            float t = l.TimeLimitSeconds; int hits = l.CollisionLimit; int coins = l.CoinsFor(3);
            PlayDifficulty.Override = null;   // edit mode without override = as designed
            Assert.AreEqual(l.TimeLimitSeconds, t, 1e-4f);
            Assert.AreEqual(l.CollisionLimit, hits);
            Assert.AreEqual(l.CoinsFor(3), coins);
            Assert.AreEqual(new Vector2(2.6f, 5.4f), PlayDifficulty.Bay(new Vector2(2.6f, 5.4f), Difficulty.Medium));
        }

        [Test]
        public void Easy_Is_Roomier_With_No_Collisions_And_Pays_Less()
        {
            LevelData l = LimitedLevel();
            float t = l.TimeLimitSeconds; int coins = l.CoinsFor(3);
            PlayDifficulty.Override = Difficulty.Easy;
            Assert.Greater(l.TimeLimitSeconds, t);
            Assert.IsFalse(l.HasCollisionLimit, "Easy: no collision limit");
            Assert.IsTrue(CollisionHandler.Forgiving, "Easy: contacts never count");
            Assert.AreEqual(Mathf.RoundToInt(coins * 0.75f), l.CoinsFor(3), 2);
            Vector2 bay = PlayDifficulty.Bay(new Vector2(2.6f, 5.4f), Difficulty.Easy);
            Assert.Greater(bay.x, 2.6f); Assert.Greater(bay.y, 5.4f);
            Assert.Greater(PlayDifficulty.AngleFactor(Difficulty.Easy), 1f);
        }

        [Test]
        public void Hard_Is_Tighter_Without_Assistance_And_Pays_More_But_Every_Car_Still_Fits()
        {
            LevelData l = LimitedLevel();
            float t = l.TimeLimitSeconds; int hits = l.CollisionLimit; int coins = l.CoinsFor(3);
            PlayDifficulty.Override = Difficulty.Hard;
            Assert.Less(l.TimeLimitSeconds, t);
            Assert.AreEqual(Mathf.Max(0, hits - 1), l.CollisionLimit);
            Assert.IsFalse(CollisionHandler.Forgiving);
            Assert.AreEqual(Mathf.RoundToInt(coins * 1.5f), l.CoinsFor(3), 2);
            Assert.AreEqual(AssistLevel.Expert, PlayDifficulty.AssistCap(Difficulty.Hard));
            // Biggest garage car 1.93 × 5.2 m: a Hard bay never drops below it plus clearance.
            foreach (Vector2 designed in new[] { new Vector2(2.4f, 5.5f), new Vector2(2.6f, 5.6f), new Vector2(3.2f, 6.5f) })
            {
                Vector2 b = PlayDifficulty.Bay(designed, Difficulty.Hard);
                Assert.LessOrEqual(b.x, designed.x); Assert.LessOrEqual(b.y, designed.y);
                Assert.GreaterOrEqual(b.x, Mathf.Min(designed.x, 2.33f)); Assert.GreaterOrEqual(b.y, Mathf.Min(designed.y, 5.5f));
            }
        }

        [Test]
        public void Stars_Are_Kept_Per_Difficulty_And_Each_Difficulty_Pays_Its_First_Clear()
        {
            LevelData l = Campaign.Get(0);
            var s = new SaveData { coins = 0 };
            var r = new LevelResult(l.LevelId, true, FailReason.None, 3, 20f, 0, 0);

            PlayDifficulty.Override = Difficulty.Easy;
            ProgressionService.RecordCompletion(s, l, r);
            int easyPay = s.coins;
            Assert.AreEqual(l.CoinsFor(3), easyPay);
            Assert.AreEqual(3, s.FindLevel(l.LevelId).easyStars);
            Assert.AreEqual(0, s.FindLevel(l.LevelId).bestStars, "Medium untouched");
            Assert.AreEqual(3, ProgressionService.BestStars(s, l), "unlocks use the best of any difficulty");

            PlayDifficulty.Override = Difficulty.Hard;
            ProgressionService.RecordCompletion(s, l, r);
            Assert.AreEqual(easyPay + l.CoinsFor(3), s.coins, "Hard first clear pays the Hard reward");
            Assert.Greater(l.CoinsFor(3), easyPay, "Hard pays more than Easy");
            Assert.AreEqual(3, ProgressionService.StarsAt(s, l, Difficulty.Hard));
            Assert.AreEqual(20f, s.FindLevel(l.LevelId).TimeAt(Difficulty.Hard), 1e-4f);
            Assert.AreEqual(0, ProgressionService.StarsAt(s, l, Difficulty.Medium));
        }

        [Test]
        public void Old_Saves_Keep_Their_Stars_As_Medium()
        {
            string path = System.IO.Path.Combine(Application.temporaryCachePath, $"diff_mig_{System.Guid.NewGuid():N}.json");
            try
            {
                System.IO.File.WriteAllText(path, "{\"version\":6,\"coins\":100,\"levels\":[{\"levelId\":\"L01\",\"bestStars\":2,\"bestTimeSeconds\":31.5,\"completions\":1}]}");
                SaveData d = new SaveManager(path).Data;
                Assert.AreEqual(SaveData.CurrentVersion, d.version);
                LevelProgress p = d.FindLevel("L01");
                Assert.AreEqual(2, p.StarsAt(Difficulty.Medium));
                Assert.AreEqual(0, p.StarsAt(Difficulty.Easy));
                Assert.AreEqual(0, p.StarsAt(Difficulty.Hard));
                Assert.Less(p.TimeAt(Difficulty.Hard), 0f, "no Hard time yet");
            }
            finally { foreach (string f in new[] { path, path + ".bak" }) if (System.IO.File.Exists(f)) System.IO.File.Delete(f); }
        }

        [Test]
        public void A_Mission_Cleared_Only_On_Easy_Or_Hard_Counts_As_Completed()
        {
            var s = new SaveData();
            s.GetOrAddLevel("E").easyStars = 1;
            s.GetOrAddLevel("H").hardStars = 2;
            s.GetOrAddLevel("N");
            Assert.IsTrue(s.FindLevel("E").Completed);
            Assert.IsTrue(s.FindLevel("H").Completed);
            Assert.IsFalse(s.FindLevel("N").Completed);
            Assert.AreEqual(2, UltimateCarParking.Ads.AdPolicy.CompletedLevels(s));
        }

        [Test]
        public void A_Light_Touch_Is_Not_A_Collision()
        {
            Assert.GreaterOrEqual(CollisionHandler.TouchSpeed, 1f, "touches up to ~4 km/h never count");
            Assert.Greater(CollisionHandler.HardImpactSpeed, CollisionHandler.TouchSpeed);
        }
    }
}
