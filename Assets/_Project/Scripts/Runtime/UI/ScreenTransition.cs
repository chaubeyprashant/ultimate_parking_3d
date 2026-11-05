using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// App-wide fade-to-dark scene transition. Created on first use and kept across scenes (exactly one).
    /// Blocks input while a transition runs; a second request during a transition is ignored.
    /// </summary>
    public sealed class ScreenTransition : MonoBehaviour
    {
        private static ScreenTransition instance;

        private CanvasGroup group;
        private bool busy;

        public static bool IsTransitioning => instance != null && instance.busy;

        public static void LoadScene(string sceneName, float fadeSeconds = 0.3f)
        {
            if (instance == null) Create();
            if (instance.busy) return;
            instance.StartCoroutine(instance.Run(sceneName, fadeSeconds));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private static void Create()
        {
            var go = new GameObject("ScreenTransition");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            instance = go.AddComponent<ScreenTransition>();
            instance.group = go.AddComponent<CanvasGroup>();
            instance.group.alpha = 0f;
            instance.group.blocksRaycasts = false;
            go.AddComponent<GraphicRaycaster>();

            var veil = new GameObject("Veil", typeof(RectTransform), typeof(Image));
            veil.transform.SetParent(go.transform, false);
            var rt = (RectTransform)veil.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            veil.GetComponent<Image>().color = UiPalette.Background;
        }

        private IEnumerator Run(string sceneName, float fade)
        {
            busy = true;
            group.blocksRaycasts = true;
            yield return UiMotion.Fade(group, group.alpha, 1f, fade);
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[ScreenTransition] Scene '{sceneName}' is not in Build Settings.");
            }
            else
            {
                while (!op.isDone) yield return null;
            }
            yield return null;
            yield return UiMotion.Fade(group, 1f, 0f, fade);
            group.blocksRaycasts = false;
            busy = false;
        }
    }
}
