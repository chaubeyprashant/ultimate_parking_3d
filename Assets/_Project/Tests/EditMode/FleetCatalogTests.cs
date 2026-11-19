using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Cars;
using UltimateCarParking.EditorTools;

namespace UltimateCarParking.Tests
{
    /// <summary>The garage lineup: 18 cars, one price ladder, only fictional names, sane handling.</summary>
    public class FleetCatalogTests
    {
        private static CarCatalog Catalog => AssetDatabase.LoadAssetAtPath<CarCatalog>(CarFactory.CarCatalogPath);
        private static List<CarData> Cars() { var l = new List<CarData>(); for (int i = 0; i < Catalog.Count; i++) l.Add(Catalog.Get(i)); return l; }

        [Test]
        public void The_Garage_Has_Every_Planned_Car_With_Unique_Ids_And_Names()
        {
            List<CarData> cars = Cars();
            Assert.AreEqual(FleetExpansion.ExpectedCatalogSize, cars.Count);
            Assert.IsFalse(cars.Any(c => c == null));
            Assert.AreEqual(cars.Count, cars.Select(c => c.CarId).Distinct().Count(), "unique ids (stored in saves)");
            Assert.AreEqual(cars.Count, cars.Select(c => c.DisplayName).Distinct().Count(), "unique names");
        }

        [Test]
        public void The_Kept_Original_Cars_Keep_Their_Ids_So_Old_Saves_Still_Work()
        {
            foreach (string id in new[] { "sedan", "hatch", "sport", "suv", "wagon" })
                Assert.IsNotNull(Catalog.FindById(id), id);
            Assert.AreEqual(0, Catalog.FindById("sedan").Price, "the starter stays free");
            Assert.AreEqual("sedan", Catalog.Default.CarId);
        }

        [Test]
        public void Every_Car_Has_A_Class_And_Only_The_Starter_Is_Free()
        {
            List<CarData> cars = Cars();
            foreach (CarData c in cars) if (FleetPricing.Classes.TryGetValue(c.CarId, out CarClass k)) Assert.AreEqual(k, c.Class, c.CarId + " asset carries its class");
            Assert.AreEqual(1, cars.Count(c => c.Class == CarClass.Starter));
            Assert.AreEqual(1, cars.Count(c => c.OwnedByDefault));
            Assert.AreEqual(1, cars.Count(c => c.Price == 0));
            Assert.AreEqual(CarClass.Starter, Catalog.Default.Class);
        }

        [Test]
        public void Prices_Run_From_Budget_Cars_To_Premium_Cars_By_Class()
        {
            List<CarData> paid = Cars().Where(c => c.Class != CarClass.Starter).ToList();
            foreach (CarClass cls in new[] { CarClass.Economy, CarClass.Standard, CarClass.Performance, CarClass.Premium, CarClass.Elite })
            {
                var band = FleetPricing.Bands[cls];
                foreach (CarData c in paid.Where(c => c.Class == cls)) Assert.That(c.Price, Is.InRange(band.min, band.max), $"{c.CarId} price inside the {cls} band");
            }
            // Every car of a higher class costs more than every car of a lower class.
            foreach (CarData a in paid) foreach (CarData b in paid)
                if (a.Class < b.Class) Assert.Less(a.Price, b.Price, $"{a.CarId} ({a.Class}) must cost less than {b.CarId} ({b.Class})");
            Assert.AreEqual(paid.Count, paid.Select(c => c.Price).Distinct().Count(), "no two cars cost the same");
            Assert.LessOrEqual(paid.Where(c => c.Class == CarClass.Economy).Max(c => c.Price), 1500);
            Assert.GreaterOrEqual(paid.Where(c => c.Class == CarClass.Elite).Max(c => c.Price), 50000, "the flagship is a long-term goal");
        }

        [Test]
        public void Inside_A_Class_A_Stronger_Car_Costs_More()
        {
            foreach (var group in Cars().Where(c => c.Class != CarClass.Starter).GroupBy(c => c.Class))
            {
                List<CarData> byScore = group.OrderBy(FleetPricing.Score).ToList();
                for (int i = 1; i < byScore.Count; i++)
                    Assert.Greater(byScore[i].Price, byScore[i - 1].Price, $"{byScore[i].CarId} scores higher than {byScore[i - 1].CarId} in {group.Key}, so it costs more");
            }
        }

        [Test]
        public void The_Garage_Is_Listed_From_Cheapest_To_Dearest()
        {
            List<CarData> cars = Cars();
            Assert.AreEqual(0, cars[0].Price);
            for (int i = 1; i < cars.Count; i++) Assert.Greater(cars[i].Price, cars[i - 1].Price, $"{cars[i].CarId} listed after a dearer car");
        }

        [Test]
        public void Pricing_Is_Deterministic_And_Spread_Evenly_Across_A_Band()
        {
            Assert.AreEqual(FleetPricing.PriceAt(CarClass.Premium, 0, 3), FleetPricing.PriceAt(CarClass.Premium, 0, 3));
            Assert.AreEqual(13000, FleetPricing.PriceAt(CarClass.Premium, 0, 3));
            Assert.AreEqual(20500, FleetPricing.PriceAt(CarClass.Premium, 1, 3));
            Assert.AreEqual(28000, FleetPricing.PriceAt(CarClass.Premium, 2, 3));
            Assert.AreEqual(0, FleetPricing.PriceAt(CarClass.Starter, 0, 1));
            int mid = FleetPricing.PriceAt(CarClass.Economy, 0, 1);
            Assert.That(mid, Is.InRange(400, 1500), "a lone car sits mid-band");
        }

        [Test]
        public void Every_Car_Has_A_Drivable_Prefab_With_Four_Wheels()
        {
            foreach (CarData c in Cars())
            {
                Assert.IsNotNull(c.ModelPrefab, c.CarId + " prefab");
                Assert.AreEqual(4, c.ModelPrefab.GetComponentsInChildren<WheelCollider>(true).Length, c.CarId + " wheel colliders");
                Assert.IsNotNull(c.ModelPrefab.GetComponent<UltimateCarParking.Vehicle.VehicleController>(), c.CarId + " controller");
                Assert.IsNotNull(c.ModelPrefab.GetComponent<Rigidbody>(), c.CarId + " body");
                Assert.AreEqual(4, c.ModelPrefab.GetComponent<UltimateCarParking.Vehicle.WheelVisuals>() != null ? 4 : 0, c.CarId + " wheel visuals");
            }
        }

        [Test]
        public void Car_Bodies_Are_Parking_Sized()
        {
            foreach (CarData c in Cars())
            {
                var bounds = default(Bounds); bool any = false;
                foreach (MeshFilter mf in c.ModelPrefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    // Real vertices, not the mesh's loose bounding box (an oddly rotated mesh inflates that).
                    foreach (Vector3 v in mf.sharedMesh.vertices)
                    {
                        Vector3 w = mf.transform.TransformPoint(v);
                        if (!any) { bounds = new Bounds(w, Vector3.zero); any = true; } else bounds.Encapsulate(w);
                    }
                }
                Assert.IsTrue(any, c.CarId);
                Assert.That(bounds.size.z, Is.InRange(2.8f, 5.6f), c.CarId + " length " + bounds.size.z);
                Assert.That(bounds.size.x, Is.InRange(1.4f, 2.4f), c.CarId + " width " + bounds.size.x);
                Assert.That(bounds.size.y, Is.InRange(0.9f, 2.2f), c.CarId + " height " + bounds.size.y);
            }
        }

        [Test]
        public void Handling_Values_Stay_In_A_Sane_Parking_Range()
        {
            foreach (CarData c in Cars())
            {
                var h = c.Handling;
                Assert.That(h.mass, Is.InRange(900f, 3000f), c.CarId + " mass");
                Assert.That(h.maxForwardSpeedKmh, Is.InRange(45f, 100f), c.CarId + " top speed");
                Assert.That(h.maxSteerAngle, Is.InRange(30f, 40f), c.CarId + " steering lock");
                Assert.LessOrEqual(h.highSpeedSteerAngle, h.maxSteerAngle, c.CarId);
                Assert.That(h.motorTorque / h.mass, Is.InRange(0.8f, 3.2f), c.CarId + " power to weight");
                Assert.That(h.brakeTorque / h.mass, Is.InRange(1.2f, 3.6f), c.CarId + " braking to weight");
                CarStats s = CarStats.From(h);
                foreach (float v in new[] { s.TopSpeed, s.Acceleration, s.Handling, s.Braking }) Assert.That(v, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void Names_And_Taglines_Use_No_Real_Car_Brands()
        {
            string[] banned = { "toyota", "honda", "ford", "bmw", "mercedes", "audi", "volkswagen", "vw", "nissan", "mazda", "subaru", "tesla", "ferrari", "lamborghini",
                "porsche", "chevrolet", "chevy", "jeep", "dodge", "mini cooper", "fiat", "renault", "peugeot", "citroen", "volvo", "hyundai", "kia", "suzuki", "mitsubishi",
                "lexus", "jaguar", "bentley", "rolls", "bugatti", "mclaren", "cadillac", "buick", "gmc", "ram", "land rover", "checker", "datsun", "isuzu" };
            foreach (CarData c in Cars())
            {
                string text = (c.DisplayName + " " + c.Tagline + " " + c.CarId).ToLowerInvariant();
                foreach (string b in banned)
                    Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text, @"\b" + System.Text.RegularExpressions.Regex.Escape(b) + @"\b"), $"{c.CarId}: '{b}'");
            }
        }
    }
}
