using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UltimateCarParking.Account;
using UltimateCarParking.Audio;

namespace UltimateCarParking.UI
{
    /// <summary>
    /// Main-menu account pill ("GUEST · SIGN IN" / the player's first name) and the account panel it opens:
    /// Sign in with Google (upgrades the guest), Sign out (back to a new guest), Close. Follows
    /// <see cref="AuthService"/> state, including busy and error messages.
    /// </summary>
    public sealed class AccountPanel : MonoBehaviour
    {
        [SerializeField] private Button accountButton;
        [SerializeField] private TMP_Text accountLabel;
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private TMP_Text errorLabel;
        [SerializeField] private Button googleButton;
        [SerializeField] private TMP_Text googleLabel;
        [SerializeField] private Button signOutButton;
        [SerializeField] private Button closeButton;

        public bool PanelOpen => panel != null && panel.gameObject.activeSelf;
        public string AccountText => accountLabel != null ? accountLabel.text : "";
        public string StatusText => statusLabel != null ? statusLabel.text : "";
        public bool GoogleButtonShown => googleButton != null && googleButton.gameObject.activeSelf;
        public bool SignOutShown => signOutButton != null && signOutButton.gameObject.activeSelf;

        private void Awake()
        {
            if (accountButton != null) accountButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (googleButton != null) googleButton.onClick.AddListener(() => _ = AuthService.SignInWithGoogle());
            if (signOutButton != null) signOutButton.onClick.AddListener(() => _ = AuthService.SignOut());
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            AuthService.Changed += Refresh;
            _ = AuthService.Initialize();
            Refresh();
        }

        private void OnDisable() => AuthService.Changed -= Refresh;

        public void Open()
        {
            if (panel == null) return;
            panel.gameObject.SetActive(true);
            panel.alpha = 1f;
            AudioManager.Instance.Play(Sfx.Click, 0.6f);
            Refresh();
        }

        public void Close()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            AuthState s = AuthService.State;
            bool busy = AuthService.Busy;
            if (accountLabel != null)
                accountLabel.text = s == AuthState.Starting || (busy && s != AuthState.Google) ? "SIGNING IN…"
                    : s == AuthState.Google ? AuthService.ShortName()
                    : s == AuthState.Unavailable ? "OFFLINE" : "GUEST  ·  SIGN IN";
            if (statusLabel != null)
                statusLabel.text = s == AuthState.Google ? "SIGNED IN WITH GOOGLE" : s == AuthState.Unavailable ? "OFFLINE" : "PLAYING AS GUEST";
            if (detailLabel != null)
            {
                AuthUser u = AuthService.User;
                detailLabel.text = s == AuthState.Google && u != null
                    ? $"{(string.IsNullOrEmpty(u.DisplayName) ? "Google player" : u.DisplayName)}\n<color=#8C96A3>{u.Email}</color>"
                    : s == AuthState.Unavailable
                        ? "The account service can't be reached. You can keep playing; try again later."
                        : "Sign in with Google to link this player to your Google account. Your progress is saved on this device.";
            }
            if (errorLabel != null) errorLabel.text = AuthService.LastError;
            bool google = s == AuthState.Google;
            if (googleButton != null)
            {
                googleButton.gameObject.SetActive(!google);
                googleButton.interactable = !busy && AuthService.GoogleSupported;
            }
            if (googleLabel != null)
                googleLabel.text = busy ? "SIGNING IN…" : AuthService.GoogleSupported ? "SIGN IN WITH GOOGLE" : "GOOGLE SIGN-IN: ANDROID ONLY";
            if (signOutButton != null)
            {
                signOutButton.gameObject.SetActive(google);
                signOutButton.interactable = !busy;
            }
        }
    }
}
