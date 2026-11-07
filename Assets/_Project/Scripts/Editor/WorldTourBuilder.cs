using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UltimateCarParking.Environment;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Builds the World Tour data: 30 <see cref="CountryData"/> assets (flag, map position, arena text, preview) and
    /// the <see cref="CountryCatalog"/>. Campaign levels 1–30 map to countries 1–30 in tour order (one mission each)
    /// until country-specific arenas exist; the arena label always names the environment the level really uses.
    /// Previews are rendered from each level's actual environment, props and target bay.
    /// Inputs (already in the project): ThirdParty/Flags_flag-icons (MIT) and ThirdParty/NaturalEarth (public domain).
    /// </summary>
    public static class WorldTourBuilder
    {
        public const string Dir = "Assets/_Project/WorldTour";
        public const string CountriesDir = Dir + "/Countries";
        public const string CatalogPath = Dir + "/CountryCatalog.asset";
        public const string PreviewsDir = "Assets/_Project/Art/WorldTour/Previews";
        public const string FlagsDir = "Assets/_Project/ThirdParty/Flags_flag-icons";
        public const string MapPath = "Assets/_Project/ThirdParty/NaturalEarth/world_map.json";
        private const int PreviewW = 1280, PreviewH = 720;

        /// <summary>Single in-game frame for store art (1920×1080 by default).</summary>
        public static void ExportLevelScreenshot(LevelData level, string filePath, int width = 1920, int height = 1080)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, hideFlags = HideFlags.HideAndDontSave };
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(RealCarFactory.PrefabPath);
            if (car == null) car = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectBootstrapper.CarPrefabPath);
            var root = new GameObject("StoreShotRoot");
            try
            {
                RenderLevelInto(root.transform, level, car, rt, tex);
                File.WriteAllBytes(filePath, tex.EncodeToJPG(92));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>Named camera angle used in <see cref="RenderShots"/>.</summary>
        public static void ExportLevelView(LevelData level, string viewName, string filePath, int width = 1920, int height = 1080)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, hideFlags = HideFlags.HideAndDontSave };
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(RealCarFactory.PrefabPath);
            var root = new GameObject("StoreShotRoot");
            try
            {
                Vector3 t = level.TargetPosition, sp = level.SpawnPosition;
                Vector3 mid = (t + sp) * 0.5f;
                float span = Mathf.Max(20f, Vector3.Distance(t, sp));
                Vector3 bayBack = level.TargetRotation * Vector3.back;
                var views = new List<(string name, Vector3 from, Vector3 look)>
                {
                    ("overview", mid + new Vector3(-span * 0.35f, span * 0.9f, -span * 0.55f), mid),
                    ("target", t - bayBack * -9f + Vector3.up * 6f + level.TargetRotation * Vector3.right * 4f, t),
                    ("driver", sp + level.SpawnRotation * new Vector3(0f, 2.3f, -6.5f), sp + level.SpawnRotation * new Vector3(0f, 1f, 12f)),
                };
                var pick = views.Find(v => v.name == viewName);
                if (pick.name == null) pick = views[0];
                RenderLevelInto(root.transform, level, car, rt, tex, new List<(string, Vector3, Vector3)> { pick }, null);
                File.WriteAllBytes(filePath, tex.EncodeToJPG(92));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>Tour order = unlock order. (id, name, region, lat, lon, arena city, one-line intro)</summary>
        public static readonly (string id, string name, WorldRegion region, float lat, float lon, string city, string intro)[] Countries =
        {
            ("in", "India", WorldRegion.Asia, 28.6f, 77.2f, "New Delhi", "The World Tour starts in New Delhi."),
            ("jp", "Japan", WorldRegion.Asia, 36.2f, 138.3f, "Tokyo", "Tokyo drivers park with millimetre precision."),
            ("cn", "China", WorldRegion.Asia, 39.9f, 116.4f, "Beijing", "Beijing's car parks fill up fast."),
            ("kr", "South Korea", WorldRegion.Asia, 36.5f, 127.8f, "Seoul", "Seoul's tight bays reward careful driving."),
            ("ae", "UAE", WorldRegion.AfricaMiddleEast, 23.8f, 54.4f, "Dubai", "Dubai's lots are wide, but the clock is ticking."),
            ("th", "Thailand", WorldRegion.Asia, 15.0f, 101.0f, "Bangkok", "Bangkok traffic waits for nobody."),
            ("sg", "Singapore", WorldRegion.Asia, 1.35f, 103.82f, "Singapore", "Singapore runs on order and precision."),
            ("my", "Malaysia", WorldRegion.Asia, 2.4f, 112.6f, "Kuching", "Kuching's riverside lots test your patience."),
            ("id", "Indonesia", WorldRegion.Asia, -7.3f, 110.0f, "Jakarta", "Jakarta's crowded streets leave little room."),
            ("sa", "Saudi Arabia", WorldRegion.AfricaMiddleEast, 23.8f, 45.0f, "Riyadh", "Riyadh's car parks stretch wide under the sun."),
            ("gb", "United Kingdom", WorldRegion.Europe, 52.5f, -1.5f, "London", "London's bays are narrow and unforgiving."),
            ("fr", "France", WorldRegion.Europe, 47.0f, 2.5f, "Paris", "In Paris every centimetre counts."),
            ("de", "Germany", WorldRegion.Europe, 52.4f, 13.0f, "Berlin", "Berlin expects every car exactly in line."),
            ("it", "Italy", WorldRegion.Europe, 42.8f, 12.5f, "Rome", "Rome's tight gaps call for quick hands."),
            ("es", "Spain", WorldRegion.Europe, 40.2f, -3.7f, "Madrid", "Madrid's car parks never sleep."),
            ("ru", "Russia", WorldRegion.Europe, 55.75f, 37.6f, "Moscow", "Moscow's wide lots hide narrow gaps."),
            ("nl", "Netherlands", WorldRegion.Europe, 52.2f, 5.6f, "Amsterdam", "Amsterdam's spaces are made for small cars."),
            ("ch", "Switzerland", WorldRegion.Europe, 46.8f, 8.2f, "Zurich", "Zurich's precision standards apply to parking too."),
            ("gr", "Greece", WorldRegion.Europe, 39.5f, 22.0f, "Athens", "Athens leaves little room to manoeuvre."),
            ("us", "USA", WorldRegion.Americas, 39.5f, -98.0f, "New York", "New York parking is a contact sport. Stay clean."),
            ("ca", "Canada", WorldRegion.Americas, 53.0f, -110.0f, "Toronto", "Toronto's lots are wide but crowded."),
            ("mx", "Mexico", WorldRegion.Americas, 23.6f, -102.5f, "Mexico City", "Mexico City's lots are packed and lively."),
            ("br", "Brazil", WorldRegion.Americas, -14.0f, -51.0f, "São Paulo", "São Paulo's garages are busy around the clock."),
            ("ar", "Argentina", WorldRegion.Americas, -36.0f, -64.0f, "Buenos Aires", "Buenos Aires rewards confident reversing."),
            ("ng", "Nigeria", WorldRegion.AfricaMiddleEast, 9.0f, 7.5f, "Lagos", "Lagos traffic leaves no margin for error."),
            ("za", "South Africa", WorldRegion.AfricaMiddleEast, -29.0f, 25.0f, "Johannesburg", "Johannesburg's lots are big and busy."),
            ("eg", "Egypt", WorldRegion.AfricaMiddleEast, 26.8f, 30.8f, "Cairo", "Cairo's lots buzz day and night."),
            ("au", "Australia", WorldRegion.Oceania, -25.3f, 133.8f, "Sydney", "Sydney's car parks need steady hands."),
            ("nz", "New Zealand", WorldRegion.Oceania, -38.6f, 176.0f, "Auckland", "Auckland is the last stop before the finale."),
            ("tr", "Turkey", WorldRegion.Europe, 39.0f, 35.0f, "Istanbul", "Istanbul is the grand finale of the World Tour."),
        };

        /// <summary>Tour countries that drive on the left.</summary>
        public static readonly string[] LeftHandTraffic = { "in", "jp", "th", "sg", "my", "id", "gb", "za", "au", "nz" };

        [MenuItem("Tools/Ultimate Car Parking/Build World Tour")]
        public static void Build()
        {
            Directory.CreateDirectory(CountriesDir);
            Directory.CreateDirectory(PreviewsDir);
            ConfigureImports();
            var campaign = AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath);
            if (campaign == null || campaign.Count < Countries.Length)
            {
                Debug.LogError($"[WorldTour] Campaign catalog needs {Countries.Length} levels.");
                return;
            }

            RenderPreviews(campaign);
            // Opening a scene per preview unloads unused assets: reload what was loaded before.
            campaign = AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath);

            var countries = new CountryData[Countries.Length];
            for (int i = 0; i < Countries.Length; i++)
                countries[i] = WriteCountry(i, campaign.Get(i), ArenaCatalog(Countries[i].id));

            var catalog = AssetDatabase.LoadAssetAtPath<CountryCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<CountryCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            var so = new SerializedObject(catalog);
            so.FindProperty("campaign").objectReferenceValue = campaign;
            SerializedProperty list = so.FindProperty("countries");
            list.arraySize = countries.Length;
            for (int i = 0; i < countries.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = countries[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WorldTour] {countries.Length} countries written");
        }

        /// <summary>Countries with a built arena use its mission catalog (Levels/Countries/Catalog_xx).</summary>
        public static LevelCatalog ArenaCatalog(string countryId) =>
            AssetDatabase.LoadAssetAtPath<LevelCatalog>($"{ArenaLevelWriter.Dir}/Catalog_{countryId}.asset");

        private static readonly Dictionary<string, (string arena, string label)> ArenaNames = new()
        {
            { "in", ("Mumbai", "Mumbai city arena") }, { "us", ("New York", "New York City arena") }, { "gb", ("London", "London arena") },
            { "jp", ("Tokyo", "Tokyo city arena") }, { "ae", ("Dubai", "Dubai city arena") }, { "cn", ("Beijing", "Beijing city arena") },
            { "kr", ("Seoul", "Seoul city arena") }, { "th", ("Bangkok", "Bangkok city arena") }, { "sg", ("Singapore", "Singapore city arena") },
            { "my", ("Kuching", "Kuching riverside arena") },
            { "id", ("Jakarta", "Jakarta city arena") }, { "sa", ("Riyadh", "Riyadh city arena") }, { "fr", ("Paris", "Paris city arena") },
            { "de", ("Berlin", "Berlin city arena") }, { "it", ("Rome", "Rome city arena") },
            { "es", ("Madrid", "Madrid city arena") }, { "ru", ("Moscow", "Moscow city arena") }, { "nl", ("Amsterdam", "Amsterdam city arena") },
            { "ch", ("Zurich", "Zurich city arena") }, { "gr", ("Athens", "Athens city arena") },
            { "br", ("Rio de Janeiro", "Rio city arena") }, { "ca", ("Toronto", "Toronto arena") }, { "mx", ("Mexico City", "Mexico City arena") }, { "ar", ("Buenos Aires", "Buenos Aires arena") },
            { "eg", ("Cairo", "Cairo city arena") }, { "au", ("Sydney", "Sydney city arena") }, { "tr", ("Istanbul", "Istanbul city arena") },
            { "ng", ("Lagos", "Lagos city arena (generic)") }, { "za", ("Cape Town", "Cape Town arena (generic)") }, { "nz", ("Auckland", "Auckland arena (generic)") },
        };

        private static LevelData FirstMission(int index, LevelCatalog campaign)
        {
            LevelCatalog arena = ArenaCatalog(Countries[index].id);
            return arena != null && arena.Count > 0 ? arena.Get(0) : campaign.Get(index);
        }

        public static Sprite Flag(string id) => AssetDatabase.LoadAssetAtPath<Sprite>($"{FlagsDir}/Flag_{id}.png");
        public static Sprite Preview(LevelData level) => AssetDatabase.LoadAssetAtPath<Sprite>(PreviewPath(level));
        private static string PreviewPath(LevelData level) => $"{PreviewsDir}/Preview_{level.LevelId}.jpg";

        // ------------------------------------------------------------------ data

        private static CountryData WriteCountry(int index, LevelData campaignLevel, LevelCatalog arena)
        {
            bool hasArena = arena != null && arena.Count > 0;
            LevelData level = hasArena ? arena.Get(0) : campaignLevel;
            var c = Countries[index];
            string path = $"{CountriesDir}/Country_{index + 1:00}_{c.id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<CountryData>(path);
            if (data == null) { data = ScriptableObject.CreateInstance<CountryData>(); AssetDatabase.CreateAsset(data, path); }
            var so = new SerializedObject(data);
            so.FindProperty("countryId").stringValue = c.id;
            so.FindProperty("displayName").stringValue = c.name;
            so.FindProperty("region").enumValueIndex = (int)c.region;
            so.FindProperty("flag").objectReferenceValue = Flag(c.id);
            so.FindProperty("latLon").vector2Value = new Vector2(c.lat, c.lon);
            so.FindProperty("arenaName").stringValue = hasArena && ArenaNames.TryGetValue(c.id, out var an) ? an.arena : c.city;
            so.FindProperty("environmentLabel").stringValue = hasArena && ArenaNames.TryGetValue(c.id, out var al) ? al.label : EnvironmentLabel(level);
            so.FindProperty("description").stringValue = hasArena ? $"{c.intro} {arena.Count} missions in a dedicated city arena." : $"{c.intro} {MissionSentence(level)}";
            so.FindProperty("missionLabel").stringValue = MissionLabel(level);
            so.FindProperty("preview").objectReferenceValue = Preview(level);
            SerializedProperty levels = so.FindProperty("levels");
            levels.arraySize = hasArena ? arena.Count : 1;
            for (int m = 0; m < levels.arraySize; m++) levels.GetArrayElementAtIndex(m).objectReferenceValue = hasArena ? arena.Get(m) : level;
            so.FindProperty("missionCatalog").objectReferenceValue = hasArena ? arena : null;
            // Classic tour chain: the first stop is open, each next one needs the previous country completed.
            so.FindProperty("unlock.unlockedByDefault").boolValue = index == 0;
            so.FindProperty("unlock.requirePreviousCountry").boolValue = index > 0;
            so.FindProperty("unlock.requiredCountryIds").arraySize = 0;
            so.FindProperty("unlock.minTourStars").intValue = 0;
            so.FindProperty("unlock.minCompletedCountries").intValue = 0;
            so.FindProperty("roadDirection").enumValueIndex = (int)(LeftHandTraffic.Contains(c.id) ? RoadDirection.LeftHand : RoadDirection.RightHand);
            // No dedicated arena yet: missions keep their own level environments.
            so.FindProperty("arena.source").enumValueIndex = (int)ArenaSource.None;
            so.FindProperty("arena.key").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        public static string EnvironmentLabel(LevelData level)
        {
            if (level.Theme != null)
                return level.Theme.ThemeId switch
                {
                    "city" => "Open-air car park",
                    "garage" => "Rooftop garage",
                    "yard" => "Construction yard",
                    _ => level.Theme.DisplayName,
                };
            if (level.EnvironmentPrefab != null && level.EnvironmentPrefab.name.Contains("DrivingSchool")) return "Driving school yard";
            return level.EnvironmentPrefab != null ? level.EnvironmentPrefab.name.Replace("Env_", "").Replace('_', ' ') : "Car park";
        }

        private static string MissionLabel(LevelData level)
        {
            if (level.HasObjectives) return MissionTypeInfo.Title(level.MissionType);
            string n = level.DisplayName;
            if (n.Contains("Reverse")) return "REVERSE PARKING";
            if (n.Contains("Parallel")) return "PARALLEL PARKING";
            return "STANDARD PARKING";
        }

        private static string MissionSentence(LevelData level)
        {
            if (level.HasObjectives && !string.IsNullOrEmpty(level.Briefing))
            {
                string b = level.Briefing;
                int dot = b.IndexOf(". ");
                return b.StartsWith("Lesson") && dot > 0 ? b.Substring(dot + 2) : b;
            }
            string n = level.DisplayName;
            if (n.Contains("Reverse")) return "Reverse into the marked bay. Driving in nose-first doesn't count.";
            if (n.Contains("Parallel")) return "Parallel park in the gap between two parked cars.";
            return "Find the marked bay and park inside the lines without hitting anything.";
        }

        // ------------------------------------------------------------------ imports

        private static void ConfigureImports()
        {
            AssetDatabase.Refresh();
            foreach (var c in Countries) Sprite($"{FlagsDir}/Flag_{c.id}.png", 256);
        }

        private static void Sprite(string path, int maxSize)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter ti) { Debug.LogError("[WorldTour] missing " + path); return; }
            bool dirty = ti.textureType != TextureImporterType.Sprite || ti.maxTextureSize != maxSize || ti.mipmapEnabled;
            if (!dirty) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = false;
            ti.maxTextureSize = maxSize;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SaveAndReimport();
        }

        // ------------------------------------------------------------------ previews

        /// <summary>Renders each tour level's real environment (props, lighting, target bay, player car) to a JPG.</summary>
        private static void RenderPreviews(LevelCatalog campaign)
        {
            // DontSave: opening a new scene per preview unloads unreferenced objects.
            var rt = new RenderTexture(PreviewW, PreviewH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, hideFlags = HideFlags.HideAndDontSave };
            var tex = new Texture2D(PreviewW, PreviewH, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                for (int i = 0; i < Countries.Length; i++)
                {
                    campaign = AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath);
                    LevelData level = FirstMission(i, campaign);
                    EditorUtility.DisplayProgressBar("World Tour", "Preview " + level.LevelId, i / (float)Countries.Length);
                    GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(RealCarFactory.PrefabPath);
                    if (car == null) car = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectBootstrapper.CarPrefabPath);
                    RenderLevel(level, car, rt, tex);
                    File.WriteAllBytes(PreviewPath(level), tex.EncodeToJPG(88));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
            AssetDatabase.Refresh();
            campaign = AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath);
            for (int i = 0; i < Countries.Length; i++) Sprite(PreviewPath(FirstMission(i, campaign)), 1024);
            Debug.Log($"[WorldTour] {Countries.Length} previews rendered");
        }

        private static void RenderLevel(LevelData level, GameObject carPrefab, RenderTexture rt, Texture2D tex)
        {
            var root = new GameObject("PreviewRoot");
            try { RenderLevelInto(root.transform, level, carPrefab, rt, tex); }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// Renders a level from several viewpoints into <paramref name="outDir"/> (start view, overview, target close-up),
        /// used to check country arenas. Batch helper; not part of the bootstrap.
        /// </summary>
        public static void RenderShots(LevelData level, string outDir)
        {
            Directory.CreateDirectory(outDir);
            var rt = new RenderTexture(PreviewW, PreviewH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, hideFlags = HideFlags.HideAndDontSave };
            var tex = new Texture2D(PreviewW, PreviewH, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(RealCarFactory.PrefabPath);
            var root = new GameObject("ShotRoot");
            try
            {
                Vector3 t = level.TargetPosition, sp = level.SpawnPosition;
                Vector3 mid = (t + sp) * 0.5f;
                float span = Mathf.Max(20f, Vector3.Distance(t, sp));
                Vector3 bayBack = level.TargetRotation * Vector3.back;
                var views = new List<(string name, Vector3 from, Vector3 look)>
                {
                    ("overview", mid + new Vector3(-span * 0.35f, span * 0.9f, -span * 0.55f), mid),
                    ("target", t - bayBack * -9f + Vector3.up * 6f + level.TargetRotation * Vector3.right * 4f, t),
                    ("driver", sp + level.SpawnRotation * new Vector3(0f, 2.3f, -6.5f), sp + level.SpawnRotation * new Vector3(0f, 1f, 12f)),
                };
                RenderLevelInto(root.transform, level, car, rt, tex, views, outDir);
                File.WriteAllBytes(Path.Combine(outDir, $"{level.LevelId}_preview.jpg"), tex.EncodeToJPG(88));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
        }

        private static void RenderLevelInto(Transform root, LevelData level, GameObject carPrefab, RenderTexture rt, Texture2D tex,
            List<(string name, Vector3 from, Vector3 look)> extraViews = null, string extraDir = null)
        {
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.46f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.21f, 0.20f);
            RenderSettings.fog = false;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.sun = sun;

            if (level.HasAddressableEnvironment)
            {
                GameObject arena = ArenaDelivery.LoadInEditor(level.EnvironmentAddress);
                if (arena != null) PrefabUtility.InstantiatePrefab(arena, root);
                else Debug.LogError("[WorldTour] arena not registered: " + level.EnvironmentAddress);
            }
            else if (level.Theme != null)
            {
                GameObject env = EnvironmentBuilder.Build(level.Theme, level.Props, "Environment");
                env.transform.SetParent(root, false);
                if (level.EnvironmentPrefab != null) PrefabUtility.InstantiatePrefab(level.EnvironmentPrefab, env.transform);
            }
            else if (level.EnvironmentPrefab != null)
            {
                var env = (GameObject)PrefabUtility.InstantiatePrefab(level.EnvironmentPrefab, root);
                EnvironmentBuilder.PlaceProps(level.Props, env.transform, "Props");
            }

            // Arena lighting (normally applied in Awake at runtime).
            foreach (var lo in root.GetComponentsInChildren<SceneLightingOverride>(true))
                typeof(SceneLightingOverride).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(lo, null);

            // Target bay outline (as the in-game marker shows it) and the player's car at the spawn point.
            Vector3 t = level.TargetPosition;
            Quaternion r = level.TargetRotation;
            Vector2 s = level.TargetSize;
            var bay = new GameObject("Bay").transform;
            bay.SetParent(root, false);
            bay.SetPositionAndRotation(t + Vector3.up * 0.03f, r);
            void Edge(Vector3 local, Vector3 scale) =>
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, bay, "Edge", local, Quaternion.identity, scale, "LineTarget", false);
            Edge(new Vector3(-s.x * 0.5f, 0f, 0f), new Vector3(0.16f, 0.03f, s.y));
            Edge(new Vector3(s.x * 0.5f, 0f, 0f), new Vector3(0.16f, 0.03f, s.y));
            Edge(new Vector3(0f, 0f, -s.y * 0.5f), new Vector3(s.x, 0.03f, 0.16f));
            Edge(new Vector3(0f, 0f, s.y * 0.5f), new Vector3(s.x, 0.03f, 0.16f));

            Vector3 spawn = level.SpawnPosition;
            Quaternion spawnRot = level.SpawnRotation;
            if (carPrefab != null)
            {
                var car = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab, root);
                car.transform.SetPositionAndRotation(spawn, spawnRot);
            }

            // Camera: behind and above the car, looking towards the bay.
            Vector3 toBay = t - spawn; toBay.y = 0f;
            Vector3 dir = toBay.sqrMagnitude > 1f ? toBay.normalized : spawnRot * Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 look = Vector3.Lerp(spawn, t, 0.62f) + Vector3.up * 0.5f;
            Vector3 from = spawn - dir * 9f + Vector3.up * 7.5f - side * 3f;
            var cam = new GameObject("PreviewCamera").AddComponent<Camera>();
            cam.transform.SetParent(root, false);
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;
            if (extraViews != null)
                foreach (var v in extraViews)
                {
                    cam.transform.SetPositionAndRotation(v.from, Quaternion.LookRotation(v.look - v.from));
                    cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    if (!string.IsNullOrEmpty(extraDir))
                        File.WriteAllBytes(Path.Combine(extraDir, $"{level.LevelId}_{v.name}.jpg"), tex.EncodeToJPG(88));
                }
            if (extraViews != null && string.IsNullOrEmpty(extraDir))
                return;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from));
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
        }
    }
}
