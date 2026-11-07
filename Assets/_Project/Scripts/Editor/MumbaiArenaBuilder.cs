using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Environment;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.Parking;
using UltimateCarParking.WorldTour;
using F = UltimateCarParking.EditorTools.ArenaPropFactory;
using W = UltimateCarParking.EditorTools.ArenaLevelWriter;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// India — Mumbai City arena: prefabs from the imported Mumbai models (auto-rickshaws, bus, taxi, market stalls,
    /// shops, houses, Indian road signs, road tiles) and the arena layout. Left-hand traffic.
    /// </summary>
    public static class MumbaiArenaBuilder
    {
        public const string Arena = "Mumbai";
        public static string EnvPath => F.PrefabPath(Arena, "Env_Mumbai");
        public const RoadDirection Road = RoadDirection.LeftHand;

        // Layout (metres). Market street runs along X (eastbound = +X), the residential lane north along Z at LaneX,
        // the shopping-complex lot south-east of the street.
        public const float StreetHalf = 5.75f, LaneX = -25f, LaneHalf = 3.32f;
        public const float LotMinX = 3f, LotMaxX = 61f, LotMinZ = -58f, LotMaxZ = -6.5f;
        public const float BayW = 2.8f, BayD = 5.6f, RowStartX = 9.6f;
        public static readonly float[] RowZ = { -17f, -29.6f, -35.2f, -47.8f };     // bay centres; rows 2/3 back to back
        public static readonly float[] RowYaw = { 0f, 180f, 0f, 180f };             // nose-in direction of each row

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                // Vehicles (front turned to +Z, real-world lengths)
                new F.Spec { name = "Rickshaw_Yellow", source = "Mumbai/mum_autorickshaw_b.glb", fit = F.Fit.Length, size = 2.85f, colliderHeightCap = 1.6f },
                new F.Spec { name = "Rickshaw_Green", source = "Mumbai/mum_autorickshaw_a.glb", fit = F.Fit.Length, size = 2.75f, colliderHeightCap = 1.6f },
                new F.Spec { name = "Taxi_KaliPeeli", source = "Mumbai/mum_taxi.glb", yaw = 90f, fit = F.Fit.Length, size = 3.9f, colliderHeightCap = 1.5f },
                new F.Spec { name = "Bus_City", source = "Mumbai/mum_bus.glb", fit = F.Fit.Length, size = 10.5f },
                // Market and shops
                new F.Spec { name = "Stall_Bhelpuri", source = "Mumbai/mum_bhelpuri_shop.glb", yaw = -90f, fit = F.Fit.Height, size = 2.9f,
                    strip = new[] { "Spare_Wheel" } },
                new F.Spec { name = "Stall_Paan", source = "Mumbai/mum_paan_shop.glb", fit = F.Fit.Height, size = 2.6f },
                new F.Spec { name = "Shop_Grocery", source = "Mumbai/mum_grocery_shop.glb", yaw = -90f },
                new F.Spec { name = "Shop_Corner", source = "Mumbai/mum_lowpoly_shop.glb" },
                // Residential
                new F.Spec { name = "House_Tiled", source = "Mumbai/mum_house_game.glb" },
                new F.Spec { name = "House_Brick", source = "Mumbai/mum_house_construction.glb" },
                // Street furniture (CC0)
                new F.Spec { name = "Lamp_Wall", source = "Shared/PolyHaven_street_lamp_02/street_lamp_02_1k.gltf", fit = F.Fit.Height, size = 1.2f, collider = F.ColliderKind.None },
                new F.Spec { name = "Lamp_Pole", source = "NewYork/nyc_parking_lot_uf.glb", pick = F.Named("Lamp1"), fit = F.Fit.Height, size = 7.5f },
                // Road kit is modelled at ~40% scale: straights scaled to a 26.8 m x 11.5 m two-lane street, lanes to 13.8 m x 6.6 m.
                new F.Spec { name = "Road_Street", source = "Mumbai/mum_road_pack.glb", collider = F.ColliderKind.None, fit = F.Fit.Width, size = 26.8f,
                    pick = r => r.GetComponentsInChildren<Transform>(true).Where(t => t.name == "4in 2.007_6").Take(1) },
                new F.Spec { name = "Road_Lane", source = "Mumbai/mum_road_pack.glb", collider = F.ColliderKind.None, fit = F.Fit.Width, size = 13.8f,
                    pick = r => r.GetComponentsInChildren<Transform>(true).Where(t => t.name == "4in 2.008_8").Take(1) },
            };
            // (Shared/shopping_mall.glb is an interior, not a frontage: not used. The mall frontage is built from shops.)
            if (System.IO.File.Exists($"{F.SourceRoot}/Mumbai/mum_house_old.glb"))
                specs.Add(new F.Spec { name = "House_Old", source = "Mumbai/mum_house_old.glb" });

            // Indian mandatory signs: each plate plus the pole(s) standing under it.
            foreach (string sign in new[] { "STOP_SIGN", "Give_Way", "Horn_Prohibited", "Cycle_Prohibited", "Height_Limit" })
                specs.Add(new F.Spec { name = "Sign_" + sign.Replace("_SIGN", "").Replace("_", ""), source = "Mumbai/mum_road_signs.glb", pick = SignWithPole(sign) });

            foreach (F.Spec s in specs) F.Make(Arena, s);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arena] Mumbai props: {specs.Count}");
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Mumbai Arena")]
        public static void Build()
        {
            BuildProps();
            BuildEnvironment();
            BuildLevels();
            AssetDatabase.SaveAssets();
        }

        private static GameObject P(string name) => F.Load(Arena, name);

        /// <summary>Renders each Mumbai mission (start, overview, target) to ../UltimateCarParking3D_logs/arenas/.</summary>
        public static void RenderShots()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            string dir = System.IO.Path.GetFullPath("../UltimateCarParking3D_logs/arenas");
            foreach (string id in new[] { "IN1", "IN2", "IN3" })
                WorldTourBuilder.RenderShots(AssetDatabase.LoadAssetAtPath<LevelData>($"{W.Dir}/Level_{id}.asset"), dir);
            Debug.Log("[Arena] Mumbai shots written to " + dir);
        }

        // ------------------------------------------------------------------ environment

        public static void BuildEnvironment()
        {
            var root = new GameObject("Env_Mumbai");
            Transform Group(string n) { var g = new GameObject(n).transform; g.SetParent(root.transform, false); return g; }
            void Put(Transform parent, GameObject prefab, Vector3 pos, float yaw)
            {
                if (prefab == null) { Debug.LogError("[Arena] Mumbai missing prefab at " + pos); return; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            }

            // Ground: one drivable invisible slab + textured surfaces (dusty pavement around, asphalt in the lot).
            Transform ground = Group("Ground");
            var slab = new GameObject("DriveSurface"); slab.transform.SetParent(ground, false);
            var col = slab.AddComponent<BoxCollider>(); col.size = new Vector3(220f, 1f, 200f); col.center = new Vector3(0f, -0.5f + 0.04f, -10f);
            Surface(ground, "Pavement", new Vector3(0f, -0.01f, -10f), new Vector2(200f, 180f), Mat("M_Mumbai_Pavement", "concrete_pavement_diff_1k.jpg", "concrete_pavement_nor_gl_1k.jpg", 40f, new Color(0.93f, 0.86f, 0.76f)));
            Surface(ground, "LotAsphalt", new Vector3((LotMinX + LotMaxX) * 0.5f, 0.01f, (LotMinZ + LotMaxZ) * 0.5f), new Vector2(LotMaxX - LotMinX, LotMaxZ - LotMinZ),
                Mat("M_Mumbai_LotAsphalt", "Road007_1K-JPG_Color.jpg", "Road007_1K-JPG_NormalGL.jpg", 10f, new Color(0.8f, 0.8f, 0.82f)));
            // Invisible boundary so the car stays in the arena.
            foreach (var (c, sz) in new[] { (new Vector3(0f, 2f, 80f), new Vector3(220f, 4f, 1f)), (new Vector3(0f, 2f, -100f), new Vector3(220f, 4f, 1f)),
                                             (new Vector3(-100f, 2f, -10f), new Vector3(1f, 4f, 200f)), (new Vector3(100f, 2f, -10f), new Vector3(1f, 4f, 200f)) })
            {
                var w = new GameObject("Boundary"); w.transform.SetParent(ground, false); w.transform.localPosition = c;
                w.AddComponent<BoxCollider>().size = sz;
            }

            // Market street (east-west) and the residential lane (north).
            Transform roads = Group("Roads");
            // Tiles sit 3 cm low so the asphalt top (~2 cm) is below bay markers and level with the drive surface.
            foreach (float x in new[] { -66.9f, -40.2f, -13.4f, 13.4f, 40.2f, 66.9f }) Put(roads, P("Road_Street"), new Vector3(x, -0.03f, 0f), 0f);
            foreach (float z in new[] { 12.6f, 26.4f, 40.2f, 54f }) Put(roads, P("Road_Lane"), new Vector3(LaneX, -0.03f, z), 90f);

            // Market frontage: shops and stalls facing the street on both sides (front = +Z, so north side yaw 180).
            Transform town = Group("Market");
            var north = new (string p, float x, float depth)[]
            {
                ("Shop_Grocery", -56f, 3.7f), ("House_Brick", -46f, 2.8f), ("Stall_Bhelpuri", -39.5f, 1f), ("Stall_Paan", -34f, 1f),
                ("House_Tiled", -15.5f, 4.6f), ("Stall_Bhelpuri", -6.5f, 1f), ("House_Brick", 0f, 2.8f), ("Stall_Paan", 6f, 1f),
                ("House_Tiled", 14f, 4.6f), ("Shop_Grocery", 26f, 3.7f), ("Stall_Bhelpuri", 33f, 1f), ("House_Brick", 40f, 2.8f), ("Shop_Grocery", 50f, 3.7f), ("Stall_Paan", 57f, 1f),
            };
            foreach (var (p, x, d) in north) Put(town, P(p), new Vector3(x, 0f, StreetHalf + 0.4f + d), 180f);
            var south = new (string p, float x, float depth)[]
            {
                ("House_Tiled", -56f, 4.6f), ("Shop_Grocery", -44f, 3.7f), ("Stall_Paan", -36f, 1f), ("Stall_Bhelpuri", -30f, 1f), ("House_Brick", -22f, 2.8f),
                ("Shop_Grocery", -12f, 3.7f), ("Stall_Bhelpuri", -4f, 1f),
            };
            foreach (var (p, x, d) in south) Put(town, P(p), new Vector3(x, 0f, -StreetHalf - 0.4f - d), 0f);

            // Residential lane: houses close on both sides; a walled gap on the east side at z 27.2-31.8 is mission 2's space.
            Transform lane = Group("ResidentialLane");
            float west = LaneX - LaneHalf - 0.3f, east = LaneX + LaneHalf + 0.3f;
            Put(lane, P("House_Tiled"), new Vector3(west - 4.6f, 0f, 13f), 90f);
            Put(lane, P(HouseOldOr("House_Brick")), new Vector3(west - 4.6f, 0f, 25f), 90f);
            Put(lane, P("House_Tiled"), new Vector3(west - 4.6f, 0f, 37f), 90f);
            Put(lane, P("House_Brick"), new Vector3(west - 3.2f, 0f, 48f), 90f);
            Put(lane, P("House_Brick"), new Vector3(east + 3.1f, 0f, 13.5f), 270f);
            Put(lane, P("House_Brick"), new Vector3(east + 2.8f, 0f, ResGapZ - 2.3f - 3.1f), 0f);
            Put(lane, P("House_Brick"), new Vector3(east + 2.8f, 0f, ResGapZ + 2.3f + 3.1f), 180f);
            Put(lane, P("House_Tiled"), new Vector3(east + 5.6f + 0.6f + 4.6f, 0f, ResGapZ), 270f);     // back wall of the space
            Put(lane, P("House_Tiled"), new Vector3(east + 4.6f, 0f, 46f), 270f);

            // Shopping complex: mall frontage at the back, painted bays, lot edge barriers.
            Transform lot = Group("ShoppingComplex");
            GameObject mall = P("Mall");
            if (mall != null) Put(lot, mall, new Vector3((LotMinX + LotMaxX) * 0.5f, 0f, LotMinZ - 9f), 0f);
            else foreach (float x in new[] { 12f, 26f, 40f, 54f }) Put(lot, P(x == 26f ? "Shop_Corner" : "Shop_Grocery"), new Vector3(x, 0f, LotMinZ - 4.5f), 0f);
            for (int row = 0; row < RowZ.Length; row++)
                for (int i = 0; i <= 16; i++)
                {
                    float x = RowStartX + i * BayW;
                    ProjectBootstrapper.Primitive(PrimitiveType.Cube, lot, "BayLine", new Vector3(x, 0.03f, RowZ[row]), Quaternion.identity, new Vector3(0.12f, 0.02f, BayD), "LineWhite", false);
                }
            foreach (float x in new[] { 2f, 62f })
                for (float z = -12f; z > -58f; z -= 4.2f) Put(lot, RealPropFactory.Load("Barrier_Concrete"), new Vector3(x, 0f, z), 90f);

            // Street furniture: pole lamps both sides, Indian mandatory signs facing traffic.
            Transform street = Group("StreetFurniture");
            for (float x = -64f; x <= 64f; x += 16f)
            {
                Put(street, P("Lamp_Pole"), new Vector3(x, 0f, StreetHalf + 0.2f), 180f);
                if (x < 0f || x > 62f) Put(street, P("Lamp_Pole"), new Vector3(x + 8f, 0f, -StreetHalf - 0.2f), 0f);
            }
            for (float z = 18f; z < 56f; z += 14f) Put(street, P("Lamp_Pole"), new Vector3(LaneX - LaneHalf - 0.2f, 0f, z), 90f);
            Put(street, P("Sign_HornProhibited"), new Vector3(-50f, 0f, StreetHalf + 0.1f), 270f);
            Put(street, P("Sign_GiveWay"), new Vector3(LaneX - LaneHalf - 0.2f, 0f, 7.5f), 180f);
            Put(street, P("Sign_STOP"), new Vector3(20f, 0f, -StreetHalf - 0.1f), 0f);
            Put(street, P("Sign_HeightLimit"), new Vector3(28.5f, 0f, -StreetHalf - 0.1f), 0f);
            Put(street, P("Sign_CycleProhibited"), new Vector3(LaneX + LaneHalf + 0.2f, 0f, 50f), 180f);

            // Warm, slightly hazy daylight.
            var light = root.AddComponent<SceneLightingOverride>();
            var lso = new SerializedObject(light);
            lso.FindProperty("ambientSky").colorValue = new Color(0.66f, 0.66f, 0.66f);
            lso.FindProperty("ambientEquator").colorValue = new Color(0.55f, 0.5f, 0.44f);
            lso.FindProperty("ambientGround").colorValue = new Color(0.3f, 0.27f, 0.24f);
            lso.FindProperty("fog").boolValue = true;
            lso.FindProperty("fogColor").colorValue = new Color(0.82f, 0.78f, 0.7f);
            lso.FindProperty("fogDensity").floatValue = 0.006f;
            lso.FindProperty("sunColor").colorValue = new Color(1f, 0.9f, 0.76f);
            lso.FindProperty("sunIntensity").floatValue = 1.35f;
            lso.FindProperty("sunEuler").vector3Value = new Vector3(52f, -40f, 0f);
            lso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, EnvPath);
            Object.DestroyImmediate(root);
            Debug.Log("[Arena] Env_Mumbai written");
        }

        public const float ResGapZ = 29.5f;
        public static bool IncludeBus = true;
        public static void BuildLevelsWithoutBus() { IncludeBus = false; BuildLevels(); AssetDatabase.SaveAssets(); }
        public static float ResBayX => LaneX + LaneHalf + 0.3f + 2.8f;   // 5.6 m deep space off the lane's east side

        private static string HouseOldOr(string fallback) => P("House_Old") != null ? "House_Old" : fallback;

        private static void Surface(Transform parent, string name, Vector3 pos, Vector2 size, Material m)
        {
            Transform t = ProjectBootstrapper.Primitive(PrimitiveType.Plane, parent, name, pos, Quaternion.identity, new Vector3(size.x / 10f, 1f, size.y / 10f), null, false);
            t.GetComponent<Renderer>().sharedMaterial = m;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>URP Lit material from a CC0 texture set in ThirdParty/Arenas/Shared/Textures.</summary>
        internal static Material Mat(string name, string diffuse, string normal, float tiling, Color tint)
        {
            string dir = F.SourceRoot + "/Shared/Textures";
            string path = $"Assets/_Project/ThirdParty/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            if (AssetImporter.GetAtPath($"{dir}/{normal}") is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap)
            { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{diffuse}"));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{normal}"));
            m.EnableKeyword("_NORMALMAP");
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", 0.15f);
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            m.mainTextureScale = new Vector2(tiling, tiling);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ missions

        public static void BuildLevels()
        {
            var env = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPath);
            LevelData m1 = Mission1(env), m2 = Mission2(env), m3 = Mission3(env);
            W.WriteCatalog("in", Arena, m1, m2, m3);
            Debug.Log("[Arena] Mumbai missions written");
        }

        private static GameObject Parked(string body) => RealPropFactory.Load("Parked_" + body);

        /// <summary>Mission 1 — Market Parking: drive through the busy market, park in the marked kerbside space.</summary>
        private static LevelData Mission1(GameObject env)
        {
            var props = new List<PropPlacement>();
            // Traffic: parked rickshaws/taxis on both kerbs, a city bus loading on the far side.
            float kerbN = StreetHalf - 0.6f - 1.3f;              // parking strip on the north (left, eastbound) kerb
            if (IncludeBus) W.Place(props, P("Bus_City"), new Vector3(-12f, 0f, -kerbN + 0.2f), TrafficSide.ParkedYaw(90f, +1, Road));
            W.Place(props, P("Taxi_KaliPeeli"), new Vector3(-30f, 0f, -kerbN), TrafficSide.ParkedYaw(90f, +1, Road));
            W.Place(props, P("Rickshaw_Yellow"), new Vector3(-36f, 0f, kerbN), TrafficSide.ParkedYaw(90f, -1, Road));
            W.Place(props, P("Rickshaw_Green"), new Vector3(-8f, 0f, kerbN), TrafficSide.ParkedYaw(90f, -1, Road));
            W.Place(props, P("Rickshaw_Yellow"), new Vector3(8f, 0f, -kerbN), TrafficSide.ParkedYaw(90f, +1, Road));
            W.Place(props, P("Taxi_KaliPeeli"), new Vector3(16f, 0f, kerbN), TrafficSide.ParkedYaw(90f, -1, Road));
            // The marked space at x 30.5-38 between a rickshaw and a taxi.
            W.Place(props, P("Rickshaw_Green"), new Vector3(28.2f, 0f, kerbN), TrafficSide.ParkedYaw(90f, -1, Road));
            W.Place(props, P("Taxi_KaliPeeli"), new Vector3(40.6f, 0f, kerbN), TrafficSide.ParkedYaw(90f, -1, Road));
            W.Place(props, P("Rickshaw_Yellow"), new Vector3(48f, 0f, -kerbN), TrafficSide.ParkedYaw(90f, +1, Road));

            Vector3 spawn = TrafficSide.LaneCentre(new Vector3(-50f, 0f, 0f), Vector3.right, 4.2f, Road);
            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 10f, maxSpeedKmh = 2f, holdDuration = 1.0f };
            return W.Write(new W.Mission
            {
                id = "IN1", countryId = "in", arenaFolder = Arena, number = 1, name = "Market Parking", type = MissionType.StandardParking, road = Road, environment = env, props = props,
                briefing = "Drive through Mumbai's busy market street, keeping left, and park in the marked kerbside space past the stalls.",
                spawn = spawn, spawnYaw = 90f, time = 120f, collisions = 3, three = (55f, 0), two = (85f, 1), coins = (180, 45),
                objectives = new MissionObjective[]
                {
                    new CheckpointObjective("Drive through the market", new Vector3(0f, 0f, 0f), 90f, new Vector2(StreetHalf * 2f - 1f, 4f), "DRIVE THROUGH THE MARKET - KEEP LEFT"),
                    new ParkObjective("Park in the marked space", new Vector3(34.3f, 0f, kerbN), 90f, new Vector2(2.6f, 7.4f), ParkDirection.Either, rules),
                },
            });
        }

        /// <summary>Mission 2 — Narrow Lane Reverse: reverse into a walled space off a narrow residential lane.</summary>
        private static LevelData Mission2(GameObject env)
        {
            var props = new List<PropPlacement>();
            W.Place(props, P("Rickshaw_Green"), new Vector3(LaneX + 1.6f, 0f, 20f), 0f);        // parked tight to the east wall: squeeze past
            W.Place(props, P("Rickshaw_Yellow"), new Vector3(LaneX - 1.7f, 0f, 44f), 180f);
            W.Place(props, P("Stall_Paan"), new Vector3(LaneX - LaneHalf + 0.1f, 0f, 33f), 90f);
            Vector3 spawn = TrafficSide.LaneCentre(new Vector3(LaneX, 0f, 6.5f), Vector3.forward, 2.4f, Road);
            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 9f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            return W.Write(new W.Mission
            {
                id = "IN2", countryId = "in", arenaFolder = Arena, number = 2, name = "Narrow Lane Reverse", type = MissionType.ReverseParking, road = Road, environment = env, props = props,
                briefing = "Squeeze up the narrow residential lane, pass the gap between the houses, then reverse in. Finish within 0.7 m of the back wall line.",
                spawn = spawn, spawnYaw = 0f, time = 150f, collisions = 2, three = (65f, 0), two = (100f, 1), coins = (200, 50),
                objectives = new MissionObjective[]
                {
                    new ReverseParkObjective("Reverse into the house space", new Vector3(ResBayX, 0f, ResGapZ), 90f, new Vector2(3.0f, 5.4f), maxRearGap: 0.7f, rules),
                },
            });
        }

        /// <summary>Mission 3 — Shopping Complex: enter the mall lot, weave through parked rows, park precisely by the entrance.</summary>
        private static LevelData Mission3(GameObject env)
        {
            var props = new List<PropPlacement>();
            string[] fleet = { "sedan", "hatchback", "suv", "compact", "wagon", "minivan", "coupe", "pickup", "offroad", "sport" };
            int targetRow = 3, targetBay = 10;
            int k = 0;
            for (int row = 0; row < RowZ.Length; row++)
                for (int i = 0; i < 16; i++)
                {
                    bool target = row == targetRow && i == targetBay;
                    bool empty = !target && ((row * 7 + i * 3) % 9 == 0);            // a few free (decoy) bays
                    if (target || empty) continue;
                    float x = RowStartX + BayW * (i + 0.5f);
                    float yaw = RowYaw[row] + ((k % 4 == 1) ? 180f : 0f);           // some reversed in
                    GameObject car = (k % 6 == 2) ? P("Taxi_KaliPeeli") : (k % 11 == 5) ? P("Rickshaw_Yellow") : Parked(fleet[k % fleet.Length]);
                    W.Place(props, car, new Vector3(x, 0f, RowZ[row] + (k % 3 - 1) * 0.12f), yaw);
                    k++;
                }
            // Shoppers' cars stopped in the aisles: navigate around them.
            W.Place(props, Parked("minivan"), new Vector3(30f, 0f, -23.4f), 90f);
            W.Place(props, P("Rickshaw_Green"), new Vector3(46f, 0f, -41.5f), 270f);
            W.Place(props, RealPropFactory.Load("Cones_Cluster"), new Vector3(20f, 0f, -41.5f), 0f);

            Vector3 spawn = TrafficSide.LaneCentre(new Vector3(-6f, 0f, 0f), Vector3.right, 4.2f, Road);
            float tx = RowStartX + BayW * (targetBay + 0.5f);
            var rules = new ParkingRules { edgeMargin = 0.04f, angleTolerance = 7f, maxSpeedKmh = 2f, holdDuration = 1.5f };
            return W.Write(new W.Mission
            {
                id = "IN3", countryId = "in", arenaFolder = Arena, number = 3, name = "Shopping Complex", type = MissionType.PrecisionParking, road = Road, environment = env, props = props,
                briefing = "Turn into the shopping complex, weave between the parked rows and stopped cars, and park precisely in the free bay by the mall entrance.",
                spawn = spawn, spawnYaw = 90f, time = 170f, collisions = 2, three = (85f, 0), two = (125f, 1), coins = (240, 60),
                scoreMode = ScoreMode.Quality, quality = (0.85f, 0.6f),
                objectives = new MissionObjective[]
                {
                    new CheckpointObjective("Enter the shopping complex", new Vector3(24f, 0f, -9.5f), 0f, new Vector2(9f, 3f), "TURN INTO THE SHOPPING COMPLEX"),
                    new PrecisionParkObjective("Park by the mall entrance", new Vector3(tx, 0f, RowZ[targetRow]), RowYaw[targetRow], new Vector2(BayW - 0.15f, BayD), ParkDirection.NoseIn,
                        maxOffset: 0.4f, maxAngle: 6f, minAccuracy: 0.5f, rules),
                },
            });
        }

        private static System.Func<Transform, IEnumerable<Transform>> SignWithPole(string plateName) => root =>
        {
            Transform plate = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(plateName));
            if (plate == null) throw new System.Exception("sign " + plateName);
            Bounds pb = Bounds(plate);
            var poles = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Cylinder") && t.GetComponentInChildren<MeshFilter>() != null)
                .Where(t => { Bounds b = Bounds(t); return Mathf.Abs(b.center.x - pb.center.x) < 0.45f && Mathf.Abs(b.center.z - pb.center.z) < 0.45f && b.max.y <= pb.max.y + 0.1f; });
            return new[] { plate }.Concat(poles);
        };

        private static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
