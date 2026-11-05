using UnityEngine;
using UnityEngine.EventSystems;

namespace UltimateCarParking.UI
{
    /// <summary>Transparent UI area: horizontal drags spin the showroom turntable.</summary>
    public sealed class ShowroomDragArea : MonoBehaviour, IDragHandler
    {
        [SerializeField] private CarShowroom showroom;
        [SerializeField] private float degreesPerPixel = 6f;

        public void OnDrag(PointerEventData e)
        {
            if (showroom != null) showroom.AddSpin(-e.delta.x * degreesPerPixel);
        }
    }
}
