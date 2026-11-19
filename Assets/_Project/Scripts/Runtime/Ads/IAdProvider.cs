using System;

namespace UltimateCarParking.Ads
{
    /// <summary>The ad network behind <see cref="AdService"/> (Google Mobile Ads on device, a fake in tests and the editor).</summary>
    public interface IAdProvider
    {
        /// <summary>Consent resolved and the SDK ready to request ads.</summary>
        bool Ready { get; }
        bool RewardedLoaded { get; }
        bool InterstitialLoaded { get; }
        void Initialize(Action onReady);
        /// <summary>Shows the rewarded ad. <paramref name="done"/> gets true only when the ad was watched to the end.</summary>
        void ShowRewarded(Action<bool> done);
        /// <summary>Shows the interstitial; <paramref name="closed"/> is always called (also when it fails to show).</summary>
        void ShowInterstitial(Action closed);
        void ShowBanner();
        void HideBanner();
    }
}
