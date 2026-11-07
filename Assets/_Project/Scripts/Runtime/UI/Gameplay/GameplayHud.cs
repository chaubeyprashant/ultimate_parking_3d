using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Audio;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Levels;
using UltimateCarParking.Missions;
using UltimateCarParking.Parking;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Mission HUD: start banner (mission type + briefing), objective checklist (ticks with a lime pop when done),
    /// the live instruction line from the current objective, a mission gauge (rear sensor, kerb gap…) and toasts.
    /// In-level HUD: level number and name, countdown timer (turns red under 10 s), collision counter against the
    /// limit, gear (D / R / P), and the parking guide chip with hold progress. Event-driven except the timer and the
    /// hold bar, which update text only when the displayed value changes.
    /// </summary>
    public sealed class GameplayHud : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private ParkingValidator validator;

        [Header("Top bar")]
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text levelName;
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField] private TMP_Text collisionLabel;
        [SerializeField] private RectTransform collisionPill;
        [SerializeField] private TMP_Text gearLabel;

        [Header("Guide")]
        [SerializeField] private CanvasGroup guideGroup;
        [SerializeField] private TMP_Text guideLabel;
        [SerializeField] private Image guideIcon;
        [SerializeField] private RectTransform holdFill;

        [Header("Mission")]
        [SerializeField] private MissionRunner mission;
        [SerializeField] private RectTransform objectiveList;
        [Tooltip("Inactive row (Dot image + Text) cloned per objective.")]
        [SerializeField] private RectTransform objectiveRowTemplate;
        [SerializeField] private CanvasGroup gaugeGroup;
        [SerializeField] private TMP_Text gaugeCaption;
        [SerializeField] private TMP_Text gaugeValue;
        [SerializeField] private RectTransform gaugeFill;
        [SerializeField] private Image gaugeFillImage;
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TMP_Text toastLabel;
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private TMP_Text bannerKicker;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField, Min(0.5f)] private float bannerSeconds = 3.2f;

        [Header("Feedback")]
        [SerializeField] private ScreenFlash flash;
        [SerializeField] private VehicleCamera vehicleCamera;
        [Tooltip("Switches the main camera between the chase view and a top-down view.")]
        [SerializeField] private Button viewButton;
        [SerializeField] private TMP_Text viewLabel;

        private int shownTenths = -1;
        private string shownGear;
        private float shownHold = -1f;
        private string shownHint;
        private int shownGaugeVersion = -1;
        private readonly System.Collections.Generic.List<(Image dot, TMP_Text text)> rows = new System.Collections.Generic.List<(Image, TMP_Text)>();
        private Coroutine toastRoutine, bannerRoutine;

        public string BannerText => bannerText != null ? bannerText.text : "";
        public string GaugeText => gaugeValue != null && gaugeGroup != null && gaugeGroup.alpha > 0f ? gaugeValue.text : "";
        public string ToastText => toastLabel != null && toastGroup != null && toastGroup.alpha > 0f ? toastLabel.text : "";
        public int ObjectiveRowCount => rows.Count;
        public string ObjectiveRowText(int i) => i >= 0 && i < rows.Count ? rows[i].text.text : "";

        public string TimerText => timerLabel != null ? timerLabel.text : "";
        public string CollisionText => collisionLabel != null ? collisionLabel.text : "";
        public string GuideText => guideLabel != null ? guideLabel.text : "";

        /// <summary>Remembered for the session (restarts and next levels keep the player's choice).</summary>
        private static bool topDownPreferred;

        public string ViewText => viewLabel != null ? viewLabel.text : "";

        private void Awake()
        {
            if (viewButton != null) viewButton.onClick.AddListener(ToggleView);
            ApplyView();
        }

        public void ToggleView()
        {
            topDownPreferred = !topDownPreferred;
            ApplyView();
        }

        private void ApplyView()
        {
            if (vehicleCamera != null) vehicleCamera.TopDown = topDownPreferred;
            if (viewLabel != null) viewLabel.text = topDownPreferred ? "VIEW: TOP" : "VIEW: CHASE";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => topDownPreferred = false;

        private void OnEnable()
        {
            if (levelManager == null) return;
            levelManager.LevelStarted += OnStarted;
            levelManager.CollisionCountChanged += OnCollision;
            if (validator != null) validator.StatusChanged += OnStatus;
            if (mission != null)
            {
                mission.ObjectiveStarted += OnObjectiveStarted;
                mission.ObjectiveCompleted += OnObjectiveCompleted;
                mission.Notice += OnNotice;
            }
            if (levelManager.Level != null) OnStarted(levelManager.Level);
        }

        private void OnDisable()
        {
            if (levelManager == null) return;
            levelManager.LevelStarted -= OnStarted;
            levelManager.CollisionCountChanged -= OnCollision;
            if (validator != null) validator.StatusChanged -= OnStatus;
            if (mission != null)
            {
                mission.ObjectiveStarted -= OnObjectiveStarted;
                mission.ObjectiveCompleted -= OnObjectiveCompleted;
                mission.Notice -= OnNotice;
            }
        }

        private void OnStarted(LevelData level)
        {
            if (levelLabel != null) levelLabel.text = $"LEVEL {level.LevelNumber:00}";
            string place = level.Theme != null ? level.Theme.DisplayName.ToUpperInvariant() : level.DisplayName.ToUpperInvariant();
            if (levelName != null) levelName.text = level.HasObjectives ? MissionTypeInfo.Title(level.MissionType) : place;
            RenderCollisions(levelManager.Collisions);
            OnStatus(validator != null ? validator.Status : ParkingStatus.Outside);
            shownTenths = -1;
            shownHint = null;
            BuildObjectiveRows();
            if (gaugeGroup != null) gaugeGroup.alpha = 0f;
            if (toastGroup != null) toastGroup.alpha = 0f;
            ShowBanner(level);
        }

        // ---------------------------------------------------------------- mission widgets

        private void BuildObjectiveRows()
        {
            foreach (var r in rows) if (r.text != null) Destroy(r.text.transform.parent.gameObject);
            rows.Clear();
            if (mission == null || objectiveList == null || objectiveRowTemplate == null) return;
            for (int i = 0; i < mission.Objectives.Count; i++)
            {
                RectTransform row = Instantiate(objectiveRowTemplate, objectiveList);
                row.gameObject.SetActive(true);
                row.name = $"Objective{i + 1}";
                row.anchoredPosition = new Vector2(0f, -i * (objectiveRowTemplate.sizeDelta.y + 6f));
                var text = row.Find("Text").GetComponent<TMP_Text>();
                var dot = row.Find("Dot").GetComponent<Image>();
                text.text = mission.Objectives[i].Title.ToUpperInvariant();
                rows.Add((dot, text));
                RenderRow(i, i == mission.CurrentIndex ? RowState.Current : i < mission.CurrentIndex ? RowState.Done : RowState.Pending);
            }
        }

        private enum RowState { Pending, Current, Done }

        private void RenderRow(int i, RowState state)
        {
            if (i < 0 || i >= rows.Count) return;
            (Image dot, TMP_Text text) = rows[i];
            Color c = state == RowState.Done ? UiPalette.Lime : state == RowState.Current ? UiPalette.TextPrimary : UiPalette.TextSecondary;
            text.color = c;
            dot.color = state == RowState.Pending ? UiPalette.TextSecondary.WithAlpha(0.5f) : c;
            string label = mission.Objectives[i].Title.ToUpperInvariant();
            text.text = state == RowState.Done ? $"<s>{label}</s>  <b>DONE</b>" : label;
        }

        private void OnObjectiveStarted(int index, MissionObjective _) => RenderRow(index, RowState.Current);

        private void OnObjectiveCompleted(int index, MissionObjective objective)
        {
            RenderRow(index, RowState.Done);
            if (index < rows.Count) StartCoroutine(UiMotion.Scale(rows[index].text.transform.parent, 1.18f, 1f, 0.35f, back: true));
            if (flash != null) flash.Flash(UiPalette.Lime, 0.22f, 0.5f);
            AudioManager.Instance.Play(Sfx.Star, 0.8f);
            if (mission.Objectives.Count > 1 && index < mission.Objectives.Count - 1) OnNotice("OBJECTIVE COMPLETE", false);
        }

        private void OnNotice(string message, bool bad)
        {
            if (toastGroup == null || toastLabel == null) return;
            toastLabel.text = message;
            toastLabel.color = bad ? UiPalette.Danger : UiPalette.Lime;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine());
            if (bad)
            {
                if (flash != null) flash.Flash(UiPalette.Danger, 0.3f);
                AudioManager.Instance.Play(Sfx.CollisionWarning, 0.7f);
            }
        }

        private System.Collections.IEnumerator ToastRoutine()
        {
            toastGroup.alpha = 1f;
            StartCoroutine(UiMotion.Scale(toastGroup.transform, 1.15f, 1f, 0.25f, back: true));
            yield return new WaitForSecondsRealtime(1.6f);
            yield return UiMotion.Fade(toastGroup, 1f, 0f, 0.35f);
        }

        private void ShowBanner(LevelData level)
        {
            if (bannerGroup == null) return;
            if (!level.HasObjectives) { bannerGroup.alpha = 0f; return; }
            if (bannerKicker != null) bannerKicker.text = MissionTypeInfo.Title(level.MissionType);
            if (bannerText != null) bannerText.text = level.Briefing;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine());
        }

        private System.Collections.IEnumerator BannerRoutine()
        {
            yield return UiMotion.Fade(bannerGroup, 0f, 1f, 0.25f);
            yield return new WaitForSecondsRealtime(bannerSeconds);
            yield return UiMotion.Fade(bannerGroup, 1f, 0f, 0.4f);
        }

        private void RenderGauge()
        {
            if (mission == null || gaugeGroup == null) return;
            MissionGauge g = mission.Gauge;
            if (g.Version == shownGaugeVersion) return;
            shownGaugeVersion = g.Version;
            gaugeGroup.alpha = g.Visible ? 1f : 0f;
            if (!g.Visible) return;
            Color c = g.Severity == GaugeSeverity.Danger ? UiPalette.Danger : g.Severity == GaugeSeverity.Warn ? UiPalette.Gold : UiPalette.Lime;
            if (gaugeCaption != null) gaugeCaption.text = g.Caption;
            if (gaugeValue != null) { gaugeValue.text = g.Value; gaugeValue.color = c; }
            if (gaugeFill != null) gaugeFill.anchorMax = new Vector2(g.Fill, 1f);
            if (gaugeFillImage != null) gaugeFillImage.color = c;
        }

        private void OnCollision(int count)
        {
            RenderCollisions(count);
            if (collisionPill != null) StartCoroutine(UiMotion.Scale(collisionPill, 1.25f, 1f, 0.35f, back: true));
            if (flash != null) flash.Flash(UiPalette.Danger, 0.35f);
            if (vehicleCamera != null) vehicleCamera.Shake(0.14f, 0.28f);
            if (levelManager.Level.HasCollisionLimit && count == levelManager.Level.CollisionLimit)
                AudioManager.Instance.Play(Sfx.CollisionWarning, 0.8f);
        }

        private void RenderCollisions(int count)
        {
            if (collisionLabel == null) return;
            LevelData level = levelManager.Level;
            bool limited = level != null && level.HasCollisionLimit;
            collisionLabel.text = limited ? $"{count}/{level.CollisionLimit}" : count.ToString();
            collisionLabel.color = limited && count >= level.CollisionLimit ? UiPalette.Danger : UiPalette.TextPrimary;
        }

        private void OnStatus(ParkingStatus status)
        {
            if (guideLabel == null) return;
            (string text, Color color) = status switch
            {
                ParkingStatus.Outside => ("DRIVE TO THE BAY", UiPalette.TextPrimary),
                ParkingStatus.PartiallyInside => ("GET FULLY INSIDE THE LINES", UiPalette.Gold),
                ParkingStatus.Misaligned => (validator.ActiveRules.allowEitherDirection ? "STRAIGHTEN UP" : "STRAIGHTEN UP - REVERSE IN", UiPalette.Gold),
                ParkingStatus.Unpositioned => (validator.ConditionHint, UiPalette.Gold),
                ParkingStatus.TooFast => ("STOP THE CAR", UiPalette.Gold),
                ParkingStatus.Holding => ("HOLD IT...", UiPalette.Lime),
                ParkingStatus.Parked => ("PARKED!", UiPalette.Lime),
                _ => ("", UiPalette.TextSecondary),
            };
            if (mission == null) guideLabel.text = text;   // with a mission the objective's hint replaces the text (Update)
            guideLabel.color = color;
            if (guideIcon != null) guideIcon.color = color;
            if (guideGroup != null && mission == null) guideGroup.alpha = string.IsNullOrEmpty(text) ? 0f : 1f;
        }

        private void Update()
        {
            if (levelManager == null || levelManager.Level == null) return;
            LevelData level = levelManager.Level;

            float seconds = level.HasTimeLimit ? levelManager.RemainingSeconds : levelManager.ElapsedSeconds;
            int tenths = Mathf.FloorToInt(seconds * 10f);
            if (tenths != shownTenths && timerLabel != null)
            {
                shownTenths = tenths;
                int whole = Mathf.CeilToInt(seconds);
                timerLabel.text = level.HasTimeLimit && seconds < 10f ? seconds.ToString("0.0") : $"{whole / 60}:{whole % 60:00}";
                timerLabel.color = level.HasTimeLimit && seconds < 10f ? UiPalette.Danger : UiPalette.TextPrimary;
            }

            if (gearLabel != null && levelManager.Vehicle != null)
            {
                string gear = !levelManager.Vehicle.ControlsEnabled ? "P" : levelManager.Vehicle.IsReversing ? "R" : "D";
                if (gear != shownGear)
                {
                    shownGear = gear;
                    gearLabel.text = gear;
                    gearLabel.color = gear == "R" ? UiPalette.Gold : UiPalette.Lime;
                }
            }

            if (mission != null && guideLabel != null && mission.Hint != shownHint)
            {
                shownHint = mission.Hint;
                guideLabel.text = shownHint;
                if (guideGroup != null) guideGroup.alpha = string.IsNullOrEmpty(shownHint) ? 0f : 1f;
            }
            RenderGauge();

            if (holdFill != null && validator != null)
            {
                float hold = mission != null ? mission.Progress : validator.HoldProgress;
                if (!Mathf.Approximately(hold, shownHold))
                {
                    shownHold = hold;
                    holdFill.anchorMax = new Vector2(hold, 1f);
                }
            }
        }
    }
}
