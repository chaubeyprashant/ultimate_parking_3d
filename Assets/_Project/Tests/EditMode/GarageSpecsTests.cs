using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UltimateCarParking.Cars;

namespace UltimateCarParking.Tests
{
    /// <summary>Phase 4 garage spec sheets: every car carries measured figures, a size, a difficulty and a strength.</summary>
    public class GarageSpecsTests
    {
        private static CarCatalog Catalog => AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");

        [Test]
        public void Every_Garage_Car_Has_A_Measured_Spec_Sheet()
        {
            for (int i = 0; i < Catalog.Count; i++)
            {
                CarData c = Catalog.Get(i);
                CarSpecs s = c.Specs;
                Assert.IsTrue(s.Measured, c.CarId + " measured (Documentation/vehicle_specs.csv)");
                Assert.That(s.zeroTo20, Is.InRange(0.8f, 3f), c.CarId + " 0-20");
                Assert.That(s.stopFrom40, Is.InRange(3f, 12f), c.CarId + " stop from 40");
                Assert.That(s.turnRadius, Is.InRange(3f, 8f), c.CarId + " turn radius");
                Assert.That(s.length, Is.InRange(2.5f, 5.6f), c.CarId + " length");
                Assert.That(s.width, Is.InRange(1.2f, 2.2f), c.CarId + " width");
                Assert.That(s.parkingDifficulty, Is.InRange(1, 5), c.CarId);
                Assert.IsNotEmpty(s.strength, c.CarId + " strength");
            }
        }

        [Test]
        public void Difficulty_Rises_With_Size_And_Turning_Circle()
        {
            Assert.Less(CarSpecs.Difficulty(3.8f, 1.7f, 3.6f), CarSpecs.Difficulty(5.3f, 2.05f, 5.5f));
            Assert.AreEqual(1, CarSpecs.Difficulty(3.5f, 1.6f, 3.4f));
            Assert.AreEqual(5, CarSpecs.Difficulty(5.6f, 2.2f, 6.5f));
            Assert.AreEqual("EASY", CarSpecs.DifficultyName(1));
            Assert.AreEqual("EXPERT", CarSpecs.DifficultyName(5));
            CarData hatch = Catalog.FindById("hatch"), monster = Catalog.FindById("monster_mammoth");
            Assert.LessOrEqual(hatch.Specs.parkingDifficulty, monster.Specs.parkingDifficulty, "a hatchback is no harder to park than a monster truck");
        }
    }
}
