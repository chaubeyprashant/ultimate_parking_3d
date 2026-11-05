using UltimateCarParking.Save;

namespace UltimateCarParking.Cars
{
    public enum PurchaseResult { Purchased, AlreadyOwned, NotEnoughCoins, InvalidCar }

    /// <summary>Ownership and selection rules over SaveData. No scene state; unit-tested directly.</summary>
    public static class GarageService
    {
        /// <summary>Grants starter cars and repairs the selection (missing or not owned → first owned car).</summary>
        public static void EnsureDefaults(SaveData data, CarCatalog catalog)
        {
            if (data == null || catalog == null) return;
            for (int i = 0; i < catalog.Count; i++)
            {
                CarData c = catalog.Get(i);
                if (c != null && c.OwnedByDefault && !data.ownedCarIds.Contains(c.CarId)) data.ownedCarIds.Add(c.CarId);
            }
            CarData selected = catalog.FindById(data.selectedCarId);
            if (selected == null || !IsOwned(data, selected))
            {
                CarData fallback = catalog.Default;
                for (int i = 0; i < catalog.Count && (fallback == null || !IsOwned(data, fallback)); i++)
                    if (catalog.Get(i) != null && IsOwned(data, catalog.Get(i))) fallback = catalog.Get(i);
                data.selectedCarId = fallback != null ? fallback.CarId : "";
            }
        }

        public static bool IsOwned(SaveData data, CarData car) =>
            data != null && car != null && (car.OwnedByDefault || data.ownedCarIds.Contains(car.CarId));

        public static bool CanAfford(SaveData data, CarData car) => data != null && car != null && data.coins >= car.Price;

        public static PurchaseResult TryBuy(SaveData data, CarData car)
        {
            if (data == null || car == null || string.IsNullOrEmpty(car.CarId)) return PurchaseResult.InvalidCar;
            if (IsOwned(data, car)) return PurchaseResult.AlreadyOwned;
            if (data.coins < car.Price) return PurchaseResult.NotEnoughCoins;
            data.coins -= car.Price;
            data.ownedCarIds.Add(car.CarId);
            return PurchaseResult.Purchased;
        }

        public static bool Select(SaveData data, CarData car)
        {
            if (!IsOwned(data, car)) return false;
            data.selectedCarId = car.CarId;
            return true;
        }

        /// <summary>The car to drive: the saved selection if owned, else the catalog default.</summary>
        public static CarData SelectedCar(SaveData data, CarCatalog catalog)
        {
            if (catalog == null) return null;
            CarData c = data != null ? catalog.FindById(data.selectedCarId) : null;
            return c != null && IsOwned(data, c) ? c : catalog.Default;
        }
    }
}
