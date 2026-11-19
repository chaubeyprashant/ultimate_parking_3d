using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Levels;
using UltimateCarParking.Parking;
using UltimateCarParking.Settings;
using UltimateCarParking.Vehicle;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Parking assistance HUD: a top-view car icon with three sensor bars at each bumper (green / amber / red / flashing
    /// red as obstacles get closer), the nearest distance, warning beeps that speed up as the gap closes, and (Beginner)
    /// an alignment line with the angle and side offset to the target bay. Hidden in Expert mode, above parking speed
    /// and while nothing is near. Sensor sampling is 10 Hz; the beep is a short generated tone (no audio asset).
    /// </summary>
    public sealed class ParkAssistView : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private ParkingValidator validator;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Front bars left, centre, right.")]
        [SerializeField] private Image[] frontBars = new Image[3];
        [Tooltip("Rear bars left, centre, right.")]
        [SerializeField] private Image[] rearBars = new Image[3];
        [SerializeField] private TMP_Text distanceLabel;
        [SerializeField] private TMP_Text alignLabel;
        [SerializeField] private AudioSource beepSource;
        [Tooltip("Above this speed the sensors stand by (no bars, no beeps).")]
        [SerializeField, Min(1f)] private float activeBelowKmh = 15f;

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color Caution = new Color(1f, 0.78f, 0.24f, 0.95f);
        private static readonly Color Close = new Color(1f, 0.45f, 0.2f, 1f);
        private static readonly Color Stop = new Color(1f, 0.26f, 0.32f, 1f);

        private readonly ParkAssist sensors = new ParkAssist();
        private VehicleController calibratedFor;
        private float nextBeep;
        private string shownDistance, shownAlign;

        public ParkAssist Sensors => sensors;
        public bool Showing => group != null && group.alpha > 0.5f;
        public string DistanceText => distanceLabel != null ? distanceLabel.text : "";
        public string AlignText => alignLabel != null && alignLabel.gameObject.activeSelf ? alignLabel.text : "";
        public int BeepsPlayed { get; private set; }

        private void Awake()
        {
            if (group != null) group.alpha = 0f;
            if (beepSource != null && beepSource.clip == null) beepSource.clip = MakeBeep();
        }

        private void Update()
        {
            VehicleController car = levelManager != null ? levelManager.Vehicle : null;
            GameSettings s = SettingsService.Current;
            var level = AssistPolicy.Effective(levelManager != null ? levelManager.Level : null);   // a level may cap the assistance
            bool playing = levelManager != null && levelManager.State == LevelState.Playing && car != null && car.ControlsEnabled;
            if (!playing || level == AssistLevel.Expert || car.SpeedKmh > activeBelowKmh)
            {
                Fade(0f);
                sensors.Clear();
                return;
            }
            if (car != calibratedFor && validator != null)
            {
                validator.EnsureFootprint(car);
                sensors.Calibrate(validator.LocalFootprint);
                calibratedFor = car;
            }
            sensors.Tick(car, Time.time);

            for (int i = 0; i < ParkAssist.Zones; i++)
            {
                Paint(frontBars, i, sensors.Front(i));
                Paint(rearBars, i, sensors.Rear(i));
            }
            bool reversing = car.IsReversing || (car.Input != null && car.Input.Throttle < -0.05f);
            float near = reversing ? sensors.RearMin : sensors.FrontMin;
            float any = Mathf.Min(sensors.FrontMin, sensors.RearMin);
            string d = float.IsInfinity(any) ? "" : any <= 0.3f ? "STOP" : $"{any:0.0} m";
            if (d != shownDistance && distanceLabel != null)
            {
                shownDistance = d;
                distanceLabel.text = d;
                distanceLabel.color = ColorFor(any);
            }

            bool alignShown = level == AssistLevel.Beginner && UpdateAlign(car);
            if (alignLabel != null && alignLabel.gameObject.activeSelf != alignShown) alignLabel.gameObject.SetActive(alignShown);
            Fade(!float.IsInfinity(any) || alignShown ? 1f : 0f);
            Beep(near, s);
        }

        /// <summary>Angle and side offset to the target bay once the car is within 8 m of it.</summary>
        private bool UpdateAlign(VehicleController car)
        {
            if (validator == null || validator.Zone == null || alignLabel == null) return false;
            ParkingZone z = validator.Zone;
            Vector3 rel = car.transform.position - z.Center;
            if (rel.sqrMagnitude > 64f) return false;
            float side = Vector3.Dot(rel, z.Rotation * Vector3.right);
            float angle = Vector3.SignedAngle(z.Forward, car.transform.forward, Vector3.up);
            if (Mathf.Abs(angle) > 90f) angle -= Mathf.Sign(angle) * 180f;   // either direction counts
            string text = $"ANGLE {Mathf.Abs(angle):0}°  ·  SIDE {Mathf.Abs(side):0.0} m {(Mathf.Abs(side) < 0.05f ? "" : side > 0f ? "RIGHT" : "LEFT")}".TrimEnd();
            if (text != shownAlign)
            {
                shownAlign = text;
                alignLabel.text = text;
                bool good = Mathf.Abs(angle) <= (validator.ActiveRules?.angleTolerance ?? 10f) && Mathf.Abs(side) < 0.3f;
                alignLabel.color = good ? UiPalette.Lime : UiPalette.TextPrimary;
            }
            return true;
        }

        private void Beep(float near, GameSettings s)
        {
            if (beepSource == null || !s.assistSounds || s.soundVolume <= 0f) { if (beepSource != null && beepSource.isPlaying && beepSource.loop) beepSource.Stop(); return; }
            float interval = ParkAssist.BeepInterval(near);
            beepSource.volume = s.soundVolume * 0.6f;
            if (interval < 0f) { if (beepSource.loop) { beepSource.loop = false; beepSource.Stop(); } return; }
            if (interval == 0f)
            {
                if (!beepSource.loop || !beepSource.isPlaying) { beepSource.loop = true; beepSource.Play(); BeepsPlayed++; }
                return;
            }
            if (beepSource.loop) { beepSource.loop = false; beepSource.Stop(); }
            if (Time.time >= nextBeep)
            {
                beepSource.Play();
                BeepsPlayed++;
                nextBeep = Time.time + interval;
            }
        }

        private static void Paint(Image[] bars, int i, float d)
        {
            if (bars == null || i >= bars.Length || bars[i] == null) return;
            Color c = ColorFor(d);
            if (ParkAssist.Band(d) == 3) c.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 12f));
            bars[i].color = c;
        }

        private static Color ColorFor(float d) => ParkAssist.Band(d) switch { 0 => Clear, 1 => Caution, 2 => Close, _ => Stop };

        private void Fade(float target)
        {
            if (group == null) return;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 6f);
            if (target == 0f && beepSource != null && beepSource.isPlaying) { beepSource.loop = false; beepSource.Stop(); }
        }

        /// <summary>60 ms 1.2 kHz tone with soft edges.</summary>
        private static AudioClip MakeBeep()
        {
            const int rate = 22050;
            int n = rate * 60 / 1000;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Min(1f, Mathf.Min(i, n - i) / (rate * 0.006f));
                data[i] = Mathf.Sin(2f * Mathf.PI * 1200f * i / rate) * 0.5f * env;
            }
            AudioClip clip = AudioClip.Create("ParkBeep", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
