using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UltimateCarParking.Save;

namespace UltimateCarParking.Tests
{
    public class SaveManagerTests
    {
        private string path;

        [SetUp]
        public void SetUp() => path = Path.Combine(Application.temporaryCachePath, $"save_test_{System.Guid.NewGuid():N}.json");

        [TearDown]
        public void TearDown()
        {
            foreach (string f in new[] { path, path + ".bak", path + ".tmp" }) if (File.Exists(f)) File.Delete(f);
        }

        [Test]
        public void Fresh_Save_Is_Empty_And_Current_Version()
        {
            var save = new SaveManager(path);
            Assert.AreEqual(0, save.Data.coins);
            Assert.AreEqual(SaveData.CurrentVersion, save.Data.version);
            Assert.IsEmpty(save.Data.levels);
            Assert.IsFalse(File.Exists(path), "nothing written until Save()");
        }

        [Test]
        public void Coins_Cars_Selection_And_Stars_Survive_A_Restart()
        {
            var save = new SaveManager(path);
            save.Data.coins = 1234;
            save.Data.ownedCarIds.Add("sedan");
            save.Data.ownedCarIds.Add("hatch");
            save.Data.selectedCarId = "hatch";
            LevelProgress p = save.Data.GetOrAddLevel("L01");
            p.bestStars = 3; p.bestTimeSeconds = 21.5f; p.completions = 2;
            Assert.IsTrue(save.Save());

            var reloaded = new SaveManager(path);   // a new instance reading the file = an app restart
            Assert.AreEqual(1234, reloaded.Data.coins);
            CollectionAssert.AreEqual(new[] { "sedan", "hatch" }, reloaded.Data.ownedCarIds);
            Assert.AreEqual("hatch", reloaded.Data.selectedCarId);
            Assert.AreEqual(3, reloaded.Data.FindLevel("L01").bestStars);
            Assert.AreEqual(21.5f, reloaded.Data.FindLevel("L01").bestTimeSeconds, 1e-4f);
            Assert.AreEqual(2, reloaded.Data.FindLevel("L01").completions);
            Assert.IsFalse(File.Exists(path + ".tmp"), "temp file replaced atomically");
        }

        [Test]
        public void Corrupt_File_Falls_Back_To_The_Backup()
        {
            var save = new SaveManager(path);
            save.Data.coins = 100;
            save.Save();
            save.Data.coins = 200;
            save.Save();                                   // .bak now holds the 100-coin save
            File.WriteAllText(path, "{ this is not json");
            LogAssert.ignoreFailingMessages = true;
            var reloaded = new SaveManager(path);
            LogAssert.ignoreFailingMessages = false;
            Assert.AreEqual(100, reloaded.Data.coins);
        }

        [Test]
        public void Corrupt_File_Without_Backup_Starts_Fresh()
        {
            File.WriteAllText(path, "garbage");
            LogAssert.ignoreFailingMessages = true;
            var reloaded = new SaveManager(path);
            LogAssert.ignoreFailingMessages = false;
            Assert.AreEqual(0, reloaded.Data.coins);
            Assert.IsNotNull(reloaded.Data.levels);
        }

        [Test]
        public void Unversioned_Save_Migrates_To_Current()
        {
            File.WriteAllText(path, "{\"coins\":55,\"selectedCarId\":\"sedan\"}");   // written before versioning (no version, no lists)
            var reloaded = new SaveManager(path);
            Assert.AreEqual(SaveData.CurrentVersion, reloaded.Data.version);
            Assert.AreEqual(55, reloaded.Data.coins);
            Assert.IsNotNull(reloaded.Data.ownedCarIds);
            Assert.IsNotNull(reloaded.Data.levels);
        }

        [Test]
        public void Newer_Save_Is_Read_Not_Downgraded()
        {
            File.WriteAllText(path, $"{{\"version\":{SaveData.CurrentVersion + 1},\"coins\":9}}");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("newer than this build"));
            var reloaded = new SaveManager(path);
            Assert.AreEqual(SaveData.CurrentVersion + 1, reloaded.Data.version);
            Assert.AreEqual(9, reloaded.Data.coins);
        }

        [Test]
        public void Negative_Coins_And_Broken_Entries_Are_Repaired()
        {
            File.WriteAllText(path, "{\"version\":1,\"coins\":-50,\"levels\":[{\"levelId\":\"\"},{\"levelId\":\"L02\",\"bestStars\":2}]}");
            var reloaded = new SaveManager(path);
            Assert.AreEqual(0, reloaded.Data.coins);
            Assert.AreEqual(1, reloaded.Data.levels.Count);
            Assert.AreEqual("L02", reloaded.Data.levels[0].levelId);
        }
    }
}
