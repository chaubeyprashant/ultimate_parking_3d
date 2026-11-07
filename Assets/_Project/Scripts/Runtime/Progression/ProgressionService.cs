using UnityEngine;
using UltimateCarParking.Levels;
using UltimateCarParking.Save;

namespace UltimateCarParking.Progression
{
    public enum LevelLockState { Locked, Unlocked, Completed }

    /// <summary>What one completion earned.</summary>
    public readonly struct LevelReward
    {
        public readonly int Coins;
        public readonly int Stars;
        public readonly int PreviousBestStars;
        public readonly bool FirstCompletion;

        public LevelReward(int coins, int stars, int previousBestStars, bool firstCompletion)
        {
            Coins = coins;
            Stars = stars;
            PreviousBestStars = previousBestStars;
            FirstCompletion = firstCompletion;
        }

        public bool NewBest => Stars > PreviousBestStars;
    }

    /// <summary>
    /// Star progress, unlocks and coin rewards over SaveData. No scene state; unit-tested directly.
    ///
    /// Unlocks: level 1 is open; each level opens when the one before it (catalog order) has been completed.
    /// Rewards: first completion pays the level's full reward (base + per-star × stars). A replay pays
    /// per-star coins only for stars above the previous best, plus a 20 % base "replay bonus".
    /// </summary>
    public static class ProgressionService
    {
        public const float ReplayBaseFraction = 0.2f;

        public static int BestStars(SaveData data, LevelData level) =>
            level == null ? 0 : data?.FindLevel(level.LevelId)?.bestStars ?? 0;

        public static float BestTime(SaveData data, LevelData level) =>
            level == null ? -1f : data?.FindLevel(level.LevelId)?.bestTimeSeconds ?? -1f;

        public static bool IsCompleted(SaveData data, LevelData level) => BestStars(data, level) > 0;

        public static bool IsUnlocked(SaveData data, LevelCatalog catalog, LevelData level)
        {
            if (catalog == null || level == null) return false;
            int i = catalog.IndexOf(level);
            if (i < 0) return false;
            if (Core.TestingUnlocks.UnlockAll) return true;
            return i == 0 || IsCompleted(data, catalog.Get(i - 1)) || IsCompleted(data, level);
        }

        public static LevelLockState StateOf(SaveData data, LevelCatalog catalog, LevelData level) =>
            IsCompleted(data, level) ? LevelLockState.Completed :
            IsUnlocked(data, catalog, level) ? LevelLockState.Unlocked : LevelLockState.Locked;

        public static int TotalStars(SaveData data, LevelCatalog catalog, int fromIndex = 0, int toIndexExclusive = int.MaxValue)
        {
            if (catalog == null) return 0;
            int sum = 0;
            for (int i = Mathf.Max(0, fromIndex); i < Mathf.Min(catalog.Count, toIndexExclusive); i++)
                sum += BestStars(data, catalog.Get(i));
            return sum;
        }

        /// <summary>The furthest unlocked level (where the player should resume).</summary>
        public static int FurthestUnlockedIndex(SaveData data, LevelCatalog catalog)
        {
            int furthest = 0;
            if (catalog == null) return 0;
            for (int i = 0; i < catalog.Count; i++)
                if (IsUnlocked(data, catalog, catalog.Get(i))) furthest = i;
            return furthest;
        }

        /// <summary>Records a successful result, pays coins and returns what was earned. Caller saves.</summary>
        public static LevelReward RecordCompletion(SaveData data, LevelData level, LevelResult result)
        {
            if (data == null || level == null || !result.Success || result.Stars <= 0) return default;

            LevelProgress p = data.GetOrAddLevel(level.LevelId);
            int previous = p.bestStars;
            bool first = p.completions == 0 && previous == 0;
            int stars = Mathf.Clamp(result.Stars, 1, 3);

            int coins = first
                ? level.CoinsFor(stars)
                : level.CoinsPerStar * Mathf.Max(0, stars - previous) + Mathf.RoundToInt(level.BaseCoins * ReplayBaseFraction);

            p.bestStars = Mathf.Max(previous, stars);
            if (p.bestTimeSeconds < 0f || result.ElapsedSeconds < p.bestTimeSeconds) p.bestTimeSeconds = result.ElapsedSeconds;
            p.completions++;
            data.coins += coins;
            return new LevelReward(coins, stars, previous, first);
        }
    }
}
