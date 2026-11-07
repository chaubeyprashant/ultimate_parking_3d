using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.Parking;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Every campaign level, loaded for real: world built, spawn and bay unobstructed, bay reachable from the spawn
    /// for a car-sized footprint (grid search), and a car placed in the bay completes the level.
    /// </summary>
    public class CampaignLevelTests
    {
        private static LevelCatalog Catalog()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
#else
            return null;
#endif
        }

        private static IEnumerable<int> LevelNumbers()
        {
            for (int n = 1; n <= 30; n++) yield return n;
        }

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.ResetToEmpty();

        [UnityTest]
        public IEnumerator Level_Is_Playable([ValueSource(nameof(LevelNumbers))] int number)
        {
            LevelCatalog catalog = Catalog();
            LevelData level = catalog.Get(number - 1);
            LevelSelection.Request(level, catalog);
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while ((LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(LevelState.Playing, LevelManager.Instance.State, "level starts");
            LevelLoader loader = LevelLoader.Instance;
            VehicleController car = loader.Vehicle;
            car.Input.KeyboardEnabled = false;
            yield return new WaitForFixedUpdate();

            Assert.IsNotNull(loader.Environment, "environment built");
            Assert.Greater(loader.Environment.GetComponentsInChildren<Collider>().Length, 10, "environment has colliders");
            Assert.Less(Vector3.Distance(car.transform.position, level.SpawnPosition), 1f, "car at spawn");
            Assert.AreEqual(0, LevelManager.Instance.Collisions, "spawn is not inside anything");

            // Spawn and bay volumes free of obstacles (above kerb height, ignoring the car itself).
            Assert.IsEmpty(Blockers(car.transform.position, car.transform.rotation, new Vector3(1.1f, 0.6f, 2.6f), car.Body), "spawn clear");
            Assert.IsEmpty(Blockers(level.TargetPosition, level.TargetRotation, new Vector3(level.TargetSize.x * 0.5f - 0.1f, 0.6f, level.TargetSize.y * 0.5f - 0.1f), car.Body), "bay clear");

            // Reachability for a 2.0 m-wide footprint (widest car 1.95 m) on a 0.5 m grid.
            int explored;
            // Prefab-based mission levels (Driving School yard, 88 x 40 m) have no theme: search a fixed area.
            Vector2 searchArea = level.Theme != null ? level.Theme.LotSize : new Vector2(90f, 42f);
            bool reachable = Reachable(car.transform.position, level.TargetPosition, searchArea, 1.0f, car.Body, out explored);
            Assert.IsTrue(reachable, $"bay reachable from spawn ({explored} cells explored)");

            // Park it: placed square in the bay (facing the required way) the level completes.
            float placed = LevelManager.Instance.ElapsedSeconds;
            Vector3 parkAt = level.TargetPosition;
            if (level.HasObjectives && level.CreateObjectives()[0] is ReverseParkObjective)
            {
                // Reverse bays also need the rear bumper near the back line: back the car up to 0.3 m from it.
                float min = float.MaxValue, max = float.MinValue;
                foreach (Vector3 c in LevelManager.Instance.Validator.LocalFootprint) { min = Mathf.Min(min, c.z); max = Mathf.Max(max, c.z); }
                float back = level.TargetSize.y * 0.5f - (max - min) * 0.5f - 0.3f;
                parkAt -= level.TargetRotation * Vector3.forward * back;
            }
            car.ResetVehicle(parkAt + Vector3.up * 0.05f, level.TargetRotation);
            t = 0f;
            while (LevelManager.Instance.State == LevelState.Playing && t < 8f) { t += Time.deltaTime; yield return null; }
            Debug.Log($"[Test] {level.LevelId} {level.DisplayName} ({(level.Theme != null ? level.Theme.ThemeId : level.EnvironmentPrefab.name)}): props {level.Props.Length}, bay {level.TargetSize.x:F2}x{level.TargetSize.y:F2}, " +
                      $"limit {level.TimeLimitSeconds}s/{level.CollisionLimit} hits, reachable ({explored} cells), {LevelManager.Instance.State}");
            Assert.AreEqual(LevelState.Completed, LevelManager.Instance.State, $"parking completes (validator: {LevelManager.Instance.Validator.Status})");
        }

        internal static List<string> Blockers(Vector3 center, Quaternion rot, Vector3 halfExtents, Rigidbody self)
        {
            var list = new List<string>();
            foreach (Collider c in Physics.OverlapBox(center + Vector3.up * 0.95f, halfExtents, rot, ~0, QueryTriggerInteraction.Ignore))
                if (c.attachedRigidbody != self || self == null) list.Add(c.name);
            return list;
        }

        /// <summary>4-connected BFS over cells whose car-sized box (0.35–1.55 m high) hits nothing but the car.</summary>
        internal static bool Reachable(Vector3 from, Vector3 to, Vector2 lot, float halfWidth, Rigidbody self, out int explored)
        {
            const float cell = 0.5f;
            int nx = Mathf.CeilToInt(lot.x / cell), nz = Mathf.CeilToInt(lot.y / cell);
            float ox = -lot.x * 0.5f, oz = -lot.y * 0.5f;
            var state = new sbyte[nx, nz];   // 0 unknown, 1 free, -1 blocked, 2 visited
            var buffer = new Collider[16];
            bool Free(int x, int z)
            {
                if (x < 0 || z < 0 || x >= nx || z >= nz) return false;
                if (state[x, z] != 0) return state[x, z] > 0;
                Vector3 c = new Vector3(ox + (x + 0.5f) * cell, 0.95f, oz + (z + 0.5f) * cell);
                int n = Physics.OverlapBoxNonAlloc(c, new Vector3(halfWidth, 0.6f, halfWidth), buffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                bool free = true;
                for (int i = 0; i < n; i++) if (buffer[i].attachedRigidbody != self || self == null) { free = false; break; }
                state[x, z] = (sbyte)(free ? 1 : -1);
                return free;
            }
            Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt((p.x - ox) / cell), Mathf.FloorToInt((p.z - oz) / cell));
            Vector2Int start = Cell(from), goal = Cell(to);
            explored = 0;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            state[start.x, start.y] = 2;   // the car itself stands here
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                explored++;
                if (Mathf.Abs(c.x - goal.x) <= 1 && Mathf.Abs(c.y - goal.y) <= 1) return true;
                foreach (Vector2Int d in dirs)
                {
                    Vector2Int n = c + d;
                    if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || state[n.x, n.y] == 2) continue;
                    if (!Free(n.x, n.y)) continue;
                    state[n.x, n.y] = 2;
                    queue.Enqueue(n);
                }
            }
            return false;
        }
    }
}
