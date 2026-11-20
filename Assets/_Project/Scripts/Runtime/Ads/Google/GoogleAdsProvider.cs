using System;
using System.Collections.Concurrent;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace UltimateCarParking.Ads
{
    /// <summary>
    /// Google Mobile Ads (AdMob) behind <see cref="IAdProvider"/>. Consent first (UMP, required in the EEA/UK), then the SDK,
    /// then preloads one rewarded and one interstitial ad and reloads after each show.
    /// Robustness (device testing showed the start-up chain could stall silently):
    /// <list type="bullet">
    /// <item>Every SDK / UMP callback is marshalled to the Unity main thread by our own pump (<see cref="Pump"/>), so nothing
    /// depends on the plugin's executor existing yet.</item>
    /// <item>If consent was already given in an earlier session, the SDK starts at once (Google's recommended pattern) while
    /// the consent check refreshes in the background.</item>
    /// <item>A consent check that does not answer is retried; failed ad loads retry with back-off (5 s … 2 min).</item>
    /// <item>Each step logs one "[Ads]" line.</item>
    /// </list>
    /// Registered for builds only; the editor and tests use the fake provider.
    /// </summary>
    public sealed class GoogleAdsProvider : IAdProvider
    {
        private RewardedAd rewarded;
        private InterstitialAd interstitial;
        private BannerView banner;
        private bool loadingRewarded, loadingInterstitial, bannerWanted, sdkStarting, consentAnswered;
        private int rewardedFails, interstitialFails;
        private Action onReady;

        public bool Ready { get; private set; }
        public bool RewardedLoaded => rewarded != null && rewarded.CanShowAd();
        public bool InterstitialLoaded => interstitial != null && interstitial.CanShowAd();

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (Application.platform != RuntimePlatform.Android) return;
            Pump.Ensure();
            AdService.Register(new GoogleAdsProvider());
        }
#endif

        // ------------------------------------------------------------ main-thread pump

        /// <summary>Runs queued callbacks and timers on the Unity main thread (one hidden object, kept across scenes).</summary>
        private sealed class Pump : MonoBehaviour
        {
            private static Pump instance;
            private static readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();
            private static readonly System.Collections.Generic.List<(float at, Action a)> timers = new System.Collections.Generic.List<(float, Action)>();

            public static void Ensure()
            {
                if (instance != null) return;
                var go = new GameObject("[AdsPump]") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Pump>();
            }

            public static void Post(Action a) { if (a != null) queue.Enqueue(a); }
            public static void After(float seconds, Action a) => Post(() => timers.Add((Time.realtimeSinceStartup + seconds, a)));

            private void Update()
            {
                while (queue.TryDequeue(out Action a))
                {
                    try { a(); } catch (Exception e) { Debug.LogWarning("[Ads] callback failed: " + e.Message); }
                }
                float now = Time.realtimeSinceStartup;
                for (int i = timers.Count - 1; i >= 0; i--)
                {
                    if (timers[i].at > now) continue;
                    Action a = timers[i].a;
                    timers.RemoveAt(i);
                    try { a(); } catch (Exception e) { Debug.LogWarning("[Ads] timer failed: " + e.Message); }
                }
            }
        }

        // ------------------------------------------------------------ start-up

        public void Initialize(Action ready)
        {
            Pump.Ensure();
            onReady = ready;
            MobileAds.RaiseAdEventsOnUnityMainThread = false;   // we marshal callbacks ourselves (Pump)
            Debug.Log("[Ads] Start: " + (AdConfig.UsingRealIds ? "real" : "TEST") + " ad units, checking consent");
            bool already = false;
            try { already = ConsentInformation.CanRequestAds(); } catch (Exception e) { Debug.LogWarning("[Ads] CanRequestAds: " + e.Message); }
            if (already) { Debug.Log("[Ads] Consent from an earlier session allows ads: starting the SDK now"); StartSdk(); }
            RequestConsent(0);
        }

        private void RequestConsent(int attempt)
        {
            consentAnswered = false;
            try
            {
                ConsentInformation.Update(new ConsentRequestParameters(), updateError => Pump.Post(() => OnConsentUpdated(updateError)));
            }
            catch (Exception e) { Debug.LogWarning("[Ads] Consent update threw: " + e.Message); }
            // No answer within 10 s: try again (up to 3 times), then carry on with whatever consent state we have.
            Pump.After(10f, () =>
            {
                if (consentAnswered || Ready) return;
                if (attempt < 2) { Debug.LogWarning("[Ads] Consent check did not answer; retrying"); RequestConsent(attempt + 1); return; }
                Debug.LogWarning("[Ads] Consent check never answered");
                if (SafeCanRequestAds()) StartSdk();
            });
        }

        private void OnConsentUpdated(FormError updateError)
        {
            consentAnswered = true;
            if (updateError != null) Debug.LogWarning("[Ads] Consent update error: " + updateError.Message);
            else Debug.Log("[Ads] Consent info updated (status " + ConsentInformation.ConsentStatus + ")");
            try
            {
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => Pump.Post(() =>
                {
                    if (formError != null) Debug.LogWarning("[Ads] Consent form: " + formError.Message);
                    if (SafeCanRequestAds()) StartSdk();
                    else Debug.Log("[Ads] Consent does not allow ad requests; ads stay off.");
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] Consent form threw: " + e.Message);
                if (SafeCanRequestAds()) StartSdk();
            }
        }

        private static bool SafeCanRequestAds()
        {
            try { return ConsentInformation.CanRequestAds(); } catch { return false; }
        }

        private void StartSdk()
        {
            if (Ready || sdkStarting) return;
            sdkStarting = true;
            Debug.Log("[Ads] Initializing Mobile Ads SDK");
            try
            {
                RequestConfiguration rc = MobileAds.GetRequestConfiguration() ?? new RequestConfiguration();
                rc.TestDeviceIds = new System.Collections.Generic.List<string>(AdConfig.TestDeviceIds);
                MobileAds.SetRequestConfiguration(rc);
                Debug.Log("[Ads] Test devices registered: " + AdConfig.TestDeviceIds.Length);
            }
            catch (Exception e) { Debug.LogWarning("[Ads] Could not set test devices: " + e.Message); }
            MobileAds.Initialize(_ => Pump.Post(() =>
            {
                Ready = true;
                Debug.Log("[Ads] SDK ready; loading rewarded + interstitial");
                LoadRewarded();
                LoadInterstitial();
                if (bannerWanted) ShowBanner();
                onReady?.Invoke();
            }));
        }

        // ------------------------------------------------------------ loading (with back-off)

        private static float Backoff(int fails) => Mathf.Min(120f, 5f * Mathf.Pow(2f, Mathf.Max(0, fails - 1)));   // 5, 10, 20 … 120 s

        /// <summary>Asks for a rewarded ad now if none is loaded or loading (e.g. when a fail screen opens).</summary>
        public void LoadRewarded()
        {
            if (!Ready || loadingRewarded || RewardedLoaded) return;
            loadingRewarded = true;
            RewardedAd.Load(AdConfig.Rewarded, new AdRequest(), (ad, error) => Pump.Post(() =>
            {
                loadingRewarded = false;
                if (error != null || ad == null)
                {
                    rewardedFails++;
                    float wait = Backoff(rewardedFails);
                    Debug.Log($"[Ads] Rewarded failed to load ({error?.GetMessage()}); retry in {wait:0}s");
                    Pump.After(wait, LoadRewarded);
                    return;
                }
                rewardedFails = 0;
                rewarded = ad;
                Debug.Log("[Ads] Rewarded loaded");
            }));
        }

        private void LoadInterstitial()
        {
            if (!Ready || loadingInterstitial || InterstitialLoaded) return;
            loadingInterstitial = true;
            InterstitialAd.Load(AdConfig.Interstitial, new AdRequest(), (ad, error) => Pump.Post(() =>
            {
                loadingInterstitial = false;
                if (error != null || ad == null)
                {
                    interstitialFails++;
                    float wait = Backoff(interstitialFails);
                    Debug.Log($"[Ads] Interstitial failed to load ({error?.GetMessage()}); retry in {wait:0}s");
                    Pump.After(wait, LoadInterstitial);
                    return;
                }
                interstitialFails = 0;
                interstitial = ad;
                Debug.Log("[Ads] Interstitial loaded");
            }));
        }

        // ------------------------------------------------------------ showing

        public void ShowRewarded(Action<bool> done)
        {
            if (!RewardedLoaded) { done?.Invoke(false); return; }
            RewardedAd ad = rewarded;
            rewarded = null;
            bool earned = false, finished = false;
            void Finish(bool ok)
            {
                if (finished) return;
                finished = true;
                AudioListener.pause = false;
                ad.Destroy();
                LoadRewarded();
                done?.Invoke(ok);
            }
            AudioListener.pause = true;
            ad.OnAdFullScreenContentClosed += () => Pump.Post(() => Finish(earned));
            ad.OnAdFullScreenContentFailed += e => Pump.Post(() => { Debug.LogWarning("[Ads] Rewarded failed to show: " + e?.GetMessage()); Finish(false); });
            Debug.Log("[Ads] Showing rewarded");
            ad.Show(_ => Pump.Post(() => earned = true));
        }

        public void ShowInterstitial(Action closed)
        {
            if (!InterstitialLoaded) { closed?.Invoke(); return; }
            InterstitialAd ad = interstitial;
            interstitial = null;
            bool finished = false;
            void Finish()
            {
                if (finished) return;
                finished = true;
                AudioListener.pause = false;
                ad.Destroy();
                LoadInterstitial();
                closed?.Invoke();
            }
            AudioListener.pause = true;
            ad.OnAdFullScreenContentClosed += () => Pump.Post(Finish);
            ad.OnAdFullScreenContentFailed += e => Pump.Post(Finish);
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
