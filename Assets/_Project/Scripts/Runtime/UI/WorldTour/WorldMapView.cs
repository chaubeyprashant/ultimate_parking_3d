using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Pan / zoom for the World Tour map. One finger (or mouse) drags, two fingers pinch-zoom about their midpoint,
    /// the mouse wheel zooms in the Editor. The map is clamped so it never leaves the viewport, and
    /// <see cref="Focus"/> glides to a normalised map rectangle (region chips, selected country).
    /// Markers are children of the content and are counter-scaled so they keep their on-screen size.
    /// </summary>
    public sealed class WorldMapView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform markerLayer;
        [SerializeField, Min(1f)] private float maxZoomOverFit = 7f;
        [SerializeField, Min(0.1f)] private float focusSeconds = 0.5f;
        [Tooltip("Pin size at the whole-world view relative to their size when zoomed in (declutters the world view).")]
        [SerializeField, Range(0.3f, 1f)] private float worldMarkerScale = 0.62f;

        private readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        private Coroutine focusRoutine;
        private float zoom = 1f;

        public float Zoom => zoom;
        /// <summary>Zoom at which the whole map fits the viewport.</summary>
        public float FitZoom
        {
            get
            {
                Vector2 v = viewport.rect.size, c = content.rect.size;
                return Mathf.Min(v.x / c.x, v.y / c.y);
            }
        }
        public float RelativeZoom => zoom / Mathf.Max(0.0001f, FitZoom);
        public bool Interacted { get; private set; }

        /// <summary>
        /// On-screen pin scale: smaller at the whole-world view and on narrow map areas (4:3 tablets), full size when
        /// zoomed into a region on a phone — so neighbouring pins (Netherlands / Germany) don't pile up.
        /// </summary>
        public float MarkerScreenScale =>
            Mathf.Lerp(worldMarkerScale, 1f, Mathf.InverseLerp(1f, 4.5f, RelativeZoom)) * Mathf.Clamp(viewport.rect.width / 1300f, 0.7f, 1f);
        public event System.Action Changed;

        private bool initialised;

        private void Start()
        {
            if (!initialised) SetView(FitZoom, Vector2.zero);
        }

        /// <summary>Viewport-local position of a normalised map point (0..1).</summary>
        public Vector2 MapToViewport(Vector2 n)
        {
            Vector2 c = content.rect.size;
            return content.anchoredPosition + (n - new Vector2(0.5f, 0.5f)) * c * zoom;
        }

        public void SetView(float newZoom, Vector2 pan)
        {
            initialised = true;
            zoom = Mathf.Clamp(newZoom, FitZoom, FitZoom * maxZoomOverFit);
            content.localScale = new Vector3(zoom, zoom, 1f);
            content.anchoredPosition = Clamp(pan);
            if (markerLayer != null)
            {
                float k = MarkerScreenScale / zoom;
                foreach (Transform m in markerLayer) m.localScale = new Vector3(k, k, 1f);
            }
            Changed?.Invoke();
        }

        /// <summary>Glide so the normalised rect fills the viewport (with a margin).</summary>
        public void Focus(Rect normalized, bool instant = false, float margin = 0.12f)
        {
            Vector2 c = content.rect.size, v = viewport.rect.size;
            Vector2 size = Vector2.Scale(normalized.size, c) * (1f + margin * 2f);
            float target = Mathf.Min(v.x / Mathf.Max(1f, size.x), v.y / Mathf.Max(1f, size.y));
            target = Mathf.Clamp(target, FitZoom, FitZoom * maxZoomOverFit);
            Vector2 pan = -(normalized.center - new Vector2(0.5f, 0.5f)) * c * target;
            if (focusRoutine != null) StopCoroutine(focusRoutine);
            if (instant || !isActiveAndEnabled) { SetView(target, pan); return; }
            focusRoutine = StartCoroutine(Glide(target, pan));
        }

        /// <summary>Pan (keeping the zoom) so a normalised point sits at the viewport centre.</summary>
        public void CentreOn(Vector2 n, float minRelativeZoom = 1f)
        {
            float z = Mathf.Max(zoom, FitZoom * minRelativeZoom);
            Vector2 pan = -(n - new Vector2(0.5f, 0.5f)) * content.rect.size * z;
            if (focusRoutine != null) StopCoroutine(focusRoutine);
            focusRoutine = StartCoroutine(Glide(z, pan));
        }

        private IEnumerator Glide(float targetZoom, Vector2 targetPan)
        {
            float z0 = zoom;
            Vector2 p0 = content.anchoredPosition;
            Vector2 clampedTarget = ClampFor(targetPan, Mathf.Clamp(targetZoom, FitZoom, FitZoom * maxZoomOverFit));
            for (float t = 0f; t < focusSeconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / focusSeconds);
                SetView(Mathf.Lerp(z0, targetZoom, k), Vector2.Lerp(p0, clampedTarget, k));
                yield return null;
            }
            SetView(targetZoom, clampedTarget);
            focusRoutine = null;
        }

        private Vector2 Clamp(Vector2 pan) => ClampFor(pan, zoom);

        private Vector2 ClampFor(Vector2 pan, float z)
        {
            Vector2 half = (content.rect.size * z - viewport.rect.size) * 0.5f;
            pan.x = half.x > 0f ? Mathf.Clamp(pan.x, -half.x, half.x) : 0f;
            pan.y = half.y > 0f ? Mathf.Clamp(pan.y, -half.y, half.y) : 0f;
            return pan;
        }

        // ---------------------------------------------------------------- input

        private Vector2 Local(Vector2 screen, Camera cam)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screen, cam, out Vector2 p);
            return p;
        }

        public void OnPointerDown(PointerEventData e)
        {
            pointers[e.pointerId] = Local(e.position, e.pressEventCamera);
            if (focusRoutine != null) { StopCoroutine(focusRoutine); focusRoutine = null; }
        }

        public void OnPointerUp(PointerEventData e) => pointers.Remove(e.pointerId);
        public void OnBeginDrag(PointerEventData e) => Interacted = true;
        public void OnEndDrag(PointerEventData e) => pointers.Remove(e.pointerId);

        public void OnDrag(PointerEventData e)
        {
            Vector2 now = Local(e.position, e.pressEventCamera);
            if (!pointers.TryGetValue(e.pointerId, out Vector2 before)) { pointers[e.pointerId] = now; return; }

            if (pointers.Count >= 2)
            {
                // Pinch: scale by the change in finger spread, about the midpoint, and follow the midpoint.
                Vector2 other = Vector2.zero;
                foreach (var kv in pointers) if (kv.Key != e.pointerId) { other = kv.Value; break; }
                float d0 = Vector2.Distance(before, other), d1 = Vector2.Distance(now, other);
                Vector2 m0 = (before + other) * 0.5f, m1 = (now + other) * 0.5f;
                if (d0 > 1f) ZoomAbout(m0, zoom * d1 / d0, m1 - m0);
            }
            else SetView(zoom, content.anchoredPosition + (now - before));
            pointers[e.pointerId] = now;
        }

        public void OnScroll(PointerEventData e)
        {
            Interacted = true;
            ZoomAbout(Local(e.position, e.enterEventCamera), zoom * Mathf.Pow(1.12f, e.scrollDelta.y), Vector2.zero);
        }

        /// <summary>Zoom keeping the map point under <paramref name="pivot"/> (viewport-local) in place.</summary>
        public void ZoomAbout(Vector2 pivot, float newZoom, Vector2 extraPan)
        {
            float z = Mathf.Clamp(newZoom, FitZoom, FitZoom * maxZoomOverFit);
            Vector2 pan = content.anchoredPosition;
            Vector2 local = (pivot - pan) / zoom;           // content-space point under the pivot
            SetView(z, pivot - local * z + extraPan);
        }
    }
}
