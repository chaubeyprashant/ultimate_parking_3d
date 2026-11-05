using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Levels;
using UltimateCarParking.Progression;

namespace UltimateCarParking.UI
{
    /// <summary>One level tile: number, stars and a clear locked / unlocked / completed look.</summary>
    [DisallowMultipleComponent]
    public sealed class LevelCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private TMP_Text numberLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Image[] stars = Array.Empty<Image>();
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private GameObject starRow;

        public LevelData Level { get; private set; }
        public LevelLockState State { get; private set; }
        public int Stars { get; private set; }
        public Button Button => button;

        private Action<LevelCard> onClick;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void Bind(LevelData level, LevelLockState state, int bestStars, float bestTime, Action<LevelCard> clicked)
        {
            Level = level;
            State = state;
            Stars = bestStars;
            onClick = clicked;
            name = $"LevelCard_{level.LevelId}";

            bool locked = state == LevelLockState.Locked;
            bool done = state == LevelLockState.Completed;

            if (numberLabel != null)
            {
                numberLabel.text = level.LevelNumber.ToString("00");
                numberLabel.color = locked ? UiPalette.Locked : done ? UiPalette.TextOnLime : UiPalette.TextPrimary;
            }
            if (background != null) background.color = done ? UiPalette.Lime : locked ? UiPalette.Surface : UiPalette.SurfaceRaised;
            if (border != null) border.color = locked ? UiPalette.Outline : UiPalette.Lime;
            if (lockIcon != null) lockIcon.SetActive(locked);
            if (starRow != null) starRow.SetActive(!locked);
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;
                bool lit = i < bestStars;
                stars[i].color = done ? (lit ? UiPalette.TextOnLime : UiPalette.TextOnLime.WithAlpha(0.25f))
                                      : UiPalette.TextSecondary.WithAlpha(0.35f);
            }
            if (statusLabel != null)
            {
                statusLabel.text = locked ? "LOCKED" : done ? (bestTime >= 0f ? $"BEST {bestTime:0.0}s" : "CLEARED") : "PLAY";
                statusLabel.color = locked ? UiPalette.Locked : done ? UiPalette.TextOnLime.WithAlpha(0.8f) : UiPalette.Lime;
            }
        }
    }
}
