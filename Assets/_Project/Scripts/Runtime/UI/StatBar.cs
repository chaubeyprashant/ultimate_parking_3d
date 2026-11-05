using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UltimateCarParking.UI
{
    /// <summary>Labelled 0..1 bar that eases to new values.</summary>
    public sealed class StatBar : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField, Min(1f)] private float sharpness = 9f;

        private float shown;
        private float target;

        public float Target => target;

        public void SetValue(float normalized, bool instant = false)
        {
            target = Mathf.Clamp01(normalized);
            if (instant) shown = target;
            if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(target * 100f).ToString();
            Render();
        }

        private void Update()
        {
            if (Mathf.Abs(shown - target) < 0.001f) return;
            shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime));
            Render();
        }

        private void Render()
        {
            if (fill != null) fill.anchorMax = new Vector2(Mathf.Max(0.02f, shown), fill.anchorMax.y);
        }
    }
}
