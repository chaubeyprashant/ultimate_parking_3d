using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Levels;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Small HUD cluster for the car's own controls: headlights, left / right indicators, hazards, engine start / stop
    /// and a hold-to-sound horn. Bound to whichever car the level spawns. Selected states use the lime accent.
    /// </summary>
    public sealed class VehicleButtons : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private Button lightsButton, leftButton, hazardButton, rightButton, engineButton;
        [SerializeField] private HoldButton horn;

        private VehicleLights lights;
        public VehicleLights Lights => lights;

        private void Awake()
        {
            Hook(lightsButton, () => lights?.ToggleHeadlights());
            Hook(leftButton, () => lights?.ToggleIndicator(IndicatorMode.Left));
            Hook(rightButton, () => lights?.ToggleIndicator(IndicatorMode.Right));
            Hook(hazardButton, () => lights?.ToggleIndicator(IndicatorMode.Hazard));
            Hook(engineButton, () => lights?.ToggleEngine());
            if (horn != null) { horn.Pressed += () => lights?.SetHorn(true); horn.Released += () => lights?.SetHorn(false); }
            if (levelManager != null) levelManager.LevelStarted += _ => Bind();
        }

        private void Start() => Bind();

        private void Hook(Button b, System.Action a) { if (b != null) b.onClick.AddListener(() => { a(); Render(); }); }

        public void Bind()
        {
            lights = levelManager != null && levelManager.Vehicle != null ? VehicleLights.On(levelManager.Vehicle.gameObject) : null;
            Render();
        }

        private void LateUpdate()
        {
            // Hazard / indicator chips blink with the lamps; cheap (five colour writes).
            if (lights != null && lights.Indicators != IndicatorMode.Off) Render();
        }

        private void Render()
        {
            if (lights == null) return;
            Paint(lightsButton, lights.Headlights);
            bool blink = lights.BlinkOn;
            Paint(leftButton, lights.Indicators == IndicatorMode.Left && blink, amber: true);
            Paint(rightButton, lights.Indicators == IndicatorMode.Right && blink, amber: true);
            Paint(hazardButton, lights.Indicators == IndicatorMode.Hazard && blink, amber: true);
            Paint(engineButton, lights.EngineOn);
            if (engineButton != null)
            {
                TMP_Text t = engineButton.GetComponentInChildren<TMP_Text>();
                if (t != null) t.text = lights.EngineOn ? "ENGINE ON" : "ENGINE OFF";
            }
        }

        private static void Paint(Button b, bool on, bool amber = false)
        {
            if (b == null) return;
            TMP_Text t = b.GetComponentInChildren<TMP_Text>();
            Color c = on ? (amber ? new Color(1f, 0.62f, 0.12f) : UiPalette.Lime) : UiPalette.TextPrimary;
            if (t != null) t.color = c;
            Transform icon = b.transform.Find("Icon");
            if (icon != null) icon.GetComponent<Image>().color = c;
        }
    }
}
