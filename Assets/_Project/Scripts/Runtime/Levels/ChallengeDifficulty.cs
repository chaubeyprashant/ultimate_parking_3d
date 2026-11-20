using UltimateCarParking.Parking;
using UltimateCarParking.Settings;

namespace UltimateCarParking.Levels
{
    /// <summary>Challenge difficulty (Phase 5). None = an older level with its own hand-set rules.</summary>
    public enum ChallengeDifficulty { None = 0, Beginner = 1, Intermediate = 2, Advanced = 3, Expert = 4 }

    /// <summary>What each difficulty means for a level built from a challenge template.</summary>
    public readonly struct DifficultyProfile
    {
        /// <summary>Extra bay width / length beyond the largest garage car (1.93 × 5.2 m), metres.</summary>
        public readonly float BayExtraWidth, BayExtraLength;
        public readonly float AngleTolerance, HoldSeconds;
        public readonly int CollisionLimit, ThreeStarHits, TwoStarHits;
        public readonly float ThreeStarScore, TwoStarScore, TimeFactor;
        /// <summary>The most assistance this level allows (players may choose less).</summary>
        public readonly AssistLevel MaxAssist;

        private DifficultyProfile(float w, float l, float angle, float hold, int limit, int three, int two, float s3, float s2, float time, AssistLevel assist)
        {
            BayExtraWidth = w; BayExtraLength = l; AngleTolerance = angle; HoldSeconds = hold;
            CollisionLimit = limit; ThreeStarHits = three; TwoStarHits = two; ThreeStarScore = s3; TwoStarScore = s2; TimeFactor = time; MaxAssist = assist;
        }

        public static DifficultyProfile For(ChallengeDifficulty d) => d switch
        {
            // Beginner: wide bays, guides, forgiving limits.
            ChallengeDifficulty.Beginner => new DifficultyProfile(1.3f, 1.2f, 14f, 0.8f, 5, 1, 3, 0.75f, 0.5f, 1.6f, AssistLevel.Beginner),
            // Intermediate: tighter bays and obstacles.
            ChallengeDifficulty.Intermediate => new DifficultyProfile(1.0f, 0.95f, 11f, 1.0f, 3, 0, 1, 0.8f, 0.55f, 1.3f, AssistLevel.Beginner),
            // Advanced: precision alignment, restricted visibility, complex manoeuvres; no alignment hints.
            ChallengeDifficulty.Advanced => new DifficultyProfile(0.8f, 0.75f, 8f, 1.2f, 2, 0, 1, 0.85f, 0.6f, 1.1f, AssistLevel.Standard),
            // Expert: realistic constraints, minimal assistance (no sensors, no bay chevron, no guide lines).
            ChallengeDifficulty.Expert => new DifficultyProfile(0.65f, 0.6f, 6f, 1.5f, 1, 0, 0, 0.88f, 0.65f, 1.0f, AssistLevel.Expert),
            _ => new DifficultyProfile(0.9f, 0.9f, 10f, 1.0f, 3, 0, 1, 0.85f, 0.6f, 1.3f, AssistLevel.Beginner),
        };

        public static string Name(ChallengeDifficulty d) => d == ChallengeDifficulty.None ? "" : d.ToString().ToUpperInvariant();
    }

    /// <summary>The assistance actually shown: the player's choice, capped by the level (an Expert level shows none).</summary>
    public static class AssistPolicy
    {
        public static AssistLevel Effective(LevelData level)
        {
            var chosen = (AssistLevel)UnityEngine.Mathf.Clamp(SettingsService.Current.assistMode, 0, 2);
            AssistLevel playCap = PlayDifficulty.AssistCap(PlayDifficulty.Current);   // Hard: assistance off
            if ((int)playCap > (int)chosen) chosen = playCap;
            if (level == null || level.Difficulty == ChallengeDifficulty.None) return chosen;
            AssistLevel cap = DifficultyProfile.For(level.Difficulty).MaxAssist;
            return (int)chosen > (int)cap ? chosen : cap;   // higher enum value = less assistance
        }

        public static AssistLevel Current => Effective(LevelManager.Instance != null ? LevelManager.Instance.Level : null);
    }
}
