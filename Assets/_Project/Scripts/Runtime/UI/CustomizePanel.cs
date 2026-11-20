using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Audio;
using UltimateCarParking.Cars;
using UltimateCarParking.Save;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Garage customisation: category tabs (paint, finish, rims, window tint, headlights, plate), an option grid with a
    /// live preview on the turntable, the price, a two-step purchase (BUY 400 → TAP AGAIN TO CONFIRM), reset to factory.
    /// Options a model cannot show are greyed out with the reason. Coins are only spent on confirmed options.
    /// </summary>
    public sealed class CustomizePanel : MonoBehaviour
    {
        [SerializeField] private GarageController garage;
        [SerializeField] private CarShowroom showroom;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button[] tabs = new Button[6];
        [SerializeField] private RectTransform grid;
        [SerializeField] private Button optionTemplate;
        [SerializeField] private TMP_InputField plateInput;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyLabel;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button doneButton;
        [SerializeField] private CoinCounter coins;

        private CustomSlot slot = CustomSlot.Paint;
        private int pending = -1;           // option index being previewed
        private bool confirming;
        private readonly List<Button> options = new List<Button>();

        public bool IsOpen => gameObject.activeSelf;
        public CustomSlot Slot => slot;
        public string StatusText => status != null ? status.text : "";
        public string BuyText => buyLabel != null ? buyLabel.text : "";
        public int OptionCount => options.Count;

        private SaveData Save => SaveManager.Current.Data;
        private CarData Car => garage != null ? garage.Shown : null;

        private void Awake()
        {
            for (int i = 0; i < tabs.Length; i++) { int k = i; if (tabs[i] != null) tabs[i].onClick.AddListener(() => SelectSlot((CustomSlot)k)); }
            if (buyButton != null) buyButton.onClick.AddListener(Buy);
            if (resetButton != null) resetButton.onClick.AddListener(ResetLook);
            if (doneButton != null) doneButton.onClick.AddListener(Close);
            if (optionTemplate != null) optionTemplate.gameObject.SetActive(false);
            Enlarge();
            gameObject.SetActive(false);
        }

        /// <summary>The customize sheet covers the right side at a size you can read on a phone.</summary>
        private void Enlarge()
        {
            var rt = (RectTransform)transform;
            rt.anchoredPosition = new Vector2(-28f, -20f);
            rt.sizeDelta = new Vector2(900f, 1000f);
            if (title != null)
            {
                title.fontSize = 40f;
                title.enableAutoSizing = false;
                var titleRect = title.rectTransform;
                titleRect.anchoredPosition = new Vector2(28f, -22f);
                titleRect.sizeDelta = new Vector2(844f, 52f);
            }
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                var tab = tabs[i].GetComponent<RectTransform>();
                tab.sizeDelta = new Vector2(274f, 72f);
                tab.anchoredPosition = new Vector2(28f + (i % 3) * 288f, -88f - (i / 3) * 84f);
                var label = tabs[i].GetComponentInChildren<TMP_Text>();
                if (label != null) { label.fontSize = 30f; label.enableAutoSizing = false; }
            }
            if (grid != null)
            {
                grid.anchoredPosition = new Vector2(28f, -268f);
                grid.sizeDelta = new Vector2(844f, 480f);
                var layout = grid.GetComponent<GridLayoutGroup>();
                if (layout != null)
                {
                    layout.cellSize = new Vector2(272f, 88f);
                    layout.spacing = new Vector2(14f, 14f);
                }
            }
            if (optionTemplate != null)
            {
                var label = optionTemplate.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.fontSize = 28f;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 22f;
                    label.fontSizeMax = 28f;
                }
            }
            if (plateInput != null)
            {
                var input = plateInput.GetComponent<RectTransform>();
                input.anchoredPosition = new Vector2(28f, -268f);
                input.sizeDelta = new Vector2(844f, 88f);
                plateInput.pointSize = 36f;
            }
            if (status != null)
            {
                status.fontSize = 28f;
                status.enableAutoSizing = false;
                var statusRect = status.rectTransform;
                statusRect.anchoredPosition = new Vector2(28f, 220f);
                statusRect.sizeDelta = new Vector2(844f, 44f);
            }
            GrowButton(buyButton, new Vector2(28f, 118f), new Vector2(520f, 90f), 34f);
            GrowButton(resetButton, new Vector2(564f, 118f), new Vector2(308f, 90f), 30f);
            GrowButton(doneButton, new Vector2(28f, 16f), new Vector2(844f, 88f), 32f);
        }

        private static void GrowButton(Button button, Vector2 position, Vector2 size, float fontSize)
        {
            if (button == null) return;
            var rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label == null) return;
            label.fontSize = fontSize;
            label.enableAutoSizing = false;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            if (group != null) StartCoroutine(UiMotion.Fade(group, 0f, 1f, 0.2f));
            SelectSlot(CustomSlot.Paint);
        }

        public void Close()
        {
            // Back to the saved look (previews are not kept).
            if (showroom != null && Car != null) showroom.Preview(CustomizationService.Look(Save, Car.CarId));
            gameObject.SetActive(false);
        }

        public void SelectSlot(CustomSlot s)
        {
            slot = s;
            confirming = false;
            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i] != null) tabs[i].GetComponentInChildren<TMP_Text>().color = i == (int)s ? UiPalette.Lime : UiPalette.TextSecondary;
            CarLook look = CustomizationService.Look(Save, Car != null ? Car.CarId : "");
            pending = Current(look);
            BuildOptions();
            if (plateInput != null) { plateInput.gameObject.SetActive(s == CustomSlot.Plate); plateInput.SetTextWithoutNotify(look.plate); }
            Refresh();
        }

        private int Current(CarLook l) => slot switch
        {
            CustomSlot.Paint => l.paint, CustomSlot.Finish => l.finish, CustomSlot.Rims => l.rims,
            CustomSlot.Tint => l.tint, CustomSlot.Headlights => l.headlights, _ => 0,
        };

        private void BuildOptions()
        {
            foreach (Button b in options) Destroy(b.gameObject);
            options.Clear();
            if (slot == CustomSlot.Plate) return;
            var names = new List<(string label, Color swatch, int value)>();
            switch (slot)
            {
                case CustomSlot.Paint:
                    names.Add(("FACTORY", new Color(0.3f, 0.32f, 0.35f), -1));
                    for (int i = 0; i < CustomizationCatalog.Paints.Length; i++) names.Add((CustomizationCatalog.Paints[i].name, CustomizationCatalog.Paints[i].color, i));
                    break;
                case CustomSlot.Finish:
                    for (int i = 0; i < CustomizationCatalog.Finishes.Length; i++) names.Add((CustomizationCatalog.Finishes[i], Color.clear, i));
                    break;
                case CustomSlot.Rims:
                    names.Add(("FACTORY", Color.clear, -1));
                    for (int i = 0; i < CustomizationCatalog.Rims.Length; i++) names.Add((CustomizationCatalog.Rims[i].name, CustomizationCatalog.Rims[i].tint * 0.8f, i));
                    break;
                case CustomSlot.Tint:
                    for (int i = 0; i < CustomizationCatalog.Tints.Length; i++) names.Add((CustomizationCatalog.Tints[i].name, new Color(0f, 0f, 0f, CustomizationCatalog.Tints[i].darkness), i));
                    break;
                case CustomSlot.Headlights:
                    for (int i = 0; i < CustomizationCatalog.Headlights.Length; i++) names.Add((i == 0 ? "FACTORY" : CustomizationCatalog.Headlights[i].name, CustomizationCatalog.Headlights[i].color, i));
                    break;
            }
            foreach (var (label, swatch, value) in names)
            {
                Button b = Instantiate(optionTemplate, grid);
                b.gameObject.SetActive(true);
                b.name = "Option_" + value;
                b.GetComponentInChildren<TMP_Text>().text = label;
                Transform sw = b.transform.Find("Swatch");
                if (sw != null)
                {
                    sw.gameObject.SetActive(swatch.a > 0.01f);
                    sw.GetComponent<Image>().color = slot == CustomSlot.Tint ? new Color(0f, 0f, 0f, Mathf.Max(0.15f, swatch.a)) : new Color(swatch.r, swatch.g, swatch.b, 1f);
                }
                int v = value;
                b.onClick.AddListener(() => Pick(v));
                options.Add(b);
            }
        }

        /// <summary>Previews an option on the turntable car.</summary>
        public void Pick(int value)
        {
            pending = value;
            confirming = false;
            if (showroom != null && Car != null) showroom.Preview(PreviewLook());
            Refresh();
        }

        private CarLook PreviewLook()
        {
            CarLook l = CustomizationService.Look(Save, Car.CarId).Clone();
            switch (slot)
            {
                case CustomSlot.Paint: l.paint = pending; break;
                case CustomSlot.Finish: l.finish = pending; break;
                case CustomSlot.Rims: l.rims = pending; break;
                case CustomSlot.Tint: l.tint = pending; break;
                case CustomSlot.Headlights: l.headlights = pending; break;
                case CustomSlot.Plate: l.plate = CustomizationCatalog.CleanPlate(plateInput != null ? plateInput.text : ""); break;
            }
            return l;
        }

        private bool Supported()
        {
            GameObject car = showroom != null ? showroom.CurrentPreview : null;
            return car == null || CarLookApplier.On(car).Supports(slot);
        }

        public void Refresh()
        {
            if (Car == null) return;
            if (title != null) title.text = "CUSTOMIZE  ·  " + Car.DisplayName.ToUpperInvariant();
            CarLook saved = CustomizationService.Look(Save, Car.CarId);
            bool owned = GarageService.IsOwned(Save, Car);
            for (int i = 0; i < options.Count; i++)
            {
                int v = ValueOf(options[i]);
                bool selected = v == pending, applied = v == Current(saved);
                options[i].GetComponent<Image>().color = selected ? UiPalette.SurfaceRaised : UiPalette.Surface;
                TMP_Text t = options[i].GetComponentInChildren<TMP_Text>();
                t.color = applied ? UiPalette.Lime : UiPalette.TextPrimary;
            }
            int cost = slot == CustomSlot.Plate ? CustomizationCatalog.PlatePrice : CustomizationService.CostToApply(Save, Car.CarId, slot, pending);
            bool isApplied = slot != CustomSlot.Plate && pending == Current(saved);
            string reason = !Supported() ? "NOT AVAILABLE ON THIS MODEL" : !owned ? "BUY THE CAR FIRST  ·  PREVIEW ONLY"
                : isApplied ? "FITTED" : cost == 0 ? "OWNED  ·  FREE TO FIT" : $"PRICE  {cost:N0}" + (Save.coins < cost ? $"  ·  NEED {cost - Save.coins:N0} MORE" : "");
            if (status != null) status.text = reason;
            bool can = Supported() && owned && !isApplied && Save.coins >= cost;
            if (buyButton != null) buyButton.interactable = can;
            if (buyLabel != null)
                buyLabel.text = !can ? (isApplied ? "FITTED" : "APPLY") : cost == 0 ? "APPLY" : confirming ? $"CONFIRM {cost:N0}" : $"BUY {cost:N0}";
            coins?.Refresh();
        }

        private static int ValueOf(Button b) => int.TryParse(b.name.Substring("Option_".Length), out int v) ? v : 0;

        /// <summary>First tap on a paid option asks for confirmation; the second tap buys and fits it.</summary>
        public void Buy()
        {
            if (Car == null) return;
            int cost = slot == CustomSlot.Plate ? CustomizationCatalog.PlatePrice : CustomizationService.CostToApply(Save, Car.CarId, slot, pending);
            if (cost > 0 && !confirming) { confirming = true; Refresh(); return; }
            bool ok = CustomizationService.TryApply(Save, Car, slot, pending, plateInput != null ? plateInput.text : null);
            confirming = false;
            if (ok)
            {
                SaveManager.Current.Save();
                AudioManager.Instance.Play(cost > 0 ? Sfx.Coin : Sfx.Click, 0.8f);
                if (showroom != null) showroom.Preview(CustomizationService.Look(Save, Car.CarId));
            }
            Refresh();
        }

        public void ResetLook()
        {
            if (Car == null || !GarageService.IsOwned(Save, Car)) return;
            CustomizationService.ResetToFactory(Save, Car.CarId);
            SaveManager.Current.Save();
            if (showroom != null) showroom.Preview(CustomizationService.Look(Save, Car.CarId));
            SelectSlot(slot);
        }
    }
}
