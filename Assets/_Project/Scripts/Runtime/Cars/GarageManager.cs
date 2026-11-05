using System;
using UnityEngine;
using UltimateCarParking.Save;

namespace UltimateCarParking.Cars
{
    /// <summary>
    /// Garage scene service: browse the catalog, buy and select cars. Every change is saved immediately.
    /// One per scene; a duplicate removes itself.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GarageManager : MonoBehaviour
    {
        [SerializeField] private CarCatalog catalog;

        public static GarageManager Instance { get; private set; }

        /// <summary>Raised after a purchase or selection change.</summary>
        public event Action Changed;

        public CarCatalog Catalog => catalog;
        public int Coins => Store.Data.coins;
        public CarData SelectedCar => GarageService.SelectedCar(Store.Data, catalog);

        private static SaveManager Store => SaveManager.Current;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GarageManager] Duplicate on '{name}' removed.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            if (catalog == null)
            {
                Debug.LogError("[GarageManager] No CarCatalog assigned.", this);
                return;
            }
            GarageService.EnsureDefaults(Store.Data, catalog);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsOwned(CarData car) => GarageService.IsOwned(Store.Data, car);
        public bool IsSelected(CarData car) => car != null && SelectedCar == car;
        public bool CanAfford(CarData car) => GarageService.CanAfford(Store.Data, car);

        public PurchaseResult Buy(CarData car)
        {
            PurchaseResult r = GarageService.TryBuy(Store.Data, car);
            if (r == PurchaseResult.Purchased)
            {
                Store.Save();
                Changed?.Invoke();
            }
            return r;
        }

        public bool Select(CarData car)
        {
            if (!GarageService.Select(Store.Data, car)) return false;
            Store.Save();
            Changed?.Invoke();
            return true;
        }
    }
}
