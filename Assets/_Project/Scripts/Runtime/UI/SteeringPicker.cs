using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Audio;
using UltimateCarParking.Settings;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// WHEEL / LEFT / RIGHT / TILT segmented picker (pause screen). Edits the same setting as Driving Controls; the
    /// on-screen controls switch at once (ControlLayout listens to settings changes).
    /// </summary>
    public sealed class SteeringPicker : MonoBehaviour
    {
        [Tooltip("WHEEL, LEFT / RIGHT, TILT (GameSettings.SteeringChoices order).")]
        [SerializeField] private Button[] buttons = new Button[0];

        public int ButtonCount => buttons.Length;

        private void Awake()
        {
            for (int i = 0; i < buttons.Length && i < GameSettings.SteeringChoices.Length; i++)
            {
                int mode = GameSettings.SteeringChoices[i];
                if (buttons[i] != null) buttons[i].onClick.AddListener(() => Pick(mode));
            }
        }

        private void OnEnable() { SettingsService.Changed += OnSettings; Paint(); }
        private void OnDisable() => SettingsService.Changed -= OnSettings;
        private void OnSettings(GameSettings _) => Paint();

        public void Pick(int mode)
        {
            if (SettingsService.Current.steeringMode == mode) return;
            SettingsService.SetSteeringMode(mode);
            AudioManager.Instance.Play(Sfx.Click, 0.6f);
        }

        public void Paint()
        {
            int cur = SettingsService.Current.steeringMode;
            for (int i = 0; i < buttons.Length && i < GameSettings.SteeringChoices.Length; i++)
            {
                if (buttons[i] == null) continue;
                bool on = GameSettings.SteeringChoices[i] == cur;
                buttons[i].GetComponent<Image>().color = on ? UiPalette.Lime : UiPalette.SurfaceRaised;
                TMP_Text t = buttons[i].GetComponentInChildren<TMP_Text>();
                if (t != null) t.color = on ? UiPalette.TextOnLime : UiPalette.TextPrimary;
            }
        }
    }
}
