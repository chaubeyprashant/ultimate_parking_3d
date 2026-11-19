using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Audio;
using UltimateCarParking.Career;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Career map: Driving Academy + five chapters on the left (progress, stars, state), the selected chapter's missions
    /// on the right (stars, best score or time, lock), a mission details card with PLAY / REPLAY, and a reward pop-up for
    /// chapter rewards earned (also pays rewards an older save already earned). Built from inactive templates.
    /// </summary>
    public sealed class CareerController : MonoBehaviour
    {
        [SerializeField] private CareerCatalog career;
        [Header("Header")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private CoinCounter coins;
        [Header("Chapters")]
        [SerializeField] private RectTransform chapterList;
        [SerializeField] private Button chapterTemplate;
        [Header("Selected chapter")]
        [SerializeField] private TMP_Text chapterTitle;
        [SerializeField] private TMP_Text chapterSubtitle;
        [SerializeField] private TMP_Text chapterRewards;
        [SerializeField] private RectTransform chapterBar;
        [SerializeField] private Image chapterBarFill;
        [SerializeField] private RectTransform missionGrid;
        [SerializeField] private Button missionTemplate;
        [Header("Mission details")]
        [SerializeField] private CanvasGroup details;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailMeta;
        [SerializeField] private TMP_Text detailBriefing;
        [SerializeField] private TMP_Text detailBest;
        [SerializeField] private Button playButton;
        [SerializeField] private TMP_Text playLabel;
        [Header("Rewards")]
        [SerializeField] private CanvasGroup rewardPopup;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button rewardOk;

        /// <summary>-1 = Driving Academy, 0..4 = chapters. Remembered for the session.</summary>
        private static int selectedChapter = int.MinValue;
        private int selectedMission = -1;
        private readonly List<Button> chapterButtons = new List<Button>();
        private readonly List<Button> missionButtons = new List<Button>();

        public int SelectedChapter => selectedChapter;
        public int MissionCardCount => missionButtons.Count;
        public string RewardText => rewardText != null && rewardPopup != null && rewardPopup.alpha > 0.5f ? rewardText.text : "";
        public string ChapterTitleText => chapterTitle != null ? chapterTitle.text : "";
        public string DetailBestText => detailBest != null ? detailBest.text : "";
        public string PlayText => playLabel != null ? playLabel.text : "";
        private SaveData Save => SaveManager.Current.Data;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => selectedChapter = int.MinValue;

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(() => ScreenTransition.LoadScene(SceneNames.MainMenu));
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (rewardOk != null) rewardOk.onClick.AddListener(() => { if (rewardPopup != null) { rewardPopup.alpha = 0f; rewardPopup.blocksRaycasts = false; } });
            if (chapterTemplate != null) chapterTemplate.gameObject.SetActive(false);
            if (missionTemplate != null) missionTemplate.gameObject.SetActive(false);
            if (details != null) details.alpha = 0f;
            if (rewardPopup != null) { rewardPopup.alpha = 0f; rewardPopup.blocksRaycasts = false; }
        }

        private void Start()
        {
            if (career == null) { Debug.LogError("[Career] no CareerCatalog", this); return; }
            AudioManager.Instance.PlayMusic(ducked: false);
            // Rewards an older save already earned (chapters finished before the career existed) are paid here once.
            List<CareerReward> paid = CareerService.ClaimRewards(Save, career);
            if (paid.Count > 0) { SaveManager.Current.Save(); ShowRewards(paid); }
            if (selectedChapter == int.MinValue) selectedChapter = FirstUnfinishedChapter();
            BuildChapters();
            SelectChapter(selectedChapter);
        }

        private int FirstUnfinishedChapter()
        {
            for (int i = 0; i < career.ChapterCount; i++)
                if (CareerService.State(Save, career, i) == ChapterState.Open) return i;
            return 0;
        }

        // ---------------------------------------------------------------- chapters

        private void BuildChapters()
        {
            foreach (Button b in chapterButtons) Destroy(b.gameObject);
            chapterButtons.Clear();
            AddChapterCard(-1);
            for (int i = 0; i < career.ChapterCount; i++) AddChapterCard(i);
            (int done, int total) = CareerService.Progress(Save, career);
            int stars = 0; for (int i = 0; i < career.ChapterCount; i++) stars += CareerService.Stars(Save, career.Chapter(i));
            if (progressLabel != null) progressLabel.text = $"{done} / {total} MISSIONS  ·  {stars} STARS";
            if (coins != null) coins.Refresh();
        }

        private void AddChapterCard(int index)
        {
            Button card = Instantiate(chapterTemplate, chapterList);
            card.gameObject.SetActive(true);
            card.name = index < 0 ? "Chapter_Academy" : $"Chapter_{index + 1}";
            TMP_Text title = card.transform.Find("Title").GetComponent<TMP_Text>();
            TMP_Text meta = card.transform.Find("Meta").GetComponent<TMP_Text>();
            TMP_Text state = card.transform.Find("State").GetComponent<TMP_Text>();
            Image accent = card.transform.Find("Accent").GetComponent<Image>();
            if (index < 0)
            {
                LevelCatalog a = career.Academy;
                int done = 0; if (a != null) for (int i = 0; i < a.Count; i++) if (ProgressionService.IsCompleted(Save, a.Get(i))) done++;
                title.text = "DRIVING ACADEMY";
                meta.text = $"{done} / {(a != null ? a.Count : 0)} LESSONS";
                state.text = a != null && done == a.Count ? "DONE" : "TUTORIALS";
                accent.color = UiPalette.TextPrimary;
            }
            else
            {
                CareerChapter ch = career.Chapter(index);
                ChapterState st = CareerService.State(Save, career, index);
                title.text = $"{index + 1}. {ch.title}";
                meta.text = st == ChapterState.Locked ? $"COMPLETE {ch.unlockAfter} IN CHAPTER {index} TO UNLOCK"
                    : $"{CareerService.Completed(Save, ch)} / {ch.Count}  ·  {CareerService.Stars(Save, ch)} / {ch.Count * 3} STARS";
                state.text = st switch { ChapterState.Locked => "LOCKED", ChapterState.Completed => "COMPLETE", ChapterState.Mastered => "MASTERED", _ => "OPEN" };
                state.color = st == ChapterState.Locked ? UiPalette.TextSecondary : st == ChapterState.Open ? UiPalette.TextPrimary : UiPalette.Lime;
                accent.color = st == ChapterState.Locked ? UiPalette.Locked : ch.accent;
            }
            int captured = index;
            card.onClick.AddListener(() => SelectChapter(captured));
            chapterButtons.Add(card);
        }

        public void SelectChapter(int index)
        {
            selectedChapter = Mathf.Clamp(index, -1, career.ChapterCount - 1);
            for (int i = 0; i < chapterButtons.Count; i++)
            {
                var img = chapterButtons[i].GetComponent<Image>();
                if (img != null) img.color = i - 1 == selectedChapter ? UiPalette.SurfaceRaised : UiPalette.Surface;
            }
            bool academy = selectedChapter < 0;
            LevelCatalog list = academy ? career.Academy : career.Chapter(selectedChapter).missions;
            if (academy)
            {
                chapterTitle.text = "DRIVING ACADEMY";
                chapterSubtitle.text = "Short hands-on lessons: steering, pedals, gears, reversing, parallel parking, sensors and cameras.";
                chapterRewards.text = "100 COINS FOR EACH FIRST LESSON";
            }
            else
            {
                CareerChapter ch = career.Chapter(selectedChapter);
                chapterTitle.text = $"CHAPTER {selectedChapter + 1}  ·  {ch.title}";
                chapterSubtitle.text = ch.subtitle;
                bool paid = Save.careerRewardIds != null && Save.careerRewardIds.Contains(ch.id), mastered = Save.careerRewardIds != null && Save.careerRewardIds.Contains(ch.id + "*");
                chapterRewards.text = $"COMPLETE  +{ch.completionCoins:N0}{(paid ? "  PAID" : "")}     MASTERY (ALL 3 STARS)  +{ch.masteryCoins:N0}{(mastered ? "  PAID" : "")}";
            }
            int done = 0;
            if (list != null) for (int i = 0; i < list.Count; i++) if (ProgressionService.IsCompleted(Save, list.Get(i))) done++;
            if (chapterBarFill != null && list != null && list.Count > 0)
            {
                RectTransform f = chapterBarFill.rectTransform;
                f.anchorMax = new Vector2(done / (float)list.Count, 1f);
            }
            BuildMissions(list, academy);
            int next = academy ? 0 : CareerService.NextMission(Save, career, selectedChapter);
            SelectMission(Mathf.Max(0, next));
        }

        // ---------------------------------------------------------------- missions

        private bool MissionOpen(int i) => selectedChapter < 0 || CareerService.IsMissionOpen(Save, career, selectedChapter, i);

        private void BuildMissions(LevelCatalog list, bool academy)
        {
            foreach (Button b in missionButtons) Destroy(b.gameObject);
            missionButtons.Clear();
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                LevelData l = list.Get(i);
                Button card = Instantiate(missionTemplate, missionGrid);
                card.gameObject.SetActive(true);
                card.name = "Mission_" + l.LevelId;
                bool open = MissionOpen(i);
                int stars = ProgressionService.BestStars(Save, l);
                card.transform.Find("Number").GetComponent<TMP_Text>().text = academy ? $"LESSON {i + 1}" : $"MISSION {i + 1}";
                card.transform.Find("Name").GetComponent<TMP_Text>().text = l.DisplayName.ToUpperInvariant();
                card.transform.Find("Type").GetComponent<TMP_Text>().text = academy ? "TUTORIAL" : Meta(l);
                for (int s = 0; s < 3; s++)
                {
                    Transform star = card.transform.Find("Star" + s);
                    if (star != null) star.GetComponent<Image>().color = s < stars ? UiPalette.Lime : UiPalette.Outline;
                }
                card.transform.Find("Lock").gameObject.SetActive(!open);
                card.interactable = true;
                int captured = i;
                card.onClick.AddListener(() => SelectMission(captured));
                missionButtons.Add(card);
            }
        }

        private static string Meta(LevelData l)
        {
            string type = MissionTypeInfo.Title(l.MissionType);
            string diff = DifficultyProfile.Name(l.Difficulty);
            return diff.Length > 0 ? type + "  ·  " + diff : type;
        }

        public void SelectMission(int index)
        {
            LevelCatalog list = selectedChapter < 0 ? career.Academy : career.Chapter(selectedChapter).missions;
            if (list == null || list.Count == 0) return;
            selectedMission = Mathf.Clamp(index, 0, list.Count - 1);
            for (int i = 0; i < missionButtons.Count; i++)
                missionButtons[i].GetComponent<Image>().color = i == selectedMission ? UiPalette.SurfaceRaised : UiPalette.Surface;
            LevelData l = list.Get(selectedMission);
            bool open = MissionOpen(selectedMission);
            bool done = ProgressionService.IsCompleted(Save, l);
            detailName.text = l.DisplayName.ToUpperInvariant();
            detailMeta.text = selectedChapter < 0 ? "DRIVING ACADEMY" : Meta(l) + (l.Condition != Conditions.RoadCondition.Clear ? "  ·  " + l.Condition.ToString().ToUpperInvariant() : "");
            detailBriefing.text = l.Briefing;
            LevelProgress p = Save.FindLevel(l.LevelId);
            if (p == null || !p.Completed) detailBest.text = open ? "NOT PLAYED YET" : "LOCKED: FINISH THE MISSION BEFORE IT";
            else
            {
                string score = p.bestQuality > 0f && l.ScoreMode != ScoreMode.TimeAndCollisions ? $"BEST SCORE {Mathf.RoundToInt(p.bestQuality * 100f)}%  ·  " : "";
                detailBest.text = $"{score}BEST TIME {p.bestTimeSeconds:0.0}s  ·  {p.bestStars}/3 STARS";
            }
            playLabel.text = !open ? "LOCKED" : done ? "REPLAY" : "PLAY";
            playButton.interactable = open;
            if (details != null && details.alpha < 1f && isActiveAndEnabled) StartCoroutine(UiMotion.Fade(details, details.alpha, 1f, 0.2f));
        }

        public void Play()
        {
            LevelCatalog list = selectedChapter < 0 ? career.Academy : career.Chapter(selectedChapter).missions;
            if (list == null || selectedMission < 0 || !MissionOpen(selectedMission)) return;
            LevelSelection.Country = null;
            LevelSelection.Request(list.Get(selectedMission), list);   // NEXT plays on within the chapter
            LevelSelection.ReturnScene = SceneNames.Career;
            ScreenTransition.LoadScene(SceneNames.Gameplay);
        }

        private void ShowRewards(List<CareerReward> paid)
        {
            if (rewardPopup == null || rewardText == null) return;
            var sb = new System.Text.StringBuilder();
            foreach (CareerReward r in paid) sb.AppendLine(r.Label);
            rewardText.text = sb.ToString().TrimEnd();
            rewardPopup.alpha = 1f;
            rewardPopup.blocksRaycasts = true;
            AudioManager.Instance.Play(Sfx.Coin, 0.9f);
        }
    }
}
