using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Levels;
using UltimateCarParking.Parking;
using UltimateCarParking.Progression;
using UltimateCarParking.UI;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Gameplay HUD, touch controls, pause / completed / failed panels and the shared settings modal.
    ///
    /// Hierarchy (Gameplay scene):
    ///   GameplayCanvas (Canvas, CanvasScaler 1920x1080 match height, GraphicRaycaster)
    ///   ├─ Flash                 full-screen vignette, ScreenFlash (no raycast)
    ///   ├─ SafeArea              SafeAreaFitter
    ///   │  ├─ Hud                (hidden while paused / on results)
    ///   │  │  ├─ PauseButton     top-left circle
    ///   │  │  ├─ LevelBlock      top-left: LEVEL nn + theme
    ///   │  │  ├─ TimerPill       top-centre
    ///   │  │  ├─ Guide           top-centre under the timer: status text + hold bar
    ///   │  │  ├─ GearPill, HitsPill  top-right
    ///   │  │  └─ Controls        bottom: Left / Right (bottom-left), Handbrake / Brake-Reverse / Gas (bottom-right)
    ///   │  └─ Panels             Pause, Complete, Fail, Settings (centred modals over a dim backdrop)
    ///   └─ (EventSystem)
    /// </summary>
    public static partial class UiSceneBuilder
    {
        public static void BuildGameplayUi(LevelManager manager, ParkingValidator validator, ProgressionRecorder progression,
            LevelLoader loader, UltimateCarParking.CameraSystem.VehicleCamera camera)
        {
            RectTransform root = MakeCanvas("GameplayCanvas", out Canvas canvas);
            canvas.sortingOrder = 10;
            Image flashImg = Img(root, "Flash", "Vignette", Color.clear, sliced: false);
            Fill(flashImg.rectTransform, 0, 0, 0, 0);
            var flash = flashImg.gameObject.AddComponent<ScreenFlash>();
            RectTransform safe = SafeArea(root);

            // ---------------- HUD
            RectTransform hud = NewRect(safe, "Hud");
            Fill(hud, 0, 0, 0, 0);
            Button pause = CircleButton(hud, "PauseButton", "Pause", new Vector2(0f, 1f), new Vector2(84f, -78f));

            RectTransform levelBlock = Group(hud, "LevelBlock", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -38f), new Vector2(420f, 84f));
            TMP_Text levelLabel = Label(levelBlock, "Level", "LEVEL 01", true, 44f, UiPalette.TextPrimary, TextAlignmentOptions.TopLeft, 3f);
            Fill(levelLabel.rectTransform, 0, 0, 0, 34);
            TMP_Text levelName = Label(levelBlock, "Theme", "CITY LOT", false, 20f, UiPalette.TextSecondary, TextAlignmentOptions.BottomLeft, 5f);
            Fill(levelName.rectTransform, 2, 0, 50, 0);

            Image timerPill = Img(hud, "TimerPill", "Pill", UiPalette.Surface.WithAlpha(0.88f));
            Place(timerPill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(230f, 84f));
            TMP_Text timer = Label(timerPill.rectTransform, "Timer", "1:30", true, 56f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 2f);
            Fill(timer.rectTransform, 0, 0, 0, 0);

            Image guide = Img(hud, "Guide", "Pill", UiPalette.Surface.WithAlpha(0.82f));
            Place(guide.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(600f, 62f));
            var guideGroup = guide.gameObject.AddComponent<CanvasGroup>();
            Image guideDot = Img(guide.rectTransform, "Dot", "Circle", UiPalette.Lime, sliced: false);
            Place(guideDot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 2f), new Vector2(16f, 16f));
            TMP_Text guideText = Label(guide.rectTransform, "Text", "DRIVE TO THE BAY", false, 26f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 4f);
            Fill(guideText.rectTransform, 50, 30, 0, 6);
            Image holdTrack = Img(guide.rectTransform, "HoldTrack", "Pill", UiPalette.Outline);
            Place(holdTrack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(520f, 5f));
            Image holdFill = Img(holdTrack.rectTransform, "HoldFill", "Pill", UiPalette.Lime);
            holdFill.rectTransform.anchorMin = Vector2.zero;
            holdFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            holdFill.rectTransform.offsetMin = holdFill.rectTransform.offsetMax = Vector2.zero;

            Image hitsPill = Img(hud, "HitsPill", "Pill", UiPalette.Surface.WithAlpha(0.88f));
            Place(hitsPill.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -36f), new Vector2(210f, 84f));
            TMP_Text hitsCaption = Label(hitsPill.rectTransform, "Caption", "HITS", false, 20f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 5f);
            Fill(hitsCaption.rectTransform, 30, 100, 0, 0);
            TMP_Text hits = Label(hitsPill.rectTransform, "Count", "0/3", true, 44f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineRight);
            Fill(hits.rectTransform, 90, 30, 0, 0);

            // VIEW toggle (chase / top-down) under the hits pill.
            Image viewPill = Img(hud, "ViewButton", "Pill", UiPalette.Surface.WithAlpha(0.88f), raycast: true);
            Place(viewPill.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -134f), new Vector2(210f, 64f));
            var viewButton = viewPill.gameObject.AddComponent<Button>();
            viewButton.transition = Selectable.Transition.None;
            viewPill.gameObject.AddComponent<UIButtonFX>();
            TMP_Text viewLabel = Label(viewPill.rectTransform, "Label", "VIEW: CHASE", true, 22f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 3f);
            Fill(viewLabel.rectTransform, 8, 8, 0, 0);

            // Backup camera panel (top centre, under the guide pill), shown while reversing.
            Image rearFrame = Img(hud, "RearCamera", null, new Color(0.03f, 0.035f, 0.04f, 0.92f));
            Place(rearFrame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(540f, 318f));
            var rearGroup = rearFrame.gameObject.AddComponent<CanvasGroup>();
            rearFrame.gameObject.AddComponent<RectMask2D>();   // guide lines that run off the image are clipped to the frame
            var rearImage = NewRect(rearFrame.rectTransform, "Image").gameObject.AddComponent<RawImage>();
            Fill(rearImage.rectTransform, 6, 6, 6, 6);
            rearImage.raycastTarget = false;
            RectTransform guidesRect = NewRect(rearFrame.rectTransform, "Guides");
            guidesRect.gameObject.AddComponent<CanvasRenderer>();
            var rearGuides = guidesRect.gameObject.AddComponent<RearGuideLines>();
            Fill(rearGuides.rectTransform, 6, 6, 6, 6);
            rearGuides.raycastTarget = false;
            TMP_Text rearKicker = Label(rearFrame.rectTransform, "Kicker", "REAR CAMERA", true, 18f, UiPalette.TextPrimary, TextAlignmentOptions.TopLeft, 4f);
            Place(rearKicker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(300f, 28f));
            Image distPill = Img(rearFrame.rectTransform, "DistancePill", "Pill", UiPalette.Lime);
            Place(distPill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(150f, 44f));
            TMP_Text distLabel = Label(distPill.rectTransform, "Distance", "CLEAR", true, 26f, UiPalette.TextOnLime, TextAlignmentOptions.Center, 2f);
            Fill(distLabel.rectTransform, 0, 0, 0, 0);
            var rearCam = rearFrame.gameObject.AddComponent<UltimateCarParking.CameraSystem.RearViewCamera>();
            var rearView = rearFrame.gameObject.AddComponent<RearCameraView>();
            Wire(rearView, "rearCamera", rearCam);
            Wire(rearView, "group", rearGroup);
            Wire(rearView, "image", rearImage);
            Wire(rearView, "guides", rearGuides);
            Wire(rearView, "distanceLabel", distLabel);
            Wire(rearView, "distancePill", distPill);

            Image gearPill = Img(hud, "GearPill", "Circle", UiPalette.Surface.WithAlpha(0.88f), sliced: false);
            Place(gearPill.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-270f, -36f), new Vector2(84f, 84f));
            TMP_Text gear = Label(gearPill.rectTransform, "Gear", "D", true, 46f, UiPalette.Lime, TextAlignmentOptions.Center);
            Fill(gear.rectTransform, 0, 0, 0, 0);

            // ---------------- mission widgets: objective checklist, gauge, toast, start banner
            RectTransform objList = Group(hud, "Objectives", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -140f), new Vector2(560f, 200f));
            Image rowBg = Img(objList, "RowTemplate", "Pill", UiPalette.Surface.WithAlpha(0.72f));
            RectTransform rowT = rowBg.rectTransform;
            Place(rowT, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(540f, 46f));
            Image rowDot = Img(rowT, "Dot", "Circle", UiPalette.TextSecondary, sliced: false);
            Place(rowDot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26f, 0f), new Vector2(14f, 14f));
            TMP_Text rowText = Label(rowT, "Text", "PARK IN THE BAY", false, 22f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, 3f);
            Fill(rowText.rectTransform, 46, 14, 0, 0);
            rowT.gameObject.SetActive(false);

            Image gaugeBg = Img(hud, "MissionGauge", "Pill", UiPalette.Surface.WithAlpha(0.85f));
            Place(gaugeBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -208f), new Vector2(520f, 66f));
            var gaugeGroup = gaugeBg.gameObject.AddComponent<CanvasGroup>();
            gaugeGroup.alpha = 0f;
            TMP_Text gaugeCaption = Label(gaugeBg.rectTransform, "Caption", "REAR SENSOR", false, 18f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 4f);
            Fill(gaugeCaption.rectTransform, 28, 160, 4, 16);
            TMP_Text gaugeValue = Label(gaugeBg.rectTransform, "Value", "1.2 m", true, 32f, UiPalette.Lime, TextAlignmentOptions.MidlineRight, 1f);
            Fill(gaugeValue.rectTransform, 300, 28, 2, 14);
            Image gaugeTrack = Img(gaugeBg.rectTransform, "Track", "Pill", UiPalette.Outline);
            Place(gaugeTrack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(464f, 6f));
            Image gaugeFill = Img(gaugeTrack.rectTransform, "Fill", "Pill", UiPalette.Lime);
            gaugeFill.rectTransform.anchorMin = Vector2.zero;
            gaugeFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            gaugeFill.rectTransform.offsetMin = gaugeFill.rectTransform.offsetMax = Vector2.zero;

            Image toastBg = Img(hud, "Toast", "Pill", UiPalette.Surface.WithAlpha(0.9f));
            Place(toastBg.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 84f));
            var toastGroup = toastBg.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            TMP_Text toastText = Label(toastBg.rectTransform, "Text", "KERB STRIKE", true, 40f, UiPalette.Danger, TextAlignmentOptions.Center, 3f);
            Fill(toastText.rectTransform, 20, 20, 0, 0);

            Image bannerBg = Img(hud, "Banner", "RoundRect", UiPalette.Surface.WithAlpha(0.92f));
            Place(bannerBg.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 230f));
            var bannerGroup = bannerBg.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            Image bannerBar = Img(bannerBg.rectTransform, "Accent", "Pill", UiPalette.Lime);
            Place(bannerBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(90f, 6f));
            TMP_Text bannerKicker = Label(bannerBg.rectTransform, "Kicker", "REVERSE PARKING", true, 44f, UiPalette.Lime, TextAlignmentOptions.Center, 4f);
            Place(bannerKicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(920f, 60f));
            TMP_Text bannerText = Label(bannerBg.rectTransform, "Text", "Back into the bay.", false, 28f, UiPalette.TextPrimary, TextAlignmentOptions.Top, 0f);
            Place(bannerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(900f, 100f));
            bannerText.textWrappingMode = TextWrappingModes.Normal;

            // ---------------- touch controls
            RectTransform controls = NewRect(hud, "Controls");
            Fill(controls, 0, 0, 0, 0);
            HoldButton left = HoldControl(controls, "SteerLeft", "Circle", new Vector2(0f, 0f), new Vector2(170f, 170f), new Vector2(200f, 200f), icon: "ChevronLeft", label: null);
            HoldButton right = HoldControl(controls, "SteerRight", "Circle", new Vector2(0f, 0f), new Vector2(410f, 170f), new Vector2(200f, 200f), icon: "ChevronRight", label: null);
            HoldButton gas = HoldControl(controls, "Gas", "RoundRect", new Vector2(1f, 0f), new Vector2(-160f, 200f), new Vector2(190f, 300f), icon: null, label: "GAS");
            HoldButton brake = HoldControl(controls, "BrakeReverse", "RoundRect", new Vector2(1f, 0f), new Vector2(-390f, 150f), new Vector2(200f, 200f), icon: null, label: "BRAKE\n<size=60%>REVERSE</size>");
            HoldButton handbrake = HoldControl(controls, "Handbrake", "Pill", new Vector2(1f, 0f), new Vector2(-390f, 330f), new Vector2(200f, 74f), icon: null, label: "<size=80%>HANDBRAKE</size>");
            var mobile = controls.gameObject.AddComponent<MobileControls>();
            Wire(mobile, "levelManager", manager);
            Wire(mobile, "left", left);
            Wire(mobile, "right", right);
            Wire(mobile, "gas", gas);
            Wire(mobile, "brakeReverse", brake);
            Wire(mobile, "handbrake", handbrake);

            var hudComp = hud.gameObject.AddComponent<GameplayHud>();
            Wire(hudComp, "levelManager", manager);
            Wire(hudComp, "validator", validator);
            Wire(hudComp, "levelLabel", levelLabel);
            Wire(hudComp, "levelName", levelName);
            Wire(hudComp, "timerLabel", timer);
            Wire(hudComp, "collisionLabel", hits);
            Wire(hudComp, "collisionPill", hitsPill.rectTransform);
            Wire(hudComp, "gearLabel", gear);
            Wire(hudComp, "guideGroup", guideGroup);
            Wire(hudComp, "guideLabel", guideText);
            Wire(hudComp, "guideIcon", guideDot);
            Wire(hudComp, "holdFill", holdFill.rectTransform);
            Wire(hudComp, "flash", flash);
            Wire(hudComp, "mission", manager.GetComponent<UltimateCarParking.Missions.MissionRunner>());
            Wire(hudComp, "objectiveList", objList);
            Wire(hudComp, "objectiveRowTemplate", rowT);
            Wire(hudComp, "gaugeGroup", gaugeGroup);
            Wire(hudComp, "gaugeCaption", gaugeCaption);
            Wire(hudComp, "gaugeValue", gaugeValue);
            Wire(hudComp, "gaugeFill", gaugeFill.rectTransform);
            Wire(hudComp, "gaugeFillImage", gaugeFill);
            Wire(hudComp, "toastGroup", toastGroup);
            Wire(hudComp, "toastLabel", toastText);
            Wire(hudComp, "bannerGroup", bannerGroup);
            Wire(hudComp, "bannerKicker", bannerKicker);
            Wire(hudComp, "bannerText", bannerText);
            Wire(hudComp, "vehicleCamera", camera);
            Wire(hudComp, "viewButton", viewButton);
            Wire(hudComp, "viewLabel", viewLabel);

            // ---------------- panels
            RectTransform panels = NewRect(safe, "Panels");
            Fill(panels, 0, 0, 0, 0);

            CanvasGroup pausePanel = Modal(panels, "PausePanel", new Vector2(700f, 640f), "PAUSED", null, out RectTransform pauseBody);
            Button resume = PillButton(pauseBody, "Resume", "RESUME", true, new Vector2(70f, 400f), new Vector2(560f, 100f), 44f);
            Button pRestart = PillButton(pauseBody, "Restart", "RESTART", false, new Vector2(70f, 284f), new Vector2(560f, 88f), 36f);
            Button pSettings = PillButton(pauseBody, "Settings", "SETTINGS", false, new Vector2(70f, 180f), new Vector2(560f, 88f), 36f);
            Button pLevels = PillButton(pauseBody, "Levels", "LEVELS", false, new Vector2(70f, 76f), new Vector2(560f, 88f), 36f);

            CanvasGroup completePanel = Modal(panels, "CompletePanel", new Vector2(820f, 660f), "PARKED", "LEVEL COMPLETE", out RectTransform cBody);
            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                stars[i] = Img(cBody, $"Star{i}", "Star", UiPalette.Outline, sliced: false);
                float size = i == 1 ? 130f : 104f;
                Place(stars[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 150f, i == 1 ? -210f : -225f), new Vector2(size, size));
            }
            TMP_Text cTime = StatColumn(cBody, "Time", "TIME", -230f);
            TMP_Text cHits = StatColumn(cBody, "Hits", "HITS", 0f);
            TMP_Text cCoins = StatColumn(cBody, "Coins", "COINS", 230f);
            cCoins.color = UiPalette.Gold;
            TMP_Text badge = Label(cBody, "Badge", "NEW BEST", true, 26f, UiPalette.Lime, TextAlignmentOptions.Center, 10f);
            Place(badge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(400f, 36f));
            Button cLevels = PillButton(cBody, "Levels", "LEVELS", false, new Vector2(50f, 40f), new Vector2(220f, 92f), 32f);
            Button cRetry = PillButton(cBody, "Retry", "RETRY", false, new Vector2(300f, 40f), new Vector2(220f, 92f), 32f);
            Button cNext = PillButton(cBody, "Next", "NEXT", true, new Vector2(550f, 40f), new Vector2(220f, 92f), 38f);

            CanvasGroup failPanel = Modal(panels, "FailPanel", new Vector2(700f, 520f), "FAILED", "LEVEL FAILED", out RectTransform fBody);
            TMP_Text reason = Label(fBody, "Reason", "TIME'S UP", true, 60f, UiPalette.Danger, TextAlignmentOptions.Center, 3f);
            Place(reason.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(640f, 80f));
            Button fRetry = PillButton(fBody, "Retry", "RETRY", true, new Vector2(70f, 150f), new Vector2(560f, 100f), 44f);
            Button fLevels = PillButton(fBody, "Levels", "LEVELS", false, new Vector2(70f, 40f), new Vector2(560f, 88f), 36f);

            SettingsPanel settings = BuildSettingsPanel(panels);

            var ui = root.gameObject.AddComponent<GameplayUiController>();
            Wire(ui, "levelManager", manager);
            Wire(ui, "progression", progression);
            Wire(ui, "controls", mobile);
            Wire(ui, "flash", flash);
            Wire(ui, "hudRoot", hud.gameObject);
            Wire(ui, "pauseButton", pause);
            Wire(ui, "pausePanel", pausePanel);
            Wire(ui, "resumeButton", resume);
            Wire(ui, "pauseRestartButton", pRestart);
            Wire(ui, "pauseSettingsButton", pSettings);
            Wire(ui, "pauseLevelsButton", pLevels);
            Wire(ui, "settingsPanel", settings);
            Wire(ui, "completePanel", completePanel);
            Wire(ui, "completeTitle", completePanel.transform.Find("Body/Title").GetComponent<TMP_Text>());
            WireArray(ui, "completeStars", stars);
            Wire(ui, "completeTime", cTime);
            Wire(ui, "completeCollisions", cHits);
            Wire(ui, "completeCoins", cCoins);
            Wire(ui, "completeBadge", badge);
            Wire(ui, "nextButton", cNext);
            Wire(ui, "completeRetryButton", cRetry);
            Wire(ui, "completeLevelsButton", cLevels);
            Wire(ui, "failPanel", failPanel);
            Wire(ui, "failReason", reason);
            Wire(ui, "failRetryButton", fRetry);
            Wire(ui, "failLevelsButton", fLevels);

            var guideGo = new GameObject("ParkingGuide");
            var guideComp = guideGo.AddComponent<ParkingGuide>();
            Wire(guideComp, "levelManager", manager);
            Wire(guideComp, "loader", loader);
            Wire(guideComp, "chevronMaterial", ProjectBootstrapper.Mat("ShowroomRing"));

            MakeEventSystem();
        }

        /// <summary>Centred modal: dim full-screen backdrop + panel with kicker and title; returns the panel body.</summary>
        private static CanvasGroup Modal(RectTransform parent, string name, Vector2 size, string title, string kicker, out RectTransform body)
        {
            RectTransform root = NewRect(parent, name);
            Fill(root, 0, 0, 0, 0);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            Image dim = Img(root, "Backdrop", null, UiPalette.Background.WithAlpha(0.72f), raycast: true);
            Fill(dim.rectTransform, -400, -400, -400, -400);
            Image panel = Img(root, "Body", "RoundRect", UiPalette.Surface, raycast: true);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            body = panel.rectTransform;
            Image edge = Img(body, "Edge", "RoundRectOutline", UiPalette.Outline);
            Fill(edge.rectTransform, 0, 0, 0, 0);
            Image accent = Img(body, "Accent", "Pill", UiPalette.Lime);
            Place(accent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(120f, 6f));
            if (kicker != null)
            {
                TMP_Text k = Label(body, "Kicker", kicker, false, 22f, UiPalette.Lime, TextAlignmentOptions.Center, 10f);
                Place(k.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -52f), new Vector2(600f, 32f));
            }
            TMP_Text t = Label(body, "Title", title, true, 76f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 6f);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, kicker != null ? -106f : -80f), new Vector2(700f, 90f));
            root.gameObject.SetActive(false);
            return group;
        }

        private static TMP_Text StatColumn(RectTransform parent, string name, string caption, float x)
        {
            TMP_Text cap = Label(parent, name + "Caption", caption, false, 20f, UiPalette.TextSecondary, TextAlignmentOptions.Center, 8f);
            Place(cap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(x, 290f), new Vector2(200f, 30f));
            TMP_Text val = Label(parent, name + "Value", "-", true, 52f, UiPalette.TextPrimary, TextAlignmentOptions.Center);
            Place(val.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(x, 240f), new Vector2(220f, 60f));
            return val;
        }

        private static HoldButton HoldControl(RectTransform parent, string name, string sprite, Vector2 anchor, Vector2 pos, Vector2 size, string icon, string label)
        {
            Image bg = Img(parent, name, sprite, new Color(1f, 1f, 1f, 0.16f), sliced: sprite != "Circle", raycast: true);
            Place(bg.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, size);
            Image ring = Img(bg.rectTransform, "Ring", sprite == "Circle" ? null : sprite + "Outline", new Color(1f, 1f, 1f, 0.35f));
            if (sprite == "Circle") Object.DestroyImmediate(ring.gameObject);
            else Fill(ring.rectTransform, 0, 0, 0, 0);
            if (icon != null)
            {
                Image ic = Img(bg.rectTransform, "Icon", icon, UiPalette.TextPrimary, sliced: false);
                Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.42f);
            }
            if (label != null)
            {
                TMP_Text t = Label(bg.rectTransform, "Label", label, true, 40f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 4f);
                Fill(t.rectTransform, 0, 0, 0, 0);
            }
            var hold = bg.gameObject.AddComponent<HoldButton>();
            Wire(hold, "target", bg);
            return hold;
        }

        /// <summary>Music / Sound / Steering sliders + DONE. Shared by the main menu and the pause menu.</summary>
        private static SettingsPanel BuildSettingsPanel(RectTransform parent)
        {
            CanvasGroup group = Modal(parent, "SettingsPanel", new Vector2(820f, 600f), "SETTINGS", null, out RectTransform body);
            (Slider s, TMP_Text v) Row(string caption, float y)
            {
                TMP_Text cap = Label(body, caption + "Caption", caption, false, 24f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 6f);
                Place(cap.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(70f, y), new Vector2(240f, 40f));
                Slider slider = MakeSlider(body, caption + "Slider", new Vector2(300f, y), new Vector2(330f, 40f));
                TMP_Text val = Label(body, caption + "Value", "100%", true, 34f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineRight);
                Place(val.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(650f, y), new Vector2(110f, 44f));
                return (slider, val);
            }
            var music = Row("MUSIC", -210f);
            var sound = Row("SOUND", -300f);
            var steer = Row("STEERING", -390f);
            Button done = PillButton(body, "Done", "DONE", true, new Vector2(130f, 40f), new Vector2(560f, 96f), 42f);
            var panel = group.gameObject.AddComponent<SettingsPanel>();
            Wire(panel, "group", group);
            Wire(panel, "music", music.s);
            Wire(panel, "sound", sound.s);
            Wire(panel, "sensitivity", steer.s);
            Wire(panel, "musicValue", music.v);
            Wire(panel, "soundValue", sound.v);
            Wire(panel, "sensitivityValue", steer.v);
            Wire(panel, "closeButton", done);
            group.gameObject.SetActive(true);   // SettingsPanel hides itself in Awake (so it is wired while active)
            return panel;
        }

        private static Slider MakeSlider(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            RectTransform root = NewRect(parent, name);
            Place(root, new Vector2(0f, 1f), new Vector2(0f, 0.5f), pos, size);
            Image track = Img(root, "Background", "Pill", UiPalette.Outline);
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0f, 10f);
            RectTransform fillArea = NewRect(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-20f, 10f);
            Image fill = Img(fillArea, "Fill", "Pill", UiPalette.Lime);
            fill.rectTransform.sizeDelta = new Vector2(10f, 0f);
            RectTransform handleArea = NewRect(root, "Handle Slide Area");
            Fill(handleArea, 10, 10, 0, 0);
            Image handle = Img(handleArea, "Handle", "Circle", UiPalette.TextPrimary, sliced: false, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(40f, 40f);
            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.transition = Selectable.Transition.None;
            Image hit = root.gameObject.AddComponent<Image>();   // makes the whole row draggable
            hit.color = Color.clear;
            return slider;
        }
    }
}
