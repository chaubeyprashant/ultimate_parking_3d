using System.Collections.Generic;
using UnityEditor;
using UltimateCarParking.Cars;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Phase 2 handling: a chassis character per vehicle type (suspension, tyres, roll stiffness, steering return) plus
    /// a few per-car corrections, written into every garage car's CarData after the factories build them.
    /// Values are physical: suspension natural frequency in Hz, damping ratio, travel in metres.
    /// Prices are not touched here (see FleetPricing.Pinned).
    /// </summary>
    public static class HandlingProfiles
    {
        /// <summary>Chassis character. Grip values are WheelCollider friction stiffness (stock 1.5 forward, 1.8 sideways); launch is the
        /// strongest acceleration (m/s²) below parking speed.</summary>
        public struct Chassis
        {
            public float travel, hz, zeta, grip, side, antiRoll, steerReturn, launch;
            public Chassis(float travel, float hz, float zeta, float grip, float side, float antiRoll, float steerReturn, float launch)
            { this.travel = travel; this.hz = hz; this.zeta = zeta; this.grip = grip; this.side = side; this.antiRoll = antiRoll; this.steerReturn = steerReturn; this.launch = launch; }
        }

        // Hatchback: light, quick to centre, agile.
        public static readonly Chassis Hatch = new Chassis(0.20f, 1.75f, 0.62f, 1.50f, 1.85f, 7000f, 175f, 3.4f);
        // Sedan / wagon: balanced and predictable.
        public static readonly Chassis Sedan = new Chassis(0.22f, 1.60f, 0.63f, 1.50f, 1.80f, 7500f, 160f, 3.2f);
        public static readonly Chassis Wagon = new Chassis(0.22f, 1.50f, 0.62f, 1.55f, 1.85f, 8500f, 155f, 3.2f);
        // Classic muscle and luxury coupes: soft, a little roll, slower to settle.
        public static readonly Chassis Muscle = new Chassis(0.20f, 1.55f, 0.55f, 1.45f, 1.75f, 7500f, 150f, 3.9f);
        // Sports cars: firm, flat, sharp.
        public static readonly Chassis Sports = new Chassis(0.16f, 2.00f, 0.66f, 1.60f, 2.00f, 10000f, 175f, 4.2f);
        public static readonly Chassis Supercar = new Chassis(0.14f, 2.20f, 0.68f, 1.65f, 2.05f, 11000f, 180f, 4.6f);
        // Rally-tuned: longer travel than a sports car, still firm.
        public static readonly Chassis Rally = new Chassis(0.20f, 1.80f, 0.62f, 1.60f, 1.95f, 9000f, 170f, 4.2f);
        // SUVs: heavier, soft and stable, more travel, stiff anti-roll to stay upright.
        public static readonly Chassis Suv = new Chassis(0.28f, 1.35f, 0.60f, 1.50f, 1.70f, 12000f, 145f, 3.0f);
        // Monster trucks: very long, soft suspension, heavy slow steering.
        public static readonly Chassis Monster = new Chassis(0.45f, 1.00f, 0.50f, 1.50f, 1.60f, 16000f, 110f, 2.6f);
        // Formula car: very stiff, low travel, lots of grip.
        public static readonly Chassis Formula = new Chassis(0.10f, 2.80f, 0.72f, 1.75f, 2.30f, 9000f, 190f, 4.8f);

        /// <summary>Per-car chassis and corrections. Null fields leave the factory value.</summary>
        public sealed class Tune
        {
            public Chassis chassis;
            public float? torque, maxSteer, steerSpeed;
            public string note;
        }

        public static readonly Dictionary<string, Tune> Cars = new Dictionary<string, Tune>
        {
            ["sedan"] = new Tune { chassis = Sedan, note = "balanced, predictable starter" },
            ["hatch"] = new Tune { chassis = Hatch, maxSteer = 40f, steerSpeed = 140f, note = "tightest turning circle, quickest steering" },
            ["wagon"] = new Tune { chassis = Wagon, note = "AWD: best budget car in rain and snow" },
            ["coupe"] = new Tune { chassis = Sports, note = "firm sporty chassis" },
            ["suv"] = new Tune { chassis = Suv, torque = 3900f, note = "torque 4500 → 3900: strong but no longer quicker than dearer SUVs" },
            ["urban_10"] = new Tune { chassis = Suv, note = "modern premium SUV" },
            ["bokaroo_67"] = new Tune { chassis = Suv, note = "classic 4x4" },
            ["fairheaven_lt_80"] = new Tune { chassis = Muscle, note = "plush luxury ride" },
            ["phoenix455_71"] = new Tune { chassis = Muscle, note = "muscle car" },
            ["tiara_gt_83"] = new Tune { chassis = Rally, note = "rally-tuned AWD: best premium car on loose grip" },
            ["asti_stradale_89"] = new Tune { chassis = Sports, note = "balanced sports coupe" },
            ["roadster_00"] = new Tune { chassis = Sports, note = "open-top, sharp" },
            ["chapman_73"] = new Tune { chassis = Sports, note = "featherweight, most agile premium car" },
            ["sport"] = new Tune { chassis = Supercar, note = "highest top speed (98 km/h)" },
            ["monster_mammoth"] = new Tune { chassis = Monster, maxSteer = 26f, steerSpeed = 70f, note = "heavy steering, widest turning circle" },
            ["monster_goliath"] = new Tune { chassis = Monster, maxSteer = 27f, steerSpeed = 72f, note = "heavy steering, wide turning circle" },
            ["saba_v12_95"] = new Tune { chassis = Supercar, note = "V12 grand supercar" },
            ["cheetah_84"] = new Tune { chassis = Supercar, note = "light wedge supercar" },
            ["exterminator_00"] = new Tune { chassis = Supercar, note = "fastest road car" },
            ["f1_velocity"] = new Tune { chassis = Formula, maxSteer = 26f, note = "stiff, low, most grip and the shortest stops" },
        };

        /// <summary>Writes the chassis and corrections into each catalog car's handling. Idempotent.</summary>
        public static int Apply(CarCatalog catalog)
        {
            int n = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                CarData car = catalog.Get(i);
                if (car == null || !Cars.TryGetValue(car.CarId, out Tune t)) continue;
                var so = new SerializedObject(car);
                SerializedProperty h = so.FindProperty("handling");
                void F(string field, float v) => h.FindPropertyRelative(field).floatValue = v;
                F("suspensionTravel", t.chassis.travel);
                F("springHz", t.chassis.hz);
                F("dampingRatio", t.chassis.zeta);
                F("tireGrip", t.chassis.grip);
                F("sideGrip", t.chassis.side);
                F("antiRoll", t.chassis.antiRoll);
                F("steerReturnSpeed", t.chassis.steerReturn);
                F("launchAccel", t.chassis.launch);
                if (t.torque.HasValue)
                {
                    F("motorTorque", t.torque.Value);
                    float rev = h.FindPropertyRelative("reverseTorque").floatValue;
                    if (rev > t.torque.Value) F("reverseTorque", t.torque.Value * 0.6f);
                }
                if (t.maxSteer.HasValue)
                {
                    F("maxSteerAngle", t.maxSteer.Value);
                    SerializedProperty hs = h.FindPropertyRelative("highSpeedSteerAngle");
                    hs.floatValue = UnityEngine.Mathf.Min(hs.floatValue, t.maxSteer.Value);
                }
                if (t.steerSpeed.HasValue) F("steerSpeed", t.steerSpeed.Value);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(car);
                n++;
            }
            AssetDatabase.SaveAssets();
            return n;
        }
    }
}
