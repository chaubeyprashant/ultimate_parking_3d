using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Cars;
using UltimateCarParking.Environment;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.Parking;
using F = UltimateCarParking.EditorTools.ArenaPropFactory;
using W = UltimateCarParking.EditorTools.ArenaLevelWriter;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// New York layout (metres). An avenue runs east-west along X (z ±7, two lanes each way, right-hand traffic).
    /// North side: brownstone rows, and a two-level parking garage (US3). South side: the supermarket and its lot
    /// behind a ticket barrier (US1), and a loading yard reached by a narrow alley (US2). Downtown towers and the
    /// skyline landmarks stand behind.
    /// </summary>
    public static partial class NewYorkArenaBuilder
    {
        public const float AvenueHalf = 7f, SidewalkOuter = 11f;

        // US1 supermarket (model placed with its pave strip as the south sidewalk).
        public static readonly Vector3 MarketPos = new Vector3(55f, -1.22f, -43.15f);
        public const float LotRowA = -19.2f, LotRowB = -32.75f, LotBayW = 2.7f, LotBayD = 5.5f, LotBayX0 = 25f;
        public const int LotBays = 22;
        public const float GateX = 14.5f, GateZ = -12f;

        // US2 loading yard
        public const float AlleyX = -41.5f, AlleyHalf = 3.5f, YardMinX = -64f, YardMaxX = -19f, YardMinZ = -58f, YardMaxZ = -33f;
        public const float DockZ = -57.4f, LoadBayD = 6.4f, LoadBayW = 3.1f;
        public static readonly float[] LoadBayX = { -58f, -54.9f, -51.8f, -32.3f, -29.2f, -26.1f, -23f };
        public const int LoadTarget = 5;

        // US3 garage: GarageModule halls at G (level 1) and G + 4.2 m (level 2), ramp south of the halls.
        public static readonly Vector3 G = new Vector3(60f, 0f, 48f);
        public const float Level = 4.2f, HallHX = 9.755f, HallHZ = 14.637f, Aisle = 2.79f, BayBack = 8.89f;
        public const float RampFootX = 80f, RampTopX = 40f, RampZ = 29.4f, RampHalf = 3.6f;
        public static readonly float[] GarageBayX = { -8.08f, -4.78f, -1.45f, 1.71f, 4.80f, 8.10f };
        public const int GarageTarget = 3;
        public static Vector3 GarageTargetPos => G + new Vector3(GarageBayX[GarageTarget], Level, (Aisle + BayBack) * 0.5f);

        [MenuItem("Tools/Ultimate Car Parking/Build New York Arena")]
        public static void Build()
        {
            BuildProps();
            BuildGatePrefab();
            BuildEnvironment();
            BuildLevels();
            AssetDatabase.SaveAssets();
        }

        public static void RenderShots()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            string dir = System.IO.Path.GetFullPath("../UltimateCarParking3D_logs/arenas");
            foreach (string id in new[] { "US1", "US2", "US3" })
                WorldTourBuilder.RenderShots(AssetDatabase.LoadAssetAtPath<LevelData>($"{W.Dir}/Level_{id}.asset"), dir);
            Debug.Log("[Arena] New York shots written to " + dir);
        }

        // ------------------------------------------------------------------ gate

        public static string GatePath => F.PrefabPath(Arena, "Gate_Sliding");

        /// <summary>Lot ticket gate: two imported metal barrier panels on a MovingBarrier that slides them aside.</summary>
        public static void BuildGatePrefab()
        {
            var root = new GameObject("Gate_Sliding");
            var moving = new GameObject("Moving").transform;
            moving.SetParent(root.transform, false);
            GameObject panel = RealPropFactory.Load("Barrier_Metal");
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(panel);
            Bounds b = RenderBounds(probe);
            Object.DestroyImmediate(probe);
            bool alongX = b.size.x >= b.size.z;
            float span = Mathf.Max(b.size.x, b.size.z);
            int n = Mathf.CeilToInt(7.2f / span);
            for (int i = 0; i < n; i++)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(panel, moving);
                p.transform.localRotation = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f);   // span along X (across the lane)
                p.transform.localPosition = new Vector3(-3.6f + span * (i + 0.5f), 0f, 0f);
            }
            var gate = root.AddComponent<MovingBarrier>();
            gate.Configure("us1_gate", moving, MovingBarrier.Motion.Slide, new Vector3(n * span + 0.5f, 0f, 0f), 3f, 5f, 1.5f);
            PrefabUtility.SaveAsPrefabAsset(root, GatePath);
            Object.DestroyImmediate(root);
        }

        internal static Bounds RenderBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ------------------------------------------------------------------ environment

        public static void BuildEnvironment()
        {
            var root = new GameObject("Env_NewYork");
            Transform Group(string n) { var g = new GameObject(n).transform; g.SetParent(root.transform, false); return g; }
            GameObject Put(Transform parent, GameObject prefab, Vector3 pos, float yaw)
            {
                if (prefab == null) { Debug.LogError("[Arena] New York missing prefab at " + pos); return null; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                return go;
            }
            float Depth(string prefab) { GameObject p = P(prefab); var box = p != null ? p.GetComponent<BoxCollider>() : null; return box != null ? box.size.z : 20f; }
            float Width(string prefab) { GameObject p = P(prefab); var box = p != null ? p.GetComponent<BoxCollider>() : null; return box != null ? box.size.x : 20f; }

            // --- ground: drivable slab, avenue asphalt, sidewalks/plazas
            Transform ground = Group("Ground");
            var slab = new GameObject("DriveSurface"); slab.transform.SetParent(ground, false);
            var col = slab.AddComponent<BoxCollider>(); col.size = new Vector3(270f, 1f, 240f); col.center = new Vector3(0f, -0.5f, 0f);
            Material pavement = MumbaiArenaBuilder.Mat("M_NYC_Pavement", "concrete_pavement_diff_1k.jpg", "concrete_pavement_nor_gl_1k.jpg", 45f, new Color(0.74f, 0.76f, 0.8f));
            Material asphalt = MumbaiArenaBuilder.Mat("M_NYC_Asphalt", "Road007_1K-JPG_Color.jpg", "Road007_1K-JPG_NormalGL.jpg", 12f, new Color(0.72f, 0.72f, 0.74f));
            Surface(ground, "Pavement", new Vector3(0f, -0.02f, 0f), new Vector2(270f, 240f), pavement);
            Surface(ground, "Avenue", new Vector3(0f, 0.005f, 0f), new Vector2(270f, AvenueHalf * 2f), asphalt);
            Surface(ground, "AlleyAndYard", new Vector3((YardMinX + YardMaxX) * 0.5f, 0.004f, (YardMinZ + YardMaxZ) * 0.5f), new Vector2(YardMaxX - YardMinX, YardMaxZ - YardMinZ), asphalt);
            Surface(ground, "Alley", new Vector3(AlleyX, 0.004f, (-AvenueHalf + YardMaxZ) * 0.5f), new Vector2(AlleyHalf * 2f, -AvenueHalf - YardMaxZ), asphalt);
            Surface(ground, "Forecourt", new Vector3(38f, 0.004f, 31.5f), new Vector2(24f, 41f), asphalt);
            Surface(ground, "ExitApron", new Vector3(82f, 0.004f, 38.3f), new Vector2(24.5f, 25f), asphalt);
            // Lane markings: double yellow centre, white lane dashes.
            Transform marks = Group("Markings");
            foreach (float z in new[] { -0.12f, 0.12f })
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, marks, "CentreLine", new Vector3(0f, 0.02f, z), Quaternion.identity, new Vector3(268f, 0.01f, 0.1f), "LineYellow", false);
            for (float x = -130f; x < 130f; x += 9f)
                foreach (float z in new[] { -3.5f, 3.5f })
                    ProjectBootstrapper.Primitive(PrimitiveType.Cube, marks, "LaneDash", new Vector3(x, 0.02f, z), Quaternion.identity, new Vector3(3f, 0.01f, 0.12f), "LineWhite", false);
            Boundary(ground, new Vector3(0f, 2f, 119f), new Vector3(270f, 4f, 1f));
            Boundary(ground, new Vector3(0f, 2f, -119f), new Vector3(270f, 4f, 1f));
            Boundary(ground, new Vector3(-134f, 2f, 0f), new Vector3(1f, 4f, 240f));
            Boundary(ground, new Vector3(134f, 2f, 0f), new Vector3(1f, 4f, 240f));

            // --- north side: brownstone rows facing the avenue (front = +Z, so yaw 180), leaving the garage block open
            Transform north = Group("NorthRow");
            string[] northRow = { "Brownstone_FlatFacade_2", "Brownstone_Classic_1", "Brownstone_FlatFacade_7", "Brownstone_Commercial_1_C", "Brownstone_FlatFacade_4",
                                   "Brownstone_FlatFacade_9", "Brownstone_FlatFacade_12", "Brownstone_FlatFacade_5", "Brownstone_FlatFacade_8" };
            Row(north, northRow, -132f, 24.5f, SidewalkOuter, 180f, Put, Width, Depth);
            Row(north, new[] { "Brownstone_FlatFacade_12_C", "Brownstone_FlatFacade_3" }, 97f, 132f, SidewalkOuter, 180f, Put, Width, Depth);
            // Behind the garage: taller downtown blocks.
            Put(north, P("Downtown_ClassicOffice_1"), new Vector3(60f, 0f, 63.5f + Depth("Downtown_ClassicOffice_1") * 0.5f + 1f), 180f);
            Put(north, P("Downtown_ArtDeco_1"), new Vector3(105f, 0f, 84f), 180f);
            Put(north, P("Downtown_ModernOffice_4"), new Vector3(15f, 0f, 75f), 180f);
            Put(north, P("Downtown_ModernOffice_1_B"), new Vector3(-30f, 0f, 70f), 180f);
            Put(north, P("Downtown_BrownstoneOffice_1"), new Vector3(-85f, 0f, 68f), 180f);
            Put(north, P("Downtown_ModernOffice_5"), new Vector3(-118f, 0f, 85f), 180f);
            Put(north, P("Downtown_Mall_1"), new Vector3(112f, 0f, 48f), 270f);                 // east of the garage exit apron

            // --- south side: buildings facing the avenue (yaw 0) west of the alley and between the alley and the lot
            Transform south = Group("SouthRow");
            // (≤ 21.7 m deep so their backs stop short of the loading yard at z -33)
            Row(south, new[] { "Brownstone_FlatFacadeComplex_1", "Brownstone_FlatFacade_9", "Brownstone_FlatFacade_5" }, -132f, AlleyX - AlleyHalf, -SidewalkOuter, 0f, Put, Width, Depth);
            Row(south, new[] { "Brownstone_FlatFacade_3", "Brownstone_FlatFacade_8" }, AlleyX + AlleyHalf, -2f, -SidewalkOuter, 0f, Put, Width, Depth);
            // Loading yard walls: buildings facing into the yard.
            Row(south, new[] { "Brownstone_ClassicComplex_2", "Brownstone_FlatFacade_7" }, YardMinX - 4f, YardMaxX + 6f, YardMinZ - 1.2f, 0f, Put, Width, Depth, frontAt: true);
            Put(south, P("Brownstone_FlatFacade_2"), new Vector3(YardMinX - Depth("Brownstone_FlatFacade_2") * 0.5f, 0f, -46.6f), 90f);
            Put(south, P("Brownstone_FlatFacade_2"), new Vector3(YardMaxX + Depth("Brownstone_FlatFacade_2") * 0.5f, 0f, -46.6f), 270f);
            Put(south, P("Downtown_Residential_2"), new Vector3(-100f, 0f, -70f), 0f);
            Put(south, P("Downtown_PublicBuilding_1"), new Vector3(118f, 0f, -60f), 270f);

            // --- US1 supermarket + painted bays + ticket gate + jersey barrier line along the pave
            Transform market = Group("Supermarket");
            GameObject store = Put(market, P("Supermarket"), MarketPos, 0f);
            if (store != null)
            {
                // Collider for the store building (the model has none): its walls / floor parts.
                Bounds sb = new Bounds();
                bool any = false;
                foreach (Renderer r in store.GetComponentsInChildren<Renderer>())
                    if (r.name.StartsWith("InnovateHubBuilding_WhiteBright") || r.name.StartsWith("InnovateHubBuilding_Floor") || r.name.StartsWith("InnovateHubBuilding_Wall"))
                    { if (!any) { sb = r.bounds; any = true; } else sb.Encapsulate(r.bounds); }
                var bc = new GameObject("StoreCollider").AddComponent<BoxCollider>();
                bc.transform.SetParent(market, false);
                bc.center = new Vector3(sb.center.x, Mathf.Max(sb.size.y * 0.5f, sb.center.y), sb.center.z);
                bc.size = sb.size;
            }
            for (int row = 0; row < 2; row++)
            {
                float z = row == 0 ? LotRowA : LotRowB;
                for (int i = 0; i <= LotBays; i++)
                    ProjectBootstrapper.Primitive(PrimitiveType.Cube, market, "BayLine", new Vector3(LotBayX0 + i * LotBayW, 0.03f, z), Quaternion.identity, new Vector3(0.12f, 0.02f, LotBayD), "LineWhite", false);
            }
            Put(market, AssetDatabase.LoadAssetAtPath<GameObject>(GatePath), new Vector3(GateX, 0f, GateZ), 0f);
            GameObject jersey = RealPropFactory.Load("Barrier_Concrete");
            float jl = JerseyLength(jersey);
            // Jersey barriers (long axis = local X) along the pave; east of the gate they stand 1.1 m south so the gate can slide behind them.
            for (float x = MarketPos.x - 51.4f + jl * 0.5f; x < GateX - 3.7f; x += jl) Put(market, jersey, new Vector3(x, 0f, GateZ), 0f);
            for (float x = GateX + 3.7f + jl * 0.5f; x < MarketPos.x + 51.4f; x += jl) Put(market, jersey, new Vector3(x, 0f, GateZ - 1.1f), 0f);
            Put(market, P("Sign_Arrow"), new Vector3(GateX - 4.4f, 0f, -8.2f), 180f);
            Put(market, P("Sign_PhotoEnforced"), new Vector3(GateX + 4.6f, 0f, -8.2f), 180f);
            // Invisible side walls so the lot is only entered through the gate (planters and the store close the rest).
            Boundary(market, new Vector3(MarketPos.x - 51.9f, 1.5f, -27f), new Vector3(0.5f, 3f, 31f));
            Boundary(market, new Vector3(MarketPos.x + 51.9f, 1.5f, -27f), new Vector3(0.5f, 3f, 31f));

            // --- US2 loading yard: dock barrier line, alley sign
            Transform yard = Group("LoadingYard");
            for (float x = YardMinX + jl * 0.5f; x < YardMaxX; x += jl) Put(yard, jersey, new Vector3(x, 0f, DockZ), 0f);
            Boundary(yard, new Vector3(YardMinX - 0.2f, 1.5f, (YardMinZ + YardMaxZ) * 0.5f), new Vector3(0.3f, 3f, YardMaxZ - YardMinZ));
            Boundary(yard, new Vector3(YardMaxX + 0.2f, 1.5f, (YardMinZ + YardMaxZ) * 0.5f), new Vector3(0.3f, 3f, YardMaxZ - YardMinZ));
            for (int i = 0; i < LoadBayX.Length; i++)
            {
                float x0 = LoadBayX[i] - LoadBayW * 0.5f, x1 = LoadBayX[i] + LoadBayW * 0.5f;
                foreach (float x in new[] { x0, x1 })
                    ProjectBootstrapper.Primitive(PrimitiveType.Cube, yard, "BayLine", new Vector3(x, 0.03f, DockZ + 1.0f + LoadBayD * 0.5f), Quaternion.identity, new Vector3(0.12f, 0.02f, LoadBayD), "LineYellow", false);
            }
            Put(yard, P("Sign_TruckCrossing"), new Vector3(AlleyX + AlleyHalf + 0.6f, 0f, -12.5f), 0f);
            Put(yard, P("Lamp_Pole"), new Vector3(-40f, 0f, YardMinZ + 1.2f), 0f);
            Put(yard, P("Lamp_Pole"), new Vector3(-25f, 0f, YardMaxZ - 0.6f), 180f);

            // --- US3 garage
            BuildGarage(Group("Garage"), Put, jersey, jl);

            // --- street furniture along both sidewalks
            Transform street = Group("StreetFurniture");
            for (float x = -126f; x <= 126f; x += 18f)
            {
                bool northFree = x < 24f || x > 97f;
                if (northFree) Put(street, P("Lamp_Pole"), new Vector3(x, 0f, AvenueHalf + 0.6f), 180f);
                if (x < AlleyX - 5f || (x > AlleyX + 5f && x < -3f)) Put(street, P("Lamp_Pole"), new Vector3(x + 9f, 0f, -AvenueHalf - 0.6f), 0f);
            }
            foreach (float x in new[] { -64f, -10f }) Put(street, P("Parking_Meter"), new Vector3(x, 0f, AvenueHalf + 0.5f), 180f);
            foreach (float x in new[] { -26f, -14f }) Put(street, P("Parking_Meter"), new Vector3(x, 0f, -AvenueHalf - 0.5f), 0f);
            foreach (float x in new[] { -104f, -36f, 112f }) Put(street, P("Hydrant"), new Vector3(x, 0f, AvenueHalf + 0.7f), 180f);
            foreach (float x in new[] { -98f, -20f }) Put(street, P("Hydrant"), new Vector3(x, 0f, -AvenueHalf - 0.7f), 0f);
            Put(street, P("Newspaper_Box"), new Vector3(-58f, 0f, AvenueHalf + 1.6f), 180f);
            Put(street, P("Newspaper_Box"), new Vector3(-57f, 0f, AvenueHalf + 1.6f), 180f);
            Put(street, P("Hotdog_Cart"), new Vector3(-6f, 0f, AvenueHalf + 2.2f), 180f);
            Put(street, P("Subway_Entrance"), new Vector3(-75f, 0f, AvenueHalf + 3.9f), 180f);
            Put(street, P("Traffic_Light"), new Vector3(23f, 0f, AvenueHalf + 0.8f), 180f);
            Put(street, P("Sign_SignalAhead"), new Vector3(-20f, 0f, AvenueHalf + 1.1f), 270f);
            Put(street, P("Sign_Speed35"), new Vector3(-110f, 0f, -AvenueHalf - 1.1f), 90f);

            // --- skyline: Manhattan landmarks far to the north and east (backdrop only)
            Transform sky = Group("Skyline");
            Put(sky, P("Landmark_EmpireState"), new Vector3(-60f, 0f, 520f), 180f);
            Put(sky, P("Landmark_Chrysler"), new Vector3(120f, 0f, 470f), 180f);
            Put(sky, P("Landmark_OneWTC"), new Vector3(-420f, 0f, -260f), 90f);
            Put(sky, P("Landmark_Woolworth"), new Vector3(-330f, 0f, -330f), 90f);
            Put(sky, P("Landmark_NYTimes"), new Vector3(260f, 0f, 420f), 180f);
            Put(sky, P("Landmark_MetLife"), new Vector3(40f, 0f, 560f), 180f);
            Put(sky, P("Downtown_Flatiron_1"), new Vector3(-200f, 0f, 260f), 150f);

            // Crisp late-afternoon light, light haze.
            var light = root.AddComponent<SceneLightingOverride>();
            var lso = new SerializedObject(light);
            lso.FindProperty("ambientSky").colorValue = new Color(0.62f, 0.66f, 0.72f);
            lso.FindProperty("ambientEquator").colorValue = new Color(0.5f, 0.5f, 0.52f);
            lso.FindProperty("ambientGround").colorValue = new Color(0.26f, 0.26f, 0.27f);
            lso.FindProperty("fog").boolValue = true;
            lso.FindProperty("fogColor").colorValue = new Color(0.72f, 0.76f, 0.82f);
            lso.FindProperty("fogDensity").floatValue = 0.0022f;
            lso.FindProperty("sunColor").colorValue = new Color(1f, 0.93f, 0.84f);
            lso.FindProperty("sunIntensity").floatValue = 1.3f;
            lso.FindProperty("sunEuler").vector3Value = new Vector3(42f, -55f, 0f);
            lso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, EnvPath);
            Object.DestroyImmediate(root);
            Debug.Log("[Arena] Env_NewYork written");
        }

        internal static float JerseyLength(GameObject jersey)
        {
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(jersey);
            Bounds b = RenderBounds(probe);
            Object.DestroyImmediate(probe);
            return Mathf.Max(b.size.x, b.size.z) * 0.98f;   // placed with yaw 90 → along X (long axis is the prefab's Z)
        }

        /// <summary>Lines buildings up from x0 to x1 with fronts at <paramref name="frontZ"/>.</summary>
        private static void Row(Transform parent, string[] names, float x0, float x1, float frontZ, float yaw,
            System.Func<Transform, GameObject, Vector3, float, GameObject> put, System.Func<string, float> width, System.Func<string, float> depth, bool frontAt = false)
        {
            float x = x0;
            int i = 0;
            while (true)
            {
                string n = names[i % names.Length];
                float w = width(n);
                if (x + w > x1 + 0.5f) break;
                float d = depth(n);
                // yaw 180: front faces -Z, building extends to +Z from the front line. yaw 0: front faces +Z, extends to -Z.
                float z = yaw == 180f ? frontZ + d * 0.5f : frontZ - d * 0.5f;
                put(parent, P(n), new Vector3(x + w * 0.5f, 0f, z), yaw);
                x += w + 0.4f;
                i++;
            }
        }

        internal static void Boundary(Transform parent, Vector3 centre, Vector3 size)
        {
            var w = new GameObject("Boundary"); w.transform.SetParent(parent, false); w.transform.localPosition = centre;
            w.AddComponent<BoxCollider>().size = size;
        }

        internal static void Surface(Transform parent, string name, Vector3 pos, Vector2 size, Material m)
        {
            Transform t = ProjectBootstrapper.Primitive(PrimitiveType.Plane, parent, name, pos, Quaternion.identity, new Vector3(size.x / 10f, 1f, size.y / 10f), null, false);
            t.GetComponent<Renderer>().sharedMaterial = m;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        // ------------------------------------------------------------------ garage

        private static void BuildGarage(Transform g, System.Func<Transform, GameObject, Vector3, float, GameObject> put, GameObject jersey, float jl)
        {
            GameObject module = RealPropFactory.Load("GarageModule");
            put(g, module, G, 0f);
            put(g, module, G + Vector3.up * Level, 0f);
            // Underside of level 2 (seen from level 1), no collider (the module's floor box is the collider).
            RealGarageBuilder.Box(g, "Level2Soffit", G + new Vector3(0f, Level - 0.25f, 0f), new Vector3(HallHX * 2f, 0.3f, HallHZ * 2f), "M_DeckConcrete", 4f, collider: false);

            // Ramp: x 80 (ground) → 40 (level 2), along z 25.8-33, 6° slope.
            float run = RampFootX - RampTopX, len = Mathf.Sqrt(run * run + Level * Level), ang = Mathf.Atan2(Level, run) * Mathf.Rad2Deg;
            Transform ramp = RealGarageBuilder.Box(g, "Ramp", new Vector3((RampFootX + RampTopX) * 0.5f, Level * 0.5f - 0.15f, RampZ), new Vector3(len, 0.3f, RampHalf * 2f), "M_DeckConcrete", 4f);
            ramp.localRotation = Quaternion.Euler(0f, 0f, -ang);
            // Level-2 apron west of the halls: a strip at the ramp top and the main apron to the hall entry.
            RealGarageBuilder.Box(g, "ApronStrip", new Vector3(33f, Level - 0.15f, RampZ), new Vector3(14f, 0.3f, RampHalf * 2f), "M_DeckConcrete", 4f);
            RealGarageBuilder.Box(g, "ApronMain", new Vector3(38.125f, Level - 0.15f, 41.9f), new Vector3(24.25f, 0.3f, 17.8f), "M_DeckConcrete", 4f);
            // Columns under the apron (out of the entry lane z 45.2-50.8 and the forecourt drive line x 26-37).
            foreach (Vector3 c in new[] { new Vector3(26.5f, 0f, 33.5f), new Vector3(26.5f, 0f, 41f), new Vector3(49.7f, 0f, 41f), new Vector3(39.5f, 0f, 26.2f), new Vector3(39.5f, 0f, 41f) })
                RealGarageBuilder.Box(g, "Column", c + new Vector3(0f, (Level - 0.3f) * 0.5f, 0f), new Vector3(0.6f, Level - 0.3f, 0.6f), "M_ConcreteWall", 2f);

            // Invisible safety walls (no driving off a deck edge, no sneaking under the ramp).
            Boundary(g, new Vector3(60f, 3.25f, 33.2f), new Vector3(40f, 6.5f, 0.3f));                 // ramp north / level-2 hall south
            Boundary(g, new Vector3(60f, 3.25f, 25.6f), new Vector3(40f, 6.5f, 0.3f));                 // ramp south
            Boundary(g, new Vector3(40.2f, 1.6f, RampZ), new Vector3(0.3f, 3.2f, RampHalf * 2f));       // under the ramp top
            Boundary(g, new Vector3(60f, Level + 0.9f, 62.85f), new Vector3(20f, 1.8f, 0.3f));          // level 2 north
            Boundary(g, new Vector3(69.95f, Level + 0.9f, 48f), new Vector3(0.3f, 1.8f, 29.3f));        // level 2 east
            Boundary(g, new Vector3(50.05f, Level + 0.9f, 39.3f), new Vector3(0.3f, 1.8f, 11.8f));      // level 2 west, south of the aisle
            Boundary(g, new Vector3(50.05f, Level + 0.9f, 56.7f), new Vector3(0.3f, 1.8f, 11.8f));      // level 2 west, north of the aisle
            Boundary(g, new Vector3(38.1f, Level + 0.9f, 51f), new Vector3(24.3f, 1.8f, 0.3f));         // apron north
            Boundary(g, new Vector3(25.8f, Level + 0.9f, 38.4f), new Vector3(0.3f, 1.8f, 25.2f));       // apron west
            Boundary(g, new Vector3(33f, Level + 0.9f, 25.6f), new Vector3(14f, 1.8f, 0.3f));           // apron south
            Boundary(g, new Vector3(50.05f, 1.85f, 39.3f), new Vector3(0.3f, 3.7f, 11.8f));             // level 1 west, south of the aisle
            Boundary(g, new Vector3(50.05f, 1.85f, 56.7f), new Vector3(0.3f, 3.7f, 11.8f));             // level 1 west, north of the aisle
            Boundary(g, new Vector3(60f, 1.85f, 62.85f), new Vector3(20f, 3.7f, 0.3f));                 // level 1 north
            Boundary(g, new Vector3(94.4f, 1.5f, 38.3f), new Vector3(0.3f, 3f, 25.2f));                 // exit apron east
            Boundary(g, new Vector3(82f, 1.5f, 51f), new Vector3(24.5f, 3f, 0.3f));                     // exit apron north

            // Visible jersey barriers along the deck edges.
            void Rail(Vector3 a, Vector3 b)
            {
                Vector3 d = b - a; float flat = new Vector2(d.x, d.z).magnitude;
                int n = Mathf.Max(1, Mathf.FloorToInt(flat / jl));
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - 90f;    // jersey long axis is local X
                float roll = Mathf.Atan2(d.y, flat) * Mathf.Rad2Deg;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, (i + 0.5f) / n);
                    GameObject j = put(g, jersey, p, yaw);
                    if (j != null) j.transform.localRotation = Quaternion.Euler(0f, yaw, roll);
                }
            }
            Rail(new Vector3(RampFootX, 0f, 26.1f), new Vector3(RampTopX, Level, 26.1f));
            Rail(new Vector3(RampFootX - 6f, Level * 6f / run, 32.7f), new Vector3(RampTopX + 10f, Level * (run - 10f) / run, 32.7f));
            Rail(new Vector3(26.4f, Level, 26.1f), new Vector3(39.5f, Level, 26.1f));
            Rail(new Vector3(26.3f, Level, 26.5f), new Vector3(26.3f, Level, 50.5f));
            Rail(new Vector3(26.5f, Level, 50.6f), new Vector3(49.8f, Level, 50.6f));
            Rail(new Vector3(50.6f, Level, 62.4f), new Vector3(69.4f, Level, 62.4f));
            Rail(new Vector3(69.5f, Level, 34f), new Vector3(69.5f, Level, 62f));
            Rail(new Vector3(50.6f, Level, 33.7f), new Vector3(69.4f, Level, 33.7f));

            // Signs: low clearance at the entry, ramp sign at the foot of the ramp.
            put(g, P("Sign_LowClearance"), new Vector3(36.5f, 0f, 22.5f), 180f);
            put(g, P("Sign_Ramp"), new Vector3(RampFootX + 3f, 0f, 24.6f), 270f);
            put(g, P("Lamp_Pole"), new Vector3(26.8f, Level, 50.2f), 90f);
            put(g, P("Lamp_Pole"), new Vector3(69.2f, Level, 62.2f), 180f);
        }

        // ------------------------------------------------------------------ missions

        public static void BuildLevels()
        {
            var env = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPath);
            LevelData m1 = Mission1(env), m2 = Mission2(env), m3 = Mission3(env);
            W.WriteCatalog(CountryId, Arena, m1, m2, m3);
            Debug.Log("[Arena] New York missions written");
        }

        private static GameObject Parked(string body) => RealPropFactory.Load("Parked_" + body);
        private static readonly string[] Fleet = { "sedan", "hatchback", "suv", "compact", "wagon", "minivan", "coupe", "pickup", "offroad" };
        private static GameObject NycCar(int k) => (k % 7) switch
        {
            0 => P("Taxi_Yellow"), 2 => P("Sedan_Fairheaven"), 4 => P("Sedan_Negotiator"), 6 => P("Coupe_Phoenix"),
            _ => Parked(Fleet[k % Fleet.Length]),
        };

        /// <summary>US1 — Supermarket Run: wait for the lot's ticket gate, then park facing the store.</summary>
        private static LevelData Mission1(GameObject env)
        {
            var props = new List<PropPlacement>();
            int targetRow = 1, targetBay = 9, k = 0;
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < LotBays; i++)
                {
                    if (row == targetRow && i == targetBay) continue;
                    if ((row * 5 + i * 3) % 5 == 1) continue;                 // free decoy bays
                    float yaw = (row == 0 ? 0f : 180f) + (k % 5 == 3 ? 180f : 0f);
                    W.Place(props, NycCar(k), new Vector3(LotBayX0 + LotBayW * (i + 0.5f), 0f, (row == 0 ? LotRowA : LotRowB) + (k % 3 - 1) * 0.1f), yaw);
                    k++;
                }
            // Avenue traffic parked along the kerbs, a police cruiser by the gate.
            W.Place(props, P("Taxi_Yellow"), new Vector3(-30f, 0f, -AvenueHalf + 1.2f), 90f);
            W.Place(props, P("Police_Cruiser"), new Vector3(28f, 0f, -AvenueHalf + 1.2f), 90f);
            W.Place(props, P("Sedan_Negotiator"), new Vector3(45f, 0f, AvenueHalf - 1.2f), 270f);
            W.Place(props, P("Taxi_Yellow"), new Vector3(70f, 0f, -AvenueHalf + 1.2f), 90f);

            Vector3 spawn = TrafficSide.LaneCentre(new Vector3(-40f, 0f, 0f), Vector3.right, 3.5f, Road);
            float tx = LotBayX0 + LotBayW * (targetBay + 0.5f);
            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 9f, maxSpeedKmh = 2f, holdDuration = 1.0f };
            return W.Write(new W.Mission
            {
                id = "US1", countryId = CountryId, arenaFolder = Arena, number = 1, name = "Supermarket Run", type = MissionType.StandardParking, road = Road, environment = env, props = props,
                briefing = "Drive up the avenue, wait for the supermarket's ticket gate to open, then park nose-in in the free bay facing the store.",
                spawn = spawn, spawnYaw = 90f, time = 130f, collisions = 3, three = (60f, 0), two = (90f, 1), coins = (190, 45),
                objectives = new MissionObjective[]
                {
                    new BarrierGateObjective("Through the ticket gate", "us1_gate", new Vector3(GateX, 0f, -18f), 180f, new Vector2(7f, 4f), maxBarrierHits: 1),
                    new ParkObjective("Park facing the store", new Vector3(tx, 0f, LotRowB), 180f, new Vector2(LotBayW - 0.1f, LotBayD), ParkDirection.NoseIn, rules),
                },
            });
        }

        /// <summary>US2 — Loading Dock: drive the pickup down the narrow alley and reverse it up to the dock.</summary>
        private static LevelData Mission2(GameObject env)
        {
            var props = new List<PropPlacement>();
            for (int i = 0; i < LoadBayX.Length; i++)
            {
                if (i == LoadTarget || i == 1) continue;
                GameObject truck = (i % 3) switch { 0 => P("Utility_Truck"), 1 => P("Pickup_Doutrive"), _ => P("Pickup_Lightbody") };
                W.Place(props, truck, new Vector3(LoadBayX[i], 0f, DockZ + 1.0f + LoadBayD * 0.5f + 0.2f), 0f);   // backed up to the dock
            }
            // Clutter in the yard and the alley: pallets of cones, barrels, a parked cab half in the alley mouth.
            W.Place(props, RealPropFactory.Load("Cones_Cluster"), new Vector3(-46f, 0f, -40f), 0f);
            W.Place(props, RealPropFactory.Load("Barrel"), new Vector3(AlleyX + 2.4f, 0f, -22f), 0f);
            W.Place(props, RealPropFactory.Load("Barrel"), new Vector3(AlleyX + 2.5f, 0f, -26f), 0f);
            W.Place(props, RealPropFactory.Load("WaterBarrier_Red"), new Vector3(YardMinX + 3f, 0f, -36f), 90f);
            W.Place(props, P("Taxi_Yellow"), new Vector3(-60f, 0f, -AvenueHalf + 1.2f), 90f);

            Vector3 spawn = TrafficSide.LaneCentre(new Vector3(-85f, 0f, 0f), Vector3.right, 3.5f, Road);
            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 9f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            CarData pickup = AssetDatabase.LoadAssetAtPath<CarData>("Assets/_Project/Cars/Car_Pickup.asset");
            return W.Write(new W.Mission
            {
                id = "US2", countryId = CountryId, arenaFolder = Arena, number = 2, name = "Loading Dock", type = MissionType.ReverseParking, road = Road, environment = env, props = props,
                forcedCar = pickup,
                briefing = "You're driving the long-bed pickup. Turn into the narrow alley, find room to turn in the loading yard, and reverse into the free dock bay. Tailgate within 0.5 m of the dock.",
                spawn = spawn, spawnYaw = 90f, time = 170f, collisions = 2, three = (80f, 0), two = (120f, 1), coins = (230, 55),
                objectives = new MissionObjective[]
                {
                    new CheckpointObjective("Turn into the loading alley", new Vector3(AlleyX, 0f, -20f), 180f, new Vector2(AlleyHalf * 2f - 0.6f, 4f), "TURN RIGHT INTO THE LOADING ALLEY"),
                    new ReverseParkObjective("Reverse up to the dock", new Vector3(LoadBayX[LoadTarget], 0f, DockZ + 1.0f + LoadBayD * 0.5f), 180f, new Vector2(LoadBayW - 0.1f, LoadBayD), maxRearGap: 0.5f, rules),
                },
            });
        }

        /// <summary>US3 — Level 2 Only: up the garage ramp and into the reserved bay on level 2 (the same bay on level 1 doesn't count).</summary>
        private static LevelData Mission3(GameObject env)
        {
            var props = new List<PropPlacement>();
            int k = 0;
            for (int level = 0; level < 2; level++)
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < GarageBayX.Length; i++)
                    {
                        bool target = side == 1 && i == GarageTarget;      // free on both levels: only level 2 counts
                        if (target || (level == 0 && side == -1 && i == 1) || (level == 1 && side == -1 && i == 4)) continue;
                        Vector3 p = G + new Vector3(GarageBayX[i], level * Level, side * (Aisle + BayBack) * 0.5f);
                        W.Place(props, NycCar(k + 3), p, side == 1 ? 0f : 180f);
                        k++;
                    }
            W.Place(props, P("Police_Cruiser"), new Vector3(31f, 0f, 16f), 0f);              // parked in the forecourt: steer round it
            W.Place(props, P("Taxi_Yellow"), new Vector3(52f, 0f, AvenueHalf - 1.2f), 270f);

            Vector3 spawn = TrafficSide.LaneCentre(new Vector3(75f, 0f, 0f), Vector3.left, 3.5f, Road);
            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 9f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            return W.Write(new W.Mission
            {
                id = "US3", countryId = CountryId, arenaFolder = Arena, number = 3, name = "Level 2 Only", type = MissionType.MultiStoreyRamp, road = Road, environment = env, props = props,
                briefing = "Your permit is for LEVEL 2. Turn into the garage, drive through level 1, take the ramp up and park in the reserved bay. The same bay on level 1 does not count.",
                spawn = spawn, spawnYaw = 270f, time = 210f, collisions = 2, three = (100f, 0), two = (150f, 1), coins = (260, 60),
                objectives = new MissionObjective[]
                {
                    new CheckpointObjective("Enter the garage", G + new Vector3(-HallHX - 2f, 0f, 0f), 90f, new Vector2(Aisle * 2f - 0.4f, 3f), "TURN INTO THE GARAGE - LEVEL 1"),
                    new CheckpointObjective("Take the ramp up", new Vector3(60f, Level * 0.5f, RampZ), 270f, new Vector2(RampHalf * 2f - 0.4f, 4f), "UP THE RAMP TO LEVEL 2"),
                    new FloorParkObjective("Park in the level 2 bay", GarageTargetPos, 0f, new Vector2(2.9f, 5.5f), ParkDirection.NoseIn, "LEVEL 2", rules),
                },
            });
        }
    }
}
