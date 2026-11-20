using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.UI;

namespace UltimateCarParking.EditorTools
{
    /// <summary>Garage customisation panel (Phase 7): tabs, option grid with swatches, plate input, price / status, buy, reset, done.</summary>
    public static partial class UiSceneBuilder
    {
        private static CustomizePanel BuildCustomizePanel(RectTransform safe, GarageController garage, CarShowroom showroom)
        {
            Image bg = Img(safe, "CustomizePanel", "RoundRect", UiPalette.Surface.WithAlpha(0.94f), raycast: true);
            Place(bg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -20f), new Vector2(900f, 1000f));
            RectTransform p = bg.rectTransform;
            var group = bg.gameObject.AddComponent<CanvasGroup>();
            Image edge = Img(p, "Edge", "RoundRectOutline", UiPalette.TextPrimary.WithAlpha(0.14f));
            Fill(edge.rectTransform, 0, 0, 0, 0);
            TMP_Text title = Label(p, "Title", "CUSTOMIZE", true, 40f, UiPalette.TextPrimary, TextAlignmentOptions.TopLeft, 3f);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -22f), new Vector2(844f, 52f));

            string[] tabNames = { "PAINT", "FINISH", "RIMS", "TINT", "LIGHTS", "PLATE" };
            var tabs = new Button[6];
            for (int i = 0; i < 6; i++)
            {
                Image t = Img(p, "Tab_" + tabNames[i], "Pill", UiPalette.SurfaceRaised, raycast: true);
                Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f + (i % 3) * 288f, -88f - (i / 3) * 84f), new Vector2(274f, 72f));
                tabs[i] = t.gameObject.AddComponent<Button>();
                tabs[i].transition = Selectable.Transition.None;
                t.gameObject.AddComponent<UIButtonFX>();
                TMP_Text l = Label(t.rectTransform, "Label", tabNames[i], true, 30f, UiPalette.TextSecondary, TextAlignmentOptions.Center, 4f);
                Fill(l.rectTransform, 0, 0, 0, 0);
            }

            RectTransform grid = Group(p, "Options", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -268f), new Vector2(844f, 480f));
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(272f, 88f); gl.spacing = new Vector2(14f, 14f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
            Image opt = Img(grid, "OptionTemplate", "RoundRect", UiPalette.Surface, raycast: true);
            var optButton = opt.gameObject.AddComponent<Button>();
            optButton.transition = Selectable.Transition.None;
            opt.gameObject.AddComponent<UIButtonFX>();
            Image sw = Img(opt.rectTransform, "Swatch", "Circle", Color.white, sliced: false);
            Place(sw.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(30f, 30f));
            TMP_Text ol = Label(opt.rectTransform, "Label", "OPTION", true, 28f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, 2f);
            Fill(ol.rectTransform, 50, 8, 0, 0);
            ol.textWrappingMode = TextWrappingModes.Normal;
            ol.enableAutoSizing = true; ol.fontSizeMin = 22f; ol.fontSizeMax = 28f;

            // Plate text input (PLATE tab).
            Image inBg = Img(p, "PlateInput", "Pill", UiPalette.SurfaceRaised, raycast: true);
            Place(inBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -268f), new Vector2(844f, 88f));
            RectTransform area = NewRect(inBg.rectTransform, "Text Area");
            Fill(area, 24, 24, 6, 6);
            area.gameObject.AddComponent<RectMask2D>();
            TMP_Text placeholder = Label(area, "Placeholder", "TYPE YOUR PLATE (2–8)", false, 26f, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, 4f);
            Fill(placeholder.rectTransform, 0, 0, 0, 0);
            TMP_Text inText = Label(area, "Text", "", true, 32f, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, 6f);
            Fill(inText.rectTransform, 0, 0, 0, 0);
            var input = inBg.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area; input.textComponent = inText; input.placeholder = placeholder;
            input.characterLimit = 8; input.contentType = TMP_InputField.ContentType.Standard;
            input.fontAsset = UiAssets.HeadingFont; input.pointSize = 32f;
            inBg.gameObject.SetActive(false);

            TMP_Text status = Label(p, "Status", "", true, 28f, UiPalette.Gold, TextAlignmentOptions.MidlineLeft, 3f);
            Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 220f), new Vector2(844f, 44f));
            Button buy = PillButton(p, "BuyButton", "APPLY", true, new Vector2(28f, 118f), new Vector2(520f, 90f), 34f);
            Button reset = PillButton(p, "ResetButton", "FACTORY", false, new Vector2(564f, 118f), new Vector2(308f, 90f), 30f);
            Button done = PillButton(p, "DoneButton", "DONE", false, new Vector2(28f, 16f), new Vector2(844f, 88f), 32f);

            var panel = bg.gameObject.AddComponent<CustomizePanel>();
            Wire(panel, "garage", garage);
            Wire(panel, "showroom", showroom);
            Wire(panel, "group", group);
            Wire(panel, "title", title);
            WireArray(panel, "tabs", tabs);
            Wire(panel, "grid", grid);
            Wire(panel, "optionTemplate", optButton);
            Wire(panel, "plateInput", input);
            Wire(panel, "status", status);
            Wire(panel, "buyButton", buy);
            Wire(panel, "buyLabel", buy.GetComponentInChildren<TMP_Text>());
            Wire(panel, "resetButton", reset);
            Wire(panel, "doneButton", done);
            Wire(panel, "coins", Object.FindAnyObjectByType<CoinCounter>());
            bg.gameObject.SetActive(true);   // CustomizePanel hides itself in Awake
            return panel;
        }
    }
}
