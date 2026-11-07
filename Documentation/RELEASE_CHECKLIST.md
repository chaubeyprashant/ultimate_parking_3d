# Release checklist

Things that are deliberately switched on for development and must change before a production upload.

## 1. Lock the content again (testing unlock)

`Assets/_Project/Scripts/Runtime/Core/TestingUnlocks.cs`:

```csharp
public const bool DefaultUnlockAll = true;   // ← set to false for release
```

- While `true`, every country, mission and campaign level is playable, and the game logs a `[Testing]` warning at startup.
- Only the lock checks are bypassed. Saves keep real progress and real unlocks, so setting it back to `false` restores normal progression without touching anyone's save.
- Tests switch it off themselves; no test changes are needed.
- After switching it off, check on the phone that only India is open on a fresh install.

## 2. Google sign-in for Play installs

Add the Play App Signing SHA-1 in Firebase (see [FIREBASE_AUTH.md](FIREBASE_AUTH.md)), download the new `google-services.json`, rebuild.

## 3. Compliance

- Credits screen for the CC BY 4.0 models (records in `Assets/_Project/ThirdParty/ATTRIBUTION.md`).
- Play Console Data safety form and privacy policy: Google sign-in (name, email, user ID) and Firebase Analytics.

## 4. Signing and package

- Release AAB signed with the upload keystore (`Builds/ultimate-parking-upload.keystore`). Keep it backed up.
- Package id: `om.ultimateparking.carparking3d` (live; never change).
- The `PerfMonitor` flag file (`perfmon`) is off by default.
