using NUnit.Framework;
using UnityEngine;
using UltimateCarParking.Quality;
using UltimateCarParking.Save;
using UltimateCarParking.Settings;

namespace UltimateCarParking.Tests
{
    public class DeviceProfileTests
    {
        private static QualityTier Tier(int ram, string gpu, int cores = 8, int api = 33) =>
            DeviceProfile.Classify(new DeviceInfo(ram, cores, gpu, api, 60));

        [TestCase("Adreno (TM) 650", QualityTier.High)]
        [TestCase("Adreno (TM) 740", QualityTier.High)]
        [TestCase("Adreno (TM) 619", QualityTier.Mid)]
        [TestCase("Adreno (TM) 610", QualityTier.Mid)]
        [TestCase("Adreno (TM) 506", QualityTier.Low)]
        [TestCase("Mali-G68", QualityTier.Mid)]
        [TestCase("Mali-G52 MC2", QualityTier.Low)]
        [TestCase("Mali-G78 MP14", QualityTier.High)]
        [TestCase("Immortalis-G715", QualityTier.High)]
        [TestCase("Mali-T830", QualityTier.Low)]
        [TestCase("PowerVR Rogue GE8320", QualityTier.Low)]
        [TestCase("Some New GPU", QualityTier.Mid)]
        [TestCase("", QualityTier.Mid)]
        public void Gpu_Family_Sets_The_Tier_On_A_Big_Phone(string gpu, QualityTier expected) =>
            Assert.AreEqual(expected, Tier(8000, gpu));

        [Test]
        public void The_Two_Test_Phones_Land_Where_Phase_One_Measured_Them()
        {
            Assert.AreEqual(QualityTier.Mid, Tier(5518, "Mali-G68"), "Galaxy A33: 60 fps holds but the GPU is the tight resource");
            Assert.AreEqual(QualityTier.High, Tier(5612, "Adreno (TM) 650"), "Xiaomi M2007J3SP: flagship GPU");
        }

        [Test]
        public void Low_Ram_Caps_The_Tier_Whatever_The_Gpu()
        {
            Assert.AreEqual(QualityTier.Low, Tier(3000, "Adreno (TM) 740"));
            Assert.AreEqual(QualityTier.Mid, Tier(4096, "Adreno (TM) 740"));
            Assert.AreEqual(QualityTier.High, Tier(6000, "Adreno (TM) 740"));
        }

        [Test]
        public void Old_Android_Or_Few_Cores_Is_Low()
        {
            Assert.AreEqual(QualityTier.Low, Tier(8000, "Adreno (TM) 740", api: 27));
            Assert.AreEqual(QualityTier.Low, Tier(8000, "Adreno (TM) 740", cores: 4));
        }

        [Test]
        public void Presets_Are_Ordered_And_Sane()
        {
            QualityPreset low = QualityPresets.For(QualityTier.Low), mid = QualityPresets.For(QualityTier.Mid), high = QualityPresets.For(QualityTier.High);
            Assert.Less(low.RenderScale, mid.RenderScale);
            Assert.LessOrEqual(mid.RenderScale, high.RenderScale);
            Assert.AreEqual(30, low.TargetFps);
            Assert.AreEqual(60, mid.TargetFps);
            Assert.LessOrEqual(low.ShadowDistance, mid.ShadowDistance);
            foreach (QualityPreset p in new[] { low, mid, high })
            {
                Assert.LessOrEqual(p.MinScale, p.RenderScale);
                Assert.LessOrEqual(p.RenderScale, p.MaxScale);
                Assert.Contains(p.Msaa, new[] { 1, 2, 4 });
            }
        }
    }

    public class QualityModeTests
    {
        [Test]
        public void Auto_Uses_The_Detected_Tier_Manual_Overrides_It()
        {
            Assert.AreEqual(QualityTier.High, QualityService.Resolve(QualityModes.GraphicsAuto, QualityTier.High));
            Assert.AreEqual(QualityTier.Low, QualityService.Resolve(QualityModes.GraphicsLow, QualityTier.High));
            Assert.AreEqual(QualityTier.Mid, QualityService.Resolve(QualityModes.GraphicsMedium, QualityTier.Low));
            Assert.AreEqual(QualityTier.High, QualityService.Resolve(QualityModes.GraphicsHigh, QualityTier.Low));
        }

        [Test]
        public void Frame_Rate_Modes_Resolve_To_Sensible_Targets()
        {
            Assert.AreEqual(30, QualityService.ResolveFps(QualityModes.FpsAuto, QualityTier.Low, 30, 120));
            Assert.AreEqual(60, QualityService.ResolveFps(QualityModes.FpsAuto, QualityTier.Mid, 60, 120));
            Assert.AreEqual(30, QualityService.ResolveFps(QualityModes.Fps30, QualityTier.High, 60, 120));
            Assert.AreEqual(60, QualityService.ResolveFps(QualityModes.Fps60, QualityTier.Low, 30, 120));
            Assert.AreEqual(120, QualityService.ResolveFps(QualityModes.FpsMax, QualityTier.High, 60, 144), "capped at 120");
            Assert.AreEqual(90, QualityService.ResolveFps(QualityModes.FpsMax, QualityTier.High, 60, 90));
            Assert.AreEqual(60, QualityService.ResolveFps(QualityModes.FpsMax, QualityTier.Mid, 60, 120), "Max only on High tier");
            Assert.AreEqual(60, QualityService.ResolveFps(QualityModes.FpsMax, QualityTier.High, 60, 60));
        }

        [Test]
        public void Mode_Buttons_Cycle_Through_All_Four_And_Wrap()
        {
            int g = 0; for (int i = 0; i < 4; i++) g = QualityModes.NextGraphics(g);
            Assert.AreEqual(0, g);
            Assert.AreEqual(QualityModes.GraphicsLow, QualityModes.NextGraphics(QualityModes.GraphicsAuto));
            Assert.AreEqual(QualityModes.GraphicsAuto, QualityModes.NextGraphics(QualityModes.GraphicsHigh));
            Assert.AreEqual("AUTO", QualityModes.GraphicsName(0));
            Assert.AreEqual("MAX", QualityModes.FpsName(QualityModes.FpsMax));
        }

        [Test]
        public void An_Old_Save_Without_The_New_Fields_Loads_As_Auto_And_Round_Trips()
        {
            // A version-4 save written before Phase 3: no graphicsMode / frameRateMode keys.
            const string oldJson = "{\"version\":4,\"coins\":120,\"settings\":{\"musicVolume\":0.3,\"soundVolume\":0.8,\"steeringSensitivity\":1.5}}";
            SaveData data = JsonUtility.FromJson<SaveData>(oldJson);
            Assert.AreEqual(0, data.settings.graphicsMode);
            Assert.AreEqual(0, data.settings.frameRateMode);
            Assert.AreEqual(120, data.coins);
            Assert.AreEqual(0.3f, data.settings.musicVolume, 1e-4f);
            data.settings.graphicsMode = 2; data.settings.frameRateMode = 3;
            SaveData back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            Assert.AreEqual(2, back.settings.graphicsMode);
            Assert.AreEqual(3, back.settings.frameRateMode);
        }

        [Test]
        public void Out_Of_Range_Modes_Are_Clamped()
        {
            var s = new GameSettings { graphicsMode = 9, frameRateMode = -4 };
            s.Clamp();
            Assert.AreEqual(3, s.graphicsMode);
            Assert.AreEqual(0, s.frameRateMode);
        }
    }

    public class AdaptiveScalerTests
    {
        // Runs the scaler for 'seconds' at a constant frame time; returns the scale at the end.
        private static float Run(AdaptiveScaler s, float seconds, float frameMs, float gpuMs = -1f, float cpuMs = -1f)
        {
            float dt = frameMs / 1000f;
            for (float t = 0f; t < seconds; t += dt) s.Tick(dt, gpuMs, cpuMs);
            return s.Scale;
        }

        [Test]
        public void Holds_The_Scale_When_Frames_Are_On_Budget()
        {
            var s = new AdaptiveScaler(1f, 0.65f, 1f, 60f);
            Assert.AreEqual(1f, Run(s, 30f, 16.6f), 1e-4f);
        }

        [Test]
        public void Sustained_Slow_Frames_Step_The_Scale_Down_Within_A_Few_Seconds_And_Stop_At_The_Minimum()
        {
            var s = new AdaptiveScaler(1f, 0.65f, 1f, 60f);
            float after = Run(s, 3f, 25f);                       // 40 fps on a 60 fps target
            Assert.Less(after, 1f);
            Assert.AreEqual(0.95f, after, 1e-4f, "one 0.05 step per evaluation, not a cliff");
            Run(s, 60f, 25f);
            Assert.AreEqual(0.65f, s.Scale, 1e-4f, "never below the minimum");
        }

        [Test]
        public void A_Few_Hitches_Do_Not_Lower_The_Scale()
        {
            var s = new AdaptiveScaler(1f, 0.65f, 1f, 60f);
            for (int i = 0; i < 600; i++) s.Tick(i % 100 == 0 ? 0.060f : 0.0166f);   // 1% hitches
            Assert.AreEqual(1f, s.Scale, 1e-4f);
        }

        [Test]
        public void Loading_Frames_Over_A_Quarter_Second_Are_Ignored()
        {
            var s = new AdaptiveScaler(1f, 0.65f, 1f, 60f);
            for (int i = 0; i < 10; i++) s.Tick(0.5f);
            Run(s, 10f, 16.6f);
            Assert.AreEqual(1f, s.Scale, 1e-4f);
        }

        [Test]
        public void With_Gpu_Headroom_It_Climbs_Back_After_The_Lock_Expires()
        {
            var s = new AdaptiveScaler(0.80f, 0.65f, 1f, 60f);
            Run(s, 2.1f, 25f);                                    // slow: one evaluation, drops and locks for 20 s
            float dropped = s.Scale;
            Assert.Less(dropped, 0.80f);
            Run(s, 15f, 16.6f, gpuMs: 8f);                        // headroom, but still locked
            Assert.AreEqual(dropped, s.Scale, 1e-4f, "locked");
            Run(s, 20f, 16.6f, gpuMs: 8f);
            Assert.Greater(s.Scale, dropped, "climbs once the lock has passed");
        }

        [Test]
        public void Without_Gpu_Timing_It_Only_Probes_Upward_After_A_Long_Stable_Stretch()
        {
            var s = new AdaptiveScaler(0.80f, 0.65f, 1f, 60f);
            Run(s, 2.1f, 25f);
            float dropped = s.Scale;
            Run(s, 30f, 16.6f);
            Assert.AreEqual(dropped, s.Scale, 1e-4f, "no evidence of headroom yet");
            Run(s, 30f, 16.6f);
            Assert.Greater(s.Scale, dropped, "after 40+ s stable it tries one step up");
        }

        [Test]
        public void Dropping_Right_After_A_Climb_Doubles_The_Wait_So_It_Cannot_Flap()
        {
            var s = new AdaptiveScaler(0.80f, 0.65f, 1f, 60f);
            Run(s, 2.1f, 25f);                                    // drop, lock 20 s
            float dropped = s.Scale;
            int guard = 0;
            while (s.Scale <= dropped && guard++ < 100) Run(s, 2f, 16.6f, gpuMs: 8f);   // wait for the climb
            float climbed = s.Scale;
            Assert.Greater(climbed, dropped, "it climbed");
            Run(s, 2.1f, 25f, gpuMs: 24f, cpuMs: 6f);             // the climb was too much (GPU now busy): drops straight away, lock doubles to 40 s
            Assert.Less(s.Scale, climbed);
            float low = s.Scale;
            Run(s, 30f, 16.6f, gpuMs: 8f);
            Assert.AreEqual(low, s.Scale, 1e-4f, "still locked at 30 s (needs 40)");
            Run(s, 20f, 16.6f, gpuMs: 8f);
            Assert.Greater(s.Scale, low, "free again after the longer lock");
        }

        [Test]
        public void Thirty_Fps_Target_Uses_A_Thirty_Fps_Budget()
        {
            var s = new AdaptiveScaler(0.7f, 0.55f, 0.8f, 30f);
            Assert.AreEqual(0.7f, Run(s, 20f, 33f), 1e-4f, "33 ms frames are on budget at 30 fps");
            Assert.Less(Run(s, 4f, 50f), 0.7f, "50 ms frames are not");
        }

        [Test]
        public void A_Display_Locked_Below_The_Target_Does_Not_Lower_The_Scale()
        {
            // Xiaomi M2007J3SP: 20 ms frames (a fixed 50 Hz mode) against a 60 fps target, GPU 4.5 ms, CPU 3 ms.
            var s = new AdaptiveScaler(1f, 0.75f, 1f, 60f);
            Assert.AreEqual(1f, Run(s, 60f, 20f, gpuMs: 4.5f, cpuMs: 3f), 1e-4f, "idle GPU: not a rendering problem");
        }

        [Test]
        public void Budgeting_Against_The_Real_Refresh_Rate_Treats_Fifty_Hz_As_On_Time()
        {
            var s = new AdaptiveScaler(1f, 0.75f, 1f, 60f);
            s.SetTargetFps(51f);
            Assert.AreEqual(1f, Run(s, 60f, 20f), 1e-4f, "no GPU timing needed once the budget matches the display");
        }

        [Test]
        public void Slow_Frames_With_A_Busy_Gpu_Still_Lower_The_Scale()
        {
            var s = new AdaptiveScaler(1f, 0.65f, 1f, 60f);
            Assert.Less(Run(s, 2.1f, 25f, gpuMs: 24f, cpuMs: 6f), 1f);
        }

        [Test]
        public void Thermal_Cap_Pulls_The_Scale_Down_At_Once_And_Limits_Climbing()
        {
            var s = new AdaptiveScaler(1f, 0.65f, 1f, 60f);
            s.SetMax(0.7f);
            Assert.AreEqual(0.7f, s.Scale, 1e-4f);
            Run(s, 120f, 16.6f, gpuMs: 6f);
            Assert.LessOrEqual(s.Scale, 0.7f + 1e-4f);
        }
    }
}
