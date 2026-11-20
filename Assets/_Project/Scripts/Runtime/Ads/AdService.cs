using System;
using UnityEngine;
using UltimateCarParking.Save;

namespace UltimateCarParking.Ads
{
    /// <summary>
    /// The one entry point UI calls for ads. Decides *whether* with <see cref="AdPolicy"/> (saved state) and *how* with
    /// the registered <see cref="IAdProvider"/>. Without a registered provider (editor, tests) a <see cref="FakeAdProvider"/>
    /// is used. Everything degrades to "no ad, carry on": a missing or unloaded ad never blocks the player.
    /// </summary>
    public static class AdService
    {
        private static IAdProvider provider;
        private static AdPolicy policy;
        private static AdState policyFor;
        private static bool initializing;

        public static IAdProvider Provider => provider ??= new FakeAdProvider();

        /// <summary>Called once by the Google provider's startup hook (Android), or by tests.</summary>
        public static void Register(IAdProvider p)
        {
            provider = p;
            initializing = false;
        }

        public static void ResetForTests()
        {
            provider = null; policy = null; policyFor = null; initializing = false;
        }

        public static AdPolicy Policy
        {
            get
            {
                AdState s = SaveManager.Current.Data.ads ??= new AdState();
                if (policy == null || !ReferenceEquals(policyFor, s)) { policy = new AdPolicy(s); policyFor = s; }
                return policy;
            }
        }

        /// <summary>Overrides the clock for tests.</summary>
        public static void UseClock(Func<DateTime> now)
        {
            AdState s = SaveManager.Current.Data.ads ??= new AdState();
            policy = new AdPolicy(s, now);
            policyFor = s;
        }

        private static int Completed => AdPolicy.CompletedLevels(SaveManager.Current.Data);

        public static void Initialize()
        {
            if (initializing || Provider.Ready) return;
            initializing = true;
            Provider.Initialize(() => initializing = false);
        }

        // ------------------------------------------------------------ rewarded

        /// <summary>True when the button for this placement should be visible: allowed by the policy and an ad is loaded.</summary>
        public static bool CanOfferRewarded(AdPlacement placement) =>
            Provider.Ready && Provider.RewardedLoaded && Policy.CanOfferRewarded(placement, Completed);

        /// <summary>The policy allows offering this placement (whether or not an ad has loaded yet).</summary>
        public static bool RewardedAllowed(AdPlacement placement) => Policy.CanOfferRewarded(placement, Completed);

        /// <summary>Makes sure the SDK is starting and a rewarded ad is on its way (no-op when one is loaded).</summary>
        public static void RequestRewarded()
        {
            Initialize();
            if (Provider.Ready) Provider.LoadRewarded();
        }

        /// <summary>Shows a rewarded ad; <paramref name="done"/> gets true only if it was watched to the end.</summary>
        public static void ShowRewarded(AdPlacement placement, Action<bool> done)
        {
            if (!CanOfferRewarded(placement)) { done?.Invoke(false); return; }
            Provider.ShowRewarded(watched =>
            {
                if (watched)
                {
                    Policy.RecordRewardedShown(placement);
                    SaveManager.Current.Save();
                }
                done?.Invoke(watched);
            });
        }

        public static void AddCoins(int amount)
        {
            if (amount <= 0) return;
            SaveManager.Current.Data.coins += amount;
            SaveManager.Current.Save();
        }

        // ------------------------------------------------------------ interstitial

        /// <summary>
        /// Call at a natural break after a *successful* level, with what should happen next. Shows an interstitial only
        /// when every rule allows it and one is already loaded; otherwise (or after it closes) runs <paramref name="proceed"/>.
        /// </summary>
        public static void TryShowInterstitialThen(Action proceed)
        {
            if (Provider.Ready && Provider.InterstitialLoaded && Policy.CanShowInterstitial(Completed))
            {
                Policy.RecordInterstitialShown();
                SaveManager.Current.Save();
                Provider.ShowInterstitial(() => proceed?.Invoke());
                return;
            }
            proceed?.Invoke();
        }

        // ------------------------------------------------------------ level events

        public static void NotifyLevelCompleted()
        {
            Policy.RecordLevelCompleted();
            SaveManager.Current.Save();
        }

        public static void NotifyLevelFailed(string levelId)
        {
            Policy.RecordLevelFailed(levelId);
            SaveManager.Current.Save();
        }

        // ------------------------------------------------------------ banner

        public static void ShowBanner()
        {
            if (Provider.Ready && Policy.CanShowBanner(Completed)) Provider.ShowBanner();
        }

        public static void HideBanner() => Provider.HideBanner();
    }
}
