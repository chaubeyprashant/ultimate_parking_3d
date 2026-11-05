using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UltimateCarParking.UI
{
    /// <summary>Brief full-screen edge tint (collision, failure). Idle costs nothing: a coroutine runs only while fading.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class ScreenFlash : MonoBehaviour
    {
        private Image image;
        private Coroutine running;

        private void Awake()
        {
            image = GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;
        }

        public void Flash(Color color, float peakAlpha = 0.35f, float seconds = 0.45f)
        {
            if (image == null) return;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Run(color, peakAlpha, seconds));
        }

        private IEnumerator Run(Color c, float peak, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float a = peak * (1f - UiMotion.EaseOutCubic(t / seconds));
                image.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
            image.color = Color.clear;
            running = null;
        }
    }
}
