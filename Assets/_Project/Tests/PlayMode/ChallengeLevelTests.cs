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
using UltimateCarParking.Progression;
using UltimateCarParking.Save;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Phase 5 challenge chapter in the real Gameplay scene. Each level is played through objective by objective: the
    /// car is placed at the objective's target (gate, valet stand, bay) the way a player would finish it, and the level
    /// must complete with a parking scorecard. This proves detection, objectives and scoring work for every level; it does
    /// not prove a human can drive the route (that needs hands-on play).
    /// </summary>
    public class ChallengeLevelTests
    {
        private static LevelCatalog Campaign()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/LevelCatalog.asset");
#else
            return null;
#endif
        }

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() { Time.timeScale = 1f; yield return TestScenes.ResetToEmpty(); }

        private static VehicleController Car => LevelLoader.Instance.Vehicle;

        private static IEnumerator WaitFor(System.Func<bool> c, float timeout, string what)
        {
            float t = 0f;
            while (!c()) { t += Time.deltaTime; if (t > timeout) Assert.Fail("timed out: " + what); yield return null; }
        }

        private static IEnumerator Load(LevelData level)
        {
            LevelSelection.Request(level, Campaign());
            SceneManager.LoadScene(SceneNames.Gameplay);
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing && LevelManager.Instance.Level == level, 10f, level.LevelId + " playing");
            Car.Input.KeyboardEnabled = false;
            yield return new WaitForSeconds(0.5f);
        }

        private static void Place(Vector3 groundPoint, Quaternion rot)
        {
            // Put the car's footprint centre (not its pivot) on the point.
            ParkingValidator v = LevelManager.Instance.Validator;
            v.EnsureFootprint(Car);
            Vector3 c = Vector3.zero;
            foreach (Vector3 p in v.LocalFootprint) c += p;
            c /= v.LocalFootprint.Length;
            Vector3 pos = groundPoint - rot * new Vector3(c.x, 0f, c.z) + Vector3.up * 0.05f;
            Car.ResetVehicle(pos, rot);
        }

        /// <summary>Finishes the current objective the way a player would, trying a few final positions inside a bay.</summary>
        private static IEnumerator Finish(MissionRunner m, MissionObjective o, int index)
        {
            float t;
            switch (o)
            {
                case ExamRouteObjective exam:
                    while (m.CurrentIndex == index && exam.NextGate < exam.GateCount)
                    {
                        ExamGate g = exam.Gate(exam.NextGate);
                        Place(g.position, Quaternion.Euler(0f, g.yaw, 0f));
                        yield return new WaitForSeconds(g.stop ? 1.6f : 0.4f);
                    }
                    break;
                case CheckpointObjective cp:
                    Place(cp.Position, Quaternion.Euler(0f, 90f, 0f));
                    yield return new WaitForSeconds(0.4f);
                    break;
                case ParkObjective park:
                    if (o is ValetParkObjective)
                    {
                        // Phase 1: the valet stand (the objective's current target), hold for the handover.
                        Place(o.TargetPoint, Quaternion.Euler(0f, 90f, 0f));
                        for (t = 0f; t < 8f && Vector3.Distance(o.TargetPoint, park.Position) > 0.5f; t += 0.25f) yield return new WaitForSeconds(0.25f);
                    }
                    var offsets = new List<Vector2> { Vector2.zero };
                    for (float x = -0.5f; x <= 0.51f; x += 0.25f) for (float z = -0.6f; z <= 0.61f; z += 0.3f) if (x != 0f || z != 0f) offsets.Add(new Vector2(x, z));
                    offsets.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
                    foreach (Vector2 off in offsets)
                    {
                        if (m.CurrentIndex != index) break;
                        Quaternion rot = park.ZoneRotation;
                        Place(park.Position + rot * new Vector3(off.x, 0f, off.y), rot);
                        Car.Input.SetHandbrake(true);   // finishing in P (needed where the level requires the parking brake)
                        for (t = 0f; t < 3f && m.CurrentIndex == index; t += Time.deltaTime) yield return null;
                        Car.Input.SetHandbrake(false);
                    }
                    break;
                default:
                    Assert.Fail("no autopilot for " + o.GetType().Name);
                    break;
            }
        }

        private static IEnumerable<int> ChallengeIndices()
        {
            for (int i = 0; i < 12; i++) yield return 30 + i;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Each_Challenge_Level_Completes_And_Is_Scored([ValueSource(nameof(ChallengeIndices))] int catalogIndex)
        {
            LevelData level = Campaign().Get(catalogIndex);
            yield return Load(level);
            MissionRunner m = LevelManager.Instance.Mission;
            Assert.AreEqual(0, LevelManager.Instance.Collisions, "spawn is clear of the props");
            if (level.ForcedCar != null) StringAssert.Contains("Monster", LevelLoader.Instance.Vehicle.name, "the heavy-vehicle level forces the monster truck");
            int guard = 0;
            while (LevelManager.Instance.State == LevelState.Playing && guard++ < 10)
            {
                int idx = m.CurrentIndex;
                yield return Finish(m, m.Current, idx);
                if (m.CurrentIndex == idx && LevelManager.Instance.State == LevelState.Playing)
                    Assert.Fail($"{level.LevelId}: objective {idx + 1} '{m.Current.Title}' did not complete (hint: {m.Hint})");
            }
            yield return WaitFor(() => LevelManager.Instance.State != LevelState.Playing, 4f, "level end");
            Assert.AreEqual(LevelState.Completed, LevelManager.Instance.State, level.LevelId + " completes (" + LevelManager.Instance.FailDetail + ")");
            LevelResult r = LevelManager.Instance.LastResult.Value;
            Debug.Log($"[Challenge] {level.LevelId} {level.DisplayName} ({level.Difficulty}, {level.MissionType}): {r.Stars}★ quality {r.Quality:F2}  {r.ScoreLabel}");
            StringAssert.Contains("PARKING", r.ScoreLabel, "parking scorecard on the results");
            Assert.GreaterOrEqual(r.Stars, 1);
            Assert.Greater(ProgressionService.BestStars(SaveManager.Current.Data, level), 0, "result saved");
        }

        [UnityTest]
        public IEnumerator A_Sloppy_Park_Earns_Fewer_Stars_Than_A_Clean_One_And_Restart_Resets()
        {
            LevelData level = Campaign().Get(30);   // C01, Beginner
            yield return Load(level);
            var park = (ParkObjective)LevelManager.Instance.Mission.Current;
            Place(park.Position, park.ZoneRotation);
            yield return WaitFor(() => LevelManager.Instance.State == LevelState.Completed, 5f, "clean park");
            LevelResult clean = LevelManager.Instance.LastResult.Value;

            LevelManager.Instance.RestartLevel();
            yield return WaitFor(() => LevelManager.Instance.State == LevelState.Playing, 6f, "restart");
            Assert.AreEqual(0, LevelManager.Instance.Mission.Scorecards.Count, "restart clears the score");
            Assert.AreEqual(0, LevelManager.Instance.Collisions);
            park = (ParkObjective)LevelManager.Instance.Mission.Current;
            // Off-centre and 7° crooked, but still fully inside the lines (so it parks, just badly).
            Quaternion crooked = park.ZoneRotation * Quaternion.Euler(0f, 7f, 0f);
            Place(park.Position + park.ZoneRotation * new Vector3(0.35f, 0f, 0.4f), crooked);
            yield return WaitFor(() => LevelManager.Instance.State == LevelState.Completed, 5f, "sloppy park");
            LevelResult sloppy = LevelManager.Instance.LastResult.Value;
            Debug.Log($"[Challenge] C01 clean: {clean.Stars}★ {clean.Quality:F2} | sloppy: {sloppy.Stars}★ {sloppy.Quality:F2}");
            Assert.AreEqual(3, clean.Stars);
            Assert.Less(sloppy.Quality, clean.Quality - 0.15f);
            Assert.Less(sloppy.Stars, clean.Stars);
            Assert.AreEqual(3, ProgressionService.BestStars(SaveManager.Current.Data, level), "best result kept");
        }

        [UnityTest]
        public IEnumerator Exam_Requires_The_Parking_Brake_And_Counts_A_Missed_Stop()
        {
            LevelData level = Campaign().Get(41);   // C12 Driving Exam
            yield return Load(level);
            MissionRunner m = LevelManager.Instance.Mission;
            var exam = (ExamRouteObjective)m.Current;
            // Drive straight through the stop line without stopping: one fault.
            ExamGate g0 = exam.Gate(0), stop = exam.Gate(1);
            Place(g0.position, Quaternion.Euler(0f, 90f, 0f));
            yield return new WaitForSeconds(0.4f);
            Place(stop.position, Quaternion.Euler(0f, 90f, 0f));
            Car.GetComponent<Rigidbody>().linearVelocity = Car.transform.forward * 5f;   // rolling through
            yield return new WaitForSeconds(0.2f);
            Place(exam.Gate(2).position, Quaternion.Euler(0f, 90f, 0f));
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1, exam.Faults, "rolling through the stop line is a fault");
            yield return WaitFor(() => m.CurrentIndex == 1, 3f, "exam route done");
            var park = (ParkObjective)m.Current;
            Place(park.Position + park.ZoneRotation * new Vector3(0f, 0f, -0.3f), park.ZoneRotation);
            Car.Input.SetHandbrake(false);
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(LevelState.Playing, LevelManager.Instance.State, "not parked without P / handbrake");
            StringAssert.Contains("HANDBRAKE", m.Hint);
            Car.Input.SetHandbrake(true);
            yield return WaitFor(() => LevelManager.Instance.State == LevelState.Completed, 6f, "parked with the handbrake");
            StringAssert.Contains("EXAM: 1 FAULT", LevelManager.Instance.LastResult.Value.ScoreLabel);
        }
    }
}
