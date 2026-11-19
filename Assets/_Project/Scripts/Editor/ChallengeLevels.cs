using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Cars;
using UltimateCarParking.Conditions;
using UltimateCarParking.Environment;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.Parking;
using DS = UltimateCarParking.EditorTools.DrivingSchoolBuilder;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Phase 5 challenge chapter: 12 levels built from reusable templates in the existing Driving School yard (real props,
    /// no new environment), appended to the campaign after level 30 (existing level ids, order and saves untouched).
    /// Each level is a <see cref="Spec"/>: difficulty (bay size, tolerances, hold, limits, assistance cap, star rules),
    /// condition, car, objectives and props. Stars use the realistic parking scorecard (ScoreMode.Parking).
    /// </summary>
    public static class ChallengeLevels
    {
        public const int Count = 12;
        public const int FirstNumber = LevelDesigner.LevelCount + 1;
        private const string Dir = ProjectBootstrapper.LevelsDir + "/Challenges";

        /// <summary>One challenge, as data.</summary>
        public sealed class Spec
        {
            public string id, name, briefing;
            public MissionType type;
            public ChallengeDifficulty difficulty;
            public RoadCondition condition = RoadCondition.Clear;
            public string forcedCar;
            public Vector3 spawn = new Vector3(-35f, 0f, -1.6f);
            public float spawnYaw = 90f;
            public float time = 150f;
            public bool requireParkingBrake;
            public List<PropPlacement> props = new List<PropPlacement>();
            public List<MissionObjective> objectives = new List<MissionObjective>();
            public int baseCoins = 200, perStar = 50;
        }

        // ------------------------------------------------------------------ templates

        /// <summary>Bay size for a difficulty: the largest garage car (1.93 × 5.2 m) plus the tier's margin (the tightest tier still
        /// leaves every car 0.4 m of width and 0.3 m of length after the line margins, like every other mission bay).</summary>
        public static Vector2 Bay(ChallengeDifficulty d)
        {
            DifficultyProfile p = DifficultyProfile.For(d);
            return new Vector2(1.93f + p.BayExtraWidth, 5.2f + p.BayExtraLength);
        }

        public static ParkingRules Rules(ChallengeDifficulty d, bool parkingBrake = false)
        {
            DifficultyProfile p = DifficultyProfile.For(d);
            return new ParkingRules { edgeMargin = 0.04f, angleTolerance = p.AngleTolerance, maxSpeedKmh = 1.5f, holdDuration = p.HoldSeconds, requireParkingBrake = parkingBrake };
        }

        private static Vector3 BayA(int i) => new Vector3(DS.AreaABayX(i), 0f, DS.BayCentreZ);
        private static Vector3 BayB(int i) => new Vector3(DS.AreaBBayX(i), 0f, DS.BayCentreZ);

        private static void Car(Spec s, string prop, Vector3 pos, float yaw)
        {
            GameObject prefab = RealPropFactory.Load(prop);
            if (prefab == null) { Debug.LogError("[Challenges] missing prop " + prop); return; }
            s.props.Add(new PropPlacement(prefab, pos, yaw));
        }

        private static void ParkedA(Spec s, params int[] bays) { string[] n = { "Parked_hatchback", "Parked_suv", "Parked_wagon", "Parked_sedan", "Parked_compact", "Parked_coupe" }; foreach (int b in bays) Car(s, n[b % n.Length], BayA(b) + new Vector3(0f, 0f, 0.2f), (b % 2) * 180f); }
        private static void ParkedB(Spec s, params int[] bays) { string[] n = { "Parked_sedan", "Parked_pickup", "Parked_suv", "Parked_coupe", "Parked_minivan", "Parked_wagon" }; foreach (int b in bays) Car(s, n[b % n.Length], BayB(b) + new Vector3(0f, 0f, 0.35f), 180f); }

        /// <summary>Kerbside slot between two parked cars (Area C): slot from x0 to x1, cars behind and ahead.</summary>
        private static ParallelParkObjective Kerb(Spec s, float x0, float x1, ChallengeDifficulty d, string title)
        {
            float z = DS.SlotCentreZ - 0.3f;
            Car(s, "Parked_wagon", new Vector3(x0 - 0.3f - 2.3f, 0f, z), 90f);
            Car(s, "Parked_sedan", new Vector3(x1 + 0.3f + 2.2f, 0f, z), 90f);
            const float slotWidth = 2.6f;
            return new ParallelParkObjective(title, new Vector3((x0 + x1) * 0.5f, 0f, DS.KerbZ + slotWidth * 0.5f), 90f,
                new Vector2(slotWidth, x1 - x0), kerbSide: 1, maxKerbGap: d >= ChallengeDifficulty.Advanced ? 0.4f : 0.5f,
                kerbStrikeLimit: d >= ChallengeDifficulty.Expert ? 1 : 2, Rules(d));
        }

        // ------------------------------------------------------------------ the chapter

        public static List<Spec> Specs()
        {
            var list = new List<Spec>();
            ChallengeDifficulty B = ChallengeDifficulty.Beginner, I = ChallengeDifficulty.Intermediate, A = ChallengeDifficulty.Advanced, E = ChallengeDifficulty.Expert;

            var c = new Spec { id = "C01", name = "Wide Open", type = MissionType.StandardParking, difficulty = B, time = 150f,
                briefing = "Challenge 1, Beginner. A wide bay with guides on. Park in bay 3 either way round and hold still. Your score: how centred and straight you finish, and no hits." };
            ParkedA(c, 0, 4);
            c.objectives.Add(new ParkObjective("Park in bay 3", BayA(2), 0f, Bay(B), ParkDirection.Either, Rules(B)));
            list.Add(c);

            c = new Spec { id = "C02", name = "First Reverse", type = MissionType.ReverseParking, difficulty = B, time = 150f,
                briefing = "Challenge 2, Beginner. Drive past bay R2 and reverse in. The bay is wide and the sensors will help." };
            ParkedB(c, 0, 3);
            c.objectives.Add(new ReverseParkObjective("Reverse into bay R2", BayB(1), 0f, Bay(B), maxRearGap: 1.3f, Rules(B)));
            list.Add(c);

            c = new Spec { id = "C03", name = "Between Two", type = MissionType.ParallelParking, difficulty = I, time = 160f,
                briefing = "Challenge 3, Intermediate. Parallel park in the 7 m gap between two parked cars. Finish close to the kerb without touching them." };
            c.objectives.Add(Kerb(c, 18f, 25f, I, "Parallel park between the cars"));
            list.Add(c);

            c = new Spec { id = "C04", name = "Cone Course", type = MissionType.ObstacleCourse, difficulty = I, time = 160f,
                briefing = "Challenge 4, Intermediate. Weave through the three cone gates on the training lane, then park nose-in in bay 5. Every cone you hit costs points." };
            float[] gx = { -22f, -8f, 6f }, gz = { 1.8f, -1.8f, 1.8f };
            for (int g = 0; g < 3; g++)
            {
                foreach (float side in new[] { -1f, 1f })
                    for (int k = 0; k < 2; k++)
                        Car(c, "Cone_Street", new Vector3(gx[g] - 0.6f + 1.2f * k, 0f, gz[g] + side * 1.9f), 0f);
                c.objectives.Add(new CheckpointObjective($"Cone gate {g + 1}", new Vector3(gx[g], 0f, gz[g]), 90f, new Vector2(3.2f, 1.6f), "THROUGH THE CONE GATE"));
            }
            ParkedA(c, 3, 5);
            c.objectives.Add(new ParkObjective("Park in bay 5", BayA(4), 0f, Bay(I), ParkDirection.NoseIn, Rules(I)));
            list.Add(c);

            c = new Spec { id = "C05", name = "Rush Hour", type = MissionType.TimedParking, difficulty = I, time = 120f,
                briefing = "Challenge 5, Intermediate. 40 seconds to park in bay R5. Hits cost 3 seconds each. Fast and tidy scores best." };
            ParkedB(c, 3, 5);
            c.objectives.Add(new TimedParkObjective("Park in bay R5 in 40 s", BayB(4), 0f, Bay(I), ParkDirection.Either, 40f, 3f, Rules(I)));
            list.Add(c);

            c = new Spec { id = "C06", name = "Tight Squeeze", type = MissionType.ReverseParking, difficulty = A, time = 150f,
                briefing = "Challenge 6, Advanced. Reverse into bay R3 between two cars, back to within 0.6 m of the wall. No alignment hints at this level." };
            ParkedB(c, 1, 3);
            c.objectives.Add(new ReverseParkObjective("Reverse into bay R3", BayB(2), 0f, Bay(A), maxRearGap: 0.6f, Rules(A)));
            list.Add(c);

            c = new Spec { id = "C07", name = "Night Precision", type = MissionType.PrecisionParking, difficulty = A, condition = RoadCondition.Night, time = 150f,
                briefing = "Challenge 7, Advanced. At night, park dead centre in bay 4 by headlight. The car only counts once it is centred and straight." };
            ParkedA(c, 2, 4);
            c.objectives.Add(new PrecisionParkObjective("Precision park in bay 4", BayA(3), 0f, Bay(A), ParkDirection.Either, 0.35f, 5f, 0.6f, Rules(A)));
            list.Add(c);

            c = new Spec { id = "C08", name = "Wet Kerb", type = MissionType.ParallelParking, difficulty = A, condition = RoadCondition.Rain, time = 160f,
                briefing = "Challenge 8, Advanced. Rain: less grip, longer stops. Parallel park at the kerb between the cars, gently." };
            c.objectives.Add(Kerb(c, 4f, 11f, A, "Parallel park in the rain"));
            list.Add(c);

            c = new Spec { id = "C09", name = "Snow Valet", type = MissionType.ValetParking, difficulty = A, condition = RoadCondition.Snow, time = 180f,
                briefing = "Challenge 9, Advanced. Snow and ice. Pick up the guest's car at the valet stand on the lane, then park it in bay 6 smoothly. Harsh driving lowers the guest rating." };
            ParkedA(c, 4);
            c.objectives.Add(new ValetParkObjective("Valet: park the guest's car", new Vector3(-2f, 0f, -1.6f), 90f, BayA(5), 0f, Bay(A), ParkDirection.Either, 15f, Rules(A)));
            list.Add(c);

            c = new Spec { id = "C10", name = "Three Points", type = MissionType.MultiStage, difficulty = E, time = 300f,
                briefing = "Challenge 10, Expert. Three manoeuvres in a row: nose into bay 2, reverse into bay R5, then parallel park at the kerb. No sensors or guides." };
            ParkedA(c, 0, 2); ParkedB(c, 3, 5);
            c.objectives.Add(new ParkObjective("Nose into bay 2", BayA(1), 0f, Bay(E), ParkDirection.NoseIn, Rules(E)));
            c.objectives.Add(new ReverseParkObjective("Reverse into bay R5", BayB(4), 0f, Bay(E), maxRearGap: 0.6f, Rules(E)));
            c.objectives.Add(Kerb(c, -10f, -3f, E, "Parallel park at the kerb"));
            list.Add(c);

            c = new Spec { id = "C11", name = "Heavy Metal", type = MissionType.HeavyVehicle, difficulty = E, forcedCar = "monster_mammoth", time = 180f,
                briefing = "Challenge 11, Expert. Reverse a monster truck into bay R3. Heavy steering and a wide turning circle: plan the swing early." };
            ParkedB(c, 0, 4);
            c.objectives.Add(new ReverseParkObjective("Reverse the truck into bay R3", BayB(2), 0f, Bay(E) + new Vector2(0.4f, 0.3f), maxRearGap: 0.8f, Rules(E)));
            list.Add(c);

            c = new Spec { id = "C12", name = "Driving Exam", type = MissionType.ParkingExam, difficulty = E, time = 240f, requireParkingBrake = true,
                briefing = "Challenge 12, Expert. The driving test: keep under 20 km/h, stop fully at the stop line, follow the lane, then reverse into bay R4 and finish in P or with the handbrake on. More than two faults fails." };
            Car(c, "Sign_Stop", new Vector3(-6f, 0f, -4.6f), 0f);
            c.objectives.Add(new ExamRouteObjective("Follow the exam route", new[]
            {
                new ExamGate(new Vector3(-22f, 0f, -1.6f), 90f, new Vector2(3.6f, 2f)),
                new ExamGate(new Vector3(-7f, 0f, -1.6f), 90f, new Vector2(3.6f, 3f), stop: true),
                new ExamGate(new Vector3(8f, 0f, -1.6f), 90f, new Vector2(3.6f, 2f)),
            }, speedLimitKmh: 20f, maxFaults: 2));
            ParkedB(c, 2, 4);
            c.objectives.Add(new ReverseParkObjective("Reverse into bay R4", BayB(3), 0f, Bay(E), maxRearGap: 0.7f, Rules(E, parkingBrake: true)));
            list.Add(c);
            return list;
        }

        // ------------------------------------------------------------------ writing

        public static void Build()
        {
            var env = AssetDatabase.LoadAssetAtPath<GameObject>(DS.EnvPrefabPath);
            if (env == null) { Debug.LogError("[Challenges] Driving School prefab missing."); return; }
            var cars = AssetDatabase.LoadAssetAtPath<CarCatalog>(CarFactory.CarCatalogPath);
            System.IO.Directory.CreateDirectory(Dir);
            var levels = new List<LevelData>();
            List<Spec> specs = Specs();
            for (int i = 0; i < specs.Count; i++) levels.Add(Write(FirstNumber + i, specs[i], env, cars));

            // Append to the campaign after the existing levels (idempotent: replaces an earlier copy of the chapter).
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath);
            var so = new SerializedObject(catalog);
            SerializedProperty list = so.FindProperty("levels");
            var kept = new List<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                Object o = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o is LevelData l && l.LevelId.StartsWith("C") && l.LevelNumber > LevelDesigner.LevelCount) continue;
                kept.Add(o);
            }
            kept.AddRange(levels);
            list.arraySize = kept.Count;
            for (int i = 0; i < kept.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Challenges] {levels.Count} challenge levels written ({FirstNumber}–{FirstNumber + levels.Count - 1}); campaign now {kept.Count} levels");
        }

        private static LevelData Write(int number, Spec s, GameObject env, CarCatalog cars)
        {
            string path = $"{Dir}/Challenge_{s.id}.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null) { level = ScriptableObject.CreateInstance<LevelData>(); AssetDatabase.CreateAsset(level, path); }
            DifficultyProfile p = DifficultyProfile.For(s.difficulty);
            ParkObjective first = s.objectives.OfType<ParkObjective>().FirstOrDefault();
            var so = new SerializedObject(level);
            so.FindProperty("levelId").stringValue = s.id;
            so.FindProperty("displayName").stringValue = s.name;
            so.FindProperty("levelNumber").intValue = number;
            so.FindProperty("missionType").enumValueIndex = (int)s.type;
            so.FindProperty("condition").enumValueIndex = (int)s.condition;
            so.FindProperty("difficulty").enumValueIndex = (int)s.difficulty;
            so.FindProperty("briefing").stringValue = s.briefing;
            SerializedProperty objs = so.FindProperty("objectives");
            objs.arraySize = s.objectives.Count;
            for (int i = 0; i < s.objectives.Count; i++) objs.GetArrayElementAtIndex(i).managedReferenceValue = s.objectives[i];
            so.FindProperty("theme").objectReferenceValue = null;
            so.FindProperty("environmentPrefab").objectReferenceValue = env;
            LevelDesigner.WritePlacements(so.FindProperty("props"), s.props);
            so.FindProperty("forcedCar").objectReferenceValue = string.IsNullOrEmpty(s.forcedCar) || cars == null ? null : cars.FindById(s.forcedCar);
            so.FindProperty("cameraHeight").floatValue = 0f;
            so.FindProperty("cameraDistance").floatValue = 0f;
            so.FindProperty("spawnPosition").vector3Value = s.spawn;
            so.FindProperty("spawnYaw").floatValue = s.spawnYaw;
            if (first != null)
            {
                so.FindProperty("targetPosition").vector3Value = first.Position;
                so.FindProperty("targetYaw").floatValue = Mathf.Repeat(first.ZoneRotation.eulerAngles.y, 360f);
                so.FindProperty("targetSize").vector2Value = first.Size;
            }
            so.FindProperty("timeLimitSeconds").floatValue = s.time;
            so.FindProperty("collisionLimit").intValue = p.CollisionLimit;
            so.FindProperty("overrideParkingRules").boolValue = false;
            so.FindProperty("threeStarTime").floatValue = s.time * 0.8f;   // Parking score mode ignores time; kept below the limit
            so.FindProperty("threeStarMaxCollisions").intValue = p.ThreeStarHits;
            so.FindProperty("twoStarTime").floatValue = s.time * 0.95f;
            so.FindProperty("twoStarMaxCollisions").intValue = p.TwoStarHits;
            so.FindProperty("scoreMode").enumValueIndex = (int)ScoreMode.Parking;
            so.FindProperty("threeStarQuality").floatValue = p.ThreeStarScore;
            so.FindProperty("twoStarQuality").floatValue = p.TwoStarScore;
            so.FindProperty("baseCoins").intValue = s.baseCoins + 25 * (int)s.difficulty;
            so.FindProperty("coinsPerStar").intValue = s.perStar + 10 * (int)s.difficulty;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            return level;
        }
    }
}
