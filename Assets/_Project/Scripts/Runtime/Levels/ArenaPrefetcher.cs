using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UltimateCarParking.Levels
{
    /// <summary>
    /// Starts downloading a country's asset pack while the player is still choosing (on the World Tour map), so the on-demand pack
    /// is already on the device when Play is pressed. Download only: nothing is loaded into memory. Each address is requested once;
    /// a failed download is forgotten so the real load can retry it and show its normal retry screen.
    /// </summary>
    public static class ArenaPrefetcher
    {
        private static readonly HashSet<string> Requested = new HashSet<string>();
        public static int Started { get; private set; }

        public static void Prefetch(LevelData level)
        {
            if (level == null || !level.HasAddressableEnvironment) return;
            string address = level.EnvironmentAddress;
            if (!Requested.Add(address)) return;
            Started++;
            AsyncOperationHandle h = Addressables.DownloadDependenciesAsync(address, autoReleaseHandle: true);
            h.Completed += op =>
            {
                if (op.Status != AsyncOperationStatus.Succeeded) Requested.Remove(address);
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Requested.Clear(); Started = 0; }
    }
}
