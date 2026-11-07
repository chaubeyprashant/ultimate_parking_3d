using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.CameraSystem;
using UltimateCarParking.Levels;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// HUD backup-camera panel: shows the <see cref="RearViewCamera"/> image (mirrored like a real car's), the
    /// steering-dependent guide lines and the free distance behind the bumper. Fades in when the car reverses.
    /// Attaches the rear camera to whichever car the level spawns.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RearCameraView : MonoBehaviour
    {
        [SerializeField] private RearViewCamera rearCamera;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RawImage image;
        [SerializeField] private RearGuideLines guides;
        [SerializeField] private TMP_Text distanceLabel;
        [SerializeField] private Image distancePill;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.15f;

        private UltimateCarParking.Vehicle.VehicleController attachedTo;

        public bool Showing => group != null && group.alpha > 0.5f;
        public RearViewCamera RearCamera => rearCamera;
        public string DistanceText => distanceLabel != null ? distanceLabel.text : "";

        private void Awake()
        {
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
        }

        private void LateUpdate()
        {
            if (rearCamera == null) return;
            var loader = LevelLoader.Instance;
            var car = loader != null ? loader.Vehicle : null;
            if (car != attachedTo)
            {
                attachedTo = car;
                if (car != null) rearCamera.Attach(car); else rearCamera.Detach();
                if (image != null) image.texture = rearCamera.Texture;
                if (image != null) image.uvRect = rearCamera.Mirrored ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
            }
            bool playing = LevelManager.Instance == null || LevelManager.Instance.State == LevelState.Playing;
            bool show = playing && rearCamera.Active;
            if (group != null)
            {
                float target = show ? 1f : 0f;
                group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / fadeSeconds);
            }
            if (!show) return;
            if (guides != null) guides.Refresh(rearCamera);
            float d = rearCamera.RearDistance;
            if (distanceLabel != null) distanceLabel.text = float.IsInfinity(d) ? "CLEAR" : $"{d:0.0} m";
            if (distancePill != null)
                distancePill.color = float.IsInfinity(d) || d > 2f ? UiPalette.Lime : d > 1f ? new Color(1f, 0.78f, 0.1f) : UiPalette.Danger;
            if (distanceLabel != null) distanceLabel.color = UiPalette.TextOnLime;
        }
    }
}
