using UnityEngine;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Cars
{
    /// <summary>Market class: sets the price band. Budget cars cost least, premium cars most (see FleetPricing).</summary>
    public enum CarClass { Starter = 0, Economy = 1, Standard = 2, Performance = 3, Premium = 4, Elite = 5 }

    /// <summary>
    /// One car the player can own: id, name, model prefab, paint, price and handling. Data only.
    /// Create via Assets ▸ Create ▸ Ultimate Car Parking ▸ Car Data and add it to the CarCatalog.
    /// </summary>
    [CreateAssetMenu(fileName = "Car_", menuName = "Ultimate Car Parking/Car Data", order = 10)]
    public sealed class CarData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique, stable id stored in the save file. Do not change after release.")]
        [SerializeField] private string carId = "sedan";
        [SerializeField] private string displayName = "City Sedan";
        [SerializeField] private string tagline = "Balanced all-rounder";

        [Header("Model")]
        [Tooltip("Drivable prefab: Rigidbody + VehicleController + VehicleInput + WheelVisuals (+ CarPaint for paint).")]
        [SerializeField] private GameObject modelPrefab;
        [Tooltip("Body paint; applied to the renderers listed in the prefab's CarPaint. Empty = keep the prefab's paint.")]
        [SerializeField] private Material paintMaterial;

        [Header("Economy")]
        [SerializeField] private CarClass carClass = CarClass.Standard;
        [SerializeField, Min(0)] private int price;
        [Tooltip("Owned from the first launch (the starter car).")]
        [SerializeField] private bool ownedByDefault;

        [Header("Handling")]
        [SerializeField] private VehicleHandling handling = new VehicleHandling();

        [Header("Spec sheet (measured, imported by FleetSpecs)")]
        [SerializeField] private CarSpecs specs = new CarSpecs();

        public string CarId => carId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? carId : displayName;
        public string Tagline => tagline;
        public GameObject ModelPrefab => modelPrefab;
        public Material PaintMaterial => paintMaterial;
        public CarClass Class => carClass;
        public string ClassLabel => carClass == CarClass.Starter ? "STARTER" : carClass.ToString().ToUpperInvariant();
        public int Price => price;
        public bool OwnedByDefault => ownedByDefault;
        public VehicleHandling Handling => handling;
        public CarSpecs Specs => specs ??= new CarSpecs();

        /// <summary>Applies paint and handling to a spawned instance of <see cref="ModelPrefab"/>.</summary>
        public void ApplyTo(GameObject instance)
        {
            if (instance == null) return;
            if (paintMaterial != null && instance.TryGetComponent(out CarPaint paint)) paint.Apply(paintMaterial);
            if (instance.TryGetComponent(out VehicleController controller)) controller.ApplyHandling(handling);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(carId)) carId = name;
        }
    }
}
