using UnityEngine;

namespace UltimateCarParking.Parking
{
    /// <summary>
    /// Realistic parking score for one finished park (0..1 overall plus its parts). Fair for every car size: the
    /// position score measures the car's offset from the bay centre against the room that particular car has to spare
    /// in that bay, not in absolute metres, so a monster truck in a snug bay and a hatchback in the same bay are judged
    /// on the same scale.
    /// Weights: centring 35 %, alignment 25 %, collisions 25 % (impacts cost more than scrapes), time 10 %, stillness 5 %.
    /// </summary>
    public readonly struct ParkingScorecard
    {
        public readonly float Centring, Alignment, Collisions, Time, Stillness, Overall;
        public readonly float SideOffset, DepthOffset, AngleError;
        public readonly int Impacts, Scrapes;

        public const float WCentre = 0.35f, WAngle = 0.25f, WHits = 0.25f, WTime = 0.10f, WStill = 0.05f;

        public ParkingScorecard(float centring, float alignment, float collisions, float time, float stillness,
            float sideOffset, float depthOffset, float angleError, int impacts, int scrapes)
        {
            Centring = Mathf.Clamp01(centring); Alignment = Mathf.Clamp01(alignment); Collisions = Mathf.Clamp01(collisions);
            Time = Mathf.Clamp01(time); Stillness = Mathf.Clamp01(stillness);
            SideOffset = sideOffset; DepthOffset = depthOffset; AngleError = angleError; Impacts = impacts; Scrapes = scrapes;
            Overall = Mathf.Clamp01(Centring * WCentre + Alignment * WAngle + Collisions * WHits + Time * WTime + Stillness * WStill);
        }

        /// <summary>
        /// Scores a park. Offsets are the car centre in the bay frame (x across, z along), metres. carSize / baySize are
        /// (width, length). angleTolerance is the level's limit (degrees). timeLimit 0 = untimed (full time credit).
        /// </summary>
        public static ParkingScorecard Score(Vector2 offset, Vector2 carSize, Vector2 baySize, float angleError, float angleTolerance,
            int impacts, int scrapes, float elapsed, float timeLimit, float speedKmh)
        {
            // Room to spare on each side: a car that just fits cannot be expected to sit far from the centre line.
            float slackX = Mathf.Max(0.08f, (baySize.x - carSize.x) * 0.5f);
            float slackZ = Mathf.Max(0.15f, (baySize.y - carSize.y) * 0.5f);
            float side = Mathf.Clamp01(Mathf.Abs(offset.x) / slackX);
            float depth = Mathf.Clamp01(Mathf.Abs(offset.y) / slackZ);
            float centring = 1f - (side * 0.7f + depth * 0.3f);
            float alignment = 1f - Mathf.Clamp01(angleError / Mathf.Max(1f, angleTolerance));
            float hits = 1f - Mathf.Min(1f, impacts * 0.25f + scrapes * 0.1f);
            float time = timeLimit <= 0f ? 1f : 1f - 0.5f * Mathf.Clamp01((elapsed / timeLimit - 0.5f) / 0.5f);   // full credit inside half the limit
            float still = speedKmh <= 0.5f ? 1f : 0.6f;
            return new ParkingScorecard(centring, alignment, hits, time, still, offset.x, offset.y, angleError, impacts, scrapes);
        }

        /// <summary>Results line, e.g. "PARKING 91%  ·  CENTRE 95%  ·  ANGLE 1.4°  ·  HITS 0".</summary>
        public string Label =>
            $"PARKING {Mathf.RoundToInt(Overall * 100f)}%  ·  CENTRE {Mathf.RoundToInt(Centring * 100f)}%  ·  ANGLE {AngleError:0.0}°  ·  HITS {Impacts + Scrapes}";
    }
}
