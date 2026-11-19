using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Cars;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Adds the other drivable cars the project has the rights to use to the garage, on top of the ten car-pack bodies that RealCarFactory
    /// owns (their names, handling and classes are defined there): the low-poly Zhabotinsky cars, a low-poly F1 car and two monster trucks (all CC BY 4.0, credited in ATTRIBUTION.md). Fictional
    /// model names only, no real brands. Prices come from FleetPricing (class bands, ranked by measured performance), so the garage runs from budget cars to premium ones.
    /// Runs after RealCarFactory.WirePlayableFleet (CarFactory resets the catalog on every bootstrap); safe to run repeatedly.
    /// </summary>
    public static class FleetExpansion
    {
        private const string PrefabDir = "Assets/_Project/Prefabs/Vehicles";
        private const string Arenas = "Assets/_Project/ThirdParty/Arenas/";

        private sealed class Entry
        {
            public string Id, Name, Tagline, PackBody, ModelPath;
            public int Price;
            public float TargetLength, ExtraScale = 1f;
            public bool AutoFit;
            public float WheelScale = 1f;
            public VehicleHandling Handling;
        }

        private static VehicleHandling H(VehicleController.DriveType drive, float mass, float torque, float top, float brake, float steer, float highSteer, float steerSpeed, float comY = 0.32f) =>
            new VehicleHandling
            {
                driveType = drive, mass = mass, centerOfMass = new Vector3(0f, comY, 0.05f),
                motorTorque = torque, reverseTorque = torque * 0.68f, maxForwardSpeedKmh = top, maxReverseSpeedKmh = Mathf.Round(top * 0.34f),
                brakeTorque = brake, handbrakeTorque = brake * 1.5f, maxSteerAngle = steer, highSpeedSteerAngle = Mathf.Min(highSteer, steer), steerSpeed = steerSpeed,
            };

        private const string Premium = "Assets/_Project/ThirdParty/Vehicles/Premium/";

        /// <summary>How many cars the catalog must hold: the car-pack fleet built by RealCarFactory plus every entry here.</summary>
        public static int ExpectedCatalogSize => RealCarFactory.FleetCount + Fleet().Count();

        private static IEnumerable<Entry> Fleet()
        {
            const VehicleController.DriveType Fwd = VehicleController.DriveType.FrontWheelDrive, Rwd = VehicleController.DriveType.RearWheelDrive, Awd = VehicleController.DriveType.AllWheelDrive;
            yield return new Entry { Id = "kiri86", Name = "Kiri '86", Tagline = "A lightweight 80s coupe. Quick to turn.", ModelPath = Arenas + "Japan/jp_car_kiri86.glb", Price = 1200, TargetLength = 4.1f,
                Handling = H(Rwd, 1150f, 1680f, 64f, 2700f, 34f, 12f, 125f) };
            yield return new Entry { Id = "kiri94", Name = "Kiri '94", Tagline = "A 90s sports coupe with sharp steering.", ModelPath = Arenas + "Japan/jp_car_kiri94.glb", Price = 2400, TargetLength = 4.2f,
                Handling = H(Rwd, 1200f, 1930f, 68f, 2950f, 34f, 11f, 120f) };
            yield return new Entry { Id = "lasley67", Name = "Lasley '67", Tagline = "A tiny 60s classic. Easy to place in the tightest bay.", ModelPath = Arenas + "London/ldn_car_lasley.glb", Price = 3500, TargetLength = 3.5f,
                Handling = H(Rwd, 1500f, 2300f, 68f, 3200f, 32f, 10f, 100f) };
            yield return new Entry { Id = "lct95", Name = "LCT 3000 '95", Tagline = "A boxy delivery van: tall, wide at the back, grippy on all four wheels.", ModelPath = Arenas + "Japan/jp_car_lct95.glb", Price = 5000, TargetLength = 4.4f,
                Handling = H(Awd, 1350f, 2250f, 66f, 3300f, 33f, 10.5f, 115f) };
            yield return new Entry { Id = "canyon75", Name = "Canyon Taxi '75", Tagline = "A big city cab. Steady and wide.", ModelPath = Arenas + "NewYork/nyc_taxi.glb", Price = 7000, TargetLength = 5.0f, ExtraScale = 0.9f,
                Handling = H(Rwd, 1700f, 2400f, 60f, 3200f, 32f, 10f, 100f) };
            yield return new Entry { Id = "doutrive78", Name = "Doutrive '78", Tagline = "A rugged pickup with a roll cage.", ModelPath = Arenas + "NewYork/nyc_pickup_doutrive.glb", Price = 8500, TargetLength = 5.1f,
                Handling = H(Rwd, 1750f, 3000f, 64f, 3500f, 33f, 10f, 100f, 0.4f) };
            yield return new Entry { Id = "lightbody85", Name = "Lightbody Lifted '85", Tagline = "Lifted on big tyres. Four-wheel drive power.", ModelPath = Arenas + "NewYork/nyc_pickup_lightbody85.glb", Price = 10000, TargetLength = 5.2f, ExtraScale = 0.9f,
                Handling = H(Awd, 2200f, 4000f, 66f, 4400f, 32f, 10f, 92f, 0.46f) };
            yield return new Entry { Id = "lightbody90", Name = "Lightbody Utility '90", Tagline = "The heavy-duty work truck. Strong and wide.", ModelPath = Arenas + "NewYork/nyc_pickup_lightbody90.glb", Price = 12000, TargetLength = 5.3f, ExtraScale = 0.95f,
                Handling = H(Rwd, 2400f, 4600f, 66f, 4900f, 32f, 10f, 90f, 0.44f) };

            // --- Premium sports and muscle cars, rally-tuned, premium SUVs (Zhabotinsky low-poly series, CC BY 4.0) ---
            const string P = Premium;
            yield return new Entry { Id = "chapman_73", Name = "Chapman '73", Tagline = "A featherweight roadster. Pure sports-car steering.", ModelPath = P + "chapman_73.glb", AutoFit = true,
                Handling = H(Rwd, 1050f, 2900f, 76f, 3300f, 34f, 11f, 128f, 0.28f) };
            yield return new Entry { Id = "roadster_00", Name = "Roadster '00", Tagline = "Open-top fun with a sharp turn-in.", ModelPath = P + "roadster_00.glb", AutoFit = true,
                Handling = H(Rwd, 1150f, 2850f, 78f, 3400f, 34f, 11f, 126f, 0.28f) };
            yield return new Entry { Id = "asti_stradale_89", Name = "Asti Stradale '89", Tagline = "A road-going sports coupe. Fast and balanced.", ModelPath = P + "asti_stradale_89.glb", AutoFit = true,
                Handling = H(Rwd, 1250f, 2950f, 77f, 3500f, 33f, 10.5f, 122f, 0.3f) };
            yield return new Entry { Id = "tiara_gt_83", Name = "Tiara GT '83 Tuned", Tagline = "Rally-tuned with four-wheel grip.", ModelPath = P + "tiara_gt_83.glb", AutoFit = true,
                Handling = H(Awd, 1350f, 3300f, 75f, 3700f, 33f, 10.5f, 118f, 0.3f) };
            yield return new Entry { Id = "duke_69", Name = "Duke '69", Tagline = "A big-block muscle car with a long, low nose.", ModelPath = P + "duke_69.glb", AutoFit = true,
                Handling = H(Rwd, 1450f, 3250f, 74f, 3600f, 32f, 10f, 108f) };
            yield return new Entry { Id = "negotiator_80", Name = "Negotiator '80", Tagline = "A heavy, powerful grand tourer.", ModelPath = P + "negotiator_80.glb", AutoFit = true,
                Handling = H(Rwd, 1500f, 3300f, 74f, 3700f, 32f, 10f, 106f) };
            yield return new Entry { Id = "fairheaven_lt_80", Name = "Fairheaven LT '80", Tagline = "A plush, wide luxury coupe with serious power.", ModelPath = P + "fairheaven_lt_80.glb", AutoFit = true,
                Handling = H(Rwd, 1550f, 3200f, 72f, 3600f, 32f, 10f, 104f) };
            yield return new Entry { Id = "phoenix455_71", Name = "Phoenix 455 '71", Tagline = "The classic muscle icon. Loud, quick and heavy.", ModelPath = P + "phoenix455_71.glb", AutoFit = true,
                Handling = H(Rwd, 1600f, 3500f, 76f, 3800f, 32f, 10f, 104f) };
            yield return new Entry { Id = "bokaroo_67", Name = "Bokaroo '67", Tagline = "A classic four-wheel-drive explorer.", ModelPath = P + "bokaroo_67.glb", AutoFit = true,
                Handling = H(Awd, 1850f, 3400f, 66f, 4000f, 33f, 10.5f, 98f, 0.42f) };
            yield return new Entry { Id = "rancher_80_lifted", Name = "Rancher '80 Lifted", Tagline = "Lifted, tough and built for any road.", ModelPath = P + "rancher_80_lifted.glb", AutoFit = true,
                Handling = H(Awd, 2050f, 3500f, 64f, 4100f, 32f, 10f, 94f, 0.46f) };
            yield return new Entry { Id = "urban_10", Name = "Urban '10", Tagline = "A modern premium SUV. Smooth, quick and grippy.", ModelPath = P + "urban_10.glb", AutoFit = true,
                Handling = H(Awd, 2000f, 3600f, 70f, 4200f, 33f, 10.5f, 100f, 0.42f) };

            // --- Supercars ---
            yield return new Entry { Id = "cheetah_84", Name = "Cheetah '84", Tagline = "A wedge-shaped 80s supercar. Light and ferocious.", ModelPath = P + "cheetah_84.glb", AutoFit = true,
                Handling = H(Rwd, 1300f, 3500f, 82f, 4000f, 33f, 9.5f, 118f, 0.27f) };
            yield return new Entry { Id = "f1_velocity", Name = "Velocity F1", Tagline = "An open-wheel racer. Razor sharp and very narrow to park.", ModelPath = P + "f1_lowpoly.glb", AutoFit = true,
                Handling = H(Awd, 950f, 2500f, 90f, 2900f, 30f, 8f, 130f, 0.22f) };
            // Monster trucks: the lifted 4x4s with giant wheels and a raised body (derivatives of CC BY models; see ATTRIBUTION.md).
            yield return new Entry { Id = "monster_goliath", Name = "Goliath '85 Monster", Tagline = "A monster truck on giant wheels. Climbs anything.", ModelPath = "Assets/_Project/ThirdParty/Arenas/NewYork/nyc_pickup_lightbody85.glb",
                WheelScale = 2.4f, ExtraScale = 0.72f, Handling = H(Awd, 2600f, 5200f, 60f, 5000f, 31f, 10f, 85f, 0.55f) };
            yield return new Entry { Id = "monster_mammoth", Name = "Mammoth '80 Monster", Tagline = "A lifted 4x4 turned monster. Huge and unstoppable.", ModelPath = P + "rancher_80_lifted.glb",
                WheelScale = 2.4f, ExtraScale = 0.72f, Handling = H(Awd, 2700f, 5400f, 58f, 5200f, 31f, 10f, 82f, 0.55f) };
            yield return new Entry { Id = "saba_v12_95", Name = "Saba V12 '95", Tagline = "A 90s V12 supercar. Huge power, long nose.", ModelPath = P + "saba_v12_95.glb", AutoFit = true,
                Handling = H(Rwd, 1450f, 3850f, 86f, 4300f, 32f, 9f, 114f, 0.27f) };
            yield return new Entry { Id = "exterminator_00", Name = "Exterminator '00", Tagline = "The fastest car in the garage. Park it very carefully.", ModelPath = P + "exterminator_00.glb", AutoFit = true,
                Handling = H(Rwd, 1500f, 4000f, 88f, 4500f, 32f, 9f, 112f, 0.27f) };
        }

        public static int Build()
        {
            var catalogAsset = AssetDatabase.LoadAssetAtPath<CarCatalog>(CarFactory.CarCatalogPath);
            if (catalogAsset == null) { Debug.LogError("[Fleet] no CarCatalog"); return 0; }
            int built = 0;
            foreach (Entry e in Fleet())
            {
                string prefabPath = $"{PrefabDir}/Car_{char.ToUpperInvariant(e.Id[0])}{e.Id.Substring(1)}.prefab";
                GameObject prefab = e.PackBody != null
                    ? RealCarFactory.BuildOne(e.PackBody, prefabPath, writeStandaloneData: false, tightBounds: true)
                    : RiggedCarFactory.Build(e.ModelPath, prefabPath, e.Handling.mass, e.TargetLength, e.ExtraScale, e.AutoFit, e.WheelScale);
                if (prefab == null) { Debug.LogError($"[Fleet] skipped {e.Id}: prefab was not built"); continue; }
                WriteCarData(e, prefab);
                built++;
            }
            SortCatalogByPrice();
            FleetPricing.Apply(AssetDatabase.LoadAssetAtPath<CarCatalog>(CarFactory.CarCatalogPath));   // class bands + performance rank, never hand-set
            SortCatalogByPrice();                                                                      // list the garage from budget to premium
            AssetDatabase.SaveAssets();
            Debug.Log($"[Fleet] garage expanded: {built} new cars, {AssetDatabase.LoadAssetAtPath<CarCatalog>(CarFactory.CarCatalogPath).Count} in the catalog");
            return built;
        }

        private static CarData WriteCarData(Entry e, GameObject prefab)
        {
            string path = $"{CarFactory.CarsDir}/Car_{char.ToUpperInvariant(e.Id[0])}{e.Id.Substring(1)}.asset";
            var data = AssetDatabase.LoadAssetAtPath<CarData>(path);
            if (data == null) { data = ScriptableObject.CreateInstance<CarData>(); AssetDatabase.CreateAsset(data, path); }
            var so = new SerializedObject(data);
            so.FindProperty("carId").stringValue = e.Id;
            so.FindProperty("displayName").stringValue = e.Name;
            so.FindProperty("tagline").stringValue = e.Tagline;
            so.FindProperty("modelPrefab").objectReferenceValue = prefab;
            so.FindProperty("paintMaterial").objectReferenceValue = null;     // keep the model's own paint
            so.FindProperty("price").intValue = e.Price;
            so.FindProperty("ownedByDefault").boolValue = false;
            SerializedProperty h = so.FindProperty("handling");
            VehicleHandling v = e.Handling.Clone();
            // Driving force is torque over wheel radius, so a small-wheeled car would out-accelerate a big-wheeled one with the same
            // number. Normalise to the 0.33 m wheels of the starter cars, keeping the price ladder meaningful.
            WheelCollider wc = prefab.GetComponentInChildren<WheelCollider>(true);
            float k = wc != null ? Mathf.Clamp(wc.radius / 0.33f, 0.6f, 1.4f) : 1f;
            v.motorTorque *= k; v.reverseTorque *= k; v.brakeTorque *= k; v.handbrakeTorque *= k;
            h.FindPropertyRelative("driveType").enumValueIndex = (int)v.driveType;
            h.FindPropertyRelative("mass").floatValue = v.mass;
            h.FindPropertyRelative("centerOfMass").vector3Value = v.centerOfMass;
            h.FindPropertyRelative("motorTorque").floatValue = v.motorTorque;
            h.FindPropertyRelative("reverseTorque").floatValue = v.reverseTorque;
            h.FindPropertyRelative("maxForwardSpeedKmh").floatValue = v.maxForwardSpeedKmh;
            h.FindPropertyRelative("maxReverseSpeedKmh").floatValue = v.maxReverseSpeedKmh;
            h.FindPropertyRelative("brakeTorque").floatValue = v.brakeTorque;
            h.FindPropertyRelative("handbrakeTorque").floatValue = v.handbrakeTorque;
            h.FindPropertyRelative("maxSteerAngle").floatValue = v.maxSteerAngle;
            h.FindPropertyRelative("highSpeedSteerAngle").floatValue = v.highSpeedSteerAngle;
            h.FindPropertyRelative("steerSpeed").floatValue = v.steerSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>Starter first, then by price, so the garage reads as one ladder (stable for equal prices).</summary>
        private static void SortCatalogByPrice()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>(CarFactory.CarCatalogPath);
            var so = new SerializedObject(catalog);
            SerializedProperty list = so.FindProperty("cars");
            var cars = new List<CarData>();
            for (int i = 0; i < list.arraySize; i++) { var c = list.GetArrayElementAtIndex(i).objectReferenceValue as CarData; if (c != null) cars.Add(c); }
            // Add any fleet cars not yet in the catalog.
            foreach (string guid in AssetDatabase.FindAssets("t:CarData", new[] { CarFactory.CarsDir }))
            {
                var c = AssetDatabase.LoadAssetAtPath<CarData>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null && c.CarId != "real_sedan" && !cars.Contains(c)) cars.Add(c);
            }
            List<CarData> sorted = cars.OrderBy(c => c.OwnedByDefault ? 0 : 1).ThenBy(c => c.Price).ThenBy(c => c.DisplayName).ToList();
            list.arraySize = sorted.Count;
            for (int i = 0; i < sorted.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sorted[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }
    }
}
