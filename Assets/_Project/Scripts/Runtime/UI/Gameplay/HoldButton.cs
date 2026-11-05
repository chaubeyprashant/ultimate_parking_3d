using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// A touch control that is "held" while a pointer is down on it (multi-touch: each control tracks its own finger).
    /// Dims/brightens its graphic and raises Pressed/Released. Event-driven: no Update.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
    {
        [SerializeField] private Graphic target;
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.16f);
        [SerializeField] private Color heldColor = new Color(0.71f, 1f, 0.18f, 0.55f);

        private int pointerId = int.MinValue;

        public event Action Pressed;
        public event Action Released;
        public bool IsHeld => pointerId != int.MinValue;

        private void Awake()
        {
            if (target == null) target = GetComponent<Graphic>();
            Render();
        }

        private void OnDisable() => Release();

        public void OnPointerDown(PointerEventData e)
        {
            if (IsHeld) return;
            pointerId = e.pointerId;
            Render();
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointerId) Release();
        }

        // A finger sliding off the pedal releases it; sliding back on (still down) re-engages it.
        public void OnPointerExit(PointerEventData e)
        {
            if (e.pointerId == pointerId) Release();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!IsHeld && e.eligibleForClick && e.pointerPress == null && e.dragging) OnPointerDown(e);
        }

        /// <summary>Programmatic press/release (tests, accessibility).</summary>
        public void SetHeld(bool held)
        {
            if (held && !IsHeld) { pointerId = -999; Render(); Pressed?.Invoke(); }
            else if (!held) Release();
        }

        private void Release()
        {
            if (!IsHeld) return;
            pointerId = int.MinValue;
            Render();
            Released?.Invoke();
        }

        private void Render()
        {
            if (target != null) target.color = IsHeld ? heldColor : idleColor;
            transform.localScale = IsHeld ? new Vector3(0.95f, 0.95f, 1f) : Vector3.one;
        }
    }
}
