using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UltimateCarParking.Cars;
using UltimateCarParking.Save;

namespace UltimateCarParking.Tests
{
    /// <summary>Phase 7 customisation rules: prices, buy once, free re-fit, factory reset, plate text, save v6.</summary>
    public class CustomizationTests
    {
        private static CarData Car(string id) => AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset").FindById(id);

        private static SaveData Owned(string id, int coins)
        {
            var s = new SaveData { coins = coins };
            s.ownedCarIds.Add("sedan"); s.ownedCarIds.Add(id);
            return s;
        }

        [Test]
        public void Options_Are_Bought_Once_Refitting_And_Factory_Are_Free()
        {
            SaveData s = Owned("saba_v12_95", 5000);
            CarData c = Car("saba_v12_95");
            Assert.IsTrue(CustomizationService.TryApply(s, c, CustomSlot.Paint, 4));
            Assert.AreEqual(5000 - CustomizationCatalog.PaintPrice, s.coins);
            Assert.AreEqual(4, CustomizationService.Look(s, c.CarId).paint);
            Assert.IsTrue(CustomizationService.TryApply(s, c, CustomSlot.Paint, -1), "factory is free");
            Assert.IsTrue(CustomizationService.TryApply(s, c, CustomSlot.Paint, 4), "re-fitting a bought colour is free");
            Assert.AreEqual(5000 - CustomizationCatalog.PaintPrice, s.coins, "paid once");
            Assert.IsTrue(CustomizationService.TryApply(s, c, CustomSlot.Finish, 2));
            Assert.AreEqual(5000 - CustomizationCatalog.PaintPrice - CustomizationCatalog.MetallicPrice, s.coins);
            CustomizationService.ResetToFactory(s, c.CarId);
            Assert.IsTrue(CustomizationService.Look(s, c.CarId).IsFactory);
            Assert.AreEqual(0, CustomizationService.CostToApply(s, c.CarId, CustomSlot.Finish, 2), "still owned after a reset");
        }

        [Test]
        public void Not_Owned_Cars_And_Short_Coins_Change_Nothing()
        {
            var s = new SaveData { coins = 100 };
            s.ownedCarIds.Add("sedan");
            Assert.IsFalse(CustomizationService.TryApply(s, Car("cheetah_84"), CustomSlot.Paint, 1), "car not owned");
            Assert.IsFalse(CustomizationService.TryApply(s, Car("sedan"), CustomSlot.Rims, 0), "not enough coins");
            Assert.AreEqual(100, s.coins);
            Assert.IsTrue(CustomizationService.Look(s, "sedan").IsFactory);
        }

        [Test]
        public void Plate_Text_Is_Cleaned_And_Charged_Per_Change()
        {
            Assert.AreEqual("PARK-42", CustomizationCatalog.CleanPlate("park-42"));
            Assert.AreEqual("ABCDEFGH", CustomizationCatalog.CleanPlate("abc!def$ghijk"));
            Assert.AreEqual("", CustomizationCatalog.CleanPlate("x"), "too short");
            SaveData s = Owned("hatch", 1000);
            Assert.IsTrue(CustomizationService.TryApply(s, Car("hatch"), CustomSlot.Plate, 0, "my car 1"));
            Assert.AreEqual("MY CAR 1", CustomizationService.Look(s, "hatch").plate);
            Assert.AreEqual(1000 - CustomizationCatalog.PlatePrice, s.coins);
            Assert.IsTrue(CustomizationService.TryApply(s, Car("hatch"), CustomSlot.Plate, 0, "MY CAR 1"), "same plate: no charge");
            Assert.AreEqual(1000 - CustomizationCatalog.PlatePrice, s.coins);
        }

        [Test]
        public void Prices_Stay_Small_Next_To_Car_Prices()
        {
            int all = CustomizationCatalog.PaintPrice + CustomizationCatalog.MattePrice + CustomizationCatalog.RimPrice + CustomizationCatalog.TintPrice
                      + CustomizationCatalog.HeadlightPrice + CustomizationCatalog.PlatePrice;
            Assert.Less(all, 3500, "a full custom look costs less than the cheapest Standard car");
        }

        [Test]
        public void A_Version_5_Save_Migrates_To_6_And_Looks_Persist()
        {
            string path = Path.Combine(Application.temporaryCachePath, $"look_{System.Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(path, "{\"version\":5,\"coins\":2000,\"ownedCarIds\":[\"sedan\",\"hatch\"],\"selectedCarId\":\"hatch\",\"careerRewardIds\":[\"ch1\"]}");
                var save = new SaveManager(path);
                Assert.AreEqual(6, save.Data.version);
                Assert.AreEqual(2000, save.Data.coins);
                Assert.IsNotNull(save.Data.carLooks);
                CollectionAssert.Contains(save.Data.careerRewardIds, "ch1");
                Assert.IsTrue(CustomizationService.TryApply(save.Data, Car("hatch"), CustomSlot.Tint, 2));
                save.Save();
                var again = new SaveManager(path);
                Assert.AreEqual(2, CustomizationService.Look(again.Data, "hatch").tint, "look survives a restart");
                Assert.AreEqual(2000 - CustomizationCatalog.TintPrice, again.Data.coins);
            }
            finally { foreach (string f in new[] { path, path + ".bak" }) if (File.Exists(f)) File.Delete(f); }
        }
    }
}
