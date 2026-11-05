using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Audio;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Parking;
using UltimateCarParking.Settings;
using SettingsService = UltimateCarParking.Settings.SettingsService;
using UltimateCarParking.UI;
using UltimateCarParking.Vehicle;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateCarParking.Tests
{
    /// <summary>
    /// Phase 4 acceptance in the real Gameplay scene: HUD, touch controls, guidance, pause / completed / failed panels,
    /// feedback, audio and settings. Uses the Phase 2 fixture levels (T01: spawn x=-20 facing +X, bay at (4.2, 9.25)).
    /// </summary>
    public class GameplayUiTests
    {
        private static LevelCatalog TestCatalog()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/Test/TestCatalog.asset");
#else
            return null;
#endif
        }

        private LevelManager Manager => LevelManager.Instance;
        private VehicleController Car => LevelLoader.Instance.Vehicle;
        private static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

        [SetUp]
        public void SetUp() => TestScenes.UseTemporarySave();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            yield return TestScenes.ResetToEmpty();
        }

        private static IEnumerator WaitFor(System.Func<bool> c, float timeout, string what)
        {
            float t = 0f;
            while (!c()) { t += Time.unscaledDeltaTime; if (t > timeout) Assert.Fail("timed out: " + what); yield return null; }
        }

        private IEnumerator Load(LevelData level = null, LevelCatalog catalog = null)
        {
            LevelSelection.Request(level ?? TestCatalog().Get(0), catalog ?? (level == null ? TestCatalog() : null));
            SceneManager.LoadScene(SceneNames.Gameplay);
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 6f, "level playing");
            Car.Input.KeyboardEnabled = false;
            yield return null;
        }

        private static LevelData Variant(string field, object value)
        {
            LevelData copy = Object.Instantiate(TestCatalog().Get(0));
#if UNITY_EDITOR
            var so = new SerializedObject(copy);
            SerializedProperty p = so.FindProperty(field);
            if (value is float f) p.floatValue = f; else if (value is int i) p.intValue = i; else if (value is Vector3 v) p.vector3Value = v;
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
            return copy;
        }

        // ---------------------------------------------------------------- HUD

        [UnityTest]
        public IEnumerator Hud_Shows_Level_Timer_Hits_Gear_And_Guide()
        {
            yield return Load();
            var hud = Find<GameplayHud>();
            var texts = hud.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.text).ToList();
            Assert.Contains("LEVEL 01", texts);   // T01 has level number 1
            string t0 = hud.TimerText;
            yield return new WaitForSeconds(1.2f);
            Assert.AreNotEqual(t0, hud.TimerText, "timer counts down");
            StringAssert.StartsWith("1:2", hud.TimerText, "90 s limit → 1:2x after a second");
            Assert.AreEqual("0/3", hud.CollisionText);
            Assert.AreEqual("DRIVE TO THE BAY", hud.GuideText);
            Debug.Log($"[Test] HUD: timer '{hud.TimerText}', hits '{hud.CollisionText}', guide '{hud.GuideText}'");
        }

        [UnityTest]
        public IEnumerator Guidance_Follows_The_Parking_Status_And_The_Chevron_Marks_The_Bay()
        {
            yield return Load();
            var hud = Find<GameplayHud>();
            var guide = Find<ParkingGuide>();
            Assert.IsNotNull(guide.Chevron, "chevron built");
            Vector3 bay = LevelLoader.Instance.Zone.Center;
            Assert.Less(Vector2.Distance(new Vector2(guide.Chevron.position.x, guide.Chevron.position.z), new Vector2(bay.x, bay.z)), 0.01f, "above the bay");
            Assert.Greater(guide.Chevron.position.y, 2f);

            Car.ResetVehicle(bay + Manager.Level.TargetRotation * new Vector3(0f, 0f, 1.6f) + Vector3.up * 0.05f, Manager.Level.TargetRotation);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual("GET FULLY INSIDE THE LINES", hud.GuideText);
            Car.ResetVehicle(bay + Vector3.up * 0.05f, Manager.Level.TargetRotation * Quaternion.Euler(0f, 11f, 0f));
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual("STRAIGHTEN UP", hud.GuideText);
            Car.ResetVehicle(bay + Vector3.up * 0.05f, Manager.Level.TargetRotation);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual("HOLD IT...", hud.GuideText);
            yield return WaitFor(() => Manager.State == LevelState.Completed, 4f, "parked");
            Assert.IsFalse(guide.Chevron.gameObject.activeSelf, "chevron hidden once parked");
        }

        // ---------------------------------------------------------------- touch controls

        [UnityTest]
        public IEnumerator Gas_And_Brake_Reverse_Pedals_Drive_The_Car()
        {
            yield return Load();
            var controls = Find<MobileControls>();
            HoldButton gas = GameObject.Find("Gas").GetComponent<HoldButton>();
            HoldButton brake = GameObject.Find("BrakeReverse").GetComponent<HoldButton>();
            Vector3 start = Car.transform.position;

            gas.SetHeld(true);
            yield return new WaitForSeconds(2f);
            gas.SetHeld(false);
            float forward = Vector3.Dot(Car.transform.position - start, Car.transform.forward);
            Assert.Greater(forward, 4f, "gas drives forward");
            Assert.AreSame(Car.Input, controls.Target, "controls bound to the spawned car");

            brake.SetHeld(true);          // brakes first, then reverses
            yield return new WaitForSeconds(4f);
            Assert.Less(Car.ForwardSpeedKmh, -3f, "brake pedal ends up reversing");
            Assert.AreEqual("R", GameObject.Find("Gear").GetComponent<TMPro.TMP_Text>().text, "gear shows R");
            brake.SetHeld(false);
            yield return null;
            Assert.AreEqual(0f, Car.Input.Throttle);
            Debug.Log($"[Test] Pedals: gas 2 s → {forward:F1} m; brake/reverse 4 s → {Car.ForwardSpeedKmh:F1} km/h");
        }

        [UnityTest]
        public IEnumerator Steering_Buttons_Ramp_With_Sensitivity_And_Return_To_Centre()
        {
            yield return Load();
            var controls = Find<MobileControls>();
            HoldButton left = GameObject.Find("SteerLeft").GetComponent<HoldButton>();
            HoldButton right = GameObject.Find("SteerRight").GetComponent<HoldButton>();

            float TimeToFullLock(float sensitivity)
            {
                SettingsService.SetSteeringSensitivity(sensitivity);
                return 1f / (2.2f * SettingsService.Current.steeringSensitivity);
            }

            TimeToFullLock(1f);
            right.SetHeld(true);
            yield return null; yield return null;
            float early = controls.Steering;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(early, Is.InRange(0.001f, 0.5f), "ramps, not instant");
            Assert.AreEqual(1f, controls.Steering, 1e-3f);
            Assert.AreEqual(1f, Car.Input.Steering, 1e-3f, "reaches the car");
            right.SetHeld(false);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0f, controls.Steering, 1e-3f, "returns to centre");
            Assert.IsFalse(controls.enabled, "no Update while centred");

            float slow = TimeToFullLock(0.5f);
            left.SetHeld(true);
            yield return new WaitForSecondsRealtime(0.3f);
            float slowValue = controls.Steering;
            left.SetHeld(false);
            yield return new WaitForSecondsRealtime(1f);
            TimeToFullLock(2f);
            left.SetHeld(true);
            yield return new WaitForSecondsRealtime(0.3f);
            float fastValue = controls.Steering;
            left.SetHeld(false);
            Debug.Log($"[Test] Steering after 0.3 s: sensitivity 0.5 → {slowValue:F2}, 2.0 → {fastValue:F2} (full lock in {slow:F2} s at 0.5)");
            Assert.Less(fastValue, slowValue - 0.3f, "higher sensitivity steers faster (left = negative)");
        }

        // ---------------------------------------------------------------- panels

        [UnityTest]
        public IEnumerator Pause_Freezes_The_Game_And_Resume_Continues()
        {
            yield return Load();
            var ui = Find<GameplayUiController>();
            GameObject.Find("PauseButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.IsTrue(ui.IsPaused);
            Assert.IsTrue(ui.PauseShown);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(Find<GameplayHud>().gameObject.activeSelf, "HUD hidden behind the menu");
            float elapsed = Manager.ElapsedSeconds;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(elapsed, Manager.ElapsedSeconds, "timer frozen");

            ui.Resume();
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(ui.PauseShown);
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(Manager.ElapsedSeconds, elapsed);
        }

        [UnityTest]
        public IEnumerator Completed_Panel_Reveals_Stars_And_Coins()
        {
            yield return Load();
            var ui = Find<GameplayUiController>();
            int played = AudioManager.Instance.PlayedCount;
            Car.ResetVehicle(Manager.Level.TargetPosition + Vector3.up * 0.05f, Manager.Level.TargetRotation);
            yield return WaitFor(() => ui.CompleteShown, 6f, "complete panel");
            yield return new WaitForSecondsRealtime(2f);
            var texts = ui.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.text).ToList();
            Debug.Log($"[Test] Completed panel: {ui.StarsRevealed} stars revealed, texts [{string.Join(", ", texts.Where(t => t.StartsWith("+") || t.EndsWith("s")))}], {AudioManager.Instance.PlayedCount - played} sounds");
            Assert.AreEqual(3, ui.StarsRevealed);
            Assert.Contains($"+{Manager.Level.CoinsFor(3)}", texts, "coins rolled up to the reward");
            Assert.IsTrue(GameObject.Find("CompletePanel/Body/Next").activeSelf, "T01 has a next level");
            Assert.GreaterOrEqual(AudioManager.Instance.PlayedCount - played, 5, "success + 3 stars + coin sounds");
        }

        [UnityTest]
        public IEnumerator Failed_Panel_Shows_The_Reason_And_Retry_Restarts()
        {
            yield return Load(Variant("timeLimitSeconds", 1.5f));
            var ui = Find<GameplayUiController>();
            yield return WaitFor(() => ui.FailShown, 6f, "fail panel");
            var texts = ui.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.text).ToList();
            Assert.Contains("TIME'S UP", texts);
            Assert.AreEqual(Sfx.Fail, AudioManager.Instance.LastPlayed);

            GameObject.Find("FailPanel/Body/Retry").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => Manager != null && Manager.State == LevelState.Playing && Manager.ElapsedSeconds < 1f, 6f, "restarted");
            Assert.IsFalse(Find<GameplayUiController>().FailShown);
        }

        [UnityTest]
        public IEnumerator Levels_Button_From_Pause_Leaves_With_Time_Restored()
        {
            yield return Load();
            var ui = Find<GameplayUiController>();
            ui.Pause();
            yield return null;
            GameObject.Find("PausePanel/Body/Levels").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.LevelSelect, 6f, "level select");
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
            yield return TestScenes.WaitForTransition();
        }

        // ---------------------------------------------------------------- feedback + audio

        [UnityTest]
        public IEnumerator Collision_Gives_Sound_Flash_Shake_And_Counter()
        {
            yield return Load(Variant("spawnPosition", new Vector3(0f, 0f, 22f)), null);
            Car.transform.rotation = Quaternion.identity;
            Car.ResetVehicle(new Vector3(0f, 0.2f, 22f), Quaternion.identity);   // facing the north wall
            var hud = Find<GameplayHud>();
            int played = AudioManager.Instance.PlayedCount;
            Car.Input.SetThrottle(1f);
            yield return WaitFor(() => Manager.Collisions > 0, 8f, "hits the wall");
            Car.Input.SetThrottle(0f);
            yield return null;
            Assert.AreEqual("1/3", hud.CollisionText);
            Assert.Greater(AudioManager.Instance.PlayedCount, played, "impact sound");
            var flash = GameObject.Find("Flash").GetComponent<UnityEngine.UI.Image>();
            Assert.Greater(flash.color.a, 0f, "screen flash");
        }

        [UnityTest]
        public IEnumerator Engine_Pitch_Follows_Speed_And_Volumes_Follow_Settings()
        {
            yield return Load();
            var audio = Car.GetComponent<VehicleAudio>();
            Assert.IsNotNull(audio, "car prefab carries VehicleAudio");
            Assert.IsTrue(audio.Engine.isPlaying);
            Assert.IsNotNull(audio.Engine.clip);
            yield return new WaitForSeconds(0.5f);
            float idle = audio.Engine.pitch;
            Car.Input.SetThrottle(1f);
            yield return new WaitForSeconds(3f);
            float fast = audio.Engine.pitch;
            Car.Input.SetThrottle(0f);
            Debug.Log($"[Test] Engine pitch idle {idle:F2} → {fast:F2} at {Car.SpeedKmh:F0} km/h");
            Assert.Greater(fast, idle + 0.3f);

            Assert.IsTrue(AudioManager.Instance.MusicSource.isPlaying, "music playing");
            SettingsService.SetMusicVolume(0.4f);
            Assert.AreEqual(0.4f * AudioManager.Instance.Library.gameplayMusicDuck, AudioManager.Instance.MusicSource.volume, 1e-3f, "ducked in gameplay");
            SettingsService.SetSoundVolume(0f);
            yield return null;
            Assert.AreEqual(0f, audio.Engine.volume, 1e-4f, "sound volume 0 silences the engine");
        }

        [UnityTest]
        public IEnumerator Settings_Panel_From_Pause_Changes_And_Saves()
        {
            yield return Load();
            var ui = Find<GameplayUiController>();
            ui.Pause();
            yield return null;
            GameObject.Find("PausePanel/Body/Settings").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            var panel = Find<SettingsPanel>();
            Assert.IsTrue(panel.IsOpen);
            GameObject.Find("STEERINGSlider").GetComponent<UnityEngine.UI.Slider>().value = 1.5f;
            GameObject.Find("MUSICSlider").GetComponent<UnityEngine.UI.Slider>().value = 0.25f;
            Assert.AreEqual(1.5f, SettingsService.Current.steeringSensitivity, 1e-3f);
            GameObject.Find("SettingsPanel/Body/Done").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsFalse(panel.IsOpen);
            var restarted = new UltimateCarParking.Save.SaveManager(UltimateCarParking.Save.SaveManager.Current.FilePath);
            Assert.AreEqual(1.5f, restarted.Data.settings.steeringSensitivity, 1e-3f);
            Assert.AreEqual(0.25f, restarted.Data.settings.musicVolume, 1e-3f);
            ui.Resume();
        }

        [UnityTest]
        public IEnumerator Ui_Buttons_Click_And_Main_Menu_Opens_Settings()
        {
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneNames.MainMenu, 5f, "menu");
            yield return null;
            int played = AudioManager.Instance.PlayedCount;
            GameObject gear = GameObject.Find("SettingsButton");
            ExecuteEvents.Execute(gear, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.IsTrue(Find<SettingsPanel>().IsOpen, "gear opens settings");
            Assert.AreEqual(Sfx.Click, AudioManager.Instance.LastPlayed);
            Assert.Greater(AudioManager.Instance.PlayedCount, played);
            Assert.IsNotNull(Camera.main.GetComponent<CameraDrift>(), "showroom camera drifts");
            Vector3 p0 = Camera.main.transform.position;
            yield return new WaitForSecondsRealtime(1f);
            Assert.Greater(Vector3.Distance(p0, Camera.main.transform.position), 0.005f, "camera moves");
        }
    }
}
