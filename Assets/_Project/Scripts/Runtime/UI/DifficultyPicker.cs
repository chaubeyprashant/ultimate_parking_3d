using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Audio;
using UltimateCarParking.Levels;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// EASY / MEDIUM / HARD segmented picker. Every copy (main menu, missions, World Tour, career) edits the same remembered
    /// setting, so the choice follows the player from screen to screen and into PLAY.
    /// </summary>
    public sealed class DifficultyPicker : MonoBehaviour
    {
        [Tooltip("EASY, MEDIUM, HARD (in that order).")]
        [SerializeField] private Button[] buttons = new Button[0];

        public int ButtonCount => buttons.Length;

        private void Awake()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var d = (Difficulty)i;
                if (buttons[i] != null) buttons[i].onClick.AddListener(() => Pick(d));
            }
        }

        private void OnEnable() { PlayDifficulty.Changed += Paint; Paint(); }
        private void OnDisable() => PlayDifficulty.Changed -= Paint;

        public void Pick(Difficulty d)
        {
            if (PlayDifficulty.Current == d) return;
            PlayDifficulty.Current = d;
            AudioManager.Instance.Play(Sfx.Click, 0.6f);
        }

        public void Paint()
        {
            int cur = (int)PlayDifficulty.Current;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                bool on = i == cur;
                buttons[i].GetComponent<Image>().color = on ? UiPalette.Lime : UiPalette.SurfaceRaised;
                TMP_Text t = buttons[i].GetComponentInChildren<TMP_Text>();
                if (t != null) t.color = on ? UiPalette.TextOnLime : UiPalette.TextPrimary;
            }
        }
    }
}
