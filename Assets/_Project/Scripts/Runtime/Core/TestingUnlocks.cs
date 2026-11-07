using UnityEngine;

namespace UltimateCarParking.Core
{
    /// <summary>
    /// ⚠️ TESTING ONLY — every country, mission and campaign level is playable while <see cref="UnlockAll"/> is on.
    ///
    /// PRODUCTION: set <see cref="DefaultUnlockAll"/> to <c>false</c> before uploading a release build.
    /// (Documentation/RELEASE_CHECKLIST.md.)
    ///
    /// Only the checks are bypassed: saves keep recording real progress and real unlocks, so switching this off
    /// restores normal progression without touching anyone's save. Automated tests switch it off themselves.
    /// </summary>
    public static class TestingUnlocks
    {
        /// <summary>Build-time default. TODO(production): false.</summary>
        public const bool DefaultUnlockAll = true;

        public static bool UnlockAll { get; set; } = DefaultUnlockAll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => UnlockAll = DefaultUnlockAll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Announce()
        {
            if (UnlockAll) Debug.LogWarning("[Testing] All countries, missions and levels are unlocked (TestingUnlocks.DefaultUnlockAll). Turn off before release.");
        }
    }
}
