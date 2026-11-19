using System.Collections.Generic;
using UnityEngine;
using UltimateCarParking.Settings;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Arranges the touch controls from the player's settings: steering on the left and pedals on the right (or the
    /// reverse for left-handed play), control size, opacity and height, and which controls are shown for the chosen
    /// steering mode (wheel / buttons / tilt) and pedal mode (gears / classic). Re-applies whenever a setting changes.
    /// Both zones are anchored to bottom corners of the safe area, so they hold their place on any aspect ratio.
    /// </summary>
    public sealed class ControlLayout : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform steerZone;
        [SerializeField] private RectTransform pedalZone;
        [Header("Steering modes")]
        [SerializeField] private GameObject[] buttonsOnly;
        [SerializeField] private GameObject[] wheelOnly;
        [SerializeField] private GameObject[] tiltOnly;
        [Header("Pedal modes")]
        [SerializeField] private GameObject[] gearsOnly;
        [SerializeField] private GameObject[] classicOnly;
        [Tooltip("Canvas units the Height setting raises the controls at its maximum.")]
        [SerializeField] private float maxRaise = 180f;

        private struct Pose { public Vector2 anchorMin, anchorMax, pivot, pos; }
        private readonly Dictionary<RectTransform, Pose> original = new Dictionary<RectTransform, Pose>();
        private bool captured;

        public bool Mirrored { get; private set; }
        /// <summary>Extra shrink chosen by HudLayout so the controls never cover the speedometer on narrow screens.</summary>
        public float FitScale
        {
            get => fitScale;
            set { float v = Mathf.Clamp(value, 0.5f, 1f); if (Mathf.Abs(v - fitScale) < 1e-4f) return; fitScale = v; if (captured) Apply(SettingsService.Current); }
        }
        private float fitScale = 1f;
        public RectTransform SteerZone => steerZone;
        public RectTransform PedalZone => pedalZone;

        private void Awake() { Capture(); Apply(SettingsService.Current); }
        private void OnEnable() { SettingsService.Changed += Apply; Apply(SettingsService.Current); }
        private void OnDisable() => SettingsService.Changed -= Apply;

        private void Capture()
        {
            if (captured) return;
            captured = true;
            foreach (RectTransform zone in new[] { steerZone, pedalZone })
            {
                if (zone == null) continue;
                Remember(zone);
                foreach (Transform c in zone) Remember((RectTransform)c);
            }
        }

        private void Remember(RectTransform r) =>
            original[r] = new Pose { anchorMin = r.anchorMin, anchorMax = r.anchorMax, pivot = r.pivot, pos = r.anchoredPosition };

        public void Apply(GameSettings s)
        {
            if (s == null) return;
            Capture();
            Mirrored = s.leftHanded;
            if (group != null) group.alpha = s.controlOpacity;
            float raise = s.controlsRaise * maxRaise;
            // Left-handed: the zones swap corners and every control mirrors its place inside its zone.
            foreach (KeyValuePair<RectTransform, Pose> kv in original)
            {
                RectTransform r = kv.Key;
                if (r == null) continue;
                Pose p = kv.Value;
                bool zone = r == steerZone || r == pedalZone;
                if (s.leftHanded)
                {
                    r.anchorMin = new Vector2(1f - p.anchorMax.x, p.anchorMin.y);
                    r.anchorMax = new Vector2(1f - p.anchorMin.x, p.anchorMax.y);
                    r.pivot = new Vector2(1f - p.pivot.x, p.pivot.y);
                    r.anchoredPosition = new Vector2(-p.pos.x, p.pos.y + (zone ? raise : 0f));
                }
                else
                {
                    r.anchorMin = p.anchorMin; r.anchorMax = p.anchorMax; r.pivot = p.pivot;
                    r.anchoredPosition = new Vector2(p.pos.x, p.pos.y + (zone ? raise : 0f));
                }
                if (zone) r.localScale = new Vector3(s.controlScale * fitScale, s.controlScale * fitScale, 1f);
            }
            Show(buttonsOnly, s.steeringMode == GameSettings.SteerButtons);
            Show(wheelOnly, s.steeringMode == GameSettings.SteerWheel);
            Show(tiltOnly, s.steeringMode == GameSettings.SteerTilt);
            Show(gearsOnly, s.pedalMode == GameSettings.PedalsGears);
            Show(classicOnly, s.pedalMode == GameSettings.PedalsClassic);
        }

        private static void Show(GameObject[] list, bool on)
        {
            if (list == null) return;
            foreach (GameObject go in list) if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}
