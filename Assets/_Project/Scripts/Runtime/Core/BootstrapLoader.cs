using UnityEngine;
using UnityEngine.SceneManagement;

namespace UltimateCarParking.Core
{
    /// <summary>
    /// Lives in the Bootstrap scene (build index 0). Applies app-wide runtime settings once,
    /// then loads the first scene (the main menu).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BootstrapLoader : MonoBehaviour
    {
        [SerializeField] private string firstScene = SceneNames.MainMenu;
        [Tooltip("Frame-rate cap on device. -1 = platform default (30 on Android).")]
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool keepScreenOn = true;

        private void Start()
        {
            Application.targetFrameRate = targetFrameRate;
            if (keepScreenOn) Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (string.IsNullOrEmpty(firstScene) || !Application.CanStreamedLevelBeLoaded(firstScene))
            {
                Debug.LogError($"[BootstrapLoader] Scene '{firstScene}' is not in Build Settings.", this);
                return;
            }
            SceneManager.LoadScene(firstScene);
        }
    }
}
