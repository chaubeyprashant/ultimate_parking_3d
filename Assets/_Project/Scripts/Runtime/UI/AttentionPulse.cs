using UnityEngine;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// A gentle "breathing" scale on a call-to-action (the gold watch-ad buttons) so the eye goes to it. Unscaled time,
    /// so it also runs on result screens with the game paused. Stops while the button cannot be tapped.
    /// </summary>
    public sealed class AttentionPulse : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.2f)] private float amount = 0.06f;
        [SerializeField, Min(0.1f)] private float period = 1.1f;

        private UnityEngine.UI.Selectable selectable;
        private Vector3 baseScale = Vector3.one;

        private void Awake() { selectable = GetComponent<UnityEngine.UI.Selectable>(); baseScale = transform.localScale; }
        private void OnDisable() => transform.localScale = baseScale;

        private void Update()
        {
            bool live = selectable == null || selectable.interactable;
            float k = live ? 1f + amount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period)) : 1f;
            transform.localScale = baseScale * k;
        }
    }
}
