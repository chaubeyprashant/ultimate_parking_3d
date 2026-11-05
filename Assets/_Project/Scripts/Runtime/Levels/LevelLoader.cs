using UnityEngine;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Cars;
using UltimateCarParking.Environment;
using UltimateCarParking.Save;
using UltimateCarParking.Parking;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.Levels
{
    /// <summary>
    /// Builds a level from its LevelData: instantiates the environment, spawns the car at the spawn point,
    /// creates the ParkingZone at the target, points the camera at the car and hands everything to the
    /// LevelManager. All references are Inspector-assigned; nothing is searched for during gameplay.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class LevelLoader : MonoBehaviour
    {
        [Header("Levels")]
        [SerializeField] private LevelCatalog catalog;
        [Tooltip("Level loaded when nothing was requested through LevelSelection (e.g. pressing Play in Gameplay).")]
        [SerializeField, Min(0)] private int defaultLevelIndex;
        [SerializeField] private bool loadOnStart = true;

        [Header("Spawning")]
        [Tooltip("Cars the player can own; the saved selection is spawned. Empty = always Vehicle Prefab.")]
        [SerializeField] private CarCatalog carCatalog;
        [Tooltip("Fallback car when no catalog / selection is available.")]
        [SerializeField] private GameObject vehiclePrefab;
        [Tooltip("Height added to the spawn point so the wheels settle onto the ground instead of starting in it.")]
        [SerializeField, Min(0f)] private float spawnLift = 0.2f;
        [SerializeField] private Material zoneMarkerMaterial;

        [Header("Scene references")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private VehicleCamera vehicleCamera;

        public static LevelLoader Instance { get; private set; }

        /// <summary>The catalog the current level came from (the requested one, else the Inspector one).</summary>
        public LevelCatalog Catalog => activeCatalog != null ? activeCatalog : catalog;
        public CarData CurrentCar { get; private set; }

        private LevelCatalog activeCatalog;
        public LevelData CurrentLevel { get; private set; }
        public GameObject Environment { get; private set; }
        public VehicleController Vehicle { get; private set; }
        public ParkingZone Zone { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[LevelLoader] A LevelLoader already exists on '{Instance.name}'; removing the duplicate on '{name}'.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (!loadOnStart) return;
            activeCatalog = LevelSelection.ConsumeCatalog();
            LevelData level = LevelSelection.Consume();
            if (level == null)
            {
                if (catalog == null)
                {
                    Debug.LogError("[LevelLoader] No LevelCatalog assigned and no level requested; nothing to load.", this);
                    return;
                }
                level = catalog.Get(defaultLevelIndex);
                if (level == null)
                {
                    Debug.LogError($"[LevelLoader] LevelCatalog has no level at index {defaultLevelIndex}.", this);
                    return;
                }
            }
            Load(level);
        }

        /// <summary>Tears down the current level (if any) and builds <paramref name="level"/>. Returns false on a setup error.</summary>
        public bool Load(LevelData level)
        {
            if (level == null) { Debug.LogError("[LevelLoader] Load: level is null.", this); return false; }
            CarData carData = null;
            if (carCatalog != null)
            {
                GarageService.EnsureDefaults(SaveManager.Current.Data, carCatalog);
                carData = GarageService.SelectedCar(SaveManager.Current.Data, carCatalog);
            }
            GameObject carPrefab = carData != null && carData.ModelPrefab != null ? carData.ModelPrefab : vehiclePrefab;
            if (carPrefab == null) { Debug.LogError("[LevelLoader] No vehicle prefab assigned and no car selected.", this); return false; }
            if (levelManager == null) { Debug.LogError("[LevelLoader] No LevelManager assigned.", this); return false; }

            Unload();
            CurrentLevel = level;

            if (level.Theme != null)
            {
                Environment = EnvironmentBuilder.Build(level.Theme, level.Props, $"Environment_{level.LevelId}");
                if (level.EnvironmentPrefab != null) Instantiate(level.EnvironmentPrefab, Environment.transform);
            }
            else if (level.EnvironmentPrefab != null)
                Environment = Instantiate(level.EnvironmentPrefab, Vector3.zero, Quaternion.identity);
            else
                Debug.LogWarning($"[LevelLoader] {level.LevelId} has no theme or environment prefab; spawning into an empty scene.", this);

            GameObject car = Instantiate(carPrefab, level.SpawnPosition + Vector3.up * spawnLift, level.SpawnRotation);
            car.name = carPrefab.name;
            if (!car.TryGetComponent(out VehicleController controller))
            {
                Debug.LogError($"[LevelLoader] Vehicle prefab '{carPrefab.name}' has no VehicleController.", this);
                Destroy(car);
                return false;
            }
            Vehicle = controller;
            CurrentCar = carData;
            if (carData != null) carData.ApplyTo(car);
            if (!car.TryGetComponent(out CollisionHandler collisions))
            {
                Debug.LogWarning($"[LevelLoader] '{carPrefab.name}' has no CollisionHandler; adding one with default settings.", this);
                collisions = car.AddComponent<CollisionHandler>();
            }

            var zoneGo = new GameObject($"ParkingZone_{level.LevelId}");
            zoneGo.transform.SetPositionAndRotation(level.TargetPosition, level.TargetRotation);
            Zone = zoneGo.AddComponent<ParkingZone>();
            Zone.Configure(level.TargetSize, zoneMarkerMaterial);

            if (vehicleCamera != null) vehicleCamera.SetTarget(car.transform, snap: true);
            else Debug.LogWarning("[LevelLoader] No VehicleCamera assigned; the camera will not follow the car.", this);

            levelManager.BeginLevel(level, Vehicle, collisions, Zone);
            return true;
        }

        public void Unload()
        {
            if (Environment != null) Destroy(Environment);
            if (Vehicle != null) Destroy(Vehicle.gameObject);
            if (Zone != null) Destroy(Zone.gameObject);
            Environment = null;
            Vehicle = null;
            Zone = null;
            CurrentLevel = null;
        }
    }
}
