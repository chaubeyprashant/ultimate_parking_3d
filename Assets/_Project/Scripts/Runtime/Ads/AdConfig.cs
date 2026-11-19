namespace UltimateCarParking.Ads
{
    /// <summary>
    /// Ad unit ids. Until real units exist (docs/ADS_PLAN.md section 10) these are Google's published TEST ids, which
    /// always serve clearly-labelled test ads and are safe to click. Replace the Real* ids before release.
    /// </summary>
    public static class AdConfig
    {
        // Google's documented Android test ids.
        public const string TestAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";
        public const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";
        public const string TestBanner = "ca-app-pub-3940256099942544/6300978111";

        public const string RealAppId = "ca-app-pub-2773109772362951~1921125633";
        public const string RealRewarded = "ca-app-pub-2773109772362951/7546017675";
        public const string RealInterstitial = "ca-app-pub-2773109772362951/8651215789";
        public const string RealBanner = "ca-app-pub-2773109772362951/5849792628";

        public static bool UsingRealIds => RealRewarded.Length > 0 && RealInterstitial.Length > 0 && RealBanner.Length > 0 && RealAppId.Length > 0;
        public static string AppId => UsingRealIds ? RealAppId : TestAppId;
        public static string Rewarded => UsingRealIds ? RealRewarded : TestRewarded;
        public static string Interstitial => UsingRealIds ? RealInterstitial : TestInterstitial;
        public static string Banner => UsingRealIds ? RealBanner : TestBanner;
    }
}
