using UnityEngine;

namespace UltimateCarParking.Levels
{
    /// <summary>
    /// Carries the chosen level across a scene load (LevelSelect → Gameplay, restart, next level).
    /// The LevelLoader consumes it once; when nothing was requested it uses its default level.
    /// </summary>
    public static class LevelSelection
    {
        private static LevelData requested;
        private static LevelCatalog requestedCatalog;

        public static bool HasRequest => requested != null;

        /// <summary>
        /// Scene the gameplay "levels / menu" exit returns to: the screen the level was started from
        /// (level select by default, World Tour when started there). Kept across restarts and next-level loads.
        /// </summary>
        public static string ReturnScene { get; set; } = Core.SceneNames.LevelSelect;

        /// <summary>World Tour country the level was started from (country rewards); null from the campaign.
        /// Sticky across restarts and next-level loads; a stale value is harmless (the level is not in it).</summary>
        public static WorldTour.CountryData Country { get; set; }

        /// <summary>The catalog the requested level belongs to (for Next Level); null = the loader's own.</summary>
        public static LevelCatalog RequestedCatalog => requestedCatalog;

        public static void Request(LevelData level, LevelCatalog catalog = null)
        {
            requested = level;
            requestedCatalog = catalog;
        }

        /// <summary>Returns the requested level (or null) and clears the request.</summary>
        public static LevelData Consume()
        {
            LevelData l = requested;
            requested = null;
            return l;
        }

        // Static state survives Play-mode entry when domain reload is disabled; clear it explicitly.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            requested = null;
            requestedCatalog = null;
            ReturnScene = Core.SceneNames.LevelSelect;
            Country = null;
        }

        /// <summary>Returns the requested catalog (or null) and clears it.</summary>
        public static LevelCatalog ConsumeCatalog()
        {
            LevelCatalog c = requestedCatalog;
            requestedCatalog = null;
            return c;
        }
    }
}
