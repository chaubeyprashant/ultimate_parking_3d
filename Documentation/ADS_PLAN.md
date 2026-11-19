# Ads plan: Ultimate Car Parking 3D

Status: **plan only. No ad SDK is in the project yet** (checked 2026-11-16: no AdMob/rewarded/interstitial code or package). Nothing below is built or tested.

## 1. Principles (what "not frustrating" means here)

1. **The player never gets an ad while driving, parking, or reading a result they just earned.** Parking needs concentration; an interruption mid-manoeuvre is the fastest way to a 1-star review.
2. **Rewarded first.** Most income comes from ads the player chooses to watch for something they want.
3. **Interstitials are rare, predictable and skippable by playing well.** Only at natural breaks, never after a failure, never in the first session.
4. **No ad on a button the player is about to press.** Ads never appear right where a tap is expected (accidental-click policy risk too).
5. **Never block progress.** Every level can be finished and every country unlocked without watching an ad.
6. **Offline-safe.** If no ad is ready, the reward button is hidden (not greyed, not an error) and play continues.
7. **No "Remove ads" purchase in version 1** (your decision). The caps in section 3 are what keep ads fair for everyone.

## 2. Ad formats and where each is allowed

| Format | Allowed | Never |
|---|---|---|
| **Rewarded video** | Result screen, garage, out-of-coins moments (always opt-in) | Auto-played, or forced to continue |
| **Interstitial** | Between levels, only at a natural break (see 3.2) | In gameplay, after a fail, during a country download, on app start, in the first 3 levels |
| **Banner** | Main menu and level-select only, anchored bottom, small | Gameplay, result screen, settings, loader screen, garage (it hides the car) |
| **App-open ad** | Not used | Always |

## 3. Placement plan (by player action)

### 3.1 Rewarded (player taps a button)

| Moment | Button text | Reward | Notes |
|---|---|---|---|
| Level complete (result screen) | "Watch ad: double coins" | Doubles that level's coin reward | Shown beside the normal CONTINUE, never replaces it. Once per result. |
| Level failed (time up / too many hits) | "Watch ad: +30 seconds, keep going" | Continue from where they failed once | Max once per attempt. Offered only on **time-up**; for crash-outs, offer retry only. |
| Garage, locked car | "Watch ad: try this car for 1 level" | One-level trial | Trial car reverts afterwards. Not a permanent unlock. |
| Not enough coins for a car | "Watch ad: +150 coins" | Small coin top-up | Cap 5 per day. |
| Main menu coin pill | "Free coins" chip with a short cooldown | +100 coins | Cooldown 15 min. Chip hides while cooling down or no ad ready. |

Rewards are granted only when the ad is **fully completed**. If it is closed early or fails to load, no reward and a plain message ("Reward not available, try again later"). Never charge a penalty for a failed load.

### 3.2 Interstitial (frequency-capped, natural breaks only)

Show only when **all** are true:

- The player has completed **at least 4 levels** in total (nothing in the first session).
- The player **just finished a level successfully** and tapped NEXT or BACK TO MAP (never right after a failure, never on RETRY).
- At least **4 minutes** since the last interstitial **and** at least **3 completed levels** since the last one.
- Max **6 interstitials per day**. Max **1 per level transition**.
- The player has not watched a rewarded ad in the last **3 minutes** (a rewarded view counts as their ad for that break).
- The device is online with an ad already loaded (never wait on a spinner for an ad).

Not shown: after a fail, on RETRY, when opening Settings/Garage/World Tour, when returning from the background, during or right after an on-demand country download.

### 3.3 Banners

- Main menu and level-select only, at the bottom edge, standard small banner.
- Hidden whenever a modal (Settings, Account, Garage preview) is open.

### 3.4 Screens with no ads at all

Gameplay (including pause), result screen apart from the optional rewarded buttons, loader and download screen, Settings, Account, first launch, tutorial or first 3 levels.

## 4. Pacing by player stage

| Stage | What can show |
|---|---|
| Session 1, levels 1-3 | Nothing. No banners either. |
| Levels 4-10 | Rewarded buttons, interstitials at the lowest cap (every 4th break) |
| Later | Full plan above |
| Player struggling (3 fails in a row on the same level) | Stop interstitials for 10 minutes; offer the rewarded "+30 seconds" |

## 5. Policy and compliance (must be settled before release)

- **Audience (decided): general audience.** Standard AdMob setup. No child-specific ad settings are used.
- **Consent:** integrate Google's User Messaging Platform (UMP) for EEA/UK consent. Ads load only after consent is resolved.
- **Play Console:** declare ads ("Contains ads"), complete the Data safety form (ad ID and usage data), and keep the privacy policy current (already on GitHub Pages).
- **AdMob policy:** no incentives for clicking, no ads placed under buttons, no ads covering the controls, rewarded ads must be clearly labelled as optional.
- **app-ads.txt** must verify (currently failing on the AdMob screen; see the earlier note about the publisher ID and the Play listing website).

## 6. Technical plan

1. Add the Google Mobile Ads Unity plugin through the Package Manager (generated like the Firebase setup, so it stays reproducible by the bootstrap).
2. New runtime `AdService` (static, like `AuthService`): `Initialize`, `IsRewardedReady`, `ShowRewarded(onReward)`, `TryShowInterstitial(context)`, `RemoveAds` flag. Interstitial and banner logic reads only from one **AdPolicy** class that holds the caps in section 3, so the numbers are easy to tune.
3. AdPolicy state saved in the save file: completed levels, last interstitial time, interstitials today, rewarded coins today.
4. Preload the next rewarded and interstitial in the background; reload after each show. Show nothing if not loaded.
5. Hook points (all existing screens): result screen controller (rewarded double coins, +30 s, interstitial on NEXT/BACK), garage controller (trial car), coin pill (free coins chip), main menu and level-select (banner).
6. **Test IDs only in debug and the editor**; real unit IDs only in release builds.
7. Fake provider for tests (a `FakeAdProvider` like `FakeAuthBackend`) so the caps can be unit tested without the SDK.

## 7. Tests (to write with the feature)

- Unit tests for AdPolicy: first-session no ads, the 4-level threshold, time and level gaps, daily caps, no interstitial after a fail or on retry, no interstitial within 3 minutes of a rewarded view.
- Reward tests: reward only on completed view; closed early gives nothing; failed load hides the button.
- Device checks on the A33 using AdMob test ads: each placement, background and foreground, airplane mode (buttons hidden, no errors), and a fresh install (nothing shows in the first 3 levels).

## 8. What to watch after launch

- Day-1 and day-7 retention and session length, compared with an ad-light build.
- Interstitials per session and the share of players who watch rewarded ads (healthy: more rewarded than interstitial views).
- One-star reviews that mention ads. If they appear, raise the level gap and time gap before anything else.

## 9. Decisions made (2026-11-16)

1. Audience: general audience, standard ad setup (no child-specific settings).
2. No "Remove ads" in the first version.
3. "+30 seconds" continue after a time-up is accepted.
4. No AdMob ad unit IDs yet. Build with Google's **test IDs**, and swap in real IDs once created (see the checklist below). The `app-ads.txt` publisher ID still needs to be confirmed from the AdMob screen.

## 10. Your checklist in AdMob (needed before the real release)

1. Make sure the app `om.ultimateparking.carparking3d` is added in AdMob (the verification screen showed it was added; verification still failing).
2. Create three ad units for the app: **Rewarded**, **Interstitial**, **Banner**. Copy the three unit IDs and the app ID (`ca-app-pub-…~…`).
3. Fix `app-ads.txt` (publisher ID, and the Website on the Play listing).
