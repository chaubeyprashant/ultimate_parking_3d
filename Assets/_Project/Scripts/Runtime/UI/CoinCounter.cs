using TMPro;
using UnityEngine;
using UltimateCarParking.Save;

namespace UltimateCarParking.UI
{
    /// <summary>Shows the saved coin balance and rolls the number when it changes.</summary>
    public sealed class CoinCounter : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0.05f)] private float rollSeconds = 0.6f;

        private float shown;
        private int target;
        private float velocity;

        private void OnEnable()
        {
            var plate = GetComponent<UnityEngine.UI.Image>();
            if (plate != null) plate.color = new Color(1f, 0.82f, 0.16f, 1f);
            if (label != null) label.color = new Color(0.07f, 0.08f, 0.09f, 1f);
            SaveManager.Current.Saved += OnSaved;
            target = SaveManager.Current.Data.coins;
            shown = target;
            Render();
        }

        private void OnDisable() => SaveManager.Current.Saved -= OnSaved;

        private void OnSaved(SaveData data) => target = data.coins;

        /// <summary>Re-read the balance without a save event (after code changed coins directly).</summary>
        public void Refresh() => target = SaveManager.Current.Data.coins;

        private void Update()
        {
            if (Mathf.Approximately(shown, target)) return;
            shown = Mathf.SmoothDamp(shown, target, ref velocity, rollSeconds * 0.35f, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(shown - target) < 0.5f) shown = target;
            Render();
        }

        private void Render()
        {
            if (label != null) label.text = Mathf.RoundToInt(shown).ToString("N0");
        }
    }
}
