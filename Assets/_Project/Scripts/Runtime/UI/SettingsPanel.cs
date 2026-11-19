using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Account;
using UltimateCarParking.Quality;
using UltimateCarParking.Settings;

namespace UltimateCarParking.UI
{
    /// <summary>Music and sound sliders, graphics, account, and the DRIVING CONTROLS button (opens <see cref="ControlsPanel"/>).
    /// Changes apply live; the save is written on close.</summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Slider music;
        [SerializeField] private Slider sound;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private TMP_Text soundValue;
        [SerializeField] private Button closeButton;
        [Header("Controls")]
        [SerializeField] private Button controlsButton;
        [SerializeField] private ControlsPanel controlsPanel;
        [Header("Graphics")]
        [SerializeField] private Button graphicsButton;
        [SerializeField] private TMP_Text graphicsLabel;
        [SerializeField] private Button frameRateButton;
        [SerializeField] private TMP_Text frameRateLabel;
        [Header("Account")]
        [SerializeField] private TMP_Text accountValue;
        [SerializeField] private Button accountButton;
        [SerializeField] private TMP_Text accountButtonLabel;
        [SerializeField] private Button signOutButton;
        [SerializeField] private TMP_Text signOutLabel;

        public bool IsOpen => gameObject.activeSelf;
        public ControlsPanel Controls => controlsPanel;

        private void Awake()
        {
            if (music != null) music.onValueChanged.AddListener(v => { SettingsService.SetMusicVolume(v); Render(); });
            if (sound != null) sound.onValueChanged.AddListener(v => { SettingsService.SetSoundVolume(v); Render(); });
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (controlsButton != null && controlsPanel != null) controlsButton.onClick.AddListener(controlsPanel.Open);
            if (graphicsButton != null) graphicsButton.onClick.AddListener(() =>
            {
                SettingsService.SetGraphicsMode(QualityModes.NextGraphics(SettingsService.Current.graphicsMode));
                RenderGraphics();
            });
            if (frameRateButton != null) frameRateButton.onClick.AddListener(() =>
            {
                SettingsService.SetFrameRateMode(QualityModes.NextFps(SettingsService.Current.frameRateMode));
                RenderGraphics();
            });
            if (accountButton != null) accountButton.onClick.AddListener(() =>
            {
                if (AuthService.State == AuthState.Google) _ = AuthService.SignOut(); else _ = AuthService.SignInWithGoogle();
            });
            if (signOutButton != null) signOutButton.onClick.AddListener(() => _ = AuthService.SignOut());
            gameObject.SetActive(false);
        }

        private void OnEnable() { AuthService.Changed += RenderAccount; RenderAccount(); }
        private void OnDisable() => AuthService.Changed -= RenderAccount;

        /// <summary>Account row: who is signed in, and one clear button — Sign in with Google or Sign out.</summary>
        private void RenderAccount()
        {
            AuthState st = AuthService.State;
            bool busy = AuthService.Busy;
            bool google = st == AuthState.Google;
            if (accountValue != null)
            {
                AuthUser u = AuthService.User;
                accountValue.text = google && u != null ? (string.IsNullOrEmpty(u.Email) ? AuthService.ShortName() : u.Email)
                    : st == AuthState.Unavailable ? "Offline" : "Guest (not signed in)";
            }
            if (signOutButton != null)
            {
                if (accountButton != null) accountButton.interactable = !busy && !google && st != AuthState.Unavailable && AuthService.GoogleSupported;
                if (accountButtonLabel != null)
                    accountButtonLabel.text = busy && !google ? "PLEASE WAIT…" : "SIGN IN WITH GOOGLE";
                signOutButton.gameObject.SetActive(true);
                signOutButton.interactable = !busy && st != AuthState.Starting && st != AuthState.Unavailable;
                if (signOutLabel != null) signOutLabel.text = busy ? "PLEASE WAIT…" : "SIGN OUT";
            }
            else
            {
                if (accountButton != null) accountButton.interactable = !busy && st != AuthState.Unavailable && (google || AuthService.GoogleSupported);
                if (accountButtonLabel != null)
                    accountButtonLabel.text = busy ? "PLEASE WAIT…" : google ? "SIGN OUT" : "SIGN IN WITH GOOGLE";
            }
        }

        public void Open()
        {
            GameSettings s = SettingsService.Current;
            if (music != null) music.SetValueWithoutNotify(s.musicVolume);
            if (sound != null) sound.SetValueWithoutNotify(s.soundVolume);
            Render();
            gameObject.SetActive(true);
            if (group != null) StartCoroutine(UiMotion.Fade(group, 0f, 1f, 0.2f));
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            if (controlsPanel != null) controlsPanel.Close();
            SettingsService.Commit();
            gameObject.SetActive(false);
        }

        /// <summary>Graphics and frame-rate buttons: what is chosen, and in Auto what that resolved to on this phone.</summary>
        private void RenderGraphics()
        {
            GameSettings s = SettingsService.Current;
            if (graphicsLabel != null)
            {
                string name = QualityModes.GraphicsName(s.graphicsMode);
                graphicsLabel.text = s.graphicsMode == QualityModes.GraphicsAuto && QualityService.Initialized
                    ? $"GRAPHICS: AUTO ({QualityService.ActiveTier.ToString().ToUpperInvariant()})" : $"GRAPHICS: {name}";
            }
            if (frameRateLabel != null)
            {
                string name = QualityModes.FpsName(s.frameRateMode);
                frameRateLabel.text = s.frameRateMode == QualityModes.FpsAuto && QualityService.Initialized
                    ? $"FRAME RATE: AUTO ({QualityService.TargetFps})" : $"FRAME RATE: {name}";
            }
        }

        private void Render()
        {
            RenderGraphics();
            GameSettings s = SettingsService.Current;
            if (musicValue != null) musicValue.text = $"{Mathf.RoundToInt(s.musicVolume * 100f)}%";
            if (soundValue != null) soundValue.text = $"{Mathf.RoundToInt(s.soundVolume * 100f)}%";
        }
    }
}
