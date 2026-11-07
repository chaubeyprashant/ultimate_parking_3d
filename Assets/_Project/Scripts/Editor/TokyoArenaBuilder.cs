using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Cars;
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
    /// Japan — Tokyo arena, left-hand traffic, built from the imported Japan models: three residential homes, a
    /// konbini, drink and snack vending machines, an elevated expressway with its street lamps, a rotary tower car park
    /// (landmark), two-level hydraulic stackers (the working mechanical parking), Japanese hatchbacks and a box truck,
    /// stone lanterns. A main street with a coin-parking lot, a narrow residential alley and a stacker car park.
    /// </summary>
    public static class TokyoArenaBuilder
    {
        public const string Arena = "Tokyo";
        public const string CountryId = "jp";
        public static string EnvPath => F.PrefabPath(Arena, "Env_Tokyo");
        public const RoadDirection Road = RoadDirection.LeftHand;

        public const float RoadHalf = 4.5f, WalkOuter = 8f;
        // JP1 coin parking (south): bays along X, entered from the road side, 2.4 m wide.
        public const float LotX0 = -42f, LotBayW = 2.4f, LotFrontZ = -WalkOuter - 2f, LotBayD = 5f;
        public const int LotBays = 11, LotTarget = 6;
        public static float LotBayZ => LotFrontZ - 2.5f - LotBayD * 0.5f;      // a 2.5 m aisle then the bays
        // JP2 alley (north)
        public const float AlleyX = 10f, AlleyHalf = 1.5f, AlleyEndZ = 40f;
        // JP3 stackers (south-east): three side by side, decks entered from the road (nose-in heading -Z).
        public static readonly float[] StackerX = { 50f, 54.4f, 58.8f };
        public const int StackerTarget = 1;
        public const float StackerZ = -WalkOuter - 9.5f, StackerRise = 2.2f, DeckTopLocal = 0.66f;
        // Expressway crossing overhead along Z.
        public const float ExpresswayX = 30f;

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
                // Homes are modelled in centimetres, front door to -X (yaw 90 turns it to +Z).
                new F.Spec { name = "Home_A", source = "Japan/jp_home_01.glb", yaw = 90f, fit = F.Fit.Scale, size = 0.01f },
                new F.Spec { name = "Home_B", source = "Japan/jp_home_02.glb", yaw = 90f, fit = F.Fit.Scale, size = 0.01f },
                new F.Spec { name = "Home_C", source = "Japan/jp_home_03.glb", yaw = 90f, fit = F.Fit.Scale, size = 0.01f },
                new F.Spec { name = "Konbini", source = "Japan/jp_konbini.glb" },
                new F.Spec { name = "Vending_Snacks", source = "Japan/jp_vending.glb", yaw = 90f, fit = F.Fit.Height, size = 1.83f },
                new F.Spec { name = "Vending_Drinks", source = "Japan/jp_vending_drinks.glb", fit = F.Fit.Height, size = 1.83f },
                new F.Spec { name = "Stone_Lantern", source = "Japan/jp_street_lamp.glb", fit = F.Fit.Height, size = 1.7f },
                new F.Spec { name = "Lamp_Street", source = "Japan/jp_storefront.glb", pick = Exact("A_LampStreet"), fit = F.Fit.Height, size = 7.3f },
                new F.Spec { name = "Expressway", source = "Japan/jp_storefront.glb", pick = Exact("A_Highway_v1"), collider = F.ColliderKind.None },
                new F.Spec { name = "Tower_Parking", source = "Japan/jp_parking_lift.glb", fit = F.Fit.Height, size = 18f },
                // Stacker: posts/frame (static) and deck (moves), both at 0.46 (the lift is modelled ~2.2x real size).
                new F.Spec { name = "Stacker_Frame", source = "Japan/jp_hydraulic_lift.glb", pick = Exact("Solid_154.002_body_0", "Solid_154.002_bolts_0", "Solid_154.002_hydraulic_0"),
                    yaw = 90f, fit = F.Fit.Scale, size = 0.46f, collider = F.ColliderKind.None },
                new F.Spec { name = "Stacker_Deck", source = "Japan/jp_hydraulic_lift.glb", pick = Exact("Solid_154.002_platform_0"), yaw = 90f, fit = F.Fit.Scale, size = 0.46f, collider = F.ColliderKind.None },
                // Vehicles (front along -X: yaw 90)
                new F.Spec { name = "Coupe_Kiri86", source = "Japan/jp_car_kiri86.glb", yaw = 90f, fit = F.Fit.Length, size = 4.2f, colliderHeightCap = 1.4f, strip = new[] { "Interior" } },
                new F.Spec { name = "Sedan_Kiri94", source = "Japan/jp_car_kiri94.glb", yaw = 90f, fit = F.Fit.Length, size = 4.4f, colliderHeightCap = 1.45f, strip = new[] { "INTERIOR", "Interior" } },
                new F.Spec { name = "Box_Truck", source = "Japan/jp_car_lct95.glb", yaw = 90f, fit = F.Fit.Length, size = 5.8f, colliderHeightCap = 2.6f, strip = new[] { "Interior" } },
            };
            foreach (F.Spec s in specs) F.Make(Arena, s);
            BuildExpresswaySegment();
            BuildStacker();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arena] Tokyo props: {specs.Count + 2}");
        }

        private static GameObject P(string name)
        {
            GameObject g = F.Load(Arena, name);
            if (g == null) Debug.LogError("[Arena] Tokyo prefab missing: " + name);
            return g;
        }

        /// <summary>Expressway section with a collider only on its central pier (traffic passes underneath).</summary>
        private static void BuildExpresswaySegment()
        {
            var root = new GameObject("Expressway_Segment");
            var e = (GameObject)PrefabUtility.InstantiatePrefab(P("Expressway"), root.transform);
            Bounds b = NY.RenderBounds(e);
            var pier = root.AddComponent<BoxCollider>();
            pier.size = new Vector3(2.6f, b.size.y * 0.62f, 2.6f);
            pier.center = new Vector3(0f, pier.size.y * 0.5f, 0f);
            PrefabUtility.SaveAsPrefabAsset(root, F.PrefabPath(Arena, "Expressway_Segment"));
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Working two-level stacker: imported frame + deck, a deck collider flush with the ground, side post colliders
        /// and a <see cref="MechanicalParkingSystem"/> that lifts the deck 2.2 m (negative depth) with the car on it.
        /// </summary>
        private static void BuildStacker()
        {
            var root = new GameObject("Stacker");
            var frame = (GameObject)PrefabUtility.InstantiatePrefab(P("Stacker_Frame"), root.transform);
            var deckRoot = new GameObject("Deck").transform;
            deckRoot.SetParent(root.transform, false);
            var deck = (GameObject)PrefabUtility.InstantiatePrefab(P("Stacker_Deck"), deckRoot);
            Bounds db = NY.RenderBounds(deck);
            deck.transform.localPosition = new Vector3(0f, -db.size.y + 0.05f, 0f);   // deck top flush with the ground (pit installation)
            var top = deckRoot.gameObject.AddComponent<BoxCollider>();
            top.size = new Vector3(db.size.x, 0.12f, db.size.z);
            top.center = new Vector3(0f, -0.01f, 0f);
            Bounds fb = NY.RenderBounds(frame);
            foreach (float sx in new[] { -1f, 1f })
            {
                var post = new GameObject("PostCollider").AddComponent<BoxCollider>();
                post.transform.SetParent(root.transform, false);
                post.size = new Vector3(0.25f, fb.size.y, fb.size.z * 0.8f);
                post.center = new Vector3(sx * (db.size.x * 0.5f + 0.2f), fb.size.y * 0.5f, 0f);
            }
            var system = root.AddComponent<MechanicalParkingSystem>();
            system.Configure(deckRoot, null, new Vector2(db.size.x - 0.3f, db.size.z - 0.3f), -StackerRise, Vector3.zero);
            var so = new SerializedObject(system);
            so.FindProperty("systemId").stringValue = "stacker";
            so.FindProperty("safetySeconds").floatValue = 1.5f;
            so.FindProperty("gateSeconds").floatValue = 0.2f;
            so.FindProperty("lowerSeconds").floatValue = 3f;
            so.FindProperty("shiftSeconds").floatValue = 0.6f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, F.PrefabPath(Arena, "Stacker"));
            Object.DestroyImmediate(root);
            Debug.Log($"[Arena] Tokyo stacker deck {db.size}, frame {fb.size}");
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Tokyo Arena")]
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
            foreach (string id in new[] { "JP1", "JP2", "JP3" })
                WorldTourBuilder.RenderShots(AssetDatabase.LoadAssetAtPath<LevelData>($"{W.Dir}/Level_{id}.asset"), dir);
        }

        // ------------------------------------------------------------------ environment

        public static void BuildEnvironment()
        {
            var root = new GameObject("Env_Tokyo");
            Transform Group(string n) { var g = new GameObject(n).transform; g.SetParent(root.transform, false); return g; }
            GameObject Put(Transform parent, GameObject prefab, Vector3 pos, float yaw)
            {
                if (prefab == null) { Debug.LogError("[Arena] Tokyo missing prefab at " + pos); return null; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                return go;
            }
            Vector3 Size(string prefab) { GameObject p = P(prefab); var box = p != null ? p.GetComponent<BoxCollider>() : null; return box != null ? box.size : new Vector3(10f, 8f, 10f); }

            Material pavement = MumbaiArenaBuilder.Mat("M_TKY_Pavement", "concrete_pavement_diff_1k.jpg", "concrete_pavement_nor_gl_1k.jpg", 35f, new Color(0.78f, 0.78f, 0.76f));
            Material asphalt = MumbaiArenaBuilder.Mat("M_TKY_Asphalt", "Road007_1K-JPG_Color.jpg", "Road007_1K-JPG_NormalGL.jpg", 10f, new Color(0.6f, 0.6f, 0.62f));
            Material walkway = MumbaiArenaBuilder.Mat("M_TKY_Walk", "red_sandstone_pavement_diff_1k.jpg", "red_sandstone_pavement_nor_gl_1k.jpg", 20f, new Color(0.75f, 0.68f, 0.64f));

            Transform ground = Group("Ground");
            var slab = new GameObject("DriveSurface"); slab.transform.SetParent(ground, false);
            var col = slab.AddComponent<BoxCollider>(); col.size = new Vector3(220f, 1f, 160f); col.center = new Vector3(0f, -0.5f, 0f);
            NY.Surface(ground, "Ground", new Vector3(0f, -0.02f, 0f), new Vector2(220f, 160f), pavement);
            NY.Surface(ground, "MainStreet", new Vector3(0f, 0.004f, 0f), new Vector2(218f, RoadHalf * 2f), asphalt);
            NY.Surface(ground, "WalkNorth", new Vector3(0f, 0.003f, (RoadHalf + WalkOuter) * 0.5f), new Vector2(218f, WalkOuter - RoadHalf), walkway);
            NY.Surface(ground, "WalkSouth", new Vector3(0f, 0.003f, -(RoadHalf + WalkOuter) * 0.5f), new Vector2(218f, WalkOuter - RoadHalf), walkway);
            float lotX1 = LotX0 + LotBays * LotBayW;
            NY.Surface(ground, "CoinLot", new Vector3((LotX0 + lotX1) * 0.5f, 0.005f, (LotFrontZ + LotBayZ - LotBayD * 0.5f) * 0.5f), new Vector2(lotX1 - LotX0 + 1f, LotFrontZ - (LotBayZ - LotBayD * 0.5f) + 2f), asphalt);
            NY.Surface(ground, "Alley", new Vector3(AlleyX, 0.005f, (WalkOuter + AlleyEndZ) * 0.5f), new Vector2(AlleyHalf * 2f + 0.2f, AlleyEndZ - WalkOuter), asphalt);
            NY.Surface(ground, "StackerApron", new Vector3(54.4f, 0.005f, -WalkOuter - 4f), new Vector2(18f, 8f), asphalt);
            NY.Boundary(ground, new Vector3(0f, 2f, 79f), new Vector3(220f, 4f, 1f));
            NY.Boundary(ground, new Vector3(0f, 2f, -79f), new Vector3(220f, 4f, 1f));
            NY.Boundary(ground, new Vector3(-109f, 2f, 0f), new Vector3(1f, 4f, 160f));
            NY.Boundary(ground, new Vector3(109f, 2f, 0f), new Vector3(1f, 4f, 160f));

            // Markings: white edge lines, white centre dashes, a ladder crosswalk by the konbini.
            Transform marks = Group("Markings");
            foreach (float z in new[] { -RoadHalf + 0.25f, RoadHalf - 0.25f })
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, marks, "EdgeLine", new Vector3(0f, 0.02f, z), Quaternion.identity, new Vector3(216f, 0.01f, 0.15f), "LineWhite", false);
            for (float x = -106f; x < 106f; x += 8f)
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, marks, "CentreDash", new Vector3(x, 0.02f, 0f), Quaternion.identity, new Vector3(5f, 0.01f, 0.15f), "LineWhite", false);
            for (int i = 0; i < 8; i++)
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, marks, "Crosswalk", new Vector3(-12f, 0.021f, -RoadHalf + 0.6f + i * 1.15f), Quaternion.identity, new Vector3(4f, 0.01f, 0.45f), "LineWhite", false);

            // Houses both sides, gaps for the alley, the expressway piers, the coin lot and the stackers.
            Transform north = Group("NorthRow"), south = Group("SouthRow");
            string[] homes = { "Home_A", "Home_B", "Home_C", "Home_B", "Home_A" };
            void Row(Transform parent, float x0, float x1, int side, int start)
            {
                float x = x0; int i = start;
                while (true)
                {
                    string n = homes[i % homes.Length];
                    Vector3 sz = Size(n);
                    if (x + sz.x > x1 + 0.2f) break;
                    float z = side * (WalkOuter + 0.6f + sz.z * 0.5f);
                    Put(parent, P(n), new Vector3(x + sz.x * 0.5f, 0f, z), side > 0 ? 180f : 0f);
                    x += sz.x + 0.8f; i++;          // Tokyo homes stand a narrow gap apart
                }
            }
            Vector3 konbini = Size("Konbini");
            Row(north, -105f, -36f, 1, 0);
            Put(north, P("Konbini"), new Vector3(-36f + 0.8f + konbini.x * 0.5f, 0f, WalkOuter + 0.6f + konbini.z * 0.5f), 180f);
            Row(north, -36f + konbini.x + 2f, AlleyX - AlleyHalf - 0.1f, 1, 2);
            Row(north, AlleyX + AlleyHalf + 0.1f, ExpresswayX - 9f, 1, 1);
            Row(north, ExpresswayX + 9f, 75f, 1, 3);
            Put(north, P("Tower_Parking"), new Vector3(90f, 0f, WalkOuter + 1f + Size("Tower_Parking").z * 0.5f), 180f);
            Row(south, -105f, LotX0 - 2f, -1, 4);
            Row(south, lotX1 + 2f, ExpresswayX - 9f, -1, 0);
            Row(south, ExpresswayX + 9f, StackerX[0] - 4f, -1, 2);
            Row(south, StackerX[2] + 4f, 105f, -1, 1);
            // Alley back: a house closing the alley end.
            Put(north, P("Home_C"), new Vector3(AlleyX, 0f, AlleyEndZ + 0.3f + Size("Home_C").z * 0.5f), 180f);
            NY.Boundary(north, new Vector3(AlleyX - AlleyHalf - 0.15f, 1.5f, (WalkOuter + AlleyEndZ) * 0.5f + 1f), new Vector3(0.3f, 3f, AlleyEndZ - WalkOuter + 2f));
            NY.Boundary(north, new Vector3(AlleyX + AlleyHalf + 0.15f, 1.5f, (WalkOuter + AlleyEndZ) * 0.5f + 1f), new Vector3(0.3f, 3f, AlleyEndZ - WalkOuter + 2f));
            NY.Boundary(north, new Vector3(AlleyX, 1.5f, AlleyEndZ + 0.15f), new Vector3(AlleyHalf * 2f + 0.6f, 3f, 0.3f));

            // JP1 coin lot: bay lines, a block wall behind, vending machines at the corner.
            Transform lot = Group("CoinLot");
            float backZ = LotBayZ - LotBayD * 0.5f;
            for (int i = 0; i <= LotBays; i++)
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, lot, "BayLine", new Vector3(LotX0 + i * LotBayW, 0.03f, LotBayZ), Quaternion.identity, new Vector3(0.12f, 0.02f, LotBayD), "LineWhite", false);
            RealGarageBuilder.Box(lot, "BlockWall", new Vector3((LotX0 + lotX1) * 0.5f, 0.6f, backZ - 0.35f), new Vector3(lotX1 - LotX0 + 2f, 1.2f, 0.25f), "M_ConcreteWall", 1.5f);
            RealGarageBuilder.Box(lot, "BlockWallW", new Vector3(LotX0 - 1.1f, 0.6f, (LotFrontZ + backZ) * 0.5f - 0.2f), new Vector3(0.25f, 1.2f, LotFrontZ - backZ + 0.4f), "M_ConcreteWall", 1.5f);
            RealGarageBuilder.Box(lot, "BlockWallE", new Vector3(lotX1 + 1.1f, 0.6f, (LotFrontZ + backZ) * 0.5f - 0.2f), new Vector3(0.25f, 1.2f, LotFrontZ - backZ + 0.4f), "M_ConcreteWall", 1.5f);
            Put(lot, P("Vending_Drinks"), new Vector3(lotX1 + 0.4f, 0f, LotFrontZ + 0.6f), 0f);
            Put(lot, P("Vending_Snacks"), new Vector3(lotX1 + 1.7f, 0f, LotFrontZ + 0.6f), 0f);

            // JP3 stackers + apron.
            Transform stack = Group("Stackers");
            foreach (float x in StackerX) Put(stack, P("Stacker"), new Vector3(x, 0f, StackerZ), 180f);
            // Only the middle machine runs (ids must be unique): the others become decoration.
            var machines = stack.GetComponentsInChildren<MechanicalParkingSystem>();
            for (int i = 0; i < machines.Length; i++)
                if (i != StackerTarget)
                {
                    Transform deck = machines[i].Platform;
                    deck.localPosition += Vector3.up * StackerRise;                 // parked up, a car on top (mission props)
                    Object.DestroyImmediate(machines[i]);
                }
            RealGarageBuilder.Box(stack, "StackerWall", new Vector3(54.4f, 1.5f, StackerZ - 3.4f), new Vector3(14f, 3f, 0.3f), "M_ConcreteWall", 2f);

            // Expressway crossing overhead along Z, piers clear of the carriageway.
            Transform xway = Group("Expressway");
            for (int k = -4; k < 4; k++) Put(xway, P("Expressway_Segment"), new Vector3(ExpresswayX, 0f, 15.7f * (k + 0.5f)), 0f);

            // Street furniture.
            Transform street = Group("Street");
            for (float x = -100f; x <= 100f; x += 20f)
            {
                if (Mathf.Abs(x - AlleyX) > 4f && Mathf.Abs(x - ExpresswayX) > 3f) Put(street, P("Lamp_Street"), new Vector3(x, 0f, RoadHalf + 0.6f), 180f);
                if (Mathf.Abs(x + 10f - ExpresswayX) > 3f) Put(street, P("Lamp_Street"), new Vector3(x + 10f, 0f, -RoadHalf - 0.6f), 0f);
            }
            Put(street, P("Vending_Drinks"), new Vector3(AlleyX + AlleyHalf + 0.9f, 0f, WalkOuter + 0.5f), 180f);
            Put(street, P("Vending_Snacks"), new Vector3(-60f, 0f, WalkOuter + 0.4f), 180f);
            Put(street, P("Stone_Lantern"), new Vector3(AlleyX - AlleyHalf - 0.8f, 0f, WalkOuter + 0.8f), 180f);

            // Warm late-afternoon light.
            var light = root.AddComponent<SceneLightingOverride>();
            var lso = new SerializedObject(light);
            lso.FindProperty("ambientSky").colorValue = new Color(0.62f, 0.64f, 0.7f);
            lso.FindProperty("ambientEquator").colorValue = new Color(0.56f, 0.5f, 0.46f);
            lso.FindProperty("ambientGround").colorValue = new Color(0.3f, 0.28f, 0.27f);
            lso.FindProperty("fog").boolValue = true;
            lso.FindProperty("fogColor").colorValue = new Color(0.86f, 0.78f, 0.7f);
            lso.FindProperty("fogDensity").floatValue = 0.0035f;
            lso.FindProperty("sunColor").colorValue = new Color(1f, 0.84f, 0.66f);
            lso.FindProperty("sunIntensity").floatValue = 1.2f;
            lso.FindProperty("sunEuler").vector3Value = new Vector3(28f, 120f, 0f);
            lso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, EnvPath);
            Object.DestroyImmediate(root);
            Debug.Log("[Arena] Env_Tokyo written");
        }

        // ------------------------------------------------------------------ missions

        public static void BuildLevels()
        {
            var env = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPath);
            LevelData m1 = Mission1(env), m2 = Mission2(env), m3 = Mission3(env);
            W.WriteCatalog(CountryId, Arena, m1, m2, m3);
            Debug.Log("[Arena] Tokyo missions written");
        }

        private static GameObject Parked(string body) => RealPropFactory.Load("Parked_" + body);
        private static CarData Hatch => AssetDatabase.LoadAssetAtPath<CarData>("Assets/_Project/Cars/Car_Hatch.asset");
        private static GameObject JpCar(int k) => (k % 4) switch { 0 => P("Coupe_Kiri86"), 1 => P("Sedan_Kiri94"), 2 => Parked("compact"), _ => Parked("hatchback") };

        /// <summary>JP1 — Coin Parking: reverse the compact hatch into a 2.4 m bay, centred.</summary>
        private static LevelData Mission1(GameObject env)
        {
            var props = new List<PropPlacement>();
            for (int i = 0; i < LotBays; i++)
            {
                if (i == LotTarget || i == 2) continue;
                // Most backed in (Japanese habit): facing the road (+Z).
                W.Place(props, JpCar(i), new Vector3(LotX0 + LotBayW * (i + 0.5f), 0f, LotBayZ), i % 3 == 1 ? 180f : 0f);
            }
            W.Place(props, P("Box_Truck"), new Vector3(-20f, 0f, -RoadHalf + 1.4f), TrafficSide.ParkedYaw(90f, -1, Road));
            Vector3 spawn = TrafficSide.LaneCentre(new Vector3(5f, 0f, 0f), Vector3.left, 4.5f, Road);
            var rules = new ParkingRules { edgeMargin = 0.04f, angleTolerance = 6f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            return W.Write(new W.Mission
            {
                id = "JP1", countryId = CountryId, arenaFolder = Arena, number = 1, name = "Coin Parking", type = MissionType.PrecisionParking, road = Road, environment = env, props = props,
                forcedCar = Hatch, scoreMode = ScoreMode.Quality, quality = (0.85f, 0.6f),
                briefing = "Tokyo-size bays: 2.4 m wide. Turn left into the coin parking lot and reverse the compact hatch into the free bay, centred and straight — the Japanese way.",
                spawn = spawn, spawnYaw = 270f, time = 140f, collisions = 2, three = (70f, 0), two = (100f, 1), coins = (230, 55),
                objectives = new MissionObjective[]
                {
                    new PrecisionParkObjective("Reverse into the coin bay", new Vector3(LotX0 + LotBayW * (LotTarget + 0.5f), 0f, LotBayZ), 180f, new Vector2(LotBayW - 0.1f, LotBayD),
                        ParkDirection.ReverseIn, maxOffset: 0.22f, maxAngle: 4f, minAccuracy: 0.55f, rules),
                },
            });
        }

        /// <summary>JP2 — Alley Reverse: reverse the hatch down a 3 m residential alley into the carport at the end.</summary>
        private static LevelData Mission2(GameObject env)
        {
            var props = new List<PropPlacement>();
            W.Place(props, P("Box_Truck"), new Vector3(AlleyX - 9f, 0f, RoadHalf - 1.4f), TrafficSide.ParkedYaw(90f, +1, Road));   // delivery truck by the alley
            W.Place(props, P("Sedan_Kiri94"), new Vector3(AlleyX + 14f, 0f, -RoadHalf + 1.2f), TrafficSide.ParkedYaw(90f, -1, Road));
            Vector3 spawn = TrafficSide.LaneCentre(new Vector3(-30f, 0f, 0f), Vector3.right, 4.5f, Road);
            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 7f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            return W.Write(new W.Mission
            {
                id = "JP2", countryId = CountryId, arenaFolder = Arena, number = 2, name = "Alley Reverse", type = MissionType.NarrowRoute, road = Road, environment = env, props = props,
                forcedCar = Hatch, scoreMode = ScoreMode.Quality, quality = (0.8f, 0.5f),
                briefing = "Pass the alley, stop, and reverse the hatch all the way down it — 3 m between the walls — into the carport at the end. Two scrapes allowed.",
                spawn = spawn, spawnYaw = 90f, time = 170f, collisions = 3, three = (85f, 0), two = (120f, 1), coins = (240, 55),
                objectives = new MissionObjective[]
                {
                    new NarrowAlleyObjective("Reverse down the alley", new Vector3(AlleyX, 0f, WalkOuter + 0.5f), new Vector3(AlleyX, 0f, AlleyEndZ - 2f), AlleyHalf * 2f, maxScrapes: 2, reverseThrough: true, goodClearance: 0.4f),
                    new ReverseParkObjective("Into the carport", new Vector3(AlleyX, 0f, AlleyEndZ - 2.8f), 0f, new Vector2(AlleyHalf * 2f - 0.1f, 5.4f), maxRearGap: 0.7f, rules),
                },
            });
        }

        /// <summary>JP3 — Stacker Parking: drive onto the stacker deck; the machine lifts the car to the upper level.</summary>
        private static LevelData Mission3(GameObject env)
        {
            var props = new List<PropPlacement>();
            for (int i = 0; i < StackerX.Length; i++)
            {
                if (i == StackerTarget) continue;
                W.Place(props, JpCar(i + 1), new Vector3(StackerX[i], StackerRise + 0.06f, StackerZ), 180f);   // on the raised deck
                W.Place(props, JpCar(i + 2), new Vector3(StackerX[i], 0f, StackerZ), 180f);                     // underneath
            }
            W.Place(props, P("Box_Truck"), new Vector3(70f, 0f, -RoadHalf + 1.4f), TrafficSide.ParkedYaw(90f, -1, Road));
            Vector3 spawn = TrafficSide.LaneCentre(new Vector3(90f, 0f, 0f), Vector3.left, 4.5f, Road);
            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 6f, maxSpeedKmh = 2f, holdDuration = 1.0f };
            return W.Write(new W.Mission
            {
                id = "JP3", countryId = CountryId, arenaFolder = Arena, number = 3, name = "Stacker Parking", type = MissionType.ParkingExam, road = Road, environment = env, props = props,
                briefing = "Turn into the stacker car park and drive nose-first onto the empty deck between the posts. Keep still: the machine lifts your car to the upper level.",
                spawn = spawn, spawnYaw = 270f, time = 150f, collisions = 2, three = (70f, 0), two = (100f, 1), coins = (260, 60),
                objectives = new MissionObjective[]
                {
                    new MechanicalParkObjective("Park on the stacker deck", new Vector3(StackerX[StackerTarget], 0f, StackerZ), 180f, new Vector2(2.7f, 5.0f), "stacker", rules),
                },
            });
        }
    }
}
