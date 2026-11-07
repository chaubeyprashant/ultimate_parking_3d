using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UltimateCarParking.Account;
using UltimateCarParking.Core;
using UltimateCarParking.UI;

namespace UltimateCarParking.Tests
{
    /// <summary>Main-menu account pill and panel, driven by the local auth backend and a fake Google picker.</summary>
    public class AccountUiTests
    {
        [SetUp]
        public void SetUp()
        {
            TestScenes.UseTemporarySave();
            AuthService.UseBackends(new LocalAuthBackend(), new FakeGoogleSignIn());
        }

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.ResetToEmpty();

        [UnityTest]
        public IEnumerator Pill_Shows_Guest_Then_Google_Name_And_Panel_Signs_In_And_Out()
        {
            SceneManager.LoadScene(SceneNames.MainMenu);
            float t = 0f;
            AccountPanel ui = null;
            while ((ui = Object.FindAnyObjectByType<AccountPanel>()) == null && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsNotNull(ui, "main menu has the account pill");
            t = 0f;
            while (AuthService.State == AuthState.Starting && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return null;
            Assert.AreEqual("GUEST  ·  SIGN IN", ui.AccountText);

            ui.Open();
            Assert.IsTrue(ui.PanelOpen);
            Assert.AreEqual("PLAYING AS GUEST", ui.StatusText);
            Assert.IsTrue(ui.GoogleButtonShown);
            Assert.IsFalse(ui.SignOutShown);

            var signIn = AuthService.SignInWithGoogle();
            while (!signIn.IsCompleted) yield return null;
            yield return null;
            Assert.AreEqual("LOCAL", ui.AccountText, "first name on the pill");
            Assert.AreEqual("SIGNED IN WITH GOOGLE", ui.StatusText);
            Assert.IsFalse(ui.GoogleButtonShown);
            Assert.IsTrue(ui.SignOutShown);

            var signOut = AuthService.SignOut();
            while (!signOut.IsCompleted) yield return null;
            yield return null;
            Assert.AreEqual("GUEST  ·  SIGN IN", ui.AccountText);
            ui.Close();
            Assert.IsFalse(ui.PanelOpen);
        }
    }
}
