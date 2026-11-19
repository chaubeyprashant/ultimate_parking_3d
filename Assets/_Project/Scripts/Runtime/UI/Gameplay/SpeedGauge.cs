using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Levels;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Speedometer: a 270° arc that fills with speed (scaled to the current car's top speed), a digital km/h readout
    /// and the gear letter. Text is only rewritten when the shown whole number or gear changes.
    /// </summary>
    public sealed class SpeedGauge : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private MobileControls controls;
        [Tooltip("Radial-filled image for the speed arc (fill origin bottom, clockwise).")]
        [SerializeField] private Image arc;
        [SerializeField] private TMP_Text speedLabel;
        [SerializeField] private TMP_Text gearLabel;
        [Tooltip("Share of a full circle the arc covers at top speed.")]
        [SerializeField, Range(0.1f, 1f)] private float arcSpan = 0.75f;
        [Tooltip("Arc fill follow rate (higher = snappier).")]
        [SerializeField, Min(1f)] private float response = 10f;

        private int shownSpeed = -1;
        private string shownGear;
        private float fill;

        public string SpeedText => speedLabel != null ? speedLabel.text : "";
        public string GearText => gearLabel != null ? gearLabel.text : "";

        private void Update()
        {
            VehicleController car = levelManager != null ? levelManager.Vehicle : null;
            float kmh = car != null ? car.SpeedKmh : 0f;
            float top = car != null ? Mathf.Max(car.MaxForwardSpeedKmh, 1f) : 60f;
            fill = Mathf.Lerp(fill, Mathf.Clamp01(kmh / top) * arcSpan, 1f - Mathf.Exp(-response * Time.unscaledDeltaTime));
            if (arc != null) arc.fillAmount = fill;

            int whole = Mathf.RoundToInt(kmh);
            if (whole != shownSpeed && speedLabel != null) { shownSpeed = whole; speedLabel.text = whole.ToString(); }

            string gear = GearLetter(car);
            if (gear != shownGear && gearLabel != null)
            {
                shownGear = gear;
                gearLabel.text = gear;
                gearLabel.color = gear == "R" ? UiPalette.Gold : gear == "P" ? UiPalette.TextSecondary : UiPalette.TextPrimary;
            }
        }

        /// <summary>P when the car is held by the level, else the selected gear (Gears mode) or the travel direction (Classic / keyboard).</summary>
        public string GearLetter(VehicleController car)
        {
            if (car == null || !car.ControlsEnabled) return "P";
            if (controls != null && controls.UsesGears)
                return controls.Gear == DriveGear.Drive && car.IsReversing ? "R" : GearRules.Letter(controls.Gear);
            // Simple pedals: R while reversing or while reverse is held (even when stopped against something).
            return car.IsReversing || (car.Input != null && car.Input.Throttle < -0.05f) ? "R" : "D";
        }
    }
}
