using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Objective driving-feel measurements with on-screen-style (digital) pedal input, in the real Gameplay scene.
    /// Jerk = rate of change of longitudinal acceleration; pitch = body nose-up/down angle. Lower is smoother.
    /// </summary>
    public class DrivingSmoothnessTests
    {
        private LevelManager Manager => LevelManager.Instance;
        private VehicleController Car => LevelLoader.Instance.Vehicle;

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return TestScenes.ResetToEmpty();
        }

        private static IEnumerator WaitFor(System.Func<bool> c, float timeout, string what)
        {
            float t = 0f;
            while (!c()) { t += Time.unscaledDeltaTime; if (t > timeout) Assert.Fail("timed out: " + what); yield return null; }
        }

        private IEnumerator Load()
        {
#if UNITY_EDITOR
            var cat = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/Test/TestCatalog.asset");
            LevelData level = Object.Instantiate(cat.Get(0));
            var so = new SerializedObject(level);
            so.FindProperty("timeLimitSeconds").floatValue = 0f;       // no time limit during the measurement
            so.FindProperty("collisionLimit").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            LevelSelection.Request(level, null);
#endif
            SceneManager.LoadScene(SceneNames.Gameplay);
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 6f, "level playing");
            Car.Input.KeyboardEnabled = false;
            yield return null;
        }

#if UNITY_EDITOR
        // Overrides any values saved in the scene's car with the shipped defaults, so the test measures the code under test.
        private void ApplyTuning()
        {
            var so = new SerializedObject(Car);
            // Guard: the car saved in the scene must already carry the shipped values (a stale scene would ship old tuning).
            Assert.AreEqual(Tune.Start, so.FindProperty("coastFadeStartKmh").floatValue, 0.001f, "scene car is stale: run Tools/bootstrap.sh");
            Assert.AreEqual(Tune.End, so.FindProperty("coastFadeEndKmh").floatValue, 0.001f, "scene car is stale: run Tools/bootstrap.sh");
            Assert.AreEqual(Tune.Decel, so.FindProperty("rollOutDecel").floatValue, 0.001f, "scene car is stale: run Tools/bootstrap.sh");
            so.FindProperty("coastFadeStartKmh").floatValue = Tune.Start;
            so.FindProperty("coastFadeEndKmh").floatValue = Tune.End;
            so.FindProperty("rollOutDecel").floatValue = Tune.Decel;
            so.FindProperty("rollOutTaperKmh").floatValue = Tune.Taper;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static class Tune { public const float Start = 4.5f, End = 7f, Decel = 1.5f, Taper = 1.5f; }
#else
        private void ApplyTuning() { }
#endif

        private struct Sample { public float t, speed, accel, pitch; }

        private IEnumerator Record(float seconds, List<Sample> into, System.Action each = null)
        {
            Rigidbody rb = Car.GetComponent<Rigidbody>();
            float prevSpeed = Vector3.Dot(rb.linearVelocity, Car.transform.forward);
            float t = 0f;
            while (t < seconds)
            {
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                each?.Invoke();
                float speed = Vector3.Dot(rb.linearVelocity, Car.transform.forward);
                float pitch = Mathf.DeltaAngle(0f, Car.transform.eulerAngles.x);
                into.Add(new Sample { t = t, speed = speed, accel = (speed - prevSpeed) / Time.fixedDeltaTime, pitch = pitch });
                prevSpeed = speed;
            }
        }

        private static (float peakAccel, float peakJerk, float pitchRange, float peakPitchRate) Analyse(List<Sample> s)
        {
            float peakAccel = 0f, peakJerk = 0f, minP = float.MaxValue, maxP = float.MinValue, peakRate = 0f;
            // Smooth over 0.1 s so single-step physics noise does not dominate.
            const int w = 5;
            for (int i = w; i < s.Count; i++)
            {
                float a0 = 0f, a1 = 0f;
                for (int k = 0; k < w; k++) { a0 += s[i - w + k].accel; a1 += s[Mathf.Min(i - w + k + w / 2, s.Count - 1)].accel; }
                a0 /= w; a1 /= w;
                if (Mathf.Abs(s[i].speed) > 0.3f)   // below ~1 km/h the car is effectively stopped; wheel stick-slip noise is not 'motion'
                {
                    peakAccel = Mathf.Max(peakAccel, Mathf.Abs(a1));
                    peakJerk = Mathf.Max(peakJerk, Mathf.Abs(a1 - a0) / (Time.fixedDeltaTime * (w / 2f)));
                }
                minP = Mathf.Min(minP, s[i].pitch); maxP = Mathf.Max(maxP, s[i].pitch);
                peakRate = Mathf.Max(peakRate, Mathf.Abs(s[i].pitch - s[i - 1].pitch) / Time.fixedDeltaTime);
            }
            return (peakAccel, peakJerk, maxP - minP, peakRate);
        }

        [UnityTest]
        public IEnumerator Parking_Speed_Pedal_Press_And_Release_Is_Smooth()
        {
            yield return Load();
#if UNITY_EDITOR
            ApplyTuning();
#endif
            yield return new WaitForSeconds(0.8f);                      // settle on the suspension
            var launch = new List<Sample>();
            Car.Input.SetThrottle(1f);                                   // digital Gas, as the on-screen button does
            yield return Record(0.9f, launch);
            var release = new List<Sample>();
            Car.Input.SetThrottle(0f);
            yield return Record(4.5f, release);
            var a = Analyse(launch);
            var b = Analyse(release);
            Debug.Log($"[Feel] LAUNCH  peak accel {a.peakAccel:F2} m/s2, peak jerk {a.peakJerk:F1} m/s3, pitch range {a.pitchRange:F2} deg, peak pitch rate {a.peakPitchRate:F1} deg/s, speed at end {launch[launch.Count - 1].speed * 3.6f:F1} km/h");
            Debug.Log($"[Feel] RELEASE peak decel {b.peakAccel:F2} m/s2, peak jerk {b.peakJerk:F1} m/s3, pitch range {b.pitchRange:F2} deg, peak pitch rate {b.peakPitchRate:F1} deg/s");
            // Soft launch: the digital Gas button ramps, it does not step.
            Assert.Less(a.peakAccel, 4.0f, "launch acceleration");
            Assert.Less(a.peakJerk, 20f, "launch jerk");
            // Controlled roll-out: no lift-off spike, no clamp at the end (was 5.6-7.4 m/s2 and jerk 31-61 before Phase 2).
            Assert.Less(b.peakAccel, 3.0f, "roll-out deceleration");
            Assert.Less(b.peakJerk, 25f, "roll-out jerk");
            Assert.Less(b.peakPitchRate, 3f, "nose dip rate");
            Assert.Less(Mathf.Abs(release[release.Count - 1].speed), 0.05f, "comes to rest");
        }

        [UnityTest]
        public IEnumerator Reverse_Pedal_From_Standstill_Is_Smooth()
        {
            yield return Load();
            ApplyTuning();
            yield return new WaitForSeconds(0.8f);
            var rev = new List<Sample>();
            Car.Input.SetThrottle(-1f);
            yield return Record(1.0f, rev);
            var stop = new List<Sample>();
            Car.Input.SetThrottle(0f);
            yield return Record(4.5f, stop);
            var a = Analyse(rev);
            var b = Analyse(stop);
            Debug.Log($"[Feel] REVERSE peak accel {a.peakAccel:F2} m/s2, peak jerk {a.peakJerk:F1} m/s3, pitch range {a.pitchRange:F2} deg, peak pitch rate {a.peakPitchRate:F1} deg/s, speed {rev[rev.Count - 1].speed * 3.6f:F1} km/h");
            Debug.Log($"[Feel] REVERSE-STOP peak decel {b.peakAccel:F2} m/s2, peak jerk {b.peakJerk:F1} m/s3, pitch range {b.pitchRange:F2} deg, peak pitch rate {b.peakPitchRate:F1} deg/s");
            Assert.Less(a.peakAccel, 3.0f, "reverse acceleration");
            Assert.Less(a.peakJerk, 20f, "reverse jerk");
            Assert.Less(b.peakAccel, 3.0f, "reverse stop deceleration (was 7.1-7.4)");
            Assert.Less(b.peakJerk, 25f, "reverse stop jerk (was 58-59)");
            Assert.Less(b.peakPitchRate, 3f, "nose dip rate");
            Assert.Less(Mathf.Abs(stop[stop.Count - 1].speed), 0.05f, "comes to rest");
        }

        [UnityTest]
        public IEnumerator On_Screen_Pedal_Ramps_Up_And_Down_Instead_Of_Stepping()
        {
            yield return Load();
            Car.Input.SetThrottle(1f);
            yield return null;
            Assert.Less(Car.Input.Throttle, 0.25f, "first frame is not full torque");
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(1f, Car.Input.Throttle, 0.01f, "reaches full after the ramp");
            Car.Input.SetThrottle(0f);
            yield return new WaitForSeconds(0.1f);
            Assert.Greater(Car.Input.Throttle, 0.3f, "lifting off eases down, it does not cut");
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(0f, Car.Input.Throttle, 0.01f, "back to zero");
        }
    }
}
