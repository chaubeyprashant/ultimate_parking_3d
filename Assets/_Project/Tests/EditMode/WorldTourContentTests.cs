using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Levels;
using UltimateCarParking.Save;
using UltimateCarParking.UI;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.Tests
{
    /// <summary>World Tour data: the 30 requested countries in order, artwork, missions, map pins and unlock rules.</summary>
    public class WorldTourContentTests
    {
        private static readonly string[] RequestedOrder =
        {
            "India", "Japan", "China", "South Korea", "UAE", "Thailand", "Singapore", "Malaysia", "Indonesia", "Saudi Arabia",
            "United Kingdom", "France", "Germany", "Italy", "Spain", "Russia", "Netherlands", "Switzerland", "Greece", "USA",
            "Canada", "Mexico", "Brazil", "Argentina", "Nigeria", "South Africa", "Egypt", "Australia", "New Zealand", "Turkey",
        };

        private static CountryCatalog Tour => AssetDatabase.LoadAssetAtPath<CountryCatalog>("Assets/_Project/WorldTour/CountryCatalog.asset");
        private static LevelCatalog Campaign => AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
        private static WorldMapData Map => WorldMapData.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/ThirdParty/NaturalEarth/world_map.json"));

        [Test]
        public void Thirty_Requested_Countries_In_Order_With_Unique_Ids()
        {
            Assert.AreEqual(30, Tour.Count);
            CollectionAssert.AreEqual(RequestedOrder, Enumerable.Range(0, 30).Select(i => Tour.Get(i).DisplayName).ToArray());
            Assert.AreEqual(30, Enumerable.Range(0, 30).Select(i => Tour.Get(i).CountryId).Distinct().Count());
        }

        [Test]
        public void Every_Country_Has_Flag_Preview_Text_And_Its_Campaign_Level()
        {
            Assert.AreSame(Campaign, Tour.Campaign);
            for (int i = 0; i < Tour.Count; i++)
            {
                CountryData c = Tour.Get(i);
                Assert.IsNotNull(c.Flag, c.CountryId);
                Assert.AreEqual(4f / 3f, c.Flag.rect.width / c.Flag.rect.height, 0.01f, $"{c.CountryId} flag is 4:3");
                Assert.IsNotNull(c.Preview, c.CountryId + " preview");
                Assert.AreEqual(16f / 9f, c.Preview.rect.width / c.Preview.rect.height, 0.01f, $"{c.CountryId} preview is 16:9");
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.ArenaName), c.CountryId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.EnvironmentLabel), c.CountryId);
                Assert.Greater(c.Description.Length, 30, c.CountryId + " description");
                if (c.MissionCatalog != null)
                {
                    // Countries with a dedicated arena play its missions, in catalog order.
                    Assert.GreaterOrEqual(c.MissionCount, 3, c.CountryId + " arena missions");
                    Assert.AreEqual(c.MissionCatalog.Count, c.MissionCount);
                    for (int m = 0; m < c.MissionCount; m++)
                    {
                        Assert.AreSame(c.MissionCatalog.Get(m), c.GetLevel(m));
                        Assert.IsTrue(c.GetLevel(m).HasObjectives, $"{c.CountryId} mission {m + 1} objectives");
                        Assert.AreEqual(c.RoadDirection, c.GetLevel(m).RoadDirection, $"{c.CountryId} mission {m + 1} road direction");
                        Assert.AreSame(c, Tour.CountryOf(c.GetLevel(m)));
                    }
                }
                else
                {
                    Assert.AreEqual(1, c.MissionCount, c.CountryId);
                    Assert.AreSame(Campaign.Get(i), c.GetLevel(0), $"{c.CountryId} plays campaign level {i + 1}");
                    Assert.AreSame(c, Tour.CountryOf(Campaign.Get(i)));
                }
            }
        }

        [Test]
        public void Map_Mesh_Is_Valid_And_Has_The_Tour_Countries()
        {
            WorldMapData map = Map;
            Assert.AreEqual("miller", map.projection);
            Assert.AreEqual(WorldMapProjection.LatMin, map.latMin);
            Assert.AreEqual(WorldMapProjection.LatMax, map.latMax);
            Assert.Greater(map.land.VertexCount, 4000);
            Assert.AreEqual(0, map.land.t.Length % 3);
            Assert.IsTrue(map.land.t.All(i => i >= 0 && i < map.land.VertexCount), "land indices in range");
            for (int i = 0; i < map.land.v.Length; i++) Assert.That(map.land.v[i], Is.InRange(0f, 1f));
            // Singapore is too small for the 1:110m data set; every other tour country has a shape.
            for (int i = 0; i < Tour.Count; i++)
                if (Tour.Get(i).CountryId != "sg") Assert.IsNotNull(map.Country(Tour.Get(i).CountryId), Tour.Get(i).CountryId);
        }

        [Test]
        public void Every_Pin_Sits_Inside_Its_Country()
        {
            WorldMapData map = Map;
            for (int i = 0; i < Tour.Count; i++)
            {
                CountryData c = Tour.Get(i);
                WorldMapData.MeshPart shape = map.Country(c.CountryId);
                if (shape == null) continue;   // Singapore
                Assert.IsTrue(shape.Contains(WorldMapProjection.ToNormalized(c.LatLon)), $"{c.DisplayName} pin {c.LatLon} lies inside {c.CountryId}");
            }
        }

        [Test]
        public void Every_Pin_Is_Inside_Its_Region_Chip_View()
        {
            for (int i = 0; i < Tour.Count; i++)
            {
                CountryData c = Tour.Get(i);
                Rect r = WorldTourController.RegionRect(WorldTourController.ChipOf(c.Region));
                Assert.IsTrue(r.Contains(WorldMapProjection.ToNormalized(c.LatLon)), $"{c.DisplayName} visible in its region view");
            }
        }

        [Test]
        public void Unlocks_Follow_The_Campaign()
        {
            var save = new SaveData();
            Assert.AreEqual(CountryState.Unlocked, WorldTourService.StateOf(save, Tour, Tour.Get(0)));
            for (int i = 1; i < Tour.Count; i++) Assert.AreEqual(CountryState.Locked, WorldTourService.StateOf(save, Tour, Tour.Get(i)));
            Assert.AreEqual(0, WorldTourService.CurrentIndex(save, Tour));

            CountryData first = Tour.Get(0);
            for (int m = 0; m < first.MissionCount; m++)
            {
                Assert.IsTrue(CountryProgressManager.IsMissionUnlocked(save, first, m), $"mission {m + 1} open once the previous is done");
                if (m + 1 < first.MissionCount) Assert.IsFalse(CountryProgressManager.IsMissionUnlocked(save, first, m + 1), $"mission {m + 2} locked until mission {m + 1}");
                LevelData l = first.GetLevel(m);
                Progression.ProgressionService.RecordCompletion(save, l, new LevelResult(l.LevelId, true, FailReason.None, 2, 30f, 0, 0));
                if (m < first.MissionCount - 1) Assert.AreEqual(CountryState.Locked, WorldTourService.StateOf(save, Tour, Tour.Get(1)), "next country waits for every mission");
            }
            Assert.AreEqual(CountryState.Completed, WorldTourService.StateOf(save, Tour, Tour.Get(0)));
            Assert.AreEqual(CountryState.Unlocked, WorldTourService.StateOf(save, Tour, Tour.Get(1)));
            Assert.AreEqual(CountryState.Locked, WorldTourService.StateOf(save, Tour, Tour.Get(2)));
            Assert.AreEqual(1, WorldTourService.CurrentIndex(save, Tour));
            Assert.AreEqual(1, WorldTourService.CompletedCountries(save, Tour));
            Assert.AreEqual(2 * first.MissionCount, WorldTourService.Stars(save, Tour.Get(0)));
            Assert.AreSame(Tour.Get(1).GetLevel(0), WorldTourService.NextMission(save, Tour.Get(1)));
        }

        [Test]
        public void Projection_Matches_The_Map_Data()
        {
            Vector2 sw = WorldMapProjection.ToNormalized(WorldMapProjection.LatMin, -180f);
            Vector2 ne = WorldMapProjection.ToNormalized(WorldMapProjection.LatMax, 180f);
            Assert.AreEqual(Vector2.zero, sw);
            Assert.AreEqual(1f, ne.x, 1e-5f);
            Assert.AreEqual(1f, ne.y, 1e-5f);
            Assert.AreEqual(0.5f, WorldMapProjection.ToNormalized(0f, 0f).x, 1e-5f);
            Assert.That(WorldMapProjection.Aspect, Is.InRange(2.0f, 2.3f));
        }
    }
}
