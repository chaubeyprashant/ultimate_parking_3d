using UnityEngine;
using UnityEngine.InputSystem;

namespace UltimateCarParking.UI
{
    /// <summary>Android back button / Escape goes to a scene (or quits from the main menu).</summary>
    public sealed class BackNavigation : MonoBehaviour
    {
        [Tooltip("Scene to open on Back. Empty = quit the app.")]
        [SerializeField] private string targetScene;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || ScreenTransition.IsTransitioning) return;
            if (string.IsNullOrEmpty(targetScene))
            {
                if (!Application.isEditor) Application.Quit();
            }
            else
            {
                ScreenTransition.LoadScene(targetScene);
            }
        }
    }
}
