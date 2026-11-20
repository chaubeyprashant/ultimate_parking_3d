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
    ///   │  │  ├─ HitsPill, ViewButton, ZoomButton  top-right (collisions, camera)
    ///   │  │  ├─ Speedometer     bottom-centre: speed arc, km/h, gear letter
    ///   │  │  └─ Controls        ControlLayout: SteerZone (wheel / buttons / tilt) and PedalZone (gas, brake,
    ///   │  │                     handbrake, P-R-D selector or classic brake/reverse); sides swap for left-handed play
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

            RectTransform levelBlock = Group(hud, "LevelBlock", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -38f), new Vector2(320f, 84f));
            // Dark backing so the level name and mission type stay readable over bright sky (bumper / cockpit views).
            Image levelPlate = Img(levelBlock, "Plate", "RoundRect", UiPalette.Background.WithAlpha(0.45f));
            Fill(levelPlate.rectTransform, -16, -12, -6, -8);
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

            // Camera controls under the hits pill: VIEW (chase / top-down) and ZOOM (near / mid / far).
            Button viewButton = HudChip(hud, "ViewButton", "CameraIcon", "CHASE", new Vector2(-170f, -134f), new Vector2(170f, 64f), out TMP_Text viewLabel);
            Button zoomButton = HudChip(hud, "ZoomButton", "ZoomIcon", "MID", new Vector2(-40f, -134f), new Vector2(122f, 64f), out TMP_Text zoomLabel);
            // Car controls: lights, indicators, hazards (row 1); engine and horn (row 2).
            RectTransform carControls = Group(hud, "VehicleButtons", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -212f), new Vector2(330f, 124f));
            Button rightInd = HudChip(carControls, "IndicatorRight", "ChevronRight", "", new Vector2(0f, 0f), new Vector2(60f, 56f), out _);
            Button hazard = HudChip(carControls, "Hazard", null, "HAZARD", new Vector2(-68f, 0f), new Vector2(98f, 56f), out _);
            Button leftInd = HudChip(carControls, "IndicatorLeft", "ChevronLeft", "", new Vector2(-174f, 0f), new Vector2(60f, 56f), out _);
            Button lightsBtn = HudChip(carControls, "Headlights", null, "LIGHTS", new Vector2(-242f, 0f), new Vector2(88f, 56f), out _);
            Button engineBtn = HudChip(carControls, "Engine", null, "ENGINE ON", new Vector2(-124f, -66f), new Vector2(206f, 56f), out _);
            Image hornBg = Img(carControls, "Horn", "Pill", UiPalette.Surface.WithAlpha(0.8f), raycast: true);
            Place(hornBg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -66f), new Vector2(116f, 56f));
            TMP_Text hornLabel = Label(hornBg.rectTransform, "Label", "HORN", true, 22f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 4f);
            Fill(hornLabel.rectTransform, 0, 0, 0, 0);
            var hornHold = hornBg.gameObject.AddComponent<HoldButton>();
            Wire(hornHold, "target", hornBg);
            var vb = carControls.gameObject.AddComponent<VehicleButtons>();
            Wire(vb, "levelManager", manager);
            Wire(vb, "lightsButton", lightsBtn); Wire(vb, "leftButton", leftInd); Wire(vb, "hazardButton", hazard);
            Wire(vb, "rightButton", rightInd); Wire(vb, "engineButton", engineBtn); Wire(vb, "horn", hornHold);

            // Backup camera panel (top centre, under the guide pill), shown while reversing.
            Image rearFrame = Img(hud, "RearCamera", null, new Color(0.03f, 0.035f, 0.04f, 0.92f));
            Place(rearFrame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -284f), new Vector2(540f, 318f));
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
            Wire(rearView, "frame", rearFrame);


            // ---------------- mission widgets: objective checklist, gauge, toast, start banner
            Image objPlate = Img(hud, "ObjectiveHeaderPlate", "Pill", UiPalette.Background.WithAlpha(0.45f));
            Place(objPlate.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -134f), new Vector2(440f, 38f));
            TMP_Text objProgress = Label(hud, "ObjectiveProgress", "OBJECTIVES  0/3", true, 20f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 6f);
            Place(objProgress.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -138f), new Vector2(420f, 30f));
            objProgress.raycastTarget = true;
            var objToggle = objProgress.gameObject.AddComponent<Button>();
            objToggle.transition = Selectable.Transition.None;
            objProgress.gameObject.SetActive(false);
            RectTransform objList = Group(hud, "Objectives", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -172f), new Vector2(440f, 200f));
            Image rowBg = Img(objList, "RowTemplate", "Pill", UiPalette.Surface.WithAlpha(0.72f));
            RectTransform rowT = rowBg.rectTransform;
            Place(rowT, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(420f, 46f));
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

            // ---------------- touch controls (laid out at runtime by ControlLayout from the player's settings)
            RectTransform controls = NewRect(hud, "Controls");
            Fill(controls, 0, 0, 0, 0);
            var controlsGroup = controls.gameObject.AddComponent<CanvasGroup>();
            RectTransform steerZone = Group(controls, "SteerZone", Vector2.zero, Vector2.zero, new Vector2(36f, 24f), new Vector2(560f, 440f));
            RectTransform pedalZone = Group(controls, "PedalZone", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 24f), new Vector2(660f, 440f));

            // Steering: wheel (default), Left/Right buttons, or tilt (indicator + straighten button).
            SteeringWheel wheel = BuildWheel(steerZone, new Vector2(250f, 215f), 400f);
            HoldButton left = HoldControl(steerZone, "SteerLeft", "Circle", Vector2.zero, new Vector2(140f, 150f), new Vector2(200f, 200f), icon: "ChevronLeft", label: null);
            HoldButton right = HoldControl(steerZone, "SteerRight", "Circle", Vector2.zero, new Vector2(380f, 150f), new Vector2(200f, 200f), icon: "ChevronRight", label: null);
            RectTransform tiltRoot = Group(steerZone, "Tilt", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(170f, 220f), new Vector2(220f, 300f));
            Image tiltDisc = Img(tiltRoot, "Disc", "Circle", UiPalette.Surface.WithAlpha(0.55f), sliced: false);
            Place(tiltDisc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(190f, 190f));
            Image tiltIcon = Img(tiltDisc.rectTransform, "Indicator", "SteeringWheel", UiPalette.TextPrimary.WithAlpha(0.85f), sliced: false);
            Place(tiltIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
            TMP_Text tiltCaption = Label(tiltRoot, "Caption", "TILT", true, 18f, UiPalette.TextSecondary, TextAlignmentOptions.Center, 8f);
            Place(tiltCaption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(200f, 26f));
            Button tiltCentre = HudChip(tiltRoot, "Straighten", null, "STRAIGHTEN", new Vector2(0f, 0f), new Vector2(210f, 62f), out _, anchor: new Vector2(0.5f, 0f), pivot: new Vector2(0.5f, 0f));

            // Pedals: gas, foot brake, handbrake and the P / R / D selector (Gears); Brake/Reverse (Classic).
            HoldButton gas = Pedal(pedalZone, "Gas", "GAS", new Vector2(-110f, 210f), new Vector2(170f, 300f),
                new Color(0.90f, 0.92f, 0.94f, 0.92f), new Color(0.71f, 1f, 0.18f, 1f));
            HoldButton brake = Pedal(pedalZone, "Brake", "BRAKE", new Vector2(-320f, 150f), new Vector2(210f, 200f),
                new Color(0.90f, 0.92f, 0.94f, 0.92f), new Color(1f, 0.42f, 0.45f, 1f));
            HoldButton brakeReverse = Pedal(pedalZone, "BrakeReverse", "BRAKE\n<size=60%>REVERSE</size>", new Vector2(-320f, 150f), new Vector2(210f, 200f),
                new Color(0.90f, 0.92f, 0.94f, 0.92f), new Color(1f, 0.42f, 0.45f, 1f));
            HoldButton handbrake = HoldControl(pedalZone, "Handbrake", "Pill", new Vector2(1f, 0f), new Vector2(-320f, 318f), new Vector2(210f, 70f), icon: null, label: "<size=70%>HANDBRAKE</size>",
                idle: UiPalette.Surface.WithAlpha(0.78f), held: new Color(1f, 0.78f, 0.24f, 0.9f));
            RectTransform gearPlate = Group(pedalZone, "GearSelector", new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-540f, 232f), new Vector2(108f, 300f));
            Image plate = Img(gearPlate, "Plate", "RoundRect", UiPalette.Surface.WithAlpha(0.72f));
            Fill(plate.rectTransform, 0, 0, 0, 0);
            Button gearP = GearButton(gearPlate, "GearP", "P", 96f);
            Button gearR = GearButton(gearPlate, "GearR", "R", 0f);
            Button gearD = GearButton(gearPlate, "GearD", "D", -96f);

            var mobile = controls.gameObject.AddComponent<MobileControls>();
            Wire(mobile, "levelManager", manager);
            Wire(mobile, "left", left);
            Wire(mobile, "right", right);
            Wire(mobile, "wheel", wheel);
            Wire(mobile, "tiltCentreButton", tiltCentre);
            Wire(mobile, "tiltIndicator", tiltIcon.rectTransform);
            Wire(mobile, "gas", gas);
            Wire(mobile, "brake", brake);
            Wire(mobile, "handbrake", handbrake);
            Wire(mobile, "brakeReverse", brakeReverse);
            Wire(mobile, "gearPark", gearP);
            Wire(mobile, "gearReverse", gearR);
            Wire(mobile, "gearDrive", gearD);
            var layout = controls.gameObject.AddComponent<ControlLayout>();
            Wire(layout, "group", controlsGroup);
            Wire(layout, "steerZone", steerZone);
            Wire(layout, "pedalZone", pedalZone);
            WireArray(layout, "buttonsOnly", left.gameObject, right.gameObject);
            WireArray(layout, "wheelOnly", wheel.gameObject);
            WireArray(layout, "tiltOnly", tiltRoot.gameObject);
            WireArray(layout, "gearsOnly", brake.gameObject, gearPlate.gameObject);
            WireArray(layout, "classicOnly", brakeReverse.gameObject);

            // ---------------- speedometer (bottom centre)
            Image speedo = Img(hud, "Speedometer", "RoundRect", UiPalette.Surface.WithAlpha(0.74f));
            Place(speedo.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(300f, 150f));
            Image arcTrack = Img(speedo.rectTransform, "ArcTrack", "Ring", UiPalette.Outline, sliced: false);
            Place(arcTrack.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(80f, 0f), new Vector2(124f, 124f));
            Radial(arcTrack, 0.75f);
            Image arc = Img(speedo.rectTransform, "Arc", "Ring", UiPalette.Lime.WithAlpha(0.9f), sliced: false);
            Place(arc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(80f, 0f), new Vector2(124f, 124f));
            Radial(arc, 0f);
            TMP_Text gear = Label(speedo.rectTransform, "Gear", "D", true, 54f, UiPalette.TextPrimary, TextAlignmentOptions.Center);
            Place(gear.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(80f, 2f), new Vector2(90f, 70f));
            TMP_Text speedValue = Label(speedo.rectTransform, "Speed", "0", true, 64f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineRight);
            Place(speedValue.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 14f), new Vector2(140f, 74f));
            TMP_Text speedUnit = Label(speedo.rectTransform, "Unit", "KM/H", false, 18f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineRight, 8f);
            Place(speedUnit.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, -36f), new Vector2(140f, 26f));
            // ---------------- parking sensors (above the speedometer) and the alignment line
            BuildParkAssist(hud, manager, validator);

            var gauge = speedo.gameObject.AddComponent<SpeedGauge>();
            Wire(gauge, "levelManager", manager);
            Wire(gauge, "controls", mobile);
            Wire(gauge, "arc", arc);
            Wire(gauge, "speedLabel", speedValue);
            Wire(gauge, "gearLabel", gear);

            var hudComp = hud.gameObject.AddComponent<GameplayHud>();
            Wire(hudComp, "levelManager", manager);
            Wire(hudComp, "validator", validator);
            Wire(hudComp, "levelLabel", levelLabel);
            Wire(hudComp, "levelName", levelName);
            Wire(hudComp, "timerLabel", timer);
            Wire(hudComp, "collisionLabel", hits);
            Wire(hudComp, "collisionPill", hitsPill.rectTransform);
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
            Wire(hudComp, "zoomButton", zoomButton);
            Wire(hudComp, "zoomLabel", zoomLabel);
            Wire(hudComp, "controls", mobile);
            Wire(hudComp, "objectiveProgress", objProgress);
            Wire(hudComp, "objectiveToggle", objToggle);
            Wire(hudComp, "rearView", rearView);

            // Dashboard finish: a thin light edge on the main panels (no glow, no gradients).
            foreach (Image panel in new[] { timerPill, hitsPill, guide, speedo, plate })
            {
                Image e = Img(panel.rectTransform, "Edge", panel.sprite != null && panel.sprite.name == "Pill" ? "PillOutline" : "RoundRectOutline", UiPalette.TextPrimary.WithAlpha(0.14f));
                Fill(e.rectTransform, 0, 0, 0, 0);
                e.transform.SetAsFirstSibling();
            }

            // Responsive layout (HUD size, narrow screens, display cut-outs via the safe area).
            var hudLayout = hud.gameObject.AddComponent<HudLayout>();
            Wire(hudLayout, "area", hud);
            WireArray(hudLayout, "cornerWidgets", pause.transform, levelBlock, timerPill.transform, hitsPill.transform, viewButton.transform, zoomButton.transform,
                objProgress.transform, objList, objPlate.transform);
            WireArray(hudLayout, "centreColumn", guide.transform, gaugeBg.transform, rearFrame.transform);
            WireArray(hudLayout, "bottomCentre", speedo.transform, hud.Find("ParkAssist"), hud.Find("AlignHint"));
            Wire(hudLayout, "controls", layout);
            var so2 = new SerializedObject(hudLayout);
            so2.FindProperty("rightReserve").floatValue = 375f;     // corner widgets now include the car-control chips
            var cw = so2.FindProperty("cornerWidgets");
            cw.arraySize++; cw.GetArrayElementAtIndex(cw.arraySize - 1).objectReferenceValue = carControls;
            so2.ApplyModifiedPropertiesWithoutUndo();

            // ---------------- panels
            RectTransform panels = NewRect(safe, "Panels");
            Fill(panels, 0, 0, 0, 0);

            CanvasGroup pausePanel = Modal(panels, "PausePanel", new Vector2(700f, 640f), "PAUSED", null, out RectTransform pauseBody);
            Button resume = PillButton(pauseBody, "Resume", "RESUME", true, new Vector2(70f, 400f), new Vector2(560f, 100f), 44f);
            Button pRestart = PillButton(pauseBody, "Restart", "RESTART", false, new Vector2(70f, 284f), new Vector2(560f, 88f), 36f);
            Button pSettings = PillButton(pauseBody, "Settings", "SETTINGS", false, new Vector2(70f, 180f), new Vector2(560f, 88f), 36f);
            Button pLevels = PillButton(pauseBody, "Levels", "LEVELS", false, new Vector2(70f, 76f), new Vector2(560f, 88f), 36f);

            CanvasGroup completePanel = Modal(panels, "CompletePanel", new Vector2(820f, 760f), "PARKED", "LEVEL COMPLETE", out RectTransform cBody);
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
            Place(badge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 244f), new Vector2(400f, 36f));
            Button cDouble = PillButton(cBody, "DoubleCoins", "WATCH AD: DOUBLE COINS", false, new Vector2(110f, 148f), new Vector2(600f, 80f), 28f);
            cDouble.GetComponentInChildren<TMP_Text>().color = UiPalette.Gold;
            cDouble.gameObject.SetActive(false);
            Button cLevels = PillButton(cBody, "Levels", "LEVELS", false, new Vector2(50f, 40f), new Vector2(220f, 92f), 32f);
            Button cRetry = PillButton(cBody, "Retry", "RETRY", false, new Vector2(300f, 40f), new Vector2(220f, 92f), 32f);
            Button cNext = PillButton(cBody, "Next", "NEXT", true, new Vector2(550f, 40f), new Vector2(220f, 92f), 38f);

            CanvasGroup failPanel = Modal(panels, "FailPanel", new Vector2(700f, 640f), "FAILED", "LEVEL FAILED", out RectTransform fBody);
            TMP_Text reason = Label(fBody, "Reason", "TIME'S UP", true, 60f, UiPalette.Danger, TextAlignmentOptions.Center, 3f);
            Place(reason.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(640f, 80f));
            Button fContinue = PillButton(fBody, "Continue", "WATCH AD: +30 SECONDS", false, new Vector2(70f, 270f), new Vector2(560f, 92f), 30f);
            fContinue.GetComponentInChildren<TMP_Text>().color = UiPalette.Gold;
            fContinue.gameObject.SetActive(false);
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
            Wire(ui, "doubleCoinsButton", cDouble);
            Wire(ui, "continueButton", fContinue);

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
            Place(cap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(x, 380f), new Vector2(200f, 30f));
            TMP_Text val = Label(parent, name + "Value", "-", true, 52f, UiPalette.TextPrimary, TextAlignmentOptions.Center);
            Place(val.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(x, 330f), new Vector2(220f, 60f));
            return val;
        }

        private static HoldButton HoldControl(RectTransform parent, string name, string sprite, Vector2 anchor, Vector2 pos, Vector2 size, string icon, string label,
            Color? idle = null, Color? held = null, Color? labelColor = null)
        {
            Color idleColor = idle ?? new Color(1f, 1f, 1f, 0.16f);
            Image bg = Img(parent, name, sprite, idleColor, sliced: sprite != "Circle", raycast: true);
            Place(bg.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, size);
            Image ring = Img(bg.rectTransform, "Ring", sprite == "Circle" ? null : sprite + "Outline", new Color(0.07f, 0.08f, 0.09f, idle.HasValue ? 0.35f : 0.35f));
            if (sprite == "Circle") Object.DestroyImmediate(ring.gameObject);
            else Fill(ring.rectTransform, 0, 0, 0, 0);
            if (icon != null)
            {
                Image ic = Img(bg.rectTransform, "Icon", icon, UiPalette.TextPrimary, sliced: false);
                Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.42f);
            }
            if (label != null)
            {
                TMP_Text t = Label(bg.rectTransform, "Label", label, true, name == "Gas" ? 56f : 40f, labelColor ?? UiPalette.TextPrimary, TextAlignmentOptions.Center, 4f);
                Fill(t.rectTransform, 0, 0, 0, 0);
            }
            var hold = bg.gameObject.AddComponent<HoldButton>();
            Wire(hold, "target", bg);
            var so = new SerializedObject(hold);
            so.FindProperty("idleColor").colorValue = idleColor;
            so.FindProperty("heldColor").colorValue = held ?? new Color(0.71f, 1f, 0.18f, 0.55f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return hold;
        }

        /// <summary>Music / Sound / Steering sliders + DONE. Shared by the main menu and the pause menu.</summary>
        private static SettingsPanel BuildSettingsPanel(RectTransform parent)
        {
            CanvasGroup group = Modal(parent, "SettingsPanel", new Vector2(820f, 960f), "SETTINGS", null, out RectTransform body);
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
            Button controlsButton = PillButton(body, "ControlsButton", "DRIVING CONTROLS  ›", false, new Vector2(130f, 540f), new Vector2(560f, 76f), 28f);
            TMP_Text accCap = Label(body, "AccountCaption", "ACCOUNT", false, 24f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 6f);
            Place(accCap.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(70f, -720f), new Vector2(200f, 40f));
            TMP_Text accValue = Label(body, "AccountValue", "Guest (not signed in)", true, 26f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineRight);
            Place(accValue.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(280f, -720f), new Vector2(480f, 44f));
            Button gfxButton = PillButton(body, "GraphicsButton", "GRAPHICS: AUTO", false, new Vector2(130f, 370f), new Vector2(560f, 76f), 28f);
            Button fpsButton = PillButton(body, "FrameRateButton", "FRAME RATE: AUTO", false, new Vector2(130f, 280f), new Vector2(560f, 76f), 28f);
            Button accButton = PillButton(body, "AccountButton", "SIGN IN WITH GOOGLE", false, new Vector2(100f, 220f), new Vector2(270f, 76f), 26f);
            Button signOut = PillButton(body, "SignOutButton", "SIGN OUT", false, new Vector2(390f, 220f), new Vector2(270f, 76f), 26f);
            Button done = PillButton(body, "Done", "DONE", true, new Vector2(130f, 30f), new Vector2(560f, 96f), 42f);
            var panel = group.gameObject.AddComponent<SettingsPanel>();
            Wire(panel, "group", group);
            Wire(panel, "music", music.s);
            Wire(panel, "sound", sound.s);
            Wire(panel, "musicValue", music.v);
            Wire(panel, "soundValue", sound.v);
            Wire(panel, "controlsButton", controlsButton);
            Wire(panel, "controlsPanel", BuildControlsPanel(parent));
            Wire(panel, "closeButton", done);
            Wire(panel, "graphicsButton", gfxButton);
            Wire(panel, "graphicsLabel", gfxButton.GetComponentInChildren<TMP_Text>());
            Wire(panel, "frameRateButton", fpsButton);
            Wire(panel, "frameRateLabel", fpsButton.GetComponentInChildren<TMP_Text>());
            Wire(panel, "accountValue", accValue);
            Wire(panel, "accountButton", accButton);
            Wire(panel, "accountButtonLabel", accButton.GetComponentInChildren<TMP_Text>());
            Wire(panel, "signOutButton", signOut);
            Wire(panel, "signOutLabel", signOut.GetComponentInChildren<TMP_Text>());
            group.gameObject.SetActive(true);   // SettingsPanel hides itself in Awake (so it is wired while active)
            return panel;
        }

        /// <summary>Settings → Controls: steering mode, pedals, sensitivity, size, opacity, height, hand, tilt calibration.</summary>
        private static ControlsPanel BuildControlsPanel(RectTransform parent)
        {
            CanvasGroup group = Modal(parent, "ControlsPanel", new Vector2(900f, 1000f), "CONTROLS", null, out RectTransform body);
            (Slider s, TMP_Text v) Row(string caption, float y)
            {
                TMP_Text cap = Label(body, caption + "Caption", caption, false, 24f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 6f);
                Place(cap.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(64f, y), new Vector2(260f, 40f));
                Slider slider = MakeSlider(body, caption + "Slider", new Vector2(330f, y), new Vector2(390f, 40f));
                TMP_Text val = Label(body, caption + "Value", "100%", true, 32f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineRight);
                Place(val.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(730f, y), new Vector2(110f, 44f));
                return (slider, val);
            }
            Button steering = PillButton(body, "SteeringMode", "STEERING: WHEEL", false, new Vector2(56f, 770f), new Vector2(384f, 72f), 24f);
            Button pedals = PillButton(body, "PedalMode", "PEDALS: GEARS P-R-D", false, new Vector2(460f, 770f), new Vector2(384f, 72f), 24f);
            Button hand = PillButton(body, "Handedness", "LAYOUT: RIGHT-HANDED", false, new Vector2(56f, 682f), new Vector2(384f, 72f), 24f);
            Button reset = PillButton(body, "ResetControls", "RESET CONTROLS", false, new Vector2(460f, 682f), new Vector2(384f, 72f), 24f);
            var sens = Row("SENSITIVITY", -380f);
            var size = Row("CONTROL SIZE", -446f);
            var opacity = Row("OPACITY", -512f);
            var height = Row("HEIGHT", -578f);
            var hudRow = Row("HUD SIZE", -644f);
            Button assist = PillButton(body, "AssistMode", "ASSIST: STANDARD", false, new Vector2(56f, 256f), new Vector2(384f, 64f), 24f);
            Button beeps = PillButton(body, "AssistSounds", "SENSOR BEEPS: ON", false, new Vector2(460f, 256f), new Vector2(384f, 64f), 24f);
            Button calibrate = PillButton(body, "CalibrateTilt", "SET STRAIGHT AHEAD (TILT)", false, new Vector2(56f, 176f), new Vector2(788f, 66f), 24f);
            TMP_Text hint = Label(body, "Hint", "", false, 20f, UiPalette.TextSecondary, TextAlignmentOptions.Center, 0f);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 136f), new Vector2(800f, 34f));
            Button done = PillButton(body, "Done", "DONE", true, new Vector2(170f, 28f), new Vector2(560f, 96f), 42f);
            var panel = group.gameObject.AddComponent<ControlsPanel>();
            Wire(panel, "group", group);
            Wire(panel, "steeringButton", steering);
            Wire(panel, "steeringLabel", steering.GetComponentInChildren<TMP_Text>());
            Wire(panel, "pedalsButton", pedals);
            Wire(panel, "pedalsLabel", pedals.GetComponentInChildren<TMP_Text>());
            Wire(panel, "handButton", hand);
            Wire(panel, "handLabel", hand.GetComponentInChildren<TMP_Text>());
            Wire(panel, "sensitivity", sens.s);
            Wire(panel, "sensitivityValue", sens.v);
            Wire(panel, "size", size.s);
            Wire(panel, "sizeValue", size.v);
            Wire(panel, "opacity", opacity.s);
            Wire(panel, "opacityValue", opacity.v);
            Wire(panel, "height", height.s);
            Wire(panel, "heightValue", height.v);
            Wire(panel, "hudSize", hudRow.s);
            Wire(panel, "hudSizeValue", hudRow.v);
            Wire(panel, "calibrateButton", calibrate);
            Wire(panel, "calibrateLabel", calibrate.GetComponentInChildren<TMP_Text>());
            Wire(panel, "hint", hint);
            Wire(panel, "resetButton", reset);
            Wire(panel, "assistButton", assist);
            Wire(panel, "assistLabel", assist.GetComponentInChildren<TMP_Text>());
            Wire(panel, "beepsButton", beeps);
            Wire(panel, "beepsLabel", beeps.GetComponentInChildren<TMP_Text>());
            Wire(panel, "closeButton", done);
            group.gameObject.SetActive(true);   // ControlsPanel hides itself in Awake (so it is wired while active)
            return panel;
        }

        /// <summary>Parking sensor widget: top-view car with three bars at each bumper, nearest distance, beep source, alignment line.</summary>
        private static void BuildParkAssist(RectTransform hud, LevelManager manager, ParkingValidator validator)
        {
            RectTransform root = Group(hud, "ParkAssist", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 184f), new Vector2(260f, 176f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Image plate = Img(root, "Plate", "RoundRect", UiPalette.Surface.WithAlpha(0.62f));
            Fill(plate.rectTransform, 0, 0, 0, 0);
            Image car = Img(root, "Car", "RoundRect", UiPalette.TextPrimary.WithAlpha(0.85f));
            Place(car.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70f, 0f), new Vector2(46f, 86f));
            var front = new Image[3];
            var rear = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float x = 70f + (i - 1) * 34f;
                front[i] = Img(root, "Front" + i, "Pill", Color.white.WithAlpha(0.16f));
                Place(front[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 62f - Mathf.Abs(i - 1) * 6f), new Vector2(30f, 12f));
                rear[i] = Img(root, "Rear" + i, "Pill", Color.white.WithAlpha(0.16f));
                Place(rear[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -62f + Mathf.Abs(i - 1) * 6f), new Vector2(30f, 12f));
            }
            TMP_Text caption = Label(root, "Caption", "SENSORS", false, 16f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 6f);
            Place(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(136f, 34f), new Vector2(120f, 24f));
            TMP_Text dist = Label(root, "Distance", "", true, 40f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            Place(dist.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(134f, -8f), new Vector2(120f, 50f));
            TMP_Text align = Label(hud, "AlignHint", "", true, 24f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 3f);
            Place(align.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 368f), new Vector2(620f, 34f));
            align.gameObject.SetActive(false);
            var beep = root.gameObject.AddComponent<AudioSource>();
            beep.playOnAwake = false;
            beep.spatialBlend = 0f;
            var view = root.gameObject.AddComponent<ParkAssistView>();
            Wire(view, "levelManager", manager);
            Wire(view, "validator", validator);
            Wire(view, "group", group);
            WireArray(view, "frontBars", front);
            WireArray(view, "rearBars", rear);
            Wire(view, "distanceLabel", dist);
            Wire(view, "alignLabel", align);
            Wire(view, "beepSource", beep);
        }

        /// <summary>Small translucent HUD button with an optional icon and a label (camera view, zoom, straighten).</summary>
        private static Button HudChip(RectTransform parent, string name, string icon, string text, Vector2 pos, Vector2 size, out TMP_Text label,
            Vector2? anchor = null, Vector2? pivot = null)
        {
            Image bg = Img(parent, name, "Pill", UiPalette.Surface.WithAlpha(0.8f), raycast: true);
            Place(bg.rectTransform, anchor ?? new Vector2(1f, 1f), pivot ?? new Vector2(1f, 1f), pos, size);
            var b = bg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            bg.gameObject.AddComponent<UIButtonFX>();
            Image edge = Img(bg.rectTransform, "Edge", "PillOutline", UiPalette.TextPrimary.WithAlpha(0.14f));
            Fill(edge.rectTransform, 0, 0, 0, 0);
            float left = 18f;
            if (icon != null)
            {
                Image ic = Img(bg.rectTransform, "Icon", icon, UiPalette.TextPrimary.WithAlpha(0.9f), sliced: false);
                Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(30f, 30f));
                left = 54f;
            }
            label = Label(bg.rectTransform, "Label", text, true, 22f, UiPalette.TextPrimary, icon != null ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center, 4f);
            Fill(label.rectTransform, icon != null ? left : 8f, 10f, 0, 0);
            return b;
        }

        /// <summary>Steering wheel: a hit disc, a rotating rotor (rim, spokes, hub, top marker) and a held glow.</summary>
        private static SteeringWheel BuildWheel(RectTransform parent, Vector2 centre, float diameter)
        {
            Image disc = Img(parent, "Wheel", "Circle", UiPalette.Surface.WithAlpha(0.35f), sliced: false, raycast: true);
            Place(disc.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), centre, new Vector2(diameter, diameter));
            Image glow = Img(disc.rectTransform, "Glow", "Ring", UiPalette.Lime.WithAlpha(0.35f), sliced: false);
            Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter, diameter));
            var glowGroup = glow.gameObject.AddComponent<CanvasGroup>();
            glowGroup.alpha = 0f;
            glowGroup.blocksRaycasts = false;
            RectTransform rotor = NewRect(disc.rectTransform, "Rotor");
            Place(rotor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter * 0.92f, diameter * 0.92f));
            Image rim = Img(rotor, "Rim", "SteeringWheel", new Color(0.93f, 0.95f, 0.96f, 0.92f), sliced: false);
            Fill(rim.rectTransform, 0, 0, 0, 0);
            Image marker = Img(rotor, "Marker", "Pill", UiPalette.Lime, sliced: false);
            Place(marker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(16f, 38f));
            var wheel = disc.gameObject.AddComponent<SteeringWheel>();
            Wire(wheel, "rotor", rotor);
            Wire(wheel, "heldGlow", glowGroup);
            return wheel;
        }

        /// <summary>Ribbed pedal with a label; idle and held tints.</summary>
        private static HoldButton Pedal(RectTransform parent, string name, string text, Vector2 pos, Vector2 size, Color idle, Color held)
        {
            Image bg = Img(parent, name, "Pedal", idle, sliced: false, raycast: true);
            Place(bg.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), pos, size);
            TMP_Text t = Label(bg.rectTransform, "Label", text, true, 38f, UiPalette.Background, TextAlignmentOptions.Bottom, 4f);
            Fill(t.rectTransform, 0, 0, 0, 22);
            var hold = bg.gameObject.AddComponent<HoldButton>();
            Wire(hold, "target", bg);
            var so = new SerializedObject(hold);
            so.FindProperty("idleColor").colorValue = idle;
            so.FindProperty("heldColor").colorValue = held;
            so.ApplyModifiedPropertiesWithoutUndo();
            return hold;
        }

        private static Button GearButton(RectTransform plate, string name, string letter, float y)
        {
            Image bg = Img(plate, name, "RoundRect", UiPalette.Surface.WithAlpha(0.55f), raycast: true);
            Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(88f, 84f));
            var b = bg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = bg;
            bg.gameObject.AddComponent<UIButtonFX>();
            TMP_Text t = Label(bg.rectTransform, "Label", letter, true, 46f, UiPalette.TextSecondary, TextAlignmentOptions.Center);
            Fill(t.rectTransform, 0, 0, 0, 0);
            return b;
        }

        /// <summary>270° gauge arc: radial fill starting at the lower left, running clockwise.</summary>
        private static void Radial(Image img, float amount)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Bottom;
            img.fillClockwise = true;
            img.fillAmount = amount;
            img.rectTransform.localEulerAngles = new Vector3(0f, 0f, -45f);
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
