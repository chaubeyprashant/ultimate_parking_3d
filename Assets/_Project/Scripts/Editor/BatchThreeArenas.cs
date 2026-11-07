using System.Collections.Generic;
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
    /// <summary>Shared mission recipes for the batch-3 arenas (all on <see cref="CityKit"/>), built from mechanics that already exist.</summary>
    public static class BatchThree
    {
        public static void RenderAll() { IndonesiaArenaBuilder.RenderShots(); SaudiArabiaArenaBuilder.RenderShots(); FranceArenaBuilder.RenderShots(); GermanyArenaBuilder.RenderShots(); ItalyArenaBuilder.RenderShots(); }
        public static void RenderAll4() { SpainArenaBuilder.RenderShots(); RussiaArenaBuilder.RenderShots(); NetherlandsArenaBuilder.RenderShots(); SwitzerlandArenaBuilder.RenderShots(); GreeceArenaBuilder.RenderShots(); }
        public static void RenderAll5() { BrazilArenaBuilder.RenderShots(); CanadaArenaBuilder.RenderShots(); MexicoArenaBuilder.RenderShots(); ArgentinaArenaBuilder.RenderShots(); }
        public static void RenderAll6() { EgyptArenaBuilder.RenderShots(); AustraliaArenaBuilder.RenderShots(); TurkeyArenaBuilder.RenderShots(); }
        public static void RenderAll7() { NigeriaArenaBuilder.RenderShots(); SouthAfricaArenaBuilder.RenderShots(); NewZealandArenaBuilder.RenderShots(); }
        static Vector3 Bay(K.Plan p) => new Vector3(K.LotBayX(p, p.lotTarget), 0f, K.LotRowZ);
        static Vector2 BaySize(K.Plan p) => new Vector2(p.lotBayW - 0.1f, K.LotBayD);
        static CheckpointObjective IntoLot(string label) => new CheckpointObjective("Turn into the car park", new Vector3(18f, 0f, 16f), 0f, new Vector2(8f, 4f), label);

        public static LevelData Parallel(K.Plan p, GameObject env, string id, int n, string name, string brief, float time = 150f)
        {
            var props = new List<PropPlacement>(); var (slot, yaw) = K.AvenueSlot(p, props, 34f); var rules = K.Rules(8f, 1.2f);
            return BatchTwo.Write(p, env, id, n, name, MissionType.ParallelParking, props, brief, K.AvenueSpawn(p, -40f), 90f, time, 2, (70f, 0), (105f, 1), (220, 50), new MissionObjective[]
            {
                new ParallelParkObjective("Parallel park at the kerb", slot, yaw, new Vector2(2.4f, 6.4f), TrafficSide.KerbSide(p.road), 0.45f, 2, rules),
            });
        }

        public static LevelData Precision(K.Plan p, GameObject env, string id, int n, string name, string brief, float tol = 0.3f)
        {
            var props = new List<PropPlacement>(); K.FillLot(p, props); var rules = K.Rules(6f, 1.4f);
            return BatchTwo.Write(p, env, id, n, name, MissionType.PrecisionParking, props, brief, K.AvenueSpawn(p, -40f), 90f, 150f, 1, (75f, 0), (105f, 1), (260, 60), new MissionObjective[]
            {
                IntoLot("TURN INTO THE CAR PARK"),
                new PrecisionParkObjective("Park exactly", Bay(p), 0f, BaySize(p), ParkDirection.NoseIn, tol, 5f, 0.55f, rules),
            }, ScoreMode.Quality, (0.88f, 0.65f));
        }

        public static LevelData Reverse(K.Plan p, GameObject env, string id, int n, string name, string brief)
        {
            var props = new List<PropPlacement>(); K.FillLot(p, props); var rules = K.Rules(8f, 1.2f);
            return BatchTwo.Write(p, env, id, n, name, MissionType.ReverseParking, props, brief, K.AvenueSpawn(p, -40f), 90f, 140f, 2, (65f, 0), (95f, 1), (210, 50), new MissionObjective[]
            {
                IntoLot("TURN INTO THE CAR PARK"),
                new ReverseParkObjective("Reverse into the free bay", Bay(p), 0f, BaySize(p), 0.7f, rules),
            });
        }

        public static LevelData Garage(K.Plan p, GameObject env, string id, int n, string name, string brief, int bay = 2, bool gate = false, string gateName = null)
        {
            var props = new List<PropPlacement>(); K.FillGarage(p, props, bay, 1); var rules = K.Rules(8f, 1.2f);
            var objs = new List<MissionObjective>();
            if (gate)
            {
                W.Place(props, K.P(p, gateName), new Vector3(K.RampX, 0f, -9f), 0f);
                objs.Add(new BarrierGateObjective("Through the ticket gate", id.ToLower() + "_gate", new Vector3(K.RampX, 0f, -12.5f), 180f, new Vector2(6f, 4f), maxBarrierHits: 1));
            }
            objs.Add(K.GarageRamp());
            objs.Add(new FloorParkObjective("Park on level -1", K.HallBay(bay, 1), 90f, new Vector2(2.9f, 5.5f), ParkDirection.NoseIn, "LEVEL -1", rules));
            return BatchTwo.Write(p, env, id, n, name, MissionType.MultiStoreyRamp, props, brief, K.AvenueSpawnWest(p, 80f), 270f, 180f, 2, (90f, 0), (130f, 1), (240, 55), objs.ToArray(), camH: 2.2f, camD: 5.5f);
        }

        public static LevelData Alley(K.Plan p, GameObject env, string id, int n, string name, string brief, bool reverse, string obstacle = null)
        {
            var props = new List<PropPlacement>();
            if (obstacle != null) W.Place(props, RealPropFactory.Load(obstacle), new Vector3(K.NarrowX + (p.road == RoadDirection.LeftHand ? -1.1f : 1.1f), 0f, 28f), 0f);
            var rules = K.Rules(8f, 1.0f);
            var objs = new List<MissionObjective>
            {
                new NarrowAlleyObjective(reverse ? "Reverse down the lane" : "Down the lane", new Vector3(K.NarrowX, 0f, K.Walk + 1f), new Vector3(K.NarrowX, 0f, reverse ? K.NarrowEndZ - 2f : 44f), K.NarrowHalf * 2f, maxScrapes: 2, reverseThrough: reverse, goodClearance: 0.3f),
            };
            if (reverse) objs.Add(new ReverseParkObjective("Into the space at the end", new Vector3(K.NarrowX, 0f, K.NarrowEndZ - 3.1f), 0f, new Vector2(K.NarrowHalf * 2f - 0.1f, 6f), 0.8f, rules));
            else objs.Add(new ParkObjective("Park at the end", new Vector3(K.NarrowX, 0f, 50f), 0f, new Vector2(2.8f, 5.5f), ParkDirection.NoseIn, rules));
            return BatchTwo.Write(p, env, id, n, name, MissionType.NarrowRoute, props, brief, K.AvenueSpawn(p, 70f), 90f, 190f, 3, (90f, 0), (130f, 1), (240, 55), objs.ToArray(), ScoreMode.Quality, (0.8f, 0.5f));
        }

        public static LevelData Timed(K.Plan p, GameObject env, string id, int n, string name, string brief, string gateName, float seconds = 30f)
        {
            var props = new List<PropPlacement>(); K.FillLot(p, props);
            W.Place(props, K.P(p, gateName), new Vector3(18f, 0f, 15f), 0f); var rules = K.Rules(9f, 1.0f);
            return BatchTwo.Write(p, env, id, n, name, MissionType.TimedParking, props, brief, K.AvenueSpawn(p, -35f), 90f, 170f, 3, (70f, 0), (100f, 1), (240, 55), new MissionObjective[]
            {
                new BarrierGateObjective("Through the gate", id.ToLower() + "_gate", new Vector3(18f, 0f, 21f), 0f, new Vector2(10f, 4f), maxBarrierHits: 1),
                new TimedParkObjective("Beat the clock", Bay(p), 0f, BaySize(p), ParkDirection.NoseIn, seconds, 3f, rules),
            }, ScoreMode.Quality, (0.45f, 0.2f));
        }

        public static LevelData Valet(K.Plan p, GameObject env, string id, int n, string name, string brief)
        {
            var props = new List<PropPlacement>(); K.FillLot(p, props); var rules = K.Rules(8f, 1.0f);
            return BatchTwo.Write(p, env, id, n, name, MissionType.ValetParking, props, brief, K.AvenueSpawn(p, -35f), 90f, 160f, 2, (80f, 0), (110f, 1), (250, 60), new MissionObjective[]
            {
                new ValetParkObjective("Valet the guest's car", new Vector3(18f, 0f, 17f), 0f, Bay(p), 0f, BaySize(p), ParkDirection.NoseIn, 18f, rules),
            }, ScoreMode.Quality, (0.85f, 0.6f));
        }

        public static LevelData Exam(K.Plan p, GameObject env, string id, int n, string name, string brief)
        {
            var props = new List<PropPlacement>(); K.FillLot(p, props);
            var (slot, yaw) = K.AvenueSlot(p, props, -58f); var rules = K.Rules(8f, 1.2f);
            return BatchTwo.Write(p, env, id, n, name, MissionType.ParkingExam, props, brief, K.AvenueSpawn(p, -112f), 90f, 260f, 3, (150f, 0), (200f, 2), (320, 80), new MissionObjective[]
            {
                new CheckpointObjective("Start line", K.AvenueSpawn(p, -98f), 90f, new Vector2(4f, 8f), "CROSS THE START LINE"),
                new ParallelParkObjective("Parallel park at the kerb", slot, yaw, new Vector2(2.4f, 6.4f), TrafficSide.KerbSide(p.road), 0.45f, 2, rules),
                IntoLot("TO THE CAR PARK"),
                new PrecisionParkObjective("Precision bay", Bay(p), 0f, BaySize(p), ParkDirection.NoseIn, 0.35f, 6f, 0.5f, rules),
            }, ScoreMode.Quality, (0.8f, 0.5f));
        }
    }

    // ==========================================================================================  INDONESIA
    public static class IndonesiaArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Indonesia", id = "id", envName = "Env_Jakarta", road = RoadDirection.LeftHand, lotBays = 20, lotTarget = 12, lotBayW = 2.7f,
            northRow = new[] { "Shophouse", "House", "Residence", "Shophouse" },
            southRow = new[] { "House", "Shophouse", "House", "Residence" },
            look = new K.Look
            {
                skyAmbient = new Color(0.74f, 0.78f, 0.78f), equatorAmbient = new Color(0.6f, 0.58f, 0.52f), groundAmbient = new Color(0.36f, 0.33f, 0.28f),
                fog = new Color(0.84f, 0.84f, 0.78f), sun = new Color(1f, 0.93f, 0.8f), fogDensity = 0.005f, sunIntensity = 1.3f, sunEuler = new Vector3(60f, -25f, 0f),
                paveTint = new Color(0.84f, 0.8f, 0.72f),
            },
        };

        static IndonesiaArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Monas"), new Vector3(-20f, 0f, 190f), 0f);
                foreach (float x in new[] { -85f, 20f, 85f }) put(g, K.P(Plan, "Street_Stall"), new Vector3(x, 0f, K.RoadHalf + 1.6f), 180f);
                put(g, K.P(Plan, "Angkot"), new Vector3(-30f, 0f, K.RoadHalf - 1.4f), 90f);
                put(g, K.P(Plan, "Angkot"), new Vector3(52f, 0f, -K.RoadHalf + 1.4f), 270f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Shophouse", source = "Indonesia/id_ea79a679.glb", fit = F.Fit.Width, size = 13f },
                new F.Spec { combine = true, name = "House", source = "Indonesia/id_903e7b4e.glb", fit = F.Fit.Width, size = 10f },
                new F.Spec { combine = true, name = "Residence", source = "Indonesia/id_99f42491.glb", fit = F.Fit.Width, size = 22f },
                new F.Spec { combine = true, name = "Monas", source = "Indonesia/id_c4fbf756.glb", fit = F.Fit.Height, size = 110f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Street_Stall", source = "Indonesia/id_069fc2e4.glb", fit = F.Fit.Height, size = 2.2f },
                new F.Spec { combine = true, name = "Angkot", source = "Indonesia/id_a406663c.glb", fit = F.Fit.Length, size = 4.3f, colliderHeightCap = 1.8f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Sliding", "id3_gate", 12f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Indonesia Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "ID");

        static LevelData M1(GameObject env) => BatchThree.Parallel(Plan, env, "ID1", 1, "Angkot Gap",
            "Jakarta's angkots stop anywhere. Keep left along the avenue and parallel park at the left kerb between the two parked cars, without touching either.");
        static LevelData M2(GameObject env) => BatchThree.Alley(Plan, env, "ID2", 2, "Gang Squeeze",
            "Turn left into the gang — a lane barely wider than the car — squeeze past the cones and park at the end.", false, "Cones_Cluster");
        static LevelData M3(GameObject env) => BatchThree.Timed(Plan, env, "ID3", 3, "Mall Rush",
            "The mall car park's gate opens on a timer. Wait for it, drive through, then take the free bay within 30 seconds; every knock costs 3.", "Gate_Sliding");
    }

    // ==========================================================================================  SAUDI ARABIA
    public static class SaudiArabiaArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "SaudiArabia", id = "sa", envName = "Env_Riyadh", road = RoadDirection.RightHand, lotBays = 22, lotTarget = 13, lotBayW = 3.0f,
            northRow = new[] { "Villa", "Arab_House", "Villa" },
            southRow = new[] { "Arab_House", "Villa", "Big_House" },
            look = new K.Look
            {
                skyAmbient = new Color(0.82f, 0.78f, 0.68f), equatorAmbient = new Color(0.72f, 0.62f, 0.48f), groundAmbient = new Color(0.46f, 0.38f, 0.28f),
                fog = new Color(0.92f, 0.82f, 0.66f), sun = new Color(1f, 0.92f, 0.76f), fogDensity = 0.0048f, sunIntensity = 1.5f, sunEuler = new Vector3(64f, 10f, 0f),
                paveTint = new Color(0.9f, 0.82f, 0.68f),
            },
        };

        static SaudiArabiaArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Faisaliyah"), new Vector3(-60f, 0f, 220f), 180f);
                put(g, K.P(Plan, "NCCI_Towers"), new Vector3(80f, 0f, 250f), 180f);
                put(g, K.P(Plan, "Mosque"), new Vector3(-10f, 0f, -52f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Villa", source = "SaudiArabia/sa_dfd9dd93.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Arab_House", source = "SaudiArabia/sa_da4172d6.glb", fit = F.Fit.Width, size = 10f },
                new F.Spec { combine = true, name = "Big_House", source = "SaudiArabia/sa_29c8806e.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Faisaliyah", source = "SaudiArabia/sa_b4b8c762.glb", fit = F.Fit.Height, size = 267f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "NCCI_Towers", source = "SaudiArabia/sa_81712b16.glb", fit = F.Fit.Height, size = 160f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Mosque", source = "SaudiArabia/sa_f18722b6.glb", fit = F.Fit.Width, size = 45f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Sliding", "sa3_gate", 12f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Saudi Arabia Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "SA");

        static LevelData M1(GameObject env) => BatchThree.Precision(Plan, env, "SA1", 1, "Wide Bay",
            "Riyadh's wide family-SUV bays leave no excuse. Turn into the car park and park exactly in the middle of the free bay; accuracy earns the stars.");
        static LevelData M2(GameObject env) => BatchThree.Valet(Plan, env, "SA2", 2, "Hotel Valet",
            "Valet duty at the hotel: pull up at the stand, take the keys, then park the guest's car smoothly. Keep under 18 km/h and don't knock anything.");
        static LevelData M3(GameObject env) => BatchThree.Timed(Plan, env, "SA3", 3, "Mall Clock",
            "The mall car park's gate opens on a timer. Wait for it, drive through, then take the free bay within 30 seconds; every knock costs 3.", "Gate_Sliding");
    }

    // ==========================================================================================  FRANCE
    public static class FranceArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "France", id = "fr", envName = "Env_Paris", road = RoadDirection.RightHand, lotBays = 20, lotTarget = 11, lotBayW = 2.5f,
            northRow = new[] { "Commissariat", "Commissariat", "Commissariat" },
            southRow = new[] { "Commissariat", "Commissariat", "Commissariat" },
            look = new K.Look
            {
                skyAmbient = new Color(0.68f, 0.72f, 0.8f), equatorAmbient = new Color(0.55f, 0.55f, 0.58f), groundAmbient = new Color(0.32f, 0.32f, 0.34f),
                fog = new Color(0.78f, 0.8f, 0.86f), sun = new Color(1f, 0.95f, 0.88f), fogDensity = 0.0055f, sunIntensity = 1.15f, sunEuler = new Vector3(44f, 30f, 0f),
            },
        };

        static FranceArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Rotonde"), new Vector3(-10f, 0f, -70f), 0f);
                put(g, K.P(Plan, "Halle"), new Vector3(60f, 0f, 150f), 180f);
                foreach (float x in new[] { -70f, 5f, 90f }) put(g, K.P(Plan, "Traffic_Light"), new Vector3(x, 0f, K.RoadHalf + 1f), 180f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Commissariat", source = "France/fr_63146921.glb", fit = F.Fit.Width, size = 20f },
                new F.Spec { combine = true, name = "Rotonde", source = "France/fr_567c327f.glb", fit = F.Fit.Width, size = 34f },
                new F.Spec { combine = true, name = "Halle", source = "France/fr_856b6dcd.glb", fit = F.Fit.Width, size = 70f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Traffic_Light", source = "France/fr_95a52360.glb", fit = F.Fit.Height, size = 5f, collider = F.ColliderKind.None },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build France Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "FR");

        static LevelData M1(GameObject env) => BatchThree.Parallel(Plan, env, "FR1", 1, "Rue Parallèle",
            "The classic Paris créneau: keep right along the avenue and parallel park at the right kerb between the two cars. Finish within 0.45 m of the kerb.");
        static LevelData M2(GameObject env) => BatchThree.Garage(Plan, env, "FR2", 2, "Underground Ramp",
            "Paris parks underground. Turn into the car park, take the ramp down to level -1 and park in the free bay on the right.", bay: 2);
        static LevelData M3(GameObject env) => BatchThree.Precision(Plan, env, "FR3", 3, "Place Précise",
            "A tight 2.5 m bay between two cars. Turn in and park dead centre; accuracy earns the stars, one knock costs the third.", 0.25f);
    }

    // ==========================================================================================  GERMANY
    public static class GermanyArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Germany", id = "de", envName = "Env_Berlin", road = RoadDirection.RightHand, lotBays = 22, lotTarget = 13, lotBayW = 2.5f,
            northRow = new[] { "Altbau", "Gruenderzeit", "Altbau" },
            southRow = new[] { "Gruenderzeit", "Altbau", "Gruenderzeit" },
            look = new K.Look
            {
                skyAmbient = new Color(0.66f, 0.7f, 0.76f), equatorAmbient = new Color(0.52f, 0.52f, 0.52f), groundAmbient = new Color(0.3f, 0.3f, 0.3f),
                fog = new Color(0.74f, 0.77f, 0.82f), sun = new Color(1f, 0.96f, 0.9f), fogDensity = 0.0055f, sunIntensity = 1.1f, sunEuler = new Vector3(42f, -30f, 0f),
            },
        };

        static GermanyArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Reichstag"), new Vector3(-20f, 0f, 150f), 180f);
                put(g, K.P(Plan, "Old_Town"), new Vector3(100f, 0f, -90f), 0f);
                foreach (float x in new[] { -80f, 0f, 70f }) put(g, K.P(Plan, "Street_Props"), new Vector3(x, 0f, K.RoadHalf + 1.5f), 180f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Altbau", source = "Germany/de_a5334d96.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Gruenderzeit", source = "Germany/de_ee64e1e2.glb", fit = F.Fit.Width, size = 18f },
                new F.Spec { combine = true, name = "Reichstag", source = "Germany/de_850b9cdb.glb", fit = F.Fit.Width, size = 90f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Old_Town", source = "Germany/de_af819339.glb", yaw = 90f, fit = F.Fit.Width, size = 50f },
                new F.Spec { combine = true, name = "Street_Props", source = "Germany/de_b45e3294.glb", fit = F.Fit.Width, size = 5f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Ticket", "de2_gate", 6.8f, 3f, 5f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Germany Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "DE");

        static LevelData M1(GameObject env) => BatchThree.Exam(Plan, env, "DE1", 1, "Fahrprüfung",
            "The driving test: cross the start line, parallel park at the kerb on the right, then drive to the car park and park precisely in the marked bay.");
        static LevelData M2(GameObject env) => BatchThree.Garage(Plan, env, "DE2", 2, "Parkhaus",
            "The Parkhaus ticket gate opens on a timer. Wait for it, drive through and down the ramp to level -1, and park in the free bay.", bay: 3, gate: true, gateName: "Gate_Ticket");
        static LevelData M3(GameObject env) => BatchThree.Reverse(Plan, env, "DE3", 3, "Rückwärts Einparken",
            "Turn into the car park and reverse into the free bay between the two cars, finishing within 0.7 m of the back wall.");
    }

    // ==========================================================================================  ITALY
    public static class ItalyArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Italy", id = "it", envName = "Env_Rome", road = RoadDirection.RightHand, lotBays = 20, lotTarget = 12, lotBayW = 2.5f,
            northRow = new[] { "Palazzo_A", "Arcade", "Palazzo_A" },
            southRow = new[] { "Arcade", "Palazzo_A", "Arcade" },
            look = new K.Look
            {
                skyAmbient = new Color(0.78f, 0.76f, 0.7f), equatorAmbient = new Color(0.64f, 0.58f, 0.5f), groundAmbient = new Color(0.4f, 0.34f, 0.28f),
                fog = new Color(0.88f, 0.84f, 0.76f), sun = new Color(1f, 0.92f, 0.78f), fogDensity = 0.0045f, sunIntensity = 1.35f, sunEuler = new Vector3(52f, 40f, 0f),
                paveTint = new Color(0.86f, 0.82f, 0.74f),
            },
        };

        static ItalyArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Colosseum"), new Vector3(0f, 0f, 230f), 180f);
                put(g, K.P(Plan, "Temple"), new Vector3(-70f, 0f, -75f), 0f);
                put(g, K.P(Plan, "Baths"), new Vector3(70f, 0f, -90f), 0f);
                foreach (float x in new[] { -75f, 10f, 80f }) put(g, K.P(Plan, "Scooter"), new Vector3(x, 0f, K.RoadHalf + 1.4f), 180f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Palazzo_A", source = "Italy/it_0304c661.glb", pick = BatchTwo.Exact("Object_17"), fit = F.Fit.Width, size = 26f },
                new F.Spec { combine = true, name = "Arcade", source = "Italy/it_0304c661.glb", pick = BatchTwo.Exact("Object_16"), yaw = 90f, fit = F.Fit.Width, size = 30f },
                new F.Spec { combine = true, name = "Colosseum", source = "Italy/it_554186cb.glb", fit = F.Fit.Width, size = 150f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Temple", source = "Italy/it_f3e9bae1.glb", fit = F.Fit.Width, size = 36f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Baths", source = "Italy/it_ecc5e51b.glb", strip = new[] { "Statue" }, fit = F.Fit.Width, size = 40f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Scooter", source = "Italy/it_db9f2f8c.glb", fit = F.Fit.Length, size = 1.9f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Sliding", "it3_gate", 12f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Italy Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "IT");

        static LevelData M1(GameObject env) => BatchThree.Alley(Plan, env, "IT1", 1, "Vicolo Stretto",
            "Turn into the vicolo — a medieval lane barely wider than the car — squeeze past the cones and park at the end.", false, "Cones_Cluster");
        static LevelData M2(GameObject env) => BatchThree.Garage(Plan, env, "IT2", 2, "Colle Garage",
            "Rome's hills hide garages under the palazzi. Turn in, take the ramp down to level -1 and park in the free bay.", bay: 2);
        static LevelData M3(GameObject env) => BatchThree.Timed(Plan, env, "IT3", 3, "Traffico Romano",
            "The ZTL gate opens on a timer. Wait for it, drive through, then take the free bay within 30 seconds; every knock costs 3.", "Gate_Sliding");
    }

    // ==========================================================================================  RUSSIA
    public static class RussiaArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Russia", id = "ru", envName = "Env_Moscow", road = RoadDirection.RightHand, lotBays = 20, lotTarget = 12, lotBayW = 2.5f,
            northRow = new[] { "Moscow_Old", "Panel_Slab", "Khrushchevka", "Panel_Slab" },
            southRow = new[] { "Panel_Slab", "Moscow_Old", "Khrushchevka" },
            look = new K.Look
            {
                skyAmbient = new Color(0.7f, 0.72f, 0.76f), equatorAmbient = new Color(0.55f, 0.55f, 0.58f), groundAmbient = new Color(0.32f, 0.32f, 0.34f),
                fog = new Color(0.8f, 0.82f, 0.86f), sun = new Color(0.95f, 0.95f, 1.0f), fogDensity = 0.0062f, sunIntensity = 1.0f, sunEuler = new Vector3(38f, 25f, 0f),
            },
        };

        static RussiaArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Tall_Block"), new Vector3(-40f, 0f, 200f), 180f);
                put(g, K.P(Plan, "Tall_Block"), new Vector3(70f, 0f, 230f), 180f);
                put(g, K.P(Plan, "School"), new Vector3(-10f, 0f, -60f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Moscow_Old", source = "Russia/ru_aa9e9bcb.glb", fit = F.Fit.Width, size = 18f },
                new F.Spec { combine = true, name = "Panel_Slab", source = "Russia/ru_a3508b75.glb", fit = F.Fit.Width, size = 30f, yaw = 90f },
                new F.Spec { combine = true, name = "Khrushchevka", source = "Russia/ru_4c759da8.glb", fit = F.Fit.Width, size = 24f, yaw = 90f },
                new F.Spec { combine = true, name = "Tall_Block", source = "Russia/ru_05229ac1.glb", fit = F.Fit.Height, size = 40f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "School", source = "Russia/ru_d34ac09a.glb", fit = F.Fit.Width, size = 44f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Russia Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "RU");

        static LevelData M1(GameObject env) => BatchThree.Alley(Plan, env, "RU1", 1, "Dvor Squeeze",
            "Turn into the narrow courtyard lane between the panel blocks, squeeze past the cones and park at the end.", false, "Cones_Cluster");
        static LevelData M2(GameObject env) => BatchThree.Garage(Plan, env, "RU2", 2, "Metro Garage",
            "Under the district's blocks: turn in, take the ramp down to level -1 and park in the free bay.", bay: 2);
        static LevelData M3(GameObject env) => BatchThree.Exam(Plan, env, "RU3", 3, "Driving Exam",
            "The state driving exam: cross the start line, parallel park at the kerb, then drive to the car park and park precisely in the bay.");
    }

    // ==========================================================================================  GREECE
    public static class GreeceArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Greece", id = "gr", envName = "Env_Athens", road = RoadDirection.RightHand, lotBays = 20, lotTarget = 12, lotBayW = 2.7f,
            northRow = new[] { "Old_House", "Old_House", "Old_House", "Cyclades" },
            southRow = new[] { "Old_House", "Old_House", "Old_House" },
            look = new K.Look
            {
                skyAmbient = new Color(0.8f, 0.8f, 0.76f), equatorAmbient = new Color(0.68f, 0.62f, 0.52f), groundAmbient = new Color(0.42f, 0.36f, 0.28f),
                fog = new Color(0.88f, 0.86f, 0.8f), sun = new Color(1.0f, 0.94f, 0.8f), fogDensity = 0.0042f, sunIntensity = 1.5f, sunEuler = new Vector3(62f, 15f, 0f),
                paveTint = new Color(0.9f, 0.86f, 0.78f),
            },
        };

        static GreeceArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Temple"), new Vector3(-30f, 0f, 150f), 180f);
                put(g, K.P(Plan, "Cyclades"), new Vector3(70f, 0f, -70f), 0f);
                put(g, K.P(Plan, "Kiosk"), new Vector3(-20f, 0f, -60f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Old_House", source = "Greece/gr_4a906147.glb", fit = F.Fit.Width, size = 16f },
                new F.Spec { combine = true, name = "Cyclades", source = "Greece/gr_b2a1e35e.glb", fit = F.Fit.Width, size = 22f },
                new F.Spec { combine = true, name = "Temple", source = "Greece/gr_1cb5bcd8.glb", fit = F.Fit.Width, size = 46f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Kiosk", source = "Greece/gr_68f499bf.glb", fit = F.Fit.Width, size = 14f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Sliding", "gr3_gate", 12f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Greece Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "GR");

        static LevelData M1(GameObject env) => BatchThree.Alley(Plan, env, "GR1", 1, "Plaka Lane",
            "Turn into the Plaka lane — a stone alley barely wider than the car — squeeze past the cones and park at the end.", false, "Cones_Cluster");
        static LevelData M2(GameObject env) => BatchThree.Reverse(Plan, env, "GR2", 2, "Harbour Reverse",
            "Turn into the harbour car park and reverse into the free bay, finishing within 0.7 m of the back wall.");
        static LevelData M3(GameObject env) => BatchThree.Timed(Plan, env, "GR3", 3, "Agora Rush",
            "The agora car park's gate opens on a timer. Wait for it, drive through, then take the free bay within 30 seconds; every knock costs 3.", "Gate_Sliding");
    }

    // ==========================================================================================  NETHERLANDS
    public static class NetherlandsArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Netherlands", id = "nl", envName = "Env_Amsterdam", road = RoadDirection.RightHand, lotBays = 20, lotTarget = 12, lotBayW = 2.5f,
            northRow = new[] { "Stylized_House", "Rowhome", "Stylized_House" },
            southRow = new[] { "Rowhome", "Stylized_House", "Stylized_House" },
            look = new K.Look
            {
                skyAmbient = new Color(0.7f, 0.74f, 0.8f), equatorAmbient = new Color(0.56f, 0.56f, 0.58f), groundAmbient = new Color(0.32f, 0.32f, 0.34f),
                fog = new Color(0.78f, 0.82f, 0.88f), sun = new Color(0.96f, 0.95f, 0.92f), fogDensity = 0.0058f, sunIntensity = 1.05f, sunEuler = new Vector3(40f, -20f, 0f),
            },
        };

        static NetherlandsArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Windmill"), new Vector3(-40f, 0f, 150f), 180f);
                put(g, K.P(Plan, "Windmill"), new Vector3(80f, 0f, -140f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Stylized_House", source = "Netherlands/nl_c523ef1f.glb", fit = F.Fit.Width, size = 6f },
                new F.Spec { combine = true, name = "Rowhome", source = "Netherlands/nl_d031d063.glb", fit = F.Fit.Width, size = 7f },
                new F.Spec { combine = true, name = "Windmill", source = "Netherlands/nl_07307053.glb", fit = F.Fit.Width, size = 50f, collider = F.ColliderKind.None },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Netherlands Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "NL");

        static LevelData M1(GameObject env) => BatchThree.Parallel(Plan, env, "NL1", 1, "Gracht Parallel",
            "Along the canal: keep right and parallel park at the right kerb between the two cars, without touching either.");
        static LevelData M2(GameObject env) => BatchThree.Alley(Plan, env, "NL2", 2, "Canal Alley",
            "Reverse all the way down the narrow canal-side alley, between the houses, and into the space at the end.", true);
        static LevelData M3(GameObject env) => BatchThree.Valet(Plan, env, "NL3", 3, "Hotel Valet",
            "Valet duty at the canal hotel: pull up at the stand, take the keys, then park the guest's car smoothly. Keep under 18 km/h.");
    }

    // ==========================================================================================  SPAIN
    public static class SpainArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Spain", id = "es", envName = "Env_Madrid", road = RoadDirection.RightHand, lotBays = 22, lotTarget = 13, lotBayW = 2.6f,
            northRow = new[] { "Edificio", "Edificio", "Edificio" },
            southRow = new[] { "Edificio", "Edificio", "Edificio", "Edificio" },
            look = new K.Look
            {
                skyAmbient = new Color(0.82f, 0.78f, 0.7f), equatorAmbient = new Color(0.72f, 0.62f, 0.5f), groundAmbient = new Color(0.46f, 0.38f, 0.3f),
                fog = new Color(0.92f, 0.84f, 0.7f), sun = new Color(1.0f, 0.92f, 0.76f), fogDensity = 0.0045f, sunIntensity = 1.55f, sunEuler = new Vector3(66f, 30f, 0f),
                paveTint = new Color(0.9f, 0.84f, 0.74f),
            },
        };

        static SpainArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Colon_Tower"), new Vector3(-60f, 0f, 220f), 180f);
                put(g, K.P(Plan, "Picasso_Tower"), new Vector3(80f, 0f, 260f), 180f);
                put(g, K.P(Plan, "Castle"), new Vector3(-20f, 0f, -95f), 0f);
                put(g, K.P(Plan, "Barcelona_Block"), new Vector3(-85f, 0f, -60f), 0f);
                put(g, K.P(Plan, "Barcelona_Block"), new Vector3(90f, 0f, -70f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Edificio", source = "Germany/de_ee64e1e2.glb", fit = F.Fit.Width, size = 16f },
                new F.Spec { combine = true, name = "Colon_Tower", source = "Spain/es_1500c198.glb", fit = F.Fit.Height, size = 110f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Picasso_Tower", source = "Spain/es_f2779648.glb", fit = F.Fit.Height, size = 160f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Castle", source = "Spain/es_5dbe4dc7.glb", fit = F.Fit.Width, size = 56f },
                new F.Spec { combine = true, name = "Barcelona_Block", source = "Spain/es_14faa847.glb", fit = F.Fit.Width, size = 28f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Sliding", "es3_gate", 12f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Spain Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "ES");

        static LevelData M1(GameObject env) => BatchThree.Precision(Plan, env, "ES1", 1, "Plaza Bay",
            "Madrid's plaza car park: turn in and park dead centre in the free bay between the two cars; accuracy earns the stars.", 0.28f);
        static LevelData M2(GameObject env) => BatchThree.Parallel(Plan, env, "ES2", 2, "Calle Paralela",
            "Keep right along the avenue and parallel park at the right kerb between the two cars. Finish within 0.45 m of the kerb.");
        static LevelData M3(GameObject env) => BatchThree.Timed(Plan, env, "ES3", 3, "Siesta Rush",
            "The car park's gate opens on a timer. Wait for it, drive through, then take the free bay within 30 seconds; every knock costs 3.", "Gate_Sliding");
    }

    // ==========================================================================================  SWITZERLAND
    public static class SwitzerlandArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Switzerland", id = "ch", envName = "Env_Zurich", road = RoadDirection.RightHand, lotBays = 20, lotTarget = 12, lotBayW = 2.6f,
            northRow = new[] { "Chalet", "Cottage", "Villa", "Chalet" },
            southRow = new[] { "Cottage", "Chalet", "Villa" },
            look = new K.Look
            {
                skyAmbient = new Color(0.7f, 0.76f, 0.84f), equatorAmbient = new Color(0.55f, 0.58f, 0.6f), groundAmbient = new Color(0.34f, 0.34f, 0.34f),
                fog = new Color(0.8f, 0.86f, 0.92f), sun = new Color(1.0f, 0.97f, 0.92f), fogDensity = 0.0045f, sunIntensity = 1.2f, sunEuler = new Vector3(46f, -35f, 0f),
            },
        };

        static SwitzerlandArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Scheiben_Building"), new Vector3(-30f, 0f, -70f), 0f);
                put(g, K.P(Plan, "Villa"), new Vector3(70f, 0f, -60f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Chalet", source = "Switzerland/ch_2ac40506.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "Cottage", source = "Switzerland/ch_1f0e4563.glb", fit = F.Fit.Width, size = 8f },
                new F.Spec { combine = true, name = "Villa", source = "Switzerland/ch_527d2af9.glb", fit = F.Fit.Width, size = 16f, pick = BatchTwo.Exact("Park Villa Rieter building_3") },
                new F.Spec { combine = true, name = "Scheiben_Building", source = "Switzerland/ch_82e33f54.glb", fit = F.Fit.Width, size = 22f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Ticket", "ch1_gate", 6.8f, 3f, 5f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Switzerland Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "CH");

        static LevelData M1(GameObject env) => BatchThree.Garage(Plan, env, "CH1", 1, "Parkhaus Zürich",
            "The Parkhaus ticket gate opens on a timer. Wait for it, drive through and down the ramp to level -1, and park in the free bay.", bay: 3, gate: true, gateName: "Gate_Ticket");
        static LevelData M2(GameObject env) => BatchThree.Precision(Plan, env, "CH2", 2, "Chalet Bay",
            "Swiss precision: turn into the car park and park exactly in the middle of the free bay; accuracy earns the stars.", 0.25f);
        static LevelData M3(GameObject env) => BatchThree.Reverse(Plan, env, "CH3", 3, "Hill Reverse",
            "Turn into the car park and reverse into the free bay between the two cars, finishing within 0.7 m of the back wall.");
    }

    // ==========================================================================================  BRAZIL
    public static class BrazilArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Brazil", id = "br", envName = "Env_Rio", road = RoadDirection.RightHand, lotBays = 20, lotTarget = 12, lotBayW = 2.6f,
            northRow = new[] { "Slums", "Old_Houses", "Street_Block", "Slums" },
            southRow = new[] { "Old_Houses", "Slums", "Street_Block" },
            look = new K.Look
            {
                skyAmbient = new Color(0.78f, 0.8f, 0.8f), equatorAmbient = new Color(0.66f, 0.62f, 0.52f), groundAmbient = new Color(0.4f, 0.36f, 0.3f),
                fog = new Color(0.86f, 0.86f, 0.8f), sun = new Color(1.0f, 0.94f, 0.8f), fogDensity = 0.0048f, sunIntensity = 1.45f, sunEuler = new Vector3(62f, -20f, 0f),
                paveTint = new Color(0.88f, 0.82f, 0.74f),
            },
        };

        static BrazilArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Theatre"), new Vector3(-40f, 0f, 150f), 180f);
                put(g, K.P(Plan, "Cathedral"), new Vector3(90f, 0f, -110f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Slums", source = "Brazil/br_e2b6f532.glb", fit = F.Fit.Width, size = 16f },
                new F.Spec { combine = true, name = "Old_Houses", source = "Brazil/br_be4a54f6.glb", fit = F.Fit.Width, size = 26f },
                new F.Spec { combine = true, name = "Street_Block", source = "Brazil/br_9631e0ba.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Theatre", source = "Brazil/br_89a72b71.glb", fit = F.Fit.Width, size = 42f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Cathedral", source = "Brazil/br_a2e7c359.glb", fit = F.Fit.Width, size = 46f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Brazil Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "BR");

        static LevelData M1(GameObject env) => BatchThree.Alley(Plan, env, "BR1", 1, "Favela Reverse",
            "Reverse all the way down the narrow favela lane, between the houses, and into the space at the end.", true);
        static LevelData M2(GameObject env) => BatchThree.Parallel(Plan, env, "BR2", 2, "Rio Parallel",
            "Keep right along the avenue and parallel park at the right kerb between the two cars. Finish within 0.45 m of the kerb.");
        static LevelData M3(GameObject env) => BatchThree.Valet(Plan, env, "BR3", 3, "Hotel Valet",
            "Valet duty at the beach hotel: pull up at the stand, take the keys, then park the guest's car smoothly. Keep under 18 km/h.");
    }

    // ==========================================================================================  CANADA
    public static class CanadaArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Canada", id = "ca", envName = "Env_Toronto", road = RoadDirection.RightHand, lotBays = 22, lotTarget = 13, lotBayW = 2.8f,
            northRow = new[] { "Cabin_A", "Home", "Cabin_B" },
            southRow = new[] { "Home", "Cabin_B", "Cabin_A", "Home" },
            look = new K.Look
            {
                skyAmbient = new Color(0.72f, 0.76f, 0.84f), equatorAmbient = new Color(0.6f, 0.62f, 0.66f), groundAmbient = new Color(0.38f, 0.38f, 0.4f),
                fog = new Color(0.84f, 0.88f, 0.94f), sun = new Color(0.96f, 0.97f, 1.0f), fogDensity = 0.0058f, sunIntensity = 1.05f, sunEuler = new Vector3(34f, -30f, 0f),
                paveTint = new Color(0.82f, 0.82f, 0.84f),
            },
        };

        static CanadaArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Cabin_A"), new Vector3(90f, 0f, -60f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Cabin_A", source = "Canada/ca_94ed8fca.glb", fit = F.Fit.Width, size = 11f },
                new F.Spec { combine = true, name = "Cabin_B", source = "Canada/ca_21bf3df0.glb", fit = F.Fit.Width, size = 10f },
                new F.Spec { combine = true, name = "Home", source = "Canada/ca_328df599.glb", fit = F.Fit.Width, size = 18f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Canada Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "CA");

        static LevelData M1(GameObject env) => BatchThree.Garage(Plan, env, "CA1", 1, "Mall Garage",
            "Under the shopping centre: turn in, take the ramp down to level -1 and park in the free bay.", bay: 2);
        static LevelData M2(GameObject env) => BatchThree.Precision(Plan, env, "CA2", 2, "Cabin Bay",
            "Snowbanks narrow the car park. Turn in and park exactly in the middle of the free bay; accuracy earns the stars.", 0.28f);
        static LevelData M3(GameObject env) => BatchThree.Exam(Plan, env, "CA3", 3, "Road Test",
            "The road test: cross the start line, parallel park at the kerb, then drive to the car park and park precisely in the bay.");
    }

    // ==========================================================================================  MEXICO
    public static class MexicoArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Mexico", id = "mx", envName = "Env_MexicoCity", road = RoadDirection.RightHand, lotBays = 20, lotTarget = 12, lotBayW = 2.6f,
            northRow = new[] { "Apartment", "Apartment", "Apartment" },
            southRow = new[] { "Apartment", "Apartment", "Apartment", "Apartment" },
            look = new K.Look
            {
                skyAmbient = new Color(0.82f, 0.78f, 0.7f), equatorAmbient = new Color(0.7f, 0.6f, 0.48f), groundAmbient = new Color(0.44f, 0.36f, 0.28f),
                fog = new Color(0.92f, 0.84f, 0.7f), sun = new Color(1.0f, 0.92f, 0.76f), fogDensity = 0.0045f, sunIntensity = 1.1f, sunEuler = new Vector3(64f, 25f, 0f),
                paveTint = new Color(0.9f, 0.82f, 0.7f),
            },
        };

        static MexicoArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Maya_Temple"), new Vector3(-30f, 0f, 210f), 180f);
                put(g, K.P(Plan, "Facade"), new Vector3(-70f, 0f, -55f), 0f);
                put(g, K.P(Plan, "Facade"), new Vector3(80f, 0f, -60f), 0f);
                put(g, K.P(Plan, "Hut"), new Vector3(20f, 0f, -50f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Apartment", source = "Mexico/mx_885d323a.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Maya_Temple", source = "Mexico/mx_65181c6d.glb", fit = F.Fit.Width, size = 90f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Facade", source = "Mexico/mx_6db7aa6a.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Hut", source = "Mexico/mx_04ed348f.glb", fit = F.Fit.Width, size = 12f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Sliding", "mx1_gate", 12f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Mexico Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "MX");

        static LevelData M1(GameObject env) => BatchThree.Timed(Plan, env, "MX1", 1, "Zócalo Rush",
            "The market car park's gate opens on a timer. Wait for it, drive through, then take the free bay within 30 seconds; every knock costs 3.", "Gate_Sliding");
        static LevelData M2(GameObject env) => BatchThree.Alley(Plan, env, "MX2", 2, "Callejón",
            "Turn into the callejón — a lane barely wider than the car — squeeze past the cones and park at the end.", false, "Cones_Cluster");
        static LevelData M3(GameObject env) => BatchThree.Parallel(Plan, env, "MX3", 3, "Calle Paralela",
            "Keep right along the avenue and parallel park at the right kerb between the two cars, without touching either.");
    }

    // ==========================================================================================  ARGENTINA
    public static class ArgentinaArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Argentina", id = "ar", envName = "Env_BuenosAires", road = RoadDirection.RightHand, lotBays = 22, lotTarget = 13, lotBayW = 2.6f,
            northRow = new[] { "Retiro_Row", "Retiro_Row" },
            southRow = new[] { "Retiro_Row", "Retiro_Row", "Retiro_Row" },
            look = new K.Look
            {
                skyAmbient = new Color(0.74f, 0.76f, 0.8f), equatorAmbient = new Color(0.6f, 0.58f, 0.54f), groundAmbient = new Color(0.36f, 0.34f, 0.32f),
                fog = new Color(0.82f, 0.84f, 0.88f), sun = new Color(1.0f, 0.95f, 0.86f), fogDensity = 0.005f, sunIntensity = 1.25f, sunEuler = new Vector3(50f, 15f, 0f),
            },
        };

        static ArgentinaArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Barolo"), new Vector3(-35f, 0f, 160f), 180f);
                put(g, K.P(Plan, "Retiro"), new Vector3(80f, 0f, -90f), 0f);
                put(g, K.P(Plan, "Barolo"), new Vector3(100f, 0f, 230f), 180f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Retiro_Row", source = "Argentina/ar_46bd6b9e.glb", fit = F.Fit.Width, size = 30f },
                new F.Spec { combine = true, name = "Barolo", source = "Argentina/ar_13438555.glb", fit = F.Fit.Height, size = 75f },
                new F.Spec { combine = true, name = "Retiro", source = "Argentina/ar_46bd6b9e.glb", fit = F.Fit.Width, size = 34f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Ticket", "ar3_gate", 6.8f, 3f, 5f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Argentina Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "AR");

        static LevelData M1(GameObject env) => BatchThree.Reverse(Plan, env, "AR1", 1, "Calle Reverse",
            "Turn into the car park and reverse into the free bay between the two cars, finishing within 0.7 m of the back wall.");
        static LevelData M2(GameObject env) => BatchThree.Precision(Plan, env, "AR2", 2, "Tango Bay",
            "Turn in and park exactly in the middle of the free bay; accuracy earns the stars.", 0.28f);
        static LevelData M3(GameObject env) => BatchThree.Garage(Plan, env, "AR3", 3, "Parking Subterráneo",
            "The garage ticket gate opens on a timer. Wait for it, drive through and down the ramp to level -1, and park in the free bay.", bay: 3, gate: true, gateName: "Gate_Ticket");
    }

    // ==========================================================================================  EGYPT
    public static class EgyptArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Egypt", id = "eg", envName = "Env_Cairo", road = RoadDirection.RightHand, lotBays = 20, lotTarget = 12, lotBayW = 2.7f,
            northRow = new[] { "Arab_House", "Villa", "Arab_House" },
            southRow = new[] { "Villa", "Arab_House", "Villa" },
            look = new K.Look
            {
                skyAmbient = new Color(0.84f, 0.78f, 0.66f), equatorAmbient = new Color(0.74f, 0.62f, 0.46f), groundAmbient = new Color(0.48f, 0.38f, 0.28f),
                fog = new Color(0.94f, 0.84f, 0.66f), sun = new Color(1.0f, 0.92f, 0.74f), fogDensity = 0.0048f, sunIntensity = 1.2f, sunEuler = new Vector3(66f, 20f, 0f),
                paveTint = new Color(0.9f, 0.82f, 0.7f),
            },
        };

        static EgyptArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Temples"), new Vector3(-30f, 0f, 200f), 180f);
                put(g, K.P(Plan, "Mosque"), new Vector3(-10f, 0f, -55f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Arab_House", source = "SaudiArabia/sa_da4172d6.glb", fit = F.Fit.Width, size = 10f },
                new F.Spec { combine = true, name = "Villa", source = "SaudiArabia/sa_dfd9dd93.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Mosque", source = "SaudiArabia/sa_f18722b6.glb", fit = F.Fit.Width, size = 45f },
                new F.Spec { combine = true, name = "Temples", source = "Egypt/eg_ab516f77.glb", fit = F.Fit.Width, size = 90f, collider = F.ColliderKind.None },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Sliding", "eg2_gate", 12f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Egypt Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "EG");

        static LevelData M1(GameObject env) => BatchThree.Alley(Plan, env, "EG1", 1, "Khan Lane",
            "Turn into the khan lane — an old market alley barely wider than the car — squeeze past the cones and park at the end.", false, "Cones_Cluster");
        static LevelData M2(GameObject env) => BatchThree.Timed(Plan, env, "EG2", 2, "Souk Rush",
            "The souk car park's gate opens on a timer. Wait for it, drive through, then take the free bay within 30 seconds; every knock costs 3.", "Gate_Sliding");
        static LevelData M3(GameObject env) => BatchThree.Parallel(Plan, env, "EG3", 3, "Corniche Parallel",
            "Keep right along the avenue and parallel park at the right kerb between the two cars. Finish within 0.45 m of the kerb.");
    }

    // ==========================================================================================  AUSTRALIA
    public static class AustraliaArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Australia", id = "au", envName = "Env_Sydney", road = RoadDirection.LeftHand, lotBays = 22, lotTarget = 13, lotBayW = 2.6f,
            northRow = new[] { "Modern_House", "Blue_House", "Suburb_House" },
            southRow = new[] { "Blue_House", "Suburb_House", "Modern_House" },
            look = new K.Look
            {
                skyAmbient = new Color(0.74f, 0.8f, 0.88f), equatorAmbient = new Color(0.6f, 0.62f, 0.6f), groundAmbient = new Color(0.38f, 0.36f, 0.32f),
                fog = new Color(0.84f, 0.9f, 0.96f), sun = new Color(1.0f, 0.96f, 0.88f), fogDensity = 0.0042f, sunIntensity = 1.5f, sunEuler = new Vector3(60f, -30f, 0f),
            },
        };

        static AustraliaArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Opera_House"), new Vector3(-60f, 0f, 230f), 180f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Modern_House", source = "Australia/au_3eeb7a40.glb", fit = F.Fit.Width, size = 16f },
                new F.Spec { combine = true, name = "Blue_House", source = "Australia/au_b8375c5e.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "Suburb_House", source = "Australia/au_7af6b3aa.glb", fit = F.Fit.Width, size = 10f },
                new F.Spec { combine = true, name = "Opera_House", source = "Australia/au_317b2d54.glb", fit = F.Fit.Width, size = 110f, collider = F.ColliderKind.None },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Australia Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "AU");

        static LevelData M1(GameObject env) => BatchThree.Reverse(Plan, env, "AU1", 1, "Suburb Reverse",
            "Keep left, turn into the car park and reverse into the free bay between the two cars, finishing within 0.7 m of the back wall.");
        static LevelData M2(GameObject env) => BatchThree.Garage(Plan, env, "AU2", 2, "Harbour Garage",
            "Under the harbourfront: turn in, take the ramp down to level -1 and park in the free bay on the left.", bay: 2);
        static LevelData M3(GameObject env) => BatchThree.Exam(Plan, env, "AU3", 3, "Driving Test",
            "Keep left. Cross the start line, parallel park at the left kerb, then drive to the car park and park precisely in the bay.");
    }

    // ==========================================================================================  TURKEY
    public static class TurkeyArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Turkey", id = "tr", envName = "Env_Istanbul", road = RoadDirection.RightHand, lotBays = 20, lotTarget = 12, lotBayW = 2.6f,
            northRow = new[] { "Old_House", "Old_House", "Old_House", "Wall_Tower" },
            southRow = new[] { "Old_House", "Wall_Tower", "Old_House" },
            look = new K.Look
            {
                skyAmbient = new Color(0.78f, 0.78f, 0.78f), equatorAmbient = new Color(0.66f, 0.6f, 0.52f), groundAmbient = new Color(0.4f, 0.36f, 0.3f),
                fog = new Color(0.88f, 0.86f, 0.82f), sun = new Color(1.0f, 0.94f, 0.82f), fogDensity = 0.0048f, sunIntensity = 1.3f, sunEuler = new Vector3(55f, 25f, 0f),
                paveTint = new Color(0.88f, 0.84f, 0.76f),
            },
        };

        static TurkeyArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Galata"), new Vector3(-40f, 0f, 210f), 180f);
                put(g, K.P(Plan, "Maiden"), new Vector3(90f, 0f, -90f), 0f);
                put(g, K.P(Plan, "Mosque"), new Vector3(-20f, 0f, -55f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Old_House", source = "Greece/gr_4a906147.glb", fit = F.Fit.Width, size = 16f },
                new F.Spec { combine = true, name = "Wall_Tower", source = "Turkey/tr_9220360d.glb", fit = F.Fit.Height, size = 18f },
                new F.Spec { combine = true, name = "Galata", source = "Turkey/tr_4778557d.glb", fit = F.Fit.Height, size = 80f, collider = F.ColliderKind.None },
                new F.Spec { combine = true, name = "Maiden", source = "Turkey/tr_33a30218.glb", fit = F.Fit.Height, size = 30f },
                new F.Spec { combine = true, name = "Mosque", source = "SaudiArabia/sa_f18722b6.glb", fit = F.Fit.Width, size = 45f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Ticket", "tr3_gate", 6.8f, 3f, 5f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Turkey Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "TR");

        static LevelData M1(GameObject env) => BatchThree.Alley(Plan, env, "TR1", 1, "Bosphorus Lane",
            "Reverse all the way down the narrow lane behind the old houses and into the space at the end.", true);
        static LevelData M2(GameObject env) => BatchThree.Valet(Plan, env, "TR2", 2, "Hotel Valet",
            "Valet duty at the hotel: pull up at the stand, take the keys, then park the guest's car smoothly. Keep under 18 km/h.");
        static LevelData M3(GameObject env) => BatchThree.Garage(Plan, env, "TR3", 3, "Bazaar Garage",
            "The garage ticket gate opens on a timer. Wait for it, drive through and down the ramp to level -1, and park in the free bay.", bay: 3, gate: true, gateName: "Gate_Ticket");
    }

    // ==========================================================================================  NIGERIA (generic Kenney CC0 stand-in arena)
    public static class NigeriaArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "Nigeria", id = "ng", envName = "Env_Lagos", road = RoadDirection.RightHand, lotBays = 20, lotTarget = 12, lotBayW = 2.7f,
            northRow = new[] { "Shop_A", "Shop_B", "Shop_C" },
            southRow = new[] { "Shop_B", "Shop_A", "Shop_D" },
            look = new K.Look
            {
                skyAmbient = new Color(0.8f, 0.78f, 0.7f), equatorAmbient = new Color(0.7f, 0.62f, 0.5f), groundAmbient = new Color(0.44f, 0.38f, 0.3f),
                fog = new Color(0.92f, 0.86f, 0.72f), sun = new Color(1.0f, 0.93f, 0.78f), fogDensity = 0.0052f, sunIntensity = 1.3f, sunEuler = new Vector3(62f, 10f, 0f),
            },
        };

        static NigeriaArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Tower"), new Vector3(-50f, 0f, 230f), 180f);
                put(g, K.P(Plan, "Tower"), new Vector3(90f, 0f, 260f), 180f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "Shop_A", source = "Kenney/Commercial/building-a.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Shop_B", source = "Kenney/Commercial/building-b.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Shop_C", source = "Kenney/Commercial/building-c.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Shop_D", source = "Kenney/Commercial/building-d.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Tower", source = "Kenney/Commercial/building-skyscraper-a.glb", fit = F.Fit.Height, size = 90f, collider = F.ColliderKind.None },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
            K.BuildGate(Plan, "Gate_Sliding", "ng2_gate", 12f);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build Nigeria Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "NG");

        static LevelData M1(GameObject env) => BatchThree.Alley(Plan, env, "NG1", 1, "Market Lane",
            "Turn into the market lane — barely wider than the car — squeeze past the cones and park at the end.", false, "Cones_Cluster");
        static LevelData M2(GameObject env) => BatchThree.Timed(Plan, env, "NG2", 2, "Eko Rush",
            "The plaza car park's gate opens on a timer. Wait for it, drive through, then take the free bay within 30 seconds; every knock costs 3.", "Gate_Sliding");
        static LevelData M3(GameObject env) => BatchThree.Parallel(Plan, env, "NG3", 3, "Marina Parallel",
            "Keep right along the avenue and parallel park at the right kerb between the two cars. Finish within 0.45 m of the kerb.");
    }

    // ==========================================================================================  SOUTHAFRICA (generic Kenney CC0 stand-in arena)
    public static class SouthAfricaArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "SouthAfrica", id = "za", envName = "Env_CapeTown", road = RoadDirection.LeftHand, lotBays = 22, lotTarget = 13, lotBayW = 2.7f,
            northRow = new[] { "House_A", "House_B", "Shop_A" },
            southRow = new[] { "House_C", "Shop_B", "House_D" },
            look = new K.Look
            {
                skyAmbient = new Color(0.76f, 0.8f, 0.86f), equatorAmbient = new Color(0.62f, 0.62f, 0.58f), groundAmbient = new Color(0.4f, 0.38f, 0.32f),
                fog = new Color(0.86f, 0.9f, 0.94f), sun = new Color(1.0f, 0.95f, 0.84f), fogDensity = 0.0042f, sunIntensity = 1.45f, sunEuler = new Vector3(58f, -15f, 0f),
            },
        };

        static SouthAfricaArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Tower"), new Vector3(-40f, 0f, 230f), 180f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "House_A", source = "Kenney/Suburban/building-type-a.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "House_B", source = "Kenney/Suburban/building-type-b.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "House_C", source = "Kenney/Suburban/building-type-c.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "House_D", source = "Kenney/Suburban/building-type-d.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "Shop_A", source = "Kenney/Commercial/building-e.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Shop_B", source = "Kenney/Commercial/building-f.glb", fit = F.Fit.Width, size = 14f },
                new F.Spec { combine = true, name = "Tower", source = "Kenney/Commercial/building-skyscraper-b.glb", fit = F.Fit.Height, size = 90f, collider = F.ColliderKind.None },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build SouthAfrica Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "ZA");

        static LevelData M1(GameObject env) => BatchThree.Parallel(Plan, env, "ZA1", 1, "Cape Parallel",
            "Keep left along the avenue and parallel park at the left kerb between the two cars, without touching either.");
        static LevelData M2(GameObject env) => BatchThree.Valet(Plan, env, "ZA2", 2, "Hotel Valet",
            "Valet duty at the hotel: pull up at the stand, take the keys, then park the guest's car smoothly. Keep under 18 km/h.");
        static LevelData M3(GameObject env) => BatchThree.Reverse(Plan, env, "ZA3", 3, "Township Reverse",
            "Keep left, turn into the car park and reverse into the free bay between the two cars, finishing within 0.7 m of the back wall.");
    }

    // ==========================================================================================  NEWZEALAND (generic Kenney CC0 stand-in arena)
    public static class NewZealandArenaBuilder
    {
        public static readonly K.Plan Plan = new K.Plan
        {
            arena = "NewZealand", id = "nz", envName = "Env_Auckland", road = RoadDirection.LeftHand, lotBays = 20, lotTarget = 12, lotBayW = 2.7f,
            northRow = new[] { "House_A", "House_B", "House_C" },
            southRow = new[] { "House_D", "House_E", "House_F" },
            look = new K.Look
            {
                skyAmbient = new Color(0.72f, 0.8f, 0.86f), equatorAmbient = new Color(0.58f, 0.64f, 0.58f), groundAmbient = new Color(0.34f, 0.38f, 0.3f),
                fog = new Color(0.82f, 0.9f, 0.94f), sun = new Color(1.0f, 0.97f, 0.9f), fogDensity = 0.0042f, sunIntensity = 1.35f, sunEuler = new Vector3(52f, -25f, 0f),
            },
        };

        static NewZealandArenaBuilder()
        {
            Plan.landmarks = (g, put) =>
            {
                put(g, K.P(Plan, "Tree"), new Vector3(60f, 0f, -45f), 0f);
            };
            Plan.missions = new System.Func<GameObject, LevelData>[] { M1, M2, M3 };
        }

        public static void BuildProps()
        {
            var specs = new List<F.Spec>
            {
                new F.Spec { combine = true, name = "House_A", source = "Kenney/Suburban/building-type-e.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "House_B", source = "Kenney/Suburban/building-type-f.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "House_C", source = "Kenney/Suburban/building-type-g.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "House_D", source = "Kenney/Suburban/building-type-h.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "House_E", source = "Kenney/Suburban/building-type-a.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "House_F", source = "Kenney/Suburban/building-type-b.glb", fit = F.Fit.Width, size = 12f },
                new F.Spec { combine = true, name = "Tree", source = "Kenney/Suburban/tree-large.glb", fit = F.Fit.Height, size = 8f },
            };
            foreach (F.Spec s in specs) F.Make(Plan.arena, s);
        }

        [MenuItem("Tools/Ultimate Car Parking/Build NewZealand Arena")]
        public static void Build() => K.Build(Plan, BuildProps);
        public static void RenderShots() => K.RenderShots(Plan, "NZ");

        static LevelData M1(GameObject env) => BatchThree.Precision(Plan, env, "NZ1", 1, "Harbour Bay",
            "Keep left, turn into the car park and park exactly in the middle of the free bay; accuracy earns the stars.", 0.28f);
        static LevelData M2(GameObject env) => BatchThree.Alley(Plan, env, "NZ2", 2, "Hillside Lane",
            "Turn left into the narrow hillside lane, squeeze past the cones and park at the end.", false, "Cones_Cluster");
        static LevelData M3(GameObject env) => BatchThree.Garage(Plan, env, "NZ3", 3, "Wellington Garage",
            "Under the waterfront: turn in, take the ramp down to level -1 and park in the free bay on the left.", bay: 2);
    }
}
