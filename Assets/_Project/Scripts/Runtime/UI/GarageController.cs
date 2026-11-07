using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Audio;
using UltimateCarParking.Cars;
using UltimateCarParking.Core;
using UltimateCarParking.Save;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Garage screen: browse cars on the turntable, see stats and price, buy with coins, select an owned car.
    /// The action button reads SELECT / SELECTED / BUY {price} (dimmed with the shortfall when unaffordable).
    /// </summary>
    public sealed class GarageController : MonoBehaviour
    {
        [SerializeField] private GarageManager garage;
        [SerializeField] private CarShowroom showroom;

        [Header("Info")]
        [SerializeField] private TMP_Text carName;
        [SerializeField] private TMP_Text tagline;
        [SerializeField] private TMP_Text indexLabel;
        [SerializeField] private StatBar topSpeed;
        [SerializeField] private StatBar acceleration;
        [SerializeField] private StatBar handling;
        [SerializeField] private StatBar braking;
        [SerializeField] private CanvasGroup infoGroup;

        [Header("Action")]
        [SerializeField] private Button actionButton;
        [SerializeField] private Image actionBackground;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private GameObject actionLockIcon;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Navigation")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button backButton;
        [SerializeField] private RectTransform dotsParent;
        [SerializeField] private Image dotTemplate;
        [SerializeField] private CoinCounter coins;
        [Header("Upgrades (Engine, Brakes, Steering)")]
        [SerializeField] private Button[] upgradeButtons = new Button[3];
        [SerializeField] private TMP_Text[] upgradeLabels = new TMP_Text[3];

        private readonly List<Image> dots = new List<Image>();
        private int index;

        public int Index => index;
        public CarData Shown => garage != null && garage.Catalog != null ? garage.Catalog.Get(index) : null;
        public string ActionText => actionLabel != null ? actionLabel.text : "";

        private void Awake()
        {
            if (previousButton != null) previousButton.onClick.AddListener(() => Step(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
            if (backButton != null) backButton.onClick.AddListener(() => ScreenTransition.LoadScene(SceneNames.MainMenu));
            if (actionButton != null) actionButton.onClick.AddListener(OnAction);
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                var kind = (UpgradeKind)i;
                if (upgradeButtons[i] != null) upgradeButtons[i].onClick.AddListener(() => OnUpgrade(kind));
            }
        }

        private void Start()
        {
            if (garage == null || garage.Catalog == null || garage.Catalog.Count == 0)
            {
                Debug.LogError("[Garage] GarageManager / CarCatalog missing or empty.", this);
                return;
            }
            AudioManager.Instance.PlayMusic(ducked: false);
            garage.Changed += Refresh;
            BuildDots();
            index = Mathf.Max(0, garage.Catalog.IndexOf(garage.SelectedCar));
            ShowCar(0);
        }

        private void OnDestroy()
        {
            if (garage != null) garage.Changed -= Refresh;
        }

        public void Step(int delta)
        {
            int count = garage.Catalog.Count;
            if (count <= 1) return;
            index = (index + delta + count) % count;
            ShowCar(delta);
        }

        private void ShowCar(int direction)
        {
            CarData car = Shown;
            if (car == null) return;
            if (showroom != null) showroom.Show(car, direction);
            if (carName != null) carName.text = car.DisplayName.ToUpperInvariant();
            if (tagline != null) tagline.text = car.Tagline;
            if (indexLabel != null) indexLabel.text = $"{index + 1:00} / {garage.Catalog.Count:00}";
            CarStats s = CarStats.From(Upgraded(car));
            topSpeed?.SetValue(s.TopSpeed);
            acceleration?.SetValue(s.Acceleration);
            handling?.SetValue(s.Handling);
            braking?.SetValue(s.Braking);
            if (infoGroup != null && direction != 0 && isActiveAndEnabled) StartCoroutine(UiMotion.Fade(infoGroup, 0.2f, 1f, 0.3f));
            Refresh();
        }

        private void Refresh()
        {
            CarData car = Shown;
            if (car == null) return;
            bool owned = garage.IsOwned(car);
            bool selected = garage.IsSelected(car);
            bool affordable = garage.CanAfford(car);

            string text = selected ? "SELECTED" : owned ? "SELECT" : $"BUY  {car.Price:N0}";
            if (actionLabel != null)
            {
                actionLabel.text = text;
                actionLabel.color = selected ? UiPalette.Lime : owned || affordable ? UiPalette.TextOnLime : UiPalette.TextSecondary;
            }
            if (actionBackground != null)
                actionBackground.color = selected ? UiPalette.LimeDim : owned || affordable ? UiPalette.Lime : UiPalette.SurfaceRaised;
            if (actionLockIcon != null) actionLockIcon.SetActive(!owned);
            if (actionButton != null) actionButton.interactable = !selected;
            if (statusLabel != null)
            {
                statusLabel.text = selected ? "IN USE" : owned ? "OWNED" : affordable ? "AVAILABLE" : $"NEED {car.Price - garage.Coins:N0} MORE";
                statusLabel.color = owned ? UiPalette.Lime : affordable ? UiPalette.TextPrimary : UiPalette.Danger;
            }
            for (int i = 0; i < dots.Count; i++)
                dots[i].color = i == index ? UiPalette.Lime : UiPalette.Outline;
            for (int i = 0; i < upgradeButtons.Length && i < upgradeLabels.Length; i++)
            {
                var kind = (UpgradeKind)i;
                int level = garage.UpgradeLevel(car, kind), price = garage.UpgradePrice(car, kind);
                string pips = $"LV {level}/{CarUpgrades.MaxLevel}";
                if (upgradeLabels[i] != null)
                {
                    upgradeLabels[i].text = $"{CarUpgrades.Name(kind)}\n<size=80%>{pips}  {(level >= CarUpgrades.MaxLevel ? "MAX" : owned ? price.ToString("N0") : "BUY CAR")}</size>";
                    upgradeLabels[i].color = garage.CanUpgrade(car, kind) ? UiPalette.TextPrimary : UiPalette.TextSecondary;
                }
                if (upgradeButtons[i] != null) upgradeButtons[i].interactable = owned && level < CarUpgrades.MaxLevel;
            }
            coins?.Refresh();
        }

        private void OnAction()
        {
            CarData car = Shown;
            if (car == null) return;
            if (garage.IsOwned(car))
            {
                garage.Select(car);
                return;
            }
            PurchaseResult r = garage.Buy(car);
            if (r == PurchaseResult.Purchased)
            {
                garage.Select(car);
                AudioManager.Instance.Play(Sfx.Coin, 0.9f);
                if (actionButton != null) StartCoroutine(UiMotion.Scale(actionButton.transform, 1.12f, 1f, 0.35f, back: true));
            }
            else if (r == PurchaseResult.NotEnoughCoins && actionButton != null)
            {
                StartCoroutine(UiMotion.Shake((RectTransform)actionButton.transform));
            }
        }

        private VehicleHandling Upgraded(CarData car) =>
            SaveManager.Current != null ? CarUpgrades.Upgraded(SaveManager.Current.Data, car) : car.Handling;

        public string UpgradeText(int i) => i < upgradeLabels.Length && upgradeLabels[i] != null ? upgradeLabels[i].text : "";

        public void OnUpgrade(UpgradeKind kind)
        {
            CarData car = Shown;
            if (car == null) return;
            if (garage.Upgrade(car, kind))
            {
                AudioManager.Instance.Play(Sfx.Coin, 0.9f);
                ShowCar(0);
                Button b = upgradeButtons[(int)kind];
                if (b != null) StartCoroutine(UiMotion.Scale(b.transform, 1.12f, 1f, 0.35f, back: true));
            }
            else if (upgradeButtons[(int)kind] != null)
                StartCoroutine(UiMotion.Shake((RectTransform)upgradeButtons[(int)kind].transform));
        }

        private void BuildDots()
        {
            if (dotsParent == null || dotTemplate == null) return;
            dotTemplate.gameObject.SetActive(false);
            for (int i = 0; i < garage.Catalog.Count; i++)
            {
                Image d = Instantiate(dotTemplate, dotsParent);
                d.gameObject.SetActive(true);
                d.name = $"Dot{i}";
                dots.Add(d);
            }
        }
    }
}
