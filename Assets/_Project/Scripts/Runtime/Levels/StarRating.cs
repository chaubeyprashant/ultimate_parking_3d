using UnityEngine;

namespace UltimateCarParking.Levels
{
    /// <summary>
    /// What a level's stars are earned on. <see cref="TimeAndCollisions"/>: the classic time + hits ceilings.
    /// <see cref="Quality"/>: the mission's own 0..1 score (accuracy, time left, clearance, guest rating, ...) plus the
    /// same hit ceilings; the elapsed time is ignored.
    /// </summary>
    public enum ScoreMode { TimeAndCollisions, Quality }

    /// <summary>Per-level star rules: a time (or quality) and a collision ceiling for three stars and for two.</summary>
    public readonly struct StarThresholds
    {
        public readonly float ThreeStarTime;
        public readonly int ThreeStarMaxCollisions;
        public readonly float TwoStarTime;
        public readonly int TwoStarMaxCollisions;
        public readonly ScoreMode Mode;
        public readonly float ThreeStarQuality;
        public readonly float TwoStarQuality;

        public StarThresholds(float threeStarTime, int threeStarMaxCollisions, float twoStarTime, int twoStarMaxCollisions,
            ScoreMode mode = ScoreMode.TimeAndCollisions, float threeStarQuality = 0.85f, float twoStarQuality = 0.6f)
        {
            ThreeStarTime = threeStarTime;
            ThreeStarMaxCollisions = threeStarMaxCollisions;
            TwoStarTime = Mathf.Max(twoStarTime, threeStarTime);
            TwoStarMaxCollisions = Mathf.Max(twoStarMaxCollisions, threeStarMaxCollisions);
            Mode = mode;
            ThreeStarQuality = Mathf.Clamp01(threeStarQuality);
            TwoStarQuality = Mathf.Min(Mathf.Clamp01(twoStarQuality), ThreeStarQuality);
        }
    }

    /// <summary>
    /// Star outcome of a completed level: 3 when both the time and the collision count meet the three-star
    /// ceilings, 2 when they meet the two-star ceilings, otherwise 1. A failed level earns 0 (not computed here).
    /// </summary>
    public static class StarRating
    {
        public static int Calculate(float elapsedSeconds, int collisions, StarThresholds t, float quality = 1f)
        {
            if (t.Mode == ScoreMode.Quality)
            {
                if (quality >= t.ThreeStarQuality && collisions <= t.ThreeStarMaxCollisions) return 3;
                if (quality >= t.TwoStarQuality && collisions <= t.TwoStarMaxCollisions) return 2;
                return 1;
            }
            if (elapsedSeconds <= t.ThreeStarTime && collisions <= t.ThreeStarMaxCollisions) return 3;
            if (elapsedSeconds <= t.TwoStarTime && collisions <= t.TwoStarMaxCollisions) return 2;
            return 1;
        }
    }
}
