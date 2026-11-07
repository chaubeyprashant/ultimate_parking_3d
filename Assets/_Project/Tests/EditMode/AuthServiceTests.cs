using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using UltimateCarParking.Account;

namespace UltimateCarParking.Tests
{
    /// <summary>Account flow with the local backend + fake Google picker (no network, no Firebase users created).</summary>
    public class AuthServiceTests
    {
        private LocalAuthBackend backend;
        private FakeGoogleSignIn google;

        [SetUp]
        public void SetUp()
        {
            backend = new LocalAuthBackend();
            google = new FakeGoogleSignIn();
            AuthService.UseBackends(backend, google);
        }

        private static IEnumerator Wait(Task t)
        {
            int frames = 0;
            while (!t.IsCompleted && frames++ < 600) yield return null;
            Assert.IsTrue(t.IsCompleted, "task finished");
            if (t.IsFaulted) throw t.Exception;
        }

        [UnityTest]
        public IEnumerator First_Launch_Signs_In_As_A_Guest()
        {
            yield return Wait(AuthService.Initialize());
            Assert.AreEqual(AuthState.Guest, AuthService.State);
            Assert.IsTrue(AuthService.User.IsAnonymous);
            Assert.AreEqual("GUEST", AuthService.ShortName());
        }

        [UnityTest]
        public IEnumerator Google_Sign_In_Upgrades_The_Guest_Keeping_The_Same_Player()
        {
            yield return Wait(AuthService.Initialize());
            string guestUid = AuthService.User.Uid;
            Task<bool> t = AuthService.SignInWithGoogle();
            yield return Wait(t);
            Assert.IsTrue(t.Result);
            Assert.AreEqual(AuthState.Google, AuthService.State);
            Assert.AreEqual(guestUid, AuthService.User.Uid, "guest linked, not replaced");
            Assert.AreEqual("LOCAL", AuthService.ShortName());
            Assert.AreEqual("", AuthService.LastError);
        }

        [UnityTest]
        public IEnumerator Google_Account_That_Already_Has_A_Player_Switches_To_It()
        {
            var existing = new AuthUser { Uid = "existing-google", IsAnonymous = false, DisplayName = "Old Player", Email = "old@example.com" };
            backend.GoogleUsers[google.Token] = existing;
            yield return Wait(AuthService.Initialize());
            yield return Wait(AuthService.SignInWithGoogle());
            Assert.AreEqual("existing-google", AuthService.User.Uid);
            Assert.AreEqual(AuthState.Google, AuthService.State);
        }

        [UnityTest]
        public IEnumerator Sign_Out_Returns_To_A_New_Guest()
        {
            yield return Wait(AuthService.Initialize());
            yield return Wait(AuthService.SignInWithGoogle());
            string googleUid = AuthService.User.Uid;
            yield return Wait(AuthService.SignOut());
            Assert.AreEqual(AuthState.Guest, AuthService.State);
            Assert.AreNotEqual(googleUid, AuthService.User.Uid);
            Assert.AreEqual(1, google.SignOuts, "Google account choice cleared");
        }

        [UnityTest]
        public IEnumerator Cancelled_Picker_Stays_Guest_Without_An_Error_And_Failures_Are_Reported()
        {
            yield return Wait(AuthService.Initialize());
            google.ErrorCode = "cancelled";
            Task<bool> t = AuthService.SignInWithGoogle();
            yield return Wait(t);
            Assert.IsFalse(t.Result);
            Assert.AreEqual(AuthState.Guest, AuthService.State);
            Assert.AreEqual("", AuthService.LastError, "cancelling is not an error");
            google.ErrorCode = "no_account";
            yield return Wait(AuthService.SignInWithGoogle());
            StringAssert.Contains("No Google account", AuthService.LastError);
        }

        [UnityTest]
        public IEnumerator No_Network_At_Start_Means_Offline_But_Playable()
        {
            backend.FailNext = true;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            yield return Wait(AuthService.Initialize());
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            Assert.AreEqual(AuthState.Unavailable, AuthService.State);
            Assert.AreEqual("OFFLINE", AuthService.ShortName());
            StringAssert.Contains("offline", AuthService.LastError.ToLowerInvariant());
        }

        [Test]
        public void Emails_Are_Masked_In_Logs()
        {
            Assert.AreEqual("pr…@gmail.com", AuthService.Mask("prashant@gmail.com"));
            Assert.AreEqual("", AuthService.Mask(""));
        }
    }
}
