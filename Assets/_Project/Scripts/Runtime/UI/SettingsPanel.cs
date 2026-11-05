using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Settings;

namespace UltimateCarParking.UI
{
    /// <summary>Music, sound and steering-sensitivity sliders. Changes apply live; the save is written on close.</summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Slider music;
        [SerializeField] private Slider sound;
        [SerializeField] private Slider sensitivity;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private TMP_Text soundValue;
        [SerializeField] private TMP_Text sensitivityValue;
        [SerializeField] private Button closeButton;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (sensitivity != null) { sensitivity.minValue = GameSettings.MinSensitivity; sensitivity.maxValue = GameSettings.MaxSensitivity; }
            if (music != null) music.onValueChanged.AddListener(v => { SettingsService.SetMusicVolume(v); Render(); });
            if (sound != null) sound.onValueChanged.AddListener(v => { SettingsService.SetSoundVolume(v); Render(); });
            if (sensitivity != null) sensitivity.onValueChanged.AddListener(v => { SettingsService.SetSteeringSensitivity(v); Render(); });
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open()
        {
            GameSettings s = SettingsService.Current;
            if (music != null) music.SetValueWithoutNotify(s.musicVolume);
            if (sound != null) sound.SetValueWithoutNotify(s.soundVolume);
            if (sensitivity != null) sensitivity.SetValueWithoutNotify(s.steeringSensitivity);
            Render();
            gameObject.SetActive(true);
            if (group != null) StartCoroutine(UiMotion.Fade(group, 0f, 1f, 0.2f));
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            SettingsService.Commit();
            gameObject.SetActive(false);
        }

        private void Render()
        {
            GameSettings s = SettingsService.Current;
            if (musicValue != null) musicValue.text = $"{Mathf.RoundToInt(s.musicVolume * 100f)}%";
            if (soundValue != null) soundValue.text = $"{Mathf.RoundToInt(s.soundVolume * 100f)}%";
            if (sensitivityValue != null) sensitivityValue.text = $"{s.steeringSensitivity:0.0}x";
        }
    }
}
