using NUnit.Framework;
using UnityEditor;
using UltimateCarParking.Cars;
using UltimateCarParking.Save;

namespace UltimateCarParking.Tests
{
    public class GarageServiceTests
    {
        private CarCatalog catalog;
        private SaveData data;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/_Project/Cars/CarCatalog.asset");
            data = new SaveData();
            GarageService.EnsureDefaults(data, catalog);
        }

        [Test]
        public void Starter_Car_Is_Owned_And_Selected()
        {
            CarData starter = catalog.Default;
            Assert.IsTrue(starter.OwnedByDefault);
            Assert.AreEqual(0, starter.Price);
            Assert.IsTrue(GarageService.IsOwned(data, starter));
            Assert.AreEqual(starter.CarId, data.selectedCarId);
            Assert.AreSame(starter, GarageService.SelectedCar(data, catalog));
        }

        [Test]
        public void Buying_Needs_Enough_Coins_And_Deducts_The_Price()
        {
            CarData hatch = catalog.FindById("hatch");
            data.coins = hatch.Price - 1;
            Assert.AreEqual(PurchaseResult.NotEnoughCoins, GarageService.TryBuy(data, hatch));
            Assert.IsFalse(GarageService.IsOwned(data, hatch));
            Assert.AreEqual(hatch.Price - 1, data.coins);

            data.coins = hatch.Price + 10;
            Assert.AreEqual(PurchaseResult.Purchased, GarageService.TryBuy(data, hatch));
            Assert.IsTrue(GarageService.IsOwned(data, hatch));
            Assert.AreEqual(10, data.coins);
            Assert.AreEqual(PurchaseResult.AlreadyOwned, GarageService.TryBuy(data, hatch));
            Assert.AreEqual(10, data.coins, "no double charge");
        }

        [Test]
        public void Only_Owned_Cars_Can_Be_Selected()
        {
            CarData sport = catalog.FindById("sport");
            Assert.IsFalse(GarageService.Select(data, sport));
            Assert.AreNotEqual("sport", data.selectedCarId);
            data.coins = sport.Price;
            GarageService.TryBuy(data, sport);
            Assert.IsTrue(GarageService.Select(data, sport));
            Assert.AreSame(sport, GarageService.SelectedCar(data, catalog));
        }

        [Test]
        public void Unknown_Or_Unowned_Selection_Falls_Back_To_The_Starter()
        {
            data.selectedCarId = "deleted_car";
            Assert.AreSame(catalog.Default, GarageService.SelectedCar(data, catalog));
            data.selectedCarId = "pickup";   // not owned (e.g. edited save)
            GarageService.EnsureDefaults(data, catalog);
            Assert.AreEqual(catalog.Default.CarId, data.selectedCarId);
        }

        [Test]
        public void Null_Inputs_Are_Safe()
        {
            Assert.AreEqual(PurchaseResult.InvalidCar, GarageService.TryBuy(data, null));
            Assert.IsFalse(GarageService.Select(data, null));
            Assert.IsNull(GarageService.SelectedCar(data, null));
            Assert.DoesNotThrow(() => GarageService.EnsureDefaults(null, catalog));
        }
    }
}
