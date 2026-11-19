using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Cars;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Prices every garage car from its class and its measured performance, never by hand. Four price bands run from budget to premium:
    /// Economy, Standard, Performance, Premium (the starter is free). Inside a band cars are ranked by a performance score built from the
    /// same stats the garage shows (top speed, acceleration, braking, handling) and spread evenly across the band, so a stronger car always
    /// costs more than a weaker one in its class, and any car of a higher class costs more than every car of a lower class.
    /// </summary>
    public static class FleetPricing
    {
        public static readonly Dictionary<CarClass, (int min, int max)> Bands = new Dictionary<CarClass, (int, int)>
        {
            [CarClass.Economy] = (400, 1500),
            [CarClass.Standard] = (1800, 5200),
            [CarClass.Performance] = (6500, 10500),
            [CarClass.Premium] = (13000, 28000),
            [CarClass.Elite] = (32000, 55000),
        };

        /// <summary>Market class of each car (a judgement about the vehicle: small budget cars, family and work cars, sporty or rugged cars, flagships).</summary>
        public static readonly Dictionary<string, CarClass> Classes = new Dictionary<string, CarClass>
        {
            // The six car-pack bodies (sedan, hatch, coupe, sport, wagon, suv) carry the class set in RealCarFactory.
            // Sports and muscle cars, rally-tuned and premium SUVs.
            ["phoenix455_71"] = CarClass.Premium, ["fairheaven_lt_80"] = CarClass.Premium, 
            ["chapman_73"] = CarClass.Premium, ["tiara_gt_83"] = CarClass.Premium, ["asti_stradale_89"] = CarClass.Premium, ["roadster_00"] = CarClass.Premium,
            ["bokaroo_67"] = CarClass.Premium, ["urban_10"] = CarClass.Premium, 
            // Supercars.
            ["saba_v12_95"] = CarClass.Elite, ["exterminator_00"] = CarClass.Elite, ["f1_velocity"] = CarClass.Elite, ["monster_goliath"] = CarClass.Elite, ["monster_mammoth"] = CarClass.Elite, ["cheetah_84"] = CarClass.Elite,
        };

        /// <summary>Performance score, 0..1: the garage's own stat ratings, weighted towards power.</summary>
        public static float Score(CarData car)
        {
            // Same ratings as CarStats but unclamped, so the fastest cars still rank apart once the garage bars are full.
            var h = car.Handling;
            float mass = Mathf.Max(300f, h.mass);
            float top = (h.maxForwardSpeedKmh - 40f) / 40f, accel = (h.motorTorque / mass - 0.8f) / 1.2f, brake = (h.brakeTorque / mass - 1.2f) / 1.4f;
            float handling = (h.maxSteerAngle - 28f) / 12f * 0.7f + (h.steerSpeed - 80f) / 60f * 0.3f;
            return 0.30f * top + 0.40f * accel + 0.15f * brake + 0.15f * handling;
        }

        /// <summary>
        /// Garage prices locked on 2026-11-19 (the prices players see; handling work must not move them). A car listed
        /// here keeps this price whatever its performance score; cars not listed are priced by class and rank.
        /// </summary>
        public static readonly Dictionary<string, int> Pinned = new Dictionary<string, int>
        {
            ["sedan"] = 0, ["hatch"] = 950, ["wagon"] = 3500, ["coupe"] = 8500,
            ["suv"] = 13000, ["urban_10"] = 14650, ["bokaroo_67"] = 16350, ["fairheaven_lt_80"] = 18000,
            ["phoenix455_71"] = 19650, ["tiara_gt_83"] = 21350, ["asti_stradale_89"] = 23000, ["roadster_00"] = 24650,
            ["chapman_73"] = 26350, ["sport"] = 28000, ["monster_mammoth"] = 32000, ["monster_goliath"] = 36600,
            ["saba_v12_95"] = 41200, ["cheetah_84"] = 45800, ["exterminator_00"] = 50400, ["f1_velocity"] = 55000,
        };

        /// <summary>Price for a car's rank within its class (0 = weakest). Rounded to 50.</summary>
        public static int PriceAt(CarClass cls, int rank, int count)
        {
            if (cls == CarClass.Starter) return 0;
            (int min, int max) = Bands[cls];
            float t = count <= 1 ? 0.5f : rank / (float)(count - 1);
            return Mathf.RoundToInt(Mathf.Lerp(min, max, t) / 50f) * 50;
        }

        /// <summary>Assigns class and price to every car in the catalog. Idempotent.</summary>
        public static void Apply(CarCatalog catalog)
        {
            var cars = new List<CarData>();
            for (int i = 0; i < catalog.Count; i++) if (catalog.Get(i) != null) cars.Add(catalog.Get(i));
            foreach (var group in cars.GroupBy(c => Classes.TryGetValue(c.CarId, out CarClass k) ? k : c.Class))
            {
                List<CarData> ranked = group.OrderBy(Score).ThenBy(c => c.DisplayName).ToList();
                for (int r = 0; r < ranked.Count; r++)
                {
                    var so = new SerializedObject(ranked[r]);
                    so.FindProperty("carClass").enumValueIndex = (int)group.Key;
                    so.FindProperty("price").intValue = Pinned.TryGetValue(ranked[r].CarId, out int pinned) ? pinned : PriceAt(group.Key, r, ranked.Count);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(ranked[r]);
                }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
