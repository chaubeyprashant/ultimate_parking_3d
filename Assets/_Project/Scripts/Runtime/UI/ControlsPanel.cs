using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Settings;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Settings → Controls: steering mode, pedal mode, steering sensitivity, control size, opacity, height,
    /// left/right-handed layout and tilt calibration. Changes apply live (the touch controls re-lay themselves out);
    /// the save is written when the panel closes, so the choices persist across sessions.
    /// </summary>
    public sealed class ControlsPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Button steeringButton;
        [SerializeField] private TMP_Text steeringLabel;
        [SerializeField] private Button pedalsButton;
        [SerializeField] private TMP_Text pedalsLabel;
        [SerializeField] private Button handButton;
        [SerializeField] private TMP_Text handLabel;
        [SerializeField] private Slider sensitivity;
        [SerializeField] private TMP_Text sensitivityValue;
        [SerializeField] private Slider size;
        [SerializeField] private TMP_Text sizeValue;
        [SerializeField] private Slider opacity;
        [SerializeField] private TMP_Text opacityValue;
        [SerializeField] private Slider height;
        [SerializeField] private TMP_Text heightValue;
        [SerializeField] private Slider hudSize;
        [SerializeField] private TMP_Text hudSizeValue;
        [SerializeField] private Button calibrateButton;
        [SerializeField] private TMP_Text calibrateLabel;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button assistButton;
        [SerializeField] private TMP_Text assistLabel;
        [SerializeField] private Button beepsButton;
        [SerializeField] private TMP_Text beepsLabel;
        [SerializeField] private Button closeButton;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            Range(sensitivity, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, v => SettingsService.SetSteeringSensitivity(v));
            Range(size, GameSettings.MinControlScale, GameSettings.MaxControlScale, v => SettingsService.SetControlScale(v));
            Range(opacity, GameSettings.MinControlOpacity, 1f, v => SettingsService.SetControlOpacity(v));
            Range(height, 0f, 1f, v => SettingsService.SetControlsRaise(v));
            Range(hudSize, GameSettings.MinHudScale, GameSettings.MaxHudScale, v => SettingsService.SetHudScale(v));
            Click(steeringButton, () => SettingsService.SetSteeringMode((SettingsService.Current.steeringMode + 1) % 3));
            Click(pedalsButton, () => SettingsService.SetPedalMode(1 - SettingsService.Current.pedalMode));
            Click(handButton, () => SettingsService.SetLeftHanded(!SettingsService.Current.leftHanded));
            Click(calibrateButton, Calibrate);
            Click(assistButton, () => SettingsService.SetAssistMode((SettingsService.Current.assistMode + 1) % 3));
            Click(beepsButton, () => SettingsService.SetAssistSounds(!SettingsService.Current.assistSounds));
            Click(resetButton, () => { SettingsService.ResetControls(); Sync(); });
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        private void Range(Slider s, float min, float max, System.Action<float> set)
        {
            if (s == null) return;
            s.minValue = min;
            s.maxValue = max;
            s.onValueChanged.AddListener(v => { set(v); Render(); });
        }

        private void Click(Button b, System.Action a)
        {
            if (b != null) b.onClick.AddListener(() => { a(); Render(); });
        }

        public void Open()
        {
            Sync();
            gameObject.SetActive(true);
            if (group != null) StartCoroutine(UiMotion.Fade(group, 0f, 1f, 0.2f));
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            SettingsService.Commit();
            gameObject.SetActive(false);
        }

        private void Sync()
        {
            GameSettings s = SettingsService.Current;
            if (sensitivity != null) sensitivity.SetValueWithoutNotify(s.steeringSensitivity);
            if (size != null) size.SetValueWithoutNotify(s.controlScale);
            if (opacity != null) opacity.SetValueWithoutNotify(s.controlOpacity);
            if (height != null) height.SetValueWithoutNotify(s.controlsRaise);
            if (hudSize != null) hudSize.SetValueWithoutNotify(s.hudScale);
            if (hint != null) hint.text = "";
            Render();
        }

        private void Calibrate()
        {
            bool ok = TiltInput.Calibrate();
            if (hint != null) hint.text = ok ? "Straight ahead saved. Hold the phone like this to drive straight."
                                             : "No tilt reading yet. Hold the phone steady and tap again.";
        }

        private void Render()
        {
            GameSettings s = SettingsService.Current;
            if (s.steeringMode == GameSettings.SteerTilt) TiltInput.Enable(true);   // warm the sensor so calibration reads at once
            if (steeringLabel != null) steeringLabel.text = "STEERING: " + GameSettings.SteeringName(s.steeringMode);
            if (pedalsLabel != null) pedalsLabel.text = "PEDALS: " + (s.pedalMode == GameSettings.PedalsGears ? "GEARS P-R-D" : "SIMPLE");
            if (handLabel != null) handLabel.text = s.leftHanded ? "LAYOUT: LEFT-HANDED" : "LAYOUT: RIGHT-HANDED";
            if (sensitivityValue != null) sensitivityValue.text = $"{s.steeringSensitivity:0.0}x";
            if (sizeValue != null) sizeValue.text = $"{Mathf.RoundToInt(s.controlScale * 100f)}%";
            if (opacityValue != null) opacityValue.text = $"{Mathf.RoundToInt(s.controlOpacity * 100f)}%";
            if (heightValue != null) heightValue.text = $"{Mathf.RoundToInt(s.controlsRaise * 100f)}%";
            if (hudSizeValue != null) hudSizeValue.text = $"{Mathf.RoundToInt(s.hudScale * 100f)}%";
            if (assistLabel != null) assistLabel.text = "ASSIST: " + GameSettings.AssistName(s.assistMode);
            if (beepsLabel != null) beepsLabel.text = s.assistSounds ? "SENSOR BEEPS: ON" : "SENSOR BEEPS: OFF";
            bool tilt = s.steeringMode == GameSettings.SteerTilt;
            if (calibrateButton != null) calibrateButton.interactable = tilt;
            if (calibrateLabel != null) calibrateLabel.text = tilt ? "SET STRAIGHT AHEAD (TILT)" : "TILT CALIBRATION (TILT MODE ONLY)";
        }
    }
}
