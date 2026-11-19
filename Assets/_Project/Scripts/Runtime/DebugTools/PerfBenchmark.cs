using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.UI;

namespace UltimateCarParking.DebugTools
{
    /// <summary>
    /// Unattended on-device benchmark: plays World Tour missions one after another, driving each for a few seconds, so any phone can be
    /// measured without anyone tapping (some phones block scripted taps). Enabled by writing <c>bench:id:0,us:0,ae:0</c> (country id and
    /// mission index, comma separated, optional <c>x2</c> suffix to repeat the whole list) into the perfmon flag file. Results go to
    /// logcat as <c>[PerfBench]</c> lines next to the regular <c>[PerfMon]</c> windows. Never present without that flag file.
    /// </summary>
    public sealed class PerfBenchmark : MonoBehaviour
    {
        private readonly List<(string country, int mission)> plan = new List<(string, int)>();
        private int repeats = 1;

        public void Configure(string spec)
        {
            string[] parts = spec.Split(',');
            foreach (string raw in parts)
            {
                string p = raw.Trim();
                if (p.Length == 0) continue;
                if (p.StartsWith("x") && int.TryParse(p.Substring(1), out int r)) { repeats = Mathf.Clamp(r, 1, 20); continue; }
                string[] kv = p.Split(':');
                plan.Add((kv[0], kv.Length > 1 && int.TryParse(kv[1], out int m) ? m : 0));
            }
        }

        private IEnumerator Start()
        {
            UnityEngine.Debug.Log($"[PerfBench] start: {plan.Count} missions x{repeats}");
            while (SceneManager.GetActiveScene().name != SceneNames.MainMenu) yield return null;
            yield return new WaitForSecondsRealtime(4f);

            int run = 0;
            for (int rep = 0; rep < repeats; rep++)
            {
                foreach ((string country, int mission) in plan)
                {
                    ScreenTransition.LoadScene(SceneNames.WorldTour);
                    float wait = 0f;
                    WorldTourController tour = null;
                    while (tour == null && wait < 20f) { yield return null; wait += Time.unscaledDeltaTime; tour = FindAnyObjectByType<WorldTourController>(); }
                    yield return new WaitForSecondsRealtime(1.5f);
                    if (tour == null) { UnityEngine.Debug.Log("[PerfBench] World Tour screen did not open"); yield break; }

                    var clock = Stopwatch.StartNew();
                    if (!tour.PlayCountryMission(country, mission)) { UnityEngine.Debug.Log($"[PerfBench] cannot start {country}:{mission}"); continue; }
                    wait = 0f;
                    while ((LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) && wait < 60f) { yield return null; wait += Time.unscaledDeltaTime; }
                    if (LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) { UnityEngine.Debug.Log($"[PerfBench] {country}:{mission} never started"); continue; }
                    long startMs = clock.ElapsedMilliseconds;
                    string levelId = LevelManager.Instance.Level.LevelId;

                    var input = LevelManager.Instance.Vehicle.Input;
                    input.KeyboardEnabled = false;
                    input.SetThrottle(1f);
                    yield return new WaitForSeconds(3f);
                    input.SetSteering(0.5f);
                    yield return new WaitForSeconds(3f);
                    input.SetSteering(0f);
                    yield return new WaitForSeconds(2f);
                    input.SetThrottle(0f);
                    yield return new WaitForSeconds(2f);

                    run++;
                    UnityEngine.Debug.Log($"[PerfBench] #{run} {levelId} ({country}:{mission}) request to playable {startMs} ms, unity {Profiler.GetTotalAllocatedMemoryLong() / 1048576} MB, " +
                                          $"reserved {Profiler.GetTotalReservedMemoryLong() / 1048576} MB, gfx {Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576} MB, mono {Profiler.GetMonoUsedSizeLong() / 1048576} MB");
                }
            }
            UnityEngine.Debug.Log("[PerfBench] done");
        }
    }
}
