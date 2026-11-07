using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Cars;
using UltimateCarParking.Environment;
using UltimateCarParking.Levels;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Tests
{
    /// <summary>The generated content: 30 levels over three themes built from reusable modules, and five cars.</summary>
    public class ContentTests
    {
        private static LevelCatalog Levels => AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
        private static CarCatalog Cars => AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");
        private static IEnumerable<LevelData> All => Enumerable.Range(0, Levels.Count).Select(i => Levels.Get(i));
        /// <summary>Levels still produced by the Phase 3 generator (mission levels use the real-prop environments).</summary>
        private static IEnumerable<LevelData> Generated => All.Where(l => !l.HasObjectives);

        [Test]
        public void Thirty_Levels_In_Three_Themes_Of_Ten()
        {
            Assert.AreEqual(30, Levels.Count);
            for (int i = 0; i < 30; i++)
            {
                LevelData l = Levels.Get(i);
                string expected = i < 10 ? "city" : i < 20 ? "garage" : "yard";
                if (l.HasObjectives) Assert.IsNotNull(l.EnvironmentPrefab, $"mission level {i + 1} environment");
                else Assert.AreEqual(expected, l.Theme.ThemeId, $"level {i + 1} theme");
                Assert.AreEqual(i + 1, l.LevelNumber);
                Assert.AreEqual($"L{i + 1:00}", l.LevelId);
            }
            Assert.AreEqual(30, All.Select(l => l.LevelId).Distinct().Count(), "unique ids");
        }

        [Test]
        public void Levels_Use_The_Reusable_Module_Prefabs()
        {
            var used = new HashSet<string>();
            foreach (LevelData l in Generated)
                foreach (PropPlacement p in l.Props)
                {
                    Assert.IsNotNull(p.prefab, $"{l.LevelId}: placement without prefab");
                    string path = AssetDatabase.GetAssetPath(p.prefab);
                    StringAssert.StartsWith("Assets/_Project/Prefabs/Modules/Mod_", path, $"{l.LevelId} uses a non-module prefab");
                    used.Add(p.prefab.name);
                }
            foreach (string needed in new[] { "Mod_Cone", "Mod_Barrier", "Mod_Pillar", "Mod_ParkedCar", "Mod_BayLine" })
                Assert.Contains(needed, used.ToList(), $"{needed} used by levels");
            foreach (string theme in new[] { "city", "garage", "yard" })
            {
                var t = AssetDatabase.LoadAssetAtPath<EnvironmentTheme>($"Assets/_Project/Environment/Themes/Theme_{theme}.asset");
                Assert.IsNotNull(t.PerimeterWallPrefab, $"{theme} wall module");
                StringAssert.StartsWith("Mod_", t.PerimeterWallPrefab.name);
            }
        }

        [Test]
        public void Difficulty_Ramps_Up()
        {
            LevelData first = Generated.First(), last = Levels.Get(29);
            Assert.Greater(first.TargetSize.x, last.TargetSize.x - 0.001f + 0.2f, "bays narrow");
            Assert.Greater(first.ParkingRulesOverride.angleTolerance, last.ParkingRulesOverride.angleTolerance, "angle tightens");
            Assert.Greater(first.CollisionLimit, last.CollisionLimit, "fewer collisions allowed");
            int propsEarly = Generated.First().Props.Count(p => p.prefab.name == "Mod_ParkedCar");
            int propsLate = Levels.Get(27).Props.Count(p => p.prefab.name == "Mod_ParkedCar");
            Assert.Greater(propsLate, propsEarly, "busier lots later");
            Assert.Greater(last.BaseCoins, first.BaseCoins, "bigger rewards later");
        }

        [Test]
        public void Special_Levels_Parallel_And_Reverse_In()
        {
            foreach (int n in new[] { 5, 10, 15, 20, 25, 30 })
                StringAssert.Contains("Parallel", Levels.Get(n - 1).DisplayName);
            foreach (int n in new[] { 8, 13, 18, 23, 27 })
            {
                LevelData l = Levels.Get(n - 1);
                StringAssert.Contains("Reverse", l.DisplayName);
                Assert.IsFalse(l.ParkingRulesOverride.allowEitherDirection);
            }
        }

        [Test]
        public void Every_Bay_Fits_Every_Car()
        {
            float widest = 0f, longest = 0f;
            for (int i = 0; i < Cars.Count; i++)
                foreach (BoxCollider b in Cars.Get(i).ModelPrefab.GetComponentsInChildren<BoxCollider>())
                {
                    widest = Mathf.Max(widest, b.size.x);
                    longest = Mathf.Max(longest, b.size.z);
                }
            foreach (LevelData l in All)
            {
                float m = l.ParkingRulesOverride?.edgeMargin ?? 0.05f;   // mission levels keep rules on the objective
                Assert.Greater(l.TargetSize.x - 2f * m, widest + 0.4f, $"{l.LevelId} width");
                Assert.Greater(l.TargetSize.y - 2f * m, longest + 0.3f, $"{l.LevelId} length");
            }
        }

        [Test]
        public void Five_Cars_With_Distinct_Ids_Paint_And_Handling()
        {
            Assert.AreEqual(5, Cars.Count);
            var cars = Enumerable.Range(0, 5).Select(i => Cars.Get(i)).ToList();
            Assert.AreEqual(5, cars.Select(c => c.CarId).Distinct().Count());
            // Real bodies keep their own paint (PaintMaterial empty); every drivable car must be a real model.
            foreach (CarData c in cars) StringAssert.StartsWith("Car_Real", c.ModelPrefab.name, $"{c.CarId} uses a real body, not a primitive box");
            Assert.AreEqual(1, cars.Count(c => c.OwnedByDefault));
            for (int i = 1; i < cars.Count; i++) Assert.Greater(cars[i].Price, cars[i - 1].Price, "prices ascend");
            foreach (CarData c in cars)
            {
                Assert.IsNotNull(c.ModelPrefab.GetComponent<VehicleController>(), c.CarId);
                Assert.IsNotNull(c.ModelPrefab.GetComponent<CarPaint>(), c.CarId);
                if (c.PaintMaterial != null) Assert.Greater(c.ModelPrefab.GetComponent<CarPaint>().RendererCount, 0, c.CarId);
                CarStats s = CarStats.From(c.Handling);
                foreach (float v in new[] { s.TopSpeed, s.Acceleration, s.Handling, s.Braking }) Assert.That(v, Is.InRange(0f, 1f));
            }
            Assert.AreEqual(5, cars.Select(c => c.ModelPrefab).Distinct().Count(), "five body styles");
            Assert.AreEqual(5, cars.Select(c => c.Handling.maxForwardSpeedKmh + c.Handling.mass).Distinct().Count(), "handling differs");
        }

        [Test]
        public void Starter_Car_Matches_The_Phase_1_Defaults()
        {
            VehicleHandling d = new VehicleHandling(), s = Cars.Default.Handling;
            Assert.AreEqual("Car_RealSedan", Cars.Default.ModelPrefab.name);
            Assert.AreEqual(d.mass, s.mass);
            Assert.AreEqual(d.motorTorque, s.motorTorque);
            Assert.AreEqual(d.maxForwardSpeedKmh, s.maxForwardSpeedKmh);
            Assert.AreEqual(d.maxSteerAngle, s.maxSteerAngle);
        }
    }
}
