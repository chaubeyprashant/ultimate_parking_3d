using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Environment;
using UltimateCarParking.Levels;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Designs the 30 campaign levels from rules (deterministic: same seed → same layout every run).
    ///   City Lot 1–10, Sky Garage 11–20, The Yard 21–30.
    /// Difficulty d = 0..1 over the campaign narrows bays and the aisle, fills more bays, adds aisle obstacles,
    /// tightens the angle/hold rules, the collision limit and the clock. Levels 5/10/15/20/25/30 are parallel
    /// parking; 8/13/18/23/27 must be reversed in. Spawn zones and each target's approach are kept clear.
    /// </summary>
    public static class LevelDesigner
    {
        public const int LevelCount = 30;
        private static readonly int[] ReverseInLevels = { 8, 13, 18, 23, 27 };

        public static void BuildAll()
        {
            var levels = new LevelData[LevelCount];
            for (int n = 1; n <= LevelCount; n++) levels[n - 1] = Design(n);

            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, ProjectBootstrapper.CatalogPath);
            }
            var so = new SerializedObject(catalog);
            SerializedProperty list = so.FindProperty("levels");
            list.arraySize = levels.Length;
            for (int i = 0; i < levels.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Bootstrap] {LevelCount} levels + catalog written");
        }

        private sealed class Layout
        {
            public readonly List<PropPlacement> Props = new List<PropPlacement>();
            public Vector3 Spawn;
            public float SpawnYaw;
            public Vector3 Target;
            public float TargetYaw;
            public Vector2 TargetSize;
            public float Aisle;
        }

        private static LevelData Design(int n)
        {
            float d = (n - 1) / (float)(LevelCount - 1);
            int k = (n - 1) % 10;
            string themeId = n <= 10 ? EnvironmentKit.CityId : n <= 20 ? EnvironmentKit.GarageId : EnvironmentKit.YardId;
            EnvironmentTheme theme = EnvironmentKit.Theme(themeId);
            var rng = new System.Random(7919 * n);

            bool parallel = k == 4 || k == 9;
            bool reverseIn = !parallel && System.Array.IndexOf(ReverseInLevels, n) >= 0;
            bool fromEast = n % 2 == 0;
            bool garage = themeId == EnvironmentKit.GarageId;

            float bayW = Round(Mathf.Lerp(3.0f, 2.65f, d), 0.05f);
            float bayL = Round(Mathf.Lerp(5.9f, 5.7f, d), 0.05f);   // real pickup is 5.18 m long
            float aisle = Mathf.Max(8f, Round(Mathf.Lerp(13f, 8.5f, d) - (garage ? 0.5f : 0f), 0.5f));
            float aisleHalf = aisle * 0.5f;
            float hx = theme.LotSize.x * 0.5f;
            int bays = Mathf.Min(18, Mathf.FloorToInt((theme.LotSize.x - 15f) / bayW));
            float rowLen = bays * bayW, x0 = -rowLen * 0.5f;
            float fill = Mathf.Lerp(0.45f, 0.92f, d);
            int lineVariant = garage ? 1 : 0;

            var L = new Layout
            {
                Aisle = aisle,
                Spawn = new Vector3(fromEast ? hx - 9.5f : -(hx - 9.5f), 0f, 0f),   // 9.5 m from the end wall: room for the chase camera
                SpawnYaw = fromEast ? 270f : 90f,
            };

            int targetRow = parallel ? 0 : rng.Next(2);
            int targetIdx = fromEast ? Range(rng, 1, bays / 2) : Range(rng, bays / 2, bays - 2);

            for (int row = 0; row < 2; row++)
            {
                float s = row == 0 ? 1f : -1f;
                if (parallel && row == 0)
                {
                    ParallelLane(L, rng, n, d, s, aisleHalf, x0, rowLen, fromEast, lineVariant);
                    continue;
                }

                float centerZ = s * (aisleHalf + bayL * 0.5f);
                for (int b = 0; b <= bays; b++)
                    L.Props.Add(new PropPlacement(Mod(EnvironmentKit.BayLine), new Vector3(x0 + b * bayW, 0f, centerZ), 0f, new Vector3(1f, 1f, bayL), lineVariant));
                L.Props.Add(new PropPlacement(Mod(EnvironmentKit.BayLine), new Vector3(0f, 0f, s * (aisleHalf + bayL)), 90f, new Vector3(1f, 1f, rowLen), lineVariant));

                for (int b = 0; b < bays; b++)
                {
                    float cx = x0 + (b + 0.5f) * bayW;
                    L.Props.Add(new PropPlacement(Mod(EnvironmentKit.WheelStop), new Vector3(cx, 0f, s * (aisleHalf + bayL - 0.45f)), 0f));
                    bool isTarget = row == targetRow && b == targetIdx;
                    if (isTarget)
                    {
                        L.Target = new Vector3(cx, 0f, centerZ);
                        float noseIn = s > 0f ? 0f : 180f;
                        L.TargetYaw = reverseIn ? noseIn + 180f : noseIn;
                        L.TargetSize = new Vector2(bayW, bayL);
                        continue;
                    }
                    bool neighbour = row == targetRow && Mathf.Abs(b - targetIdx) == 1 && n >= 3;
                    if (neighbour || rng.NextDouble() < fill)
                    {
                        float yaw = (s > 0f ? 0f : 180f) + (rng.NextDouble() < 0.5 ? 0f : 180f) + Jitter(rng, 2f);
                        L.Props.Add(new PropPlacement(Mod(EnvironmentKit.ParkedCar), new Vector3(cx + Jitter(rng, 0.08f), 0f, centerZ + Jitter(rng, 0.15f)), yaw, default, rng.Next(8)));
                    }
                }

                if (garage)
                {
                    for (int b = 0; b <= bays; b += 3)   // structural columns on the back line
                        L.Props.Add(new PropPlacement(Mod(EnvironmentKit.Pillar), new Vector3(x0 + b * bayW, 0f, s * (aisleHalf + bayL + 0.45f)), 0f));
                    if (k >= 6)                           // later garage levels: columns on the aisle edge too
                        for (int b = 1; b < bays; b += 3)
                            if (!(row == targetRow && b >= targetIdx - 1 && b <= targetIdx + 2))   // keep the turn-in clear
                                L.Props.Add(new PropPlacement(Mod(EnvironmentKit.Pillar), new Vector3(x0 + b * bayW, 0f, s * (aisleHalf - 0.35f)), 0f));
                }
            }

            AisleObstacles(L, rng, themeId, n, d, k, aisleHalf, x0, rowLen);

            // Rules, limits, rewards.
            float travel = Mathf.Abs(L.Target.x - L.Spawn.x) + Mathf.Abs(L.Target.z);
            float timeLimit = 25f + travel * 1.1f + (parallel ? 20f : 0f) + (reverseIn ? 12f : 0f) + (garage ? 5f : 0f);
            timeLimit = Mathf.Round(Mathf.Clamp(timeLimit * Mathf.Lerp(1.35f, 1.0f, d), 45f, 120f) / 5f) * 5f;
            int collisionLimit = Mathf.RoundToInt(Mathf.Lerp(3f, 1f, d));
            var rules = new UltimateCarParking.Parking.ParkingRules
            {
                edgeMargin = Round(Mathf.Lerp(0.05f, 0.08f, d), 0.01f),
                angleTolerance = Mathf.Round(Mathf.Lerp(12f, 6f, d)),
                allowEitherDirection = !reverseIn,
                maxSpeedKmh = 2f,
                holdDuration = Round(Mathf.Lerp(1.2f, 2.0f, d), 0.1f),
            };
            string suffix = parallel ? " - Parallel" : reverseIn ? " - Reverse In" : "";

            return WriteLevel(n, $"L{n:00}", $"Level {n}{suffix}", theme, L, timeLimit, collisionLimit, rules,
                threeStar: (Mathf.Round(timeLimit * 0.5f), 0), twoStar: (Mathf.Round(timeLimit * 0.75f), 1),
                coins: (80 + 10 * n, 20 + 2 * n));
        }

        /// <summary>North side as a kerbside lane of parallel slots; the target slot has cars fore and aft.</summary>
        private static void ParallelLane(Layout L, System.Random rng, int n, float d, float s, float aisleHalf, float x0, float rowLen,
            bool fromEast, int lineVariant)
        {
            const float laneWidth = 2.6f;
            float slotL = Round(Mathf.Lerp(7.4f, 6.8f, d), 0.1f);
            int slots = Mathf.FloorToInt(rowLen / slotL);
            float lx0 = -slots * slotL * 0.5f;
            float laneZ = s * (aisleHalf + laneWidth * 0.5f);
            int targetSlot = fromEast ? Range(rng, 1, slots / 2) : Range(rng, slots / 2, slots - 2);

            L.Props.Add(new PropPlacement(Mod(EnvironmentKit.BayLine), new Vector3(0f, 0f, s * (aisleHalf + 0.05f)), 90f, new Vector3(1f, 1f, slots * slotL), lineVariant));
            L.Props.Add(new PropPlacement(Mod(EnvironmentKit.Kerb), new Vector3(0f, 0f, s * (aisleHalf + laneWidth + 0.2f)), 0f, new Vector3(slots * slotL + 4f, 1f, 1f)));
            for (int i = 0; i <= slots; i++)
                L.Props.Add(new PropPlacement(Mod(EnvironmentKit.BayLine), new Vector3(lx0 + i * slotL, 0f, s * (aisleHalf + 0.45f)), 0f, new Vector3(1f, 1f, 0.8f), lineVariant));

            for (int i = 0; i < slots; i++)
            {
                float cx = lx0 + (i + 0.5f) * slotL;
                if (i == targetSlot)
                {
                    L.Target = new Vector3(cx, 0f, laneZ);
                    L.TargetYaw = 90f;
                    L.TargetSize = new Vector2(laneWidth, slotL);
                    continue;
                }
                bool neighbour = Mathf.Abs(i - targetSlot) == 1;
                if (neighbour || rng.NextDouble() < 0.7)
                {
                    float yaw = (rng.NextDouble() < 0.5 ? 90f : 270f) + Jitter(rng, 1.5f);
                    L.Props.Add(new PropPlacement(Mod(EnvironmentKit.ParkedCar), new Vector3(cx + Jitter(rng, 0.3f), 0f, laneZ + Jitter(rng, 0.1f)), yaw, default, rng.Next(8)));
                }
            }
        }

        /// <summary>Theme obstacles hugging the aisle edges, never near the spawn or the target approach.</summary>
        private static void AisleObstacles(Layout L, System.Random rng, string themeId, int n, float d, int k, float aisleHalf, float x0, float rowLen)
        {
            int count = Mathf.RoundToInt(Mathf.Lerp(0f, 5f, d) + (k >= 5 ? 1 : 0));
            if (n <= 2) count = 0;
            float maxProtrusion = L.Aisle - 5.2f;   // keep at least 5.2 m of the aisle free at every obstacle
            var used = new List<float>();
            int side = rng.Next(2) == 0 ? 1 : -1;
            for (int attempt = 0; attempt < 60 && used.Count < count; attempt++)
            {
                float x = Mathf.Lerp(x0 + 2f, x0 + rowLen - 2f, (float)rng.NextDouble());
                if (Mathf.Abs(x - L.Spawn.x) < 10f || Mathf.Abs(x - L.Target.x) < 8f) continue;
                bool clash = false;
                foreach (float u in used) if (Mathf.Abs(u - x) < 6.5f) clash = true;
                if (clash) continue;
                used.Add(x);
                float edge = side * aisleHalf;
                int kind = rng.Next(3);
                switch (themeId)
                {
                    case EnvironmentKit.CityId:
                        if (kind == 0 || maxProtrusion < 2.1f) ConeLine(L, x, edge - side * 0.5f);
                        else L.Props.Add(new PropPlacement(Mod(EnvironmentKit.Barrier), new Vector3(x, 0f, edge - side * 1.05f), 0f, default, rng.Next(4)));
                        break;
                    case EnvironmentKit.GarageId:
                        if (kind == 0) ConeLine(L, x, edge - side * 0.5f);
                        else L.Props.Add(new PropPlacement(Mod(EnvironmentKit.Barrier), new Vector3(x, 0f, edge - side * 0.4f), 90f, default, 0));
                        break;
                    default:
                        if (kind == 0 && maxProtrusion >= 2.5f) L.Props.Add(new PropPlacement(Mod(EnvironmentKit.SandPile), new Vector3(x, 0f, edge - side * 1.3f), 0f));
                        else if (kind == 1) L.Props.Add(new PropPlacement(Mod(EnvironmentKit.PipeStack), new Vector3(x, 0f, edge - side * 0.8f), 90f));
                        else L.Props.Add(new PropPlacement(Mod(EnvironmentKit.Barrier), new Vector3(x, 0f, edge - side * 1.05f), 0f, default, 3));
                        break;
                }
                side = -side;
            }
        }

        private static void ConeLine(Layout L, float x, float z)
        {
            for (int i = -1; i <= 1; i++) L.Props.Add(new PropPlacement(Mod(EnvironmentKit.Cone), new Vector3(x + i * 1.1f, 0f, z), 0f));
        }

        // ------------------------------------------------------------------ writing

        private static LevelData WriteLevel(int number, string id, string displayName, EnvironmentTheme theme, Layout L, float timeLimit,
            int collisionLimit, UltimateCarParking.Parking.ParkingRules rules, (float time, int collisions) threeStar,
            (float time, int collisions) twoStar, (int baseCoins, int perStar) coins)
        {
            string path = $"{ProjectBootstrapper.LevelsDir}/Level_{number:00}.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null) { level = ScriptableObject.CreateInstance<LevelData>(); AssetDatabase.CreateAsset(level, path); }
            var so = new SerializedObject(level);
            so.FindProperty("levelId").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("levelNumber").intValue = number;
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("environmentPrefab").objectReferenceValue = null;
            so.FindProperty("missionType").enumValueIndex = 0;          // generated levels: one legacy park objective
            so.FindProperty("briefing").stringValue = "";
            so.FindProperty("objectives").arraySize = 0;
            WritePlacements(so.FindProperty("props"), L.Props);
            so.FindProperty("spawnPosition").vector3Value = L.Spawn;
            so.FindProperty("spawnYaw").floatValue = L.SpawnYaw;
            so.FindProperty("targetPosition").vector3Value = L.Target;
            so.FindProperty("targetYaw").floatValue = Mathf.Repeat(L.TargetYaw, 360f);
            so.FindProperty("targetSize").vector2Value = L.TargetSize;
            so.FindProperty("timeLimitSeconds").floatValue = timeLimit;
            so.FindProperty("collisionLimit").intValue = collisionLimit;
            so.FindProperty("overrideParkingRules").boolValue = true;
            SerializedProperty r = so.FindProperty("parkingRules");
            r.FindPropertyRelative("edgeMargin").floatValue = rules.edgeMargin;
            r.FindPropertyRelative("angleTolerance").floatValue = rules.angleTolerance;
            r.FindPropertyRelative("allowEitherDirection").boolValue = rules.allowEitherDirection;
            r.FindPropertyRelative("maxSpeedKmh").floatValue = rules.maxSpeedKmh;
            r.FindPropertyRelative("holdDuration").floatValue = rules.holdDuration;
            so.FindProperty("threeStarTime").floatValue = threeStar.time;
            so.FindProperty("threeStarMaxCollisions").intValue = threeStar.collisions;
            so.FindProperty("twoStarTime").floatValue = twoStar.time;
            so.FindProperty("twoStarMaxCollisions").intValue = twoStar.collisions;
            so.FindProperty("baseCoins").intValue = coins.baseCoins;
            so.FindProperty("coinsPerStar").intValue = coins.perStar;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            return level;
        }

        public static void WritePlacements(SerializedProperty array, IReadOnlyList<PropPlacement> props)
        {
            array.arraySize = props.Count;
            for (int i = 0; i < props.Count; i++)
            {
                SerializedProperty e = array.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("prefab").objectReferenceValue = props[i].prefab;
                e.FindPropertyRelative("position").vector3Value = props[i].position;
                e.FindPropertyRelative("yaw").floatValue = props[i].yaw;
                e.FindPropertyRelative("scale").vector3Value = props[i].scale;
                e.FindPropertyRelative("variant").intValue = props[i].variant;
            }
        }

        private static GameObject Mod(string name) => EnvironmentKit.Module(name);
        private static float Round(float v, float step) => Mathf.Round(v / step) * step;
        private static float Jitter(System.Random rng, float amount) => ((float)rng.NextDouble() * 2f - 1f) * amount;
        private static int Range(System.Random rng, int minInclusive, int maxInclusive) => rng.Next(minInclusive, Mathf.Max(minInclusive, maxInclusive) + 1);
    }
}
