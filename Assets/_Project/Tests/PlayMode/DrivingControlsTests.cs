using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
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
    /// Phase 1 premium controls in the real Gameplay scene: separate pedals with P / R / D, the classic reverse guard,
    /// the steering wheel with multi-touch, layout options, the speedometer, camera zoom, and pause release.
    /// Fixture level T01 (spawn x = -20 facing +X).
    /// </summary>
    public class DrivingControlsTests
    {
        private static LevelCatalog TestCatalog()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Project/Levels/Test/TestCatalog.asset");
#else
            return null;
#endif
        }

        private static VehicleController Car => LevelLoader.Instance.Vehicle;
        private static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        private static HoldButton Hold(string name) => GameObject.Find(name).GetComponent<HoldButton>();

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

        private static IEnumerator Load(int steering, int pedals)
        {
            SettingsService.SetSteeringMode(steering);
            SettingsService.SetPedalMode(pedals);
            LevelSelection.Request(TestCatalog().Get(0), TestCatalog());
            SceneManager.LoadScene(SceneNames.Gameplay);
            yield return WaitFor(() => LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing, 6f, "level playing");
            Car.Input.KeyboardEnabled = false;
            yield return null;
        }

        // ---------------------------------------------------------------- pedals and gears

        [UnityTest]
        public IEnumerator Gears_Brake_Stops_Without_Reversing_And_R_Reverses_Only_When_Stopped()
        {
            yield return Load(GameSettings.SteerWheel, GameSettings.PedalsGears);
            var controls = Find<MobileControls>();
            var gauge = Find<SpeedGauge>();
            Assert.AreEqual(DriveGear.Drive, controls.Gear, "levels start in D");
            Assert.IsNull(GameObject.Find("BrakeReverse"), "classic button hidden in Gears mode");

            HoldButton gas = Hold("Gas"), brake = Hold("Brake");
            gas.SetHeld(true);
            yield return new WaitForSeconds(2f);
            gas.SetHeld(false);
            float moving = Car.ForwardSpeedKmh;
            Assert.Greater(moving, 8f, "gas drives forward in D");
            int refused = 0;
            controls.ShiftRefused += () => refused++;
            Assert.IsFalse(controls.RequestGear(DriveGear.Reverse), "no shift into R while rolling");
            Assert.AreEqual(1, refused);
            Assert.AreEqual(DriveGear.Drive, controls.Gear);

            brake.SetHeld(true);
            yield return new WaitForSeconds(4f);
            float afterBrake = Car.ForwardSpeedKmh;
            Assert.Greater(afterBrake, -0.5f, "holding the brake never reverses");
            Assert.Less(Mathf.Abs(afterBrake), 0.6f, "brake stops the car");
            brake.SetHeld(false);

            Assert.IsTrue(controls.RequestGear(DriveGear.Reverse), "R allowed at a standstill");
            yield return null; yield return null;
            Assert.AreEqual("R", gauge.GearText);
            gas.SetHeld(true);
            yield return new WaitForSeconds(2.5f);
            gas.SetHeld(false);
            float reversing = Car.ForwardSpeedKmh;
            Assert.Less(reversing, -3f, "gas in R reverses");
            Debug.Log($"[Test] Gears: D gas 2 s → {moving:F1} km/h; brake 4 s → {afterBrake:F2} km/h (no reverse); R gas 2.5 s → {reversing:F1} km/h");
        }

        [UnityTest]
        public IEnumerator Park_Holds_The_Car_And_Ignores_Gas()
        {
            yield return Load(GameSettings.SteerWheel, GameSettings.PedalsGears);
            var controls = Find<MobileControls>();
            Assert.IsTrue(controls.RequestGear(DriveGear.Park));
            Vector3 start = Car.transform.position;
            Hold("Gas").SetHeld(true);
            yield return new WaitForSeconds(1.5f);
            Hold("Gas").SetHeld(false);
            float moved = Vector3.Distance(start, Car.transform.position);
            Assert.Less(moved, 0.15f, "Park holds the car");
            Assert.IsTrue(Car.Input.Handbrake);
            Assert.AreEqual("P", Find<SpeedGauge>().GearText);
        }

        [UnityTest]
        public IEnumerator Classic_Brake_Reverse_Stops_First_And_Reverses_Only_On_A_New_Press()
        {
            yield return Load(GameSettings.SteerButtons, GameSettings.PedalsClassic);
            HoldButton gas = Hold("Gas"), br = Hold("BrakeReverse");
            Assert.IsNull(GameObject.Find("Brake"), "separate brake hidden in Classic mode");
            Assert.IsNull(GameObject.Find("GearSelector"), "gear selector hidden in Classic mode");
            gas.SetHeld(true);
            yield return new WaitForSeconds(2f);
            gas.SetHeld(false);
            br.SetHeld(true);
            yield return new WaitForSeconds(4f);
            float held = Car.ForwardSpeedKmh;
            Assert.Greater(held, -0.5f, "a press made while rolling only brakes");
            br.SetHeld(false);
            yield return new WaitForSeconds(0.3f);
            br.SetHeld(true);
            yield return new WaitForSeconds(3f);
            br.SetHeld(false);
            Assert.Less(Car.ForwardSpeedKmh, -3f, "a fresh press from the stop reverses");
            Debug.Log($"[Test] Classic: held through the stop → {held:F2} km/h; new press → {Car.ForwardSpeedKmh:F1} km/h");
        }

        // ---------------------------------------------------------------- steering wheel, multi-touch

        private static PointerEventData Pointer(int id, Vector2 screen) =>
            new PointerEventData(EventSystem.current) { pointerId = id, position = screen, button = PointerEventData.InputButton.Left };

        private static Vector2 ScreenPos(RectTransform r, Vector2 localOffset) =>
            RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center + localOffset));

        [UnityTest]
        public IEnumerator Wheel_Steers_With_Its_Own_Finger_While_The_Gas_Is_Held_And_Self_Centres()
        {
            yield return Load(GameSettings.SteerWheel, GameSettings.PedalsGears);
            var controls = Find<MobileControls>();
            SteeringWheel wheel = controls.Wheel;
            Assert.IsTrue(wheel.gameObject.activeInHierarchy, "wheel shown in Wheel mode");
            Assert.IsNull(GameObject.Find("SteerLeft"), "buttons hidden in Wheel mode");
            var rt = (RectTransform)wheel.transform;
            float r = rt.rect.width * 0.4f;

            // Finger 3 holds the gas; finger 7 grabs the wheel at 12 o'clock and drags to 3 o'clock (90° clockwise).
            var gasPointer = Pointer(3, ScreenPos((RectTransform)Hold("Gas").transform, Vector2.zero));
            Hold("Gas").OnPointerDown(gasPointer);
            var finger = Pointer(7, ScreenPos(rt, new Vector2(0f, r)));
            wheel.OnPointerDown(finger);
            for (int i = 1; i <= 9; i++)
            {
                float a = i * 10f * Mathf.Deg2Rad;
                finger.position = ScreenPos(rt, new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r));
                wheel.OnDrag(finger);
            }
            // A different finger dragging must not move this wheel.
            var stray = Pointer(9, ScreenPos(rt, new Vector2(-r, 0f)));
            wheel.OnDrag(stray);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(90f, wheel.Angle, 1.5f, "wheel followed its own finger only");
            Assert.AreEqual(90f / WheelSteer.FullLockDeg, controls.Steering, 0.02f);
            Assert.Greater(Car.Input.Steering, 0.3f, "steering reaches the car");
            Assert.Greater(Car.Input.Throttle, 0.5f, "gas still held at the same time");
            Assert.Greater(Car.SteerAngle, 3f, "front wheels turned right");

            wheel.OnPointerUp(finger);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(0f, wheel.Angle, 0.01f, "self-centres when released");
            Hold("Gas").OnPointerUp(gasPointer);
            Debug.Log($"[Test] Wheel: 90° turn → steering {Car.Input.Steering:F2} with gas {Car.Input.Throttle:F2}; released → centred");
        }

        [UnityTest]
        public IEnumerator Tilt_Mode_Shows_The_Tilt_Controls_And_Holds_Straight_Without_A_Sensor_Reading()
        {
            yield return Load(GameSettings.SteerTilt, GameSettings.PedalsGears);
            var controls = Find<MobileControls>();
            Assert.IsNotNull(GameObject.Find("Tilt"), "tilt indicator shown");
            Assert.IsFalse(Find<SteeringWheel>().gameObject.activeInHierarchy, "wheel hidden");
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[Test] Tilt: accelerometer reading in the editor = {controls.HasTiltReading}");
            if (!controls.HasTiltReading) Assert.AreEqual(0f, controls.Steering, 1e-3f, "no sensor → straight ahead");
        }

        // ---------------------------------------------------------------- layout

        [UnityTest]
        public IEnumerator Left_Handed_Size_Opacity_And_Height_Apply_Live()
        {
            yield return Load(GameSettings.SteerWheel, GameSettings.PedalsGears);
            var layout = Find<ControlLayout>();
            var canvasGroup = layout.GetComponent<CanvasGroup>();
            float GasX() => ((RectTransform)Hold("Gas").transform).position.x;
            float WheelX() => Find<SteeringWheel>().transform.position.x;
            float mid = UnityEngine.Screen.width * 0.5f;
            Assert.Less(WheelX(), mid); Assert.Greater(GasX(), mid);

            SettingsService.SetLeftHanded(true);
            yield return null;
            Assert.Greater(WheelX(), mid, "wheel moves to the right");
            Assert.Less(GasX(), mid, "pedals move to the left");

            float y0 = layout.PedalZone.position.y;
            SettingsService.SetControlScale(1.3f);
            SettingsService.SetControlOpacity(0.5f);
            SettingsService.SetControlsRaise(1f);
            yield return null;
            Assert.AreEqual(1.3f * layout.FitScale, layout.PedalZone.localScale.x, 1e-4f, "size setting × the narrow-screen fit");
            Assert.Greater(layout.PedalZone.localScale.x, 0.9f);
            Assert.AreEqual(0.5f, canvasGroup.alpha, 1e-4f);
            Assert.Greater(layout.PedalZone.position.y, y0 + 10f, "height raises the controls");

            SettingsService.SetSteeringMode(GameSettings.SteerButtons);
            yield return null;
            Assert.IsNotNull(GameObject.Find("SteerLeft"));
            Assert.IsFalse(Find<SteeringWheel>().gameObject.activeInHierarchy);
            SettingsService.ResetControls();
            yield return null;
            Assert.Less(WheelX(), mid, "reset puts the wheel back on the left");
        }

        // ---------------------------------------------------------------- HUD

        [UnityTest]
        public IEnumerator Speedometer_Shows_Speed_And_Zoom_Cycles_The_Camera()
        {
            yield return Load(GameSettings.SteerWheel, GameSettings.PedalsGears);
            var gauge = Find<SpeedGauge>();
            var hud = Find<GameplayHud>();
            var cam = Find<VehicleCamera>();
            Hold("Gas").SetHeld(true);
            yield return new WaitForSeconds(1.5f);
            Hold("Gas").SetHeld(false);
            int shown = int.Parse(gauge.SpeedText);
            Assert.AreEqual(Mathf.RoundToInt(Car.SpeedKmh), shown, 2, "speed readout follows the car");
            Assert.Greater(shown, 5);
            Assert.AreEqual("D", gauge.GearText);

            Assert.AreEqual("MID", hud.ZoomText);
            hud.CycleZoom();
            Assert.AreEqual("FAR", hud.ZoomText);
            Assert.AreEqual(10f, cam.Distance, 1e-3f);
            hud.CycleZoom();
            Assert.AreEqual(5.5f, cam.Distance, 1e-3f);
            hud.CycleZoom();
        }

        [UnityTest]
        public IEnumerator Pause_Releases_Every_Control_And_Keeps_The_Gear()
        {
            yield return Load(GameSettings.SteerWheel, GameSettings.PedalsGears);
            var controls = Find<MobileControls>();
            var ui = Find<GameplayUiController>();
            Assert.IsTrue(controls.RequestGear(DriveGear.Reverse));
            HoldButton gas = Hold("Gas");   // looked up now: the HUD (and its buttons) is hidden while paused
            gas.SetHeld(true);
            controls.Wheel.SetHeld(true);
            controls.Wheel.Turn(120f);
            yield return new WaitForSeconds(0.5f);
            ui.Pause();
            yield return null;
            Assert.IsFalse(gas.IsHeld, "gas released on pause");
            Assert.AreEqual(0f, controls.Wheel.Angle, 1e-3f, "wheel centred on pause");
            Assert.AreEqual(0f, Car.Input.Steering, 1e-3f);
            Assert.AreEqual(DriveGear.Reverse, controls.Gear, "gear kept");
            ui.Resume();
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(0f, Car.Input.Throttle, 0.02f, "nothing stuck after resume");
        }
    }
}
