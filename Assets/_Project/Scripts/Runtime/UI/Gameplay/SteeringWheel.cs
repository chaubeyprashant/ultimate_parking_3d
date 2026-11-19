using UnityEngine;
using UnityEngine.EventSystems;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// On-screen steering wheel. Grab it anywhere inside its circle and turn it; the finger can then move anywhere on
    /// screen. Tracks only the finger that grabbed it, so pedals and gear buttons work at the same time (multi-touch).
    /// Released, it self-centres. The steering value is the wheel angle over the full-lock angle (scaled by the
    /// Steering Sensitivity setting); the car's own steering speed, Ackermann and speed-dependent lock still apply.
    /// </summary>
    public sealed class SteeringWheel : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        [Tooltip("The graphic that rotates (rim, spokes, hub).")]
        [SerializeField] private RectTransform rotor;
        [Tooltip("Optional highlight shown while held.")]
        [SerializeField] private CanvasGroup heldGlow;

        private int pointerId = int.MinValue;
        private Vector2 lastDir;
        private float angle;
        private float sensitivity = 1f;

        /// <summary>Wheel rotation in degrees, clockwise positive (right).</summary>
        public float Angle => angle;
        public bool IsHeld => pointerId != int.MinValue;
        /// <summary>-1 (full left) .. 1 (full right).</summary>
        public float Output => WheelSteer.Output(angle, sensitivity);

        public float Sensitivity
        {
            get => sensitivity;
            set => sensitivity = Mathf.Clamp(value, 0.1f, 4f);
        }

        private RectTransform Rect => (RectTransform)transform;

        private void OnDisable() { pointerId = int.MinValue; angle = 0f; Render(); }

        public void OnPointerDown(PointerEventData e)
        {
            if (IsHeld) return;
            if (!TryDir(e, out Vector2 dir)) return;
            pointerId = e.pointerId;
            lastDir = dir;
            Render();
        }

        // Turn from the first pixel of movement (no drag threshold dead spot).
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointerId) return;
            if (!TryDir(e, out Vector2 dir)) return;
            Turn(WheelSteer.ClockwiseDelta(lastDir, dir));
            lastDir = dir;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointerId) Release();
        }

        /// <summary>Turns the wheel by a number of degrees (clockwise positive). Used by touch input and tests.</summary>
        public void Turn(float degrees)
        {
            float limit = WheelSteer.Limit(sensitivity);
            angle = Mathf.Clamp(angle + degrees, -limit, limit);
            Render();
        }

        /// <summary>Programmatic grab (tests): holds the wheel so it does not self-centre.</summary>
        public void SetHeld(bool held)
        {
            if (held && !IsHeld) { pointerId = -998; Render(); }
            else if (!held) Release();
        }

        public void Release()
        {
            pointerId = int.MinValue;
            Render();
        }

        /// <summary>Snaps to centre (level start, pause).</summary>
        public void ResetWheel()
        {
            pointerId = int.MinValue;
            angle = 0f;
            Render();
        }

        private void Update()
        {
            if (IsHeld || angle == 0f) return;
            angle = Mathf.MoveTowards(angle, 0f, WheelSteer.ReturnDegPerSec * Time.unscaledDeltaTime);
            Render();
        }

        private bool TryDir(PointerEventData e, out Vector2 dir)
        {
            dir = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, e.position, e.pressEventCamera, out Vector2 local)) return false;
            local -= Rect.rect.center;
            if (local.sqrMagnitude < 16f) return false;   // too close to the hub to read a direction
            dir = local.normalized;
            return true;
        }

        private void Render()
        {
            if (rotor != null) rotor.localEulerAngles = new Vector3(0f, 0f, -angle);
            if (heldGlow != null) heldGlow.alpha = IsHeld ? 1f : 0f;
        }
    }
}
