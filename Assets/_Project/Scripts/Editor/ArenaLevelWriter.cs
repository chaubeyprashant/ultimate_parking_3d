using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Cars;
using UltimateCarParking.Environment;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.EditorTools
{
    /// <summary>Writes country-arena mission levels (LevelData) and their per-country mission catalog.</summary>
    public static class ArenaLevelWriter
    {
        public const string Dir = "Assets/_Project/Levels/Countries";

        public sealed class Mission
        {
            public string id, name, briefing;
            public int number;
            public MissionType type;
            public GameObject environment;
            public List<PropPlacement> props = new List<PropPlacement>();
            public Vector3 spawn;
            public float spawnYaw;
            public float time = 120f;
            public int collisions = 3;
            public (float time, int hits) three = (60f, 0), two = (90f, 1);
            public (int baseCoins, int perStar) coins = (150, 40);
            public CarData forcedCar;
            /// <summary>Chase camera framing (0 = default); low for underground car parks.</summary>
            public float cameraHeight, cameraDistance;
            public ScoreMode scoreMode = ScoreMode.TimeAndCollisions;
            public (float three, float two) quality = (0.85f, 0.6f);
            public RoadDirection road = RoadDirection.RightHand;
            public MissionObjective[] objectives;
            /// <summary>Country id + arena folder: when set, the environment and props are baked into one
            /// addressable prefab (per-country asset pack) instead of being referenced from the level.</summary>
            public string countryId, arenaFolder;
        }

        public static string MissionPrefabPath(string arenaFolder, string levelId) =>
            $"{ArenaPropFactory.PrefabRoot}/{arenaFolder}/Missions/Env_{levelId}.prefab";

        /// <summary>Bakes arena + mission props into Prefabs/Arenas/&lt;arena&gt;/Missions/Env_&lt;id&gt;.prefab.</summary>
        private static string BakeMission(Mission m)
        {
            string path = MissionPrefabPath(m.arenaFolder, m.id);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var root = new GameObject("Env_" + m.id);
            if (m.environment != null) PrefabUtility.InstantiatePrefab(m.environment, root.transform);
            var props = new GameObject("Props").transform;
            props.SetParent(root.transform, false);
            foreach (PropPlacement p in m.props)
            {
                if (p.prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(p.prefab, props);
                go.transform.SetLocalPositionAndRotation(p.position, Quaternion.Euler(0f, p.yaw, 0f));
                go.transform.localScale = p.EffectiveScale;
            }
            ArenaOptimizer.Stats before = ArenaOptimizer.Measure(root);
            ArenaOptimizer.Optimize(root);
            Debug.Log($"[Perf] {m.id} before: {before}\n[Perf] {m.id} after:  {ArenaOptimizer.Measure(root)}");
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return path;
        }

        public static LevelData Write(Mission m)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/Level_{m.id}.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null) { level = ScriptableObject.CreateInstance<LevelData>(); AssetDatabase.CreateAsset(level, path); }
            var so = new SerializedObject(level);
            so.FindProperty("levelId").stringValue = m.id;
            so.FindProperty("displayName").stringValue = m.name;
            so.FindProperty("levelNumber").intValue = m.number;
            so.FindProperty("missionType").enumValueIndex = (int)m.type;
            so.FindProperty("roadDirection").enumValueIndex = (int)m.road;
            so.FindProperty("briefing").stringValue = m.briefing;
            SerializedProperty list = so.FindProperty("objectives");
            list.arraySize = m.objectives.Length;
            for (int i = 0; i < m.objectives.Length; i++) list.GetArrayElementAtIndex(i).managedReferenceValue = m.objectives[i];
            so.FindProperty("theme").objectReferenceValue = null;
            bool addressable = !string.IsNullOrEmpty(m.countryId) && !string.IsNullOrEmpty(m.arenaFolder);
            if (addressable)
            {
                BakeMission(m);
                so.FindProperty("environmentPrefab").objectReferenceValue = null;
                LevelDesigner.WritePlacements(so.FindProperty("props"), new List<PropPlacement>());
                so.FindProperty("environmentAddress").stringValue = ArenaDelivery.Address(m.countryId, m.id);
            }
            else
            {
                so.FindProperty("environmentPrefab").objectReferenceValue = m.environment;
                LevelDesigner.WritePlacements(so.FindProperty("props"), m.props);
                so.FindProperty("environmentAddress").stringValue = "";
            }
            so.FindProperty("forcedCar").objectReferenceValue = m.forcedCar;
            so.FindProperty("cameraHeight").floatValue = m.cameraHeight;
            so.FindProperty("cameraDistance").floatValue = m.cameraDistance;
            so.FindProperty("spawnPosition").vector3Value = m.spawn;
            so.FindProperty("spawnYaw").floatValue = m.spawnYaw;
            // Legacy target = the last parking objective (previews, debug overlay, level-fit tests).
            ParkObjective park = null;
            foreach (MissionObjective o in m.objectives) if (o is ParkObjective p) park = p;
            if (park != null)
            {
                so.FindProperty("targetPosition").vector3Value = park.Position;
                so.FindProperty("targetYaw").floatValue = Mathf.Repeat(park.ZoneRotation.eulerAngles.y, 360f);
                so.FindProperty("targetSize").vector2Value = park.Size;
            }
            so.FindProperty("timeLimitSeconds").floatValue = m.time;
            so.FindProperty("collisionLimit").intValue = m.collisions;
            so.FindProperty("overrideParkingRules").boolValue = false;
            so.FindProperty("threeStarTime").floatValue = m.three.time;
            so.FindProperty("threeStarMaxCollisions").intValue = m.three.hits;
            so.FindProperty("twoStarTime").floatValue = m.two.time;
            so.FindProperty("twoStarMaxCollisions").intValue = m.two.hits;
            so.FindProperty("scoreMode").enumValueIndex = (int)m.scoreMode;
            so.FindProperty("threeStarQuality").floatValue = m.quality.three;
            so.FindProperty("twoStarQuality").floatValue = m.quality.two;
            so.FindProperty("baseCoins").intValue = m.coins.baseCoins;
            so.FindProperty("coinsPerStar").intValue = m.coins.perStar;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            return level;
        }

        public static LevelCatalog WriteCatalog(string countryId, params LevelData[] levels) => WriteCatalog(countryId, null, levels);

        /// <summary>Writes the country's mission catalog; with an arena folder, also registers its asset pack.</summary>
        public static LevelCatalog WriteCatalog(string countryId, string arenaFolder, params LevelData[] levels)
        {
            if (!string.IsNullOrEmpty(arenaFolder))
                ArenaDelivery.RegisterCountry(countryId, levels.Select(l => (l.LevelId, MissionPrefabPath(arenaFolder, l.LevelId))));
            string path = $"{Dir}/Catalog_{countryId}.asset";
            var cat = AssetDatabase.LoadAssetAtPath<LevelCatalog>(path);
            if (cat == null) { cat = ScriptableObject.CreateInstance<LevelCatalog>(); AssetDatabase.CreateAsset(cat, path); }
            var so = new SerializedObject(cat);
            SerializedProperty list = so.FindProperty("levels");
            list.arraySize = levels.Length;
            for (int i = 0; i < levels.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cat);
            return cat;
        }

        public static void Place(List<PropPlacement> props, GameObject prefab, Vector3 pos, float yaw)
        {
            if (prefab == null) { Debug.LogError("[Arena] missing prefab at " + pos); return; }
            props.Add(new PropPlacement(prefab, pos, yaw));
        }
    }
}
