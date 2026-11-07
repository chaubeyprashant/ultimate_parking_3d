using UnityEngine;

namespace UltimateCarParking.Environment
{
    /// <summary>
    /// Marks a collider as kerb / pavement. Parallel-parking objectives count a strike when a wheel of the player's
    /// car touches it. Data only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KerbMarker : MonoBehaviour { }
}
