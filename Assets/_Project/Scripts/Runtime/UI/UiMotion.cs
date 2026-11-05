using System.Collections;
using UnityEngine;

namespace UltimateCarParking.UI
{
    /// <summary>Small coroutine tweens (unscaled time) used for screen and element transitions.</summary>
    public static class UiMotion
    {
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        public static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t = Mathf.Clamp01(t);
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration, float delay = 0f)
        {
            if (group == null) yield break;
            group.alpha = from;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (group == null) yield break;
                group.alpha = Mathf.Lerp(from, to, EaseOutCubic(t / duration));
                yield return null;
            }
            if (group != null) group.alpha = to;
        }

        public static IEnumerator Move(RectTransform rt, Vector2 from, Vector2 to, float duration, float delay = 0f)
        {
            if (rt == null) yield break;
            rt.anchoredPosition = from;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (rt == null) yield break;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, EaseOutCubic(t / duration));
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = to;
        }

        public static IEnumerator Scale(Transform tf, float from, float to, float duration, float delay = 0f, bool back = false)
        {
            if (tf == null) yield break;
            tf.localScale = Vector3.one * from;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (tf == null) yield break;
                float k = back ? EaseOutBack(t / duration) : EaseOutCubic(t / duration);
                tf.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, k);
                yield return null;
            }
            if (tf != null) tf.localScale = Vector3.one * to;
        }

        /// <summary>Horizontal shake for "not allowed" feedback (locked level, not enough coins).</summary>
        public static IEnumerator Shake(RectTransform rt, float amplitude = 14f, float duration = 0.35f)
        {
            if (rt == null) yield break;
            Vector2 origin = rt.anchoredPosition;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (rt == null) yield break;
                float k = 1f - t / duration;
                rt.anchoredPosition = origin + Vector2.right * (Mathf.Sin(t * 60f) * amplitude * k);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = origin;
        }
    }
}
