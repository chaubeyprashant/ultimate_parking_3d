using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UltimateCarParking.Quality;
using UltimateCarParking.Save;
using UltimateCarParking.Settings;

namespace UltimateCarParking.Tests
{
    /// <summary>The quality service against the real URP asset: it changes a runtime copy and never the asset in the project.</summary>
    public class QualityServiceTests
    {
        private RenderPipelineAsset originalPipeline;
        private float origScale, origShadow;
        private int origMsaa, origTarget, origVsync;

        [SetUp]
        public void SetUp()
        {
            TestScenes.UseTemporarySave();
            originalPipeline = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
            var urp = (UniversalRenderPipelineAsset)originalPipeline;
            origScale = urp.renderScale; origMsaa = urp.msaaSampleCount; origShadow = urp.shadowDistance;
            origTarget = Application.targetFrameRate; origVsync = QualitySettings.vSyncCount;
        }

        [TearDown]
        public void TearDown()
        {
            QualitySettings.renderPipeline = originalPipeline as UniversalRenderPipelineAsset;
            Application.targetFrameRate = origTarget;
            QualitySettings.vSyncCount = origVsync;
        }

        private static DeviceInfo Phone(string gpu, int ram = 6000) => new DeviceInfo(ram, 8, gpu, 33, 120);

        [Test]
        public void Auto_Applies_The_Detected_Tier_To_A_Runtime_Copy_Not_The_Asset()
        {
            QualityService.Initialize(Phone("Mali-G68", 5500));          // Galaxy A33 class
            Assert.AreEqual(QualityTier.Mid, QualityService.ActiveTier);
            var live = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
            Assert.AreNotSame(originalPipeline, live, "a copy is in use");
            Assert.AreEqual(0.85f, live.renderScale, 1e-4f);
            Assert.AreEqual(1, live.msaaSampleCount);
            Assert.AreEqual(25f, live.shadowDistance, 1e-4f);
            Assert.AreEqual(60, Application.targetFrameRate);
            Assert.AreEqual(0, QualitySettings.vSyncCount, "vSync off so the target frame rate is honoured");
            var asset = (UniversalRenderPipelineAsset)originalPipeline;
            Assert.AreEqual(origScale, asset.renderScale, 1e-6f, "project asset untouched");
            Assert.AreEqual(origMsaa, asset.msaaSampleCount);
            Assert.AreEqual(origShadow, asset.shadowDistance, 1e-6f);
        }

        [Test]
        public void High_Tier_Keeps_The_Original_Look_And_Low_Tier_Runs_At_Thirty()
        {
            QualityService.Initialize(Phone("Adreno (TM) 650"));
            Assert.AreEqual(QualityTier.High, QualityService.ActiveTier);
            Assert.AreEqual(1f, QualityService.RenderScale, 1e-4f);
            Assert.AreEqual(2, ((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).msaaSampleCount);

            SettingsService.SetGraphicsMode(QualityModes.GraphicsLow);
            Assert.AreEqual(QualityTier.Low, QualityService.ActiveTier);
            Assert.AreEqual(0.7f, QualityService.RenderScale, 1e-4f);
            Assert.AreEqual(30, Application.targetFrameRate);
            Assert.IsFalse(QualityService.AdaptiveEnabled, "a hand-picked level is fixed, not adapted");

            SettingsService.SetGraphicsMode(QualityModes.GraphicsAuto);
            Assert.AreEqual(QualityTier.High, QualityService.ActiveTier);
            Assert.IsTrue(QualityService.AdaptiveEnabled);
        }

        [Test]
        public void Frame_Rate_Choice_Applies_Immediately_And_Max_Needs_The_High_Tier()
        {
            QualityService.Initialize(Phone("Adreno (TM) 650"));
            SettingsService.SetFrameRateMode(QualityModes.Fps30);
            Assert.AreEqual(30, Application.targetFrameRate);
            SettingsService.SetFrameRateMode(QualityModes.FpsMax);
            Assert.AreEqual(120, Application.targetFrameRate);
            SettingsService.SetGraphicsMode(QualityModes.GraphicsMedium);
            Assert.AreEqual(60, Application.targetFrameRate, "Max is only for the High tier");
        }

        [Test]
        public void Thermal_Throttling_Caps_Frame_Rate_And_Scale_Then_Releases()
        {
            QualityService.Initialize(Phone("Adreno (TM) 650"));
            Assert.AreEqual(1f, QualityService.RenderScale, 1e-4f);
            QualityService.SetThermalCaps(30f, 0.7f);
            Assert.AreEqual(30, Application.targetFrameRate);
            Assert.AreEqual(0.7f, QualityService.RenderScale, 1e-4f);
            QualityService.SetThermalCaps(0f, 0f);
            Assert.AreEqual(60, Application.targetFrameRate);
            Assert.AreEqual(1f, QualityService.RenderScale, 1e-4f);
        }

        [Test]
        public void The_Adaptive_Render_Scale_Is_Applied_To_The_Runtime_Copy()
        {
            QualityService.Initialize(Phone("Mali-G68", 5500));
            QualityService.SetRenderScale(0.75f);
            Assert.AreEqual(0.75f, ((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale, 1e-4f);
            Assert.AreEqual(origScale, ((UniversalRenderPipelineAsset)originalPipeline).renderScale, 1e-6f);
        }
    }
}
