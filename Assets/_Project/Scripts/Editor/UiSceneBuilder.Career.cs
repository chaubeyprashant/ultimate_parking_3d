using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UltimateCarParking.Career;
using UltimateCarParking.Core;
using UltimateCarParking.UI;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Career scene (Phase 6):
    ///   left  — chapter cards: Driving Academy + 5 chapters (title, progress, stars, state, accent bar)
    ///   right — chapter header (title, theme, rewards, progress bar), mission cards (number, name, type · difficulty,
    ///           3 stars, lock), mission details (name, meta, briefing, best score / time, PLAY / REPLAY)
    ///   modal — chapter reward pop-up.
    /// </summary>
    public static partial class UiSceneBuilder
    {
        private static void BuildCareer()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera2D();
            RectTransform root = MakeCanvas("CareerCanvas", out _);
            Fill(Img(root, "Background", "Gradient", Color.white).rectTransform, 0, 0, 0, 0);
            Image glow = Img(root, "Glow", "Glow", UiPalette.Lime.WithAlpha(0.035f), sliced: false);
            Place(glow.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-200f, -120f), new Vector2(1400f, 1400f));
            RectTransform safe = SafeArea(root);

            Button back = CircleButton(safe, "BackButton", "ChevronLeft", new Vector2(0f, 1f), new Vector2(110f, -100f));
            TMP_Text title = Label(safe, "Title", "CAREER", true, 66f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, 4f);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(190f, -90f), new Vector2(500f, 80f));
            TMP_Text progress = Label(safe, "Progress", "0 / 42 MISSIONS", false, 24f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 5f);
            Place(progress.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(194f, -136f), new Vector2(700f, 34f));
            CoinPill(safe);

            // Left: chapter list
            RectTransform chapters = Group(safe, "Chapters", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -190f), new Vector2(560f, 820f));
            var vl = chapters.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 14f; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.childControlHeight = false; vl.childControlWidth = true;
            Image chapterCard = Img(chapters, "ChapterTemplate", "RoundRect", UiPalette.Surface, raycast: true);
            chapterCard.rectTransform.sizeDelta = new Vector2(560f, 112f);
            var chapterButton = chapterCard.gameObject.AddComponent<Button>();
            chapterButton.transition = Selectable.Transition.None;
            chapterCard.gameObject.AddComponent<UIButtonFX>();
            Image accent = Img(chapterCard.rectTransform, "Accent", "Pill", UiPalette.Lime);
            Place(accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(8f, 72f));
            TMP_Text cTitle = Label(chapterCard.rectTransform, "Title", "1. BEGINNER ACADEMY", true, 30f, UiPalette.TextPrimary, TextAlignmentOptions.TopLeft, 2f);
            Place(cTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -16f), new Vector2(380f, 40f));
            TMP_Text cMeta = Label(chapterCard.rectTransform, "Meta", "0 / 8  ·  0 / 24 STARS", false, 20f, UiPalette.TextSecondary, TextAlignmentOptions.BottomLeft, 3f);
            Place(cMeta.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(42f, 16f), new Vector2(480f, 30f));
            TMP_Text cState = Label(chapterCard.rectTransform, "State", "OPEN", true, 20f, UiPalette.TextPrimary, TextAlignmentOptions.TopRight, 5f);
            Place(cState.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -18f), new Vector2(160f, 30f));

            // Right: selected chapter
            RectTransform right = Group(safe, "ChapterView", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(660f, -190f), new Vector2(1180f, 820f));
            TMP_Text chTitle = Label(right, "ChapterTitle", "CHAPTER 1", true, 40f, UiPalette.TextPrimary, TextAlignmentOptions.TopLeft, 3f);
            Place(chTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1180f, 50f));
            TMP_Text chSub = Label(right, "ChapterSubtitle", "", false, 22f, UiPalette.TextSecondary, TextAlignmentOptions.TopLeft, 0f);
            Place(chSub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -52f), new Vector2(1180f, 32f));
            TMP_Text chRewards = Label(right, "ChapterRewards", "", true, 20f, UiPalette.Gold, TextAlignmentOptions.TopLeft, 3f);
            Place(chRewards.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -88f), new Vector2(1180f, 30f));
            Image barTrack = Img(right, "ProgressBar", "Pill", UiPalette.Outline);
            Place(barTrack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -126f), new Vector2(1180f, 8f));
            Image barFill = Img(barTrack.rectTransform, "Fill", "Pill", UiPalette.Lime);
            barFill.rectTransform.anchorMin = Vector2.zero; barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            barFill.rectTransform.offsetMin = barFill.rectTransform.offsetMax = Vector2.zero;

            RectTransform grid = Group(right, "Missions", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -150f), new Vector2(1180f, 400f));
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(284f, 124f); gl.spacing = new Vector2(14f, 14f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 4;
            Image mCard = Img(grid, "MissionTemplate", "RoundRect", UiPalette.Surface, raycast: true);
            var mButton = mCard.gameObject.AddComponent<Button>();
            mButton.transition = Selectable.Transition.None;
            mCard.gameObject.AddComponent<UIButtonFX>();
            TMP_Text mNum = Label(mCard.rectTransform, "Number", "MISSION 1", false, 16f, UiPalette.TextSecondary, TextAlignmentOptions.TopLeft, 5f);
            Place(mNum.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(180f, 24f));
            TMP_Text mName = Label(mCard.rectTransform, "Name", "FIRST BAY", true, 26f, UiPalette.TextPrimary, TextAlignmentOptions.TopLeft, 1f);
            Place(mName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -36f), new Vector2(256f, 34f));
            TMP_Text mType = Label(mCard.rectTransform, "Type", "STANDARD", false, 15f, UiPalette.TextSecondary, TextAlignmentOptions.BottomLeft, 3f);
            Place(mType.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 12f), new Vector2(256f, 22f));
            for (int s = 0; s < 3; s++)
            {
                Image st = Img(mCard.rectTransform, "Star" + s, "Star", UiPalette.Outline, sliced: false);
                Place(st.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f - (2 - s) * 26f, -12f), new Vector2(24f, 24f));
            }
            Image mLock = Img(mCard.rectTransform, "Lock", "Lock", UiPalette.TextSecondary, sliced: false);
            Place(mLock.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 14f), new Vector2(28f, 28f));

            // Mission details
            Image detBg = Img(right, "Details", "RoundRect", UiPalette.Surface.WithAlpha(0.9f));
            Place(detBg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1180f, 250f));
            var detGroup = detBg.gameObject.AddComponent<CanvasGroup>();
            TMP_Text dName = Label(detBg.rectTransform, "Name", "", true, 40f, UiPalette.TextPrimary, TextAlignmentOptions.TopLeft, 2f);
            Place(dName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -20f), new Vector2(760f, 48f));
            TMP_Text dMeta = Label(detBg.rectTransform, "Meta", "", false, 20f, UiPalette.Lime, TextAlignmentOptions.TopLeft, 4f);
            Place(dMeta.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -70f), new Vector2(760f, 28f));
            TMP_Text dBrief = Label(detBg.rectTransform, "Briefing", "", false, 20f, UiPalette.TextSecondary, TextAlignmentOptions.TopLeft, 0f);
            dBrief.textWrappingMode = TextWrappingModes.Normal;
            Place(dBrief.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -104f), new Vector2(780f, 90f));
            TMP_Text dBest = Label(detBg.rectTransform, "Best", "", true, 20f, UiPalette.TextPrimary, TextAlignmentOptions.BottomLeft, 3f);
            Place(dBest.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 18f), new Vector2(780f, 30f));
            Button play = PillButton(detBg.rectTransform, "PlayButton", "PLAY", true, new Vector2(850f, 70f), new Vector2(300f, 110f), 44f);
            // Difficulty picker above PLAY (Easy ×0.75 coins, Medium as designed, Hard ×1.5).
            var diffButtons = new Button[3];
            string[] diffNames = { "EASY", "MEDIUM", "HARD" };
            for (int i = 0; i < 3; i++)
                diffButtons[i] = PillButton(detBg.rectTransform, "Difficulty" + diffNames[i], diffNames[i], true, new Vector2(850f + i * 102f, 190f), new Vector2(96f, 46f), 18f);

            // Reward pop-up
            Image popBg = Img(safe, "RewardPopup", "RoundRect", UiPalette.Surface);
            Place(popBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 320f));
            var popGroup = popBg.gameObject.AddComponent<CanvasGroup>();
            TMP_Text popKicker = Label(popBg.rectTransform, "Kicker", "CAREER REWARD", true, 24f, UiPalette.Lime, TextAlignmentOptions.Center, 10f);
            Place(popKicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(760f, 34f));
            TMP_Text popText = Label(popBg.rectTransform, "Text", "", true, 34f, UiPalette.Gold, TextAlignmentOptions.Center, 2f);
            Place(popText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(760f, 140f));
            Button popOk = PillButton(popBg.rectTransform, "OkButton", "GREAT", true, new Vector2(260f, 28f), new Vector2(300f, 84f), 34f);

            MakeEventSystem();
            var host = new GameObject("Career");
            var ctrl = host.AddComponent<CareerController>();
            Wire(ctrl, "career", AssetDatabase.LoadAssetAtPath<CareerCatalog>(CareerBuilder.CatalogPath));
            Wire(ctrl, "backButton", back);
            Wire(ctrl, "progressLabel", progress);
            Wire(ctrl, "coins", Object.FindAnyObjectByType<CoinCounter>());
            Wire(ctrl, "chapterList", chapters);
            Wire(ctrl, "chapterTemplate", chapterButton);
            Wire(ctrl, "chapterTitle", chTitle);
            Wire(ctrl, "chapterSubtitle", chSub);
            Wire(ctrl, "chapterRewards", chRewards);
            Wire(ctrl, "chapterBar", barTrack.rectTransform);
            Wire(ctrl, "chapterBarFill", barFill);
            Wire(ctrl, "missionGrid", grid);
            Wire(ctrl, "missionTemplate", mButton);
            Wire(ctrl, "details", detGroup);
            Wire(ctrl, "detailName", dName);
            Wire(ctrl, "detailMeta", dMeta);
            Wire(ctrl, "detailBriefing", dBrief);
            Wire(ctrl, "detailBest", dBest);
            Wire(ctrl, "playButton", play);
            Wire(ctrl, "playLabel", play.GetComponentInChildren<TMP_Text>());
            WireArray(ctrl, "difficultyButtons", diffButtons);
            Wire(ctrl, "rewardPopup", popGroup);
            Wire(ctrl, "rewardText", popText);
            Wire(ctrl, "rewardOk", popOk);
            Wire(host.AddComponent<BackNavigation>(), "targetScene", SceneNames.MainMenu);
            EditorSceneManager.SaveScene(scene, ProjectBootstrapper.ScenePathOf(SceneNames.Career));
        }
    }
}
