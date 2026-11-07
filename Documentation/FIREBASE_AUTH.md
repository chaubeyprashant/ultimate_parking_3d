# Firebase sign-in (Google + anonymous)

Firebase project: `ultimate-car-parking-3d` (number 23454512813).
Android app: `om.ultimateparking.carparking3d`, app ID `1:23454512813:android:d1d8f541b801d715abb824`.

## How it works

- **First launch:** the player is signed in **anonymously** (a Firebase guest user), and the session persists across launches. The main menu pill reads `GUEST · SIGN IN`.
- **Sign in with Google** (account panel):
  - Android's Credential Manager shows Google's account sheet and returns an ID token.
  - Firebase links the guest to that Google account (`LinkWithCredential`), so it is the same uid and the same player.
  - If that Google account already belongs to another player, it signs in as that player instead.
- **Sign out:** signs out of Firebase, clears the Google choice, and continues as a new guest.
- **Offline:** if Firebase can't start, the pill shows `OFFLINE` and the game stays fully playable. Progress is saved on the device as before; there is no cloud save yet.
- **Editor and tests:** these use a local stand-in backend (`LocalAuthBackend`), so play mode never creates users in the live project. Google sign-in is Android-only.

## Files

| File | Role |
|---|---|
| `Runtime/Account/AuthService.cs` | State machine (Starting / Guest / Google / SignedOut / Unavailable), linking, sign-out, errors |
| `Runtime/Account/AuthBackends.cs` | `FirebaseAuthBackend`, `AndroidGoogleSignIn` (Java bridge), local/fake backends for the editor and tests |
| `Assets/Plugins/Android/GoogleSignInBridge.java` | Credential Manager "Sign in with Google". Reads `default_web_client_id` from the resources Firebase generates. |
| `Runtime/UI/AccountPanel.cs` | Main-menu pill + account modal (built by `UiSceneBuilder.BuildAccount`) |
| `Assets/_Project/Firebase/google-services.json` | Firebase config (the Firebase editor plugin generates `Plugins/Android/FirebaseApp.androidlib`) |
| `Assets/_Project/Firebase/Editor/GoogleSignInDependencies.xml` | `androidx.credentials` 1.3.0 + `googleid` 1.1.1 for the External Dependency Manager |
| `Packages/GoogleTarballs/*.tgz` | Firebase App + Auth 14.1.0 and External Dependency Manager 1.2.190 (Google, Apache-2.0) |
| `Assets/Plugins/Android/*Template*` + `ProjectSettings/GvhProjectSettings.xml` | Gradle templates that the dependency manager patches; `BuildScript.ResolveAndroidDependencies` runs before each Android build |
| Tests | `EditMode/AuthServiceTests` (7), `PlayMode/AccountUiTests` (1) |

## Firebase console set-up (done 2026-11-06)

- Android app registered.
- SHA-1 fingerprints:
  - this Mac's debug key `04:59:C3:7E:…:F3`
  - the upload key (`Builds/ultimate-parking-upload.keystore`) `2A:16:2F:12:…:80`
- Sign-in providers: **Anonymous** enabled (auto clean-up off); **Google** enabled.
  - Public name: "Ultimate Car Parking 3D".
  - Support email: prashantchaubey493@gmail.com.

## ⚠️ Still needed for the live Play Store app

Google re-signs Play installs with the **app signing key**. Google sign-in fails for those installs until its SHA-1 is added. (Guest sign-in is unaffected.)

1. In **Play Console** → the app → **Test and release → Setup → App signing** (or "App integrity"), copy the **App signing key certificate SHA-1**. The live app's Play account is not the Google account signed in on this Mac.
2. In **Firebase console** → Project settings → your Android app → **Add fingerprint**, paste it, and save.
3. Download the new `google-services.json`, replace `Assets/_Project/Firebase/google-services.json`, rebuild and upload.

## Play Store data safety

Signing in with Google gives the app the player's name, email address and a user ID. Firebase Auth also brings in Firebase Analytics (app interactions, device IDs). Update the Play Console **Data safety** form and the privacy policy before releasing this version.
