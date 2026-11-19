using UnityEngine;
using UnityEngine.EventSystems;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Transparent UI area: one-finger horizontal drags spin the showroom turntable; two-finger pinch (or the mouse
    /// wheel) zooms the showroom camera between a close-up and the full view.
    /// </summary>
    public sealed class ShowroomDragArea : MonoBehaviour, IDragHandler, IScrollHandler
    {
        [SerializeField] private CarShowroom showroom;
        [SerializeField] private float degreesPerPixel = 6f;
        [Tooltip("Camera zoomed by pinch / wheel (field of view).")]
        [SerializeField] private Camera zoomCamera;
        [SerializeField] private Vector2 fovRange = new Vector2(24f, 0f);

        private float baseFov = -1f;
        private float lastPinch = -1f;

        public float Fov => zoomCamera != null ? zoomCamera.fieldOfView : 0f;

        /// <summary>Zoom in (positive) or out (negative), in field-of-view degrees, within the allowed range.</summary>
        public void Zoom(float degreesIn)
        {
            if (zoomCamera == null) return;
            if (baseFov < 0f) baseFov = zoomCamera.fieldOfView;
            float max = fovRange.y > 0f ? fovRange.y : baseFov;
            zoomCamera.fieldOfView = Mathf.Clamp(zoomCamera.fieldOfView - degreesIn, fovRange.x, max);
        }

        public void OnScroll(PointerEventData e) => Zoom(e.scrollDelta.y * 2f);

        private void Update()
        {
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts == null || zoomCamera == null) return;
            Vector2 a = default, b = default; int n = 0;
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                if (n == 0) a = t.position.ReadValue(); else if (n == 1) b = t.position.ReadValue();
                n++;
            }
            if (n < 2) { lastPinch = -1f; return; }
            float spread = Vector2.Distance(a, b);
            if (lastPinch > 0f) Zoom((spread - lastPinch) * 0.05f);
            lastPinch = spread;
        }

        public void OnDrag(PointerEventData e)
        {
            if (lastPinch > 0f) return;   // two fingers down: zooming, not spinning
            if (showroom != null) showroom.AddSpin(-e.delta.x * degreesPerPixel);
        }
    }
}
