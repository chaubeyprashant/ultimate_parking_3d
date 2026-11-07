using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.Parking;
using UltimateCarParking.UI;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Mission system, Phase C: standard (L01), reverse (L02) and parallel (L03) parking in the Driving School yard,
    /// loaded through the normal Gameplay flow. Each mission's own rule is checked both ways (accepted / rejected),
    /// the reverse bay is also driven into with real input, and kerb strikes are produced by driving onto the kerb.
    /// Renders go to ../UltimateCarParking3D_logs/missions/*.png.
    /// </summary>
    public class MissionTests
    {
        private const string CatalogPath = "Assets/_Project/Levels/LevelCatalog.asset";

        private static LevelManager Manager => LevelManager.Instance;
        private static MissionRunner Runner => LevelManager.Instance.Mission;
        private static VehicleController Car => LevelLoader.Instance.Vehicle;

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.ResetToEmpty();

        private static LevelCatalog Catalog()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
#else
            return null;
#endif
        }

        private static IEnumerator Load(LevelData level)
        {
            LevelSelection.Request(level, Catalog());
            SceneManager.LoadScene(SceneNames.Gameplay);
            float t = 0f;
            while ((Manager == null || Manager.State != LevelState.Playing) && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(LevelState.Playing, Manager.State, "level starts");
            Car.Input.KeyboardEnabled = false;
            yield return new WaitForSeconds(0.3f);
        }

        private static IEnumerator Load(int number) => Load(Catalog().Get(number - 1));

        private static IEnumerator Place(Vector3 pos, float yaw, float settle = 0.4f)
        {
            Car.ResetVehicle(pos + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            yield return new WaitForSeconds(settle);
        }

        private static IEnumerator WaitWhilePlaying(float seconds)
        {
            float t = 0f;
            while (Manager.State == LevelState.Playing && t < seconds) { t += Time.deltaTime; yield return null; }
        }

        private static GameplayHud Hud => Object.FindAnyObjectByType<GameplayHud>();

        /// <summary>Half the car's length, from the validator's measured footprint.</summary>
        private static float HalfLength()
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector3 c in Manager.Validator.LocalFootprint) { min = Mathf.Min(min, c.z); max = Mathf.Max(max, c.z); }
            return (max - min) * 0.5f;
        }

        private static float HalfWidth()
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector3 c in Manager.Validator.LocalFootprint) { min = Mathf.Min(min, c.x); max = Mathf.Max(max, c.x); }
            return (max - min) * 0.5f;
        }

        // ---------------------------------------------------------------- data

        [Test]
        public void First_Three_Levels_Are_Three_Different_Missions()
        {
            LevelCatalog catalog = Catalog();
            Assert.IsTrue(MissionTypeInfo.TableMatchesEnum(), "mission title table covers every type");
            LevelData l1 = catalog.Get(0), l2 = catalog.Get(1), l3 = catalog.Get(2);
            Assert.AreEqual(MissionType.StandardParking, l1.MissionType);
            Assert.AreEqual(MissionType.ReverseParking, l2.MissionType);
            Assert.AreEqual(MissionType.ParallelParking, l3.MissionType);
            Assert.IsInstanceOf<ParkObjective>(l1.CreateObjectives()[0]);
            Assert.IsInstanceOf<ReverseParkObjective>(l2.CreateObjectives()[0]);
            Assert.IsInstanceOf<ParallelParkObjective>(l3.CreateObjectives()[0]);
            foreach (LevelData l in new[] { l1, l2, l3 })
            {
                Assert.IsNotNull(l.EnvironmentPrefab, $"{l.LevelId} uses the real-prop environment");
                Assert.IsNotEmpty(l.Briefing, $"{l.LevelId} has a briefing");
            }
            // Generated levels without objectives still get one standard parking objective.
            LevelData legacy = catalog.Get(9);
            Assert.IsFalse(legacy.HasObjectives);
            Assert.AreEqual(1, legacy.CreateObjectives().Length);
        }

        // ---------------------------------------------------------------- standard

        [UnityTest]
        public IEnumerator Standard_Shows_The_Mission_And_Completes_Either_Way_Round()
        {
            yield return Load(1);
            Assert.AreEqual(1, Runner.Objectives.Count);
            Assert.AreEqual(0, Runner.CurrentIndex);
            Assert.AreEqual(1, Hud.ObjectiveRowCount, "objective row on the HUD");
            StringAssert.Contains("BAY 4", Hud.ObjectiveRowText(0));
            StringAssert.Contains("Lesson 1", Hud.BannerText);
            StringAssert.Contains("DRIVE TO THE BAY", Hud.GuideText);

            var park = (ParkObjective)Runner.Current;
            // Reversed in is fine for standard parking.
            yield return Place(park.Position, 180f);
            yield return WaitWhilePlaying(5f);
            Assert.AreEqual(LevelState.Completed, Manager.State, $"completes ({Manager.Validator.Status})");
            StringAssert.Contains("DONE", Hud.ObjectiveRowText(0), "objective row ticked");
            Assert.IsTrue(Runner.IsComplete);
        }

        // ---------------------------------------------------------------- reverse

        [UnityTest]
        public IEnumerator Reverse_Rejects_Nose_In_And_Shallow_Parking()
        {
            yield return Load(2);
            var rev = (ReverseParkObjective)Runner.Current;
            float backLine = rev.Position.z + rev.Size.y * 0.5f;

            // Nose in, fully inside: the wrong way round.
            yield return Place(rev.Position, 0f);
            yield return WaitWhilePlaying(2.5f);
            Assert.AreEqual(LevelState.Playing, Manager.State, "nose-in does not complete");
            StringAssert.Contains("WRONG WAY", Runner.Hint);

            // Facing out but 1.0 m short of the back line.
            yield return Place(new Vector3(rev.Position.x, 0f, backLine - 1.0f - HalfLength()), 180f);
            yield return WaitWhilePlaying(2.5f);
            Assert.AreEqual(LevelState.Playing, Manager.State, "shallow does not complete");
            Assert.AreEqual(ParkingStatus.Unpositioned, Manager.Validator.Status);
            StringAssert.Contains("BACK IN FURTHER", Runner.Hint);
            Assert.That(rev.RearGap, Is.InRange(0.9f, 1.1f), "measured rear gap");
            Assert.AreNotEqual("", Hud.GaugeText, "rear sensor gauge shown");
        }

        [UnityTest]
        public IEnumerator Reverse_Driven_In_With_Real_Input_Completes_And_Sensor_Beeps()
        {
            yield return Load(2);
            var rev = (ReverseParkObjective)Runner.Current;
            float backLine = rev.Position.z + rev.Size.y * 0.5f;
            var beeper = Object.FindAnyObjectByType<ParkingSensorAudio>();
            int beepsBefore = beeper.BeepCount;

            // Stopped in the lane, square to the bay, facing away from it: reverse straight back.
            yield return Place(new Vector3(rev.Position.x, 0f, 0.5f), 180f, 0.6f);
            float closest = float.PositiveInfinity;
            Car.Input.SetThrottle(-0.6f);
            float t = 0f;
            float stopAt = backLine - 0.4f - HalfLength();    // aim for the rear bumper ~0.4 m from the line
            // Closed loop, like a driver watching the sensor: fast while far, creep near the line, brake if creeping too fast.
            while (stopAt - Car.transform.position.z > 0.03f && t < 20f)
            {
                t += Time.deltaTime;
                float remaining = stopAt - Car.transform.position.z;
                float speed = Car.SpeedKmh;
                float wanted = remaining > 2.5f ? 9f : Mathf.Lerp(1.5f, 5f, remaining / 2.5f);
                Car.Input.SetThrottle(speed < wanted ? (remaining > 2.5f ? -0.5f : -0.2f) : 0f);
                Car.Input.SetBrake(speed > wanted + 1.5f ? 0.6f : 0f);
                if (Hud.GaugeText.EndsWith(" m")) closest = Mathf.Min(closest, float.Parse(Hud.GaugeText.Replace(" m", ""), System.Globalization.CultureInfo.InvariantCulture));
                yield return null;
            }
            Car.Input.SetThrottle(0f);
            Car.Input.SetBrake(1f);
            yield return WaitWhilePlaying(6f);
            Car.Input.SetBrake(0f);
            Debug.Log($"[Test] Reverse drive: stopped at z {Car.transform.position.z:F2} (stop target {stopAt:F2}), rear gap {rev.RearGap:F2} m, " +
                      $"closest sensor reading {closest:F1} m, beeps {beeper.BeepCount - beepsBefore}, state {Manager.State}");
            Assert.AreEqual(LevelState.Completed, Manager.State, $"reversed in completes ({Manager.Validator.Status}, hint '{Runner.Hint}')");
            Assert.LessOrEqual(rev.RearGap, rev.MaxRearGap);
            Assert.Greater(beeper.BeepCount, beepsBefore, "parking sensor beeped while reversing toward the barrier");
            Assert.Less(closest, 1.6f, "sensor read the real barrier behind the bay");
        }

        // ---------------------------------------------------------------- parallel

        [UnityTest]
        public IEnumerator Parallel_Needs_The_Kerb_Gap()
        {
            yield return Load(3);
            var par = (ParallelParkObjective)Runner.Current;
            float kerbZ = par.Position.z - par.Size.x * 0.5f;    // slot faces +X, kerb on the car's right (−Z)

            // In the slot, straight, but 0.62 m from the kerb.
            yield return Place(new Vector3(par.Position.x, 0f, kerbZ + 0.62f + HalfWidth()), 90f);
            yield return WaitWhilePlaying(2.5f);
            Assert.AreEqual(LevelState.Playing, Manager.State, "too far from the kerb does not complete");
            StringAssert.Contains("CLOSER TO THE KERB", Runner.Hint);
            Assert.That(par.KerbGap, Is.InRange(0.55f, 0.7f));
            StringAssert.Contains("STRIKES 0/2", Runner.Gauge.Caption);

            // 0.25 m from the kerb: parked.
            yield return Place(new Vector3(par.Position.x, 0f, kerbZ + 0.25f + HalfWidth()), 90f);
            yield return WaitWhilePlaying(5f);
            Assert.AreEqual(LevelState.Completed, Manager.State, $"close to the kerb completes ({Manager.Validator.Status}, '{Runner.Hint}')");
            Assert.AreEqual(0, par.KerbStrikes);
        }

        [UnityTest]
        public IEnumerator Parallel_Fails_After_Too_Many_Kerb_Strikes()
        {
            yield return Load(3);
            var par = (ParallelParkObjective)Runner.Current;
            float kerbZ = par.Position.z - par.Size.x * 0.5f;
            string lastNotice = "";
            Runner.Notice += (m, bad) => lastNotice = m;

            for (int attempt = 1; attempt <= par.KerbStrikeLimit + 1 && Manager.State == LevelState.Playing; attempt++)
            {
                // West of the slots, angled toward the kerb, creep forward until a wheel climbs it.
                yield return Place(new Vector3(-24f, 0f, kerbZ + 2.2f), 90f + 28f, 1.0f);
                int before = par.KerbStrikes;
                Car.Input.SetThrottle(0.35f);
                float t = 0f;
                while (par.KerbStrikes == before && Manager.State == LevelState.Playing && t < 6f) { t += Time.deltaTime; yield return null; }
                Car.Input.SetThrottle(0f);
                Debug.Log($"[Test] Kerb attempt {attempt}: strikes {par.KerbStrikes}, notice '{lastNotice}', hits {Manager.Collisions}, state {Manager.State}");
                Assert.AreEqual(before + 1, par.KerbStrikes, $"strike {attempt} registered by driving onto the kerb");
            }
            yield return null;
            Assert.AreEqual(LevelState.Failed, Manager.State);
            Assert.AreEqual(FailReason.ObjectiveFailed, Manager.LastResult.Value.FailReason);
            StringAssert.Contains("KERB", Manager.FailDetail);
            StringAssert.Contains("KERB STRIKE 3/2", lastNotice);
        }

        // ---------------------------------------------------------------- sequence

        [UnityTest]
        public IEnumerator Objective_Sequence_Runs_In_Order()
        {
#if UNITY_EDITOR
            LevelData level = Object.Instantiate(Catalog().Get(0));
            var so = new SerializedObject(level);
            SerializedProperty list = so.FindProperty("objectives");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).managedReferenceValue = new ParkObjective("Park in bay 2", new Vector3(-21.5f, 0f, 7.25f), 0f, new Vector2(3f, 5.5f), ParkDirection.Either);
            list.GetArrayElementAtIndex(1).managedReferenceValue = new ReverseParkObjective("Reverse into bay R6", new Vector3(24.5f, 0f, 7.25f), 0f, new Vector2(3f, 5.5f), 0.7f);
            so.FindProperty("missionType").enumValueIndex = (int)MissionType.MultiStage;
            so.ApplyModifiedPropertiesWithoutUndo();
            yield return Load(level);

            int completed = -1;
            Runner.ObjectiveCompleted += (i, _) => completed = i;
            Assert.AreEqual(2, Hud.ObjectiveRowCount);
            Vector3 firstZone = LevelLoader.Instance.Zone.Center;
            Assert.Less(Vector3.Distance(firstZone, new Vector3(-21.5f, 0f, 7.25f)), 0.01f, "first bay active");

            yield return Place(new Vector3(-21.5f, 0f, 7.25f), 0f);
            float t = 0f;
            while (completed < 0 && t < 5f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(0, completed, "first objective completed");
            Assert.AreEqual(LevelState.Playing, Manager.State, "level continues to the second objective");
            Assert.AreEqual(1, Runner.CurrentIndex);
            Assert.Less(Vector3.Distance(LevelLoader.Instance.Zone.Center, new Vector3(24.5f, 0f, 7.25f)), 0.01f, "guide moved to the second bay");
            StringAssert.Contains("DONE", Hud.ObjectiveRowText(0));
            Assert.AreEqual("MULTI-STAGE", MissionTypeInfo.Title(level.MissionType));

            yield return Place(new Vector3(24.5f, 0f, 10f - 0.3f - HalfLength()), 180f);
            yield return WaitWhilePlaying(5f);
            Assert.AreEqual(LevelState.Completed, Manager.State, $"both objectives done ({Manager.Validator.Status}, '{Runner.Hint}')");
            Object.Destroy(level);
#else
            yield break;
#endif
        }

        // ---------------------------------------------------------------- visual review

        [UnityTest]
        public IEnumerator Render_Mission_Levels([Values(1, 2, 3)] int number)
        {
            yield return Load(number);
            yield return new WaitForSeconds(0.6f);   // banner fully faded in
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../UltimateCarParking3D_logs/missions"));
            Directory.CreateDirectory(dir);
            Camera main = Camera.main;
            Capture(main, Path.Combine(dir, $"L{number:00}_chase.png"), withUi: true);

            var cam = new GameObject("ShotCam").AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.enabled = false;
            Vector3 target = LevelLoader.Instance.Zone.Center;
            cam.fieldOfView = 50f;
            Vector3 from = target + new Vector3(-9f, 7f, -11f);
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target - from));
            Capture(cam, Path.Combine(dir, $"L{number:00}_target.png"), withUi: false);
            from = new Vector3(-48f, 26f, -34f);
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(new Vector3(0f, 0f, 0f) - from));
            Capture(cam, Path.Combine(dir, $"L{number:00}_yard.png"), withUi: false);
            Object.Destroy(cam.gameObject);
            Assert.IsTrue(File.Exists(Path.Combine(dir, $"L{number:00}_chase.png")));
        }

        private static void Capture(Camera cam, string path, bool withUi)
        {
            const int w = 1600, h = 720;
            _ = withUi;   // overlay UI is not in camera renders; the HUD is checked on device
            var rt = new RenderTexture(w, h, 24);
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(rt);
            Object.Destroy(tex);
        }
    }
}
