using System;
using System.Threading.Tasks;
using UnityEngine;

namespace UltimateCarParking.Account
{
    /// <summary>
    /// Firebase Auth. The session persists across launches (Firebase keeps the user); anonymous users are upgraded
    /// to Google with LinkWithCredential so their uid stays the same.
    /// </summary>
    public sealed class FirebaseAuthBackend : IAuthBackend
    {
        private Firebase.Auth.FirebaseAuth auth;

        public async Task<AuthUser> Initialize()
        {
            Firebase.DependencyStatus status = await Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != Firebase.DependencyStatus.Available) throw new Exception("Firebase dependencies: " + status);
            auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            return From(auth.CurrentUser);
        }

        public async Task<AuthUser> SignInAnonymously()
        {
            Firebase.Auth.AuthResult r = await auth.SignInAnonymouslyAsync();
            return From(r.User);
        }

        public async Task<AuthUser> SignInWithGoogle(string idToken)
        {
            Firebase.Auth.Credential credential = Firebase.Auth.GoogleAuthProvider.GetCredential(idToken, null);
            Firebase.Auth.FirebaseUser current = auth.CurrentUser;
            if (current != null && current.IsAnonymous)
            {
                try
                {
                    Firebase.Auth.AuthResult linked = await current.LinkWithCredentialAsync(credential);
                    return From(linked.User);
                }
                catch (Exception e) when (IsCredentialInUse(e))
                {
                    // This Google account already has a player: switch to it (the guest session is left behind).
                    credential = Firebase.Auth.GoogleAuthProvider.GetCredential(idToken, null);
                }
            }
            Firebase.Auth.FirebaseUser user = await auth.SignInWithCredentialAsync(credential);
            return From(user);
        }

        public void SignOut() => auth?.SignOut();

        private static bool IsCredentialInUse(Exception e)
        {
            for (Exception x = e; x != null; x = x.InnerException)
                if (x is Firebase.FirebaseException fe &&
                    ((Firebase.Auth.AuthError)fe.ErrorCode == Firebase.Auth.AuthError.CredentialAlreadyInUse ||
                     (Firebase.Auth.AuthError)fe.ErrorCode == Firebase.Auth.AuthError.AccountExistsWithDifferentCredentials))
                    return true;
            if (e is AggregateException ae) foreach (Exception inner in ae.InnerExceptions) if (IsCredentialInUse(inner)) return true;
            return false;
        }

        private static AuthUser From(Firebase.Auth.FirebaseUser u) => u == null ? null : new AuthUser
        {
            Uid = u.UserId, IsAnonymous = u.IsAnonymous, DisplayName = u.DisplayName ?? "", Email = u.Email ?? "",
        };
    }

    /// <summary>Editor / test stand-in: a local guest, upgraded to "Google" when given a token. No network.</summary>
    public sealed class LocalAuthBackend : IAuthBackend
    {
        private AuthUser current;
        private int next = 1;
        /// <summary>Token → existing Google user (simulates "this Google account already has a player").</summary>
        public readonly System.Collections.Generic.Dictionary<string, AuthUser> GoogleUsers = new System.Collections.Generic.Dictionary<string, AuthUser>();
        public bool FailNext { get; set; }

        public Task<AuthUser> Initialize() => Task.FromResult(current);

        public Task<AuthUser> SignInAnonymously()
        {
            if (FailNext) { FailNext = false; throw new Exception("offline"); }
            current = new AuthUser { Uid = "local-guest-" + next++, IsAnonymous = true };
            return Task.FromResult(current);
        }

        public Task<AuthUser> SignInWithGoogle(string idToken)
        {
            if (GoogleUsers.TryGetValue(idToken, out AuthUser existing)) { current = existing; return Task.FromResult(existing); }
            if (current == null || !current.IsAnonymous) current = new AuthUser { Uid = "local-google-" + next++ };
            current.IsAnonymous = false;                      // upgrade in place: same uid
            current.DisplayName = "Local Player";
            current.Email = "player@example.com";
            GoogleUsers[idToken] = current;
            return Task.FromResult(current);
        }

        public void SignOut() => current = null;
    }

    public sealed class UnsupportedGoogleSignIn : IGoogleSignIn
    {
        public bool Supported => false;
        public Task<GoogleSignInResult> SignIn() => Task.FromResult(new GoogleSignInResult("unsupported", "Google sign-in needs Android"));
        public void SignOut() { }
    }

    /// <summary>Test double: returns a fixed token (or an error code).</summary>
    public sealed class FakeGoogleSignIn : IGoogleSignIn
    {
        public string Token = "google-token-1";
        public string ErrorCode = "";
        public bool Supported => true;
        public int SignOuts { get; private set; }
        public Task<GoogleSignInResult> SignIn() => Task.FromResult(string.IsNullOrEmpty(ErrorCode)
            ? new GoogleSignInResult(Token, "Local Player", "player@example.com")
            : new GoogleSignInResult(ErrorCode, "fake error"));
        public void SignOut() => SignOuts++;
    }

    /// <summary>Android: AndroidX Credential Manager via the GoogleSignInBridge Java plugin.</summary>
    public sealed class AndroidGoogleSignIn : IGoogleSignIn
    {
        public bool Supported => Application.platform == RuntimePlatform.Android;

        private sealed class Callback : AndroidJavaProxy
        {
            private readonly TaskCompletionSource<GoogleSignInResult> tcs;
            public Callback(TaskCompletionSource<GoogleSignInResult> tcs) : base("com.ultimateparking.auth.GoogleSignInBridge$Callback") => this.tcs = tcs;
            // Called from a Java background thread; the awaiting code resumes on Unity's main thread.
            public void onSuccess(string idToken, string displayName, string email) => tcs.TrySetResult(new GoogleSignInResult(idToken, displayName, email));
            public void onError(string code, string message) => tcs.TrySetResult(new GoogleSignInResult(code, message));
        }

        private static AndroidJavaObject Activity()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        public Task<GoogleSignInResult> SignIn()
        {
            var tcs = new TaskCompletionSource<GoogleSignInResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                using var bridge = new AndroidJavaClass("com.ultimateparking.auth.GoogleSignInBridge");
                bridge.CallStatic("signIn", Activity(), new Callback(tcs));
            }
            catch (Exception e) { tcs.TrySetResult(new GoogleSignInResult("exception", e.Message)); }
            return tcs.Task;
        }

        public void SignOut()
        {
            try
            {
                using var bridge = new AndroidJavaClass("com.ultimateparking.auth.GoogleSignInBridge");
                bridge.CallStatic("signOut", Activity());
            }
            catch (Exception e) { Debug.LogWarning("[Auth] Google sign-out: " + e.Message); }
        }
    }
}
