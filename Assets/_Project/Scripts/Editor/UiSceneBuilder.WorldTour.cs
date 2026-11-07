using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UltimateCarParking.Core;
using UltimateCarParking.UI;
using UltimateCarParking.WorldTour;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// World Tour scene: full-height interactive map on the left (pins, region chips, legend, gesture hint) and the
    /// selected country's card on the right. Reference 1920x1080 matched on height, so the card keeps its size and
    /// the map simply gets wider on 20:9 phones and narrower on 4:3 tablets.
    /// </summary>
    public static partial class UiSceneBuilder
    {
        public const string CountryMarkerPrefabPath = "Assets/_Project/Prefabs/UI/CountryMarker.prefab";
        private const float PanelWidth = 640f, PanelRight = 40f, PanelTop = 190f, PanelBottom = 40f;

        public static void BuildWorldTourScene()
        {
            BuildCountryMarkerPrefab();
            BuildWorldTour();
        }

        private static void BuildCountryMarkerPrefab()
        {
            var go = new GameObject("CountryMarker", typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = Vector2.zero;

            Image dotHalo = Img(rt, "DotHalo", "Circle", new Color(0f, 0f, 0f, 0.55f), sliced: false);
            Place(dotHalo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            Image dot = Img(rt, "Dot", "Circle", UiPalette.Lime, sliced: false);
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            Image stem = Img(rt, "Stem", "RoundRect", UiPalette.TextPrimary.WithAlpha(0.9f), sliced: false);
            Place(stem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(4f, 30f));

            RectTransform pin = NewRect(rt, "Pin");
            Place(pin, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(104f, 80f));
            Image shadow = Img(pin, "Shadow", "RoundRect", new Color(0f, 0f, 0f, 0.45f));
            Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Vector2(108f, 84f));
            Image frame = Img(pin, "Frame", "RoundRect", UiPalette.TextPrimary, raycast: true);
            Fill(frame.rectTransform, 0, 0, 0, 0);
            var button = frame.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            frame.gameObject.AddComponent<UIButtonFX>();
            Image flag = Img(pin, "Flag", null, Color.white);
            Fill(flag.rectTransform, 7, 7, 7, 7);
            Image lockOverlay = Img(pin, "LockOverlay", null, new Color(0.03f, 0.035f, 0.04f, 0.62f));
            Fill(lockOverlay.rectTransform, 7, 7, 7, 7);
            Image lockIcon = Img(lockOverlay.rectTransform, "Lock", "Lock", UiPalette.TextPrimary, sliced: false);
            Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            Image check = Img(pin, "Check", "Circle", UiPalette.Lime, sliced: false);
            Place(check.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-4f, -4f), new Vector2(34f, 34f));
            Image tick = Img(check.rectTransform, "Tick", "Check", UiPalette.TextOnLime, sliced: false);
            Place(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            Image ring = Img(pin, "SelectionRing", "RoundRectOutline", UiPalette.Lime);
            Fill(ring.rectTransform, -9, -9, -9, -9);
            ring.enabled = false;

            Image tag = Img(rt, "NameTag", "Pill", new Color(0.04f, 0.05f, 0.06f, 0.9f));
            Place(tag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(270f, 40f));
            TMP_Text tagLabel = Label(tag.rectTransform, "Name", "01  COUNTRY", true, 22f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 2f);
            Fill(tagLabel.rectTransform, 10, 10, 0, 0);
            tagLabel.enableAutoSizing = true; tagLabel.fontSizeMin = 14f; tagLabel.fontSizeMax = 22f;
            tag.gameObject.SetActive(false);

            var marker = go.AddComponent<CountryMarker>();
            Wire(marker, "button", button);
            Wire(marker, "pin", pin);
            Wire(marker, "frame", frame);
            Wire(marker, "flag", flag);
            Wire(marker, "lockOverlay", lockOverlay.gameObject);
            Wire(marker, "checkBadge", check.gameObject);
            Wire(marker, "selectionRing", ring);
            Wire(marker, "dot", dot);
            Wire(marker, "nameTag", tag.gameObject);
            Wire(marker, "nameLabel", tagLabel);
            PrefabUtility.SaveAsPrefabAsset(go, CountryMarkerPrefabPath);
            Object.DestroyImmediate(go);
        }

        private static void BuildWorldTour()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera2D();
            RectTransform root = MakeCanvas("WorldTourCanvas", out _);
            Image ocean = Img(root, "Ocean", "Gradient", Color.white);
            Fill(ocean.rectTransform, 0, 0, 0, 0);
            Image glow = Img(root, "Glow", "Glow", UiPalette.Lime.WithAlpha(0.03f), sliced: false);
            Place(glow.rectTransform, new Vector2(0.35f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800f, 1800f));

            // Map viewport (left of the card). The viewport image catches drags / pinches / wheel.
            RectTransform viewport = NewRect(root, "MapViewport");
            Fill(viewport, 0, PanelWidth + PanelRight + 24f, 0, 0);
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = NewRect(viewport, "MapContent");
            Place(content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f * WorldMapProjection.Aspect, 1000f));
            var map = content.gameObject.AddComponent<WorldMapGraphic>();
            map.raycastTarget = false;
            Wire(map, "mapAsset", AssetDatabase.LoadAssetAtPath<TextAsset>(WorldTourBuilder.MapPath));
            RectTransform markers = NewRect(content, "Markers");
            Fill(markers, 0, 0, 0, 0);
            var view = viewport.gameObject.AddComponent<WorldMapView>();
            Wire(view, "viewport", viewport);
            Wire(view, "content", content);
            Wire(view, "markerLayer", markers);

            // Header shade so the title reads over the map.
            Image shade = Img(root, "TopShade", "SideFade", UiPalette.Background.WithAlpha(0.92f));
            Place(shade.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(300f, 6000f));
            shade.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);

            RectTransform safe = SafeArea(root);
            Button back = CircleButton(safe, "BackButton", "ChevronLeft", new Vector2(0f, 1f), new Vector2(110f, -100f));
            TMP_Text title = Label(safe, "Title", "WORLD TOUR", true, 66f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, 4f);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(190f, -86f), new Vector2(700f, 80f));
            TMP_Text progress = Label(safe, "Progress", "0 / 30   COUNTRIES COMPLETED", false, 21f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 3f);
            Place(progress.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(192f, -134f), new Vector2(520f, 30f));
            Image track = Img(safe, "TourTrack", "Pill", UiPalette.Outline);
            Place(track.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(560f, -134f), new Vector2(220f, 8f));
            Image fill = Img(track.rectTransform, "Fill", "Pill", UiPalette.Lime);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            TMP_Text stars = StarPill(safe, new Vector2(-370f, -100f));
            CoinPill(safe);

            // Region chips.
            float[] widths = { 124f, 112f, 140f, 164f, 290f, 154f };
            var chipButtons = new Button[widths.Length];
            var chipBgs = new Image[widths.Length];
            var chipLabels = new TMP_Text[widths.Length];
            float x = 96f;
            for (int i = 0; i < widths.Length; i++)
            {
                Image bg = Img(safe, "Region_" + i, "Pill", new Color(0.07f, 0.08f, 0.09f, 0.82f), raycast: true);
                Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(x, -212f), new Vector2(widths[i], 58f));
                chipButtons[i] = bg.gameObject.AddComponent<Button>();
                chipButtons[i].transition = Selectable.Transition.None;
                bg.gameObject.AddComponent<UIButtonFX>();
                chipBgs[i] = bg;
                chipLabels[i] = Label(bg.rectTransform, "Label", WorldTourController.RegionNames[i], true, 22f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 2.5f);
                Fill(chipLabels[i].rectTransform, 0, 0, 0, 0);
                x += widths[i] + 12f;
            }

            // Legend + gesture hint (bottom-left of the map).
            RectTransform legend = Group(safe, "Legend", new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(96f, 104f), new Vector2(620f, 30f));
            (string text, Color color)[] keys = { ("UNLOCKED", UiPalette.Hex(0x55761E)), ("COMPLETED", UiPalette.Hex(0x7FB424)), ("LOCKED", UiPalette.Hex(0x3C4652)), ("SELECTED", UiPalette.Lime) };
            float lx = 0f;
            foreach (var k in keys)
            {
                Image sw = Img(legend, "Key_" + k.text, "RoundRect", k.color, sliced: false);
                Place(sw.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(lx, 0f), new Vector2(22f, 16f));
                TMP_Text kt = Label(legend, "KeyLabel_" + k.text, k.text, false, 18f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 3f);
                Place(kt.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(lx + 30f, 0f), new Vector2(140f, 26f));
                lx += 150f;
            }
            TMP_Text hint = Label(safe, "GestureHint", "DRAG TO MOVE   ·   PINCH TO ZOOM   ·   TAP A FLAG", false, 20f, UiPalette.TextPrimary.WithAlpha(0.75f), TextAlignmentOptions.MidlineLeft, 4f);
            Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(96f, 60f), new Vector2(760f, 30f));
            var hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            hintGroup.blocksRaycasts = false;

            // Country card.
            RectTransform panel = NewRect(safe, "CountryCard");
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.offsetMin = new Vector2(-PanelRight - PanelWidth, PanelBottom);
            panel.offsetMax = new Vector2(-PanelRight, -PanelTop);
            Image card = panel.gameObject.AddComponent<Image>();
            card.sprite = UiAssets.Sprite("RoundRect"); card.type = Image.Type.Sliced; card.color = UiPalette.Surface.WithAlpha(0.96f);
            card.raycastTarget = true;   // the card swallows taps so they don't reach the map
            Image cardLine = Img(panel, "Outline", "RoundRectOutline", UiPalette.Outline);
            Fill(cardLine.rectTransform, 0, 0, 0, 0);
            var detailGroup = panel.gameObject.AddComponent<CanvasGroup>();
            RectTransform body = NewRect(panel, "Content");
            Fill(body, 32, 32, 0, 0);

            TMP_Text kicker = Label(body, "Kicker", "ASIA   ·   STOP 01 / 30", false, 19f, UiPalette.Lime, TextAlignmentOptions.MidlineLeft, 5f);
            Place(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -40f), new Vector2(400f, 28f));
            Image chip = Img(body, "StatusChip", "Pill", UiPalette.LimeDim);
            Place(chip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(0f, -40f), new Vector2(176f, 40f));
            TMP_Text status = Label(chip.rectTransform, "Status", "UNLOCKED", true, 20f, UiPalette.Lime, TextAlignmentOptions.Center, 3f);
            Fill(status.rectTransform, 0, 0, 0, 0);

            Image flagFrame = Img(body, "FlagFrame", "RoundRect", UiPalette.TextPrimary);
            Place(flagFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -70f), new Vector2(128f, 96f));
            Image flagImg = Img(flagFrame.rectTransform, "Flag", null, Color.white);
            Fill(flagImg.rectTransform, 5, 5, 5, 5);
            TMP_Text name = Label(body, "CountryName", "COUNTRY", true, 56f, UiPalette.TextPrimary, TextAlignmentOptions.BottomLeft, 2f);
            Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -66f), new Vector2(426f, 64f));
            name.enableAutoSizing = true; name.fontSizeMin = 32f; name.fontSizeMax = 56f;
            TMP_Text arena = Label(body, "Arena", "CITY  ·  ENVIRONMENT", false, 20f, UiPalette.TextPrimary, TextAlignmentOptions.TopLeft, 2.5f);
            Place(arena.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(152f, -136f), new Vector2(424f, 30f));
            arena.enableAutoSizing = true; arena.fontSizeMin = 14f; arena.fontSizeMax = 20f;

            Image preview = Img(body, "Preview", null, Color.white);
            Place(preview.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(576f, 324f));
            preview.preserveAspect = false;
            Image previewFrame = Img(preview.rectTransform, "Frame", "RoundRectOutline", UiPalette.Outline);
            Fill(previewFrame.rectTransform, -2, -2, -2, -2);
            Image missionPill = Img(preview.rectTransform, "MissionChip", "Pill", new Color(0.04f, 0.05f, 0.06f, 0.86f));
            Place(missionPill.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 14f), new Vector2(280f, 40f));
            TMP_Text mission = Label(missionPill.rectTransform, "Label", "STANDARD PARKING", true, 19f, UiPalette.Lime, TextAlignmentOptions.Center, 3f);
            Fill(mission.rectTransform, 12, 12, 0, 0);
            Image bonusPill = Img(preview.rectTransform, "BonusChip", "Pill", new Color(0.04f, 0.05f, 0.06f, 0.86f));
            Place(bonusPill.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(548f, 40f));
            TMP_Text bonus = Label(bonusPill.rectTransform, "Label", "COUNTRY BONUS", true, 18f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 2f);
            Fill(bonus.rectTransform, 12, 12, 0, 0);
            bonus.enableAutoSizing = true; bonus.fontSizeMin = 12f; bonus.fontSizeMax = 18f;
            Image previewLock = Img(preview.rectTransform, "LockOverlay", null, new Color(0.03f, 0.035f, 0.04f, 0.55f));
            Fill(previewLock.rectTransform, 0, 0, 0, 0);
            Image bigLock = Img(previewLock.rectTransform, "Lock", "Lock", UiPalette.TextPrimary, sliced: false);
            Place(bigLock.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(72f, 72f));
            TMP_Text lockHint = Label(previewLock.rectTransform, "LockHint", "FINISH INDIA TO UNLOCK", true, 26f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 3f);
            Place(lockHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(540f, 40f));
            lockHint.enableAutoSizing = true; lockHint.fontSizeMin = 16f; lockHint.fontSizeMax = 26f;

            TMP_Text desc = Label(body, "Description", "Description", false, 21f, UiPalette.TextSecondary, TextAlignmentOptions.TopLeft);
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.overflowMode = TextOverflowModes.Ellipsis;
            desc.lineSpacing = 4f;
            Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -522f), new Vector2(576f, 62f));
            desc.lineSpacing = 0f;

            // Mission picker: one chip per mission (hidden for single-mission countries).
            const int chipCount = 4;
            var missionButtons = new Button[chipCount];
            var missionBgs = new Image[chipCount];
            var missionLabels = new TMP_Text[chipCount];
            var missionLocks = new GameObject[chipCount];
            float chipW = (576f - (chipCount - 1) * 12f) / chipCount;
            for (int i = 0; i < chipCount; i++)
            {
                Image bg = Img(body, "Mission" + (i + 1), "RoundRect", UiPalette.SurfaceRaised, raycast: true);
                Place(bg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(i * (chipW + 12f), 206f), new Vector2(chipW, 52f));
                missionButtons[i] = bg.gameObject.AddComponent<Button>();
                missionButtons[i].transition = Selectable.Transition.None;
                bg.gameObject.AddComponent<UIButtonFX>();
                missionBgs[i] = bg;
                missionLabels[i] = Label(bg.rectTransform, "Label", $"M{i + 1}", true, 22f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 2f);
                Fill(missionLabels[i].rectTransform, 0, 0, 0, 0);
                Image lk = Img(bg.rectTransform, "Lock", "Lock", UiPalette.TextSecondary, sliced: false);
                Place(lk.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(24f, 24f));
                missionLocks[i] = lk.gameObject;
                bg.gameObject.SetActive(false);
            }

            TMP_Text missions = Stat(body, "Missions", "MISSIONS", 0);
            TMP_Text starsStat = Stat(body, "Stars", "STARS", 1);
            TMP_Text time = Stat(body, "Time", "TIME LIMIT", 2);

            Button play = PillButton(body, "PlayButton", "PLAY", filled: true, Vector2.zero, new Vector2(576f, 96f), 40f);
            Place((RectTransform)play.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(576f, 92f));
            Image playLock = Img(play.transform, "Lock", "Lock", UiPalette.TextSecondary, sliced: false);
            Place(playLock.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(40f, 40f));
            MakeEventSystem();

            var host = new GameObject("WorldTour");
            var ctrl = host.AddComponent<WorldTourController>();
            Wire(ctrl, "tour", AssetDatabase.LoadAssetAtPath<CountryCatalog>(WorldTourBuilder.CatalogPath));
            Wire(ctrl, "mapView", view);
            Wire(ctrl, "mapGraphic", map);
            Wire(ctrl, "markerLayer", markers);
            Wire(ctrl, "markerPrefab", AssetDatabase.LoadAssetAtPath<CountryMarker>(CountryMarkerPrefabPath));
            Wire(ctrl, "backButton", back);
            Wire(ctrl, "progressLabel", progress);
            Wire(ctrl, "starTotal", stars);
            Wire(ctrl, "tourBarFill", fill.rectTransform);
            Wire(ctrl, "gestureHint", hintGroup);
            WireArray(ctrl, "regionButtons", chipButtons);
            WireArray(ctrl, "regionBackgrounds", chipBgs);
            WireArray(ctrl, "regionLabels", chipLabels);
            Wire(ctrl, "detailGroup", detailGroup);
            Wire(ctrl, "detailContent", body);
            Wire(ctrl, "kicker", kicker);
            Wire(ctrl, "flagImage", flagImg);
            Wire(ctrl, "countryName", name);
            Wire(ctrl, "arenaLabel", arena);
            Wire(ctrl, "previewImage", preview);
            Wire(ctrl, "previewLock", previewLock.gameObject);
            Wire(ctrl, "missionChip", mission);
            Wire(ctrl, "description", desc);
            Wire(ctrl, "missionsValue", missions);
            Wire(ctrl, "starsValue", starsStat);
            Wire(ctrl, "timeValue", time);
            Wire(ctrl, "statusChip", chip);
            Wire(ctrl, "statusLabel", status);
            Wire(ctrl, "lockHint", lockHint);
            Wire(ctrl, "bonusLabel", bonus);
            WireArray(ctrl, "missionButtons", missionButtons);
            WireArray(ctrl, "missionBackgrounds", missionBgs);
            WireArray(ctrl, "missionLabels", missionLabels);
            WireArray(ctrl, "missionLocks", missionLocks);
            Wire(ctrl, "playButton", play);
            Wire(ctrl, "playBackground", play.GetComponent<Image>());
            Wire(ctrl, "playLabel", play.GetComponentInChildren<TMP_Text>());
            Wire(ctrl, "playLock", playLock.gameObject);
            Wire(host.AddComponent<BackNavigation>(), "targetScene", SceneNames.MainMenu);

            EditorSceneManager.SaveScene(scene, ProjectBootstrapper.ScenePathOf(SceneNames.WorldTour));
        }

        private static TMP_Text Stat(RectTransform parent, string name, string caption, int column)
        {
            const float w = 180f, gap = 18f;
            Image box = Img(parent, name + "Stat", "RoundRect", UiPalette.SurfaceRaised);
            Place(box.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(column * (w + gap), 128f), new Vector2(w, 70f));
            TMP_Text value = Label(box.rectTransform, "Value", "0/1", true, 30f, UiPalette.TextPrimary, TextAlignmentOptions.Center, 1f);
            Fill(value.rectTransform, 0, 0, 4, 26);
            TMP_Text cap = Label(box.rectTransform, "Caption", caption, false, 14f, UiPalette.TextSecondary, TextAlignmentOptions.Center, 4f);
            Fill(cap.rectTransform, 0, 0, 46, 4);
            return value;
        }
    }
}
