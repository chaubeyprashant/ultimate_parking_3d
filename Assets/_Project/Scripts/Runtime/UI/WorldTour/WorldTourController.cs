using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Audio;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Save;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// World Tour screen: an interactive world map with 30 country pins (pan, pinch, region chips) and a detail
    /// card for the selected country — flag, arena, preview, description, mission progress, unlock status, Play.
    /// Locks and progress come from <see cref="WorldTourService"/> (i.e. the campaign save), so they always agree
    /// with level select.
    /// </summary>
    public sealed class WorldTourController : MonoBehaviour
    {
        public static readonly string[] RegionNames = { "WORLD", "ASIA", "EUROPE", "AMERICAS", "AFRICA & MIDDLE EAST", "OCEANIA" };

        [SerializeField] private CountryCatalog tour;
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldMapGraphic mapGraphic;
        [SerializeField] private RectTransform markerLayer;
        [SerializeField] private CountryMarker markerPrefab;

        [Header("Header")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text starTotal;
        [SerializeField] private RectTransform tourBarFill;
        [SerializeField] private CanvasGroup gestureHint;

        [Header("Region chips (World first, then WorldRegion order)")]
        [SerializeField] private Button[] regionButtons = System.Array.Empty<Button>();
        [SerializeField] private Image[] regionBackgrounds = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text[] regionLabels = System.Array.Empty<TMP_Text>();

        [Header("Detail card")]
        [SerializeField] private CanvasGroup detailGroup;
        [SerializeField] private RectTransform detailContent;
        [SerializeField] private TMP_Text kicker;
        [SerializeField] private Image flagImage;
        [SerializeField] private TMP_Text countryName;
        [SerializeField] private TMP_Text arenaLabel;
        [SerializeField] private Image previewImage;
        [Tooltip("Country bonus still to earn, on the preview.")]
        [SerializeField] private TMP_Text bonusLabel;
        [SerializeField] private GameObject previewLock;
        [SerializeField] private TMP_Text missionChip;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text missionsValue;
        [SerializeField] private TMP_Text starsValue;
        [SerializeField] private TMP_Text timeValue;
        [SerializeField] private Image statusChip;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text lockHint;
        [Header("Mission picker (one chip per mission; unused chips hide)")]
        [SerializeField] private Button[] missionButtons = System.Array.Empty<Button>();
        [SerializeField] private Image[] missionBackgrounds = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text[] missionLabels = System.Array.Empty<TMP_Text>();
        [SerializeField] private GameObject[] missionLocks = System.Array.Empty<GameObject>();
        [SerializeField] private Button playButton;
        [SerializeField] private Image playBackground;
        [SerializeField] private TMP_Text playLabel;
        [SerializeField] private GameObject playLock;

        private static readonly Color MapLocked = UiPalette.Hex(0x3C4652);
        private static readonly Color MapOpen = UiPalette.Hex(0x55761E);
        private static readonly Color MapDone = UiPalette.Hex(0x7FB424);
        private static string lastCountryId;
        /// <summary>Relative zoom from which every pin shows its name (below it only the selected pin does).</summary>
        public const float NameTagZoom = 6f;

        private readonly List<CountryMarker> markers = new List<CountryMarker>();
        private CountryMarker selected;
        private int region = -1;
        private SaveData save;
        private Coroutine detailAnim;
        private int selectedMission;

        public IReadOnlyList<CountryMarker> Markers => markers;
        public CountryMarker SelectedMarker => selected;
        public CountryData SelectedCountry => selected != null ? selected.Country : null;
        public int Region => region;
        public bool PlayAvailable => playButton != null && playButton.interactable;
        public string DetailName => countryName != null ? countryName.text : "";
        public string DetailStatus => statusLabel != null ? statusLabel.text : "";
        public string DetailMissions => missionsValue != null ? missionsValue.text : "";
        public string DetailLockHint => lockHint != null && lockHint.gameObject.activeSelf ? lockHint.text : "";
        public Sprite DetailFlag => flagImage != null ? flagImage.sprite : null;
        public string BonusText => bonusLabel != null && bonusLabel.transform.parent.gameObject.activeSelf ? bonusLabel.text : "";
        public Sprite DetailPreview => previewImage != null ? previewImage.sprite : null;
        public WorldMapView MapView => mapView;
        public int SelectedMission => selectedMission;
        public LevelData SelectedLevel => selected != null ? selected.Country.GetLevel(selectedMission) : null;
        public string DetailDescription => description != null ? description.text : "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => lastCountryId = null;

        /// <summary>Forget the remembered country so the next visit opens on the current stop (tests, new save).</summary>
        public static void ForgetSelection() => lastCountryId = null;

        /// <summary>Normalised map rectangle for a region chip (0 = whole world).</summary>
        public static Rect RegionRect(int chip) => chip switch
        {
            1 => WorldMapProjection.Box(-12f, 52f, 64f, 148f),     // Asia
            2 => WorldMapProjection.Box(35f, 62f, -12f, 42f),      // Europe
            3 => WorldMapProjection.Box(-56f, 62f, -128f, -32f),   // Americas
            4 => WorldMapProjection.Box(-36f, 42f, -20f, 60f),     // Africa & Middle East
            5 => WorldMapProjection.Box(-48f, 8f, 92f, 180f),      // Oceania
            _ => new Rect(0f, 0f, 1f, 1f),
        };

        public static int ChipOf(WorldRegion r) => 1 + (int)r;

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(() => ScreenTransition.LoadScene(SceneNames.MainMenu));
            for (int i = 0; i < regionButtons.Length; i++)
            {
                int chip = i;
                if (regionButtons[i] != null) regionButtons[i].onClick.AddListener(() => ShowRegion(chip));
            }
            if (playButton != null) playButton.onClick.AddListener(PlaySelected);
            for (int i = 0; i < missionButtons.Length; i++)
            {
                int m = i;
                if (missionButtons[i] != null) missionButtons[i].onClick.AddListener(() => { AudioManager.Click(); SelectMission(m); });
            }
        }

        private void Start()
        {
            if (tour == null || mapView == null || markerPrefab == null || markerLayer == null)
            {
                Debug.LogError("[WorldTour] Tour catalog, map view or marker prefab is not assigned.", this);
                return;
            }
            AudioManager.Instance.PlayMusic(ducked: false);
            save = SaveManager.Current.Data;
            if (CountryUnlockManager.RecordUnlocks(save, tour).Count > 0) SaveManager.Current.Save();   // sticky unlocks
            for (int i = 0; i < tour.Count; i++)
            {
                CountryData c = tour.Get(i);
                if (c == null) continue;
                CountryMarker m = Instantiate(markerPrefab, markerLayer);
                var rt = (RectTransform)m.transform;
                Vector2 n = WorldMapProjection.ToNormalized(c.LatLon);
                rt.anchorMin = rt.anchorMax = n;
                rt.anchoredPosition = Vector2.zero;
                m.Bind(c, i + 1, OnMarkerClicked);
                markers.Add(m);
            }
            mapView.Changed += UpdateNameTags;
            Refresh();

            int start = WorldTourService.CurrentIndex(save, tour);
            if (!string.IsNullOrEmpty(lastCountryId))
                for (int i = 0; i < markers.Count; i++) if (markers[i].Country.CountryId == lastCountryId) start = i;
            mapView.SetView(mapView.FitZoom, Vector2.zero);
            if (markers.Count > 0)
            {
                Select(start, focus: false);
                ShowRegion(ChipOf(markers[start].Country.Region), sound: false);
            }
            if (gestureHint != null) StartCoroutine(HintFade());
        }

        private void OnDestroy()
        {
            if (mapView != null) mapView.Changed -= UpdateNameTags;
        }

        /// <summary>Re-reads progress from the save and repaints pins, country shapes and the header.</summary>
        public void Refresh()
        {
            if (save == null) save = SaveManager.Current.Data;
            int stars = 0;
            foreach (CountryMarker m in markers)
            {
                CountryState s = WorldTourService.StateOf(save, tour, m.Country);
                m.SetState(s);
                stars += WorldTourService.Stars(save, m.Country);
                PaintCountry(m);
            }
            int done = WorldTourService.CompletedCountries(save, tour);
            if (progressLabel != null) progressLabel.text = $"{done} / {tour.Count}   COUNTRIES COMPLETED";
            if (starTotal != null) starTotal.text = $"{stars}/{tour.Count * 3}";
            if (tourBarFill != null) tourBarFill.anchorMax = new Vector2(tour.Count > 0 ? (float)done / tour.Count : 0f, 1f);
            if (selected != null) ShowDetail(selected, animate: false);
        }

        private void PaintCountry(CountryMarker m)
        {
            if (mapGraphic == null) return;
            Color c = m.Selected ? UiPalette.Lime
                : m.State == CountryState.Locked ? MapLocked
                : m.State == CountryState.Completed ? MapDone : MapOpen;
            mapGraphic.SetCountryColor(m.Country.CountryId, c);
        }

        private void OnMarkerClicked(CountryMarker m)
        {
            AudioManager.Click();
            Select(markers.IndexOf(m), focus: true);
        }

        public void Select(int index, bool focus = true)
        {
            if (index < 0 || index >= markers.Count) return;
            CountryMarker m = markers[index];
            CountryMarker previous = selected;
            selected = m;
            lastCountryId = m.Country.CountryId;
            if (previous != m) selectedMission = Mathf.Max(0, m.Country.IndexOfMission(WorldTourService.NextMission(save, m.Country)));
            if (previous != null && previous != m) { previous.SetSelected(false); PaintCountry(previous); }
            m.SetSelected(true);
            PaintCountry(m);
            ShowDetail(m, animate: previous != null && previous != m);
            UpdateNameTags();
            if (focus) mapView.CentreOn(WorldMapProjection.ToNormalized(m.Country.LatLon), mapView.RelativeZoom);
        }

        public void ShowRegion(int chip) => ShowRegion(chip, sound: true);

        private void ShowRegion(int chip, bool sound)
        {
            if (sound) AudioManager.Click();
            region = chip;
            for (int i = 0; i < regionBackgrounds.Length; i++)
            {
                bool on = i == chip;
                if (regionBackgrounds[i] != null) regionBackgrounds[i].color = on ? UiPalette.Lime : new Color(0.07f, 0.08f, 0.09f, 0.82f);
                if (i < regionLabels.Length && regionLabels[i] != null) regionLabels[i].color = on ? UiPalette.TextOnLime : UiPalette.TextPrimary;
            }
            mapView.Focus(RegionRect(chip), instant: !isActiveAndEnabled);
        }

        private void UpdateNameTags()
        {
            bool zoomed = mapView.RelativeZoom >= NameTagZoom;
            foreach (CountryMarker m in markers) m.SetNameVisible(zoomed || m.Selected);
        }

        private void ShowDetail(CountryMarker m, bool animate)
        {
            CountryData c = m.Country;
            int index = markers.IndexOf(m);
            selectedMission = Mathf.Clamp(selectedMission, 0, Mathf.Max(0, c.MissionCount - 1));
            LevelData next = c.GetLevel(selectedMission);
            bool multi = c.MissionCount > 1;
            bool missionLocked = !CountryProgressManager.IsMissionUnlocked(save, c, selectedMission);
            int done = WorldTourService.CompletedMissions(save, c);
            CountryState state = m.State;
            bool locked = state == CountryState.Locked;

            if (kicker != null) kicker.text = $"{RegionNames[ChipOf(c.Region)]}   ·   STOP {index + 1:00} / {tour.Count}";
            if (flagImage != null) flagImage.sprite = c.Flag;
            if (countryName != null) countryName.text = c.DisplayName.ToUpperInvariant();
            if (arenaLabel != null) arenaLabel.text = $"{c.ArenaName.ToUpperInvariant()}   <color=#8C96A3>·   {c.EnvironmentLabel.ToUpperInvariant()}</color>";
            if (previewImage != null) { previewImage.sprite = c.Preview; previewImage.color = locked ? new Color(0.45f, 0.47f, 0.5f) : Color.white; }
            if (previewLock != null) previewLock.SetActive(locked);
            if (missionChip != null)
                missionChip.text = multi && next != null && next.HasObjectives
                    ? Missions.MissionTypeInfo.Title(next.MissionType) : c.MissionLabel.ToUpperInvariant();
            if (description != null)
                description.text = multi && next != null
                    ? $"<color=#F2F5F7><b>MISSION {selectedMission + 1}: {next.DisplayName.ToUpperInvariant()}</b></color>  {Briefing(next, c)}"
                    : c.Description;
            PaintMissionChips(c);
            if (missionsValue != null) missionsValue.text = $"{done}/{c.MissionCount}";
            if (starsValue != null) starsValue.text = $"{WorldTourService.Stars(save, c)}/{c.MissionCount * 3}";
            if (timeValue != null && next != null)
            {
                int s = Mathf.RoundToInt(next.TimeLimitSeconds);
                timeValue.text = $"{s / 60}:{s % 60:00}";
            }
            int percent = CountryProgressManager.CompletionPercent(save, c);
            if (statusLabel != null) statusLabel.text = locked ? "LOCKED" : state == CountryState.Completed ? "COMPLETED" : percent > 0 ? $"{percent}% DONE" : "UNLOCKED";

            if (statusChip != null) statusChip.color = locked ? UiPalette.Locked : state == CountryState.Completed ? UiPalette.Lime : UiPalette.LimeDim;
            if (statusLabel != null) statusLabel.color = state == CountryState.Completed ? UiPalette.TextOnLime : locked ? UiPalette.TextPrimary : UiPalette.Lime;

            if (bonusLabel != null)
            {
                string bonus = locked ? "" : RewardNote(save, c);
                bonusLabel.transform.parent.gameObject.SetActive(bonus.Length > 0);
                bonusLabel.text = bonus;
            }
            if (lockHint != null)
            {
                lockHint.gameObject.SetActive(locked);
                string reason = CountryUnlockManager.Evaluate(save, tour, c).Reason;
                lockHint.text = string.IsNullOrEmpty(reason) ? "LOCKED" : reason;
            }
            bool blocked = locked || missionLocked;
            if (playButton != null) playButton.interactable = !blocked;
            if (playBackground != null) playBackground.color = blocked ? UiPalette.Locked : UiPalette.Lime;
            if (playLabel != null)
            {
                bool missionDone = next != null && Progression.ProgressionService.IsCompleted(save, next);
                playLabel.text = locked ? "LOCKED" : missionLocked ? $"FINISH MISSION {selectedMission}" : missionDone ? "PLAY AGAIN"
                    : multi ? $"PLAY MISSION {selectedMission + 1}" : "PLAY";
                playLabel.color = blocked ? UiPalette.TextSecondary : UiPalette.TextOnLime;
            }
            if (playLock != null) playLock.SetActive(blocked);

            if (animate && detailGroup != null && detailContent != null && isActiveAndEnabled)
            {
                if (detailAnim != null) StopCoroutine(detailAnim);
                detailAnim = StartCoroutine(DetailIn());
            }
        }

        public void SelectMission(int index)
        {
            if (selected == null || index < 0 || index >= selected.Country.MissionCount) return;
            selectedMission = index;
            ShowDetail(selected, animate: false);
        }

        /// <summary>Country bonuses still to earn ("" when both are paid).</summary>
        public static string RewardNote(SaveData save, CountryData c)
        {
            var paid = save?.countryRewardIds;
            bool done = paid != null && paid.Contains(c.CountryId), perfect = paid != null && paid.Contains(c.CountryId + "*");
            if (done && perfect) return "";
            string note = done ? $"3 stars everywhere: +{c.PerfectCoins}" : $"Finish all: +{c.CompletionCoins}   ·   3 stars everywhere: +{c.PerfectCoins}";
            return $"<color=#C8F03C>COUNTRY BONUS</color>  {note} coins";
        }

        private void PaintMissionChips(CountryData c)
        {
            for (int i = 0; i < missionButtons.Length; i++)
            {
                bool shown = c.MissionCount > 1 && i < c.MissionCount;
                if (missionButtons[i] != null) missionButtons[i].gameObject.SetActive(shown);
                if (!shown) continue;
                LevelData l = c.GetLevel(i);
                bool open = CountryProgressManager.IsMissionUnlocked(save, c, i);
                int stars = l != null ? Progression.ProgressionService.BestStars(save, l) : 0;
                bool on = i == selectedMission;
                if (i < missionBackgrounds.Length && missionBackgrounds[i] != null)
                    missionBackgrounds[i].color = on ? UiPalette.Lime : UiPalette.SurfaceRaised;
                if (i < missionLabels.Length && missionLabels[i] != null)
                {
                    missionLabels[i].text = open ? $"M{i + 1}   {stars}/3" : $"M{i + 1}";
                    missionLabels[i].color = on ? UiPalette.TextOnLime : open ? UiPalette.TextPrimary : UiPalette.TextSecondary;
                }
                if (i < missionLocks.Length && missionLocks[i] != null) missionLocks[i].SetActive(!open);
            }
        }

        private static string Briefing(LevelData level, CountryData country)
        {
            string b = level.Briefing;
            return string.IsNullOrEmpty(b) ? country.Description : b;
        }

        private IEnumerator DetailIn()
        {
            StartCoroutine(UiMotion.Fade(detailGroup, 0.2f, 1f, 0.25f));
            yield return UiMotion.Move(detailContent, new Vector2(26f, 0f), Vector2.zero, 0.25f);
        }

        public void PlaySelected()
        {
            if (selected == null || selected.State == CountryState.Locked) return;
            if (!CountryProgressManager.IsMissionUnlocked(save, selected.Country, selectedMission)) return;
            LevelData level = selected.Country.GetLevel(selectedMission);
            if (level == null) { Debug.LogError("[WorldTour] Country has no missions: " + selected.Country.CountryId, this); return; }
            CountrySelectionManager.Select(tour, selected.Country);
            LevelSelection.Country = selected.Country;
            LevelSelection.Request(level, selected.Country.MissionCatalog != null ? selected.Country.MissionCatalog : tour.Campaign);
            LevelSelection.ReturnScene = SceneNames.WorldTour;
            ScreenTransition.LoadScene(SceneNames.Gameplay);
        }

        private IEnumerator HintFade()
        {
            gestureHint.alpha = 1f;
            float t = 0f;
            while (t < 6f && !mapView.Interacted) { t += Time.unscaledDeltaTime; yield return null; }
            yield return UiMotion.Fade(gestureHint, 1f, 0f, 0.6f);
        }
    }
}
