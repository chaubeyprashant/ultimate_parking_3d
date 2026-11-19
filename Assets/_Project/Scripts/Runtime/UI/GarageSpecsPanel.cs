using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Cars;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Garage spec sheet: measured 0–20 km/h, dry and wet stopping from 40 km/h, turning radius, size, top speed,
    /// a parking difficulty rating and the car's strength. COMPARE shows each figure against the car currently in use
    /// (green = better, red = worse), so players can judge a car before buying it.
    /// </summary>
    public sealed class GarageSpecsPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text values;
        [SerializeField] private TMP_Text difficulty;
        [SerializeField] private TMP_Text strength;
        [SerializeField] private Button compareButton;
        [SerializeField] private TMP_Text compareLabel;

        private CarData shown, reference;
        private readonly StringBuilder sb = new StringBuilder(256);

        public bool Comparing { get; private set; }
        public string ValuesText => values != null ? values.text : "";
        public string DifficultyText => difficulty != null ? difficulty.text : "";

        private void Awake()
        {
            if (compareButton != null) compareButton.onClick.AddListener(() => { Comparing = !Comparing; Render(); });
        }

        /// <summary>Shows a car; <paramref name="inUse"/> is the selected car used for COMPARE.</summary>
        public void Show(CarData car, CarData inUse)
        {
            shown = car;
            reference = inUse;
            Render();
        }

        private void Render()
        {
            if (shown == null) return;
            CarSpecs s = shown.Specs;
            CarSpecs r = reference != null && reference != shown ? reference.Specs : null;
            bool compare = Comparing && r != null && r.Measured && s.Measured;
            if (compareButton != null) compareButton.interactable = reference != null && reference != shown;
            if (compareLabel != null)
                compareLabel.text = reference == null || reference == shown ? "IN USE" : Comparing ? "VS " + reference.DisplayName.ToUpperInvariant() : "COMPARE";

            sb.Clear();
            float top = shown.Handling != null ? shown.Handling.maxForwardSpeedKmh : 0f;
            float refTop = reference != null && reference.Handling != null ? reference.Handling.maxForwardSpeedKmh : 0f;
            if (s.Measured)
            {
                Row("0–20 KM/H", s.zeroTo20, "0.00", " s", compare ? r.zeroTo20 : float.NaN, lowerIsBetter: true);
                Row("STOP FROM 40", s.stopFrom40, "0.0", " m", compare ? r.stopFrom40 : float.NaN, lowerIsBetter: true);
                Row("WET STOP", s.wetStopFrom40, "0.0", " m", compare ? r.wetStopFrom40 : float.NaN, lowerIsBetter: true);
                Row("TURN RADIUS", s.turnRadius, "0.0", " m", compare ? r.turnRadius : float.NaN, lowerIsBetter: true);
            }
            Row("TOP SPEED", top, "0", " km/h", compare ? refTop : float.NaN, lowerIsBetter: false);
            if (s.length > 0f) sb.Append("SIZE<pos=55%><b>").Append(s.length.ToString("0.0")).Append(" × ").Append(s.width.ToString("0.0")).Append(" m</b>\n");
            if (values != null) values.text = sb.ToString();

            if (difficulty != null)
            {
                int d = Mathf.Clamp(s.parkingDifficulty, 1, 5);
                Color c = d <= 2 ? UiPalette.Lime : d == 3 ? UiPalette.Gold : UiPalette.Danger;
                difficulty.text = $"PARKING  <color=#{ColorUtility.ToHtmlStringRGB(c)}>{CarSpecs.DifficultyName(d)}  {d}/5</color>";
            }
            if (strength != null) strength.text = s.strength;
        }

        private void Row(string label, float v, string fmt, string unit, float refValue, bool lowerIsBetter)
        {
            sb.Append(label).Append("<pos=55%><b>").Append(v.ToString(fmt)).Append(unit).Append("</b>");
            if (!float.IsNaN(refValue) && refValue > 0f)
            {
                float delta = v - refValue;
                if (Mathf.Abs(delta) >= (fmt == "0" ? 1f : fmt == "0.00" ? 0.01f : 0.1f))
                {
                    bool better = lowerIsBetter ? delta < 0f : delta > 0f;
                    sb.Append("  <size=80%><color=#").Append(ColorUtility.ToHtmlStringRGB(better ? UiPalette.Lime : UiPalette.Danger)).Append('>')
                      .Append(delta > 0f ? "+" : "").Append(delta.ToString(fmt)).Append("</color></size>");
                }
            }
            sb.Append('\n');
        }
    }
}
