using UnityEngine;
using UltimateCarParking.Environment;
using UltimateCarParking.Parking;

namespace UltimateCarParking.Levels
{
    /// <summary>
    /// One level's configuration. Data only: no gameplay logic reads the scene from here.
    /// Create via Assets ▸ Create ▸ Ultimate Car Parking ▸ Level Data, then add it to the LevelCatalog.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_", menuName = "Ultimate Car Parking/Level Data", order = 0)]
    public sealed class LevelData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique, stable id (used for selection and, later, save data). Do not change after release.")]
        [SerializeField] private string levelId = "L01";
        [SerializeField] private string displayName = "Level 1";
        [Tooltip("Number shown on the level card.")]
        [SerializeField, Min(0)] private int levelNumber = 1;

        [Header("Environment")]
        [Tooltip("Modular world: theme shell + the props below. Leave empty to use Environment Prefab instead.")]
        [SerializeField] private EnvironmentTheme theme;
        [Tooltip("This level's modules (bay lines, parked cars, cones, barriers…), placed on the theme shell.")]
        [SerializeField] private PropPlacement[] props = System.Array.Empty<PropPlacement>();
        [Tooltip("Optional prefab instantiated as (or in addition to) the level's world.")]
        [SerializeField] private GameObject environmentPrefab;

        [Header("Spawn point")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(-20f, 0f, 0f);
        [Tooltip("Car heading in degrees around Y (0 = +Z, 90 = +X).")]
        [SerializeField] private float spawnYaw = 90f;

        [Header("Parking target")]
        [Tooltip("Centre of the target bay on the ground.")]
        [SerializeField] private Vector3 targetPosition = new Vector3(4.2f, 0f, 9.25f);
        [Tooltip("Bay heading in degrees around Y; the bay's long axis points this way.")]
        [SerializeField] private float targetYaw;
        [Tooltip("Bay width and length in metres.")]
        [SerializeField] private Vector2 targetSize = new Vector2(2.8f, 5.5f);

        [Header("Limits")]
        [Tooltip("Seconds to park. 0 = no time limit.")]
        [SerializeField, Min(0f)] private float timeLimitSeconds = 90f;
        [Tooltip("Collisions allowed. The level fails when the count goes ABOVE this. -1 = unlimited.")]
        [SerializeField, Min(-1)] private int collisionLimit = 3;

        [Header("Parking rules")]
        [Tooltip("Use the rules below instead of the ParkingValidator's Inspector defaults.")]
        [SerializeField] private bool overrideParkingRules;
        [SerializeField] private ParkingRules parkingRules = new ParkingRules();

        [Header("Stars (completion always earns at least 1)")]
        [SerializeField, Min(0f)] private float threeStarTime = 45f;
        [SerializeField, Min(0)] private int threeStarMaxCollisions;
        [SerializeField, Min(0f)] private float twoStarTime = 70f;
        [SerializeField, Min(0)] private int twoStarMaxCollisions = 1;

        [Header("Rewards")]
        [SerializeField, Min(0)] private int baseCoins = 100;
        [SerializeField, Min(0)] private int coinsPerStar = 50;

        public string LevelId => levelId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? levelId : displayName;
        public int LevelNumber => levelNumber;
        public EnvironmentTheme Theme => theme;
        public PropPlacement[] Props => props;
        public GameObject EnvironmentPrefab => environmentPrefab;
        public bool HasEnvironment => theme != null || environmentPrefab != null;
        public Vector3 SpawnPosition => spawnPosition;
        public Quaternion SpawnRotation => Quaternion.Euler(0f, spawnYaw, 0f);
        public Vector3 TargetPosition => targetPosition;
        public Quaternion TargetRotation => Quaternion.Euler(0f, targetYaw, 0f);
        public Vector2 TargetSize => targetSize;
        public float TimeLimitSeconds => timeLimitSeconds;
        public bool HasTimeLimit => timeLimitSeconds > 0f;
        public int CollisionLimit => collisionLimit;
        public bool HasCollisionLimit => collisionLimit >= 0;
        public ParkingRules ParkingRulesOverride => overrideParkingRules ? parkingRules : null;
        public StarThresholds Stars => new StarThresholds(threeStarTime, threeStarMaxCollisions, twoStarTime, twoStarMaxCollisions);
        public int BaseCoins => baseCoins;
        public int CoinsPerStar => coinsPerStar;

        public int CoinsFor(int stars) => stars <= 0 ? 0 : baseCoins + coinsPerStar * Mathf.Clamp(stars, 1, 3);

        private void OnValidate()
        {
            targetSize = new Vector2(Mathf.Max(1f, targetSize.x), Mathf.Max(1f, targetSize.y));
            twoStarTime = Mathf.Max(twoStarTime, threeStarTime);
            twoStarMaxCollisions = Mathf.Max(twoStarMaxCollisions, threeStarMaxCollisions);
            if (string.IsNullOrWhiteSpace(levelId)) levelId = name;
        }
    }
}
