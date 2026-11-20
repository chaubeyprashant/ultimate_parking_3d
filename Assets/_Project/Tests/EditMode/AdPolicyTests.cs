using System;
using System.IO;
using NUnit.Framework;
using UltimateCarParking.Ads;
using UltimateCarParking.Save;

namespace UltimateCarParking.Tests
{
    /// <summary>The frequency rules from docs/ADS_PLAN.md, one rule per test, with a controllable clock.</summary>
    public class AdPolicyTests
    {
        private DateTime now;
        private AdState state;
        private AdPolicy policy;

        [SetUp]
        public void SetUp()
        {
            now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            state = new AdState();
            policy = new AdPolicy(state, () => now);
            // A player with enough completed levels; individual tests lower it.
        }

        private void Minutes(double m) => now = now.AddMinutes(m);

        // ---- interstitial gating: every 2nd win, 2 minutes apart from any full-screen ad

        [Test]
        public void Interstitial_After_Every_Second_Win_From_The_Start()
        {
            policy.RecordLevelCompleted();
            Assert.IsFalse(policy.CanShowInterstitial(1), "not after the first win");
            policy.RecordLevelCompleted();
            Assert.IsTrue(policy.CanShowInterstitial(2), "after the second win");
            policy.RecordInterstitialShown();
            Assert.AreEqual(0, state.levelsSinceInterstitial);
            Minutes(5);
            policy.RecordLevelCompleted();
            Assert.IsFalse(policy.CanShowInterstitial(3), "one win since the last ad");
            policy.RecordLevelCompleted();
            Assert.IsTrue(policy.CanShowInterstitial(4), "two wins since the last ad");
        }

        [Test]
        public void Never_Two_Full_Screen_Ads_Within_Two_Minutes()
        {
            state.levelsSinceInterstitial = 5;
            policy.RecordInterstitialShown();
            state.levelsSinceInterstitial = 5;
            Minutes(1.5);
            Assert.IsFalse(policy.CanShowInterstitial(20), "too soon after the last interstitial");
            Minutes(1);
            Assert.IsTrue(policy.CanShowInterstitial(20));
            policy.RecordRewardedShown(AdPlacement.ContinueTime);
            Assert.IsFalse(policy.CanShowInterstitial(20), "not straight after a rewarded ad");
            Minutes(2.5);
            Assert.IsTrue(policy.CanShowInterstitial(20));
        }

        [Test]
        public void No_Daily_Cap_Or_Fail_Pause_Any_More()
        {
            for (int i = 0; i < 12; i++)
            {
                state.levelsSinceInterstitial = 2;
                Minutes(3);
                Assert.IsTrue(policy.CanShowInterstitial(20), "ad " + i);
                policy.RecordInterstitialShown();
            }
            policy.RecordLevelFailed("L1"); policy.RecordLevelFailed("L1"); policy.RecordLevelFailed("L1");
            state.levelsSinceInterstitial = 2; Minutes(3);
            Assert.IsTrue(policy.CanShowInterstitial(20), "fails never show an ad themselves; the next win may");
        }

        // ---- rewarded and banner

        [Test]
        public void Rewarded_Offers_And_Banner_From_The_First_Mission()
        {
            Assert.IsTrue(policy.CanOfferRewarded(AdPlacement.ContinueTime, 0), "+30 s on time-out from the first mission");
            Assert.IsTrue(policy.CanOfferRewarded(AdPlacement.DoubleCoins, 0));
            Assert.IsTrue(policy.CanShowBanner(0), "banner from the start");
            Assert.IsTrue(policy.CanOfferRewarded(AdPlacement.DoubleCoins, 3));
            Assert.IsTrue(policy.CanShowBanner(3));
        }

        [Test]
        public void Free_Coins_Have_A_Fifteen_Minute_Cooldown()
        {
            Assert.IsTrue(policy.CanOfferRewarded(AdPlacement.FreeCoins, 10));
            policy.RecordRewardedShown(AdPlacement.FreeCoins);
            Assert.IsFalse(policy.CanOfferRewarded(AdPlacement.FreeCoins, 10));
            Minutes(14);
            Assert.IsFalse(policy.CanOfferRewarded(AdPlacement.FreeCoins, 10));
            Minutes(2);
            Assert.IsTrue(policy.CanOfferRewarded(AdPlacement.FreeCoins, 10));
        }

        [Test]
        public void Free_Coins_Are_Capped_At_Five_Per_Day()
        {
            for (int i = 0; i < AdPolicy.FreeCoinsPerDay; i++)
            {
                Assert.IsTrue(policy.CanOfferRewarded(AdPlacement.FreeCoins, 10), "claim " + i);
                policy.RecordRewardedShown(AdPlacement.FreeCoins);
                Minutes(20);
            }
            Assert.IsFalse(policy.CanOfferRewarded(AdPlacement.FreeCoins, 10));
            now = now.AddDays(1);
            Assert.IsTrue(policy.CanOfferRewarded(AdPlacement.FreeCoins, 10));
        }

        [Test]
        public void Double_Coins_And_Continue_Are_Not_Cooldown_Limited()
        {
            policy.RecordRewardedShown(AdPlacement.DoubleCoins);
            Assert.IsTrue(policy.CanOfferRewarded(AdPlacement.DoubleCoins, 10));
            Assert.IsTrue(policy.CanOfferRewarded(AdPlacement.ContinueTime, 10));
        }

        [Test]
        public void CompletedLevels_Counts_Only_Starred_Levels()
        {
            var data = new SaveData();
            data.levels.Add(new LevelProgress { levelId = "a", bestStars = 2 });
            data.levels.Add(new LevelProgress { levelId = "b", bestStars = 0 });
            data.levels.Add(new LevelProgress { levelId = "c", bestStars = 1 });
            Assert.AreEqual(2, AdPolicy.CompletedLevels(data));
        }

        [Test]
        public void AdState_Survives_A_Save_Round_Trip()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ucp_ads_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var save = new SaveManager(Path.Combine(dir, "s.json"));
                save.Load();
                save.Data.ads.interstitialsToday = 3;
                save.Data.ads.consecutiveFails = 2;
                save.Data.ads.failLevelId = "L9";
                Assert.IsTrue(save.Save());
                var again = new SaveManager(Path.Combine(dir, "s.json"));
                again.Load();
                Assert.AreEqual(3, again.Data.ads.interstitialsToday);
                Assert.AreEqual(2, again.Data.ads.consecutiveFails);
                Assert.AreEqual("L9", again.Data.ads.failLevelId);
            }
            finally { Directory.Delete(dir, true); }
        }
    }

    /// <summary>AdService with a fake provider: never shows when not allowed or not loaded, rewards only on completion.</summary>
    public class AdServiceTests
    {
        private string dir;
        private FakeAdProvider fake;
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "ucp_adsvc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            SaveManager.Initialize(Path.Combine(dir, "s.json"));
            for (int i = 0; i < 6; i++) SaveManager.Current.Data.levels.Add(new LevelProgress { levelId = "L" + i, bestStars = 3 });
            fake = new FakeAdProvider();
            AdService.ResetForTests();
            AdService.Register(fake);
            now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            AdService.UseClock(() => now);
        }

        [TearDown]
        public void TearDown()
        {
            AdService.ResetForTests();
            try { Directory.Delete(dir, true); } catch { }
        }

        [Test]
        public void Rewarded_Is_Hidden_When_No_Ad_Is_Loaded()
        {
            fake.RewardedLoaded = false;
            Assert.IsFalse(AdService.CanOfferRewarded(AdPlacement.DoubleCoins));
            bool? result = null;
            AdService.ShowRewarded(AdPlacement.DoubleCoins, w => result = w);
            Assert.AreEqual(false, result);
            Assert.AreEqual(0, fake.RewardedShown);
        }

        [Test]
        public void Closing_A_Rewarded_Ad_Early_Gives_No_Reward_And_Costs_No_Cooldown()
        {
            fake.RewardedCompletes = false;
            bool? result = null;
            AdService.ShowRewarded(AdPlacement.FreeCoins, w => result = w);
            Assert.AreEqual(false, result);
            Assert.IsTrue(AdService.CanOfferRewarded(AdPlacement.FreeCoins), "no cooldown started");
        }

        [Test]
        public void Completed_Rewarded_Ad_Reports_Success_And_Starts_The_Cooldown()
        {
            bool? result = null;
            AdService.ShowRewarded(AdPlacement.FreeCoins, w => result = w);
            Assert.AreEqual(true, result);
            Assert.IsFalse(AdService.CanOfferRewarded(AdPlacement.FreeCoins));
        }

        [Test]
        public void Interstitial_Runs_The_Continuation_Exactly_Once_Either_Way()
        {
            int calls = 0;
            SaveManager.Current.Data.ads.levelsSinceInterstitial = 5;
            AdService.TryShowInterstitialThen(() => calls++);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, fake.InterstitialsShown);
            AdService.TryShowInterstitialThen(() => calls++);   // immediately again: capped, so no ad
            Assert.AreEqual(2, calls);
            Assert.AreEqual(1, fake.InterstitialsShown);
        }

        [Test]
        public void Interstitial_Is_Skipped_When_Not_Loaded_But_Play_Continues()
        {
            fake.InterstitialLoaded = false;
            SaveManager.Current.Data.ads.levelsSinceInterstitial = 5;
            int calls = 0;
            AdService.TryShowInterstitialThen(() => calls++);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, fake.InterstitialsShown);
        }

        [Test]
        public void Nothing_Shows_Until_The_SDK_Is_Ready()
        {
            fake.Ready = false;
            Assert.IsFalse(AdService.CanOfferRewarded(AdPlacement.DoubleCoins));
            int calls = 0;
            SaveManager.Current.Data.ads.levelsSinceInterstitial = 5;
            AdService.TryShowInterstitialThen(() => calls++);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, fake.InterstitialsShown);
            AdService.ShowBanner();
            Assert.IsFalse(fake.BannerVisible);
        }

        [Test]
        public void Banner_Shows_For_Experienced_Players_And_Hides()
        {
            AdService.ShowBanner();
            Assert.IsTrue(fake.BannerVisible);
            AdService.HideBanner();
            Assert.IsFalse(fake.BannerVisible);
        }

        [Test]
        public void New_Players_See_The_Banner_From_The_Start()
        {
            SaveManager.Current.Data.levels.Clear();
            AdService.ShowBanner();
            Assert.IsTrue(fake.BannerVisible);
        }

        [Test]
        public void AddCoins_Updates_The_Saved_Balance()
        {
            int before = SaveManager.Current.Data.coins;
            AdService.AddCoins(100);
            Assert.AreEqual(before + 100, SaveManager.Current.Data.coins);
        }
    }
}
