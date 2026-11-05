using UnityEngine;

namespace UltimateCarParking.Cars
{
    /// <summary>Every car in the garage, in display order.</summary>
    [CreateAssetMenu(fileName = "CarCatalog", menuName = "Ultimate Car Parking/Car Catalog", order = 11)]
    public sealed class CarCatalog : ScriptableObject
    {
        [SerializeField] private CarData[] cars = System.Array.Empty<CarData>();

        public int Count => cars.Length;
        public CarData Get(int index) => index >= 0 && index < cars.Length ? cars[index] : null;
        public int IndexOf(CarData car) => car == null ? -1 : System.Array.IndexOf(cars, car);

        public CarData FindById(string carId)
        {
            if (string.IsNullOrEmpty(carId)) return null;
            foreach (CarData c in cars)
                if (c != null && c.CarId == carId) return c;
            return null;
        }

        /// <summary>The first car owned by default (the starter), else the first car.</summary>
        public CarData Default
        {
            get
            {
                foreach (CarData c in cars)
                    if (c != null && c.OwnedByDefault) return c;
                return cars.Length > 0 ? cars[0] : null;
            }
        }

        private void OnValidate()
        {
            for (int i = 0; i < cars.Length; i++)
                for (int j = i + 1; j < cars.Length; j++)
                    if (cars[i] != null && cars[j] != null && cars[i].CarId == cars[j].CarId)
                        Debug.LogWarning($"[CarCatalog] duplicate car id '{cars[i].CarId}'.", this);
        }
    }
}
