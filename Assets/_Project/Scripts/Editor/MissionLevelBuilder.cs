using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Environment;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.Parking;
using DS = UltimateCarParking.EditorTools.DrivingSchoolBuilder;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Writes mission-based level configurations over the generated campaign assets (same files and ids, so save
    /// progress and catalog order are kept). Phase C: Chapter 1 levels 1–3 in the Driving School yard —
    /// standard, reverse and parallel parking. Levels 4–30 stay on the Phase 3 generator until later phases.
    /// </summary>
    public static class MissionLevelBuilder
    {
        public static void BuildChapterOne()
        {
            var env = AssetDatabase.LoadAssetAtPath<GameObject>(DS.EnvPrefabPath);
            if (env == null) { Debug.LogError("[Missions] Driving School prefab missing; build it first."); return; }
            Level01Standard(env);
            Level02Reverse(env);
            Level03Parallel(env);
            AssetDatabase.SaveAssets();
            Debug.Log("[Missions] Chapter 1 levels 1-3 written");
        }

        // ------------------------------------------------------------------ levels

        private static void Level01Standard(GameObject env)
        {
            var props = new List<PropPlacement>();
            Parked(props, "Parked_hatchback", new Vector3(DS.AreaABayX(0), 0f, DS.BayCentreZ + 0.2f), 0f);
            Parked(props, "Parked_wagon", new Vector3(DS.AreaABayX(1), 0f, DS.BayCentreZ + 0.1f), 180f);
            Parked(props, "Parked_suv", new Vector3(DS.AreaABayX(5), 0f, DS.BayCentreZ + 0.2f), 0f);
            Parked(props, "Parked_compact", new Vector3(DS.AreaBBayX(1), 0f, DS.BayCentreZ + 0.4f), 180f);
            Parked(props, "Parked_minivan", new Vector3(DS.AreaBBayX(4), 0f, DS.BayCentreZ + 0.4f), 180f);

            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 12f, maxSpeedKmh = 2f, holdDuration = 1.0f };
            var park = new ParkObjective("Park in bay 4", new Vector3(DS.AreaABayX(3), 0f, DS.BayCentreZ), 0f,
                new Vector2(DS.BayWidth, DS.BayBack - DS.BayFront), ParkDirection.Either, rules);

            Write(1, "L01", "First Bay", MissionType.StandardParking,
                "Lesson 1. Drive down the training lane and park in bay 4 - nose first or backwards, your choice. Stop inside the lines and hold still.",
                env, props, spawn: new Vector3(-35f, 0f, -1.6f), spawnYaw: 90f, target: park,
                time: 120f, collisions: 3, three: (40f, 0), two: (70f, 1), coins: (120, 30), park);
        }

        private static void Level02Reverse(GameObject env)
        {
            var props = new List<PropPlacement>();
            // Neighbours on both sides of R3, reversed in like a driving examiner would park them.
            Parked(props, "Parked_sedan", new Vector3(DS.AreaBBayX(1), 0f, DS.BayCentreZ + 0.35f), 180f);
            Parked(props, "Parked_suv", new Vector3(DS.AreaBBayX(3), 0f, DS.BayCentreZ + 0.35f), 180f);
            Parked(props, "Parked_coupe", new Vector3(DS.AreaBBayX(5), 0f, DS.BayCentreZ + 0.35f), 180f);
            Parked(props, "Parked_pickup", new Vector3(DS.AreaABayX(2), 0f, DS.BayCentreZ + 0.1f), 0f);
            Parked(props, "Parked_hatchback", new Vector3(DS.AreaABayX(4), 0f, DS.BayCentreZ + 0.2f), 0f);

            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 10f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            var park = new ReverseParkObjective("Reverse into bay R3", new Vector3(DS.AreaBBayX(2), 0f, DS.BayCentreZ), 0f,
                new Vector2(DS.BayWidth, DS.BayBack - DS.BayFront), maxRearGap: 0.7f, rules);

            Write(2, "L02", "Back It In", MissionType.ReverseParking,
                "Lesson 2. Drive past bay R3, stop, then reverse in between the two cars. Use the rear sensor: finish within 0.7 m of the back line.",
                env, props, spawn: new Vector3(-30f, 0f, -1.6f), spawnYaw: 90f, target: park,
                time: 120f, collisions: 2, three: (55f, 0), two: (85f, 1), coins: (140, 35), park);
        }

        private static void Level03Parallel(GameObject env)
        {
            const float slotStart = 4.3f, slotEnd = 10.7f;           // inside the painted 7 m slot x 4 … 11
            float z = DS.SlotCentreZ - 0.3f;                         // parked cars sit close to the kerb (kerb is −Z)
            var props = new List<PropPlacement>();
            Parked(props, "Parked_wagon", new Vector3(slotStart - 0.3f - 2.3f, 0f, z), 90f);    // behind the gap
            Parked(props, "Parked_sedan", new Vector3(slotEnd + 0.3f + 2.2f, 0f, z), 90f);      // ahead of the gap
            Parked(props, "Parked_compact", new Vector3(-6.4f, 0f, z), 90f);
            Parked(props, "Parked_offroad", new Vector3(21.6f, 0f, z), 90f);

            var rules = new ParkingRules { edgeMargin = 0.04f, angleTolerance = 8f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            const float slotWidth = 2.6f;                             // kerb to 2.6 m out (inside the painted 3 m strip)
            var park = new ParallelParkObjective("Parallel park at the kerb", new Vector3((slotStart + slotEnd) * 0.5f, 0f, DS.KerbZ + slotWidth * 0.5f), 90f,
                new Vector2(slotWidth, slotEnd - slotStart), kerbSide: 1, maxKerbGap: 0.45f, kerbStrikeLimit: 2, rules);

            Write(3, "L03", "Kerbside", MissionType.ParallelParking,
                "Lesson 3. Parallel park in the gap at the kerb. Pull level with the car ahead, then reverse in. Finish within 0.45 m of the kerb - mount it more than twice and you fail.",
                env, props, spawn: new Vector3(-34f, 0f, -2f), spawnYaw: 90f, target: park,
                time: 150f, collisions: 2, three: (70f, 0), two: (110f, 1), coins: (160, 40), park);
        }

        // ------------------------------------------------------------------ writing

        private static void Parked(List<PropPlacement> props, string name, Vector3 pos, float yaw)
        {
            GameObject prefab = RealPropFactory.Load(name);
            if (prefab == null) { Debug.LogError("[Missions] missing " + name); return; }
            props.Add(new PropPlacement(prefab, pos, yaw));
        }

        private static void Write(int number, string id, string name, MissionType type, string briefing, GameObject env,
            List<PropPlacement> props, Vector3 spawn, float spawnYaw, ParkObjective target, float time, int collisions,
            (float time, int hits) three, (float time, int hits) two, (int baseCoins, int perStar) coins, params MissionObjective[] objectives)
        {
            string path = $"{ProjectBootstrapper.LevelsDir}/Level_{number:00}.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null) { level = ScriptableObject.CreateInstance<LevelData>(); AssetDatabase.CreateAsset(level, path); }
            var so = new SerializedObject(level);
            so.FindProperty("levelId").stringValue = id;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("levelNumber").intValue = number;
            so.FindProperty("missionType").enumValueIndex = (int)type;
            so.FindProperty("briefing").stringValue = briefing;
            SerializedProperty list = so.FindProperty("objectives");
            list.arraySize = objectives.Length;
            for (int i = 0; i < objectives.Length; i++) list.GetArrayElementAtIndex(i).managedReferenceValue = objectives[i];
            so.FindProperty("theme").objectReferenceValue = null;
            so.FindProperty("environmentPrefab").objectReferenceValue = env;
            LevelDesigner.WritePlacements(so.FindProperty("props"), props);
            so.FindProperty("forcedCar").objectReferenceValue = null;
            so.FindProperty("cameraHeight").floatValue = 0f;
            so.FindProperty("cameraDistance").floatValue = 0f;
            so.FindProperty("spawnPosition").vector3Value = spawn;
            so.FindProperty("spawnYaw").floatValue = spawnYaw;
            // Legacy target = the first bay (level select preview, debug overlay, older tests).
            so.FindProperty("targetPosition").vector3Value = target.Position;
            so.FindProperty("targetYaw").floatValue = Mathf.Repeat(target.ZoneRotation.eulerAngles.y, 360f);
            so.FindProperty("targetSize").vector2Value = target.Size;
            so.FindProperty("timeLimitSeconds").floatValue = time;
            so.FindProperty("collisionLimit").intValue = collisions;
            so.FindProperty("overrideParkingRules").boolValue = false;
            so.FindProperty("threeStarTime").floatValue = three.time;
            so.FindProperty("threeStarMaxCollisions").intValue = three.hits;
            so.FindProperty("twoStarTime").floatValue = two.time;
            so.FindProperty("twoStarMaxCollisions").intValue = two.hits;
            so.FindProperty("baseCoins").intValue = coins.baseCoins;
            so.FindProperty("coinsPerStar").intValue = coins.perStar;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
        }
    }
}
