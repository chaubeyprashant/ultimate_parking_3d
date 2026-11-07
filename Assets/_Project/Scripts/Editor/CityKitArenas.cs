using System;
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
    /// Shared city layout for the batch-2 arenas (South Korea, Thailand, Singapore, Malaysia). The layout is the same
    /// everywhere (an avenue along X, a lot, a wide lane and a narrow lane to the north, an underground garage to the
    /// south); each country brings its own imported models, lighting, road direction and three missions built from the
    /// reusable mechanics.
    /// </summary>
    public static class CityKit
    {
        public const float RoadHalf = 6.5f, Walk = 11f, KerbW = 0.3f, KerbH = 0.13f;
        public const float LotX0 = 24f, LotRowZ = 34f, LotBayD = 5.5f;
        public const float LaneX = -45f, LaneHalf = 3.5f, LaneEndZ = 62f;
        public const float NarrowX = 100f, NarrowHalf = 2.2f, NarrowEndZ = 56f;
        public const float RampX = 60f, RampHalf = 3.2f, RampTopZ = -Walk, RampBotZ = -Walk - 36f, Depth = 4.2f;
        public static readonly Vector3 Hall = new Vector3(RampX, -Depth, RampBotZ - 2.2f - 9.755f);
        public static readonly float[] HallBayX = { -8.08f, -4.78f, -1.45f, 1.71f, 4.80f, 8.10f };
        public static Vector3 HallBay(int i, int side) => Hall + new Vector3(side * 5.84f, 0f, -HallBayX[i]);

        public sealed class Look
        {
            public Color skyAmbient, equatorAmbient, groundAmbient, fog, sun;
            public float fogDensity, sunIntensity;
            public Vector3 sunEuler;
            public string pavement = "concrete_pavement", paveNormal = "concrete_pavement_nor_gl_1k.jpg", paveDiffuse = "concrete_pavement_diff_1k.jpg";
            public Color paveTint = new Color(0.8f, 0.8f, 0.8f), roadTint = new Color(0.58f, 0.58f, 0.6f);
        }

        public sealed class Plan
        {
            public string arena, id, envName;
            public RoadDirection road;
            public int lotBays = 20, lotTarget = 12;
            public float lotBayW = 2.8f;
            public Look look;
            public string[] northRow, southRow;       // building prefab names (arena folder)
            public Action<Transform, Func<Transform, GameObject, Vector3, float, GameObject>> landmarks;
            public Func<GameObject, LevelData>[] missions;
            public string[] propNames;                // props scattered on the lot / lane for flavour
        }

        public static string EnvPath(Plan p) => F.PrefabPath(p.arena, p.envName);
        public static GameObject P(Plan p, string name)
        {
            GameObject g = F.Load(p.arena, name);
            if (g == null) Debug.LogError($"[Arena] {p.arena} prefab missing: {name}");
            return g;
        }
        public static int Side(Plan p) => TrafficSide.KerbSide(p.road);       // +1 right-hand, −1 left-hand: the keep side across the heading
        public static GameObject Parked(string body) => RealPropFactory.Load("Parked_" + body);
        public static GameObject Car(int k) => (k % 6) switch
        {
            0 => F.Load("London", "Saloon_67") ?? Parked("sedan"), 1 => Parked("hatchback"), 2 => Parked("suv"),
            3 => F.Load("Tokyo", "Sedan_Kiri94") ?? Parked("wagon"), 4 => Parked("compact"), _ => Parked("sedan"),
        };

        public static void Build(Plan p, Action buildProps)
        {
            buildProps();
            BuildEnvironment(p);
            var env = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPath(p));
            LevelData[] levels = p.missions.Select(m => m(env)).ToArray();
            W.WriteCatalog(p.id, p.arena, levels);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arena] {p.arena} built: {levels.Length} missions");
        }

        public static void RenderShots(Plan p, string prefix)
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            string dir = System.IO.Path.GetFullPath("../UltimateCarParking3D_logs/arenas");
            foreach (string id in new[] { prefix + "1", prefix + "2", prefix + "3" })
                WorldTourBuilder.RenderShots(AssetDatabase.LoadAssetAtPath<LevelData>($"{W.Dir}/Level_{id}.asset"), dir);
        }

        // ------------------------------------------------------------------ gate prefab

        public static void BuildGate(Plan p, string prefabName, string barrierId, float width, float closed = 4f, float open = 6f)
        {
            var root = new GameObject(prefabName);
            var moving = new GameObject("Moving").transform;
            moving.SetParent(root.transform, false);
            GameObject panel = RealPropFactory.Load("Barrier_Metal");
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(panel);
            Bounds b = NY.RenderBounds(probe);
            UnityEngine.Object.DestroyImmediate(probe);
            bool alongX = b.size.x >= b.size.z;
            float span = Mathf.Max(b.size.x, b.size.z);
            int n = Mathf.CeilToInt(width / span);
            for (int i = 0; i < n; i++)
            {
                var piece = (GameObject)PrefabUtility.InstantiatePrefab(panel, moving);
                piece.transform.localRotation = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f);
                piece.transform.localPosition = new Vector3(-width * 0.5f + span * (i + 0.5f), 0f, 0f);
            }
            var gate = root.AddComponent<MovingBarrier>();
            gate.Configure(barrierId, moving, MovingBarrier.Motion.Slide, new Vector3(n * span + 0.6f, 0f, 0f), closed, open, 1.6f);
            PrefabUtility.SaveAsPrefabAsset(root, F.PrefabPath(p.arena, prefabName));
            UnityEngine.Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------------ environment

        public static void BuildEnvironment(Plan p)
        {
            var root = new GameObject(p.envName);
            Transform Group(string n) { var g = new GameObject(n).transform; g.SetParent(root.transform, false); return g; }
            GameObject Put(Transform parent, GameObject prefab, Vector3 pos, float yaw)
            {
                if (prefab == null) { Debug.LogError($"[Arena] {p.arena} missing prefab at {pos}"); return null; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                return go;
            }
            Vector3 Size(string n) { GameObject g = P(p, n); var box = g != null ? g.GetComponent<BoxCollider>() : null; return box != null ? box.size : new Vector3(12f, 8f, 12f); }
            Look l = p.look;
            Material pave = MumbaiArenaBuilder.Mat($"M_{p.id.ToUpper()}_Pave", l.paveDiffuse, l.paveNormal, 35f, l.paveTint);
            Material road = MumbaiArenaBuilder.Mat($"M_{p.id.ToUpper()}_Road", "Road007_1K-JPG_Color.jpg", "Road007_1K-JPG_NormalGL.jpg", 10f, l.roadTint);
            Material cobble = MumbaiArenaBuilder.Mat($"M_{p.id.ToUpper()}_Cobble", "large_sandstone_blocks_diff_1k.jpg", "large_sandstone_blocks_nor_gl_1k.jpg", 9f, new Color(0.6f, 0.58f, 0.55f));

            // Ground: slabs around the garage ramp opening (the hall lies under the south slab).
            Transform ground = Group("Ground");
            void Slab(string n, float x0, float x1, float z0, float z1)
            {
                var g = new GameObject(n); g.transform.SetParent(ground, false);
                var c = g.AddComponent<BoxCollider>(); c.center = new Vector3((x0 + x1) * 0.5f, -0.15f, (z0 + z1) * 0.5f); c.size = new Vector3(x1 - x0, 0.3f, z1 - z0);
                NY.Surface(ground, n + "_S", new Vector3((x0 + x1) * 0.5f, -0.02f, (z0 + z1) * 0.5f), new Vector2(x1 - x0, z1 - z0), pave);
            }
            Slab("North", -125f, 125f, RampTopZ, 105f);
            Slab("South", -125f, 125f, -135f, RampBotZ);
            Slab("West", -125f, RampX - RampHalf, RampBotZ, RampTopZ);
            Slab("East", RampX + RampHalf, 125f, RampBotZ, RampTopZ);
            NY.Surface(ground, "Avenue", new Vector3(0f, 0.004f, 0f), new Vector2(248f, RoadHalf * 2f), road);
            float lotX1 = LotX0 + p.lotBays * p.lotBayW;
            NY.Surface(ground, "Lot", new Vector3((12f + lotX1 + 6f) * 0.5f, 0.005f, (Walk + 40f) * 0.5f), new Vector2(lotX1 + 6f - 12f, 40f - Walk), road);
            NY.Surface(ground, "Lane", new Vector3(LaneX, 0.006f, (Walk + LaneEndZ) * 0.5f), new Vector2(LaneHalf * 2f + 0.6f, LaneEndZ - Walk), cobble);
            NY.Surface(ground, "NarrowLane", new Vector3(NarrowX, 0.006f, (Walk + NarrowEndZ) * 0.5f), new Vector2(NarrowHalf * 2f + 0.4f, NarrowEndZ - Walk), cobble);
            NY.Boundary(ground, new Vector3(0f, 2f, 104f), new Vector3(250f, 4f, 1f));
            NY.Boundary(ground, new Vector3(0f, 2f, -134f), new Vector3(250f, 4f, 1f));
            NY.Boundary(ground, new Vector3(-124f, 2f, -15f), new Vector3(1f, 4f, 240f));
            NY.Boundary(ground, new Vector3(124f, 2f, -15f), new Vector3(1f, 4f, 240f));

            // Avenue markings and kerbs (KerbMarker: wheel strikes count). Gaps at the lane, the lot, the ramp.
            Transform marks = Group("Markings");
            foreach (float z in new[] { -0.12f, 0.12f })
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, marks, "Centre", new Vector3(0f, 0.02f, z), Quaternion.identity, new Vector3(246f, 0.01f, 0.1f), "LineYellow", false);
            for (float x = -120f; x < 120f; x += 9f)
                foreach (float z in new[] { -3.25f, 3.25f })
                    ProjectBootstrapper.Primitive(PrimitiveType.Cube, marks, "Dash", new Vector3(x, 0.02f, z), Quaternion.identity, new Vector3(3f, 0.01f, 0.12f), "LineWhite", false);
            Transform kerbs = Group("Kerbs");
            void Kerb(float x0, float x1, int side)
            {
                if (x1 - x0 < 0.5f) return;
                Transform k = RealGarageBuilder.Box(kerbs, "Kerb", new Vector3((x0 + x1) * 0.5f, KerbH * 0.5f, side * (RoadHalf + KerbW * 0.5f)), new Vector3(x1 - x0, KerbH, KerbW), "M_KerbStone", 1f);
                k.gameObject.AddComponent<KerbMarker>();
            }
            Kerb(-123f, LaneX - LaneHalf - 1f, 1); Kerb(LaneX + LaneHalf + 1f, 11f, 1); Kerb(lotX1 + 6f, NarrowX - NarrowHalf - 1f, 1); Kerb(NarrowX + NarrowHalf + 1f, 123f, 1);
            Kerb(-123f, RampX - RampHalf - 1f, -1); Kerb(RampX + RampHalf + 1f, 123f, -1);

            // Buildings: north and south rows (gaps for the lane, lot, narrow lane and ramp).
            Transform north = Group("NorthRow"), south = Group("SouthRow");
            void Row(Transform parent, string[] names, float x0, float x1, int side)
            {
                if (names == null || names.Length == 0 || x1 - x0 < 6f) return;
                float x = x0; int i = 0;
                while (true)
                {
                    string n = names[i % names.Length];
                    Vector3 sz = Size(n);
                    if (x + sz.x > x1 + 0.3f) break;
                    float z = side * (Walk + 0.4f + sz.z * 0.5f);
                    Put(parent, P(p, n), new Vector3(x + sz.x * 0.5f, 0f, z), side > 0 ? 180f : 0f);
                    x += sz.x + 0.4f; i++;
                }
            }
            Row(north, p.northRow, -122f, LaneX - LaneHalf - 2.5f, 1);
            Row(north, p.northRow, LaneX + LaneHalf + 2.5f, 9f, 1);
            Row(north, p.northRow, lotX1 + 8f, NarrowX - NarrowHalf - 2.5f, 1);
            Row(north, p.northRow, NarrowX + NarrowHalf + 2.5f, 122f, 1);
            Row(south, p.southRow, -122f, RampX - RampHalf - 2f, -1);
            Row(south, p.southRow, RampX + RampHalf + 2f, 122f, -1);

            // The lot: bay lines, back wall, side walls; the narrow lane and lane are closed at their ends.
            Transform lot = Group("Lot");
            for (int i = 0; i <= p.lotBays; i++)
                ProjectBootstrapper.Primitive(PrimitiveType.Cube, lot, "BayLine", new Vector3(LotX0 + i * p.lotBayW, 0.03f, LotRowZ), Quaternion.identity, new Vector3(0.12f, 0.02f, LotBayD), "LineWhite", false);
            RealGarageBuilder.Box(lot, "LotWall", new Vector3((LotX0 + lotX1) * 0.5f, 0.75f, LotRowZ + LotBayD * 0.5f + 0.6f), new Vector3(lotX1 - LotX0 + 8f, 1.5f, 0.4f), "M_ConcreteWall", 1.5f);
            NY.Boundary(lot, new Vector3(lotX1 + 3.2f, 1.5f, (Walk + 38f) * 0.5f), new Vector3(0.4f, 3f, 38f - Walk));
            NY.Boundary(lot, new Vector3(12.2f, 1.5f, 24f), new Vector3(0.4f, 3f, 26f));
            Transform lanes = Group("Lanes");
            NY.Boundary(lanes, new Vector3(LaneX - LaneHalf - 0.3f, 1.5f, (Walk + LaneEndZ) * 0.5f), new Vector3(0.4f, 3f, LaneEndZ - Walk));
            NY.Boundary(lanes, new Vector3(LaneX + LaneHalf + 0.3f, 1.5f, (Walk + LaneEndZ) * 0.5f), new Vector3(0.4f, 3f, LaneEndZ - Walk));
            NY.Boundary(lanes, new Vector3(LaneX, 1.5f, LaneEndZ + 0.2f), new Vector3(LaneHalf * 2f + 1f, 3f, 0.4f));
            NY.Boundary(lanes, new Vector3(NarrowX - NarrowHalf - 0.4f, 1.5f, (Walk + NarrowEndZ) * 0.5f), new Vector3(0.4f, 3f, NarrowEndZ - Walk));
            NY.Boundary(lanes, new Vector3(NarrowX + NarrowHalf + 0.4f, 1.5f, (Walk + NarrowEndZ) * 0.5f), new Vector3(0.4f, 3f, NarrowEndZ - Walk));
            NY.Boundary(lanes, new Vector3(NarrowX, 1.5f, NarrowEndZ + 0.2f), new Vector3(NarrowHalf * 2f + 1f, 3f, 0.4f));
            // The lane's keep-side kerb (parallel parking there) and the narrow lane's side walls (a house wall each side).
            Transform kerbLane = RealGarageBuilder.Box(lanes, "LaneKerb", new Vector3(LaneX + Side(p) * (LaneHalf - KerbW * 0.5f), KerbH * 0.5f, (Walk + LaneEndZ) * 0.5f), new Vector3(KerbW, KerbH, LaneEndZ - Walk - 4f), "M_KerbStone", 1f);
            kerbLane.gameObject.AddComponent<KerbMarker>();
            GameObject wall = F.Load("London", "Brick_Row") ?? F.Load("China", "Hall_House");
            foreach (float sx in new[] { -1f, 1f })
                RealGarageBuilder.Box(lanes, "NarrowWall", new Vector3(NarrowX + sx * (NarrowHalf + 1.6f), 2f, (Walk + NarrowEndZ) * 0.5f), new Vector3(2.4f, 4f, NarrowEndZ - Walk), "M_ConcreteWall", 3f);
            RealGarageBuilder.Box(lanes, "LaneWallW", new Vector3(LaneX - LaneHalf - 2f, 2.2f, (Walk + LaneEndZ) * 0.5f), new Vector3(3.2f, 4.4f, LaneEndZ - Walk), "M_ConcreteWall", 3f);
            RealGarageBuilder.Box(lanes, "LaneWallE", new Vector3(LaneX + LaneHalf + 2f, 2.2f, (Walk + LaneEndZ) * 0.5f), new Vector3(3.2f, 4.4f, LaneEndZ - Walk), "M_ConcreteWall", 3f);
            _ = wall;

            // Underground garage (ramp + hall + ceiling + sparse lights).
            BuildGarage(Group("Garage"), Put);

            // Country landmarks and extras.
            p.landmarks?.Invoke(Group("Landmarks"), Put);

            // Street lamps (reused New York pole lamp).
            Transform street = Group("Street");
            GameObject lamp = F.Load("NewYork", "Lamp_Pole");
            for (float x = -112f; x <= 112f; x += 24f)
            {
                if (lamp == null) break;
                if (Mathf.Abs(x - LaneX) > 6f && (x < 8f || x > lotX1 + 6f) && Mathf.Abs(x - NarrowX) > 6f) Put(street, lamp, new Vector3(x, 0f, RoadHalf + 0.9f), 180f);
                if (Mathf.Abs(x + 12f - RampX) > 6f) Put(street, lamp, new Vector3(x + 12f, 0f, -RoadHalf - 0.9f), 0f);
            }

            var light = root.AddComponent<SceneLightingOverride>();
            var lso = new SerializedObject(light);
            lso.FindProperty("ambientSky").colorValue = l.skyAmbient;
            lso.FindProperty("ambientEquator").colorValue = l.equatorAmbient;
            lso.FindProperty("ambientGround").colorValue = l.groundAmbient;
            lso.FindProperty("fog").boolValue = true;
            lso.FindProperty("fogColor").colorValue = l.fog;
            lso.FindProperty("fogDensity").floatValue = l.fogDensity;
            lso.FindProperty("sunColor").colorValue = l.sun;
            lso.FindProperty("sunIntensity").floatValue = l.sunIntensity;
            lso.FindProperty("sunEuler").vector3Value = l.sunEuler;
            lso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, EnvPath(p));
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[Arena] {p.envName} written");
        }

        private static void BuildGarage(Transform g, Func<Transform, GameObject, Vector3, float, GameObject> put)
        {
            float run = RampTopZ - RampBotZ, len = Mathf.Sqrt(run * run + Depth * Depth), ang = Mathf.Atan2(Depth, run) * Mathf.Rad2Deg;
            Transform ramp = RealGarageBuilder.Box(g, "Ramp", new Vector3(RampX, -Depth * 0.5f - 0.15f, (RampTopZ + RampBotZ) * 0.5f), new Vector3(RampHalf * 2f, 0.3f, len), "M_DeckConcrete", 4f);
            ramp.localRotation = Quaternion.Euler(-ang, 0f, 0f);
            float hallN = Hall.z + 9.755f, hallS = Hall.z - 9.755f, hallW = Hall.x - 14.64f, hallE = Hall.x + 14.64f;
            RealGarageBuilder.Box(g, "Landing", new Vector3(RampX, -Depth - 0.15f, (RampBotZ + hallN) * 0.5f), new Vector3(RampHalf * 2f, 0.3f, RampBotZ - hallN + 0.1f), "M_DeckConcrete", 4f);
            foreach (float sx in new[] { -1f, 1f })
                RealGarageBuilder.Box(g, "RampWall", new Vector3(RampX + sx * (RampHalf + 0.15f), -Depth * 0.5f, (RampTopZ + hallN) * 0.5f), new Vector3(0.3f, Depth, RampTopZ - hallN), "M_ConcreteWall", 2f);
            put(g, RealPropFactory.Load("GarageModule"), Hall, 90f);
            RealGarageBuilder.Box(g, "Ceiling", new Vector3(Hall.x, -0.45f, Hall.z), new Vector3(29.3f, 0.3f, 19.6f), "M_DeckConcrete", 4f, collider: false);
            float wallY = -Depth * 0.5f - 0.15f, wallH = Depth - 0.3f;
            RealGarageBuilder.Box(g, "WallS", new Vector3(Hall.x, wallY, hallS - 0.15f), new Vector3(hallE - hallW + 0.6f, wallH, 0.3f), "M_ConcreteWall", 2f);
            RealGarageBuilder.Box(g, "WallW", new Vector3(hallW - 0.15f, wallY, Hall.z), new Vector3(0.3f, wallH, hallN - hallS), "M_ConcreteWall", 2f);
            RealGarageBuilder.Box(g, "WallE", new Vector3(hallE + 0.15f, wallY, Hall.z), new Vector3(0.3f, wallH, hallN - hallS), "M_ConcreteWall", 2f);
            RealGarageBuilder.Box(g, "WallN_W", new Vector3((hallW + RampX - RampHalf) * 0.5f, wallY, hallN + 0.15f), new Vector3(RampX - RampHalf - hallW, wallH, 0.3f), "M_ConcreteWall", 2f);
            RealGarageBuilder.Box(g, "WallN_E", new Vector3((hallE + RampX + RampHalf) * 0.5f, wallY, hallN + 0.15f), new Vector3(hallE - RampX - RampHalf, wallH, 0.3f), "M_ConcreteWall", 2f);
            GameObject tube = RealPropFactory.Load("FluorescentLight");
            for (float z = hallS + 4f; z < hallN; z += 8f)
                foreach (float x in new[] { Hall.x - 5.8f, Hall.x + 5.8f })
                    if (tube != null) put(g, tube, new Vector3(x, -0.62f, z), 0f);
        }

        // ------------------------------------------------------------------ mission pieces

        public static Vector3 AvenueSpawn(Plan p, float x) => TrafficSide.LaneCentre(new Vector3(x, 0f, 0f), Vector3.right, 3.25f, p.road);

        public static Vector3 AvenueSpawnWest(Plan p, float x) => TrafficSide.LaneCentre(new Vector3(x, 0f, 0f), Vector3.left, 3.25f, p.road);

        public static float LotBayX(Plan p, int i) => LotX0 + p.lotBayW * (i + 0.5f);

        /// <summary>Cars in every lot bay except the target (and one decoy), plus a shopper stopped in the aisle.</summary>
        public static void FillLot(Plan p, List<PropPlacement> props, int skipA = 5)
        {
            for (int i = 0; i < p.lotBays; i++)
            {
                if (i == p.lotTarget || i == skipA) continue;
                W.Place(props, Car(i), new Vector3(LotBayX(p, i), 0f, LotRowZ), 180f + (i % 4 == 1 ? 180f : 0f));
            }
            W.Place(props, Car(2), new Vector3(LotX0 + p.lotBays * p.lotBayW * 0.5f, 0f, 20f), 90f);
        }

        /// <summary>A kerbside slot on the avenue's keep side (parallel parking), with a car at each end.</summary>
        public static (Vector3 slot, float yaw) AvenueSlot(Plan p, List<PropPlacement> props, float x)
        {
            int side = Side(p);
            float z = -side * (RoadHalf - 1.05f);
            float parkedYaw = TrafficSide.ParkedYaw(90f, -side, p.road);
            W.Place(props, Car(0), new Vector3(x - 5.7f, 0f, z), parkedYaw);
            W.Place(props, Car(4), new Vector3(x + 5.7f, 0f, z), parkedYaw);
            return (new Vector3(x, 0f, z), 90f);
        }

        public static void FillGarage(Plan p, List<PropPlacement> props, int targetBay, int targetSide)
        {
            int k = 0;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < HallBayX.Length; i++)
                {
                    if ((side == targetSide && i == targetBay) || (side == -targetSide && i == 1)) continue;
                    W.Place(props, Car(k + 1), HallBay(i, side), side == 1 ? 90f : 270f);
                    k++;
                }
        }

        public static MissionObjective GarageRamp() =>
            new CheckpointObjective("Down the ramp", new Vector3(RampX, -Depth * 0.5f, (RampTopZ + RampBotZ) * 0.5f), 180f, new Vector2(RampHalf * 2f - 0.6f, 4f), "DOWN THE RAMP TO LEVEL -1");

        public static ParkingRules Rules(float angle = 8f, float hold = 1.2f, float speed = 2f) =>
            new ParkingRules { edgeMargin = 0.05f, angleTolerance = angle, maxSpeedKmh = speed, holdDuration = hold };
    }
}
