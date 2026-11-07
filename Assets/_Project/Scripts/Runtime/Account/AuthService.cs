using System;
using System.Threading.Tasks;
using UnityEngine;

namespace UltimateCarParking.Account
{
    public enum AuthState { Starting, Guest, Google, SignedOut, Unavailable }

    /// <summary>The signed-in player as far as the game needs to know.</summary>
    public sealed class AuthUser
    {
        public string Uid = "";
        public bool IsAnonymous = true;
        public string DisplayName = "";
        public string Email = "";
    }

    /// <summary>Firebase Auth (or a stand-in) behind a small interface so the flow is testable without a network.</summary>
    public interface IAuthBackend
    {
        /// <summary>Starts the SDK; returns the restored session's user (null = nobody signed in).</summary>
        Task<AuthUser> Initialize();
        Task<AuthUser> SignInAnonymously();
        /// <summary>
        /// Signs in with a Google ID token. A guest is upgraded in place (same uid, keeps their progress); if that
        /// Google account already belongs to another player, that player is signed in instead.
        /// </summary>
        Task<AuthUser> SignInWithGoogle(string idToken);
        void SignOut();
    }

    /// <summary>Gets a Google ID token from the platform's account picker.</summary>
    public interface IGoogleSignIn
    {
        bool Supported { get; }
        Task<GoogleSignInResult> SignIn();
        void SignOut();
    }

    public readonly struct GoogleSignInResult
    {
        public readonly bool Ok;
        public readonly string IdToken, DisplayName, Email, ErrorCode, ErrorMessage;
        public GoogleSignInResult(string idToken, string name, string email)
        { Ok = true; IdToken = idToken; DisplayName = name; Email = email; ErrorCode = ""; ErrorMessage = ""; }
        public GoogleSignInResult(string code, string message)
        { Ok = false; IdToken = DisplayName = Email = ""; ErrorCode = code; ErrorMessage = message; }
        public bool Cancelled => ErrorCode == "cancelled";
    }

    /// <summary>
    /// Player accounts: every player is signed in anonymously as a guest on first launch (Firebase Auth); "Sign in
    /// with Google" links the guest to their Google account, and signing out returns to a fresh guest. On Android the
    /// backend is Firebase Auth + Credential Manager; in the editor and tests a local stand-in is used, so play mode
    /// never creates users in the live Firebase project.
    /// </summary>
    public static class AuthService
    {
        public static AuthState State { get; private set; } = AuthState.Starting;
        public static AuthUser User { get; private set; }
        public static bool Busy { get; private set; }
        /// <summary>Last problem worth showing ("Sign-in cancelled", "No network"…). "" when fine.</summary>
        public static string LastError { get; private set; } = "";
        public static event Action Changed;

        private static IAuthBackend backend;
        private static IGoogleSignIn google;
        private static Task initTask;

        public static bool GoogleSupported => google != null && google.Supported;

        /// <summary>Replaces the backends (tests). Resets the state; call <see cref="Initialize"/> after.</summary>
        public static void UseBackends(IAuthBackend authBackend, IGoogleSignIn googleSignIn)
        {
            backend = authBackend;
            google = googleSignIn;
            initTask = null;
            State = AuthState.Starting;
            User = null;
            LastError = "";
            Busy = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            backend = null; google = null; initTask = null; Changed = null;
            State = AuthState.Starting; User = null; LastError = ""; Busy = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart() => _ = Initialize();

        /// <summary>Starts the SDK and makes sure someone is signed in (a guest if needed). Safe to call repeatedly.</summary>
        public static Task Initialize()
        {
            if (initTask != null) return initTask;
            if (backend == null)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                backend = new FirebaseAuthBackend();
                google = new AndroidGoogleSignIn();
#else
                backend = new LocalAuthBackend();
                google = new UnsupportedGoogleSignIn();
#endif
            }
            initTask = InitializeAsync();
            return initTask;
        }

        private static async Task InitializeAsync()
        {
            Busy = true;
            try
            {
                AuthUser restored = await backend.Initialize();
                Set(restored ?? await backend.SignInAnonymously(), "");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Auth] start failed: " + e.Message);
                State = AuthState.Unavailable;
                User = null;
                LastError = "Account service unavailable. Playing offline.";
            }
            finally { Busy = false; Changed?.Invoke(); }
        }

        /// <summary>Shows Google's account picker and signs in (upgrading the guest when there is one).</summary>
        public static async Task<bool> SignInWithGoogle()
        {
            await Initialize();
            if (Busy) return false;
            if (!GoogleSupported) { LastError = "Google sign-in is available on Android."; Changed?.Invoke(); return false; }
            Busy = true; LastError = ""; Changed?.Invoke();
            try
            {
                GoogleSignInResult g = await google.SignIn();
                if (!g.Ok)
                {
                    LastError = g.Cancelled ? "" : g.ErrorCode == "no_account" ? "No Google account on this device." : "Google sign-in failed.";
                    if (!g.Cancelled) Debug.LogWarning($"[Auth] Google sign-in: {g.ErrorCode} {g.ErrorMessage}");
                    return false;
                }
                AuthUser user = await backend.SignInWithGoogle(g.IdToken);
                if (string.IsNullOrEmpty(user.DisplayName)) user.DisplayName = g.DisplayName;
                if (string.IsNullOrEmpty(user.Email)) user.Email = g.Email;
                Set(user, "");
                Debug.Log($"[Auth] signed in with Google ({Mask(user.Email)})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Auth] Google sign-in failed: " + e.Message);
                LastError = "Google sign-in failed. Check your connection.";
                return false;
            }
            finally { Busy = false; Changed?.Invoke(); }
        }

        /// <summary>Signs out of Google and continues as a new guest.</summary>
        public static async Task SignOut()
        {
            await Initialize();
            if (Busy) return;
            Busy = true; LastError = "";
            try
            {
                backend.SignOut();
                google?.SignOut();
                State = AuthState.SignedOut; User = null; Changed?.Invoke();
                Set(await backend.SignInAnonymously(), "");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Auth] sign-out/guest failed: " + e.Message);
                State = AuthState.SignedOut;
                LastError = "Signed out. Couldn't start a guest session.";
            }
            finally { Busy = false; Changed?.Invoke(); }
        }

        private static void Set(AuthUser user, string error)
        {
            User = user;
            State = user == null ? AuthState.SignedOut : user.IsAnonymous ? AuthState.Guest : AuthState.Google;
            LastError = error;
        }

        /// <summary>"pr…@gmail.com" for logs (never log full addresses).</summary>
        public static string Mask(string email)
        {
            if (string.IsNullOrEmpty(email)) return "";
            int at = email.IndexOf('@');
            return at <= 2 ? "…" + email.Substring(Mathf.Max(0, at)) : email.Substring(0, 2) + "…" + email.Substring(at);
        }

        /// <summary>Short label for the menu: "GUEST", or the Google first name.</summary>
        public static string ShortName()
        {
            if (State == AuthState.Google && User != null)
            {
                string n = !string.IsNullOrEmpty(User.DisplayName) ? User.DisplayName.Split(' ')[0] : User.Email.Split('@')[0];
                return n.ToUpperInvariant();
            }
            return State == AuthState.Unavailable ? "OFFLINE" : "GUEST";
        }
    }
}
