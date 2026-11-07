using UnityEngine;

namespace UltimateCarParking.WorldTour
{
    /// <summary>
    /// Marker on the root of a country arena (prefab root, or any root object of an arena scene). Gives the loader
    /// and future missions named anchors instead of raw coordinates. Optional: an arena without it still loads.
    /// </summary>
    public sealed class CountryArenaRoot : MonoBehaviour
    {
        [SerializeField] private string arenaId = "";
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform[] parkingSlots = System.Array.Empty<Transform>();

        public string ArenaId => arenaId;
        public Transform SpawnPoint => spawnPoint;
        public int SlotCount => parkingSlots.Length;
        public Transform Slot(int i) => i >= 0 && i < parkingSlots.Length ? parkingSlots[i] : null;
    }
}
