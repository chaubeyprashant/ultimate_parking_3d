using NUnit.Framework;
using UnityEditor;
using UltimateCarParking.Levels;

namespace UltimateCarParking.Tests
{
    /// <summary>Checks the generated level assets (run Bootstrap Project first).</summary>
    public class LevelCatalogTests
    {
        private const string CatalogPath = "Assets/_Project/Levels/LevelCatalog.asset";

        [Test]
        public void Catalog_Orders_Levels_And_Finds_Next()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            Assert.IsNotNull(catalog);
            Assert.GreaterOrEqual(catalog.Count, 2);
            LevelData first = catalog.Get(0), second = catalog.Get(1);
            Assert.AreSame(second, catalog.Next(first));
            Assert.IsNull(catalog.Next(catalog.Get(catalog.Count - 1)));
            Assert.AreSame(first, catalog.FindById(first.LevelId));
            Assert.IsNull(catalog.Get(-1));
            Assert.IsNull(catalog.Get(99));
        }

        [Test]
        public void Levels_Are_Fully_Configured()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            for (int i = 0; i < catalog.Count; i++)
            {
                LevelData l = catalog.Get(i);
                Assert.IsNotNull(l, $"level {i}");
                Assert.IsNotEmpty(l.LevelId);
                Assert.IsTrue(l.HasEnvironment, $"{l.LevelId} environment");
                Assert.Greater(l.TargetSize.x, 2f);
                Assert.Greater(l.TargetSize.y, 4.5f);
                Assert.LessOrEqual(l.Stars.ThreeStarTime, l.Stars.TwoStarTime);
                if (l.HasTimeLimit) Assert.Less(l.Stars.TwoStarTime, l.TimeLimitSeconds, $"{l.LevelId}: two-star time must be reachable");
                Assert.AreEqual(l.BaseCoins + 3 * l.CoinsPerStar, l.CoinsFor(3));
                Assert.AreEqual(0, l.CoinsFor(0));
            }
        }
    }
}
