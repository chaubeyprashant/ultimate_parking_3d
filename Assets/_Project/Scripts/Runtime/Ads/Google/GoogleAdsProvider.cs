using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace UltimateCarParking.Ads
{
    /// <summary>
    /// Google Mobile Ads (AdMob) behind <see cref="IAdProvider"/>. Consent first (UMP, required in the EEA/UK), then the SDK,
    /// then preloads one rewarded and one interstitial ad and reloads after each show. All callbacks run on the main thread.
    /// Registered for builds only; the editor and tests use the fake provider.
    /// </summary>
    public sealed class GoogleAdsProvider : IAdProvider
    {
        private RewardedAd rewarded;
        private InterstitialAd interstitial;
        private BannerView banner;
        private bool loadingRewarded, loadingInterstitial, bannerWanted;

        public bool Ready { get; private set; }
        public bool RewardedLoaded => rewarded != null && rewarded.CanShowAd();
        public bool InterstitialLoaded => interstitial != null && interstitial.CanShowAd();

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (Application.platform == RuntimePlatform.Android) AdService.Register(new GoogleAdsProvider());
        }
#endif

        public void Initialize(Action onReady)
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            var request = new ConsentRequestParameters();
            ConsentInformation.Update(request, updateError =>
            {
                if (updateError != null) Debug.LogWarning("[Ads] Consent update: " + updateError.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning("[Ads] Consent form: " + formError.Message);
                    if (!ConsentInformation.CanRequestAds()) { Debug.Log("[Ads] Consent does not allow ad requests; ads stay off."); return; }
                    MobileAds.Initialize(_ =>
                    {
                        Ready = true;
                        Debug.Log("[Ads] Initialized (" + (AdConfig.UsingRealIds ? "real" : "TEST") + " ad units)");
                        LoadRewarded();
                        LoadInterstitial();
                        if (bannerWanted) ShowBanner();
                        onReady?.Invoke();
                    });
                });
            });
        }

        private void LoadRewarded()
        {
            if (loadingRewarded || RewardedLoaded) return;
            loadingRewarded = true;
            RewardedAd.Load(AdConfig.Rewarded, new AdRequest(), (ad, error) =>
            {
                loadingRewarded = false;
                if (error != null || ad == null) { Debug.Log("[Ads] Rewarded failed to load: " + error?.GetMessage()); return; }
                rewarded = ad;
            });
        }

        private void LoadInterstitial()
        {
            if (loadingInterstitial || InterstitialLoaded) return;
            loadingInterstitial = true;
            InterstitialAd.Load(AdConfig.Interstitial, new AdRequest(), (ad, error) =>
            {
                loadingInterstitial = false;
                if (error != null || ad == null) { Debug.Log("[Ads] Interstitial failed to load: " + error?.GetMessage()); return; }
                interstitial = ad;
            });
        }

        public void ShowRewarded(Action<bool> done)
        {
            if (!RewardedLoaded) { done?.Invoke(false); return; }
            RewardedAd ad = rewarded;
            rewarded = null;
            bool earned = false;
            AudioListener.pause = true;
            ad.OnAdFullScreenContentClosed += () => { AudioListener.pause = false; ad.Destroy(); LoadRewarded(); done?.Invoke(earned); };
            ad.OnAdFullScreenContentFailed += e => { AudioListener.pause = false; ad.Destroy(); LoadRewarded(); done?.Invoke(false); };
            ad.Show(_ => earned = true);
        }

        public void ShowInterstitial(Action closed)
        {
            if (!InterstitialLoaded) { closed?.Invoke(); return; }
            InterstitialAd ad = interstitial;
            interstitial = null;
            AudioListener.pause = true;
            ad.OnAdFullScreenContentClosed += () => { AudioListener.pause = false; ad.Destroy(); LoadInterstitial(); closed?.Invoke(); };
            ad.OnAdFullScreenContentFailed += e => { AudioListener.pause = false; ad.Destroy(); LoadInterstitial(); closed?.Invoke(); };
            ad.Show();
        }

        public void ShowBanner()
        {
            bannerWanted = true;
            if (!Ready || banner != null) return;
            banner = new BannerView(AdConfig.Banner, AdSize.Banner, AdPosition.Bottom);
            banner.LoadAd(new AdRequest());
        }

        public void HideBanner()
        {
            bannerWanted = false;
            if (banner == null) return;
            banner.Destroy();
            banner = null;
        }
    }
}
