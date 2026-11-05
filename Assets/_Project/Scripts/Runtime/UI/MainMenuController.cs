using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Audio;
using UltimateCarParking.Cars;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;

namespace UltimateCarParking.UI
{
    /// <summary>Title screen: Play (level select), Garage, coins, star progress and the selected car on show.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private CarCatalog cars;
        [SerializeField] private LevelCatalog levels;
        [SerializeField] private CarShowroom showroom;
        [SerializeField] private Button playButton;
        [SerializeField] private Button garageButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private TMP_Text carLabel;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private CanvasGroup[] introGroups = System.Array.Empty<CanvasGroup>();

        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(() => ScreenTransition.LoadScene(SceneNames.LevelSelect));
            if (garageButton != null) garageButton.onClick.AddListener(() => ScreenTransition.LoadScene(SceneNames.Garage));
            if (settingsButton != null && settingsPanel != null) settingsButton.onClick.AddListener(settingsPanel.Open);
        }

        private void Start()
        {
            AudioManager.Instance.PlayMusic(ducked: false);
            SaveData save = SaveManager.Current.Data;
            if (cars != null)
            {
                GarageService.EnsureDefaults(save, cars);
                CarData car = GarageService.SelectedCar(save, cars);
                if (car != null)
                {
                    if (showroom != null) showroom.Show(car);
                    if (carLabel != null) carLabel.text = car.DisplayName.ToUpperInvariant();
                }
            }
            if (levels != null && progressLabel != null)
            {
                int done = 0;
                for (int i = 0; i < levels.Count; i++) if (ProgressionService.IsCompleted(save, levels.Get(i))) done++;
                progressLabel.text = $"{done}/{levels.Count} LEVELS CLEARED      {ProgressionService.TotalStars(save, levels)}/{levels.Count * 3} STARS";
            }
            for (int i = 0; i < introGroups.Length; i++)
                if (introGroups[i] != null) StartCoroutine(Intro(introGroups[i], i * 0.08f));
        }

        private IEnumerator Intro(CanvasGroup g, float delay)
        {
            var rt = (RectTransform)g.transform;
            Vector2 end = rt.anchoredPosition;
            g.alpha = 0f;
            yield return new WaitForSecondsRealtime(delay);
            StartCoroutine(UiMotion.Fade(g, 0f, 1f, 0.45f));
            yield return UiMotion.Move(rt, end + new Vector2(-40f, 0f), end, 0.45f);
        }
    }
}
