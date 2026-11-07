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
    /// Reusable mechanics, batch 1: precision, timed, moving barrier, narrow alley, valet. Each runs through the real
    /// Gameplay scene (MissionRunner, validator, collision counter, LevelManager scoring). Barrier, alley walls and
    /// the valet test track use imported prop prefabs placed on a flat test pad away from the Driving School yard.
    /// </summary>
    public class MechanicsBatch1Tests
    {
        private const string CatalogPath = "Assets/_Project/Levels/LevelCatalog.asset";
        private static readonly Vector3 Pad = new Vector3(0f, 0f, -150f);
        private static readonly Vector3 Bay2 = new Vector3(-21.5f, 0f, 7.25f);

        private static LevelManager Manager => LevelManager.Instance;
        private static MissionRunner Runner => LevelManager.Instance.Mission;
        private static VehicleController Car => LevelLoader.Instance.Vehicle;
        private readonly List<Object> spawned = new List<Object>();
        private LevelData level;

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object o in spawned) if (o != null) Object.Destroy(o);
            spawned.Clear();
            if (level != null) Object.Destroy(level);
            yield return TestScenes.ResetToEmpty();
        }

#if UNITY_EDITOR
        private IEnumerator Load(ScoreMode mode, float timeLimit, int collisionLimit, params MissionObjective[] objectives)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            level = Object.Instantiate(catalog.Get(0));
            var so = new SerializedObject(level);
            SerializedProperty list = so.FindProperty("objectives");
            list.arraySize = objectives.Length;
            for (int i = 0; i < objectives.Length; i++) list.GetArrayElementAtIndex(i).managedReferenceValue = objectives[i];
            so.FindProperty("scoreMode").enumValueIndex = (int)mode;
            so.FindProperty("timeLimitSeconds").floatValue = timeLimit;
            so.FindProperty("collisionLimit").intValue = collisionLimit;
            so.FindProperty("threeStarMaxCollisions").intValue = 0;
            so.FindProperty("twoStarMaxCollisions").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            LevelSelection.Request(level, catalog);
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while ((Manager == null || Manager.State != LevelState.Playing) && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(LevelState.Playing, Manager.State, "level starts");
            Car.Input.KeyboardEnabled = false;
            yield return new WaitForSeconds(0.3f);
        }

        private GameObject Spawn(string prefab, Vector3 pos, float yaw)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Real/{prefab}.prefab");
            Assert.IsNotNull(p, prefab);
            GameObject go = Object.Instantiate(p, pos, Quaternion.Euler(0f, yaw, 0f));
            spawned.Add(go);
            return go;
        }
#endif

        /// <summary>A flat 80 x 80 m pad (physics only; the props on it are imported models).</summary>
        private void BuildPad()
        {
            var g = new GameObject("TestPad");
            g.transform.position = Pad + Vector3.down * 0.5f;
            g.AddComponent<BoxCollider>().size = new Vector3(80f, 1f, 80f);
            spawned.Add(g);
        }

        private static IEnumerator Place(Vector3 pos, float yaw, float settle = 0.4f)
        {
            Car.ResetVehicle(pos + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            yield return new WaitForSeconds(settle);
        }

        private static IEnumerator Until(System.Func<bool> c, float seconds)
        {
            float t = 0f;
            while (!c() && t < seconds) { t += Time.deltaTime; yield return null; }
        }

        private static float HalfWidth()
        {
            float w = 0f;
            foreach (Vector3 c in Manager.Validator.LocalFootprint) w = Mathf.Max(w, Mathf.Abs(c.x));
            return w;
        }

        // ------------------------------------------------------------------ precision

        [UnityTest]
        public IEnumerator Precision_Rejects_An_Off_Centre_Park_And_Scores_Accuracy()
        {
#if UNITY_EDITOR
            var obj = new PrecisionParkObjective("Park dead centre", Bay2, 0f, new Vector2(3f, 5.5f), ParkDirection.Either, 0.35f, 5f, 0.8f);
            yield return Load(ScoreMode.Quality, 0f, -1, obj);
            obj = (PrecisionParkObjective)Runner.Current;
            // Inside the lines but 0.18 m off the centre line: not accepted.
            yield return Place(Bay2 + new Vector3(0.18f, 0f, 0f), 0f, 3f);
            Assert.AreEqual(LevelState.Playing, Manager.State, "off-centre park not accepted");
            Assert.AreEqual(ParkingStatus.Unpositioned, Manager.Validator.Status);
            StringAssert.Contains("CENTRE", Runner.Hint);
            Assert.Less(obj.Accuracy, obj.MinAccuracy);
            // Dead centre: accepted with high accuracy and three stars.
            yield return Place(Bay2, 0f, 0.2f);
            yield return Until(() => Manager.State != LevelState.Playing, 5f);
            Assert.AreEqual(LevelState.Completed, Manager.State);
            LevelResult r = Manager.LastResult.Value;
            Debug.Log("[Test] precision " + r);
            Assert.Greater(r.Quality, 0.85f);
            StringAssert.StartsWith("ACCURACY", r.ScoreLabel);
            Assert.AreEqual(3, r.Stars);
#else
            yield break;
#endif
        }

        // ------------------------------------------------------------------ timed

        [UnityTest]
        public IEnumerator Timed_Fails_When_The_Clock_Runs_Out()
        {
#if UNITY_EDITOR
            yield return Load(ScoreMode.Quality, 0f, -1, new TimedParkObjective("Beat the clock", Bay2, 0f, new Vector2(3f, 5.5f), ParkDirection.Either, 3f));
            Assert.IsTrue(Runner.Gauge.Visible);
            Assert.AreEqual("CLOCK", Runner.Gauge.Caption);
            yield return Until(() => Manager.State != LevelState.Playing, 6f);
            Assert.AreEqual(LevelState.Failed, Manager.State);
            Assert.AreEqual(FailReason.ObjectiveFailed, Manager.LastResult.Value.FailReason);
            Assert.AreEqual("OUT OF TIME", Manager.FailDetail);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Timed_Scores_The_Time_Left()
        {
#if UNITY_EDITOR
            yield return Load(ScoreMode.Quality, 0f, -1, new TimedParkObjective("Beat the clock", Bay2, 0f, new Vector2(3f, 5.5f), ParkDirection.Either, 20f));
            yield return Place(Bay2, 0f, 0.1f);
            yield return Until(() => Manager.State != LevelState.Playing, 6f);
            Assert.AreEqual(LevelState.Completed, Manager.State);
            LevelResult r = Manager.LastResult.Value;
            Debug.Log("[Test] timed " + r);
            StringAssert.StartsWith("TIME LEFT", r.ScoreLabel);
            Assert.Greater(r.Quality, 0.8f, "parked within ~2 s of a 20 s clock");
            Assert.Less(r.Quality, 1f);
#else
            yield break;
#endif
        }

        // ------------------------------------------------------------------ moving barrier

#if UNITY_EDITOR
        /// <summary>Sliding gate across x = pad, made of the imported metal barrier; returns the barrier.</summary>
        private MovingBarrier BuildGate(float closed, float open)
        {
            BuildPad();
            var root = new GameObject("Gate");
            root.transform.position = Pad;
            spawned.Add(root);
            GameObject panel = Spawn("Barrier_Metal", Pad, 0f);
            Bounds b = RealBounds(panel);
            bool longAlongX = b.size.x >= b.size.z;
            panel.transform.SetParent(root.transform, true);
            // Lay the panel across the lane (lane runs along +x, so the gate spans z).
            panel.transform.localRotation = Quaternion.Euler(0f, longAlongX ? 90f : 0f, 0f);
            float span = Mathf.Max(b.size.x, b.size.z);
            var moving = new GameObject("Moving").transform;
            moving.SetParent(root.transform, false);
            panel.transform.SetParent(moving, false);
            panel.transform.localPosition = Vector3.zero;
            var gate = root.AddComponent<MovingBarrier>();
            gate.Configure("test_gate", moving, MovingBarrier.Motion.Slide, new Vector3(0f, 0f, span + 1f), closed, open, 1f);
            // Make the panel wide enough to block a car: repeat it once more beside itself.
            GameObject second = Spawn("Barrier_Metal", Pad, 0f);
            second.transform.SetParent(moving, false);
            second.transform.localRotation = panel.transform.localRotation;
            second.transform.localPosition = new Vector3(0f, 0f, span);
            gate.Configure("test_gate", moving, MovingBarrier.Motion.Slide, new Vector3(0f, 0f, span * 2f + 1f), closed, open, 1f);
            return gate;
        }

        private static Bounds RealBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }
#endif

        [UnityTest]
        public IEnumerator Barrier_Cycles_Holds_For_A_Car_And_Lets_It_Through()
        {
#if UNITY_EDITOR
            yield return Load(ScoreMode.TimeAndCollisions, 0f, -1);
            MovingBarrier gate = BuildGate(2f, 3f);
            Assert.IsNotEmpty(gate.Moving.GetComponentsInChildren<Collider>(), "imported barrier has colliders");
            Assert.AreEqual(0f, gate.OpenFraction, 0.01f);
            Vector3 closedPos = gate.Moving.position;
            Assert.Greater(gate.SecondsUntilOpen, 2.5f);
            yield return Until(() => gate.IsPassable, 5f);
            Assert.IsTrue(gate.IsPassable, "opens after the closed time + travel");
            yield return Until(() => gate.Current == MovingBarrier.Phase.Open, 1f);
            Assert.Greater(Vector3.Distance(gate.Moving.position, closedPos), 3f, "the barrier model actually moved aside");
            Assert.AreEqual(0f, gate.SecondsUntilOpen);

            // Park the car in the gate's path while it is about to close: the safety sensor holds it.
            yield return Place(closedPos + new Vector3(0f, 0f, 0f), 90f, 0.2f);
            yield return new WaitForSeconds(3.5f);
            Assert.IsTrue(gate.HeldBySensor, "closing held while the car is in the way");
            Assert.Greater(gate.OpenFraction, 0.5f, "not closed onto the car");
            yield return Place(Pad + new Vector3(15f, 0f, 0f), 90f, 0.2f);
            yield return Until(() => gate.OpenFraction < 0.05f, 4f);
            Assert.Less(gate.OpenFraction, 0.05f, "closes once the car has gone");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Barrier_Objective_Waits_Counts_Hits_And_Fails()
        {
#if UNITY_EDITOR
            var obj = new BarrierGateObjective("Through the gate", "test_gate", Pad + new Vector3(6f, 0f, 0f), 90f, new Vector2(4f, 6f), maxBarrierHits: 0);
            yield return Load(ScoreMode.TimeAndCollisions, 0f, -1);
            MovingBarrier gate = BuildGate(100f, 3f);       // stays closed
            // Restart the mission now that the barrier exists (objectives look it up when they begin).
            var so = new SerializedObject(level);
            so.FindProperty("objectives").arraySize = 1;
            so.FindProperty("objectives").GetArrayElementAtIndex(0).managedReferenceValue = obj;
            so.ApplyModifiedPropertiesWithoutUndo();
            Runner.Prepare(level, Car);
            Runner.Run();
            yield return null;
            Assert.AreEqual("WAIT FOR THE BARRIER", Runner.Hint);
            Assert.AreEqual("OPENS IN", Runner.Gauge.Caption);
            // Drive into the closed barrier.
            yield return Place(Pad + new Vector3(-8f, 0f, 1.2f), 90f, 0.4f);
            Car.Input.SetThrottle(0.6f);
            yield return Until(() => Manager.State != LevelState.Playing, 8f);
            Car.Input.SetThrottle(0f);
            Assert.AreEqual(LevelState.Failed, Manager.State, "hitting the barrier with 0 hits allowed fails");
            Assert.AreEqual("HIT THE BARRIER", Manager.FailDetail);
            Assert.AreEqual(0f, gate.OpenFraction, 0.01f);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Barrier_Objective_Completes_Once_Through()
        {
#if UNITY_EDITOR
            yield return Load(ScoreMode.TimeAndCollisions, 0f, -1);
            MovingBarrier gate = BuildGate(1f, 30f);
            var so = new SerializedObject(level);
            so.FindProperty("objectives").arraySize = 1;
            so.FindProperty("objectives").GetArrayElementAtIndex(0).managedReferenceValue =
                new BarrierGateObjective("Through the gate", "test_gate", Pad + new Vector3(6f, 0f, 0f), 90f, new Vector2(4f, 6f));
            so.ApplyModifiedPropertiesWithoutUndo();
            Runner.Prepare(level, Car);
            Runner.Run();
            yield return Place(Pad + new Vector3(-8f, 0f, 1.2f), 90f, 0.2f);
            yield return Until(() => gate.IsPassable, 4f);
            Assert.AreEqual("GATE OPEN - DRIVE THROUGH", Runner.Hint);
            Car.Input.SetThrottle(0.5f);
            yield return Until(() => Manager.State != LevelState.Playing, 10f);
            Car.Input.SetThrottle(0f);
            Assert.AreEqual(LevelState.Completed, Manager.State, $"drove through the open gate ({Car.transform.position})");
#else
            yield break;
#endif
        }

        // ------------------------------------------------------------------ narrow alley

#if UNITY_EDITOR
        /// <summary>Two walls of imported concrete barriers along +x, <paramref name="gap"/> metres apart (inner faces).</summary>
        private void BuildAlley(float gap, float length)
        {
            BuildPad();
            GameObject probe = Spawn("Barrier_Concrete", Pad, 0f);
            Bounds b = RealBounds(probe);
            Object.Destroy(probe);
            bool longAlongX = b.size.x >= b.size.z;
            float seg = Mathf.Max(b.size.x, b.size.z), thick = Mathf.Min(b.size.x, b.size.z);
            float yaw = longAlongX ? 0f : 90f;
            for (float x = seg * 0.5f; x < length; x += seg * 0.98f)
                foreach (float s in new[] { -1f, 1f })
                    Spawn("Barrier_Concrete", Pad + new Vector3(x, 0f, s * (gap * 0.5f + thick * 0.5f)), yaw);
        }
#endif

        [UnityTest]
        public IEnumerator Alley_Measures_Side_Gaps_And_Completes_Clean()
        {
#if UNITY_EDITOR
            var obj = new NarrowAlleyObjective("Squeeze through", Pad, Pad + new Vector3(24f, 0f, 0f), 3.0f, maxScrapes: 1);
            yield return Load(ScoreMode.Quality, 0f, -1, obj);
            obj = (NarrowAlleyObjective)Runner.Current;
            BuildAlley(3.0f, 24f);
            yield return Place(Pad + new Vector3(2f, 0f, 0f), 90f, 0.4f);
            yield return null;
            float expected = 1.5f - HalfWidth();
            Assert.AreEqual("SIDE GAP", Runner.Gauge.Caption);
            Assert.AreEqual(expected, obj.LeftGap, 0.15f, "left sensor reads the wall gap");
            Assert.AreEqual(expected, obj.RightGap, 0.15f, "right sensor reads the wall gap");
            Car.Input.SetThrottle(0.35f);
            yield return Until(() => Manager.State != LevelState.Playing, 15f);
            Car.Input.SetThrottle(0f);
            Assert.AreEqual(LevelState.Completed, Manager.State, $"through the alley ({Car.transform.position}, '{Runner.Hint}')");
            LevelResult r = Manager.LastResult.Value;
            Debug.Log($"[Test] alley {r}, gaps expected {expected:F2}");
            StringAssert.StartsWith("TIGHTEST GAP", r.ScoreLabel);
            Assert.AreEqual(0, obj.Scrapes);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Alley_Scrapes_Fail_And_Reverse_Alley_Needs_Reversing()
        {
#if UNITY_EDITOR
            var obj = new NarrowAlleyObjective("Squeeze through", Pad, Pad + new Vector3(24f, 0f, 0f), 3.0f, maxScrapes: 0);
            yield return Load(ScoreMode.Quality, 0f, -1, obj);
            BuildAlley(3.0f, 24f);
            yield return Place(Pad + new Vector3(3f, 0f, 0f), 90f, 0.4f);
            Car.Input.SetSteering(1f);
            Car.Input.SetThrottle(0.6f);
            yield return Until(() => Manager.State != LevelState.Playing, 8f);
            Car.Input.ResetMobileInput();
            Assert.AreEqual(LevelState.Failed, Manager.State, "scraping the wall with 0 scrapes allowed fails");
            StringAssert.StartsWith("SCRAPED THE WALLS", Manager.FailDetail);

            // Reverse variant: arriving nose-first is not accepted; arriving backwards is.
            var rev = new NarrowAlleyObjective("Reverse through", Pad, Pad + new Vector3(24f, 0f, 0f), 3.0f, maxScrapes: -1, reverseThrough: true);
            var so = new SerializedObject(level);
            so.FindProperty("objectives").GetArrayElementAtIndex(0).managedReferenceValue = rev;
            so.ApplyModifiedPropertiesWithoutUndo();
            Manager.RestartLevel();
            yield return Until(() => Manager != null && Manager.State == LevelState.Playing && Runner.Current is NarrowAlleyObjective, 10f);
            Car.Input.KeyboardEnabled = false;
            yield return Place(Pad + new Vector3(22.5f, 0f, 0f), 90f, 1f);
            Assert.AreEqual(LevelState.Playing, Manager.State, "nose-first is not a reverse");
            StringAssert.Contains("REVERSE", Runner.Hint);
            yield return Place(Pad + new Vector3(22.5f, 0f, 0f), -90f, 0.2f);
            yield return Until(() => Manager.State != LevelState.Playing, 3f);
            Assert.AreEqual(LevelState.Completed, Manager.State, "arrived reversing");
#else
            yield break;
#endif
        }

        // ------------------------------------------------------------------ valet

        [UnityTest]
        public IEnumerator Valet_Handover_Then_Park_With_Guest_Rating()
        {
#if UNITY_EDITOR
            Vector3 stand = Pad + new Vector3(-20f, 0f, 0f);
            Vector3 bay = Pad + new Vector3(25f, 0f, 10f);
            var obj = new ValetParkObjective("Valet the guest's car", stand, 90f, bay, 0f, new Vector2(3f, 5.5f), ParkDirection.Either, 18f);
            yield return Load(ScoreMode.Quality, 0f, -1, obj);
            obj = (ValetParkObjective)Runner.Current;
            BuildPad();
            Assert.AreEqual("PULL UP AT THE VALET STAND", Runner.Hint);
            Assert.AreEqual("GUEST RATING", Runner.Gauge.Caption);
            yield return Place(stand, 90f, 0.3f);
            yield return Until(() => obj.HandedOver, 4f);
            Debug.Log($"[Test] valet stand: car {Car.transform.position} speed {Car.SpeedKmh:F1} inStand {obj.InStand(Car.transform.position)} hint '{Runner.Hint}' rating {obj.Rating:F0} state {Manager.State}");
            Assert.IsTrue(obj.HandedOver, "keys handed over after holding still at the stand");
            Assert.Less(Vector3.Distance(LevelLoader.Instance.Zone.Center, bay), 0.05f, "guide moved to the guest's bay");
            // Floor it: speeding and harsh acceleration cost rating.
            Car.Input.SetThrottle(1f);
            yield return new WaitForSeconds(3f);
            Car.Input.SetThrottle(0f);
            Car.Input.SetBrake(1f);
            yield return new WaitForSeconds(1.5f);
            Car.Input.ResetMobileInput();
            float rating = obj.Rating;
            Debug.Log($"[Test] valet rating after speeding {rating:F0}%, last notice '{Runner.LastNotice}'");
            Assert.Less(rating, 95f, "rough driving lowers the guest rating");
            Assert.Greater(rating, 0f);
            yield return Place(bay, 0f, 0.2f);
            yield return Until(() => Manager.State != LevelState.Playing, 5f);
            Assert.AreEqual(LevelState.Completed, Manager.State);
            LevelResult r = Manager.LastResult.Value;
            StringAssert.StartsWith("GUEST RATING", r.ScoreLabel);
            Assert.AreEqual(rating / 100f, r.Quality, 0.05f);
            Assert.Less(r.Stars, 3, "a rough valet does not earn three stars");
#else
            yield break;
#endif
        }
    }
}
