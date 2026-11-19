using UnityEngine;

namespace UltimateCarParking.Vehicle
{
    /// <summary>
    /// Driver's eye position for the cockpit camera, in car-local space. Added when the car prefab is built from a model
    /// that has a modelled interior (taken from the steering wheel), so only those cars offer the cockpit view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CockpitMount : MonoBehaviour
    {
        public Vector3 eyeLocal = new Vector3(-0.35f, 1.1f, 0f);
    }
}
