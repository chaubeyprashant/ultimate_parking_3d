using System.IO;
using NUnit.Framework;
using UnityEngine;
using UltimateCarParking.Save;
using UltimateCarParking.Settings;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Tests
{
    /// <summary>Phase 1 controls: gear and pedal rules, the classic reverse guard, tilt and wheel maths, settings.</summary>
    public class DriveControlsTests
    {
        // ---------------------------------------------------------------- gears

        [Test]
        public void Drive_Gas_Goes_Forward_And_Brake_Never_Reverses()
        {
            PedalCommand c = GearRules.Gears(DriveGear.Drive, gas: true, brake: false, handbrake: false);
            Assert.AreEqual(1f, c.Throttle);
            c = GearRules.Gears(DriveGear.Drive, gas: false, brake: true, handbrake: false);
            Assert.AreEqual(0f, c.Throttle, "the brake pedal never asks for reverse");
            Assert.AreEqual(1f, c.Brake);
            c = GearRules.Gears(DriveGear.Drive, gas: true, brake: true, handbrake: true);
            Assert.AreEqual(1f, c.Throttle); Assert.AreEqual(1f, c.Brake); Assert.IsTrue(c.Handbrake);
        }

        [Test]
        public void Reverse_Gas_Reverses_And_Park_Holds()
        {
            Assert.AreEqual(-1f, GearRules.Gears(DriveGear.Reverse, true, false, false).Throttle);
            Assert.AreEqual(0f, GearRules.Gears(DriveGear.Reverse, false, true, false).Throttle);
            PedalCommand p = GearRules.Gears(DriveGear.Park, gas: true, brake: false, handbrake: false);
            Assert.AreEqual(0f, p.Throttle, "gas does nothing in Park");
            Assert.AreEqual(1f, p.Brake);
            Assert.IsTrue(p.Handbrake);
        }

        [Test]
        public void Gears_Change_Only_Near_Standstill()
        {
            Assert.IsTrue(GearRules.CanShift(0f));
            Assert.IsTrue(GearRules.CanShift(1.4f));
            Assert.IsTrue(GearRules.CanShift(-1.4f));
            Assert.IsFalse(GearRules.CanShift(2f));
            Assert.IsFalse(GearRules.CanShift(-6f));
            Assert.AreEqual("P", GearRules.Letter(DriveGear.Park));
            Assert.AreEqual("R", GearRules.Letter(DriveGear.Reverse));
            Assert.AreEqual("D", GearRules.Letter(DriveGear.Drive));
        }

        // ---------------------------------------------------------------- classic brake/reverse

        [Test]
        public void Classic_Press_While_Rolling_Only_Brakes_Until_Released()
        {
            var c = new ClassicBrakeReverse();
            PedalCommand cmd = c.Step(false, true, false, 12f);      // pressed at 12 km/h
            Assert.AreEqual(1f, cmd.Brake); Assert.AreEqual(0f, cmd.Throttle);
            cmd = c.Step(false, true, false, 0f);                     // stopped, still held
            Assert.AreEqual(1f, cmd.Brake, "still only braking at the stop");
            Assert.AreEqual(0f, cmd.Throttle, "no accidental reverse");
            c.Step(false, false, false, 0f);                          // released
            cmd = c.Step(false, true, false, 0f);                     // pressed again from a stop
            Assert.AreEqual(-1f, cmd.Throttle, "second press reverses");
            Assert.AreEqual(0f, cmd.Brake);
        }

        [Test]
        public void Classic_Press_From_Standstill_Reverses_At_Once()
        {
            var c = new ClassicBrakeReverse();
            Assert.AreEqual(-1f, c.Step(false, true, false, 0.2f).Throttle);
            Assert.AreEqual(-1f, c.Step(false, true, false, -4f).Throttle, "keeps reversing while held");
        }

        // ---------------------------------------------------------------- tilt

        [Test]
        public void Tilt_Has_A_Dead_Zone_A_Calibrated_Centre_And_Sensitivity()
        {
            Assert.AreEqual(0f, TiltSteer.Target(2f, 0f, 1f), "inside the dead zone");
            Assert.AreEqual(0f, TiltSteer.Target(-2f, 0f, 1f));
            Assert.AreEqual(0f, TiltSteer.Target(12f, 10f, 1f), "measured from the calibrated zero");
            Assert.AreEqual(1f, TiltSteer.Target(TiltSteer.FullLockDeg + TiltSteer.DeadZoneDeg, 0f, 1f), 1e-4f);
            Assert.AreEqual(-1f, TiltSteer.Target(-90f, 0f, 1f), "clamped");
            float gentle = TiltSteer.Target(12f, 0f, 0.5f), quick = TiltSteer.Target(12f, 0f, 2f);
            Assert.Greater(quick, gentle * 3.5f, "sensitivity scales the response");
        }

        [Test]
        public void Tilt_Is_Smoothed_Not_Instant()
        {
            var t = new TiltSteer();
            float first = t.Step(30f, 0f, 1f, 1f / 60f);
            Assert.That(first, Is.InRange(0.05f, 0.5f), "eases toward the target");
            for (int i = 0; i < 120; i++) t.Step(30f, 0f, 1f, 1f / 60f);
            Assert.AreEqual(TiltSteer.Target(30f, 0f, 1f), t.Value, 1e-3f);
        }

        [Test]
        public void Phone_Roll_Reads_Clockwise_Positive_In_Both_Landscapes()
        {
            float th = 20f * Mathf.Deg2Rad;
            // Landscape left: upright gravity is device -x; a clockwise turn rotates gravity counter-clockwise in device axes.
            Vector3 left = new Vector3(-Mathf.Cos(th), -Mathf.Sin(th), 0f);
            Assert.AreEqual(0f, TiltSteer.RollFromGravity(new Vector3(-1f, 0f, -0.2f), ScreenOrientation.LandscapeLeft), 1e-3f);
            Assert.AreEqual(20f, TiltSteer.RollFromGravity(left, ScreenOrientation.LandscapeLeft), 1e-3f);
            Vector3 right = new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0f);
            Assert.AreEqual(0f, TiltSteer.RollFromGravity(new Vector3(1f, 0f, 0f), ScreenOrientation.LandscapeRight), 1e-3f);
            Assert.AreEqual(20f, TiltSteer.RollFromGravity(right, ScreenOrientation.LandscapeRight), 1e-3f);
        }

        // ---------------------------------------------------------------- wheel

        [Test]
        public void Wheel_Turns_Clockwise_Positive_With_Sensitivity_And_A_Stop()
        {
            Assert.AreEqual(90f, WheelSteer.ClockwiseDelta(Vector2.up, Vector2.right), 1e-3f);
            Assert.AreEqual(-90f, WheelSteer.ClockwiseDelta(Vector2.up, Vector2.left), 1e-3f);
            Assert.AreEqual(90f / WheelSteer.FullLockDeg, WheelSteer.Output(90f, 1f), 1e-4f);
            Assert.AreEqual(1f, WheelSteer.Output(WheelSteer.FullLockDeg, 1f), 1e-4f);
            Assert.AreEqual(1f, WheelSteer.Output(WheelSteer.FullLockDeg / 2f, 2f), 1e-4f, "double sensitivity = half the turn");
            Assert.AreEqual(WheelSteer.FullLockDeg, WheelSteer.Limit(1f), 1e-4f, "stops at full lock");
            Assert.AreEqual(WheelSteer.MaxRotationDeg, WheelSteer.Limit(0.5f), 1e-4f, "never more than the wheel's own stop");
        }

        // ---------------------------------------------------------------- settings

        private string path;

        [SetUp]
        public void SetUp()
        {
            path = Path.Combine(Application.temporaryCachePath, $"controls_test_{System.Guid.NewGuid():N}.json");
            SaveManager.Initialize(path);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string f in new[] { path, path + ".bak" }) if (File.Exists(f)) File.Delete(f);
        }

        [Test]
        public void Control_Settings_Persist_Across_Sessions()
        {
            SettingsService.SetSteeringMode(GameSettings.SteerTilt);
            SettingsService.SetPedalMode(GameSettings.PedalsGears);
            SettingsService.SetControlScale(1.2f);
            SettingsService.SetControlOpacity(0.5f);
            SettingsService.SetControlsRaise(0.4f);
            SettingsService.SetLeftHanded(true);
            SettingsService.SetTiltZero(7.5f);
            SettingsService.SetSteeringSensitivity(1.4f);
            SettingsService.Commit();

            GameSettings s = new SaveManager(path).Data.settings;
            Assert.AreEqual(GameSettings.SteerTilt, s.steeringMode);
            Assert.AreEqual(GameSettings.PedalsGears, s.pedalMode);
            Assert.AreEqual(1.2f, s.controlScale, 1e-4f);
            Assert.AreEqual(0.5f, s.controlOpacity, 1e-4f);
            Assert.AreEqual(0.4f, s.controlsRaise, 1e-4f);
            Assert.IsTrue(s.leftHanded);
            Assert.AreEqual(7.5f, s.tiltZero, 1e-4f);
            Assert.AreEqual(1.4f, s.steeringSensitivity, 1e-4f);
        }

        [Test]
        public void Older_Saves_Get_The_New_Control_Defaults()
        {
            File.WriteAllText(path, "{\"version\":4,\"coins\":10,\"settings\":{\"musicVolume\":0.3,\"soundVolume\":0.8,\"steeringSensitivity\":1.5}}");
            GameSettings s = new SaveManager(path).Data.settings;
            Assert.AreEqual(1.5f, s.steeringSensitivity, 1e-4f, "existing choice kept");
            Assert.AreEqual(GameSettings.PedalsClassic, s.pedalMode, "simple pedals by default");
            Assert.That(s.controlScale, Is.InRange(GameSettings.MinControlScale, GameSettings.MaxControlScale));
            Assert.That(s.controlOpacity, Is.InRange(GameSettings.MinControlOpacity, 1f));
            Assert.IsFalse(s.leftHanded);
            Debug.Log($"[Test] Old save → steering mode {GameSettings.SteeringName(s.steeringMode)}, size {s.controlScale}, opacity {s.controlOpacity}");
        }

        [Test]
        public void Control_Values_Are_Clamped_And_Resettable()
        {
            SettingsService.SetControlScale(9f);
            SettingsService.SetControlOpacity(0.01f);
            SettingsService.SetControlsRaise(-3f);
            SettingsService.SetSteeringMode(42);
            GameSettings s = SettingsService.Current;
            Assert.AreEqual(GameSettings.MaxControlScale, s.controlScale);
            Assert.AreEqual(GameSettings.MinControlOpacity, s.controlOpacity);
            Assert.AreEqual(0f, s.controlsRaise);
            Assert.AreEqual(GameSettings.SteerWheel, s.steeringMode);
            SettingsService.SetLeftHanded(true);
            SettingsService.ResetControls();
            Assert.IsFalse(s.leftHanded);
            Assert.AreEqual(1f, s.controlScale);
        }
    }
}
