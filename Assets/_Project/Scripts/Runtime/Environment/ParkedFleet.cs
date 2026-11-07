using UnityEngine;

namespace UltimateCarParking.Environment
{
    /// <summary>Real parked-car prefabs used in place of the block parked-car module.</summary>
    public sealed class ParkedFleet : ScriptableObject
    {
        [SerializeField] private GameObject[] cars = System.Array.Empty<GameObject>();

        public int Count => cars.Length;

        public GameObject Pick(int index)
        {
            if (cars == null || cars.Length == 0) return null;
            int i = ((index % cars.Length) + cars.Length) % cars.Length;
            return cars[i];
        }
    }
}
