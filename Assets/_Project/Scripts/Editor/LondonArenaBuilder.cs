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
    /// United Kingdom — London arena, left-hand traffic, built from the imported London models (london mobile pack:
    /// terraced houses, hotel, bank, red phone box, black cab, parking signs; Routemaster double-decker; '67 saloon;
    /// industrial brick warehouses; UK regulatory signs; Belisha beacons and keep-left bollards; Victorian lamps).
    /// A narrow high street with kerbs and double yellow lines, a cobbled mews to the north and an underground car
    /// park to the south.
    /// </summary>
    public static class LondonArenaBuilder
    {
        public const string Arena = "London";
        public const string CountryId = "gb";
        public static string EnvPath => F.PrefabPath(Arena, "Env_London");
        public const RoadDirection Road = RoadDirection.LeftHand;

        // High street along X: carriageway z ±4.75 (a parking strip each side), kerbs, raised pavements to z ±9.
        public const float RoadHalf = 4.75f, KerbW = 0.3f, KerbH = 0.13f, PaveOuter = 9f;
        // GB2 mews (north) and GB3 car-park ramp (south): dropped kerbs there.
        public const float MewsX = -30f, MewsHalf = 1.8f, MewsEndZ = 43f, MewsDropHalf = 4f;
        public const float RampX = 30f, RampHalf = 3.2f, RampTopZ = -9f, RampBotZ = -45f, Depth = 4.2f;
        public static readonly Vector3 Hall = new Vector3(RampX, -Depth, -56.955f);   // GarageModule centre (yaw 90: aisle along Z)
        public static readonly float[] HallBayX = { -8.08f, -4.78f, -1.45f, 1.71f, 4.80f, 8.10f };   // module-local bay centres
        public const float HallRow = 5.84f;
        public const int HallTarget = 2;
        public static Vector3 HallBay(int i, int side) => Hall + new Vector3(side * HallRow, 0f, -HallBayX[i]);

        private static System.Func<Transform, IEnumerable<Transform>> Exact(params string[] names) => root =>
        {
            var hits = root.GetComponentsInChildren<Transform>(true).Where(t => names.Contains(t.name)).ToList();
            if (hits.Count == 0) throw new System.Exception("no node named " + string.Join("/", names));
            return hits.Where(t => !hits.Any(h => h != t && t.IsChildOf(h)));
        };

        public static void BuildProps()
        {
            const string pack = "London/ldn_mobile_pack.glb";
            const float S = 0.42f;   // the pack is modelled ~2.4x real size (its phone box is 6.7 units tall)
            var specs = new List<F.Spec>
            {
                // Vehicles
                new F.Spec { name = "Bus_Body", source = pack, pick = Exact("bus_30"), yaw = 270f, fit = F.Fit.Length, size = 8.4f, collider = F.ColliderKind.None },
                new F.Spec { name = "Saloon_67", source = "London/ldn_car_lasley.glb", yaw = 90f, fit = F.Fit.Length, size = 4.2f, colliderHeightCap = 1.4f, strip = new[] { "Interior" } },
                new F.Spec { name = "Cab_Body", source = pack, pick = Exact("taxi_33"), yaw = 270f, fit = F.Fit.Length, size = 4.58f, collider = F.ColliderKind.None },
                new F.Spec { name = "Cab_Wheel", source = pack, pick = Exact("wheel_35"), fit = F.Fit.Height, size = 0.66f, collider = F.ColliderKind.None },
                // Buildings (pack scale) + brick warehouses (real scale)
                new F.Spec { name = "Terrace_Houses", source = pack, pick = Exact("building_houses_13"), fit = F.Fit.Scale, size = S },
                new F.Spec { name = "Hotel", source = pack, pick = Exact("building_hotel_12"), fit = F.Fit.Scale, size = S },
                new F.Spec { name = "Bank", source = pack, pick = Exact("building_bank_11"), fit = F.Fit.Scale, size = S },
                new F.Spec { name = "Brick_Works", source = "London/ldn_industrial_buildings.glb", pick = Exact("Industrial_Building_1") },
                new F.Spec { name = "Brick_Mill", source = "London/ldn_industrial_buildings.glb", pick = Exact("Industrial_Building_10") },
                new F.Spec { name = "Brick_Shed", source = "London/ldn_industrial_buildings.glb", pick = Exact("Industrial_FactoryPlant_1") },
                new F.Spec { name = "Brick_Row", source = "London/ldn_industrial_buildings.glb", pick = Exact("Industrial_Warehouse_4") },
                // Street furniture
                new F.Spec { name = "Phone_Box", source = pack, pick = Exact("phonebox_3"), fit = F.Fit.Height, size = 2.55f },
                new F.Spec { name = "Lamp_Victorian", source = "London/ldn_street_lamp.glb", fit = F.Fit.Height, size = 4.2f },
                new F.Spec { name = "Bench", source = pack, pick = Exact("bench_1"), fit = F.Fit.Height, size = 0.9f },
                new F.Spec { name = "Planter", source = pack, pick = Exact("plant_4"), fit = F.Fit.Height, size = 1.6f },
                new F.Spec { name = "Fountain", source = pack, pick = Exact("fountain_2"), fit = F.Fit.Height, size = 2.8f },
                new F.Spec { name = "Billboard", source = pack, pick = Exact("ad_billboard_0"), fit = F.Fit.Height, size = 4.5f },
                new F.Spec { name = "Belisha_Beacons", source = "London/ldn_zebra_crossing.glb", pick = Exact("Cylinder.005_3"), fit = F.Fit.Height, size = 2.5f, collider = F.ColliderKind.None },
                new F.Spec { name = "KeepLeft_Bollard", source = "London/ldn_zebra_crossing.glb", pick = Exact("Cube.001_2"), fit = F.Fit.Height, size = 1.1f },
                // Signs: parking (pack, faces +Z) and UK regulatory (faces -X, hence yaw 90)
                new F.Spec { name = "Sign_PermitHolders", source = pack, pick = Exact("sign_permit_7"), fit = F.Fit.Height, size = 2.5f },
                new F.Spec { name = "Sign_Disabled", source = pack, pick = Exact("sign_disabled_6"), fit = F.Fit.Height, size = 2.5f },
                new F.Spec { name = "Sign_RampAhead", source = pack, pick = Exact("sign_ramp_8"), fit = F.Fit.Height, size = 1.4f },
                new F.Spec { name = "Sign_NoWaiting", source = "London/ldn_uk_road_signs.glb", pick = Exact("sign-giving-order-no-waiting_16"), yaw = 90f, fit = F.Fit.Height, size = 2.7f },
                new F.Spec { name = "Sign_NoEntry", source = "London/ldn_uk_road_signs.glb", pick = Exact("sign-giving-order-no-entry-vehicular-traffic_4"), yaw = 90f, fit = F.Fit.Height, size = 2.7f },
                new F.Spec { name = "Sign_Speed30", source = "London/ldn_uk_road_signs.glb", pick = Exact("sign-giving-order-maximum-speed_1"), yaw = 90f, fit = F.Fit.Height, size = 2.7f },
                new F.Spec { name = "Sign_KeepLeft", source = "London/ldn_uk_road_signs.glb", pick = Exact("sign-giving-order-keep-left_20"), yaw = 90f, fit = F.Fit.Height, size = 2.7f },
                new F.Spec { name = "Sign_HeightLimit", source = "London/ldn_uk_road_signs.glb", pick = Exact("sign-giving-order-no-vehicles-over-height_11"), yaw = 90f, fit = F.Fit.Height, size = 2.7f },
            };
            foreach (F.Spec s in specs) F.Make(Arena, s);
            BuildCab();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arena] London props: {specs.Count + 1}");
        }

        /// <summary>The pack's black cab and Routemaster come without wheels: body + the pack's wheel, one box collider.</summary>
        private static void BuildCab()
        {
            Wheeled("Black_Cab", "Cab_Body", 1f, 0.22f, 0.62f, 1.6f);
            Wheeled("Routemaster", "Bus_Body", 1.55f, 0.32f, 0.64f, 4.4f);
        }

        private static void Wheeled(string name, string bodyName, float wheelScale, float lift, float axleFrac, float colliderCap)
        {
            GameObject body = F.Load(Arena, bodyName), wheel = F.Load(Arena, "Cab_Wheel");
            if (body == null || wheel == null) { Debug.LogError("[Arena] London vehicle parts missing for " + name); return; }
            var root = new GameObject(name);
            var b = (GameObject)PrefabUtility.InstantiatePrefab(body, root.transform);
            Bounds bb = NY.RenderBounds(b);
            b.transform.localPosition = new Vector3(0f, lift, 0f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var w = (GameObject)PrefabUtility.InstantiatePrefab(wheel, root.transform);
                    w.transform.localScale = Vector3.one * wheelScale;
                    w.transform.localPosition = new Vector3(sx * (bb.extents.x - 0.12f * wheelScale), 0f, sz * bb.extents.z * axleFrac);
                    w.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);   // wheel face (+Z) to the side
                }
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(bb.size.x, Mathf.Min(colliderCap, bb.size.y + lift), bb.size.z);
            box.center = new Vector3(0f, box.size.y * 0.5f, 0f);
            PrefabUtility.SaveAsPrefabAsset(root, F.PrefabPath(Arena, name));
            Object.DestroyImmediate(root);
        }

        internal static GameObject P(string name)
        {
            GameObject g = F.Load(Arena, name);
            if (g == null) Debug.LogError("[Arena] London prefab missing: " + name);
            return g;
        }

        [MenuItem("Tools/Ultimate Car Parking/Build London Arena")]
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
            foreach (string id in new[] { "GB1", "GB2", "GB3" })
                WorldTourBuilder.RenderShots(AssetDatabase.LoadAssetAtPath<LevelData>($"{W.Dir}/Level_{id}.asset"), dir);
        }

        // ------------------------------------------------------------------ environment

        public static void BuildEnvironment()
        {
            var root = new GameObject("Env_London");
            Transform Group(string n) { var g = new GameObject(n).transform; g.SetParent(root.transform, false); return g; }
            GameObject Put(Transform parent, GameObject prefab, Vector3 pos, float yaw)
            {
                if (prefab == null) { Debug.LogError("[Arena] London missing prefab at " + pos); return null; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                return go;
            }
            Vector3 Size(string prefab) { GameObject p = P(prefab); var box = p != null ? p.GetComponent<BoxCollider>() : null; return box != null ? box.size : new Vector3(20f, 10f, 20f); }

            Material pavement = MumbaiArenaBuilder.Mat("M_LDN_Pavement", "concrete_pavement_diff_1k.jpg", "concrete_pavement_nor_gl_1k.jpg", 30f, new Color(0.7f, 0.7f, 0.72f));
            Material asphalt = MumbaiArenaBuilder.Mat("M_LDN_Asphalt", "Road007_1K-JPG_Color.jpg", "Road007_1K-JPG_NormalGL.jpg", 10f, new Color(0.62f, 0.62f, 0.64f));
            Material cobbles = MumbaiArenaBuilder.Mat("M_LDN_Cobbles", "large_sandstone_blocks_diff_1k.jpg", "large_sandstone_blocks_nor_gl_1k.jpg", 9f, new Color(0.62f, 0.6f, 0.58f));

            // --- ground: slabs around the car-park ramp opening (the hall lies under the south slab)
            Transform ground = Group("Ground");
            void Slab(string n, float x0, float x1, float z0, float z1)
            {
                var g = new GameObject(n); g.transform.SetParent(ground, false);
                var c = g.AddComponent<BoxCollider>(); c.center = new Vector3((x0 + x1) * 0.5f, -0.15f, (z0 + z1) * 0.5f); c.size = new Vector3(x1 - x0, 0.3f, z1 - z0);
                NY.Surface(ground, n + "_Surface", new Vector3((x0 + x1) * 0.5f, -0.02f, (z0 + z1) * 0.5f), new Vector2(x1 - x0, z1 - z0), pavement);
            }
            Slab("GroundNorth", -140f, 140f, RampTopZ, 120f);
            Slab("GroundSouth", -140f, 140f, -130f, RampBotZ);
            Slab("GroundWest", -140f, RampX - RampHalf, RampBotZ, RampTopZ);
            Slab("GroundEast", RampX + RampHalf, 140f, RampBotZ, RampTopZ);
            NY.Surface(ground, "HighStreet", new Vector3(0f, 0.004f, 0f), new Vector2(278f, RoadHalf * 2f), asphalt);
            NY.Surface(ground, "Mews", new Vector3(MewsX, 0.006f, (PaveOuter + MewsEndZ) * 0.5f), new Vector2(MewsHalf * 2f + 0.4f, MewsEndZ - PaveOuter + 2f), cobbles);
            NY.Boundary(ground, new Vector3(0f, 2f, 119f), new Vector3(280f, 4f, 1f));
            NY.Boundary(ground, new Vector3(0f, 2f, -129f), new Vector3(280f, 4f, 1f));
            NY.Boundary(ground, new Vector3(-139f, 2f, 0f), new Vector3(1f, 4f, 250f));
            NY.Boundary(ground, new Vector3(139f, 2f, 0f), new Vector3(1f, 4f, 250f));

            // --- kerbs (KerbMarker: wheels on them count as kerb strikes) and raised pavements, with dropped kerbs
            Transform kerbs = Group("KerbsAndPavements");
            void Kerbed(float x0, float x1, int side)
            {
                if (x1 - x0 < 0.5f) return;
                float cx = (x0 + x1) * 0.5f, len = x1 - x0;
                Transform k = RealGarageBuilder.Box(kerbs, "Kerb", new Vector3(cx, KerbH * 0.5f, side * (RoadHalf + KerbW * 0.5f)), new Vector3(len, KerbH, KerbW), "M_KerbStone", 1f);
                k.gameObject.AddComponent<KerbMarker>();
                RealGarageBuilder.Box(kerbs, "Pavement", new Vector3(cx, KerbH * 0.5f, side * (RoadHalf + KerbW + (PaveOuter - RoadHalf - KerbW) * 0.5f)),
                    new Vector3(len, KerbH, PaveOuter - RoadHalf - KerbW), "M_Pavement", 3f);
            }
            Kerbed(-139f, MewsX - MewsDropHalf, 1); Kerbed(MewsX + MewsDropHalf, 139f, 1);
            Kerbed(-139f, RampX - RampHalf - 0.6f, -1); Kerbed(RampX + RampHalf + 0.6f, 139f, -1);

            // --- markings: white centre dashes, double yellow lines (no waiting) except the parking bays, zebra crossing
            Transform marks = Group("Markings");
            for (float x = -136f; x < 136f; x += 6f)
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, marks, "CentreDash", new Vector3(x, 0.02f, 0f), Quaternion.identity, new Vector3(3f, 0.01f, 0.1f), "LineWhite", false);
            foreach (int side in new[] { -1, 1 })
                foreach (float off in new[] { 0.2f, 0.4f })
                    foreach (var (x0, x1) in new[] { (-136f, -66f), (-6f, 8f), (40f, 136f) })        // parking strips at x -66..-6 and 8..40 stay clear
                        ProjectBootstrapper.Primitive(PrimitiveType.Cube, marks, "DoubleYellow", new Vector3((x0 + x1) * 0.5f, 0.02f, side * (RoadHalf - off)), Quaternion.identity, new Vector3(x1 - x0, 0.01f, 0.08f), "LineYellow", false);
            for (int i = 0; i < 7; i++)
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, marks, "Zebra", new Vector3(-2f, 0.02f, -RoadHalf + 0.7f + i * 1.35f), Quaternion.identity, new Vector3(4f, 0.01f, 0.6f), "LineWhite", false);
            Put(marks, P("Belisha_Beacons"), new Vector3(-2f, KerbH, RoadHalf + 0.8f), 90f);
            Put(marks, P("Belisha_Beacons"), new Vector3(-2f, KerbH, -RoadHalf - 0.8f), 90f);

            // --- buildings: north row (fronts face the street: yaw 180), south row (yaw 0)
            Transform north = Group("NorthRow"), south = Group("SouthRow");
            void Row(Transform parent, string[] names, float x0, float x1, float frontZ, float yaw)
            {
                float x = x0; int i = 0;
                while (true)
                {
                    string n = names[i % names.Length];
                    Vector3 sz = Size(n);
                    if (x + sz.x > x1 + 0.3f) break;
                    float z = yaw == 180f ? frontZ + sz.z * 0.5f : frontZ - sz.z * 0.5f;
                    Put(parent, P(n), new Vector3(x + sz.x * 0.5f, 0f, z), yaw);
                    x += sz.x + 0.3f; i++;
                }
            }
            Row(north, new[] { "Terrace_Houses", "Bank", "Terrace_Houses", "Terrace_Houses" }, -136f, MewsX - MewsHalf - 0.2f, PaveOuter, 180f);
            Row(north, new[] { "Terrace_Houses", "Hotel", "Terrace_Houses" }, MewsX + MewsHalf + 0.2f, 136f, PaveOuter, 180f);
            Row(south, new[] { "Terrace_Houses", "Terrace_Houses", "Bank" }, -136f, RampX - RampHalf - 1f, -PaveOuter, 0f);
            Row(south, new[] { "Hotel", "Terrace_Houses" }, RampX + RampHalf + 1f, 136f, -PaveOuter, 0f);

            // --- the mews: brick warehouses along both sides (behind the front houses), an end wall
            Transform mews = Group("Mews");
            float houseD = Size("Terrace_Houses").z;
            float mz0 = PaveOuter + houseD + 0.5f;
            Vector3 works = Size("Brick_Works"), row = Size("Brick_Row");
            // Warehouses with their long side along the mews (yaw 90 / 270 turns their +Z front to face it).
            Put(mews, P("Brick_Works"), new Vector3(MewsX - MewsHalf - works.z * 0.5f, 0f, mz0 + works.x * 0.5f), 90f);
            Put(mews, P("Brick_Row"), new Vector3(MewsX + MewsHalf + row.z * 0.5f, 0f, mz0 + row.x * 0.5f), 270f);
            Put(mews, P("Brick_Mill"), new Vector3(MewsX, 0f, MewsEndZ + Size("Brick_Mill").z * 0.5f + 0.4f), 180f);
            // Invisible mews walls between the front houses and the warehouses (no gaps to escape through).
            NY.Boundary(mews, new Vector3(MewsX - MewsHalf - 0.15f, 1.5f, (PaveOuter + MewsEndZ) * 0.5f + 2f), new Vector3(0.3f, 3f, MewsEndZ - PaveOuter + 4f));
            NY.Boundary(mews, new Vector3(MewsX + MewsHalf + 0.15f, 1.5f, (PaveOuter + MewsEndZ) * 0.5f + 2f), new Vector3(0.3f, 3f, MewsEndZ - PaveOuter + 4f));
            NY.Boundary(mews, new Vector3(MewsX, 1.5f, MewsEndZ + 0.2f), new Vector3(MewsHalf * 2f + 0.6f, 3f, 0.3f));
            Put(mews, P("Sign_NoWaiting"), new Vector3(MewsX + MewsHalf + 1.2f, KerbH, PaveOuter - 0.8f), 180f);
            Put(mews, P("Lamp_Victorian"), new Vector3(MewsX - MewsHalf - 0.4f, 0f, 26f), 90f);

            // --- underground car park: ramp, walls, landing, hall, ceiling, lights, signs
            BuildCarPark(Group("CarPark"), Put);

            // --- street furniture
            Transform street = Group("Street");
            for (float x = -128f; x <= 128f; x += 24f)
            {
                if (Mathf.Abs(x - MewsX) > 6f) Put(street, P("Lamp_Victorian"), new Vector3(x, KerbH, RoadHalf + 0.9f), 180f);
                if (Mathf.Abs(x + 8f - RampX) > 6f) Put(street, P("Lamp_Victorian"), new Vector3(x + 8f, KerbH, -RoadHalf - 0.9f), 0f);
            }
            Put(street, P("Phone_Box"), new Vector3(-48f, KerbH, RoadHalf + 2.6f), 180f);
            Put(street, P("Phone_Box"), new Vector3(-46.5f, KerbH, RoadHalf + 2.6f), 180f);
            Put(street, P("Phone_Box"), new Vector3(52f, KerbH, -RoadHalf - 2.6f), 0f);
            Put(street, P("Bench"), new Vector3(-14f, KerbH, -RoadHalf - 2.8f), 90f);
            Put(street, P("Planter"), new Vector3(10f, KerbH, RoadHalf + 3f), 0f);
            Put(street, P("Planter"), new Vector3(-80f, KerbH, -RoadHalf - 3f), 0f);
            Put(street, P("Sign_Speed30"), new Vector3(-120f, KerbH, RoadHalf + 0.8f), 270f);
            Put(street, P("Sign_KeepLeft"), new Vector3(118f, KerbH, -RoadHalf - 0.8f), 90f);
            Put(street, P("KeepLeft_Bollard"), new Vector3(-2f, 0f, 0f), 90f);
            Put(street, P("Billboard"), new Vector3(70f, KerbH, RoadHalf + 3.5f), 180f);

            // London light: soft overcast, cool haze.
            var light = root.AddComponent<SceneLightingOverride>();
            var lso = new SerializedObject(light);
            lso.FindProperty("ambientSky").colorValue = new Color(0.66f, 0.68f, 0.72f);
            lso.FindProperty("ambientEquator").colorValue = new Color(0.54f, 0.55f, 0.57f);
            lso.FindProperty("ambientGround").colorValue = new Color(0.32f, 0.32f, 0.33f);
            lso.FindProperty("fog").boolValue = true;
            lso.FindProperty("fogColor").colorValue = new Color(0.72f, 0.74f, 0.78f);
            lso.FindProperty("fogDensity").floatValue = 0.0045f;
            lso.FindProperty("sunColor").colorValue = new Color(0.95f, 0.95f, 0.98f);
            lso.FindProperty("sunIntensity").floatValue = 1.0f;
            lso.FindProperty("sunEuler").vector3Value = new Vector3(50f, 30f, 0f);
            lso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, EnvPath);
            Object.DestroyImmediate(root);
            Debug.Log("[Arena] Env_London written");
        }

        private static void BuildCarPark(Transform g, System.Func<Transform, GameObject, Vector3, float, GameObject> put)
        {
            float run = RampTopZ - RampBotZ, len = Mathf.Sqrt(run * run + Depth * Depth), ang = Mathf.Atan2(Depth, run) * Mathf.Rad2Deg;
            // Ramp: down from z -9 (street) to z -45 (-4.2 m), ~6.7°.
            Transform ramp = RealGarageBuilder.Box(g, "Ramp", new Vector3(RampX, -Depth * 0.5f - 0.15f, (RampTopZ + RampBotZ) * 0.5f), new Vector3(RampHalf * 2f, 0.3f, len), "M_DeckConcrete", 4f);
            ramp.localRotation = Quaternion.Euler(-ang, 0f, 0f);
            float hallN = Hall.z + 9.755f, hallS = Hall.z - 9.755f, hallW = Hall.x - 14.64f, hallE = Hall.x + 14.64f;
            RealGarageBuilder.Box(g, "Landing", new Vector3(RampX, -Depth - 0.15f, (RampBotZ + hallN) * 0.5f), new Vector3(RampHalf * 2f, 0.3f, RampBotZ - hallN + 0.1f), "M_DeckConcrete", 4f);
            // Ramp side walls (the trench), concrete with colliders.
            foreach (float sx in new[] { -1f, 1f })
                RealGarageBuilder.Box(g, "RampWall", new Vector3(RampX + sx * (RampHalf + 0.15f), -Depth * 0.5f, (RampTopZ + hallN) * 0.5f), new Vector3(0.3f, Depth, RampTopZ - hallN), "M_ConcreteWall", 2f);
            // Hall: the imported garage section turned so its aisle runs along Z from the ramp.
            put(g, RealPropFactory.Load("GarageModule"), Hall, 90f);
            RealGarageBuilder.Box(g, "Ceiling", new Vector3(Hall.x, -0.45f, Hall.z), new Vector3(29.3f, 0.3f, 19.6f), "M_DeckConcrete", 4f, collider: false);
            float wallY = -Depth * 0.5f - 0.15f, wallH = Depth - 0.3f;
            RealGarageBuilder.Box(g, "WallS", new Vector3(Hall.x, wallY, hallS - 0.15f), new Vector3(hallE - hallW + 0.6f, wallH, 0.3f), "M_ConcreteWall", 2f);
            RealGarageBuilder.Box(g, "WallW", new Vector3(hallW - 0.15f, wallY, Hall.z), new Vector3(0.3f, wallH, hallN - hallS), "M_ConcreteWall", 2f);
            RealGarageBuilder.Box(g, "WallE", new Vector3(hallE + 0.15f, wallY, Hall.z), new Vector3(0.3f, wallH, hallN - hallS), "M_ConcreteWall", 2f);
            RealGarageBuilder.Box(g, "WallN_W", new Vector3((hallW + RampX - RampHalf) * 0.5f, wallY, hallN + 0.15f), new Vector3(RampX - RampHalf - hallW, wallH, 0.3f), "M_ConcreteWall", 2f);
            RealGarageBuilder.Box(g, "WallN_E", new Vector3((hallE + RampX + RampHalf) * 0.5f, wallY, hallN + 0.15f), new Vector3(hallE - RampX - RampHalf, wallH, 0.3f), "M_ConcreteWall", 2f);
            GameObject tube = RealPropFactory.Load("FluorescentLight");
            for (float z = hallS + 4f; z < hallN; z += 8f)            // fittings are ~13k triangles each: a sparse grid
                foreach (float x in new[] { Hall.x - 5.8f, Hall.x + 5.8f })
                    if (tube != null) put(g, tube, new Vector3(x, -0.62f, z), 0f);
            put(g, P("Sign_RampAhead"), new Vector3(RampX + RampHalf + 1.3f, KerbH, -RoadHalf - 1.5f), 0f);
            put(g, P("Sign_HeightLimit"), new Vector3(RampX - RampHalf - 1.2f, KerbH, -RoadHalf - 1.2f), 90f);
            put(g, P("Sign_PermitHolders"), HallBay(HallTarget, 1) + new Vector3(4.2f, 0f, 1.6f), 270f);
            put(g, P("Sign_Disabled"), HallBay(0, 1) + new Vector3(4.2f, 0f, 1.6f), 270f);
        }

        // ------------------------------------------------------------------ missions

        public static void BuildLevels()
        {
            var env = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPath);
            LevelData m1 = Mission1(env), m2 = Mission2(env), m3 = Mission3(env);
            W.WriteCatalog(CountryId, Arena, m1, m2, m3);
            Debug.Log("[Arena] London missions written");
        }

        private static GameObject Parked(string body) => RealPropFactory.Load("Parked_" + body);
        private static float StripZ(int side) => side * (RoadHalf - 1.05f);   // parking strip centre (car half-width ~0.95 + 0.1 to the kerb)

        /// <summary>GB1 — High Street Parallel: parallel park against the left kerb between a black cab and a '67 saloon.</summary>
        private static LevelData Mission1(GameObject env)
        {
            var props = new List<PropPlacement>();
            float north = StripZ(1), south = StripZ(-1);
            float yawN = TrafficSide.ParkedYaw(90f, +1, Road), yawS = TrafficSide.ParkedYaw(90f, -1, Road);
            W.Place(props, P("Black_Cab"), new Vector3(15.4f, 0f, north), yawN);
            W.Place(props, P("Saloon_67"), new Vector3(26.9f, 0f, north), yawN);
            W.Place(props, P("Black_Cab"), new Vector3(32.8f, 0f, north), yawN);
            W.Place(props, Parked("hatchback"), new Vector3(10.2f, 0f, north), yawN);
            W.Place(props, P("Routemaster"), new Vector3(-40f, 0f, south), yawS);                  // bus stop
            W.Place(props, P("Saloon_67"), new Vector3(-20f, 0f, south), yawS);
            W.Place(props, P("Black_Cab"), new Vector3(20f, 0f, south), yawS);
            Vector3 spawn = TrafficSide.LaneCentre(new Vector3(-60f, 0f, 0f), Vector3.right, 3.0f, Road);
            var rules = new ParkingRules { edgeMargin = 0.08f, angleTolerance = 8f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            return W.Write(new W.Mission
            {
                id = "GB1", countryId = CountryId, arenaFolder = Arena, number = 1, name = "High Street Parallel", type = MissionType.ParallelParking, road = Road, environment = env, props = props,
                briefing = "Keep left down the high street, past the zebra crossing, and parallel park against the left kerb between the black cab and the old saloon. Finish within 0.45 m of the kerb; three kerb strikes fail.",
                spawn = spawn, spawnYaw = 90f, time = 150f, collisions = 2, three = (70f, 0), two = (105f, 1), coins = (220, 50),
                objectives = new MissionObjective[]
                {
                    new ParallelParkObjective("Parallel park at the left kerb", new Vector3(21.25f, 0f, north), 90f, new Vector2(2.4f, 6.6f),
                        TrafficSide.KerbSide(Road), 0.45f, 2, rules),
                },
            });
        }

        /// <summary>GB2 — Mews Reverse: reverse down the narrow cobbled mews and into the space at the end.</summary>
        private static LevelData Mission2(GameObject env)
        {
            var props = new List<PropPlacement>();
            W.Place(props, P("Black_Cab"), new Vector3(-44f, 0f, StripZ(1)), TrafficSide.ParkedYaw(90f, +1, Road));
            W.Place(props, P("Saloon_67"), new Vector3(-12f, 0f, StripZ(1)), TrafficSide.ParkedYaw(90f, +1, Road));
            W.Place(props, P("Routemaster"), new Vector3(-30f, 0f, StripZ(-1)), TrafficSide.ParkedYaw(90f, -1, Road));   // tight swing opposite the mews
            Vector3 spawn = TrafficSide.LaneCentre(new Vector3(-62f, 0f, 0f), Vector3.right, 3.0f, Road);
            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 8f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            return W.Write(new W.Mission
            {
                id = "GB2", countryId = CountryId, arenaFolder = Arena, number = 2, name = "Mews Reverse", type = MissionType.NarrowRoute, road = Road, environment = env, props = props,
                scoreMode = ScoreMode.Quality, quality = (0.8f, 0.5f),
                briefing = "Drive past the cobbled mews, then reverse all the way down it — barely wider than the car — and into the space at the end. Two wall scrapes allowed.",
                spawn = spawn, spawnYaw = 90f, time = 180f, collisions = 3, three = (90f, 0), two = (130f, 1), coins = (240, 55),
                objectives = new MissionObjective[]
                {
                    new NarrowAlleyObjective("Reverse down the mews", new Vector3(MewsX, 0f, PaveOuter + 1f), new Vector3(MewsX, 0f, MewsEndZ - 2f), MewsHalf * 2f, maxScrapes: 2, reverseThrough: true, goodClearance: 0.35f),
                    new ReverseParkObjective("Into the space at the end", new Vector3(MewsX, 0f, MewsEndZ - 3.1f), 0f, new Vector2(MewsHalf * 2f - 0.1f, 6f), maxRearGap: 0.8f, rules),
                },
            });
        }

        /// <summary>GB3 — Underground Car Park: down the ramp, reverse into the permit holders' bay.</summary>
        private static LevelData Mission3(GameObject env)
        {
            var props = new List<PropPlacement>();
            string[] fleet = { "sedan", "hatchback", "compact", "wagon", "suv", "coupe" };
            int k = 0;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < HallBayX.Length; i++)
                {
                    if (side == 1 && (i == HallTarget || i == 0)) continue;     // the permit bay and the disabled bay stay free
                    if (side == -1 && i == 4) continue;
                    GameObject car = k % 4 == 1 ? P("Black_Cab") : k % 4 == 3 ? P("Saloon_67") : Parked(fleet[k % fleet.Length]);
                    W.Place(props, car, HallBay(i, side), side == 1 ? 90f : 270f);
                    k++;
                }
            W.Place(props, P("Routemaster"), new Vector3(10f, 0f, StripZ(-1)), TrafficSide.ParkedYaw(90f, -1, Road));
            Vector3 spawn = TrafficSide.LaneCentre(new Vector3(70f, 0f, 0f), Vector3.left, 3.0f, Road);
            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 8f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            return W.Write(new W.Mission
            {
                id = "GB3", countryId = CountryId, arenaFolder = Arena, number = 3, name = "Underground Car Park", type = MissionType.MultiStoreyRamp, road = Road, environment = env, props = props,
                cameraHeight = 2.2f, cameraDistance = 5.5f,
                briefing = "Turn left into the car park, take the ramp down to level -1, and reverse into the free PERMIT HOLDERS bay. Mind the columns.",
                spawn = spawn, spawnYaw = 270f, time = 180f, collisions = 2, three = (85f, 0), two = (125f, 1), coins = (250, 60),
                objectives = new MissionObjective[]
                {
                    new CheckpointObjective("Down the ramp", new Vector3(RampX, -Depth * 0.5f, (RampTopZ + RampBotZ) * 0.5f), 180f, new Vector2(RampHalf * 2f - 0.6f, 4f), "DOWN THE RAMP TO LEVEL -1"),
                    new FloorParkObjective("Reverse into the permit bay", HallBay(HallTarget, 1), 90f, new Vector2(2.9f, 5.5f), ParkDirection.ReverseIn, "LEVEL -1", rules),
                },
            });
        }
    }
}
