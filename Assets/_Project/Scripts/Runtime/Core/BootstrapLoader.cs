using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UltimateCarParking.Core
{
    /// <summary>
    /// Bootstrap scene (build index 0): shows the in-game splash, then loads the main menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BootstrapLoader : MonoBehaviour
    {
        [SerializeField] private string firstScene = SceneNames.MainMenu;
        [SerializeField] private CanvasGroup splash;
        [SerializeField, Min(0f)] private float minSplashSeconds = 2.1f;
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 0.45f;
        [Tooltip("Frame-rate cap on device. -1 = platform default.")]
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool keepScreenOn = true;

        private void Start() => StartCoroutine(Boot());

        private IEnumerator Boot()
        {
            Application.targetFrameRate = targetFrameRate;
            if (keepScreenOn) Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (string.IsNullOrEmpty(firstScene) || !Application.CanStreamedLevelBeLoaded(firstScene))
            {
                Debug.LogError($"[BootstrapLoader] Scene '{firstScene}' is not in Build Settings.", this);
                yield break;
            }

            if (splash != null)
            {
                splash.alpha = 1f;
                splash.gameObject.SetActive(true);
            }

            for (float t = 0f; t < minSplashSeconds; t += Time.unscaledDeltaTime)
                yield return null;

            if (splash != null)
            {
                for (float t = 0f; t < fadeOutSeconds; t += Time.unscaledDeltaTime)
                {
                    splash.alpha = 1f - t / fadeOutSeconds;
                    yield return null;
                }
                splash.alpha = 0f;
            }

            SceneManager.LoadScene(firstScene);
        }
    }
}
