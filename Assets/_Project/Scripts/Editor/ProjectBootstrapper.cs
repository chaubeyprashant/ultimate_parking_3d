using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Core;
using UltimateCarParking.DebugTools;
using UltimateCarParking.Levels;
using UltimateCarParking.Parking;
using UltimateCarParking.Progression;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Generates the Phase 1 project content: player/URP settings, materials, the car prefab and the five scenes.
    /// Re-runnable: everything it makes is overwritten in place.
    /// Menu: Tools > Ultimate Car Parking > Bootstrap Project.
    /// Batch: -executeMethod UltimateCarParking.EditorTools.ProjectBootstrapper.BootstrapAll
    /// </summary>
    public static class ProjectBootstrapper
    {
        public const string Company = "Ergebins";
        public const string Product = "Ultimate Car Parking 3D";
        public const string PackageId = "com.ergebins.ultimatecarparking3d";

        private const string Root = "Assets/_Project";
        public const string ScenesDir = Root + "/Scenes";
        public const string PrefabsDir = Root + "/Prefabs/Vehicles";
        public const string MaterialsDir = Root + "/Materials";
        public const string SettingsDir = Root + "/Settings";
        public const string CarPrefabPath = PrefabsDir + "/PlayerCar.prefab";
        public const string EnvironmentsDir = Root + "/Prefabs/Environments";
        public const string TestLotPrefabPath = EnvironmentsDir + "/Env_TestLot.prefab";
        public const string LevelsDir = Root + "/Levels";
        public const string CatalogPath = LevelsDir + "/LevelCatalog.asset";
        public const string TestLevelsDir = LevelsDir + "/Test";
        public const string TestCatalogPath = TestLevelsDir + "/TestCatalog.asset";

        private static readonly string[] SceneOrder =
            { SceneNames.Bootstrap, SceneNames.MainMenu, SceneNames.Garage, SceneNames.LevelSelect, SceneNames.Gameplay };

        public static string[] ShippedScenes => SceneOrder.Select(ScenePath).ToArray();
        private static string ScenePath(string scene) => $"{ScenesDir}/{scene}.unity";
        public static string ScenePathOf(string scene) => ScenePath(scene);

        [MenuItem("Tools/Ultimate Car Parking/Bootstrap Project")]
        public static void BootstrapAll()
        {
            CreateFolders();
            ConfigurePlayer();
            ConfigureUrp();
            UiAssets.GenerateAll();          // TMP essentials, fonts, sprites (first: the UI scenes need them)
            AudioSetup.BuildAll();           // clip import settings + Resources/AudioLibrary
            CreateMaterials();
            CarFactory.BuildAll();           // 5 cars + CarData + CarCatalog
            BuildEnvironmentPrefab();        // Phase 2 test lot (test fixture)
            CreateTestLevels();
            EnvironmentKit.BuildAll();       // modules + 3 themes
            LevelDesigner.BuildAll();        // 30 campaign levels + LevelCatalog
            BuildScenes();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            bool ok = Verify();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("Tools/Ultimate Car Parking/Rebuild Car Prefab")]
        public static void RebuildCarPrefab()
        {
            CreateMaterials();
            CarFactory.BuildAll();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ settings

        private static void CreateFolders()
        {
            foreach (string dir in new[]
                     {
                         ScenesDir, PrefabsDir, MaterialsDir, SettingsDir, EnvironmentsDir, LevelsDir, TestLevelsDir,
                         CarFactory.CarsDir, EnvironmentKit.ModulesDir, EnvironmentKit.ThemesDir, Root + "/Prefabs/UI", UiAssets.SpritesDir,
                         Root + "/Art/Models", Root + "/Audio", Root + "/UI",
                     })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = Product;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            // Input System package only (the legacy Input Manager is not used anywhere).
            Object playerSettingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (playerSettingsAsset != null)
            {
                var so = new SerializedObject(playerSettingsAsset);
                SerializedProperty handler = so.FindProperty("activeInputHandler");
                if (handler != null)
                {
                    handler.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        /// <summary>Lightweight mobile URP: forward, no HDR, 2x MSAA, one 1024 shadow cascade, no extra lights.</summary>
        public static void ConfigureUrp()
        {
            string rendererPath = SettingsDir + "/URP_Mobile_Renderer.asset";
            string assetPath = SettingsDir + "/URP_Mobile.asset";

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            renderer.renderingMode = RenderingMode.Forward;
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            EditorUtility.SetDirty(renderer);

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            var so = new SerializedObject(asset);
            so.FindProperty("m_RendererDataList").arraySize = 1;
            so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            SetIfPresent(so, "m_SoftShadowsSupported", false);
            SerializedProperty additional = so.FindProperty("m_AdditionalLightsRenderingMode");
            if (additional != null) additional.intValue = (int)LightRenderingMode.Disabled;
            so.ApplyModifiedPropertiesWithoutUndo();

            asset.supportsHDR = false;
            asset.msaaSampleCount = 2;
            asset.renderScale = 1f;
            asset.shadowDistance = 45f;
            asset.shadowCascadeCount = 1;
            asset.mainLightShadowmapResolution = 1024;
            asset.shadowDepthBias = 1f;
            asset.shadowNormalBias = 1f;
            asset.supportsCameraDepthTexture = false;
            asset.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        private static void SetIfPresent(SerializedObject so, string property, bool value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.boolValue = value;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = SceneOrder
                .Select(s => new EditorBuildSettingsScene(ScenePath(s), true))
                .ToArray();
        }

        // ------------------------------------------------------------------ materials

        private static readonly Dictionary<string, (Color color, float smoothness, float metallic, Color emission)> MaterialSpecs = new()
        {
            { "CarPaint", (new Color(0.78f, 0.12f, 0.10f), 0.7f, 0.3f, Color.black) },
            { "CarGlass", (new Color(0.08f, 0.10f, 0.13f), 0.9f, 0.0f, Color.black) },
            { "CarTrim", (new Color(0.12f, 0.12f, 0.12f), 0.3f, 0.0f, Color.black) },
            { "Tire", (new Color(0.05f, 0.05f, 0.05f), 0.15f, 0.0f, Color.black) },
            { "Rim", (new Color(0.70f, 0.70f, 0.72f), 0.6f, 0.8f, Color.black) },
            { "Headlight", (new Color(1.00f, 0.97f, 0.85f), 0.9f, 0.0f, new Color(0.8f, 0.78f, 0.65f)) },
            { "Taillight", (new Color(0.70f, 0.02f, 0.02f), 0.8f, 0.0f, new Color(0.5f, 0.0f, 0.0f)) },
            { "Asphalt", (new Color(0.20f, 0.20f, 0.21f), 0.15f, 0.0f, Color.black) },
            { "Grass", (new Color(0.24f, 0.38f, 0.18f), 0.05f, 0.0f, Color.black) },
            { "LineWhite", (new Color(0.92f, 0.92f, 0.90f), 0.2f, 0.0f, Color.black) },
            { "LineTarget", (new Color(1.00f, 0.78f, 0.10f), 0.2f, 0.0f, new Color(0.25f, 0.18f, 0.0f)) },
            { "Concrete", (new Color(0.62f, 0.61f, 0.58f), 0.2f, 0.0f, Color.black) },
            { "Curb", (new Color(0.75f, 0.74f, 0.70f), 0.2f, 0.0f, Color.black) },
            { "Cone", (new Color(1.00f, 0.40f, 0.05f), 0.4f, 0.0f, Color.black) },
            { "Barrier", (new Color(0.85f, 0.70f, 0.10f), 0.3f, 0.0f, Color.black) },
            { "ParkedBlue", (new Color(0.15f, 0.30f, 0.65f), 0.7f, 0.3f, Color.black) },
            { "ParkedSilver", (new Color(0.65f, 0.66f, 0.68f), 0.7f, 0.5f, Color.black) },
            { "ParkedWhite", (new Color(0.88f, 0.88f, 0.86f), 0.7f, 0.2f, Color.black) },
            { "ParkedGreen", (new Color(0.15f, 0.42f, 0.28f), 0.7f, 0.3f, Color.black) },
        };

        internal static void CreateMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("[Bootstrap] URP Lit shader not found; is the URP package installed?");
                return;
            }

            foreach (var (name, spec) in MaterialSpecs)
            {
                string path = $"{MaterialsDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(lit);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = lit;
                mat.SetColor("_BaseColor", spec.color);
                mat.SetFloat("_Smoothness", spec.smoothness);
                mat.SetFloat("_Metallic", spec.metallic);
                if (spec.emission.maxColorComponent > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", spec.emission);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                }
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        internal static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{name}.mat");

        // ------------------------------------------------------------------ scenes

        private static void BuildScenes()
        {
            BuildBootstrapScene();
            BuildGameplayScene();
            UiSceneBuilder.BuildAll();       // MainMenu, LevelSelect, Garage
        }

        private static void BuildBootstrapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.gameObject.tag = "MainCamera";
            cam.gameObject.AddComponent<AudioListener>();
            new GameObject("Bootstrap").AddComponent<BootstrapLoader>();
            EditorSceneManager.SaveScene(scene, ScenePath(SceneNames.Bootstrap));
        }

        /// <summary>
        /// Gameplay is a shell: sun, chase camera and the GameSystems object (LevelManager, ParkingValidator,
        /// LevelLoader, debug overlay). The world, car and target bay are spawned by the LevelLoader from LevelData.
        /// </summary>
        private static void BuildGameplayScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.46f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.21f, 0.20f);
            AddSun();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = 55f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<VehicleCamera>();
            var fso = new SerializedObject(follow);
            fso.FindProperty("findTargetIfMissing").boolValue = false;   // the LevelLoader assigns the target
            fso.ApplyModifiedPropertiesWithoutUndo();
            camGo.transform.SetPositionAndRotation(new Vector3(-27f, 3f, 0f), Quaternion.Euler(15f, 90f, 0f));

            var systems = new GameObject("GameSystems");
            var validator = systems.AddComponent<ParkingValidator>();
            var manager = systems.AddComponent<LevelManager>();
            var loader = systems.AddComponent<LevelLoader>();
            var progression = systems.AddComponent<ProgressionRecorder>();
            var overlay = systems.AddComponent<LevelDebugOverlay>();
            var pso = new SerializedObject(progression);
            pso.FindProperty("levelManager").objectReferenceValue = manager;
            pso.ApplyModifiedPropertiesWithoutUndo();

            // Assets loaded after NewScene so the references survive the scene switch.
            var mso = new SerializedObject(manager);
            mso.FindProperty("validator").objectReferenceValue = validator;
            mso.FindProperty("loader").objectReferenceValue = loader;
            mso.ApplyModifiedPropertiesWithoutUndo();

            var lso = new SerializedObject(loader);
            lso.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            lso.FindProperty("vehiclePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            lso.FindProperty("carCatalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UltimateCarParking.Cars.CarCatalog>(CarFactory.CarCatalogPath);
            lso.FindProperty("zoneMarkerMaterial").objectReferenceValue = Mat("LineTarget");
            lso.FindProperty("levelManager").objectReferenceValue = manager;
            lso.FindProperty("vehicleCamera").objectReferenceValue = follow;
            lso.ApplyModifiedPropertiesWithoutUndo();

            var oso = new SerializedObject(overlay);
            oso.FindProperty("levelManager").objectReferenceValue = manager;
            oso.FindProperty("validator").objectReferenceValue = validator;
            oso.FindProperty("progression").objectReferenceValue = progression;
            oso.ApplyModifiedPropertiesWithoutUndo();

            UiSceneBuilder.BuildGameplayUi(manager, validator, progression, loader, follow);   // HUD, controls, panels, guide
            EditorSceneManager.SaveScene(scene, ScenePath(SceneNames.Gameplay));
        }

        /// <summary>The test parking lot as a reusable environment prefab (static scenery + loose cones).</summary>
        private static void BuildEnvironmentPrefab()
        {
            var root = new GameObject("Env_TestLot");
            BuildParkingLot(root.transform);

            // Loose cones are dynamic so the car can knock them over.
            Transform cones = Child(root.transform, "Cones");
            for (int i = 0; i < 6; i++) Cone(cones, new Vector3(-6f + i * 2.4f, 0f, -3.2f));
            for (int i = 0; i < 4; i++) Cone(cones, new Vector3(14f, 0f, -1.5f + i * 1.2f));

            PrefabUtility.SaveAsPrefabAsset(root, TestLotPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[Bootstrap] Environment prefab written: {TestLotPrefabPath}");
        }

        /// <summary>
        /// The two Phase 2 levels in the original test lot, kept as test fixtures (ids T01/T02, own catalog);
        /// the automated parking tests depend on their exact layout.
        /// </summary>
        private static void CreateTestLevels()
        {
            var env = AssetDatabase.LoadAssetAtPath<GameObject>(TestLotPrefabPath);

            // Level 1: drive down the aisle, nose into the empty north bay (row 0, bay 7).
            LevelData l1 = WriteLevel("Level_T01", "T01", "Test 1 - First Bay", env,
                spawn: new Vector3(-20f, 0f, 0f), spawnYaw: 90f,
                target: new Vector3(4.2f, 0f, 9.25f), targetYaw: 0f,
                timeLimit: 90f, collisionLimit: 3, threeStar: (40f, 0), twoStar: (65f, 1), coins: (100, 50),
                rules: null);

            // Level 2: from the east end, through the barrier chicane, into a south bay with tighter rules.
            LevelData l2 = WriteLevel("Level_T02", "T02", "Test 2 - Chicane", env,
                spawn: new Vector3(31f, 0f, 0f), spawnYaw: 270f,
                target: new Vector3(-4.2f, 0f, -9.25f), targetYaw: 180f,
                timeLimit: 75f, collisionLimit: 2, threeStar: (45f, 0), twoStar: (60f, 1), coins: (150, 60),
                rules: new ParkingRules { edgeMargin = 0.05f, angleTolerance = 7f, allowEitherDirection = true, maxSpeedKmh = 1.5f, holdDuration = 2f });

            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(TestCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, TestCatalogPath);
            }
            var cso = new SerializedObject(catalog);
            SerializedProperty levels = cso.FindProperty("levels");
            levels.arraySize = 2;
            levels.GetArrayElementAtIndex(0).objectReferenceValue = l1;
            levels.GetArrayElementAtIndex(1).objectReferenceValue = l2;
            cso.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static LevelData WriteLevel(string file, string id, string displayName, GameObject env, Vector3 spawn, float spawnYaw,
            Vector3 target, float targetYaw, float timeLimit, int collisionLimit, (float time, int collisions) threeStar,
            (float time, int collisions) twoStar, (int baseCoins, int perStar) coins, ParkingRules rules)
        {
            string path = $"{TestLevelsDir}/{file}.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, path);
            }
            var so = new SerializedObject(level);
            so.FindProperty("levelId").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("environmentPrefab").objectReferenceValue = env;
            so.FindProperty("spawnPosition").vector3Value = spawn;
            so.FindProperty("spawnYaw").floatValue = spawnYaw;
            so.FindProperty("targetPosition").vector3Value = target;
            so.FindProperty("targetYaw").floatValue = targetYaw;
            so.FindProperty("targetSize").vector2Value = new Vector2(2.8f, 5.5f);
            so.FindProperty("timeLimitSeconds").floatValue = timeLimit;
            so.FindProperty("collisionLimit").intValue = collisionLimit;
            so.FindProperty("threeStarTime").floatValue = threeStar.time;
            so.FindProperty("threeStarMaxCollisions").intValue = threeStar.collisions;
            so.FindProperty("twoStarTime").floatValue = twoStar.time;
            so.FindProperty("twoStarMaxCollisions").intValue = twoStar.collisions;
            so.FindProperty("baseCoins").intValue = coins.baseCoins;
            so.FindProperty("coinsPerStar").intValue = coins.perStar;
            so.FindProperty("overrideParkingRules").boolValue = rules != null;
            if (rules != null)
            {
                SerializedProperty r = so.FindProperty("parkingRules");
                r.FindPropertyRelative("edgeMargin").floatValue = rules.edgeMargin;
                r.FindPropertyRelative("angleTolerance").floatValue = rules.angleTolerance;
                r.FindPropertyRelative("allowEitherDirection").boolValue = rules.allowEitherDirection;
                r.FindPropertyRelative("maxSpeedKmh").floatValue = rules.maxSpeedKmh;
                r.FindPropertyRelative("holdDuration").floatValue = rules.holdDuration;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            return level;
        }

        /// <summary>
        /// 70 x 60 m lot: two rows of 2.8 x 5.5 m bays facing a 13 m aisle, parked cars with four bays left empty
        /// (the level's ParkingZone marks the target), pillars, barriers, a curb strip and walls.
        /// </summary>
        private static void BuildParkingLot(Transform env)
        {
            Primitive(PrimitiveType.Plane, env, "Grass", new Vector3(0f, -0.02f, 0f), Quaternion.identity, new Vector3(30f, 1f, 30f), "Grass");
            Primitive(PrimitiveType.Plane, env, "LotGround", Vector3.zero, Quaternion.identity, new Vector3(7f, 1f, 6f), "Asphalt");

            const float bayWidth = 2.8f, bayLength = 5.5f, rowZ = 6.5f;
            const int bays = 12;
            float firstX = -bays * bayWidth * 0.5f;
            Transform lines = Child(env, "Lines");
            Transform parked = Child(env, "ParkedCars");
            string[] paints = { "ParkedBlue", "ParkedSilver", "ParkedWhite", "ParkedGreen" };
            int targetBay = 7;

            for (int row = 0; row < 2; row++)
            {
                float sign = row == 0 ? 1f : -1f;
                float bayCenterZ = sign * (rowZ + bayLength * 0.5f);
                for (int b = 0; b <= bays; b++)
                {
                    float x = firstX + b * bayWidth;
                    Box(lines, $"Line_{row}_{b}", new Vector3(x, 0.006f, bayCenterZ), new Vector3(0.12f, 0.01f, bayLength), "LineWhite", collider: false);
                }
                Box(lines, $"BackLine_{row}", new Vector3(0f, 0.006f, sign * (rowZ + bayLength)), new Vector3(bays * bayWidth, 0.01f, 0.12f), "LineWhite", collider: false);
                Box(env, $"WheelStopRow{row}", new Vector3(0f, 0.08f, sign * (rowZ + bayLength + 0.6f)), new Vector3(bays * bayWidth, 0.16f, 0.3f), "Curb");

                for (int b = 0; b < bays; b++)
                {
                    float x = firstX + (b + 0.5f) * bayWidth;
                    bool empty = (row == 0 && (b == targetBay || b == 2)) || (row == 1 && (b == 4 || b == 9));
                    if (empty) continue;
                    ParkedCar(parked, new Vector3(x, 0f, bayCenterZ), row == 0 ? 0f : 180f, paints[(b + row) % paints.Length]);
                }
            }

            // Aisle obstacles: pillars, a jersey-barrier chicane, a low curb strip to drive over.
            Transform obstacles = Child(env, "Obstacles");
            foreach (float x in new[] { -12f, 0f, 12f })
            {
                Primitive(PrimitiveType.Cylinder, obstacles, $"Pillar{x}", new Vector3(x, 1.5f, rowZ - 0.6f), Quaternion.identity, new Vector3(0.6f, 1.5f, 0.6f), "Concrete");
                Primitive(PrimitiveType.Cylinder, obstacles, $"PillarS{x}", new Vector3(x, 1.5f, -rowZ + 0.6f), Quaternion.identity, new Vector3(0.6f, 1.5f, 0.6f), "Concrete");
            }
            Box(obstacles, "BarrierA", new Vector3(22f, 0.45f, 2.5f), new Vector3(0.6f, 0.9f, 4f), "Barrier");
            Box(obstacles, "BarrierB", new Vector3(27f, 0.45f, -2.5f), new Vector3(0.6f, 0.9f, 4f), "Barrier");
            Box(obstacles, "CurbStrip", new Vector3(-28f, 0.07f, 0f), new Vector3(0.4f, 0.14f, 8f), "Curb");

            // Perimeter walls.
            Transform walls = Child(env, "Walls");
            Box(walls, "WallNorth", new Vector3(0f, 0.6f, 30f), new Vector3(70f, 1.2f, 0.5f), "Concrete");
            Box(walls, "WallSouth", new Vector3(0f, 0.6f, -30f), new Vector3(70f, 1.2f, 0.5f), "Concrete");
            Box(walls, "WallEast", new Vector3(35f, 0.6f, 0f), new Vector3(0.5f, 1.2f, 60f), "Concrete");
            Box(walls, "WallWest", new Vector3(-35f, 0.6f, 0f), new Vector3(0.5f, 1.2f, 60f), "Concrete");
        }

        /// <summary>Static obstacle car: two boxes, one collider, no Rigidbody.</summary>
        internal static void ParkedCar(Transform parent, Vector3 pos, float yaw, string paint)
        {
            Transform car = Child(parent, "ParkedCar");
            car.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            Box(car, "Body", new Vector3(0f, 0.65f, 0f), new Vector3(1.8f, 0.7f, 4.3f), paint, collider: false);
            Box(car, "Cabin", new Vector3(0f, 1.22f, -0.2f), new Vector3(1.6f, 0.5f, 2.2f), "CarGlass", collider: false);
            foreach (float sx in new[] { -0.8f, 0.8f })
                foreach (float sz in new[] { -1.35f, 1.35f })
                    Primitive(PrimitiveType.Cylinder, car, "Wheel", new Vector3(sx, 0.34f, sz), Quaternion.Euler(0f, 0f, 90f),
                        new Vector3(0.68f, 0.11f, 0.68f), "Tire", collider: false);
            var col = car.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.75f, 0f);
            col.size = new Vector3(1.8f, 1.3f, 4.3f);
        }

        internal static void Cone(Transform parent, Vector3 pos)
        {
            GameObject cone = Primitive(PrimitiveType.Cylinder, parent, "Cone", pos + Vector3.up * 0.35f, Quaternion.identity,
                new Vector3(0.35f, 0.35f, 0.35f), "Cone").gameObject;
            var rb = cone.AddComponent<Rigidbody>();
            rb.mass = 3f;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        internal static void AddSun()
        {
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.sun = sun;
        }

        // ------------------------------------------------------------------ helpers

        internal static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>A cube. Solid by default; parts of the player car never get colliders (it has its own).</summary>
        internal static Transform Box(Transform parent, string name, Vector3 localPos, Vector3 size, string material, bool collider = true)
            => Primitive(PrimitiveType.Cube, parent, name, localPos, Quaternion.identity, size, material, collider);

        /// <summary>Visual parts of a drivable car (under "Model" or "WheelMeshes") never get colliders.</summary>
        private static bool IsCarPart(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name == "Model" || t.name == "WheelMeshes") return true;
            return false;
        }

        internal static Transform Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPos, Quaternion localRot,
            Vector3 scale, string material, bool collider = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPos, localRot);
            go.transform.localScale = scale;
            if (!collider || IsCarPart(parent)) Object.DestroyImmediate(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            Material m = Mat(material);
            if (m != null) mr.sharedMaterial = m;
            else Debug.LogWarning($"[Bootstrap] Material '{material}' missing for {name}.");
            return go.transform;
        }

        internal static void AddBoxCollider(Transform parent, string name, Vector3 center, Vector3 size)
        {
            Transform t = Child(parent, name);
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        // ------------------------------------------------------------------ verification

        /// <summary>Re-reads what was generated and checks the wiring. Logs "[Bootstrap] VERIFY OK" on success.</summary>
        public static bool Verify()
        {
            var problems = new List<string>();
            void Check(bool condition, string what) { if (!condition) problems.Add(what); }

            Check(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset, "URP asset assigned");
            Check(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) == PackageId, "Android package id");
            Check(!PlayerSettings.allowedAutorotateToPortrait, "landscape only");
            foreach (string s in ShippedScenes) Check(File.Exists(s), $"scene {s}");
            Check(EditorBuildSettings.scenes.Length == SceneOrder.Length && EditorBuildSettings.scenes[0].path == ScenePath(SceneNames.Bootstrap),
                "build settings scene list");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            Check(prefab != null, "car prefab exists");
            if (prefab != null)
            {
                var controller = prefab.GetComponent<VehicleController>();
                Check(controller != null, "VehicleController on car");
                Check(prefab.GetComponent<VehicleInput>() != null, "VehicleInput on car");
                Check(prefab.GetComponentsInChildren<WheelCollider>().Length == 4, "4 WheelColliders");
                if (controller != null)
                {
                    var so = new SerializedObject(controller);
                    foreach (string p in new[] { "frontLeft", "frontRight", "rearLeft", "rearRight", "input" })
                        Check(so.FindProperty(p).objectReferenceValue != null, $"VehicleController.{p} wired");
                }
                var visuals = prefab.GetComponent<WheelVisuals>();
                Check(visuals != null && visuals.WheelCount == 4, "WheelVisuals has 4 wheels");
                if (visuals != null)
                {
                    SerializedProperty wheels = new SerializedObject(visuals).FindProperty("wheels");
                    for (int i = 0; i < wheels.arraySize; i++)
                    {
                        SerializedProperty e = wheels.GetArrayElementAtIndex(i);
                        Check(e.FindPropertyRelative("collider").objectReferenceValue != null && e.FindPropertyRelative("visual").objectReferenceValue != null,
                            $"WheelVisuals wheel {i} wired");
                    }
                }
            }

            if (prefab != null) Check(prefab.GetComponent<CollisionHandler>() != null, "CollisionHandler on car");
            Check(AssetDatabase.LoadAssetAtPath<GameObject>(TestLotPrefabPath) != null, "test lot environment prefab");
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            Check(catalog != null && catalog.Count == LevelDesigner.LevelCount, $"level catalog with {LevelDesigner.LevelCount} levels");
            if (catalog != null)
                for (int i = 0; i < catalog.Count; i++)
                {
                    LevelData l = catalog.Get(i);
                    Check(l != null && l.Theme != null && l.Props.Length > 10 && l.LevelNumber == i + 1, $"level {i + 1} configured");
                    if (l != null) foreach (var prop in l.Props) if (prop.prefab == null) { problems.Add($"level {i + 1} has a placement without prefab"); break; }
                }
            var testCatalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(TestCatalogPath);
            Check(testCatalog != null && testCatalog.Count == 2, "test catalog with 2 levels");
            var cars = AssetDatabase.LoadAssetAtPath<UltimateCarParking.Cars.CarCatalog>(CarFactory.CarCatalogPath);
            Check(cars != null && cars.Count == 5, "car catalog with 5 cars");
            if (cars != null)
                for (int i = 0; i < cars.Count; i++)
                {
                    var c = cars.Get(i);
                    Check(c != null && c.ModelPrefab != null && c.ModelPrefab.GetComponent<VehicleController>() != null
                          && c.ModelPrefab.GetComponent<UltimateCarParking.Cars.CarPaint>().RendererCount > 0 && c.PaintMaterial != null, $"car {i} configured");
                }
            Check(UiAssets.TmpReady && UiAssets.HeadingFont != null && UiAssets.BodyFont != null, "TMP settings + font assets");
            Check(AudioSetup.LibraryComplete(), "audio library has every clip");
            foreach (string sprite in new[] { "RoundRect", "Pill", "Star", "Lock", "Coin", "ChevronLeft", "SideFade", "Pause", "Gear", "Vignette" })
                Check(UiAssets.Sprite(sprite) != null, $"sprite {sprite}");
            Check(AssetDatabase.LoadAssetAtPath<UltimateCarParking.UI.LevelCard>(UiSceneBuilder.LevelCardPrefabPath) != null, "LevelCard prefab");

            Scene gameplay = EditorSceneManager.OpenScene(ScenePath(SceneNames.Gameplay), OpenSceneMode.Single);
            Check(gameplay.IsValid(), "Gameplay scene opens");
            Check(Object.FindObjectsByType<LevelManager>().Length == 1, "exactly one LevelManager in Gameplay");
            var loaderInScene = Object.FindAnyObjectByType<LevelLoader>();
            Check(loaderInScene != null, "LevelLoader in Gameplay");
            if (loaderInScene != null)
            {
                var lso = new SerializedObject(loaderInScene);
                foreach (string p in new[] { "catalog", "carCatalog", "vehiclePrefab", "zoneMarkerMaterial", "levelManager", "vehicleCamera" })
                    Check(lso.FindProperty(p).objectReferenceValue != null, $"LevelLoader.{p} wired");
            }
            var managerInScene = Object.FindAnyObjectByType<LevelManager>();
            if (managerInScene != null)
            {
                var mso = new SerializedObject(managerInScene);
                Check(mso.FindProperty("validator").objectReferenceValue != null, "LevelManager.validator wired");
            }
            Check(Object.FindAnyObjectByType<VehicleController>() == null, "Gameplay has no baked car (the loader spawns it)");
            Check(Object.FindAnyObjectByType<ProgressionRecorder>() != null, "ProgressionRecorder in Gameplay");
            foreach (System.Type t in new[] { typeof(UltimateCarParking.UI.GameplayUiController), typeof(UltimateCarParking.UI.GameplayHud),
                         typeof(UltimateCarParking.UI.MobileControls), typeof(UltimateCarParking.UI.ParkingGuide), typeof(UltimateCarParking.UI.SettingsPanel) })
            {
                var comp = Object.FindAnyObjectByType(t, FindObjectsInactive.Include) as MonoBehaviour;
                Check(comp != null, $"Gameplay {t.Name}");
                if (comp == null) continue;
                SerializedProperty it = new SerializedObject(comp).GetIterator();
                for (bool enter = true; it.NextVisible(enter); enter = false)
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.name != "m_Script" && it.objectReferenceValue == null)
                        problems.Add($"Gameplay: {t.Name}.{it.name} not wired");
            }

            foreach (var (sceneName, type) in new[] { (SceneNames.MainMenu, typeof(UltimateCarParking.UI.MainMenuController)),
                         (SceneNames.LevelSelect, typeof(UltimateCarParking.UI.LevelSelectController)), (SceneNames.Garage, typeof(UltimateCarParking.UI.GarageController)) })
            {
                EditorSceneManager.OpenScene(ScenePath(sceneName), OpenSceneMode.Single);
                var ctrl = Object.FindAnyObjectByType(type) as MonoBehaviour;
                Check(ctrl != null, $"{sceneName} controller");
                if (ctrl == null) continue;
                var cso = new SerializedObject(ctrl);
                SerializedProperty it = cso.GetIterator();
                for (bool enter = true; it.NextVisible(enter); enter = false)
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.name != "m_Script" && it.objectReferenceValue == null)
                        problems.Add($"{sceneName}: {type.Name}.{it.name} not wired");
                Check(Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null, $"{sceneName} EventSystem");
                Check(Object.FindObjectsByType<AudioListener>().Length == 1, $"{sceneName} has exactly one AudioListener");
            }

            if (problems.Count == 0) Debug.Log("[Bootstrap] VERIFY OK");
            else Debug.LogError("[Bootstrap] VERIFY FAILED:\n - " + string.Join("\n - ", problems));
            return problems.Count == 0;
        }
    }
}
