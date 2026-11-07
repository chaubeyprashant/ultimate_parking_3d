using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.CameraSystem;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Backup-camera guide lines: the two wheel tracks of the predicted reverse path (they bend with the steering),
    /// red to 1 m behind the bumper, yellow to 2 m, green to 3.5 m, with cross marks at 1, 2 and 3 m. Points are
    /// projected through the rear camera itself, so the lines sit on the ground in the image.
    /// </summary>
    public sealed class RearGuideLines : MaskableGraphic
    {
        [SerializeField, Min(1f)] private float thickness = 6f;
        private RearViewCamera source;

        public void Refresh(RearViewCamera cam)
        {
            source = cam;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (source == null || source.Car == null || source.Camera == null) return;
            Rect r = rectTransform.rect;
            Transform car = source.Car.transform;
            Camera cam = source.Camera;
            bool ToUi(Vector3 local, out Vector2 ui)
            {
                Vector3 v = cam.WorldToViewportPoint(car.TransformPoint(local));
                ui = default;
                if (v.z <= 0.05f) return false;
                float x = source.Mirrored ? 1f - v.x : v.x;
                ui = new Vector2(r.xMin + x * r.width, r.yMin + v.y * r.height);
                return true;
            }
            Color Band(float m) => m < 1f ? new Color(0.95f, 0.2f, 0.15f) : m < 2f ? new Color(1f, 0.8f, 0.1f) : new Color(0.55f, 0.95f, 0.25f);
            const float step = 0.25f, length = 3.5f;
            foreach (float side in new[] { -1f, 1f })
                for (float m = 0f; m < length - 0.01f; m += step)
                    if (ToUi(source.PathPoint(m, side), out Vector2 a) && ToUi(source.PathPoint(m + step, side), out Vector2 b))
                        Segment(vh, a, b, thickness, Band(m));
            foreach (float m in new[] { 1f, 2f, 3f })
                if (ToUi(source.PathPoint(m, -1f), out Vector2 a) && ToUi(source.PathPoint(m, 1f), out Vector2 b))
                {
                    Vector2 d = (b - a) * 0.22f;   // short ticks inward from each track
                    Segment(vh, a, a + d, thickness * 0.8f, Band(m - 0.01f));
                    Segment(vh, b, b - d, thickness * 0.8f, Band(m - 0.01f));
                }
        }

        private static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 dir = b - a;
            if (dir.sqrMagnitude < 0.01f) return;
            Vector2 n = new Vector2(-dir.y, dir.x).normalized * (width * 0.5f);
            int i = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = c;
            v.position = a - n; vh.AddVert(v);
            v.position = a + n; vh.AddVert(v);
            v.position = b + n; vh.AddVert(v);
            v.position = b - n; vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
