using System;
using UltimateCarParking.Save;

namespace UltimateCarParking.Ads
{
    public enum AdPlacement { DoubleCoins, ContinueTime, FreeCoins }

    /// <summary>
    /// The frequency rules from docs/ADS_PLAN.md, as pure logic over the saved <see cref="AdState"/> (no SDK, no Unity
    /// calls, injectable clock) so every cap is unit-testable. Never decides *how* to show an ad, only *whether*.
    /// </summary>
    public sealed class AdPolicy
    {
        public const int RewardedFromLevels = 0;          // opt-in rewarded offers (+30 s, double coins) from the first mission
        public const int BannerFromLevels = 0;            // the small banner (missions screen) from the start
        // Interstitials, kept to three simple rules: every 2nd win, never within 2 minutes of the last full-screen ad
        // (interstitial or rewarded), and only at the break after a win (callers never ask after a fail or on Retry).
        public const int InterstitialLevelGap = 2;
        public const double FullScreenGapMinutes = 2;
        public const double FreeCoinsCooldownMinutes = 15;
        public const int FreeCoinsPerDay = 5;
        public const int FreeCoinsAmount = 100;
        public const float ContinueSeconds = 30f;

        private readonly AdState state;
        private readonly Func<DateTime> utcNow;

        public AdPolicy(AdState state, Func<DateTime> utcNow = null)
        {
            this.state = state ?? new AdState();
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public AdState State => state;

        private DateTime Now => utcNow();
        private static DateTime FromTicks(long t) => t <= 0 ? DateTime.MinValue : new DateTime(t, DateTimeKind.Utc);
        private double MinutesSince(long ticks) => ticks <= 0 ? double.MaxValue : (Now - FromTicks(ticks)).TotalMinutes;

        private void RollDay()
        {
            string today = Now.ToString("yyyy-MM-dd");
            if (state.dayKey == today) return;
            state.dayKey = today;
            state.interstitialsToday = 0;
            state.freeCoinsToday = 0;
        }

        /// <summary>Total completed levels (from the save's progress, so returning players are not treated as new).</summary>
        public static int CompletedLevels(SaveData data)
        {
            int n = 0;
            if (data != null) foreach (LevelProgress p in data.levels) if (p != null && p.Completed) n++;
            return n;
        }

        // ------------------------------------------------------------ rewarded

        /// <summary>Whether to show the opt-in button for <paramref name="placement"/> (an ad must also be loaded).</summary>
        public bool CanOfferRewarded(AdPlacement placement, int completedLevels)
        {
            if (completedLevels < RewardedFromLevels) return false;
            RollDay();
            if (placement == AdPlacement.FreeCoins)
                return state.freeCoinsToday < FreeCoinsPerDay && MinutesSince(state.lastFreeCoinsTicks) >= FreeCoinsCooldownMinutes;
            return true;   // DoubleCoins / ContinueTime are limited by the screen (once per result / attempt)
        }

        public void RecordRewardedShown(AdPlacement placement)
        {
            RollDay();
            state.lastRewardedTicks = Now.Ticks;
            if (placement == AdPlacement.FreeCoins)
            {
                state.freeCoinsToday++;
                state.lastFreeCoinsTicks = Now.Ticks;
            }
        }

        /// <summary>The banner is allowed once the player is past the first levels.</summary>
        public bool CanShowBanner(int completedLevels) => completedLevels >= BannerFromLevels;

        // ------------------------------------------------------------ interstitial

        /// <summary>
        /// Only ever asked at a natural break after a *successful* level (Next / back to the map): never after a fail,
        /// never on Retry. Returns false whenever any rule from the plan is not met.
        /// </summary>
        public bool CanShowInterstitial(int completedLevels)
        {
            RollDay();
            if (state.levelsSinceInterstitial < InterstitialLevelGap) return false;   // every 2nd win
            if (MinutesSince(state.lastInterstitialTicks) < FullScreenGapMinutes) return false;
            if (MinutesSince(state.lastRewardedTicks) < FullScreenGapMinutes) return false;   // no ad straight after an ad
            return true;
        }

        public void RecordInterstitialShown()
        {
            RollDay();
            state.lastInterstitialTicks = Now.Ticks;
            state.interstitialsToday++;
            state.levelsSinceInterstitial = 0;
        }

        // ------------------------------------------------------------ level events

        public void RecordLevelCompleted()
        {
            state.levelsSinceInterstitial++;
            state.consecutiveFails = 0;
        }

        public void RecordLevelFailed(string levelId)
        {
            state.consecutiveFails = state.failLevelId == levelId ? state.consecutiveFails + 1 : 1;
            state.failLevelId = levelId ?? "";
            state.lastFailTicks = Now.Ticks;
        }
    }
}
