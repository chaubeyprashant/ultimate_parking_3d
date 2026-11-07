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
using K = UltimateCarParking.EditorTools.CityKit;

namespace UltimateCarParking.EditorTools
{
    /// <summary>Per-country definitions for the batch-2 arenas, on the shared <see cref="CityKit"/> layout.</summary>
    internal static class BatchTwo
    {
        internal static System.Func<Transform, IEnumerable<Transform>> Exact(params string[] names) => root =>
        {
            var hits = root.GetComponentsInChildren<Transform>(true).Where(t => names.Contains(t.name)).ToList();
            if (hits.Count == 0) throw new System.Exception("no node named " + string.Join("/", names));
            return hits.Where(t => !hits.Any(h => h != t && t.IsChildOf(h)));
        };

        internal static GameObject Put(Transform parent, GameObject prefab, Vector3 pos, float yaw)
        {
            if (prefab == null) { Debug.LogError("[Arena] batch-2 missing prefab at " + pos); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        internal static LevelData Write(K.Plan p, GameObject env, string id, int number, string name, MissionType type, List<PropPlacement> props, string briefing,
            Vector3 spawn, float spawnYaw, float time, int collisions, (float, int) three, (float, int) two, (int, int) coins, MissionObjective[] objectives,
            ScoreMode mode = ScoreMode.TimeAndCollisions, (float, float)? quality = null, float camH = 0f, float camD = 0f) =>
            W.Write(new W.Mission
            {
                id = id, countryId = p.id, arenaFolder = p.arena, number = number, name = name, type = type, road = p.road, environment = env, props = props,
                briefing = briefing, spawn = spawn, spawnYaw = spawnYaw, time = time, collisions = collisions, three = three, two = two, coins = coins,
                objectives = objectives, scoreMode = mode, quality = quality ?? (0.85f, 0.6f), cameraHeight = camH, cameraDistance = camD,
            });

        internal static string Sub(string c) => c;
    }

    // =====================================================================================================  SOUTH KOREA
    public static class SouthKoreaArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "SouthKorea", id = "kr", envName = "Env_Seoul", road = RoadDirection.RightHand, lotBays = 22, lotTarget = 13, lotBayW = 2.5f,
            northRow = new[] { "Office_Block", "Shop_Front", "Hotel_Low", "Shop_Front", "Hall_Long" },
            southRow = new[] { "Shop_Front", "Hotel_Low", "Shop_Front", "Office_Block" },
            look = new K.Look
            {
                skyAmbient = new Color(0.66f, 0.7f, 0.76f), equatorAmbient = new Color(0.52f, 0.52f, 0.54f), groundAmbient = new Color(0.3f, 0.3f, 0.32f),
                fog = new Color(0.74f, 0.78f, 0.84f), sun = new Color(1f, 0.97f, 0.92f), fogDensity = 0.0042f, sunIntensity = 1.2f, sunEuler = new Vector3(48f, 35f, 0f),
            },
        };

        static SouthKoreaArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Lotte_Tower"), new Vector3(-30f, 0f, 260f), 0f);
                put(g, K.P(Plan, "Pavilion"), new Vector3(-80f, 0f, 70f), 180f);
                put(g, K.P(Plan, "Pavilion"), new Vector3(10f, 0f, -34f), 0f);
                put(g, K.P(Plan, "Subway_Entrance"), new Vector3(-20f, 0f, K.RoadHalf + 3f), 180f);
                put(g, K.P(Plan, "Subway_Entrance"), new Vector3(35f, 0f, -K.RoadHalf - 3f), 0f);
                foreach (float x in new[] { -70f, 5f, 90f }) put(g, K.P(Plan, "Traffic_Light"), new Vector3(x, 0f, K.RoadHalf + 1f), 180f);
                foreach (float x in new[] { -100f, 40f }) put(g, K.P(Plan, "Electric_Box"), new Vector3(x, 0f, K.RoadHalf + 1.4f), 180f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { Mission1, Mission2, Mission3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Hotel_Low", source = "SouthKorea/kr_hotel.glb" },
                new F.Spec { combine = true, name = "Office_Block", source = "SouthKorea/kr_mbc_building.glb", pick = BatchTwo.Exact("Structure", "1F") },
                new F.Spec { combine = true, name = "Hall_Long", source = "SouthKorea/kr_pavilion_b.glb" },
                new F.Spec { combine = true, name = "Shop_Front", source = "SouthKorea/kr_random_street.glb", fit = F.Fit.Width, size = 9f },
                new F.Spec { combine = true, name = "Pavilion", source = "SouthKorea/kr_pavilion_a.glb", fit = F.Fit.Height, size = 8f },
                new F.Spec { combine = true, name = "Lotte_Tower", source = "SouthKorea/kr_lotte_tower.glb", fit = F.Fit.Height, size = 420f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Subway_Entrance", source = "SouthKorea/kr_subway.glb", fit = F.Fit.Width, size = 5f },
                new F.Spec { combine = true, name = "Traffic_Light", source = "SouthKorea/kr_traffic_lights.glb", fit = F.Fit.Height, size = 6f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Electric_Box", source = "SouthKorea/kr_electric_boxes.glb", fit = F.Fit.Height, size = 1.6f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build South Korea Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "KR");

        private static LevelData Mission1(GameObject env)
        {
            var p = Plan; var props = new List<PropPlacement>(); K.FillLot(p, props);
            var rules = K.Rules(8f, 1.2f);
            return BatchTwo.Write(p, env, "KR1", 1, "Apartment Reverse", MissionType.ReverseParking, props,
                "Seoul's apartment car parks have 2.5 m bays. Turn in off the avenue and reverse into the free bay; finish within 0.7 m of the back wall, using the rear sensor.",
                K.AvenueSpawn(p, -40f), 90f, 140f, 2, (65f, 0), (95f, 1), (210, 50), new MissionObjective[]
                {
                    new CheckpointObjective("Turn into the car park", new Vector3(18f, 0f, 16f), 0f, new Vector2(8f, 4f), "TURN LEFT INTO THE CAR PARK"),
                    new ReverseParkObjective("Reverse into the free bay", new Vector3(K.LotBayX(p, p.lotTarget), 0f, K.LotRowZ), 0f, new Vector2(p.lotBayW - 0.1f, K.LotBayD), 0.7f, rules),
                });
        }

        private static LevelData Mission2(GameObject env)
        {
            var p = Plan; var props = new List<PropPlacement>(); K.FillGarage(p, props, 2, 1);
            var rules = K.Rules(8f, 1.2f);
            return BatchTwo.Write(p, env, "KR2", 2, "Tower Garage", MissionType.MultiStoreyRamp, props,
                "Under the office towers: turn into the car park, take the ramp down to level -1 and park in the free bay on the right.",
                K.AvenueSpawnWest(p, 80f), 270f, 170f, 2, (85f, 0), (125f, 1), (240, 55), new MissionObjective[]
                {
                    K.GarageRamp(),
                    new FloorParkObjective("Park on level -1", K.HallBay(2, 1), 90f, new Vector2(2.9f, 5.5f), ParkDirection.NoseIn, "LEVEL -1", rules),
                }, camH: 2.2f, camD: 5.5f);
        }

        private static LevelData Mission3(GameObject env)
        {
            var p = Plan; var props = new List<PropPlacement>(); K.FillLot(p, props);
            var (slot, yaw) = K.AvenueSlot(p, props, -58f);
            var rules = K.Rules(8f, 1.2f);
            return BatchTwo.Write(p, env, "KR3", 3, "Licence Exam", MissionType.ParkingExam, props,
                "The driving licence exam: cross the start line, parallel park at the kerb between the two cars, then drive to the car park and park precisely in the marked bay.",
                K.AvenueSpawn(p, -112f), 90f, 260f, 3, (150f, 0), (200f, 2), (320, 80), new MissionObjective[]
                {
                    new CheckpointObjective("Start line", K.AvenueSpawn(p, -98f), 90f, new Vector2(4f, 8f), "CROSS THE START LINE"),
                    new ParallelParkObjective("Parallel park at the kerb", slot, yaw, new Vector2(2.4f, 6.4f), TrafficSide.KerbSide(p.road), 0.45f, 2, rules),
                    new CheckpointObjective("Turn into the car park", new Vector3(18f, 0f, 16f), 0f, new Vector2(8f, 4f), "TO THE CAR PARK"),
                    new PrecisionParkObjective("Precision bay", new Vector3(K.LotBayX(p, p.lotTarget), 0f, K.LotRowZ), 0f, new Vector2(p.lotBayW - 0.1f, K.LotBayD), ParkDirection.NoseIn, 0.35f, 6f, 0.5f, rules),
                }, ScoreMode.Quality, (0.8f, 0.5f));
        }
    }

    // =====================================================================================================  THAILAND
    public static class ThailandArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Thailand", id = "th", envName = "Env_Bangkok", road = RoadDirection.LeftHand, lotBays = 20, lotTarget = 12, lotBayW = 2.8f,
            northRow = new[] { "City_Block", "Neo_Stall", "City_Block" },
            southRow = new[] { "Neo_Stall", "City_Block", "City_Block" },
            look = new K.Look
            {
                skyAmbient = new Color(0.78f, 0.74f, 0.66f), equatorAmbient = new Color(0.66f, 0.58f, 0.46f), groundAmbient = new Color(0.4f, 0.34f, 0.26f),
                fog = new Color(0.9f, 0.82f, 0.68f), sun = new Color(1f, 0.9f, 0.7f), fogDensity = 0.0055f, sunIntensity = 1.35f, sunEuler = new Vector3(58f, -20f, 0f),
                paveTint = new Color(0.85f, 0.78f, 0.68f),
            },
        };

        static ThailandArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Khmer_Temple"), new Vector3(-20f, 0f, 95f), 180f);
                put(g, K.P(Plan, "Khmer_Temple"), new Vector3(70f, 0f, -80f), 0f);
                foreach (float x in new[] { -85f, -20f, 40f, 75f }) put(g, K.P(Plan, "Fruit_Stall"), new Vector3(x, 0f, K.RoadHalf + 1.8f), 180f);
                foreach (float x in new[] { -60f, 8f, 70f }) put(g, K.P(Plan, "Food_Cart"), new Vector3(x, 0f, -K.RoadHalf - 1.8f), 0f);
                put(g, K.P(Plan, "Tuk_Tuk"), new Vector3(-30f, 0f, K.RoadHalf - 1.4f), 90f);
                put(g, K.P(Plan, "Tuk_Tuk"), new Vector3(52f, 0f, -K.RoadHalf + 1.4f), 270f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { Mission1, Mission2, Mission3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "City_Block", source = "Thailand/th_cityscene.glb", fit = F.Fit.Width, size = 34f },
                new F.Spec { combine = true, name = "Neo_Stall", source = "Thailand/th_neo_bangkok.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "Food_Cart", source = "Thailand/th_props_a.glb", strip = new[] { "Object_2" }, fit = F.Fit.Height, size = 2.4f },
                new F.Spec { combine = true, name = "Fruit_Stall", source = "Thailand/th_fruit.glb", fit = F.Fit.Scale, size = 0.01f },
                new F.Spec { combine = true, name = "Tuk_Tuk", source = "Thailand/th_tuktuk.glb", fit = F.Fit.Length, size = 3.2f, colliderHeightCap = 1.8f },
                new F.Spec { combine = true, name = "Khmer_Temple", source = "Thailand/th_khmer_temple.glb", fit = F.Fit.Width, size = 70f, collider = F.ColliderKind.None },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Sliding", "th2_gate", 12f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Thailand Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "TH");

        private static LevelData Mission1(GameObject env)
        {
            var p = Plan; var props = new List<PropPlacement>();
            W.Place(props, RealPropFactory.Load("Cones_Cluster"), new Vector3(K.NarrowX + (p.road == RoadDirection.LeftHand ? -1.1f : 1.1f), 0f, 28f), 0f);
            W.Place(props, K.P(p, "Tuk_Tuk"), new Vector3(K.NarrowX + (p.road == RoadDirection.LeftHand ? 1.2f : -1.2f), 0f, 36f), 0f);
            var rules = K.Rules(8f, 1.0f);
            return BatchTwo.Write(p, env, "TH1", 1, "Soi Squeeze", MissionType.NarrowRoute, props,
                "Turn left into the soi — a lane barely wider than the car — squeeze past the cones and a parked tuk-tuk, and park at the end.",
                K.AvenueSpawn(p, 70f), 90f, 160f, 3, (80f, 0), (115f, 1), (230, 55), new MissionObjective[]
                {
                    new NarrowAlleyObjective("Down the soi", new Vector3(K.NarrowX, 0f, K.Walk + 1f), new Vector3(K.NarrowX, 0f, 44f), K.NarrowHalf * 2f, maxScrapes: 2, reverseThrough: false, goodClearance: 0.3f),
                    new ParkObjective("Park at the end", new Vector3(K.NarrowX, 0f, 50f), 0f, new Vector2(2.8f, 5.5f), ParkDirection.NoseIn, rules),
                }, ScoreMode.Quality, (0.8f, 0.5f));
        }

        private static LevelData Mission2(GameObject env)
        {
            var p = Plan; var props = new List<PropPlacement>(); K.FillLot(p, props);
            W.Place(props, K.P(p, "Gate_Sliding"), new Vector3(18f, 0f, 15f), 0f);
            var rules = K.Rules(9f, 1.0f);
            return BatchTwo.Write(p, env, "TH2", 2, "Bazaar Rush", MissionType.TimedParking, props,
                "The bazaar car park's gate opens on a timer. Wait for it, drive through, then take the free bay within 30 seconds; every knock costs 3.",
                K.AvenueSpawn(p, -35f), 90f, 170f, 3, (70f, 0), (100f, 1), (240, 55), new MissionObjective[]
                {
                    new BarrierGateObjective("Through the bazaar gate", "th2_gate", new Vector3(18f, 0f, 21f), 0f, new Vector2(10f, 4f), maxBarrierHits: 1),
                    new TimedParkObjective("Beat the clock", new Vector3(K.LotBayX(p, p.lotTarget), 0f, K.LotRowZ), 0f, new Vector2(p.lotBayW - 0.1f, K.LotBayD), ParkDirection.NoseIn, 30f, 3f, rules),
                }, ScoreMode.Quality, (0.45f, 0.2f));
        }

        private static LevelData Mission3(GameObject env)
        {
            var p = Plan; var props = new List<PropPlacement>(); K.FillLot(p, props);
            var rules = K.Rules(8f, 1.0f);
            return BatchTwo.Write(p, env, "TH3", 3, "Hotel Valet", MissionType.ValetParking, props,
                "Valet duty at the hotel: pull up at the stand, take the keys, then park the guest's car smoothly in the car park. Keep under 18 km/h and don't knock anything.",
                K.AvenueSpawn(p, -35f), 90f, 160f, 2, (80f, 0), (110f, 1), (250, 60), new MissionObjective[]
                {
                    new ValetParkObjective("Valet the guest's car", new Vector3(18f, 0f, 17f), 0f, new Vector3(K.LotBayX(p, p.lotTarget), 0f, K.LotRowZ), 0f, new Vector2(p.lotBayW - 0.1f, K.LotBayD), ParkDirection.NoseIn, 18f, rules),
                }, ScoreMode.Quality, (0.85f, 0.6f));
        }
    }

    // =====================================================================================================  SINGAPORE
    public static class SingaporeArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Singapore", id = "sg", envName = "Env_Singapore", road = RoadDirection.LeftHand, lotBays = 24, lotTarget = 14, lotBayW = 2.4f,
            northRow = new[] { "Shophouse", "Shophouse", "Shophouse", "Colonial" },
            southRow = new[] { "Shophouse", "Shophouse", "Shophouse", "Shophouse" },
            look = new K.Look
            {
                skyAmbient = new Color(0.72f, 0.78f, 0.84f), equatorAmbient = new Color(0.6f, 0.6f, 0.58f), groundAmbient = new Color(0.34f, 0.34f, 0.34f),
                fog = new Color(0.82f, 0.88f, 0.92f), sun = new Color(1f, 0.97f, 0.9f), fogDensity = 0.0048f, sunIntensity = 1.4f, sunEuler = new Vector3(62f, 20f, 0f),
                paveTint = new Color(0.9f, 0.88f, 0.84f),
            },
        };

        static SingaporeArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Tower"), new Vector3(-70f, 0f, 120f), 180f);
                put(g, K.P(Plan, "Tower"), new Vector3(60f, 0f, 135f), 180f);
                put(g, K.P(Plan, "Stadium"), new Vector3(-30f, 0f, -130f), 0f);
                put(g, K.P(Plan, "Lighthouse"), new Vector3(118f, 0f, -40f), 0f);
                put(g, K.P(Plan, "Colonial"), new Vector3(10f, 0f, -40f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { Mission1, Mission2, Mission3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Shophouse", source = "Singapore/sg_shophouse.glb" },
                new F.Spec { combine = true, name = "Colonial", source = "Singapore/sg_chijmes.glb", fit = F.Fit.Width, size = 60f },
                new F.Spec { combine = true, name = "Tower", source = "Singapore/sg_skyscraper.glb", fit = F.Fit.Height, size = 130f },
                new F.Spec { combine = true, name = "Stadium", source = "Singapore/sg_stadium.glb", fit = F.Fit.Width, size = 150f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Lighthouse", source = "Singapore/sg_lighthouse.glb", fit = F.Fit.Height, size = 22f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Ticket", "sg2_gate", 6.8f, 3f, 5f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Singapore Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "SG");

        private static LevelData Mission1(GameObject env)
        {
            var p = Plan; var props = new List<PropPlacement>(); K.FillLot(p, props);
            var rules = K.Rules(5f, 1.5f);
            return BatchTwo.Write(p, env, "SG1", 1, "HDB Bay", MissionType.PrecisionParking, props,
                "Singapore's housing-estate car parks: 2.4 m bays, no tolerance. Turn into the car park and park dead centre and straight; the stars come from accuracy, not speed.",
                K.AvenueSpawn(p, -40f), 90f, 150f, 2, (75f, 0), (105f, 1), (220, 55), new MissionObjective[]
                {
                    new CheckpointObjective("Into the car park", new Vector3(18f, 0f, 16f), 0f, new Vector2(8f, 4f), "TURN INTO THE CAR PARK"),
                    new PrecisionParkObjective("Park dead centre", new Vector3(K.LotBayX(p, p.lotTarget), 0f, K.LotRowZ), 0f, new Vector2(p.lotBayW - 0.1f, K.LotBayD), ParkDirection.NoseIn, 0.22f, 4f, 0.55f, rules),
                }, ScoreMode.Quality, (0.85f, 0.6f));
        }

        private static LevelData Mission2(GameObject env)
        {
            var p = Plan; var props = new List<PropPlacement>(); K.FillGarage(p, props, 3, 1);
            W.Place(props, K.P(p, "Gate_Ticket"), new Vector3(K.RampX, 0f, -9f), 0f);
            var rules = K.Rules(8f, 1.2f);
            return BatchTwo.Write(p, env, "SG2", 2, "Ticket Garage", MissionType.MultiStoreyRamp, props,
                "The car park's ticket gate opens on a timer. Wait for it, drive through and down the ramp to level -1, and park in the free bay.",
                K.AvenueSpawnWest(p, 90f), 270f, 180f, 2, (95f, 0), (135f, 1), (250, 60), new MissionObjective[]
                {
                    new BarrierGateObjective("Through the ticket gate", "sg2_gate", new Vector3(K.RampX, 0f, -12.5f), 180f, new Vector2(6f, 4f), maxBarrierHits: 1),
                    K.GarageRamp(),
                    new FloorParkObjective("Park on level -1", K.HallBay(3, 1), 90f, new Vector2(2.9f, 5.5f), ParkDirection.NoseIn, "LEVEL -1", rules),
                }, camH: 2.2f, camD: 5.5f);
        }

        private static LevelData Mission3(GameObject env)
        {
            var p = Plan; var props = new List<PropPlacement>(); K.FillLot(p, props);
            var (slot, yaw) = K.AvenueSlot(p, props, -58f);
            var rules = K.Rules(7f, 1.2f);
            return BatchTwo.Write(p, env, "SG3", 3, "Marina Exam", MissionType.ParkingExam, props,
                "The marina driving test: cross the start line, parallel park at the kerb on the left, then park precisely in the car park bay.",
                K.AvenueSpawn(p, -112f), 90f, 260f, 3, (150f, 0), (200f, 2), (320, 80), new MissionObjective[]
                {
                    new CheckpointObjective("Start line", K.AvenueSpawn(p, -98f), 90f, new Vector2(4f, 8f), "CROSS THE START LINE"),
                    new ParallelParkObjective("Parallel park at the kerb", slot, yaw, new Vector2(2.4f, 6.4f), TrafficSide.KerbSide(p.road), 0.45f, 2, rules),
                    new CheckpointObjective("Into the car park", new Vector3(18f, 0f, 16f), 0f, new Vector2(8f, 4f), "TO THE CAR PARK"),
                    new PrecisionParkObjective("Precision bay", new Vector3(K.LotBayX(p, p.lotTarget), 0f, K.LotRowZ), 0f, new Vector2(p.lotBayW - 0.1f, K.LotBayD), ParkDirection.NoseIn, 0.3f, 5f, 0.5f, rules),
                }, ScoreMode.Quality, (0.8f, 0.5f));
        }
    }

    // =====================================================================================================  MALAYSIA
    public static class MalaysiaArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Malaysia", id = "my", envName = "Env_Kuching", road = RoadDirection.LeftHand, lotBays = 20, lotTarget = 12, lotBayW = 2.8f,
            northRow = new[] { "Atap_House", "Atap_House", "Atap_House" },
            southRow = new[] { "Atap_House", "Atap_House", "Atap_House", "Atap_House" },
            look = new K.Look
            {
                skyAmbient = new Color(0.7f, 0.78f, 0.74f), equatorAmbient = new Color(0.58f, 0.58f, 0.5f), groundAmbient = new Color(0.34f, 0.32f, 0.26f),
                fog = new Color(0.8f, 0.86f, 0.8f), sun = new Color(1f, 0.95f, 0.82f), fogDensity = 0.0052f, sunIntensity = 1.3f, sunEuler = new Vector3(55f, -40f, 0f),
                paveTint = new Color(0.82f, 0.78f, 0.7f),
            },
        };

        static MalaysiaArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Mosque"), new Vector3(-10f, 0f, -48f), 0f);
                put(g, K.P(Plan, "Twin_Towers"), new Vector3(-30f, 0f, 330f), 180f);
                put(g, K.P(Plan, "Twin_Towers"), new Vector3(150f, 0f, -300f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { Mission1, Mission2, Mission3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Atap_House", source = "Malaysia/my_atap_house.glb" },
                new F.Spec { combine = true, name = "Mosque", source = "Malaysia/my_blue_mosque.glb", fit = F.Fit.Width, size = 50f },
                new F.Spec { combine = true, name = "Twin_Towers", source = "Malaysia/my_petronas_towers.glb", fit = F.Fit.Height, size = 380f, collider = F.ColliderKind.None },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Malaysia Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "MY");

        private static LevelData Mission1(GameObject env)
        {
            var p = Plan; var props = new List<PropPlacement>();
            var (slot, yaw) = K.AvenueSlot(p, props, 34f);
            var rules = K.Rules(8f, 1.2f);
            return BatchTwo.Write(p, env, "MY1", 1, "Riverside Parallel", MissionType.ParallelParking, props,
                "Keep left along the riverside road and parallel park at the left kerb between the two cars. Finish within 0.45 m of the kerb; three kerb strikes fail.",
                K.AvenueSpawn(p, -40f), 90f, 150f, 2, (70f, 0), (105f, 1), (220, 50), new MissionObjective[]
                {
                    new ParallelParkObjective("Parallel park at the kerb", slot, yaw, new Vector2(2.4f, 6.4f), TrafficSide.KerbSide(p.road), 0.45f, 2, rules),
                });
        }

        private static LevelData Mission2(GameObject env)
        {
            var p = Plan; var props = new List<PropPlacement>();
            var rules = K.Rules(8f, 1.2f);
            return BatchTwo.Write(p, env, "MY2", 2, "Kampung Reverse", MissionType.NarrowRoute, props,
                "Drive past the kampung lane, then reverse all the way down it — between the houses, barely wider than the car — and into the space at the end. Two scrapes allowed.",
                K.AvenueSpawn(p, 60f), 90f, 190f, 3, (90f, 0), (130f, 1), (240, 55), new MissionObjective[]
                {
                    new NarrowAlleyObjective("Reverse down the lane", new Vector3(K.NarrowX, 0f, K.Walk + 1f), new Vector3(K.NarrowX, 0f, K.NarrowEndZ - 2f), K.NarrowHalf * 2f, maxScrapes: 2, reverseThrough: true, goodClearance: 0.3f),
                    new ReverseParkObjective("Into the space at the end", new Vector3(K.NarrowX, 0f, K.NarrowEndZ - 3.1f), 0f, new Vector2(K.NarrowHalf * 2f - 0.1f, 6f), 0.8f, rules),
                }, ScoreMode.Quality, (0.8f, 0.5f));
        }

        private static LevelData Mission3(GameObject env)
        {
            var p = Plan; var props = new List<PropPlacement>(); K.FillLot(p, props);
            var rules = K.Rules(6f, 1.4f);
            return BatchTwo.Write(p, env, "MY3", 3, "Mosque Forecourt", MissionType.PrecisionParking, props,
                "Friday prayers: the forecourt is packed. Turn in, find the one free bay and park exactly in the middle. One knock costs the third star; two fail.",
                K.AvenueSpawn(p, -40f), 90f, 150f, 1, (75f, 0), (105f, 1), (260, 60), new MissionObjective[]
                {
                    new CheckpointObjective("Into the forecourt", new Vector3(18f, 0f, 16f), 0f, new Vector2(8f, 4f), "TURN INTO THE FORECOURT"),
                    new PrecisionParkObjective("Park exactly", new Vector3(K.LotBayX(p, p.lotTarget), 0f, K.LotRowZ), 0f, new Vector2(p.lotBayW - 0.1f, K.LotBayD), ParkDirection.NoseIn, 0.3f, 5f, 0.55f, rules),
                }, ScoreMode.Quality, (0.88f, 0.65f));
        }
    }
}
