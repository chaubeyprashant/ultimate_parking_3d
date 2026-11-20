using UnityEngine;
using UltimateCarParking.Parking;
using UltimateCarParking.Settings;

namespace UltimateCarParking.Levels
{
    public enum Difficulty { Easy = 0, Medium = 1, Hard = 2 }

    /// <summary>
    /// The player's chosen difficulty for missions (picked on the mission card, remembered in the settings). Applied on
    /// top of each mission when it starts, without changing the mission data: time limit, hits allowed, bay size, angle
    /// tolerance, assistance and coin rewards. Medium is exactly the mission as designed (and how all older progress
    /// was earned).
    /// </summary>
    public static class PlayDifficulty
    {
        public static Difficulty Current
        {
            // Only while playing: editor tools that read level data always see the mission as designed (Medium).
            get => Override ?? (Application.isPlaying ? (Difficulty)Mathf.Clamp(SettingsService.Current.playDifficulty, 0, 2) : Difficulty.Medium);
            set => SettingsService.SetPlayDifficulty((int)value);
        }

        /// <summary>Tests only: forces a difficulty without touching the settings (null = normal behaviour).</summary>
        public static Difficulty? Override;

        public static float TimeFactor(Difficulty d) => d == Difficulty.Easy ? 1.4f : d == Difficulty.Hard ? 0.75f : 1f;
        public static float AngleFactor(Difficulty d) => d == Difficulty.Easy ? 1.4f : d == Difficulty.Hard ? 0.7f : 1f;
        public static float CoinFactor(Difficulty d) => d == Difficulty.Easy ? 0.75f : d == Difficulty.Hard ? 1.5f : 1f;

        /// <summary>Hits allowed: Easy has no collisions at all (no limit; contacts never count), Hard −1 (never below 0).</summary>
        public static int CollisionLimit(int designed, Difficulty d) =>
            designed < 0 || d == Difficulty.Easy ? -1 : d == Difficulty.Hard ? Mathf.Max(0, designed - 1) : designed;

        /// <summary>
        /// Bay size: Easy wider and longer; Hard narrower and shorter, but never tighter than the largest garage car
        /// (1.93 × 5.2 m) plus the 0.4 / 0.3 m clearance every mission bay keeps.
        /// </summary>
        public static Vector2 Bay(Vector2 designed, Difficulty d)
        {
            if (d == Difficulty.Easy) return designed + new Vector2(0.4f, 0.6f);
            if (d == Difficulty.Hard) return new Vector2(Mathf.Max(Mathf.Min(designed.x, 2.45f), designed.x * 0.9f), Mathf.Max(Mathf.Min(designed.y, 5.6f), designed.y * 0.95f));
            return designed;
        }

        /// <summary>Hard turns parking assistance off; Easy and Medium leave the player's choice.</summary>
        public static AssistLevel AssistCap(Difficulty d) => d == Difficulty.Hard ? AssistLevel.Expert : AssistLevel.Beginner;

        public static string Name(Difficulty d) => d == Difficulty.Easy ? "EASY" : d == Difficulty.Hard ? "HARD" : "MEDIUM";

        public static int Coins(int designed, Difficulty d) => Mathf.RoundToInt(designed * CoinFactor(d));
    }
}
