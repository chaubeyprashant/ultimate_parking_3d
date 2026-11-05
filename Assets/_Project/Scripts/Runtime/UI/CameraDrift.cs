using UnityEngine;

namespace UltimateCarParking.UI
{
    /// <summary>Slow, subtle orbit and dolly of a showroom camera around its look target (menu and garage).</summary>
    public sealed class CameraDrift : MonoBehaviour
    {
        [SerializeField] private Vector3 lookAt = new Vector3(0f, 0.7f, 0f);
        [SerializeField, Range(0f, 20f)] private float orbitDegrees = 6f;
        [SerializeField, Range(0f, 1f)] private float dolly = 0.25f;
        [SerializeField, Range(0f, 0.5f)] private float rise = 0.12f;
        [SerializeField, Min(1f)] private float period = 14f;

        private Vector3 baseOffset;

        private void Awake() => baseOffset = transform.position - lookAt;

        private void LateUpdate()
        {
            float t = Time.unscaledTime * Mathf.PI * 2f / period;
            Vector3 offset = Quaternion.Euler(0f, Mathf.Sin(t) * orbitDegrees, 0f) * baseOffset;
            offset *= 1f + Mathf.Sin(t * 0.5f) * dolly * 0.1f;
            offset.y += Mathf.Sin(t * 0.7f) * rise;
            transform.position = lookAt + offset;
            transform.rotation = Quaternion.LookRotation(lookAt - transform.position);
        }

        public void SetLookAt(Vector3 point)
        {
            lookAt = point;
            baseOffset = transform.position - lookAt;
        }
    }
}
