using System.IO;
using NUnit.Framework;
using UnityEngine;
using UltimateCarParking.Save;
using UltimateCarParking.Settings;

namespace UltimateCarParking.Tests
{
    public class SettingsTests
    {
        private string path;

        [SetUp]
        public void SetUp()
        {
            path = Path.Combine(Application.temporaryCachePath, $"settings_test_{System.Guid.NewGuid():N}.json");
            SaveManager.Initialize(path);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string f in new[] { path, path + ".bak" }) if (File.Exists(f)) File.Delete(f);
        }

        [Test]
        public void Version_1_Save_Migrates_To_2_With_Default_Settings()
        {
            File.WriteAllText(path, "{\"version\":1,\"coins\":40,\"selectedCarId\":\"hatch\",\"ownedCarIds\":[\"sedan\",\"hatch\"]}");
            var save = new SaveManager(path);
            Assert.AreEqual(2, save.Data.version);
            Assert.AreEqual(40, save.Data.coins, "existing progress kept");
            Assert.IsNotNull(save.Data.settings);
            Assert.AreEqual(new GameSettings().musicVolume, save.Data.settings.musicVolume);
            Assert.AreEqual(1f, save.Data.settings.steeringSensitivity);
        }

        [Test]
        public void Settings_Change_Live_And_Persist_On_Commit()
        {
            GameSettings seen = null;
            void OnChanged(GameSettings s) => seen = s;
            SettingsService.Changed += OnChanged;
            SettingsService.SetMusicVolume(0.2f);
            SettingsService.SetSoundVolume(0.7f);
            SettingsService.SetSteeringSensitivity(1.6f);
            SettingsService.Changed -= OnChanged;
            Assert.IsNotNull(seen, "listeners notified");
            Assert.IsFalse(File.Exists(path), "not written until committed");
            SettingsService.Commit();

            var restarted = new SaveManager(path);
            Assert.AreEqual(0.2f, restarted.Data.settings.musicVolume, 1e-4f);
            Assert.AreEqual(0.7f, restarted.Data.settings.soundVolume, 1e-4f);
            Assert.AreEqual(1.6f, restarted.Data.settings.steeringSensitivity, 1e-4f);
        }

        [Test]
        public void Out_Of_Range_Values_Are_Clamped()
        {
            SettingsService.SetMusicVolume(3f);
            SettingsService.SetSoundVolume(-1f);
            SettingsService.SetSteeringSensitivity(10f);
            Assert.AreEqual(1f, SettingsService.Current.musicVolume);
            Assert.AreEqual(0f, SettingsService.Current.soundVolume);
            Assert.AreEqual(GameSettings.MaxSensitivity, SettingsService.Current.steeringSensitivity);
            File.WriteAllText(path, "{\"version\":2,\"settings\":{\"musicVolume\":-4,\"soundVolume\":9,\"steeringSensitivity\":0}}");
            var loaded = new SaveManager(path);
            Assert.AreEqual(0f, loaded.Data.settings.musicVolume);
            Assert.AreEqual(1f, loaded.Data.settings.soundVolume);
            Assert.AreEqual(1f, loaded.Data.settings.steeringSensitivity, "0 = unset → default");
        }
    }
}
