using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltimateCarParking.Levels;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Two TEMPORARY country configurations that exercise the arena architecture end to end — one per arena source:
    /// <list type="bullet">
    /// <item>Test Country A: Resources prefab arena (Resources/Arenas/Arena_TestA, wraps the Driving School), left-hand
    /// traffic, overcast, open from the start.</item>
    /// <item>Test Country B: additive scene arena (Scenes/Arenas/Arena_TestB, wraps Garage B1), right-hand traffic,
    /// rain, unlocked by completing A with at least 2 stars.</item>
    /// </list>
    /// They live in their own catalog (WorldTour/Test) and are not part of the 30-country tour.
    /// </summary>
    public static class CountryArenaTestBuilder
    {
        public const string Dir = WorldTourBuilder.Dir + "/Test";
        public const string CatalogPath = Dir + "/TestCountryCatalog.asset";
        public const string CountryAPath = Dir + "/TestCountry_A.asset";
        public const string CountryBPath = Dir + "/TestCountry_B.asset";
        public const string ResourcesArenaDir = "Assets/_Project/Resources/Arenas";
        public const string ArenaAPrefabPath = ResourcesArenaDir + "/Arena_TestA.prefab";
        public const string ArenaAResourcesKey = "Arenas/Arena_TestA";
        public const string ArenaScenesDir = "Assets/_Project/Scenes/Arenas";
        public const string ArenaBSceneName = "Arena_TestB";
        public static string ArenaBScenePath => $"{ArenaScenesDir}/{ArenaBSceneName}.unity";

        /// <summary>Arena scenes that must be in Build Settings (after the game scenes).</summary>
        public static string[] ArenaScenePaths => new[] { ArenaBScenePath };

        [MenuItem("Tools/Ultimate Car Parking/Build Test Country Arenas")]
        public static void Build()
        {
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory(ResourcesArenaDir);
            Directory.CreateDirectory(ArenaScenesDir);

            var school = AssetDatabase.LoadAssetAtPath<GameObject>(DrivingSchoolBuilder.EnvPrefabPath);
            var garage = AssetDatabase.LoadAssetAtPath<GameObject>(RealGarageBuilder.EnvPrefabPath);
            if (school == null || garage == null) { Debug.LogError("[Arena] Driving School / Garage B1 prefabs missing; build real assets first."); return; }

            BuildPrefabArena(school);
            BuildSceneArena(garage);

            var campaign = AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath);
            CountryData a = WriteCountry(CountryAPath, "ta", "Test Country A", WorldRegion.Asia, campaign.Get(0), "in",
                new CountryArenaReference { source = ArenaSource.ResourcesPrefab, key = ArenaAResourcesKey },
                new CountryUnlockRequirement { unlockedByDefault = true, requirePreviousCountry = false },
                RoadDirection.LeftHand,
                new TrafficSettings { parkedCarDensity = 0.5f, movingTraffic = false },
                WeatherSettings.ForPreset(WeatherPreset.Overcast));
            CountryData b = WriteCountry(CountryBPath, "tb", "Test Country B", WorldRegion.Europe, campaign.Get(1), "de",
                new CountryArenaReference { source = ArenaSource.Scene, key = ArenaBSceneName },
                new CountryUnlockRequirement { requirePreviousCountry = false, requiredCountryIds = new[] { "ta" }, minTourStars = 2 },
                RoadDirection.RightHand,
                new TrafficSettings { parkedCarDensity = 0.8f, movingTraffic = true, movingTrafficDensity = 0.3f, trafficSpeedKmh = 15f },
                WeatherSettings.ForPreset(WeatherPreset.Rain));

            var catalog = AssetDatabase.LoadAssetAtPath<CountryCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<CountryCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            var so = new SerializedObject(catalog);
            so.FindProperty("campaign").objectReferenceValue = campaign;
            SerializedProperty list = so.FindProperty("countries");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = a;
            list.GetArrayElementAtIndex(1).objectReferenceValue = b;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[Arena] test countries A (prefab arena) and B (scene arena) written");
        }

        private static void BuildPrefabArena(GameObject environment)
        {
            var root = new GameObject("Arena_TestA");
            var env = (GameObject)PrefabUtility.InstantiatePrefab(environment, root.transform);
            env.name = "Environment";
            CountryArenaRoot arenaRoot = AddRoot(root, "ta", new Vector3(-35f, 0f, -1.6f), 90f,
                new Vector3(DrivingSchoolBuilder.AreaABayX(3), 0f, DrivingSchoolBuilder.BayCentreZ),
                new Vector3(DrivingSchoolBuilder.AreaBBayX(2), 0f, DrivingSchoolBuilder.BayCentreZ));
            PrefabUtility.SaveAsPrefabAsset(root, ArenaAPrefabPath);
            Object.DestroyImmediate(root);
            _ = arenaRoot;
        }

        private static void BuildSceneArena(GameObject environment)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Arena_TestB");
            var env = (GameObject)PrefabUtility.InstantiatePrefab(environment, root.transform);
            env.name = "Environment";
            AddRoot(root, "tb", new Vector3(0f, 0f, -8f), 0f, new Vector3(4f, 0f, 6f), new Vector3(-4f, 0f, 6f));
            EditorSceneManager.SaveScene(scene, ArenaBScenePath);
        }

        private static CountryArenaRoot AddRoot(GameObject root, string id, Vector3 spawn, float spawnYaw, params Vector3[] slots)
        {
            var arenaRoot = root.AddComponent<CountryArenaRoot>();
            var spawnTf = new GameObject("SpawnPoint").transform;
            spawnTf.SetParent(root.transform, false);
            spawnTf.SetLocalPositionAndRotation(spawn, Quaternion.Euler(0f, spawnYaw, 0f));
            var so = new SerializedObject(arenaRoot);
            so.FindProperty("arenaId").stringValue = id;
            so.FindProperty("spawnPoint").objectReferenceValue = spawnTf;
            SerializedProperty list = so.FindProperty("parkingSlots");
            list.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = new GameObject($"Slot{i}").transform;
                slot.SetParent(root.transform, false);
                slot.localPosition = slots[i];
                list.GetArrayElementAtIndex(i).objectReferenceValue = slot;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return arenaRoot;
        }

        private static CountryData WriteCountry(string path, string id, string name, WorldRegion region, LevelData level, string flagId,
            CountryArenaReference arena, CountryUnlockRequirement unlock, RoadDirection road, TrafficSettings traffic, WeatherSettings weather)
        {
            var data = AssetDatabase.LoadAssetAtPath<CountryData>(path);
            if (data == null) { data = ScriptableObject.CreateInstance<CountryData>(); AssetDatabase.CreateAsset(data, path); }
            var so = new SerializedObject(data);
            so.FindProperty("countryId").stringValue = id;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("region").enumValueIndex = (int)region;
            so.FindProperty("flag").objectReferenceValue = WorldTourBuilder.Flag(flagId);
            so.FindProperty("arenaName").stringValue = name + " Arena";
            so.FindProperty("environmentLabel").stringValue = arena.source == ArenaSource.Scene ? "Scene arena (test)" : "Prefab arena (test)";
            so.FindProperty("description").stringValue = "Temporary configuration used to test arena loading, unloading, unlocks, traffic and weather data.";
            so.FindProperty("missionLabel").stringValue = "TEST";
            so.FindProperty("preview").objectReferenceValue = WorldTourBuilder.Preview(level);
            SerializedProperty levels = so.FindProperty("levels");
            levels.arraySize = 1;
            levels.GetArrayElementAtIndex(0).objectReferenceValue = level;
            so.FindProperty("arena").boxedValue = arena;
            so.FindProperty("unlock").boxedValue = unlock;
            so.FindProperty("roadDirection").enumValueIndex = (int)road;
            so.FindProperty("traffic").boxedValue = traffic;
            so.FindProperty("weather").boxedValue = weather;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }
    }
}
