using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Ads;
using UltimateCarParking.Audio;
using UltimateCarParking.Cars;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Title screen. Play continues the next unfinished campaign level; World Tour opens the country map; Missions
    /// opens the level list; Garage and Settings as before. Coins, star progress and the selected car on show.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private CarCatalog cars;
        [SerializeField] private LevelCatalog levels;
        [SerializeField] private CarShowroom showroom;
        [Tooltip("Mesh shown on the title screen. Falls back to the selected car when empty.")]
        [SerializeField] private GameObject heroPrefab;
        [SerializeField] private string heroName = "Sport";
        [SerializeField] private CarData heroCar;
        [SerializeField] private Button playButton;
        [SerializeField] private Button garageButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button worldTourButton;
        [SerializeField] private Button missionsButton;
        [Tooltip("Optional second Settings entry in the button grid (the gear stays).")]
        [SerializeField] private Button settingsMenuButton;
        [Tooltip("Featured level opened by the Featured button (the real-model showcase level).")]
        [SerializeField] private LevelCatalog featuredCatalog;
        [SerializeField] private Button featuredButton;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private TMP_Text carLabel;
        [SerializeField] private TMP_Text progressLabel;
        [Tooltip("Optional rewarded 'free coins' chip: shown only when an ad is ready and the cooldown has passed.")]
        [SerializeField] private Button freeCoinsButton;
        [Header("World Tour hero card")]
        [Tooltip("Live progress on the hero card: missions finished out of all missions.")]
        [SerializeField] private TMP_Text tourProgressLabel;
        [SerializeField] private WorldTour.CountryCatalog tourCatalog;
        [Tooltip("The hero card; it breathes gently to draw the eye.")]
        [SerializeField] private RectTransform heroCard;
        [SerializeField] private CanvasGroup[] introGroups = System.Array.Empty<CanvasGroup>();

        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(PlayNext);
            if (worldTourButton != null) worldTourButton.onClick.AddListener(() => ScreenTransition.LoadScene(SceneNames.WorldTour));
            if (missionsButton != null) missionsButton.onClick.AddListener(() => ScreenTransition.LoadScene(SceneNames.LevelSelect));
            if (settingsMenuButton != null && settingsPanel != null) settingsMenuButton.onClick.AddListener(settingsPanel.Open);
            if (garageButton != null) garageButton.onClick.AddListener(() => ScreenTransition.LoadScene(SceneNames.Garage));
            if (settingsButton != null && settingsPanel != null) settingsButton.onClick.AddListener(settingsPanel.Open);
            if (featuredButton != null) featuredButton.onClick.AddListener(PlayFeatured);
            if (freeCoinsButton != null) freeCoinsButton.onClick.AddListener(WatchFreeCoins);
        }

        private void OnEnable()
        {
            AdService.Initialize();
            AdService.ShowBanner();
            RefreshFreeCoins();
        }

        private void OnDisable() => AdService.HideBanner();

        private float nextRefresh;

        private void Update()
        {
            // The chip appears when an ad finishes loading or the cooldown ends; checked twice a second, not per frame.
            if (freeCoinsButton == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;
            RefreshFreeCoins();
            AdService.ShowBanner();
        }

        private void RefreshFreeCoins()
        {
            if (freeCoinsButton != null) freeCoinsButton.gameObject.SetActive(AdService.CanOfferRewarded(AdPlacement.FreeCoins));
        }

        private void WatchFreeCoins()
        {
            freeCoinsButton.interactable = false;
            AdService.ShowRewarded(AdPlacement.FreeCoins, watched =>
            {
                if (watched) AdService.AddCoins(AdPolicy.FreeCoinsAmount);
                freeCoinsButton.interactable = true;
                RefreshFreeCoins();
            });
        }

        private void Start()
        {
            AudioManager.Instance.PlayMusic(ducked: false);
            SaveData save = SaveManager.Current.Data;
            if (cars != null)
            {
                GarageService.EnsureDefaults(save, cars);
                CarData car = GarageService.SelectedCar(save, cars);
                if (showroom != null)
                {
                    if (heroPrefab != null) showroom.ShowPrefab(heroPrefab, heroName);
                    else if (heroCar != null) showroom.Show(heroCar);
                    else if (car != null) showroom.Show(car);
                }
                if (carLabel != null)
                {
                    if (heroPrefab != null && !string.IsNullOrEmpty(heroName)) carLabel.text = heroName.ToUpperInvariant();
                    else if (heroCar != null) carLabel.text = heroCar.DisplayName.ToUpperInvariant();
                    else if (car != null) carLabel.text = car.DisplayName.ToUpperInvariant();
                }
            }
            if (levels != null && progressLabel != null)
            {
                int done = 0;
                for (int i = 0; i < levels.Count; i++) if (ProgressionService.IsCompleted(save, levels.Get(i))) done++;
                progressLabel.text = $"{done}  /  {levels.Count}    LEVELS      ·      {ProgressionService.TotalStars(save, levels)}  /  {levels.Count * 3}    STARS";
            }
            RefreshTourProgress(save);
            if (heroCard != null) StartCoroutine(Breathe(heroCard));
            for (int i = 0; i < introGroups.Length; i++)
                if (introGroups[i] != null) StartCoroutine(Intro(introGroups[i], i * 0.08f));
        }

        /// <summary>World Tour missions finished and the total, from the save.</summary>
        public static (int done, int total) TourProgress(WorldTour.CountryCatalog tour, SaveData save)
        {
            int done = 0, total = 0;
            if (tour == null) return (0, 0);
            for (int i = 0; i < tour.Count; i++)
            {
                WorldTour.CountryData c = tour.Get(i);
                if (c == null) continue;
                total += c.MissionCount;
                done += Mathf.Min(c.MissionCount, WorldTour.WorldTourService.CompletedMissions(save, c));
            }
            return (done, total);
        }

        private void RefreshTourProgress(SaveData save)
        {
            if (tourProgressLabel == null) return;
            (int done, int total) = TourProgress(tourCatalog, save);
            tourProgressLabel.text = total > 0 ? $"{done} / {total} DONE" : "";
        }

        private static IEnumerator Breathe(RectTransform card)
        {
            while (card != null)
            {
                float k = 1f + 0.012f * Mathf.Sin(Time.unscaledTime * 2.2f);   // a slow 1.2% swell
                card.localScale = new Vector3(k, k, 1f);
                yield return null;
            }
        }

        /// <summary>The level Play starts: the furthest unlocked campaign level (the next one to beat).</summary>
        public LevelData NextLevel()
        {
            if (levels == null || levels.Count == 0) return null;
            int i = ProgressionService.FurthestUnlockedIndex(SaveManager.Current.Data, levels);
            return levels.Get(Mathf.Clamp(i, 0, levels.Count - 1));
        }

        public void PlayNext()
        {
            LevelData level = NextLevel();
            if (level == null) { ScreenTransition.LoadScene(SceneNames.LevelSelect); return; }
            LevelSelection.Country = null;
            LevelSelection.Request(level, levels);
            LevelSelection.ReturnScene = SceneNames.MainMenu;
            ScreenTransition.LoadScene(SceneNames.Gameplay);
        }

        public void PlayFeatured()
        {
            LevelData level = featuredCatalog != null ? featuredCatalog.Get(0) : null;
            if (level == null) { Debug.LogError("[MainMenu] No featured level assigned.", this); return; }
            LevelSelection.Country = null;
            LevelSelection.Request(level, featuredCatalog);
            LevelSelection.ReturnScene = SceneNames.MainMenu;
            ScreenTransition.LoadScene(SceneNames.Gameplay);
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
