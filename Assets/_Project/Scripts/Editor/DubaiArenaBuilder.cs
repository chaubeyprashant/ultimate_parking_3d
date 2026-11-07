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
    /// UAE — Dubai arena, right-hand traffic, built from the imported Dubai models: a glass tower hotel with a steel
    /// porte-cochère (valet drop-off), ornate pavilion towers, a modern villa with pool (desert resort), date / coconut /
    /// fan palms, desert rocks, and the marina hotel diorama as a distant landmark. A palm-lined boulevard, a hotel
    /// forecourt and guest lot, an underground supercar garage, and a desert road to a gated resort.
    /// </summary>
    public static class DubaiArenaBuilder
    {
        public const string Arena = "Dubai";
        public const string CountryId = "ae";
        public static string EnvPath => F.PrefabPath(Arena, "Env_Dubai");
        public const RoadDirection Road = RoadDirection.RightHand;

        public const float RoadHalf = 8f, MedianHalf = 1f, WalkOuter = 12f;
        // AE1 hotel: canopy forecourt (valet stand) and guest lot west of it.
        public static readonly Vector3 Stand = new Vector3(0f, 0f, 18.5f);
        public const float GuestRowZ = 30f, GuestBayW = 2.9f, GuestBayD = 5.5f, GuestX0 = -62f;
        public const int GuestBays = 10, GuestTarget = 6;
        // AE2 underground garage: ramp north from the boulevard down to a hall.
        public const float RampX = 42f, RampHalf = 3.2f, RampTopZ = WalkOuter + 1f, RampBotZ = WalkOuter + 37f, Depth = 4.2f;
        public static readonly Vector3 Hall = new Vector3(RampX, -Depth, RampBotZ + 2.2f + 9.755f);
        public static readonly float[] HallBayX = { -8.08f, -4.78f, -1.45f, 1.71f, 4.80f, 8.10f };
        public const float HallRow = 5.84f;
        public const int HallTarget = 3;
        public static Vector3 HallBay(int i, int side) => Hall + new Vector3(side * HallRow, 0f, -HallBayX[i]);
        // AE3 desert resort: road south off the boulevard, gate, villa.
        public const float ResortX = -70f, ResortRoadHalf = 4f, GateZ = -58f, VillaBayZ = -84f;

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
                new F.Spec { combine = true, name = "Hotel_Tower", source = "Dubai/dxb_atlanta_offices.glb", fit = F.Fit.Height, size = 140f },
                new F.Spec { combine = true, name = "Porte_Cochere", source = "Dubai/dxb_porte_cochere.glb", fit = F.Fit.Scale, size = 0.001f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Pavilion_Tower", source = "Dubai/dxb_eurasian_towers.glb", pick = Exact("OrientalTowerO-01_0"), fit = F.Fit.Height, size = 16f },
                new F.Spec { combine = true, name = "Pavilion_Small", source = "Dubai/dxb_eurasian_towers.glb", pick = Exact("OrientalTowerU-02_3"), fit = F.Fit.Height, size = 9f },
                new F.Spec { combine = true, name = "Villa", source = "Dubai/dxb_luxury_villa.glb", collider = F.ColliderKind.Box },
                new F.Spec { combine = true, name = "Marina_Landmark", source = "Dubai/dxb_marina_hotel.glb", fit = F.Fit.Width, size = 260f, collider = F.ColliderKind.None },
                new F.Spec { name = "Palm_Date", source = "Dubai/dxb_date_palm.glb", fit = F.Fit.Height, size = 7.5f, colliderHeightCap = 2f },
                new F.Spec { name = "Palm_Coconut", source = "Dubai/dxb_coconut_palm.glb", fit = F.Fit.Height, size = 8f, collider = F.ColliderKind.None },
                new F.Spec { name = "Palm_Fan", source = "Dubai/dxb_palm_trees.glb", pick = Exact("Bark_4"), fit = F.Fit.Height, size = 4.5f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Rock_Large", source = "Dubai/dxb_desert_rocks.glb", pick = Exact("Cube.006"), fit = F.Fit.Height, size = 4.3f },
                new F.Spec { name = "Rock_Wide", source = "Dubai/dxb_desert_rocks.glb", pick = Exact("Cube.010"), fit = F.Fit.Height, size = 2.8f },
                new F.Spec { name = "Rock_Small", source = "Dubai/dxb_desert_rocks.glb", pick = Exact("Cube.004"), fit = F.Fit.Height, size = 1.8f },
            };
            foreach (F.Spec s in specs) F.Make(Arena, s);
            BuildGate();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arena] Dubai props: {specs.Count + 1}");
        }

        private static GameObject P(string name)
        {
            GameObject g = F.Load(Arena, name);
            if (g == null) Debug.LogError("[Arena] Dubai prefab missing: " + name);
            return g;
        }

        /// <summary>Resort gate: imported metal barrier panels on a sliding <see cref="MovingBarrier"/> ("ae3_gate").</summary>
        private static void BuildGate()
        {
            var root = new GameObject("Resort_Gate");
            var moving = new GameObject("Moving").transform;
            moving.SetParent(root.transform, false);
            GameObject panel = RealPropFactory.Load("Barrier_Metal");
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(panel);
            Bounds b = NY.RenderBounds(probe);
            Object.DestroyImmediate(probe);
            bool alongX = b.size.x >= b.size.z;
            float span = Mathf.Max(b.size.x, b.size.z), width = ResortRoadHalf * 2f + 0.4f;
            int n = Mathf.CeilToInt(width / span);
            for (int i = 0; i < n; i++)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(panel, moving);
                p.transform.localRotation = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f);
                p.transform.localPosition = new Vector3(-width * 0.5f + span * (i + 0.5f), 0f, 0f);
            }
            var gate = root.AddComponent<MovingBarrier>();
            gate.Configure("ae3_gate", moving, MovingBarrier.Motion.Slide, new Vector3(n * span + 0.6f, 0f, 0f), 4f, 6f, 1.6f);
            PrefabUtility.SaveAsPrefabAsset(root, F.PrefabPath(Arena, "Resort_Gate"));
            Object.DestroyImmediate(root);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Dubai Arena")]
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
            foreach (string id in new[] { "AE1", "AE2", "AE3" })
                WorldTourBuilder.RenderShots(AssetDatabase.LoadAssetAtPath<LevelData>($"{W.Dir}/Level_{id}.asset"), dir);
        }

        // ------------------------------------------------------------------ environment

        public static void BuildEnvironment()
        {
            var root = new GameObject("Env_Dubai");
            Transform Group(string n) { var g = new GameObject(n).transform; g.SetParent(root.transform, false); return g; }
            GameObject Put(Transform parent, GameObject prefab, Vector3 pos, float yaw)
            {
                if (prefab == null) { Debug.LogError("[Arena] Dubai missing prefab at " + pos); return null; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                return go;
            }
            Vector3 Size(string prefab) { GameObject p = P(prefab); var box = p != null ? p.GetComponent<BoxCollider>() : null; return box != null ? box.size : new Vector3(10f, 8f, 10f); }

            Material paving = MumbaiArenaBuilder.Mat("M_DXB_Paving", "concrete_pavement_diff_1k.jpg", "concrete_pavement_nor_gl_1k.jpg", 40f, new Color(0.98f, 0.92f, 0.82f));
            Material asphalt = MumbaiArenaBuilder.Mat("M_DXB_Asphalt", "Road007_1K-JPG_Color.jpg", "Road007_1K-JPG_NormalGL.jpg", 10f, new Color(0.55f, 0.55f, 0.57f));
            Material sand = MumbaiArenaBuilder.Mat("M_DXB_Sand", "aerial_sand_diff_1k.jpg", "aerial_sand_nor_gl_1k.jpg", 25f, new Color(1f, 0.92f, 0.78f));

            // --- ground: slabs around the garage ramp opening; city paving north of the boulevard, desert south.
            Transform ground = Group("Ground");
            void Slab(string n, float x0, float x1, float z0, float z1, Material m)
            {
                var g = new GameObject(n); g.transform.SetParent(ground, false);
                var c = g.AddComponent<BoxCollider>(); c.center = new Vector3((x0 + x1) * 0.5f, -0.15f, (z0 + z1) * 0.5f); c.size = new Vector3(x1 - x0, 0.3f, z1 - z0);
                NY.Surface(ground, n + "_Surface", new Vector3((x0 + x1) * 0.5f, -0.02f, (z0 + z1) * 0.5f), new Vector2(x1 - x0, z1 - z0), m);
            }
            Slab("GroundNorth", -110f, 110f, RampBotZ, 110f, paving);
            Slab("GroundRampW", -110f, RampX - RampHalf, RampTopZ, RampBotZ, paving);
            Slab("GroundRampE", RampX + RampHalf, 110f, RampTopZ, RampBotZ, paving);
            Slab("GroundCity", -110f, 110f, -WalkOuter - 2f, RampTopZ, paving);
            Slab("Desert", -110f, 110f, -140f, -WalkOuter - 2f, sand);
            NY.Surface(ground, "Boulevard", new Vector3(0f, 0.004f, 0f), new Vector2(218f, RoadHalf * 2f), asphalt);
            NY.Surface(ground, "Forecourt", new Vector3(-20f, 0.005f, (WalkOuter + 40f) * 0.5f), new Vector2(92f, 40f - WalkOuter), asphalt);
            NY.Surface(ground, "ResortRoad", new Vector3(ResortX, 0.005f, (-RoadHalf + VillaBayZ - 6f) * 0.5f), new Vector2(ResortRoadHalf * 2f, -RoadHalf - VillaBayZ + 6f), asphalt);
            NY.Surface(ground, "VillaApron", new Vector3(ResortX, 0.006f, VillaBayZ), new Vector2(22f, 12f), asphalt);
            NY.Boundary(ground, new Vector3(0f, 2f, 109f), new Vector3(220f, 4f, 1f));
            NY.Boundary(ground, new Vector3(0f, 2f, -139f), new Vector3(220f, 4f, 1f));
            NY.Boundary(ground, new Vector3(-109f, 2f, -15f), new Vector3(1f, 4f, 250f));
            NY.Boundary(ground, new Vector3(109f, 2f, -15f), new Vector3(1f, 4f, 250f));

            // --- boulevard: median with kerbs and date palms, lane dashes, edge lines
            Transform blvd = Group("Boulevard");
            RealGarageBuilder.Box(blvd, "Median", new Vector3(0f, 0.09f, 0f), new Vector3(216f, 0.18f, MedianHalf * 2f), "M_KerbStone", 1.5f);
            for (float x = -100f; x <= 100f; x += 12f) Put(blvd, P("Palm_Date"), new Vector3(x, 0.18f, 0f), (x * 37f) % 360f);
            foreach (float z in new[] { -4.5f, 4.5f })
                for (float x = -106f; x < 106f; x += 9f)
                    ProjectBootstrapper.Primitive(PrimitiveType.Cube, blvd, "LaneDash", new Vector3(x, 0.02f, z), Quaternion.identity, new Vector3(3f, 0.01f, 0.13f), "LineWhite", false);
            foreach (float z in new[] { -RoadHalf + 0.25f, RoadHalf - 0.25f, -MedianHalf - 0.25f, MedianHalf + 0.25f })
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, blvd, "EdgeLine", new Vector3(0f, 0.02f, z), Quaternion.identity, new Vector3(216f, 0.01f, 0.13f), z * z < 4f ? "LineYellow" : "LineWhite", false);

            // --- hotel: tower behind a porte-cochère forecourt, pavilion towers, guest lot
            Transform hotel = Group("Hotel");
            Vector3 tower = Size("Hotel_Tower");
            Put(hotel, P("Hotel_Tower"), new Vector3(0f, 0f, 40f + tower.z * 0.5f), 180f);
            Put(hotel, P("Porte_Cochere"), Stand + new Vector3(0f, 0f, 2f), 0f);
            Put(hotel, P("Pavilion_Tower"), new Vector3(-16f, 0f, WalkOuter + 1.5f), 0f);
            Put(hotel, P("Pavilion_Tower"), new Vector3(16f, 0f, WalkOuter + 1.5f), 0f);
            foreach (float x in new[] { -24f, -10f, 10f, 24f }) Put(hotel, P("Palm_Coconut"), new Vector3(x, 0f, 38f), x * 11f);
            float guestX1 = GuestX0 + GuestBays * GuestBayW;
            for (int i = 0; i <= GuestBays; i++)
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, hotel, "BayLine", new Vector3(GuestX0 + i * GuestBayW, 0.03f, GuestRowZ), Quaternion.identity, new Vector3(0.12f, 0.02f, GuestBayD), "LineWhite", false);
            RealGarageBuilder.Box(hotel, "GuestLotWall", new Vector3((GuestX0 + guestX1) * 0.5f, 0.5f, GuestRowZ + GuestBayD * 0.5f + 0.5f), new Vector3(guestX1 - GuestX0 + 2f, 1f, 0.4f), "M_KerbStone", 1.5f);
            for (float x = GuestX0 + 1f; x < guestX1; x += 6f) Put(hotel, P("Palm_Fan"), new Vector3(x, 0f, GuestRowZ + GuestBayD * 0.5f + 1.8f), x * 23f);
            // Other towers along the boulevard (city skyline) and the marina diorama far away.
            Put(hotel, P("Hotel_Tower"), new Vector3(-90f, 0f, 70f), 180f);
            Put(hotel, P("Hotel_Tower"), new Vector3(85f, 0f, 75f), 180f);
            Put(hotel, P("Marina_Landmark"), new Vector3(60f, -0.5f, 330f), 200f);

            // --- underground supercar garage (ramp north of the boulevard)
            BuildGarage(Group("Garage"), Put);

            // --- desert resort: road south, gate between pavilions, villa, rocks and palms
            Transform resort = Group("Resort");
            Put(resort, P("Resort_Gate"), new Vector3(ResortX, 0f, GateZ), 0f);
            Put(resort, P("Pavilion_Small"), new Vector3(ResortX - ResortRoadHalf - 5.6f, 0f, GateZ), 0f);
            Put(resort, P("Pavilion_Small"), new Vector3(ResortX + ResortRoadHalf + 5.6f, 0f, GateZ), 0f);
            // Walls either side of the gate (so the resort is entered through it).
            RealGarageBuilder.Box(resort, "ResortWallW", new Vector3(ResortX - 24f, 1f, GateZ - 0.6f), new Vector3(36f, 2f, 0.5f), "M_KerbStone", 1.5f);
            RealGarageBuilder.Box(resort, "ResortWallE", new Vector3(ResortX + 24f, 1f, GateZ - 0.6f), new Vector3(36f, 2f, 0.5f), "M_KerbStone", 1.5f);
            NY.Boundary(resort, new Vector3(ResortX - 42f, 1.5f, -100f), new Vector3(0.5f, 3f, 84f));
            NY.Boundary(resort, new Vector3(ResortX + 42f, 1.5f, -100f), new Vector3(0.5f, 3f, 84f));
            Vector3 villa = Size("Villa");
            Put(resort, P("Villa"), new Vector3(ResortX, 0f, VillaBayZ - 8f - villa.z * 0.5f), 0f);
            for (float z = -20f; z > GateZ + 2f; z -= 9f)
                foreach (float sx in new[] { -1f, 1f }) Put(resort, P("Palm_Coconut"), new Vector3(ResortX + sx * (ResortRoadHalf + 2.2f), 0f, z), z * 13f + sx * 40f);
            var rocks = new (string p, Vector3 pos, float yaw)[]
            {
                ("Rock_Large", new Vector3(-40f, 0f, -40f), 20f), ("Rock_Wide", new Vector3(-30f, 0f, -70f), 110f), ("Rock_Small", new Vector3(-50f, 0f, -30f), 0f),
                ("Rock_Large", new Vector3(-100f, 0f, -45f), 200f), ("Rock_Wide", new Vector3(-95f, 0f, -25f), 60f), ("Rock_Small", new Vector3(-85f, 0f, -35f), 140f),
                ("Rock_Large", new Vector3(20f, 0f, -60f), 300f), ("Rock_Wide", new Vector3(60f, 0f, -45f), 10f), ("Rock_Small", new Vector3(40f, 0f, -30f), 75f),
                ("Rock_Large", new Vector3(80f, 0f, -90f), 150f), ("Rock_Wide", new Vector3(0f, 0f, -100f), 220f),
            };
            foreach (var (p, pos, yaw) in rocks) Put(resort, P(p), pos, yaw);

            // Hot, bright desert light with warm haze.
            var light = root.AddComponent<SceneLightingOverride>();
            var lso = new SerializedObject(light);
            lso.FindProperty("ambientSky").colorValue = new Color(0.74f, 0.74f, 0.76f);
            lso.FindProperty("ambientEquator").colorValue = new Color(0.66f, 0.6f, 0.52f);
            lso.FindProperty("ambientGround").colorValue = new Color(0.4f, 0.35f, 0.28f);
            lso.FindProperty("fog").boolValue = true;
            lso.FindProperty("fogColor").colorValue = new Color(0.92f, 0.85f, 0.74f);
            lso.FindProperty("fogDensity").floatValue = 0.0028f;
            lso.FindProperty("sunColor").colorValue = new Color(1f, 0.95f, 0.85f);
            lso.FindProperty("sunIntensity").floatValue = 1.5f;
            lso.FindProperty("sunEuler").vector3Value = new Vector3(62f, -30f, 0f);
            lso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, EnvPath);
            Object.DestroyImmediate(root);
            Debug.Log("[Arena] Env_Dubai written");
        }

        private static void BuildGarage(Transform g, System.Func<Transform, GameObject, Vector3, float, GameObject> put)
        {
            float run = RampBotZ - RampTopZ, len = Mathf.Sqrt(run * run + Depth * Depth), ang = Mathf.Atan2(Depth, run) * Mathf.Rad2Deg;
            Transform ramp = RealGarageBuilder.Box(g, "Ramp", new Vector3(RampX, -Depth * 0.5f - 0.15f, (RampTopZ + RampBotZ) * 0.5f), new Vector3(RampHalf * 2f, 0.3f, len), "M_DeckConcrete", 4f);
            ramp.localRotation = Quaternion.Euler(ang, 0f, 0f);                // +Z end lower
            float hallS = Hall.z - 9.755f, hallN = Hall.z + 9.755f, hallW = Hall.x - 14.64f, hallE = Hall.x + 14.64f;
            RealGarageBuilder.Box(g, "Landing", new Vector3(RampX, -Depth - 0.15f, (RampBotZ + hallS) * 0.5f), new Vector3(RampHalf * 2f, 0.3f, hallS - RampBotZ + 0.1f), "M_DeckConcrete", 4f);
            foreach (float sx in new[] { -1f, 1f })
                RealGarageBuilder.Box(g, "RampWall", new Vector3(RampX + sx * (RampHalf + 0.15f), -Depth * 0.5f, (RampTopZ + hallS) * 0.5f), new Vector3(0.3f, Depth, hallS - RampTopZ), "M_ConcreteWall", 2f);
            put(g, RealPropFactory.Load("GarageModule"), Hall, 90f);
            RealGarageBuilder.Box(g, "Ceiling", new Vector3(Hall.x, -0.45f, Hall.z), new Vector3(29.3f, 0.3f, 19.6f), "M_DeckConcrete", 4f, collider: false);
            float wallY = -Depth * 0.5f - 0.15f, wallH = Depth - 0.3f;
            RealGarageBuilder.Box(g, "WallN", new Vector3(Hall.x, wallY, hallN + 0.15f), new Vector3(hallE - hallW + 0.6f, wallH, 0.3f), "M_ConcreteWall", 2f);
            RealGarageBuilder.Box(g, "WallW", new Vector3(hallW - 0.15f, wallY, Hall.z), new Vector3(0.3f, wallH, hallN - hallS), "M_ConcreteWall", 2f);
            RealGarageBuilder.Box(g, "WallE", new Vector3(hallE + 0.15f, wallY, Hall.z), new Vector3(0.3f, wallH, hallN - hallS), "M_ConcreteWall", 2f);
            RealGarageBuilder.Box(g, "WallS_W", new Vector3((hallW + RampX - RampHalf) * 0.5f, wallY, hallS - 0.15f), new Vector3(RampX - RampHalf - hallW, wallH, 0.3f), "M_ConcreteWall", 2f);
            RealGarageBuilder.Box(g, "WallS_E", new Vector3((hallE + RampX + RampHalf) * 0.5f, wallY, hallS - 0.15f), new Vector3(hallE - RampX - RampHalf, wallH, 0.3f), "M_ConcreteWall", 2f);
            GameObject tube = RealPropFactory.Load("FluorescentLight");
            for (float z = hallS + 4f; z < hallN; z += 8f)            // fittings are ~13k triangles each: a sparse grid
                foreach (float x in new[] { Hall.x - 5.8f, Hall.x + 5.8f })
                    if (tube != null) put(g, tube, new Vector3(x, -0.62f, z), 0f);
            put(g, P("Palm_Fan"), new Vector3(RampX - RampHalf - 2f, 0f, RampTopZ + 1f), 0f);
            put(g, P("Palm_Fan"), new Vector3(RampX + RampHalf + 2f, 0f, RampTopZ + 1f), 90f);
        }

        // ------------------------------------------------------------------ missions

        public static void BuildLevels()
        {
            var env = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPath);
            LevelData m1 = Mission1(env), m2 = Mission2(env), m3 = Mission3(env);
            W.WriteCatalog(CountryId, Arena, m1, m2, m3);
            Debug.Log("[Arena] Dubai missions written");
        }

        private static GameObject Parked(string body) => RealPropFactory.Load("Parked_" + body);
        private static readonly string[] Luxury = { "sport", "suv", "coupe", "sedan", "offroad" };

        /// <summary>AE1 — Hotel Valet: pull up under the porte-cochère, then park the guest's car gently in the guest lot.</summary>
        private static LevelData Mission1(GameObject env)
        {
            var props = new List<PropPlacement>();
            for (int i = 0; i < GuestBays; i++)
            {
                if (i == GuestTarget || i == 2) continue;
                W.Place(props, Parked(Luxury[i % Luxury.Length]), new Vector3(GuestX0 + GuestBayW * (i + 0.5f), 0f, GuestRowZ), i % 3 == 0 ? 180f : 0f);
            }
            W.Place(props, Parked("suv"), Stand + new Vector3(9f, 0f, 0f), 90f);              // a guest unloading ahead of the stand
            Vector3 spawn = new Vector3(60f, 0f, 6.25f);                                     // westbound outer lane (RHT: north side)
            CarData supercar = AssetDatabase.LoadAssetAtPath<CarData>("Assets/_Project/Cars/Car_Sport.asset");
            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 8f, maxSpeedKmh = 2f, holdDuration = 1.0f };
            return W.Write(new W.Mission
            {
                id = "AE1", countryId = CountryId, arenaFolder = Arena, number = 1, name = "Hotel Valet", type = MissionType.ValetParking, road = Road, environment = env, props = props,
                forcedCar = supercar, scoreMode = ScoreMode.Quality, quality = (0.85f, 0.6f),
                briefing = "You're the valet. Pull the guest's supercar up under the hotel canopy, hand over, then park it in the free guest bay. Smoothly: under 18 km/h, no harsh braking, no knocks.",
                spawn = spawn, spawnYaw = 270f, time = 150f, collisions = 2, three = (80f, 0), two = (110f, 1), coins = (260, 60),
                objectives = new MissionObjective[]
                {
                    new ValetParkObjective("Valet the guest's car", Stand, 90f, new Vector3(GuestX0 + GuestBayW * (GuestTarget + 0.5f), 0f, GuestRowZ), 0f,
                        new Vector2(GuestBayW - 0.1f, GuestBayD), ParkDirection.NoseIn, 18f, rules),
                },
            });
        }

        /// <summary>AE2 — Supercar Garage: take the supercar down to the private garage and park it precisely in the VIP bay.</summary>
        private static LevelData Mission2(GameObject env)
        {
            var props = new List<PropPlacement>();
            int k = 0;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < HallBayX.Length; i++)
                {
                    if ((side == 1 && i == HallTarget) || (side == -1 && i == 1)) continue;
                    W.Place(props, Parked(Luxury[k % Luxury.Length]), HallBay(i, side), side == 1 ? 90f : 270f);
                    k++;
                }
            Vector3 spawn = new Vector3(95f, 0f, 6.25f);                                     // westbound outer lane

            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 6f, maxSpeedKmh = 2f, holdDuration = 1.2f };
            return W.Write(new W.Mission
            {
                id = "AE2", countryId = CountryId, arenaFolder = Arena, number = 2, name = "Supercar Garage", type = MissionType.PrecisionParking, road = Road, environment = env, props = props,
                forcedCar = AssetDatabase.LoadAssetAtPath<CarData>("Assets/_Project/Cars/Car_Sport.asset"),
                scoreMode = ScoreMode.Quality, quality = (0.88f, 0.65f), cameraHeight = 2.2f, cameraDistance = 5.5f,
                briefing = "A very expensive car and a very tight garage. Turn into the ramp, go down to the private garage and park dead centre in the VIP bay. One knock costs the third star; two fail.",
                spawn = spawn, spawnYaw = 270f, time = 160f, collisions = 1, three = (90f, 0), two = (130f, 1), coins = (280, 65),
                objectives = new MissionObjective[]
                {
                    new CheckpointObjective("Down to the garage", new Vector3(RampX, -Depth * 0.5f, (RampTopZ + RampBotZ) * 0.5f), 0f, new Vector2(RampHalf * 2f - 0.6f, 4f), "DOWN THE RAMP TO THE PRIVATE GARAGE"),
                    new PrecisionParkObjective("Park in the VIP bay", HallBay(HallTarget, 1), 90f, new Vector2(2.8f, 5.5f), ParkDirection.NoseIn, maxOffset: 0.3f, maxAngle: 5f, minAccuracy: 0.6f, rules),
                },
            });
        }

        /// <summary>AE3 — Desert Resort: through the resort gate, then reach the villa bay before the clock runs out.</summary>
        private static LevelData Mission3(GameObject env)
        {
            var props = new List<PropPlacement>();
            W.Place(props, Parked("offroad"), new Vector3(ResortX - 6f, 0f, VillaBayZ), 0f);
            W.Place(props, Parked("suv"), new Vector3(ResortX + 6f, 0f, VillaBayZ), 0f);
            W.Place(props, Parked("offroad"), new Vector3(ResortX + 2.5f, 0f, -30f), 180f);        // parked on the resort road verge
            Vector3 spawn = new Vector3(-100f, 0f, -6.25f);                                  // eastbound outer lane (RHT: south side)
            var rules = new ParkingRules { edgeMargin = 0.05f, angleTolerance = 8f, maxSpeedKmh = 2f, holdDuration = 1.0f };
            return W.Write(new W.Mission
            {
                id = "AE3", countryId = CountryId, arenaFolder = Arena, number = 3, name = "Desert Resort", type = MissionType.TimedParking, road = Road, environment = env, props = props,
                scoreMode = ScoreMode.Quality, quality = (0.5f, 0.25f),
                briefing = "Turn off the boulevard onto the desert road. Wait for the resort's gate to slide open, then you have 30 seconds to park at the villa. Every knock costs 3 seconds.",
                spawn = spawn, spawnYaw = 90f, time = 180f, collisions = 3, three = (90f, 0), two = (130f, 1), coins = (270, 60),
                objectives = new MissionObjective[]
                {
                    new BarrierGateObjective("Through the resort gate", "ae3_gate", new Vector3(ResortX, 0f, GateZ - 4f), 0f, new Vector2(ResortRoadHalf * 2f - 0.6f, 4f), maxBarrierHits: 1),
                    new TimedParkObjective("Park at the villa", new Vector3(ResortX, 0f, VillaBayZ), 180f, new Vector2(2.9f, 5.5f), ParkDirection.NoseIn, 30f),
                },
            });
        }
    }
}
