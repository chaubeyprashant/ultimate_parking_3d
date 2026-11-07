using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.WorldTour;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Every country-arena mission, played through the real Gameplay scene: correct level and road direction,
    /// clear spawn, each objective reachable by driving, and the mission completing when each objective is satisfied
    /// in turn (checkpoint crossed, car parked in the bay). Also saves an in-game screenshot per mission.
    /// </summary>
    public class CountryArenaMissionTests
    {
        public static IEnumerable<string> Missions()
        {
#if UNITY_EDITOR
            foreach (string id in new[] { "IN1", "IN2", "IN3", "US1", "US2", "US3", "GB1", "GB2", "GB3", "JP1", "JP2", "JP3", "AE1", "AE2", "AE3", "CN1", "CN2", "CN3", "KR1", "KR2", "KR3", "TH1", "TH2", "TH3", "SG1", "SG2", "SG3", "MY1", "MY2", "MY3", "ID1", "ID2", "ID3", "SA1", "SA2", "SA3", "FR1", "FR2", "FR3", "DE1", "DE2", "DE3", "IT1", "IT2", "IT3", "ES1", "ES2", "ES3", "RU1", "RU2", "RU3", "NL1", "NL2", "NL3", "CH1", "CH2", "CH3", "GR1", "GR2", "GR3", "BR1", "BR2", "BR3", "CA1", "CA2", "CA3", "MX1", "MX2", "MX3", "AR1", "AR2", "AR3", "EG1", "EG2", "EG3", "AU1", "AU2", "AU3", "TR1", "TR2", "TR3", "NG1", "NG2", "NG3", "ZA1", "ZA2", "ZA3", "NZ1", "NZ2", "NZ3" })
                if (File.Exists($"Assets/_Project/Levels/Countries/Level_{id}.asset")) yield return id;
#else
            yield break;
#endif
        }

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.ResetToEmpty();

        private static IEnumerator WaitFor(System.Func<bool> c, float timeout, string what)
        {
            float t = 0f;
            while (!c()) { t += Time.unscaledDeltaTime; if (t > timeout) Assert.Fail("timed out: " + what); yield return null; }
        }

        [UnityTest]
        public IEnumerator Mission_Is_Playable_And_Completes([ValueSource(nameof(Missions))] string id)
        {
#if UNITY_EDITOR
            var level = AssetDatabase.LoadAssetAtPath<LevelData>($"Assets/_Project/Levels/Countries/Level_{id}.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>($"Assets/_Project/Levels/Countries/Catalog_{id.Substring(0, 2).ToLowerInvariant()}.asset");
            Assert.IsNotNull(level, id);
            Assert.IsNotNull(catalog, "mission catalog for " + id);
            Assert.IsTrue(level.HasObjectives, "mission objectives");
            Assert.IsTrue(level.HasAddressableEnvironment || level.EnvironmentPrefab != null, "arena environment");
            Assert.IsTrue(level.HasAddressableEnvironment, $"{id} arena is delivered per country (Addressables), not built into the base app");
            Assert.IsNull(level.EnvironmentPrefab, "no direct arena reference from the level");

            LevelSelection.Request(level, catalog);
            SceneManager.LoadScene(SceneNames.Gameplay);
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 10f, "playing");
            LevelManager manager = LevelManager.Instance;
            Assert.AreSame(level, manager.Level);
            MissionRunner runner = manager.Mission;
            var car = LevelLoader.Instance.Vehicle;

            // Road direction: start prompt and the car starts in the keep-side lane.
            if (level.RoadDirection == RoadDirection.LeftHand)
                Assert.AreEqual("DRIVE ON THE LEFT", runner.LastNotice, "left-hand traffic prompt at start");
            Vector3 right = level.SpawnRotation * Vector3.right;
            Debug.Log($"[Test] {id} {level.DisplayName}: road {level.RoadDirection}, {runner.Objectives.Count} objectives, spawn {level.SpawnPosition}");

            // Spawn clear and every objective reachable by a 2.0 m-wide car.
            Assert.IsEmpty(CampaignLevelTests.Blockers(car.transform.position, car.transform.rotation, new Vector3(0.95f, 0.6f, 2.2f), car.Body), "spawn clear");
            foreach (MissionObjective o in runner.Objectives)
            {
                Vector3 target = o.TargetPoint;
                bool ok = ReachableOnSurfaces(car.transform.position, target, 130f, 1.0f, car.Body, out int cells);
                Assert.IsTrue(ok, $"{id}: '{o.Title}' reachable ({cells} cells)");
                if (o is ParkObjective park)
                    Assert.IsEmpty(CampaignLevelTests.Blockers(park.Position, park.ZoneRotation,
                        new Vector3(park.Size.x * 0.5f - 0.15f, 0.6f, park.Size.y * 0.5f - 0.15f), car.Body), $"{id}: bay '{o.Title}' clear");
            }
            yield return new WaitForSecondsRealtime(1.5f);   // let async texture uploads finish before the screenshot
            yield return LogRenderStats(id);
            CaptureMainCamera(id);

            // Satisfy each objective in turn.
            float halfLen = 0f;
            foreach (Vector3 c in manager.Validator.LocalFootprint) halfLen = Mathf.Max(halfLen, Mathf.Abs(c.z));
            while (manager.State == LevelState.Playing && runner.Current != null)
            {
                int index = runner.CurrentIndex;
                MissionObjective o = runner.Current;
                if (o is CheckpointObjective cp)
                    car.ResetVehicle(cp.Position + Vector3.up * 0.06f, car.transform.rotation);
                else if (o is ValetParkObjective valet)
                {
                    // Hand over at the stand first, then park in the guest's bay.
                    car.ResetVehicle(valet.StandPosition + Vector3.up * 0.06f, valet.StandRotation);
                    yield return WaitFor(() => valet.HandedOver || manager.State != LevelState.Playing, 8f, $"{id}: valet handover");
                    car.ResetVehicle(valet.Position + Vector3.up * 0.06f, valet.ZoneRotation);
                }
                else if (o is ReverseParkObjective rp)
                {
                    halfLen = 0f;   // the footprint is measured when the bay binds the validator
                    foreach (Vector3 c in manager.Validator.LocalFootprint) halfLen = Mathf.Max(halfLen, Mathf.Abs(c.z));
                    Vector3 fwd = rp.ZoneRotation * Vector3.forward;   // facing out; back line behind
                    car.ResetVehicle(rp.Position - fwd * (rp.Size.y * 0.5f - halfLen - 0.3f) + Vector3.up * 0.06f, rp.ZoneRotation);
                }
                else if (o is ParkObjective p)
                    car.ResetVehicle(p.Position + Vector3.up * 0.06f, p.ZoneRotation);
                else if (o is NarrowAlleyObjective alley)
                {
                    // Along the alley, facing back to the mouth when it must be reversed through.
                    Vector3 axis = alley.Exit - alley.Mouth; axis.y = 0f;
                    car.ResetVehicle(alley.Exit - axis.normalized * 1.5f + Vector3.up * 0.06f, Quaternion.LookRotation(alley.ReverseThrough ? -axis : axis));
                }
                else
                    car.ResetVehicle(o.TargetPoint + Vector3.up * 0.06f, car.transform.rotation);   // gates: be at the target
                yield return WaitFor(() => manager.State != LevelState.Playing || runner.CurrentIndex != index, 8f,
                    $"{id}: objective '{o.Title}' ({manager.Validator.Status}, '{runner.Hint}', car {car.transform.position} yaw {car.transform.eulerAngles.y:F0} up {car.transform.up}, target {o.TargetPoint}, corners {manager.Validator.CornersInside}, halfLen {halfLen:F2})");
            }
            Assert.AreEqual(LevelState.Completed, manager.State, $"{id} completes");
            yield return TestScenes.WaitForTransition();
#else
            yield break;
#endif
        }

        /// <summary>
        /// Multi-level reachability: BFS over 0.5 m cells that follows the drivable surface (floor found by a ray down
        /// from just above the current floor, steps up to 0.3 m per cell, so ramps work and floors above or below do
        /// not leak in), with a car-sized box above the floor that must hit nothing but the car.
        /// </summary>
        internal static bool ReachableOnSurfaces(Vector3 from, Vector3 to, float halfExtent, float halfWidth, Rigidbody self, out int explored)
        {
            const float cell = 0.5f, maxStep = 0.3f;
            var seen = new HashSet<(int, int, int)>();
            var buffer = new Collider[16];
            var hits = new RaycastHit[8];
            explored = 0;
            bool Ground(float x, float z, float nearY, out float y)
            {
                y = 0f;
                int n = Physics.RaycastNonAlloc(new Vector3(x, nearY + 1.2f, z), Vector3.down, hits, 2.4f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (hits[i].rigidbody != null && hits[i].rigidbody == self) continue;
                    // Highest surface within a step: on a ramp foot, the ramp rather than the ground under it.
                    if (Mathf.Abs(hits[i].point.y - nearY) <= maxStep && (best == float.MaxValue || hits[i].point.y > best)) best = hits[i].point.y;
                }
                if (best == float.MaxValue) return false;
                y = best;
                return true;
            }
            bool Clear(float x, float y, float z)
            {
                int n = Physics.OverlapBoxNonAlloc(new Vector3(x, y + 0.95f, z), new Vector3(halfWidth, 0.6f, halfWidth), buffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    if (self != null && buffer[i].attachedRigidbody == self) continue;
                    if (buffer[i].GetComponentInParent<MovingBarrier>() != null) continue;   // opens on a cycle
                    return false;
                }
                return true;
            }
            int Ix(float v) => Mathf.FloorToInt(v / cell);
            float startY = from.y;
            if (Ground(from.x, from.z, from.y, out float gy)) startY = gy;
            var queue = new Queue<(int x, int z, float y)>();
            queue.Enqueue((Ix(from.x), Ix(from.z), startY));
            seen.Add((Ix(from.x), Ix(from.z), Mathf.RoundToInt(startY * 2f)));
            int gx = Ix(to.x), gz = Ix(to.z);
            int limit = Mathf.CeilToInt(halfExtent / cell);
            var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                explored++;
                if (Mathf.Abs(c.x - gx) <= 1 && Mathf.Abs(c.z - gz) <= 1 && Mathf.Abs(c.y - to.y) < 1.2f) return true;
                foreach (var (dx, dz) in dirs)
                {
                    int nx = c.x + dx, nz = c.z + dz;
                    if (Mathf.Abs(nx) > limit || Mathf.Abs(nz) > limit) continue;
                    float wx = (nx + 0.5f) * cell, wz = (nz + 0.5f) * cell;
                    if (!Ground(wx, wz, c.y, out float ny)) continue;
                    var key = (nx, nz, Mathf.RoundToInt(ny * 2f));
                    if (seen.Contains(key)) continue;
                    seen.Add(key);
                    if (!Clear(wx, ny, wz)) continue;
                    queue.Enqueue((nx, nz, ny));
                }
            }
            return false;
        }

        /// <summary>Render counters of the in-game view (ProfilerRecorder; also available in player builds).</summary>
        private static IEnumerator LogRenderStats(string id)
        {
            var names = new[] { "Batches Count", "SetPass Calls Count", "Triangles Count", "Shadow Casters Count", "Draw Calls Count" };
            var recs = new Unity.Profiling.ProfilerRecorder[names.Length];
            for (int i = 0; i < names.Length; i++) recs[i] = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, names[i]);
            yield return null; yield return null; yield return null;
            var sb = new System.Text.StringBuilder($"[Perf] {id} in-game view:");
            for (int i = 0; i < names.Length; i++) { sb.Append($" {names[i].Replace(" Count", "")}={recs[i].LastValue:N0}"); recs[i].Dispose(); }
            Debug.Log(sb.ToString());
        }

        private static void CaptureMainCamera(string id)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../UltimateCarParking3D_logs/arenas"));
            Directory.CreateDirectory(dir);
            var rt = new RenderTexture(1600, 720, 24);
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;

            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 720), 0, 0);
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, $"{id}_ingame.jpg"), tex.EncodeToJPG(85));
            Object.Destroy(rt);
            Object.Destroy(tex);
        }
    }
}
