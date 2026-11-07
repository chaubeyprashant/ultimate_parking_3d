using UnityEngine;
using UltimateCarParking.Levels;

namespace UltimateCarParking.WorldTour
{
    public enum WorldRegion { Asia, Europe, Americas, AfricaMiddleEast, Oceania }

    /// <summary>
    /// One World Tour country: identity, flag, map position, preview, arena (by name — see
    /// <see cref="CountryArenaReference"/>), missions, unlock requirements, traffic, road direction and weather.
    /// Progress is never stored here: it is derived from the campaign save (<see cref="CountryProgressManager"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "Country_", menuName = "Ultimate Car Parking/World Tour Country", order = 30)]
    public sealed class CountryData : ScriptableObject
    {
        [Tooltip("ISO 3166-1 alpha-2, lower case (e.g. \"in\"). Stable: used for map shapes and tests.")]
        [SerializeField] private string countryId = "xx";
        [SerializeField] private string displayName = "Country";
        [SerializeField] private WorldRegion region;
        [SerializeField] private Sprite flag;

        [Header("Map")]
        [Tooltip("Marker location in degrees (latitude, longitude). Must fall inside the country on the map.")]
        [SerializeField] private Vector2 latLon;

        [Header("Arena")]
        [SerializeField] private string arenaName = "Arena";
        [Tooltip("Environment the arena currently uses (shown under the arena name).")]
        [SerializeField] private string environmentLabel = "City car park";
        [SerializeField, TextArea(2, 5)] private string description = "";
        [Tooltip("Short mission-type tag shown on the preview (e.g. \"REVERSE PARKING\").")]
        [SerializeField] private string missionLabel = "PARKING";
        [SerializeField] private Sprite preview;

        [Header("Missions")]
        [Tooltip("Levels played in this country, in order. Each must also be in the campaign catalog.")]
        [SerializeField] private LevelData[] levels = System.Array.Empty<LevelData>();
        [Tooltip("Optional catalog of this country's missions in order, so Next Level stays inside the country. " +
                 "Empty = the levels come from the campaign catalog.")]
        [SerializeField] private LevelCatalog missionCatalog;

        [Header("Environment")]
        [Tooltip("The country's arena, referenced by name so it is only loaded when this country is selected.")]
        [SerializeField] private CountryArenaReference arena = new CountryArenaReference();

        [Header("Rewards")]
        [Tooltip("Coins paid once when every mission in the country has been completed.")]
        [SerializeField, Min(0)] private int completionCoins = 500;
        [Tooltip("Coins paid once when every mission has three stars.")]
        [SerializeField, Min(0)] private int perfectCoins = 750;

        [Header("Unlock")]
        [SerializeField] private CountryUnlockRequirement unlock = new CountryUnlockRequirement();

        [Header("Driving conditions")]
        [SerializeField] private RoadDirection roadDirection = RoadDirection.RightHand;
        [SerializeField] private TrafficSettings traffic = new TrafficSettings();
        [SerializeField] private WeatherSettings weather = new WeatherSettings();

        public string CountryId => countryId;
        public string DisplayName => displayName;
        public WorldRegion Region => region;
        public Sprite Flag => flag;
        public Vector2 LatLon => latLon;
        public string ArenaName => arenaName;
        public string EnvironmentLabel => environmentLabel;
        public string Description => description;
        public string MissionLabel => missionLabel;
        public Sprite Preview => preview;
        public int MissionCount => levels.Length;
        public LevelCatalog MissionCatalog => missionCatalog;
        public int IndexOfMission(LevelData level) => System.Array.IndexOf(levels, level);
        public CountryArenaReference Arena => arena;
        public CountryUnlockRequirement Unlock => unlock;
        public int CompletionCoins => completionCoins;
        public int PerfectCoins => perfectCoins;
        public RoadDirection RoadDirection => roadDirection;
        public bool DrivesOnLeft => roadDirection == RoadDirection.LeftHand;
        public TrafficSettings Traffic => traffic;
        public WeatherSettings Weather => weather;
        public LevelData GetLevel(int index) => index >= 0 && index < levels.Length ? levels[index] : null;
    }
}
