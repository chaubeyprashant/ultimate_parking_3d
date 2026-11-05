using UnityEngine;
using UnityEngine.InputSystem;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Progression;
using UltimateCarParking.UI;
using UltimateCarParking.Parking;

namespace UltimateCarParking.DebugTools
{
    /// <summary>
    /// Development aid until the real HUD exists (later phase): shows level state, timer, collisions and the
    /// parking checks, with Restart / Next buttons (also R / N keys). Runs only in the Editor and in
    /// Development builds unless <see cref="showInReleaseBuilds"/> is ticked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelDebugOverlay : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private ParkingValidator validator;
        [SerializeField] private ProgressionRecorder progression;
        [SerializeField] private bool showInReleaseBuilds;
        [SerializeField] private bool hotkeys = true;
        [Tooltip("Start visible (F1 toggles). The Phase 4 HUD replaces it for normal play.")]
        [SerializeField] private bool visible;

        private GUIStyle label;
        private GUIStyle button;

        private void Awake()
        {
            if (!showInReleaseBuilds && !Application.isEditor && !Debug.isDebugBuild) enabled = false;
            if (levelManager == null) levelManager = GetComponent<LevelManager>();
            if (validator == null) validator = GetComponent<ParkingValidator>();
            if (progression == null) progression = GetComponent<ProgressionRecorder>();
        }

        private void Update()
        {
            if (!hotkeys || levelManager == null) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) visible = !visible;
            if (!visible) return;
            if (kb.rKey.wasPressedThisFrame) levelManager.RestartLevel();
            if (kb.nKey.wasPressedThisFrame) levelManager.NextLevel();
            if (kb.mKey.wasPressedThisFrame) ScreenTransition.LoadScene(SceneNames.LevelSelect);
        }

        private void OnGUI()
        {
            if (levelManager == null || !visible) return;
            float scale = Mathf.Max(1f, Screen.height / 720f);
            label ??= new GUIStyle(GUI.skin.label) { richText = true };
            button ??= new GUIStyle(GUI.skin.button);
            label.fontSize = Mathf.RoundToInt(16 * scale);
            button.fontSize = Mathf.RoundToInt(16 * scale);

            LevelData level = levelManager.Level;
            string time = level == null ? "-" :
                level.HasTimeLimit ? $"{levelManager.RemainingSeconds:F1}s left" : $"{levelManager.ElapsedSeconds:F1}s";
            string limit = level != null && level.HasCollisionLimit ? $"/{level.CollisionLimit}" : "";
            string text =
                $"<b>{(level != null ? level.DisplayName : "No level")}</b>  [{levelManager.State}]\n" +
                $"Time {time}   Collisions {levelManager.Collisions}{limit}\n";
            if (validator != null)
                text += $"Parking: {validator.Status}  corners {validator.CornersInside}/4  " +
                        $"angle {validator.HeadingErrorDegrees:F1}°/{validator.ActiveRules.angleTolerance:F0}°  " +
                        $"speed {validator.SpeedKmh:F1}/{validator.ActiveRules.maxSpeedKmh:F0} km/h  hold {validator.HoldProgress * 100f:F0}%\n";
            if (levelManager.LastResult is LevelResult r)
            {
                int paid = progression != null && progression.LastReward is LevelReward lr ? lr.Coins : 0;
                text += r.Success ? $"<b>PARKED  {r.Stars}/3 stars   +{paid} coins saved</b>" : $"<b>FAILED: {r.FailReason}</b>";
            }

            float w = 620f * scale, pad = 10f * scale;
            GUI.Box(new Rect(pad, pad, w, 120f * scale), GUIContent.none);
            GUI.Label(new Rect(pad * 2f, pad * 1.5f, w - pad * 2f, 115f * scale), text, label);

            float bw = 130f * scale, bh = 44f * scale, by = pad * 2f + 120f * scale;
            if (GUI.Button(new Rect(pad, by, bw, bh), "Restart (R)", button)) levelManager.RestartLevel();
            GUI.enabled = levelManager.HasNextLevel;
            if (GUI.Button(new Rect(pad * 2f + bw, by, bw, bh), "Next (N)", button)) levelManager.NextLevel();
            GUI.enabled = true;
            if (GUI.Button(new Rect(pad * 3f + bw * 2f, by, bw, bh), "Levels (M)", button)) ScreenTransition.LoadScene(SceneNames.LevelSelect);
        }
    }
}
