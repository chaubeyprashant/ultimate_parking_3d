using UnityEngine;
using UltimateCarParking.Settings;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Responsive HUD layout (Phase 8). Applies the player's HUD scale to the corner widgets, and fits the layout to the
    /// screen it is on (any aspect ratio, inside the safe area / display cut-outs):
    /// <list type="bullet">
    /// <item>the touch controls shrink just enough (never below 70 %) to leave the speedometer its own gap at the bottom;</item>
    /// <item>the speedometer, parking sensors and alignment line sit centred in that gap (it moves with left-handed play);</item>
    /// <item>the top-centre column (guide, mission gauge, rear camera) narrows to fit between the corner widgets.</item>
    /// </list>
    /// Re-applies when a setting changes or the screen / safe area changes size.
    /// </summary>
    [ExecuteAlways]
    public sealed class HudLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform area;
        [Tooltip("Corner widgets scaled by the HUD size setting (pause, level, timer, hits, camera, objectives).")]
        [SerializeField] private RectTransform[] cornerWidgets = new RectTransform[0];
        [Tooltip("Top-centre column narrowed on tight screens (guide, mission gauge, rear camera).")]
        [SerializeField] private RectTransform[] centreColumn = new RectTransform[0];
        [Tooltip("Bottom-centre widgets placed in the gap between the control zones (speedometer, sensors, alignment line).")]
        [SerializeField] private RectTransform[] bottomCentre = new RectTransform[0];
        [SerializeField] private ControlLayout controls;
        [Tooltip("Right edge (canvas units) the left corner widgets reach at scale 1: level block / objectives.")]
        [SerializeField] private float leftReserve = 490f;
        [SerializeField] private float rightReserve = 330f;
        [SerializeField] private float centreColumnWidth = 600f;
        [SerializeField] private float bottomGapNeeded = 340f;
        [SerializeField] private float edgeMargin = 36f;

        private readonly System.Collections.Generic.Dictionary<RectTransform, Vector2> basePos = new System.Collections.Generic.Dictionary<RectTransform, Vector2>();

        /// <summary>Widgets scale about their corner AND keep proportional spacing, so neighbours never grow into each other.</summary>
        private void ScaleAt(RectTransform r, float scale, float? x = null)
        {
            if (!basePos.TryGetValue(r, out Vector2 p)) { p = r.anchoredPosition; basePos[r] = p; }
            r.localScale = new Vector3(scale, scale, 1f);
            r.anchoredPosition = new Vector2(x ?? p.x * scale, p.y * scale);
        }

        public float AppliedHudScale { get; private set; } = 1f;
        public float ControlFit { get; private set; } = 1f;
        public float CentreScale { get; private set; } = 1f;
        public bool Narrow { get; private set; }

        private void OnEnable() { SettingsService.Changed += OnSettings; Apply(); }
        private void OnDisable() => SettingsService.Changed -= OnSettings;
        private void OnSettings(GameSettings _) => Apply();
        private void OnRectTransformDimensionsChange() { if (isActiveAndEnabled) Apply(); }

        public void Apply()
        {
            if (area == null) return;
            GameSettings s = Application.isPlaying ? SettingsService.Current : null;
            float hud = s != null ? s.hudScale : 1f;
            float w = area.rect.width;
            if (w <= 1f) return;
            Narrow = w < 1700f;
            // Narrow screens keep corner widgets at most at 100 % so the centre column still fits.
            AppliedHudScale = Narrow ? Mathf.Min(hud, 1f) : hud;
            foreach (RectTransform r in cornerWidgets) if (r != null) ScaleAt(r, AppliedHudScale);

            // Top-centre column: whatever width is left between the corner widgets.
            // The column is centred, so it must clear the wider of the two corner groups on both sides.
            float free = w - 2f * Mathf.Max(leftReserve, rightReserve) * AppliedHudScale - 40f;
            CentreScale = Mathf.Clamp(free / centreColumnWidth, 0.7f, 1f);
            foreach (RectTransform r in centreColumn) if (r != null) r.localScale = new Vector3(CentreScale, CentreScale, 1f);

            // Bottom: the control zones keep a gap for the speedometer.
            if (controls != null && controls.SteerZone != null && controls.PedalZone != null)
            {
                float userScale = s != null ? s.controlScale : 1f;
                float zones = (controls.SteerZone.rect.width + controls.PedalZone.rect.width) * userScale;
                float room = w - 2f * edgeMargin - bottomGapNeeded * AppliedHudScale;
                ControlFit = Mathf.Clamp(room / Mathf.Max(1f, zones), 0.7f, 1f);
                controls.FitScale = ControlFit;
                float steerW = controls.SteerZone.rect.width * userScale * ControlFit;
                float pedalW = controls.PedalZone.rect.width * userScale * ControlFit;
                bool left = s != null && s.leftHanded;
                float gapLeft = edgeMargin + (left ? pedalW : steerW);
                float gapRight = w - edgeMargin - (left ? steerW : pedalW);
                float centreX = (gapLeft + gapRight) * 0.5f - w * 0.5f;
                foreach (RectTransform r in bottomCentre)
                {
                    if (r == null) continue;
                    ScaleAt(r, AppliedHudScale, centreX);
                    // Text lines (the alignment hint) are kept inside the gap.
                    if (r.name == "AlignHint") r.sizeDelta = new Vector2(Mathf.Max(200f, (gapRight - gapLeft - 20f) / AppliedHudScale), r.sizeDelta.y);
                }
            }
        }
    }
}
