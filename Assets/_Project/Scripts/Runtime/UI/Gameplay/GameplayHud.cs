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
    /// limit, and the parking guide (the gear and speed are shown by SpeedGauge) chip with hold progress. Event-driven except the timer and the
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
        [Tooltip("Cycles the chase camera distance: near / mid / far.")]
        [SerializeField] private Button zoomButton;
        [SerializeField] private TMP_Text zoomLabel;
        [Tooltip("Gear refusals (\"stop to change gear\") are shown as toasts.")]
        [SerializeField] private MobileControls controls;
        [Tooltip("\"OBJECTIVES 1/3\" above the checklist (hidden for plain parking levels).")]
        [SerializeField] private TMP_Text objectiveProgress;
        [Tooltip("Tap target on the objectives header: expands / collapses the list.")]
        [SerializeField] private Button objectiveToggle;
        [Tooltip("While the backup camera is up the list collapses so nothing covers it.")]
        [SerializeField] private RearCameraView rearView;

        private int shownTenths = -1;
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
        private static int zoomIndex = 1;
        private static readonly float[] ZoomDistances = { 5.5f, 7f, 10f };
        private static readonly string[] ZoomNames = { "NEAR", "MID", "FAR" };

        public string ZoomText => zoomLabel != null ? zoomLabel.text : "";
        public string ObjectiveProgressText => objectiveProgress != null && objectiveProgress.gameObject.activeSelf ? objectiveProgress.text : "";

        public string ViewText => viewLabel != null ? viewLabel.text : "";

        private void Awake()
        {
            if (viewButton != null) viewButton.onClick.AddListener(ToggleView);
            if (zoomButton != null) zoomButton.onClick.AddListener(CycleZoom);
            if (objectiveToggle != null) objectiveToggle.onClick.AddListener(ToggleObjectives);
            EnlargeTopRight();
            ApplyView();
        }

        /// <summary>Hits, camera and zoom chips are large enough to read and tap on a phone.</summary>
        private void EnlargeTopRight()
        {
            GrowChip(collisionPill, new Vector2(-28f, -20f), new Vector2(320f, 104f), 28f, 56f);
            GrowChip(viewButton != null ? viewButton.GetComponent<RectTransform>() : null, new Vector2(-220f, -140f), new Vector2(250f, 96f), 32f);
            GrowChip(zoomButton != null ? zoomButton.GetComponent<RectTransform>() : null, new Vector2(-28f, -140f), new Vector2(180f, 96f), 32f);
        }

        private static void GrowChip(RectTransform rect, Vector2 position, Vector2 size, float labelSize, float countSize = 0f)
        {
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            foreach (var text in rect.GetComponentsInChildren<TMP_Text>(true))
            {
                bool count = text.gameObject.name == "Count";
                text.fontSize = count && countSize > 0f ? countSize : labelSize;
                text.enableAutoSizing = false;
            }
        }

        /// <summary>VIEW button: Chase → Top → Bumper → Cockpit (cars with an interior) → Orbit → Chase. Remembered.</summary>
        public void ToggleView()
        {
            if (vehicleCamera == null) return;
            vehicleCamera.SetView(vehicleCamera.NextView());
            UltimateCarParking.Settings.SettingsService.SetCameraView((int)vehicleCamera.View);
            ApplyView(false);
        }

        public static string ViewName(CameraView v) => v switch
        {
            CameraView.TopDown => "TOP", CameraView.Bumper => "BUMPER", CameraView.Cockpit => "COCKPIT", CameraView.Orbit => "ORBIT", _ => "CHASE",
        };

        /// <summary>Chase camera distance: near → mid → far → near.</summary>
        public void CycleZoom()
        {
            zoomIndex = (zoomIndex + 1) % ZoomDistances.Length;
            if (vehicleCamera != null) vehicleCamera.Distance = ZoomDistances[zoomIndex];
            UltimateCarParking.Settings.SettingsService.SetChaseDistance(ZoomDistances[zoomIndex]);
            ApplyView(false);
        }

        /// <summary>Restores the remembered view and distance (level start) or just refreshes the labels.</summary>
        private void ApplyView(bool restore = true)
        {
            var s = UltimateCarParking.Settings.SettingsService.Current;
            if (vehicleCamera != null && restore)
            {
                vehicleCamera.SetView((CameraView)s.cameraView, instant: true);
                vehicleCamera.Distance = s.chaseDistance;
            }
            float d = vehicleCamera != null ? vehicleCamera.Distance : s.chaseDistance;
            zoomIndex = d < 6.25f ? 0 : d < 8.5f ? 1 : 2;
            CameraView v = vehicleCamera != null ? vehicleCamera.View : (CameraView)s.cameraView;
            bool zoomable = v == CameraView.Chase || v == CameraView.Orbit;
            if (viewLabel != null) viewLabel.text = ViewName(v);
            if (zoomLabel != null) zoomLabel.text = zoomable ? ZoomNames[zoomIndex] : "—";
            if (zoomButton != null) zoomButton.interactable = zoomable;
        }

        private float savedDistance = -1f;

        /// <summary>Keeps the pinch-zoom distance in the settings (written to disk with the next save).</summary>
        private void LateUpdate()
        {
            LayoutObjectives();
            if (vehicleCamera == null) return;
            float d = vehicleCamera.Distance;
            if (savedDistance < 0f) savedDistance = d;
            if (Mathf.Abs(d - savedDistance) > 0.1f)
            {
                savedDistance = d;
                UltimateCarParking.Settings.SettingsService.SetChaseDistance(d);
                ApplyView(false);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { zoomIndex = 1; objectivesExpanded = false; }

        private void OnShiftRefused() => OnNotice("STOP TO CHANGE GEAR", false);

        private UltimateCarParking.Vehicle.CollisionHandler hookedContacts;

        private const string LookHintKey = "hint_look_around_v1";

        /// <summary>First few missions: tells the player they can drag the screen to look around (shown 3 times in all).</summary>
        private System.Collections.IEnumerator LookHint()
        {
            if (Application.isBatchMode) yield break;   // automated test runs
            int shown = PlayerPrefs.GetInt(LookHintKey, 0);
            if (shown >= 3) yield break;
            yield return new WaitForSeconds(4f);
            if (toastGroup == null || toastLabel == null || toastGroup.alpha > 0.05f) yield break;
            PlayerPrefs.SetInt(LookHintKey, shown + 1);
            ShowContactToast("DRAG THE SCREEN TO LOOK AROUND", UiPalette.TextPrimary);
        }

        private void HookContacts(LevelData _)
        {
            if (_ != null && isActiveAndEnabled) StartCoroutine(LookHint());
            var h = levelManager != null && levelManager.Vehicle != null ? levelManager.Vehicle.GetComponent<UltimateCarParking.Vehicle.CollisionHandler>() : null;
            if (h == hookedContacts) return;
            if (hookedContacts != null) hookedContacts.GentleContact -= OnGentle;
            hookedContacts = h;
            if (h != null) h.GentleContact += OnGentle;
        }

        /// <summary>Gentle contact: a quiet note, no flash, no penalty (only when no other message is showing).</summary>
        private void OnGentle(Collider _)
        {
            if (toastGroup == null || toastLabel == null || toastGroup.alpha > 0.05f) return;
            bool forgiven = hookedContacts != null && hookedContacts.LastTouchForgiven;
            ShowContactToast(forgiven ? "BUMP  ·  NO PENALTY ON EASY" : "LIGHT TOUCH  ·  NO PENALTY", forgiven ? UiPalette.Gold : UiPalette.TextSecondary);
        }

        private void ShowContactToast(string text, Color color)
        {
            toastLabel.text = text;
            toastLabel.color = color;
            ContactToasts++;
            LastContactText = text;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine());
        }

        public int ContactToasts { get; private set; }
        public string LastContactText { get; private set; } = "";

        /// <summary>Remembered for the session: compact (current objective only) by default.</summary>
        private static bool objectivesExpanded;
        private bool shownExpanded = true;
        public bool ObjectivesExpanded => objectivesExpanded;
        public int VisibleObjectiveRows { get { int n = 0; foreach (var r in rows) if (r.text != null && r.text.transform.parent.gameObject.activeSelf) n++; return n; } }

        public void ToggleObjectives()
        {
            objectivesExpanded = !objectivesExpanded;
            LayoutObjectives(force: true);
        }

        /// <summary>Compact: only the current objective row; expanded: every row. Collapses while reversing with the camera up.</summary>
        private void LayoutObjectives(bool force = false)
        {
            bool expanded = objectivesExpanded && !(rearView != null && rearView.Showing);
            if (!force && expanded == shownExpanded) return;
            shownExpanded = expanded;
            int current = mission != null ? Mathf.Clamp(mission.CurrentIndex, 0, Mathf.Max(0, rows.Count - 1)) : 0;
            float rowH = objectiveRowTemplate != null ? objectiveRowTemplate.sizeDelta.y + 6f : 52f;
            int slot = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].text == null) continue;
                var row = (RectTransform)rows[i].text.transform.parent;
                bool show = expanded || i == current;
                row.gameObject.SetActive(show);
                if (show) row.anchoredPosition = new Vector2(0f, -(slot++) * rowH);
            }
            if (objectiveProgress != null && objectiveProgress.gameObject.activeSelf)
            {
                string t = objectiveProgress.text;
                int cut = t.IndexOf("  ·  ", System.StringComparison.Ordinal);
                if (cut >= 0) t = t.Substring(0, cut);
                objectiveProgress.text = rows.Count > 1 ? t + (expanded ? "  ·  HIDE" : "  ·  SHOW ALL") : t;
            }
        }

        private void RenderObjectiveProgress(int done)
        {
            if (objectiveProgress == null) return;
            int total = mission != null && levelManager != null && levelManager.Level != null && levelManager.Level.HasObjectives ? mission.Objectives.Count : 0;
            objectiveProgress.gameObject.SetActive(total > 0);
            Transform plate = objectiveProgress.transform.parent != null ? objectiveProgress.transform.parent.Find("ObjectiveHeaderPlate") : null;
            if (plate != null) plate.gameObject.SetActive(total > 0);
            if (total > 0) objectiveProgress.text = $"OBJECTIVES  {Mathf.Clamp(done, 0, total)}/{total}";
            LayoutObjectives(force: true);
        }

        private void OnEnable()
        {
            if (levelManager == null) return;
            levelManager.LevelStarted += OnStarted;
            levelManager.CollisionCountChanged += OnCollision;
            levelManager.LevelStarted += HookContacts;
            HookContacts(null);
            if (controls != null) controls.ShiftRefused += OnShiftRefused;
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
            levelManager.LevelStarted -= HookContacts;
            if (hookedContacts != null) hookedContacts.GentleContact -= OnGentle;
            hookedContacts = null;
            if (controls != null) controls.ShiftRefused -= OnShiftRefused;
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
            Difficulty play = PlayDifficulty.Current;
            string playColour = play == Difficulty.Easy ? "#B5FF2E" : play == Difficulty.Hard ? "#FF6B5E" : "#FFD23F";
            if (levelName != null) levelName.text = (level.HasObjectives ? MissionTypeInfo.Title(level.MissionType) : place)
                + (level.Difficulty != ChallengeDifficulty.None ? " · " + DifficultyProfile.Name(level.Difficulty) : "")
                + $" · <color={playColour}>{PlayDifficulty.Name(play)}</color>";
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
            RenderObjectiveProgress(mission.CurrentIndex);
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

        private void OnObjectiveStarted(int index, MissionObjective _) { RenderRow(index, RowState.Current); LayoutObjectives(force: true); }

        private void OnObjectiveCompleted(int index, MissionObjective objective)
        {
            RenderRow(index, RowState.Done);
            RenderObjectiveProgress(index + 1);
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
            // Never over the backup camera: drop below it (and above the sensors) while it is showing.
            var rt = (RectTransform)toastGroup.transform;
            float y = rearView != null && rearView.Showing ? 0.405f : 0.62f;
            rt.anchorMin = new Vector2(rt.anchorMin.x, y); rt.anchorMax = new Vector2(rt.anchorMax.x, y);
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
            // Scrapes get a lighter flash and shake than real impacts.
            var hits = levelManager.Vehicle != null ? levelManager.Vehicle.GetComponent<UltimateCarParking.Vehicle.CollisionHandler>() : null;
            bool impact = hits == null || hits.LastSeverity == UltimateCarParking.Vehicle.CollisionHandler.HitSeverity.Impact;
            if (flash != null) flash.Flash(UiPalette.Danger, impact ? 0.35f : 0.18f);
            // Counted contacts also say what they were (only when no mission message is showing).
            if (toastGroup != null && toastLabel != null && toastGroup.alpha < 0.05f)
                ShowContactToast(impact ? "HIT!" : "SCRAPE", impact ? UiPalette.Danger : UiPalette.Gold);
            if (vehicleCamera != null) vehicleCamera.Shake(impact ? 0.14f : 0.05f, impact ? 0.28f : 0.18f);
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
