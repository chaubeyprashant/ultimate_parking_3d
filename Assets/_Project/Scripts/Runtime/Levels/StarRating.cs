using UnityEngine;

namespace UltimateCarParking.Levels
{
    /// <summary>Per-level star rules: a time and a collision ceiling for three stars and for two.</summary>
    public readonly struct StarThresholds
    {
        public readonly float ThreeStarTime;
        public readonly int ThreeStarMaxCollisions;
        public readonly float TwoStarTime;
        public readonly int TwoStarMaxCollisions;

        public StarThresholds(float threeStarTime, int threeStarMaxCollisions, float twoStarTime, int twoStarMaxCollisions)
        {
            ThreeStarTime = threeStarTime;
            ThreeStarMaxCollisions = threeStarMaxCollisions;
            TwoStarTime = Mathf.Max(twoStarTime, threeStarTime);
            TwoStarMaxCollisions = Mathf.Max(twoStarMaxCollisions, threeStarMaxCollisions);
        }
    }

    /// <summary>
    /// Star outcome of a completed level: 3 when both the time and the collision count meet the three-star
    /// ceilings, 2 when they meet the two-star ceilings, otherwise 1. A failed level earns 0 (not computed here).
    /// </summary>
    public static class StarRating
    {
        public static int Calculate(float elapsedSeconds, int collisions, StarThresholds t)
        {
            if (elapsedSeconds <= t.ThreeStarTime && collisions <= t.ThreeStarMaxCollisions) return 3;
            if (elapsedSeconds <= t.TwoStarTime && collisions <= t.TwoStarMaxCollisions) return 2;
            return 1;
        }
    }
}
