using System;

namespace UltimateCarParking.Ads
{
    /// <summary>Scriptable provider for tests and the editor: no SDK, ads "play" instantly.</summary>
    public sealed class FakeAdProvider : IAdProvider
    {
        public bool Ready { get; set; } = true;
        public bool RewardedLoaded { get; set; } = true;
        public bool InterstitialLoaded { get; set; } = true;
        /// <summary>What a rewarded ad reports: true = watched to the end.</summary>
        public bool RewardedCompletes { get; set; } = true;
        public int RewardedShown { get; private set; }
        public int InterstitialsShown { get; private set; }
        public bool BannerVisible { get; private set; }

        public void Initialize(Action onReady) => onReady?.Invoke();

        public int RewardedRequests { get; private set; }
        public void LoadRewarded() => RewardedRequests++;

        public void ShowRewarded(Action<bool> done)
        {
            RewardedShown++;
            done?.Invoke(RewardedCompletes);
        }

        public void ShowInterstitial(Action closed)
        {
            InterstitialsShown++;
            closed?.Invoke();
        }

        public void ShowBanner() => BannerVisible = true;
        public void HideBanner() => BannerVisible = false;
    }
}
