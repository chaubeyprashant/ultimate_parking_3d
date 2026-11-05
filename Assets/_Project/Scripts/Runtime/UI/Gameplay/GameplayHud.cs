using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Audio;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Levels;
using UltimateCarParking.Parking;

namespace UltimateCarParking.UI
{
    /// <summary>
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

        [Header("Feedback")]
        [SerializeField] private ScreenFlash flash;
        [SerializeField] private VehicleCamera vehicleCamera;

        private int shownTenths = -1;
        private string shownGear;
        private float shownHold = -1f;

        public string TimerText => timerLabel != null ? timerLabel.text : "";
        public string CollisionText => collisionLabel != null ? collisionLabel.text : "";
        public string GuideText => guideLabel != null ? guideLabel.text : "";

        private void OnEnable()
        {
            if (levelManager == null) return;
            levelManager.LevelStarted += OnStarted;
            levelManager.CollisionCountChanged += OnCollision;
            if (validator != null) validator.StatusChanged += OnStatus;
            if (levelManager.Level != null) OnStarted(levelManager.Level);
        }

        private void OnDisable()
        {
            if (levelManager == null) return;
            levelManager.LevelStarted -= OnStarted;
            levelManager.CollisionCountChanged -= OnCollision;
            if (validator != null) validator.StatusChanged -= OnStatus;
        }

        private void OnStarted(LevelData level)
        {
            if (levelLabel != null) levelLabel.text = $"LEVEL {level.LevelNumber:00}";
            if (levelName != null) levelName.text = level.Theme != null ? level.Theme.DisplayName.ToUpperInvariant() : level.DisplayName.ToUpperInvariant();
            RenderCollisions(levelManager.Collisions);
            OnStatus(validator != null ? validator.Status : ParkingStatus.Outside);
            shownTenths = -1;
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
                ParkingStatus.TooFast => ("STOP THE CAR", UiPalette.Gold),
                ParkingStatus.Holding => ("HOLD IT...", UiPalette.Lime),
                ParkingStatus.Parked => ("PARKED!", UiPalette.Lime),
                _ => ("", UiPalette.TextSecondary),
            };
            guideLabel.text = text;
            guideLabel.color = color;
            if (guideIcon != null) guideIcon.color = color;
            if (guideGroup != null) guideGroup.alpha = string.IsNullOrEmpty(text) ? 0f : 1f;
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

            if (holdFill != null && validator != null)
            {
                float hold = validator.HoldProgress;
                if (!Mathf.Approximately(hold, shownHold))
                {
                    shownHold = hold;
                    holdFill.anchorMax = new Vector2(hold, 1f);
                }
            }
        }
    }
}
