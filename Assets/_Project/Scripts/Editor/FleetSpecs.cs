using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Cars;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Writes each garage car's spec sheet: measured performance from Documentation/vehicle_specs.csv (the output of
    /// VehicleBenchmarkTests, copied in after a benchmark run), size from the prefab's collision box, a parking difficulty
    /// rating and a one-line strength (HandlingProfiles notes). Cars without a benchmark row keep zero (the garage then
    /// hides the measured rows instead of guessing).
    /// </summary>
    public static class FleetSpecs
    {
        public const string CsvPath = "Documentation/vehicle_specs.csv";

        public static int Apply(CarCatalog catalog)
        {
            Dictionary<string, string[]> rows = ReadCsv();
            int n = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                CarData car = catalog.Get(i);
                if (car == null) continue;
                var so = new SerializedObject(car);
                SerializedProperty s = so.FindProperty("specs");
                if (s == null) continue;
                void F(string f, float v) => s.FindPropertyRelative(f).floatValue = v;
                if (rows.TryGetValue(car.CarId, out string[] r))
                {
                    F("speedAfter35", P(r, 2));
                    F("zeroTo20", P(r, 3));
                    F("stopFrom40", P(r, 6));
                    F("wetStopFrom40", P(r, 7));
                    F("turnRadius", P(r, 9));
                }
                Vector2 size = Footprint(car.ModelPrefab);
                F("length", size.y);
                F("width", size.x);
                float turn = s.FindPropertyRelative("turnRadius").floatValue;
                s.FindPropertyRelative("parkingDifficulty").intValue = CarSpecs.Difficulty(size.y, size.x, turn > 0f ? turn : 4.6f);
                s.FindPropertyRelative("strength").stringValue = HandlingProfiles.Cars.TryGetValue(car.CarId, out var t) ? Capital(t.note) : "";
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(car);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Specs] spec sheets for {n} cars ({rows.Count} measured rows from {CsvPath})");
            return n;
        }

        private static string Capital(string s) => string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1) + ".";

        private static float P(string[] r, int i) => i < r.Length && float.TryParse(r[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

        private static Dictionary<string, string[]> ReadCsv()
        {
            var d = new Dictionary<string, string[]>();
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, CsvPath);
            if (!File.Exists(path)) return d;
            string[] lines = File.ReadAllLines(path);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] r = lines[i].Split(',');
                if (r.Length > 9) d[r[0].Trim()] = r;
            }
            return d;
        }

        /// <summary>Width (x) and length (y) of the car's non-wheel colliders, metres.</summary>
        public static Vector2 Footprint(GameObject prefab)
        {
            if (prefab == null) return Vector2.zero;
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                {
                    if (c is WheelCollider || c.isTrigger) continue;
                    Bounds b = c.bounds;
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 l = go.transform.InverseTransformPoint(new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z));
                        minX = Mathf.Min(minX, l.x); maxX = Mathf.Max(maxX, l.x); minZ = Mathf.Min(minZ, l.z); maxZ = Mathf.Max(maxZ, l.z);
                    }
                }
                return minX == float.MaxValue ? Vector2.zero : new Vector2(maxX - minX, maxZ - minZ);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
