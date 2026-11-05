using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UltimateCarParking.Audio;

namespace UltimateCarParking.UI
{
    /// <summary>Tactile press feedback: the button eases down while held and springs back on release.</summary>
    [DisallowMultipleComponent]
    public sealed class UIButtonFX : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.95f;
        [SerializeField, Min(1f)] private float speed = 18f;
        [SerializeField] private bool clickSound = true;

        private Selectable selectable;
        private float target = 1f;

        private void Awake() => selectable = GetComponent<Selectable>();

        public void OnPointerDown(PointerEventData e)
        {
            if (selectable == null || selectable.IsInteractable()) target = pressedScale;
        }

        public void OnPointerUp(PointerEventData e) => target = 1f;
        public void OnPointerExit(PointerEventData e) => target = 1f;

        public void OnPointerClick(PointerEventData e)
        {
            if (clickSound && (selectable == null || selectable.IsInteractable())) AudioManager.Click();
        }

        private void OnDisable()
        {
            target = 1f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float current = transform.localScale.x;
            if (Mathf.Abs(current - target) < 0.0005f) return;   // idle: nothing to do
            float s = Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
