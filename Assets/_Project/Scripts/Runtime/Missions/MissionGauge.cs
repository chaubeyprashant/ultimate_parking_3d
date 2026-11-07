using UnityEngine;

namespace UltimateCarParking.Missions
{
    public enum GaugeSeverity { Good, Warn, Danger }

    /// <summary>
    /// The mission-specific HUD readout (rear sensor distance, kerb gap…). Objectives write it every frame; the HUD
    /// re-renders only when <see cref="Version"/> changes.
    /// </summary>
    public sealed class MissionGauge
    {
        public bool Visible { get; private set; }
        public string Caption { get; private set; } = "";
        public string Value { get; private set; } = "";
        public float Fill { get; private set; }
        public GaugeSeverity Severity { get; private set; }
        public int Version { get; private set; }

        public void Show(string caption, string value, float fill01, GaugeSeverity severity)
        {
            fill01 = Mathf.Clamp01(fill01);
            if (Visible && caption == Caption && value == Value && Mathf.Abs(fill01 - Fill) < 0.01f && severity == Severity) return;
            Visible = true; Caption = caption; Value = value; Fill = fill01; Severity = severity;
            Version++;
        }

        public void Hide()
        {
            if (!Visible) return;
            Visible = false;
            Version++;
        }
    }
}
