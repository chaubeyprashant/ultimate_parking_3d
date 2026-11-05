using UnityEngine;

namespace UltimateCarParking.UI
{
    /// <summary>Keeps this RectTransform inside the device safe area (notches, rounded corners, nav bars).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect applied;
        private Vector2Int screen;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != applied || screen.x != Screen.width || screen.y != Screen.height) Apply();
        }

        private void Apply()
        {
            Rect safe = Screen.safeArea;
            applied = safe;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
