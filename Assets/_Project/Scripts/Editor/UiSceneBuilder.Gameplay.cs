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

            Image gearPill = Img(hud, "GearPill", "Circle", UiPalette.Surface.WithAlpha(0.88f), sliced: false);
            Place(gearPill.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-270f, -36f), new Vector2(84f, 84f));
            TMP_Text gear = Label(gearPill.rectTransform, "Gear", "D", true, 46f, UiPalette.Lime, TextAlignmentOptions.Center);
            Fill(gear.rectTransform, 0, 0, 0, 0);

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
            Wire(hudComp, "vehicleCamera", camera);

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
