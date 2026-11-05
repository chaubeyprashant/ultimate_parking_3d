using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UltimateCarParking.Cars;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.UI;

namespace UltimateCarParking.EditorTools
{
    /// <summary>
    /// Builds the menu scenes (MainMenu, LevelSelect, Garage) and the LevelCard prefab with uGUI + TextMeshPro.
    /// Landscape reference 1920x1080, matched on height, every element anchored to an edge or the centre so wider
    /// phones (20:9) and tablets (4:3) keep the layout. Content sits inside a SafeAreaFitter.
    /// </summary>
    public static partial class UiSceneBuilder
    {
        public const string LevelCardPrefabPath = "Assets/_Project/Prefabs/UI/LevelCard.prefab";

        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory("Assets/_Project/Prefabs/UI");
            CreateShowroomMaterials();
            BuildLevelCardPrefab();
            BuildMainMenu();
            BuildLevelSelect();
            BuildGarage();
        }

        // ------------------------------------------------------------------ MainMenu

        private static void BuildMainMenu()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CarShowroom showroom = Showroom(new Vector3(-3.7f, 2.3f, -9.4f), new Vector3(-2.15f, 0.5f, 0f));

            RectTransform root = MakeCanvas("MainMenuCanvas", out _);
            Image fade = Img(root, "SideFade", "SideFade", UiPalette.Background);
            Place(fade.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            fade.rectTransform.anchorMax = new Vector2(0f, 1f);
            fade.rectTransform.sizeDelta = new Vector2(1250f, 0f);
            RectTransform safe = SafeArea(root);

            // Title block
            RectTransform title = Group(safe, "TitleGroup", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -110f), new Vector2(1100f, 260f));
            Label(title, "Kicker", "ULTIMATE", true, 40f, UiPalette.Lime, TextAlignmentOptions.TopLeft, 18f).rectTransform.Also(r => Fill(r, 0, 0, 0, 210));
            Label(title, "Title", "CAR PARKING <color=#B6FF2E>3D</color>", true, 124f, UiPalette.TextPrimary, TextAlignmentOptions.TopLeft, 2f).rectTransform.Also(r => Fill(r, 0, 0, 46, 120));
            Image bar = Img(title, "AccentBar", "RoundRect", UiPalette.Lime);
            Place(bar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(4f, 6f), new Vector2(150f, 8f));
            TMP_Text progress = Label(safe, "Progress", "0/30 LEVELS", false, 30f, UiPalette.TextSecondary, TextAlignmentOptions.TopLeft, 3f);
            Place(progress.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(124f, -392f), new Vector2(900f, 44f));

            // Buttons
            RectTransform buttons = Group(safe, "ButtonGroup", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(120f, 110f), new Vector2(560f, 330f));
            Button play = PillButton(buttons, "PlayButton", "PLAY", filled: true, new Vector2(0f, 190f), new Vector2(560f, 116f), 60f);
            Button garage = PillButton(buttons, "GarageButton", "GARAGE", filled: false, new Vector2(0f, 64f), new Vector2(560f, 100f), 44f);
            TMP_Text car = Label(buttons, "CarLabel", "CITY SEDAN", false, 26f, UiPalette.TextSecondary, TextAlignmentOptions.BottomLeft, 4f);
            Place(car.rectTransform, Vector2.zero, Vector2.zero, new Vector2(6f, 0f), new Vector2(560f, 40f));

            CoinPill(safe);
            Button settingsButton = CircleButton(safe, "SettingsButton", "Gear", new Vector2(1f, 1f), new Vector2(-404f, -100f));
            SettingsPanel settingsPanel = BuildSettingsPanel(safe);
            TMP_Text version = Label(safe, "Version", "v0.4", false, 20f, UiPalette.TextSecondary.WithAlpha(0.5f), TextAlignmentOptions.BottomRight, 2f);
            Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 32f), new Vector2(400f, 30f));
            MakeEventSystem();

            var host = new GameObject("MainMenu");
            var ctrl = host.AddComponent<MainMenuController>();
            Wire(ctrl, "cars", AssetDatabase.LoadAssetAtPath<CarCatalog>(CarFactory.CarCatalogPath));
            Wire(ctrl, "levels", AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath));
            Wire(ctrl, "showroom", showroom);
            Wire(ctrl, "playButton", play);
            Wire(ctrl, "garageButton", garage);
            Wire(ctrl, "settingsButton", settingsButton);
            Wire(ctrl, "settingsPanel", settingsPanel);
            Wire(ctrl, "carLabel", car);
            Wire(ctrl, "progressLabel", progress);
            WireArray(ctrl, "introGroups", title.gameObject.AddComponent<CanvasGroup>(), buttons.gameObject.AddComponent<CanvasGroup>());
            host.AddComponent<BackNavigation>();   // empty target = quit on Android back

            EditorSceneManager.SaveScene(scene, ProjectBootstrapper.ScenePathOf(SceneNames.MainMenu));
        }

        // ------------------------------------------------------------------ LevelSelect

        private static void BuildLevelSelect()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera2D();
            RectTransform root = MakeCanvas("LevelSelectCanvas", out _);
            Fill(Img(root, "Background", "Gradient", Color.white).rectTransform, 0, 0, 0, 0);
            Image glow = Img(root, "Glow", "Glow", UiPalette.Lime.WithAlpha(0.035f), sliced: false);
            Place(glow.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(160f, -120f), new Vector2(1400f, 1400f));
            RectTransform safe = SafeArea(root);

            Button back = CircleButton(safe, "BackButton", "ChevronLeft", new Vector2(0f, 1f), new Vector2(110f, -100f));
            TMP_Text title = Label(safe, "Title", "SELECT LEVEL", true, 66f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, 4f);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(190f, -100f), new Vector2(800f, 90f));
            TMP_Text stars = StarPill(safe, new Vector2(-370f, -100f));
            CoinPill(safe);

            // Tabs
            RectTransform tabs = Group(safe, "Tabs", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(1362f, 96f));
            var tabButtons = new Button[3];
            var tabTitles = new TMP_Text[3];
            var tabProgress = new TMP_Text[3];
            var underlines = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform tab = NewRect(tabs, $"Tab{i}");
                Place(tab, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 466f, 0f), new Vector2(430f, 96f));
                Image hit = tab.gameObject.AddComponent<Image>();
                hit.sprite = UiAssets.Sprite("RoundRect"); hit.type = UnityEngine.UI.Image.Type.Sliced; hit.color = UiPalette.Surface;
                tabButtons[i] = tab.gameObject.AddComponent<Button>();
                tabButtons[i].transition = Selectable.Transition.None;
                tab.gameObject.AddComponent<UIButtonFX>();
                tabTitles[i] = Label(tab, "Title", "THEME", true, 38f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 3f);
                Fill(tabTitles[i].rectTransform, 28, 150, 0, 0);
                Image star = Img(tab, "StarIcon", "Star", UiPalette.Lime, sliced: false);
                Place(star.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(28f, 28f));
                tabProgress[i] = Label(tab, "Progress", "0/30", false, 26f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineRight);
                Place(tabProgress[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(110f, 40f));
                underlines[i] = Img(tab, "Underline", "RoundRect", UiPalette.Lime);
                Place(underlines[i].rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(380f, 6f));
            }

            TMP_Text caption = Label(safe, "SectionCaption", "LEVELS 1-10", false, 24f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 6f);
            Place(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-681f, 196f), new Vector2(600f, 36f));

            RectTransform gridHolder = Group(safe, "GridHolder", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(1362f, 448f));
            var gridGroup = gridHolder.gameObject.AddComponent<CanvasGroup>();
            RectTransform grid = NewRect(gridHolder, "Grid");
            Fill(grid, 0, 0, 0, 0);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(250f, 210f);
            layout.spacing = new Vector2(28f, 28f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            layout.childAlignment = TextAnchor.UpperCenter;

            TMP_Text hint = Label(safe, "Hint", "COMPLETE A LEVEL TO UNLOCK THE NEXT", false, 22f, UiPalette.TextSecondary.WithAlpha(0.7f), TextAlignmentOptions.Center, 5f);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(1000f, 34f));
            MakeEventSystem();

            var host = new GameObject("LevelSelect");
            var ctrl = host.AddComponent<LevelSelectController>();
            Wire(ctrl, "catalog", AssetDatabase.LoadAssetAtPath<LevelCatalog>(ProjectBootstrapper.CatalogPath));
            Wire(ctrl, "cardPrefab", AssetDatabase.LoadAssetAtPath<LevelCard>(LevelCardPrefabPath));
            Wire(ctrl, "grid", grid);
            Wire(ctrl, "gridGroup", gridGroup);
            WireArray(ctrl, "tabButtons", tabButtons);
            WireArray(ctrl, "tabTitles", tabTitles);
            WireArray(ctrl, "tabProgress", tabProgress);
            WireArray(ctrl, "tabUnderlines", underlines);
            Wire(ctrl, "backButton", back);
            Wire(ctrl, "starTotal", stars);
            Wire(ctrl, "sectionCaption", caption);
            Wire(host.AddComponent<BackNavigation>(), "targetScene", SceneNames.MainMenu);

            EditorSceneManager.SaveScene(scene, ProjectBootstrapper.ScenePathOf(SceneNames.LevelSelect));
        }

        private static void BuildLevelCardPrefab()
        {
            var go = new GameObject("LevelCard", typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(250f, 210f);
            Image bg = go.AddComponent<Image>();
            bg.sprite = UiAssets.Sprite("RoundRect"); bg.type = UnityEngine.UI.Image.Type.Sliced; bg.color = UiPalette.SurfaceRaised;
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            go.AddComponent<UIButtonFX>();

            Image border = Img(rt, "Border", "RoundRectOutline", UiPalette.Lime);
            Fill(border.rectTransform, 0, 0, 0, 0);
            TMP_Text number = Label(rt, "Number", "01", true, 96f, UiPalette.TextPrimary, TextAlignmentOptions.Center);
            Fill(number.rectTransform, 0, 0, 14, 70);
            RectTransform starRow = NewRect(rt, "Stars");
            Place(starRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(132f, 36f));
            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                stars[i] = Img(starRow, $"Star{i}", "Star", UiPalette.TextSecondary, sliced: false);
                Place(stars[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 44f, 0f), new Vector2(36f, 36f));
            }
            TMP_Text status = Label(rt, "Status", "PLAY", false, 22f, UiPalette.Lime, TextAlignmentOptions.Center, 4f);
            Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(230f, 30f));
            Image lockIcon = Img(rt, "Lock", "Lock", UiPalette.TextSecondary, sliced: false);
            Place(lockIcon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(40f, 40f));

            var card = go.AddComponent<LevelCard>();
            Wire(card, "button", button);
            Wire(card, "background", bg);
            Wire(card, "border", border);
            Wire(card, "numberLabel", number);
            Wire(card, "statusLabel", status);
            WireArray(card, "stars", stars);
            Wire(card, "lockIcon", lockIcon.gameObject);
            Wire(card, "starRow", starRow.gameObject);
            PrefabUtility.SaveAsPrefabAsset(go, LevelCardPrefabPath);
            Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------------ Garage

        private static void BuildGarage()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CarShowroom showroom = Showroom(new Vector3(-3.9f, 2.4f, -9.8f), new Vector3(-2.3f, 0.5f, 0f));

            var garageGo = new GameObject("Garage");
            var manager = garageGo.AddComponent<GarageManager>();
            Wire(manager, "catalog", AssetDatabase.LoadAssetAtPath<CarCatalog>(CarFactory.CarCatalogPath));

            RectTransform root = MakeCanvas("GarageCanvas", out _);
            Image drag = Img(root, "DragArea", null, new Color(0f, 0f, 0f, 0f), raycast: true);
            Fill(drag.rectTransform, 0, 0, 0, 0);
            Wire(drag.gameObject.AddComponent<ShowroomDragArea>(), "showroom", showroom);
            Image fade = Img(root, "SideFade", "SideFade", UiPalette.Background);
            Place(fade.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            fade.rectTransform.anchorMax = new Vector2(0f, 1f);
            fade.rectTransform.sizeDelta = new Vector2(1150f, 0f);
            RectTransform safe = SafeArea(root);

            Button back = CircleButton(safe, "BackButton", "ChevronLeft", new Vector2(0f, 1f), new Vector2(110f, -100f));
            TMP_Text kicker = Label(safe, "Kicker", "GARAGE", true, 34f, UiPalette.Lime, TextAlignmentOptions.MidlineLeft, 14f);
            Place(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(190f, -82f), new Vector2(400f, 44f));
            TMP_Text index = Label(safe, "Index", "01 / 05", false, 24f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 4f);
            Place(index.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(190f, -120f), new Vector2(400f, 34f));
            CoinPill(safe);

            RectTransform info = Group(safe, "Info", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120f, 10f), new Vector2(600f, 660f));
            var infoGroup = info.gameObject.AddComponent<CanvasGroup>();
            TMP_Text name = Label(info, "CarName", "CITY SEDAN", true, 88f, UiPalette.TextPrimary, TextAlignmentOptions.TopLeft, 2f);
            Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(800f, 100f));
            TMP_Text tagline = Label(info, "Tagline", "Balanced", false, 28f, UiPalette.TextSecondary, TextAlignmentOptions.TopLeft);
            tagline.textWrappingMode = TextWrappingModes.Normal;
            Place(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(2f, -104f), new Vector2(580f, 80f));
            StatBar speed = MakeStatBar(info, "TOP SPEED", -200f);
            StatBar accel = MakeStatBar(info, "ACCELERATION", -276f);
            StatBar handling = MakeStatBar(info, "HANDLING", -352f);
            StatBar braking = MakeStatBar(info, "BRAKING", -428f);
            TMP_Text status = Label(info, "Status", "OWNED", false, 26f, UiPalette.Lime, TextAlignmentOptions.BottomLeft, 6f);
            Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(4f, 124f), new Vector2(560f, 36f));

            Button action = PillButton(info, "ActionButton", "SELECT", filled: true, new Vector2(0f, 0f), new Vector2(560f, 104f), 46f);
            Image lockIcon = Img(action.transform, "Lock", "Lock", UiPalette.TextOnLime, sliced: false);
            Place(lockIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(40f, 40f));

            Button prev = CircleButton(safe, "PrevButton", "ChevronLeft", new Vector2(1f, 0f), new Vector2(-250f, 110f));
            Button next = CircleButton(safe, "NextButton", "ChevronRight", new Vector2(1f, 0f), new Vector2(-110f, 110f));
            RectTransform dots = Group(safe, "Dots", new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-560f, 110f), new Vector2(240f, 24f));
            var hl = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 14f; hl.childAlignment = TextAnchor.MiddleCenter; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            Image dot = Img(dots, "DotTemplate", "Circle", UiPalette.Outline, sliced: false);
            dot.rectTransform.sizeDelta = new Vector2(16f, 16f);
            dot.gameObject.AddComponent<LayoutElement>().preferredWidth = 16f;
            TMP_Text dragHint = Label(safe, "DragHint", "DRAG TO ROTATE", false, 20f, UiPalette.TextSecondary.WithAlpha(0.6f), TextAlignmentOptions.Center, 6f);
            Place(dragHint.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-560f, 60f), new Vector2(400f, 30f));
            MakeEventSystem();

            var ctrl = garageGo.AddComponent<GarageController>();
            Wire(ctrl, "garage", manager);
            Wire(ctrl, "showroom", showroom);
            Wire(ctrl, "carName", name);
            Wire(ctrl, "tagline", tagline);
            Wire(ctrl, "indexLabel", index);
            Wire(ctrl, "topSpeed", speed);
            Wire(ctrl, "acceleration", accel);
            Wire(ctrl, "handling", handling);
            Wire(ctrl, "braking", braking);
            Wire(ctrl, "infoGroup", infoGroup);
            Wire(ctrl, "actionButton", action);
            Wire(ctrl, "actionBackground", action.GetComponent<Image>());
            Wire(ctrl, "actionLabel", action.GetComponentInChildren<TMP_Text>());
            Wire(ctrl, "actionLockIcon", lockIcon.gameObject);
            Wire(ctrl, "statusLabel", status);
            Wire(ctrl, "previousButton", prev);
            Wire(ctrl, "nextButton", next);
            Wire(ctrl, "backButton", back);
            Wire(ctrl, "dotsParent", dots);
            Wire(ctrl, "dotTemplate", dot);
            Wire(ctrl, "coins", Object.FindAnyObjectByType<CoinCounter>());
            Wire(garageGo.AddComponent<BackNavigation>(), "targetScene", SceneNames.MainMenu);

            EditorSceneManager.SaveScene(scene, ProjectBootstrapper.ScenePathOf(SceneNames.Garage));
        }

        private static StatBar MakeStatBar(RectTransform parent, string label, float y)
        {
            RectTransform root = NewRect(parent, label.Replace(" ", "") + "Stat");
            Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(560f, 58f));
            TMP_Text name = Label(root, "Label", label, false, 22f, UiPalette.TextSecondary, TextAlignmentOptions.TopLeft, 5f);
            Fill(name.rectTransform, 2, 120, 0, 24);
            TMP_Text value = Label(root, "Value", "50", true, 30f, UiPalette.TextPrimary, TextAlignmentOptions.TopRight);
            Fill(value.rectTransform, 400, 0, -6, 24);
            Image track = Img(root, "Track", "Pill", UiPalette.Outline);
            Place(track.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(560f, 12f));
            track.rectTransform.anchorMax = new Vector2(1f, 0f);
            track.rectTransform.sizeDelta = new Vector2(0f, 12f);
            Image fill = Img(track.rectTransform, "Fill", "Pill", UiPalette.Lime);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var bar = root.gameObject.AddComponent<StatBar>();
            Wire(bar, "fill", fill.rectTransform);
            Wire(bar, "valueLabel", value);
            return bar;
        }

        // ------------------------------------------------------------------ shared pieces

        private static void CreateShowroomMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            void Make(string name, Color c, float smooth, Color emit)
            {
                string path = $"{ProjectBootstrapper.MaterialsDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", 0.2f);
                if (emit.maxColorComponent > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emit); }
                EditorUtility.SetDirty(m);
            }
            Make("ShowroomFloor", new Color(0.06f, 0.07f, 0.08f), 0.85f, Color.black);
            Make("ShowroomRing", UiPalette.Lime, 0.5f, UiPalette.Lime * 0.9f);
            Make("ShowroomWall", new Color(0.035f, 0.04f, 0.05f), 0.2f, Color.black);
        }

        /// <summary>Dark studio: glossy floor disc, lime light ring, backdrop, key light and a framing camera.</summary>
        private static CarShowroom Showroom(Vector3 cameraPos, Vector3 lookAt)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.46f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.17f, 0.19f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.06f);

            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiPalette.Background;
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;
            cam.gameObject.AddComponent<AudioListener>();
            cam.transform.SetPositionAndRotation(cameraPos, Quaternion.LookRotation(lookAt - cameraPos));
            var drift = cam.gameObject.AddComponent<CameraDrift>();
            var dso = new SerializedObject(drift);
            dso.FindProperty("lookAt").vector3Value = lookAt;
            dso.ApplyModifiedPropertiesWithoutUndo();

            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.color = new Color(1f, 0.97f, 0.93f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -40f, 0f);
            RenderSettings.sun = sun;

            var studio = new GameObject("Showroom").transform;
            ProjectBootstrapper.Primitive(PrimitiveType.Cylinder, studio, "Floor", new Vector3(0f, -0.03f, 0f), Quaternion.identity, new Vector3(8.5f, 0.03f, 8.5f), "ShowroomFloor", false);
            ProjectBootstrapper.Primitive(PrimitiveType.Cylinder, studio, "Ring", new Vector3(0f, -0.05f, 0f), Quaternion.identity, new Vector3(8.62f, 0.02f, 8.62f), "ShowroomRing", false);
            ProjectBootstrapper.Primitive(PrimitiveType.Plane, studio, "Ground", new Vector3(0f, -0.07f, 0f), Quaternion.identity, new Vector3(8f, 1f, 8f), "ShowroomWall", false);
            ProjectBootstrapper.Primitive(PrimitiveType.Cube, studio, "Backdrop", new Vector3(0f, 6f, 14f), Quaternion.identity, new Vector3(60f, 14f, 0.2f), "ShowroomWall", false);
            Transform turntable = ProjectBootstrapper.Child(studio, "Turntable");
            turntable.localRotation = Quaternion.Euler(0f, 210f, 0f);
            var showroom = studio.gameObject.AddComponent<CarShowroom>();
            Wire(showroom, "turntable", turntable);
            return showroom;
        }

        private static void Camera2D()
        {
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiPalette.Background;
            cam.cullingMask = 0;
            cam.gameObject.AddComponent<AudioListener>();
        }

        private static RectTransform MakeCanvas(string name, out Canvas canvas)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;   // landscape: keep the height, let width grow on wide phones
            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        private static RectTransform SafeArea(RectTransform root)
        {
            RectTransform safe = NewRect(root, "SafeArea");
            Fill(safe, 0, 0, 0, 0);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            return safe;
        }

        private static void MakeEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static void CoinPill(RectTransform parent)
        {
            Image pill = Img(parent, "CoinPill", "Pill", UiPalette.SurfaceRaised);
            Place(pill.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-64f, -100f), new Vector2(280f, 76f));
            Image coin = Img(pill.rectTransform, "CoinIcon", "Coin", Color.white, sliced: false);
            Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(52f, 52f));
            TMP_Text amount = Label(pill.rectTransform, "Amount", "0", true, 40f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineRight);
            Fill(amount.rectTransform, 74, 28, 0, 0);
            Wire(pill.gameObject.AddComponent<CoinCounter>(), "label", amount);
        }

        private static TMP_Text StarPill(RectTransform parent, Vector2 pos)
        {
            Image pill = Img(parent, "StarPill", "Pill", UiPalette.SurfaceRaised);
            Place(pill.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), pos, new Vector2(240f, 76f));
            Image star = Img(pill.rectTransform, "StarIcon", "Star", UiPalette.Lime, sliced: false);
            Place(star.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(42f, 42f));
            TMP_Text amount = Label(pill.rectTransform, "Amount", "0/90", true, 40f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineRight);
            Fill(amount.rectTransform, 70, 28, 0, 0);
            return amount;
        }

        private static Button PillButton(RectTransform parent, string name, string text, bool filled, Vector2 pos, Vector2 size, float fontSize)
        {
            Image bg = Img(parent, name, filled ? "Pill" : "PillOutline", filled ? UiPalette.Lime : UiPalette.TextPrimary, raycast: true);
            Place(bg.rectTransform, Vector2.zero, Vector2.zero, pos, size);
            var b = bg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            bg.gameObject.AddComponent<UIButtonFX>();
            TMP_Text label = Label(bg.rectTransform, "Label", text, true, fontSize, filled ? UiPalette.TextOnLime : UiPalette.TextPrimary, TextAlignmentOptions.Center, 8f);
            Fill(label.rectTransform, 0, 0, 0, 0);
            return b;
        }

        private static Button CircleButton(RectTransform parent, string name, string icon, Vector2 anchor, Vector2 pos)
        {
            Image bg = Img(parent, name, "Circle", UiPalette.SurfaceRaised, sliced: false, raycast: true);
            Place(bg.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(92f, 92f));
            var b = bg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            bg.gameObject.AddComponent<UIButtonFX>();
            Image ic = Img(bg.rectTransform, "Icon", icon, UiPalette.Lime, sliced: false);
            Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            return b;
        }

        private static RectTransform Group(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            RectTransform rt = NewRect(parent, name);
            Place(rt, anchor, pivot, pos, size);
            return rt;
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Stretch to the parent with insets (left, right, top, bottom).</summary>
        private static void Fill(RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static Image Img(Transform parent, string name, string sprite, Color color, bool sliced = true, bool raycast = false)
        {
            RectTransform rt = NewRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = UiAssets.Sprite(sprite);
                if (img.sprite == null) Debug.LogWarning($"[Bootstrap] UI sprite '{sprite}' missing");
                img.type = sliced && img.sprite != null && img.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            }
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        private static TMP_Text Label(Transform parent, string name, string text, bool heading, float size, Color color,
            TextAlignmentOptions align, float spacing = 0f)
        {
            RectTransform rt = NewRect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = heading ? UiAssets.HeadingFont : UiAssets.BodyFont;
            t.fontSize = size;
            t.text = text;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void Wire(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError($"[Bootstrap] {target.GetType().Name}.{property} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Wire(Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray(Object target, string property, params Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Also<T>(this T value, System.Action<T> act)
        {
            act(value);
            return value;
        }
    }
}
