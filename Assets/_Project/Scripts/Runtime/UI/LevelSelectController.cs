using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Audio;
using UltimateCarParking.Core;
using UltimateCarParking.Environment;
using UltimateCarParking.Levels;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Level grid grouped by environment theme (one tab per theme, in catalog order). Cards show locked,
    /// unlocked and completed states with best stars; tapping an open level starts it.
    /// </summary>
    public sealed class LevelSelectController : MonoBehaviour
    {
        [SerializeField] private LevelCatalog catalog;
        [SerializeField] private LevelCard cardPrefab;
        [SerializeField] private RectTransform grid;
        [SerializeField] private CanvasGroup gridGroup;

        [Header("Tabs (one per theme)")]
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private TMP_Text[] tabTitles;
        [SerializeField] private TMP_Text[] tabProgress;
        [SerializeField] private Image[] tabUnderlines;

        [Header("Header")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text starTotal;
        [SerializeField] private TMP_Text sectionCaption;

        private readonly List<LevelCard> cards = new List<LevelCard>();
        private readonly List<(EnvironmentTheme theme, int first, int count)> sections = new List<(EnvironmentTheme, int, int)>();
        private int currentTab = -1;
        private Coroutine tabAnim;

        public int CurrentTab => currentTab;
        public IReadOnlyList<LevelCard> Cards => cards;
        public int SectionCount => sections.Count;

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(() => ScreenTransition.LoadScene(SceneNames.MainMenu));
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => ShowTab(index));
            }
        }

        private void Start()
        {
            if (catalog == null || cardPrefab == null || grid == null)
            {
                Debug.LogError("[LevelSelect] Catalog, card prefab or grid is not assigned.", this);
                return;
            }
            AudioManager.Instance.PlayMusic(ducked: false);
            BuildSections();
            SaveData save = SaveManager.Current.Data;
            if (starTotal != null) starTotal.text = $"{ProgressionService.TotalStars(save, catalog)}/{catalog.Count * 3}";

            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool used = i < sections.Count;
                if (tabButtons[i] != null) tabButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                var (theme, first, count) = sections[i];
                if (i < tabTitles.Length && tabTitles[i] != null) tabTitles[i].text = theme != null ? theme.DisplayName.ToUpperInvariant() : "LEVELS";
                if (i < tabProgress.Length && tabProgress[i] != null)
                    tabProgress[i].text = $"{ProgressionService.TotalStars(save, catalog, first, first + count)}/{count * 3}";
            }
            if (sections.Count > tabButtons.Length)
                Debug.LogWarning($"[LevelSelect] {sections.Count} themes but only {tabButtons.Length} tabs; extra themes are not shown.", this);

            int resume = ProgressionService.FurthestUnlockedIndex(save, catalog);
            ShowTab(SectionOf(resume), animate: true);
        }

        /// <summary>Groups consecutive catalog levels that share a theme.</summary>
        private void BuildSections()
        {
            sections.Clear();
            for (int i = 0; i < catalog.Count; i++)
            {
                LevelData l = catalog.Get(i);
                EnvironmentTheme t = l != null ? l.Theme : null;
                // Mission levels on prefab environments have no theme: keep them in the neighbouring tab
                // (interim until Phase F's chapter tabs).
                if (t == null) t = sections.Count > 0 ? sections[^1].theme : FirstTheme();
                if (sections.Count == 0 || sections[^1].theme != t) sections.Add((t, i, 1));
                else sections[^1] = (sections[^1].theme, sections[^1].first, sections[^1].count + 1);
            }
        }

        private EnvironmentTheme FirstTheme()
        {
            for (int i = 0; i < catalog.Count; i++)
                if (catalog.Get(i) != null && catalog.Get(i).Theme != null) return catalog.Get(i).Theme;
            return null;
        }

        private int SectionOf(int levelIndex)
        {
            for (int s = 0; s < sections.Count; s++)
                if (levelIndex >= sections[s].first && levelIndex < sections[s].first + sections[s].count) return s;
            return 0;
        }

        public void ShowTab(int index, bool animate = true)
        {
            if (index < 0 || index >= sections.Count || index == currentTab) return;
            int direction = index > currentTab ? 1 : -1;
            currentTab = index;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool on = i == index;
                if (i < tabUnderlines.Length && tabUnderlines[i] != null) tabUnderlines[i].color = on ? UiPalette.Lime : UiPalette.Lime.WithAlpha(0f);
                if (i < tabTitles.Length && tabTitles[i] != null) tabTitles[i].color = on ? UiPalette.TextPrimary : UiPalette.TextSecondary;
            }
            var (theme, first, count) = sections[index];
            if (sectionCaption != null)
                sectionCaption.text = $"LEVELS {catalog.Get(first).LevelNumber} - {catalog.Get(first + count - 1).LevelNumber}";
            Populate(first, count);
            if (tabAnim != null) StopCoroutine(tabAnim);
            if (animate && isActiveAndEnabled) tabAnim = StartCoroutine(AnimateIn(direction));
        }

        private void Populate(int first, int count)
        {
            SaveData save = SaveManager.Current.Data;
            while (cards.Count < count) cards.Add(Instantiate(cardPrefab, grid));
            for (int i = 0; i < cards.Count; i++)
            {
                bool used = i < count;
                cards[i].gameObject.SetActive(used);
                if (!used) continue;
                LevelData level = catalog.Get(first + i);
                cards[i].Bind(level, ProgressionService.StateOf(save, catalog, level),
                    ProgressionService.BestStars(save, level), ProgressionService.BestTime(save, level), OnCardClicked);
            }
        }

        private IEnumerator AnimateIn(int direction)
        {
            if (gridGroup != null) StartCoroutine(UiMotion.Fade(gridGroup, 0f, 1f, 0.3f));
            for (int i = 0; i < cards.Count; i++)
            {
                if (!cards[i].gameObject.activeSelf) continue;
                StartCoroutine(UiMotion.Scale(cards[i].transform, 0.85f, 1f, 0.35f, i * 0.025f, back: true));
            }
            yield return UiMotion.Move(grid, new Vector2(80f * direction, 0f), Vector2.zero, 0.35f);
        }

        private void OnCardClicked(LevelCard card)
        {
            if (card.State == LevelLockState.Locked)
            {
                StartCoroutine(UiMotion.Shake((RectTransform)card.transform));
                return;
            }
            Play(card.Level);
        }

        public void Play(LevelData level)
        {
            if (level == null || !ProgressionService.IsUnlocked(SaveManager.Current.Data, catalog, level)) return;
            LevelSelection.Country = null;
            LevelSelection.Request(level, catalog);
            LevelSelection.ReturnScene = SceneNames.LevelSelect;
            ScreenTransition.LoadScene(SceneNames.Gameplay);
        }
    }
}
