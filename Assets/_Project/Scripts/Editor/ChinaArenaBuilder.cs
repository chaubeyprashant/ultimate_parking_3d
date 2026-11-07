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
using NY = UltimateCarParking.EditorTools.NewYorkArenaBuilder;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// China — Beijing arena, right-hand traffic, built from the imported China models: a night-city skyline, a row of
    /// shopfronts, two-storey temple-style halls (hutong houses), a pagoda tower, a gate tower, Chinese street lamps and
    /// paper lanterns. An avenue with a parking lot (CN1), a narrow hutong lane (CN2) and a lantern-lit night-market lane (CN3).
    /// </summary>
    public static class ChinaArenaBuilder
    {
        public const string Arena = "China";
        public const string CountryId = "cn";
        public static string EnvPath => F.PrefabPath(Arena, "Env_Beijing");
        public const RoadDirection Road = RoadDirection.RightHand;

        public const float RoadHalf = 6.5f, WalkOuter = 11f;
        // CN1 lot north of the avenue
        public const float LotX0 = 24f, LotBayW = 2.8f, LotBayD = 5.5f, LotRowZ = 34f;
        public const int LotBays = 20, LotTarget = 12;
        // CN2 hutong lane (north) runs along Z
        public const float HutongX = -45f, HutongHalf = 3.5f, HutongEndZ = 62f, KerbW = 0.3f, KerbH = 0.13f;
        // CN3 market lane
        public const float MarketX = 100f, MarketHalf = 2.2f, MarketEndZ = 56f;

        private static System.Func<Transform, IEnumerable<Transform>> Exact(params string[] names) => root =>
        {
            var hits = root.GetComponentsInChildren<Transform>(true).Where(t => names.Contains(t.name)).ToList();
            if (hits.Count == 0) throw new System.Exception("no node named " + string.Join("/", names));
            return hits.Where(t => !hits.Any(h => h != t && t.IsChildOf(h)));
        };

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Hall_Big", source = "China/cn_ancient_a.glb", pick = r => r.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("KF_Building_017b04_") && t.name != "KF_Building_017b04_4" && t.name != "KF_Building_017b04_3").Take(3) },
                new F.Spec { combine = true, name = "Hall_House", source = "China/cn_ancient_b.glb" },
                new F.Spec { combine = true, name = "Pagoda_Tower", source = "China/cn_storehouse.glb", fit = F.Fit.Height, size = 34f },
                new F.Spec { combine = true, name = "Shop_Row", source = "China/cn_shop_pack.glb" },
                new F.Spec { combine = true, name = "Gate_Tower", source = "China/cn_temple.glb", fit = F.Fit.Height, size = 12f },
                new F.Spec { name = "Lamp_Street", source = "China/cn_lamp.glb", pick = Exact("Object_2"), fit = F.Fit.Height, size = 4.5f },
                new F.Spec { name = "Lantern", source = "China/cn_chochin.glb", fit = F.Fit.Height, size = 0.9f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Skyline", source = "China/cn_night_city.glb", fit = F.Fit.Width, size = 320f, collider = F.ColliderKind.None },
            };
            foreach (F.Spec s in specs) F.Make(Arena, s);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arena] China props: {specs.Count}");
        }

        private static GameObject P(string name)
        {
            GameObject g = F.Load(Arena, name);
            if (g == null) Debug.LogError("[Arena] China prefab missing: " + name);
            return g;
        }

        [MenuItem("Tools/Ultimate Car Parking/Build China Arena")]
        public static void Build()
        {
            BuildProps();
            BuildEnvironment();
            BuildLevels();
            AssetDatabase.SaveAssets();
        }

        public static void RenderShots()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            string dir = System.IO.Path.GetFullPath("../UltimateCarParking3D_logs/arenas");
            foreach (string id in new[] { "CN1", "CN2", "CN3" })
                WorldTourBuilder.RenderShots(AssetDatabase.LoadAssetAtPath<LevelData>($"{W.Dir}/Level_{id}.asset"), dir);
        }

        public static void BuildEnvironment()
        {
            var root = new GameObject("Env_Beijing");
            Transform Group(string n) { var g = new GameObject(n).transform; g.SetParent(root.transform, false); return g; }
            GameObject Put(Transform parent, GameObject prefab, Vector3 pos, float yaw)
            {
                if (prefab == null) { Debug.LogError("[Arena] China missing prefab at " + pos); return null; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                return go;
            }
            Vector3 Size(string prefab) { GameObject p = P(prefab); var box = p != null ? p.GetComponent<BoxCollider>() : null; return box != null ? box.size : new Vector3(10f, 8f, 10f); }

            Material pavement = MumbaiArenaBuilder.Mat("M_CN_Pavement", "red_sandstone_pavement_diff_1k.jpg", "red_sandstone_pavement_nor_gl_1k.jpg", 30f, new Color(0.72f, 0.7f, 0.68f));
            Material asphalt = MumbaiArenaBuilder.Mat("M_CN_Asphalt", "Road007_1K-JPG_Color.jpg", "Road007_1K-JPG_NormalGL.jpg", 10f, new Color(0.56f, 0.56f, 0.58f));
            Material cobbles = MumbaiArenaBuilder.Mat("M_CN_Cobbles", "large_sandstone_blocks_diff_1k.jpg", "large_sandstone_blocks_nor_gl_1k.jpg", 9f, new Color(0.58f, 0.55f, 0.52f));

            Transform ground = Group("Ground");
            var slab = new GameObject("DriveSurface"); slab.transform.SetParent(ground, false);
            var col = slab.AddComponent<BoxCollider>(); col.size = new Vector3(240f, 1f, 190f); col.center = new Vector3(0f, -0.5f, 10f);
            NY.Surface(ground, "Ground", new Vector3(0f, -0.02f, 10f), new Vector2(240f, 190f), pavement);
            NY.Surface(ground, "Avenue", new Vector3(0f, 0.004f, 0f), new Vector2(238f, RoadHalf * 2f), asphalt);
            NY.Surface(ground, "Lot", new Vector3((12f + 86f) * 0.5f, 0.005f, (WalkOuter + 40f) * 0.5f), new Vector2(74f, 40f - WalkOuter), asphalt);
            NY.Surface(ground, "Hutong", new Vector3(HutongX, 0.005f, (WalkOuter + HutongEndZ) * 0.5f), new Vector2(HutongHalf * 2f + 0.6f, HutongEndZ - WalkOuter), cobbles);
            NY.Surface(ground, "MarketLane", new Vector3(MarketX, 0.005f, (WalkOuter + MarketEndZ) * 0.5f), new Vector2(MarketHalf * 2f + 0.4f, MarketEndZ - WalkOuter), cobbles);
            NY.Boundary(ground, new Vector3(0f, 2f, 104f), new Vector3(240f, 4f, 1f));
            NY.Boundary(ground, new Vector3(0f, 2f, -84f), new Vector3(240f, 4f, 1f));
            NY.Boundary(ground, new Vector3(-119f, 2f, 10f), new Vector3(1f, 4f, 190f));
            NY.Boundary(ground, new Vector3(119f, 2f, 10f), new Vector3(1f, 4f, 190f));

            Transform marks = Group("Markings");
            foreach (float z in new[] { -0.12f, 0.12f })
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, marks, "CentreLine", new Vector3(0f, 0.02f, z), Quaternion.identity, new Vector3(236f, 0.01f, 0.1f), "LineYellow", false);
            for (float x = -114f; x < 114f; x += 9f)
                foreach (float z in new[] { -3.25f, 3.25f })
                    ProjectBootstrapper.Primitive(PrimitiveType.Cube, marks, "LaneDash", new Vector3(x, 0.02f, z), Quaternion.identity, new Vector3(3f, 0.01f, 0.12f), "LineWhite", false);

            // Skyline backdrop and landmarks.
            Transform sky = Group("Skyline");
            Put(sky, P("Skyline"), new Vector3(0f, 0f, 135f), 180f);
            Put(sky, P("Skyline"), new Vector3(0f, 0f, -120f), 0f);
            Put(sky, P("Pagoda_Tower"), new Vector3(70f, 0f, -22f), 0f);
            Put(sky, P("Gate_Tower"), new Vector3(-85f, 0f, -20f), 0f);
            Put(sky, P("Hall_Big"), new Vector3(-10f, 0f, -45f), 0f);
            Put(sky, P("Hall_Big"), new Vector3(85f, 0f, -48f), 0f);
            Put(sky, P("Gate_Tower"), new Vector3(30f, 0f, -22f), 0f);
            Put(sky, P("Pagoda_Tower"), new Vector3(-100f, 0f, 70f), 0f);
            Put(sky, P("Gate_Tower"), new Vector3(10f, 0f, 78f), 0f);

            // Shop rows along both sides (front toward the avenue), leaving the hutong, lot and market openings.
            Transform rows = Group("ShopRows");
            Vector3 shop = Size("Shop_Row");
            void Shops(float x0, float x1, int side)
            {
                for (float x = x0; x + shop.x <= x1 + 0.3f; x += shop.x + 0.2f)
                    Put(rows, P("Shop_Row"), new Vector3(x + shop.x * 0.5f, 0f, side * (WalkOuter + 0.4f + shop.z * 0.5f)), side > 0 ? 180f : 0f);
            }
            Shops(-118f, HutongX - HutongHalf - 3f, 1);
            Shops(HutongX + HutongHalf + 3f, 12f, 1);
            Shops(88f, MarketX - MarketHalf - 2f, 1);
            Shops(MarketX + MarketHalf + 2f, 118f, 1);
            Shops(-118f, -20f, -1);
            Shops(18f, 60f, -1);
            Shops(80f, 118f, -1);
            // Lot back wall and side walls (the lot is entered from the avenue).
            Transform lot = Group("Lot");
            float lotX1 = LotX0 + LotBays * LotBayW;
            for (int i = 0; i <= LotBays; i++)
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, lot, "BayLine", new Vector3(LotX0 + i * LotBayW, 0.03f, LotRowZ), Quaternion.identity, new Vector3(0.12f, 0.02f, LotBayD), "LineWhite", false);
            RealGarageBuilder.Box(lot, "LotWall", new Vector3((LotX0 + lotX1) * 0.5f, 0.75f, LotRowZ + LotBayD * 0.5f + 0.6f), new Vector3(lotX1 - LotX0 + 6f, 1.5f, 0.4f), "M_ConcreteWall", 1.5f);
            NY.Boundary(lot, new Vector3(lotX1 + 3.2f, 1.5f, (WalkOuter + 38f) * 0.5f), new Vector3(0.4f, 3f, 38f - WalkOuter));
            NY.Boundary(lot, new Vector3(LotX0 - 12f + 0.2f, 1.5f, 24f), new Vector3(0.4f, 3f, 26f));
            Put(lot, P("Hall_Big"), new Vector3(52f, 0f, 62f), 180f);

            // CN2 hutong: two-storey halls both sides, kerb on the right (east) side, lamp + lanterns.
            Transform hut = Group("Hutong");
            Vector3 house = Size("Hall_House");
            float kerbX = HutongX + HutongHalf - KerbW * 0.5f;
            Transform k = RealGarageBuilder.Box(hut, "Kerb", new Vector3(kerbX, KerbH * 0.5f, (WalkOuter + HutongEndZ) * 0.5f), new Vector3(KerbW, KerbH, HutongEndZ - WalkOuter - 4f), "M_KerbStone", 1f);
            k.gameObject.AddComponent<KerbMarker>();
            for (float z = WalkOuter + 3f; z + house.x <= HutongEndZ - 1f; z += house.x + 0.6f)
            {
                Put(hut, P("Hall_House"), new Vector3(HutongX - HutongHalf - 1.6f - house.z * 0.5f, 0f, z + house.x * 0.5f), 90f);
                Put(hut, P("Hall_House"), new Vector3(HutongX + HutongHalf + 1.6f + house.z * 0.5f, 0f, z + house.x * 0.5f), 270f);
            }
            Put(hut, P("Hall_Big"), new Vector3(HutongX, 0f, HutongEndZ + 11f), 180f);
            NY.Boundary(hut, new Vector3(HutongX - HutongHalf - 0.3f, 1.5f, (WalkOuter + HutongEndZ) * 0.5f), new Vector3(0.4f, 3f, HutongEndZ - WalkOuter));
            NY.Boundary(hut, new Vector3(HutongX + HutongHalf + 0.3f, 1.5f, (WalkOuter + HutongEndZ) * 0.5f), new Vector3(0.4f, 3f, HutongEndZ - WalkOuter));
            NY.Boundary(hut, new Vector3(HutongX, 1.5f, HutongEndZ + 0.2f), new Vector3(HutongHalf * 2f + 1f, 3f, 0.4f));
            for (float z = 22f; z < HutongEndZ - 6f; z += 14f) Put(hut, P("Lamp_Street"), new Vector3(HutongX - HutongHalf + 0.5f, 0f, z), 90f);
            for (float z = WalkOuter + 3f; z < HutongEndZ - 4f; z += 5f) Put(hut, P("Lantern"), new Vector3(HutongX, 3.8f, z), 0f);

            // CN3 night market: shops both sides, strings of lanterns over the lane.
            Transform mk = Group("NightMarket");
            for (float z = WalkOuter + 1f; z + shop.x <= MarketEndZ; z += shop.x + 0.2f)
            {
                Put(mk, P("Shop_Row"), new Vector3(MarketX - MarketHalf - 0.8f - shop.z * 0.5f, 0f, z + shop.x * 0.5f), 90f);
                Put(mk, P("Shop_Row"), new Vector3(MarketX + MarketHalf + 0.8f + shop.z * 0.5f, 0f, z + shop.x * 0.5f), 270f);
            }
            NY.Boundary(mk, new Vector3(MarketX - MarketHalf - 0.4f, 1.5f, (WalkOuter + MarketEndZ) * 0.5f), new Vector3(0.4f, 3f, MarketEndZ - WalkOuter));
            NY.Boundary(mk, new Vector3(MarketX + MarketHalf + 0.4f, 1.5f, (WalkOuter + MarketEndZ) * 0.5f), new Vector3(0.4f, 3f, MarketEndZ - WalkOuter));
            NY.Boundary(mk, new Vector3(MarketX, 1.5f, MarketEndZ + 0.2f), new Vector3(MarketHalf * 2f + 1f, 3f, 0.4f));
            for (float z = WalkOuter + 2f; z < MarketEndZ - 2f; z += 2.6f)
                foreach (float x in new[] { MarketX - 1.3f, MarketX, MarketX + 1.3f }) Put(mk, P("Lantern"), new Vector3(x, 3.4f, z), z * 17f);

            // Street lamps along the avenue.
            Transform street = Group("Street");
            for (float x = -112f; x <= 112f; x += 22f)
            {
                if (Mathf.Abs(x - HutongX) > 6f && Mathf.Abs(x - MarketX) > 6f && (x < 10f || x > 90f)) Put(street, P("Lamp_Street"), new Vector3(x, 0f, RoadHalf + 0.7f), 180f);
                Put(street, P("Lamp_Street"), new Vector3(x + 11f, 0f, -RoadHalf - 0.7f), 0f);
            }

            // Dusk: low warm sun, blue-violet ambient, haze (lanterns glow against it).
            var light = root.AddComponent<SceneLightingOverride>();
            var lso = new SerializedObject(light);
            lso.FindProperty("ambientSky").colorValue = new Color(0.6f, 0.58f, 0.7f);
            lso.FindProperty("ambientEquator").colorValue = new Color(0.55f, 0.47f, 0.46f);
            lso.FindProperty("ambientGround").colorValue = new Color(0.3f, 0.26f, 0.26f);
            lso.FindProperty("fog").boolValue = true;
            lso.FindProperty("fogColor").colorValue = new Color(0.5f, 0.42f, 0.46f);
            lso.FindProperty("fogDensity").floatValue = 0.004f;
            lso.FindProperty("sunColor").colorValue = new Color(1f, 0.62f, 0.4f);
            lso.FindProperty("sunIntensity").floatValue = 1.25f;
            lso.FindProperty("sunEuler").vector3Value = new Vector3(24f, -50f, 0f);
            lso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, EnvPath);
            Object.DestroyImmediate(root);
            Debug.Log("[Arena] Env_Beijing written");
        }

        public static void BuildLevels()
        {
            var env = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPath);
            LevelData m1 = Mission1(env), m2 = Mission2(env), m3 = Mission3(env);
            W.WriteCatalog(CountryId, Arena, m1, m2, m3);
            Debug.Log("[Arena] China missions written");
        }

        private static GameObject Parked(string body) => RealPropFactory.Load("Parked_" + body);
        private static GameObject Car(int k) => (k % 6) switch
        {
            0 => F.Load("London", "Saloon_67") ?? Parked("sedan"), 1 => Parked("hatchback"), 2 => Parked("suv"), 3 => F.Load("Tokyo", "Sedan_Kiri94") ?? Parked("wagon"),
            4 => Parked("compact"), _ => Parked("sedan"),
        };

        /// <summary>CN1 — Beijing Rush: 25 seconds from the lot entrance to the free bay; knocks cost time.</summary>
        private static LevelData Mission1(GameObject env)
        {
            var props = new List<PropPlacement>();
            for (int i = 0; i < LotBays; i++)
            {
                if (i == LotTarget || i == 5) continue;
                W.Place(props, Car(i), new Vector3(LotX0 + LotBayW * (i + 0.5f), 0f, LotRowZ), 180f + (i % 4 == 1 ? 180f : 0f));
            }
            W.Place(props, Car(2), new Vector3(60f, 0f, 20f), 90f);       // shopper stopped in the aisle
            Vector3 spawn = new Vector3(-30f, 0f, -3.25f);
            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 9f, maxSpeedKmh = 2f, holdDuration = 1.0f };
            return W.Write(new W.Mission
            {
                id = "CN1", countryId = CountryId, arenaFolder = Arena, number = 1, name = "Beijing Rush", type = MissionType.TimedParking, road = Road, environment = env, props = props,
                scoreMode = ScoreMode.Quality, quality = (0.45f, 0.2f),
                briefing = "Rush hour. Drive up the avenue, turn into the car park and take the free bay before the clock runs out. You get 25 seconds from the entrance; every knock costs 3.",
                spawn = spawn, spawnYaw = 90f, time = 120f, collisions = 3, three = (60f, 0), two = (90f, 1), coins = (210, 50),
                objectives = new MissionObjective[]
                {
                    new CheckpointObjective("Turn into the car park", new Vector3(18f, 0f, 16f), 0f, new Vector2(8f, 4f), "TURN LEFT INTO THE CAR PARK"),
                    new TimedParkObjective("Beat the clock to the free bay", new Vector3(LotX0 + LotBayW * (LotTarget + 0.5f), 0f, LotRowZ), 0f, new Vector2(LotBayW - 0.1f, LotBayD), ParkDirection.NoseIn, 25f, 3f, rules),
                },
            });
        }

        /// <summary>CN2 — Hutong Kerb: parallel park at the right-hand kerb of a narrow hutong lane.</summary>
        private static LevelData Mission2(GameObject env)
        {
            var props = new List<PropPlacement>();
            float parkX = HutongX + HutongHalf - 1.05f;
            W.Place(props, Car(0), new Vector3(parkX, 0f, 27f), 0f);
            W.Place(props, Car(4), new Vector3(parkX, 0f, 41.5f), 0f);
            W.Place(props, Car(1), new Vector3(HutongX - HutongHalf + 1.2f, 0f, 50f), 180f);           // opposite side: squeeze past
            Vector3 spawn = new Vector3(HutongX + 1.75f, 0f, 14f);
            var rules = new ParkingRules { edgeMargin = 0.08f, angleTolerance = 8f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            return W.Write(new W.Mission
            {
                id = "CN2", countryId = CountryId, arenaFolder = Arena, number = 2, name = "Hutong Kerb", type = MissionType.ParallelParking, road = Road, environment = env, props = props,
                briefing = "Up the narrow hutong, between the old courtyard houses, and parallel park at the right-hand kerb between the two cars. Finish within 0.4 m of the kerb; three kerb strikes fail.",
                spawn = spawn, spawnYaw = 0f, time = 150f, collisions = 2, three = (70f, 0), two = (105f, 1), coins = (230, 55),
                objectives = new MissionObjective[]
                {
                    new ParallelParkObjective("Parallel park at the kerb", new Vector3(parkX, 0f, 34.4f), 0f, new Vector2(2.4f, 6.4f), TrafficSide.KerbSide(Road), 0.4f, 2, rules),
                },
            });
        }

        /// <summary>CN3 — Night Market: drive the lantern-lit lane between stalls, then park precisely at the end.</summary>
        private static LevelData Mission3(GameObject env)
        {
            var props = new List<PropPlacement>();
            W.Place(props, RealPropFactory.Load("Cones_Cluster"), new Vector3(MarketX - 1.1f, 0f, 30f), 0f);
            W.Place(props, RealPropFactory.Load("Barrel"), new Vector3(MarketX + 1.3f, 0f, 38f), 0f);
            Vector3 spawn = new Vector3(MarketX - 8f, 0f, -3.25f);
            var rules = new ParkingRules { edgeMargin = 0.04f, angleTolerance = 7f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            return W.Write(new W.Mission
            {
                id = "CN3", countryId = CountryId, arenaFolder = Arena, number = 3, name = "Night Market", type = MissionType.NightParking, road = Road, environment = env, props = props,
                scoreMode = ScoreMode.Quality, quality = (0.8f, 0.5f), cameraHeight = 2.4f, cameraDistance = 6f,
                briefing = "Dusk at the night market. Turn into the lantern-lit lane — barely wider than the car — pick your way past the cones and barrel, and park dead centre at the far end.",
                spawn = spawn, spawnYaw = 90f, time = 170f, collisions = 2, three = (85f, 0), two = (125f, 1), coins = (250, 60),
                objectives = new MissionObjective[]
                {
                    new NarrowAlleyObjective("Through the market lane", new Vector3(MarketX, 0f, WalkOuter + 1f), new Vector3(MarketX, 0f, 41f), MarketHalf * 2f, maxScrapes: 2, reverseThrough: false, goodClearance: 0.3f),
                    new PrecisionParkObjective("Park at the end of the lane", new Vector3(MarketX, 0f, 47f), 0f, new Vector2(2.8f, 5.5f), ParkDirection.NoseIn, maxOffset: 0.35f, maxAngle: 6f, minAccuracy: 0.5f, rules),
                },
            });
        }
    }
}
