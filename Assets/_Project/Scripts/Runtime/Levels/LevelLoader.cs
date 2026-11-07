using UnityEngine;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Cars;
using UltimateCarParking.Environment;
using UltimateCarParking.Save;
using UltimateCarParking.Parking;
using UltimateCarParking.Vehicle;

using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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
        private AsyncOperationHandle<GameObject> arenaHandle;

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
        /// <summary>The bay the current objective is using (created by the mission runner).</summary>
        public ParkingZone Zone => levelManager != null && levelManager.Mission != null ? levelManager.Mission.ActiveZone : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[LevelLoader] A LevelLoader already exists on '{Instance.name}'; removing the duplicate on '{name}'.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            if (loadOnStart) downloadStatus = "Loading";   // cover the screen from the first frame
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
            CarData carData = level.ForcedCar;
            if (carData == null && carCatalog != null)
            {
                GarageService.EnsureDefaults(SaveManager.Current.Data, carCatalog);
                carData = GarageService.SelectedCar(SaveManager.Current.Data, carCatalog);
            }
            GameObject carPrefab = carData != null && carData.ModelPrefab != null ? carData.ModelPrefab : vehiclePrefab;
            if (carPrefab == null) { Debug.LogError("[LevelLoader] No vehicle prefab assigned and no car selected.", this); return false; }
            if (levelManager == null) { Debug.LogError("[LevelLoader] No LevelManager assigned.", this); return false; }

            if (level.HasAddressableEnvironment)
            {
                // Country arena delivered as an Addressables asset (per-country asset pack on Android). Loaded
                // asynchronously: the Play Asset Delivery provider does not support synchronous loads.
                Unload();
                if (pendingArena != null) StopCoroutine(pendingArena);
                pendingArena = StartCoroutine(LoadArenaThenBuild(level, carData, carPrefab));
                return true;
            }
            return Build(level, carData, carPrefab, null, default);
        }

        private Coroutine pendingArena;

        /// <summary>True while a country arena is still loading from its asset pack.</summary>
        public bool IsLoading => pendingArena != null;

        // Download state for on-demand country packs (shown by OnGUI until the arena is ready).
        private string downloadStatus = "";
        private float downloadProgress;
        private bool downloadFailed;
        private bool retryRequested;
        private bool cancelRequested;

        private System.Collections.IEnumerator LoadArenaThenBuild(LevelData level, CarData carData, GameObject carPrefab)
        {
            // On-demand asset packs must be on the device first; installed packs finish this step instantly.
            while (true)
            {
                downloadFailed = false; retryRequested = false; cancelRequested = false;
                AsyncOperationHandle dl = Addressables.DownloadDependenciesAsync(level.EnvironmentAddress);
                while (!dl.IsDone)
                {
                    downloadProgress = dl.PercentComplete;
                    downloadStatus = "Downloading arena\u2026 " + Mathf.RoundToInt(downloadProgress * 100f) + "%";
                    yield return null;
                }
                bool ok = dl.Status == AsyncOperationStatus.Succeeded;
                if (dl.IsValid()) Addressables.Release(dl);
                if (ok) break;
                downloadFailed = true;
                downloadStatus = "Could not download this country. Check your connection.";
                while (!retryRequested && !cancelRequested) yield return null;
                if (cancelRequested) { downloadStatus = ""; downloadFailed = false; pendingArena = null; LoadCancelled?.Invoke(); UltimateCarParking.UI.ScreenTransition.LoadScene(LevelSelection.ReturnScene); yield break; }
            }
            downloadStatus = "Loading arena\u2026";
            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(level.EnvironmentAddress);
            yield return handle;
            pendingArena = null;
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Debug.LogError($"[LevelLoader] Arena '{level.EnvironmentAddress}' failed to load: {handle.OperationException?.Message}", this);
                if (handle.IsValid()) Addressables.Release(handle);
                downloadStatus = "";
                yield break;
            }
            Build(level, carData, carPrefab, handle.Result, handle);
        }

        /// <summary>Raised when the player gives up on a failed pack download (the UI should return to the menu).</summary>
        public event System.Action LoadCancelled;

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(downloadStatus)) return;
            float w = Screen.width, h = Screen.height, u = Mathf.Max(18f, h / 24f);
            Color old = GUI.color;
            GUI.color = new Color(0.05f, 0.06f, 0.08f, 1f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u), alignment = TextAnchor.MiddleCenter, wordWrap = true };
            label.normal.textColor = Color.white;
            string text = downloadStatus;
            if (text == "Loading") text = "LOADING" + new string('.', 1 + (int)(Time.realtimeSinceStartup * 3f) % 3);
            GUI.Label(new Rect(w * 0.15f, h * 0.35f, w * 0.7f, u * 3f), text, label);
            if (!downloadFailed)
            {
                // Indeterminate sweep, or a real bar while downloading.
                float bw = w * 0.5f, bh = u * 0.5f, bx = w * 0.25f, by = h * 0.55f;
                GUI.color = new Color(1f, 1f, 1f, 0.15f);
                GUI.DrawTexture(new Rect(bx, by, bw, bh), Texture2D.whiteTexture);
                GUI.color = new Color(0.7f, 1f, 0.1f, 1f);
                if (downloadProgress > 0f) GUI.DrawTexture(new Rect(bx, by, bw * Mathf.Clamp01(downloadProgress), bh), Texture2D.whiteTexture);
                else { float t = Mathf.Repeat(Time.realtimeSinceStartup * 0.8f, 1f); GUI.DrawTexture(new Rect(bx + (bw * 0.7f) * t, by, bw * 0.3f, bh), Texture2D.whiteTexture); }
                GUI.color = old;
                return;
            }
            GUI.color = old;
            var btn = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u) };
            if (GUI.Button(new Rect(w * 0.25f, h * 0.55f, w * 0.22f, u * 2.2f), "RETRY", btn)) retryRequested = true;
            if (GUI.Button(new Rect(w * 0.53f, h * 0.55f, w * 0.22f, u * 2.2f), "BACK", btn)) cancelRequested = true;
        }

        private bool Build(LevelData level, CarData carData, GameObject carPrefab, GameObject arena, AsyncOperationHandle<GameObject> handle)
        {
            bool ok = BuildInner(level, carData, carPrefab, arena, handle);
            downloadStatus = "";
            return ok;
        }

        private bool BuildInner(LevelData level, CarData carData, GameObject carPrefab, GameObject arena, AsyncOperationHandle<GameObject> handle)
        {
            Unload();
            CurrentLevel = level;

            if (arena != null)
            {
                arenaHandle = handle;
                Environment = Instantiate(arena, Vector3.zero, Quaternion.identity);
            }
            else if (level.Theme != null)
            {
                Environment = EnvironmentBuilder.Build(level.Theme, level.Props, $"Environment_{level.LevelId}");
                if (level.EnvironmentPrefab != null) Instantiate(level.EnvironmentPrefab, Environment.transform);
            }
            else if (level.EnvironmentPrefab != null)
            {
                Environment = Instantiate(level.EnvironmentPrefab, Vector3.zero, Quaternion.identity);
                EnvironmentBuilder.PlaceProps(level.Props, Environment.transform, "Props");
            }
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
            if (carData != null)
            {
                carData.ApplyTo(car);
                // Garage upgrades on top of the car's own handling (a stock car gets identical values).
                if (SaveManager.Current != null) controller.ApplyHandling(CarUpgrades.Upgraded(SaveManager.Current.Data, carData));
            }
            if (!car.TryGetComponent(out CollisionHandler collisions))
            {
                Debug.LogWarning($"[LevelLoader] '{carPrefab.name}' has no CollisionHandler; adding one with default settings.", this);
                collisions = car.AddComponent<CollisionHandler>();
            }

            if (levelManager.Mission != null) levelManager.Mission.Configure(null, zoneMarkerMaterial);

            if (vehicleCamera != null)
            {
                vehicleCamera.ApplyFraming(level.CameraHeight, level.CameraDistance);
                vehicleCamera.SetTarget(car.transform, snap: true);
            }
            else Debug.LogWarning("[LevelLoader] No VehicleCamera assigned; the camera will not follow the car.", this);

            levelManager.BeginLevel(level, Vehicle, collisions);
            return true;
        }

        public void Unload()
        {
            if (pendingArena != null) { StopCoroutine(pendingArena); pendingArena = null; }
            if (Environment != null) Destroy(Environment);
            if (arenaHandle.IsValid()) { Addressables.Release(arenaHandle); arenaHandle = default; }
            if (Vehicle != null) Destroy(Vehicle.gameObject);
            Environment = null;
            Vehicle = null;
            CurrentLevel = null;
        }
    }
}
