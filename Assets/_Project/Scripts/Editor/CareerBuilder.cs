using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Career;
using UltimateCarParking.Environment;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.Parking;
using DS = UltimateCarParking.EditorTools.DrivingSchoolBuilder;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Phase 6 career: five chapters that reference the existing campaign and challenge levels by id (nothing copied
    /// or rewritten: level ids, stars, best times and the campaign catalog stay as they are), and the Driving Academy:
    /// seven short interactive lessons in the Driving School yard (their own catalog, outside the campaign).
    ///
    /// Rewards (chapter completion / mastery = every mission three stars) are balanced against the locked car prices:
    /// one-off missions, countries and chapters together now total about 136,000 coins, about 28 % of the 479,000 needed
    /// for every car. Budget to Premium cars come with steady play; the six Elite cars stay long-term goals.
    /// </summary>
    public static class CareerBuilder
    {
        public const string Dir = ProjectBootstrapper.LevelsDir + "/Career";
        public const string CatalogPath = Dir + "/CareerCatalog.asset";
        public const string AcademyCatalogPath = Dir + "/Academy_Catalog.asset";

        private sealed class ChapterSpec
        {
            public string id, title, subtitle; public string[] levels; public int unlockAfter, coins, mastery; public Color accent;
        }

        public static readonly string[][] ChapterLevels =
        {
            new[] { "L01", "L02", "L03", "C01", "C02", "L04", "L05", "L06" },
            new[] { "L07", "L08", "L09", "L10", "C03", "C04", "C05", "L11" },
            new[] { "L12", "L13", "L14", "L15", "L16", "C07", "C09", "C10" },
            new[] { "L17", "L18", "L19", "L20", "L21", "L22", "C06", "C11" },
            new[] { "L23", "L24", "L25", "L26", "L27", "L28", "L29", "L30", "C08", "C12" },
        };

        private static List<ChapterSpec> Chapters() => new List<ChapterSpec>
        {
            new ChapterSpec { id = "ch1", title = "BEGINNER ACADEMY", subtitle = "Steering, braking, forward parking, reversing and simple bays.",
                levels = ChapterLevels[0], unlockAfter = 0, coins = 1500, mastery = 750, accent = new Color(0.71f, 1f, 0.18f) },
            new ChapterSpec { id = "ch2", title = "CITY DRIVER", subtitle = "Parallel parking, traffic rules, narrow streets and urban bays.",
                levels = ChapterLevels[1], unlockAfter = 6, coins = 3000, mastery = 1500, accent = new Color(0.33f, 0.78f, 1f) },
            new ChapterSpec { id = "ch3", title = "PROFESSIONAL VALET", subtitle = "Guest pickups and deliveries, multi-stage jobs and precision.",
                levels = ChapterLevels[2], unlockAfter = 6, coins = 5000, mastery = 2500, accent = new Color(1f, 0.78f, 0.24f) },
            new ChapterSpec { id = "ch4", title = "HEAVY VEHICLE EXPERT", subtitle = "Big SUVs and monster trucks, tight turns and restricted spaces.",
                levels = ChapterLevels[3], unlockAfter = 6, coins = 7500, mastery = 3500, accent = new Color(1f, 0.5f, 0.25f) },
            new ChapterSpec { id = "ch5", title = "PARKING MASTER", subtitle = "Combinations, night, bad weather and expert precision.",
                levels = ChapterLevels[4], unlockAfter = 6, coins = 10000, mastery = 5000, accent = new Color(0.85f, 0.45f, 1f) },
        };

        public static void Build()
        {
            System.IO.Directory.CreateDirectory(Dir);
            var campaign = AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath);
            if (campaign == null) { Debug.LogError("[Career] campaign catalog missing"); return; }
            var chapters = new List<CareerChapter>();
            foreach (ChapterSpec spec in Chapters())
            {
                var levels = new List<LevelData>();
                foreach (string id in spec.levels)
                {
                    LevelData l = campaign.FindById(id);
                    if (l == null) { Debug.LogError($"[Career] {spec.id}: level {id} not in the campaign"); continue; }
                    levels.Add(l);
                }
                chapters.Add(new CareerChapter
                {
                    id = spec.id, title = spec.title, subtitle = spec.subtitle, unlockAfter = spec.unlockAfter,
                    completionCoins = spec.coins, masteryCoins = spec.mastery, accent = spec.accent,
                    missions = WriteCatalog($"{Dir}/Chapter_{spec.id}.asset", levels),
                });
            }
            LevelCatalog academy = WriteCatalog(AcademyCatalogPath, BuildAcademy());

            var career = AssetDatabase.LoadAssetAtPath<CareerCatalog>(CatalogPath);
            if (career == null) { career = ScriptableObject.CreateInstance<CareerCatalog>(); AssetDatabase.CreateAsset(career, CatalogPath); }
            var so = new SerializedObject(career);
            SerializedProperty arr = so.FindProperty("chapters");
            arr.arraySize = chapters.Count;
            for (int i = 0; i < chapters.Count; i++)
            {
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                CareerChapter c = chapters[i];
                e.FindPropertyRelative("id").stringValue = c.id;
                e.FindPropertyRelative("title").stringValue = c.title;
                e.FindPropertyRelative("subtitle").stringValue = c.subtitle;
                e.FindPropertyRelative("missions").objectReferenceValue = c.missions;
                e.FindPropertyRelative("unlockAfter").intValue = c.unlockAfter;
                e.FindPropertyRelative("completionCoins").intValue = c.completionCoins;
                e.FindPropertyRelative("masteryCoins").intValue = c.masteryCoins;
                e.FindPropertyRelative("accent").colorValue = c.accent;
            }
            so.FindProperty("academy").objectReferenceValue = academy;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(career);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Career] {chapters.Count} chapters, academy {academy.Count} lessons");
        }

        private static LevelCatalog WriteCatalog(string path, List<LevelData> levels)
        {
            var cat = AssetDatabase.LoadAssetAtPath<LevelCatalog>(path);
            if (cat == null) { cat = ScriptableObject.CreateInstance<LevelCatalog>(); AssetDatabase.CreateAsset(cat, path); }
            var so = new SerializedObject(cat);
            SerializedProperty list = so.FindProperty("levels");
            list.arraySize = levels.Count;
            for (int i = 0; i < levels.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cat);
            return cat;
        }

        // ------------------------------------------------------------------ Driving Academy

        private static List<LevelData> BuildAcademy()
        {
            var env = AssetDatabase.LoadAssetAtPath<GameObject>(DS.EnvPrefabPath);
            var easy = new ParkingRules { edgeMargin = 0.04f, angleTolerance = 15f, maxSpeedKmh = 2f, holdDuration = 0.8f };
            Vector2 wideBay = new Vector2(3.3f, 6.5f);
            Vector3 laneSpawn = new Vector3(-35f, 0f, -1.6f);
            var levels = new List<LevelData>
            {
                Lesson("A01", "Steering", "Turn the wheel all the way left, then all the way right.", env, laneSpawn, 90f,
                    new TutorialObjective("Turn left, then right", TutorialAction.SteerBothWays)),
                Lesson("A02", "Gas and Brake", "Hold GAS to speed up, then BRAKE to stop.", env, laneSpawn, 90f,
                    new TutorialObjective("Reach 12 km/h", TutorialAction.Accelerate, 12f),
                    new TutorialObjective("Brake to a stop", TutorialAction.BrakeToStop)),
                Lesson("A03", "Gears and Reverse", "Stop, select reverse and back up.", env, laneSpawn, 90f,
                    new TutorialObjective("Drive forward a little", TutorialAction.Accelerate, 6f),
                    new TutorialObjective("Stop", TutorialAction.BrakeToStop),
                    new TutorialObjective("Back up 3 m", TutorialAction.Reverse, 3f)),
                Lesson("A04", "Reverse Parking", "Back into the wide bay. Look at the rear camera while reversing.", env, new Vector3(DS.AreaBBayX(1) - 9f, 0f, 1.5f), 90f,
                    new ReverseParkObjective("Reverse into the bay", new Vector3(DS.AreaBBayX(2), 0f, DS.BayCentreZ), 0f, wideBay, maxRearGap: 1.4f, easy)),
                Lesson("A05", "Parallel Parking", "Pull level with the gap, then reverse in toward the kerb.", env, new Vector3(-30f, 0f, -2f), 90f,
                    new ParallelParkObjective("Parallel park at the kerb", new Vector3(14.5f, 0f, DS.KerbZ + 1.4f), 90f, new Vector2(2.8f, 8f),
                        kerbSide: 1, maxKerbGap: 0.6f, kerbStrikeLimit: 3, easy)),
                Lesson("A06", "Parking Sensors", "Reverse toward the wall. The sensor counts down; stop when it reads under 1 m.", env,
                    new Vector3(DS.AreaBBayX(3), 0f, 6.5f), 180f,
                    new TutorialObjective("Back up to the wall", TutorialAction.BackUpToObstacle, 1.0f)),
                Lesson("A07", "Camera Views", "Tap VIEW to switch cameras: chase, top-down, bumper and more.", env, laneSpawn, 90f,
                    new TutorialObjective("Try two camera views", TutorialAction.SwitchCamera)),
            };
            return levels;
        }

        private static LevelData Lesson(string id, string name, string briefing, GameObject env, Vector3 spawn, float yaw, params MissionObjective[] steps)
        {
            string path = $"{Dir}/Academy_{id}.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null) { level = ScriptableObject.CreateInstance<LevelData>(); AssetDatabase.CreateAsset(level, path); }
            var so = new SerializedObject(level);
            so.FindProperty("levelId").stringValue = id;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("levelNumber").intValue = int.Parse(id.Substring(1));
            so.FindProperty("missionType").enumValueIndex = (int)MissionType.StandardParking;
            so.FindProperty("difficulty").enumValueIndex = (int)ChallengeDifficulty.Beginner;
            so.FindProperty("briefing").stringValue = briefing;
            SerializedProperty objs = so.FindProperty("objectives");
            objs.arraySize = steps.Length;
            for (int i = 0; i < steps.Length; i++) objs.GetArrayElementAtIndex(i).managedReferenceValue = steps[i];
            so.FindProperty("theme").objectReferenceValue = null;
            so.FindProperty("environmentPrefab").objectReferenceValue = env;
            LevelDesigner.WritePlacements(so.FindProperty("props"), new List<PropPlacement>());
            so.FindProperty("spawnPosition").vector3Value = spawn;
            so.FindProperty("spawnYaw").floatValue = yaw;
            if (steps[0] is ParkObjective p)
            {
                so.FindProperty("targetPosition").vector3Value = p.Position;
                so.FindProperty("targetYaw").floatValue = Mathf.Repeat(p.ZoneRotation.eulerAngles.y, 360f);
                so.FindProperty("targetSize").vector2Value = p.Size;
            }
            so.FindProperty("timeLimitSeconds").floatValue = 0f;          // no clock in lessons
            so.FindProperty("collisionLimit").intValue = -1;              // no fail on hits
            so.FindProperty("threeStarTime").floatValue = 9999f;
            so.FindProperty("threeStarMaxCollisions").intValue = 99;
            so.FindProperty("twoStarTime").floatValue = 9999f;
            so.FindProperty("twoStarMaxCollisions").intValue = 99;
            so.FindProperty("scoreMode").enumValueIndex = (int)ScoreMode.TimeAndCollisions;
            so.FindProperty("baseCoins").intValue = 100;                 // first completion only (replays give 20 %)
            so.FindProperty("coinsPerStar").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            return level;
        }
    }
}
