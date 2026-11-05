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
