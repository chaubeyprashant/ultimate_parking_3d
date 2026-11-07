namespace UltimateCarParking.Levels
{
    public enum LevelState { Idle, Starting, Playing, Completed, Failed }

    public enum FailReason { None, TimeUp, TooManyCollisions, ObjectiveFailed }

    /// <summary>Outcome of one attempt, handed to listeners (results UI, save system) by the LevelManager.</summary>
    public readonly struct LevelResult
    {
        public readonly string LevelId;
        public readonly bool Success;
        public readonly FailReason FailReason;
        public readonly int Stars;
        public readonly float ElapsedSeconds;
        public readonly int Collisions;
        public readonly int Coins;
        /// <summary>The mission's own 0..1 score (1 when it has none).</summary>
        public readonly float Quality;
        /// <summary>What <see cref="Quality"/> measured, for the results screen ("ACCURACY 92%"). "" = none.</summary>
        public readonly string ScoreLabel;

        public LevelResult(string levelId, bool success, FailReason failReason, int stars, float elapsedSeconds, int collisions, int coins,
            float quality = 1f, string scoreLabel = "")
        {
            Quality = quality;
            ScoreLabel = scoreLabel ?? "";
            LevelId = levelId;
            Success = success;
            FailReason = failReason;
            Stars = stars;
            ElapsedSeconds = elapsedSeconds;
            Collisions = collisions;
            Coins = coins;
        }

        public override string ToString() => Success
            ? $"{LevelId}: parked, {Stars}★, {ElapsedSeconds:F1}s, {Collisions} collisions, {Coins} coins{(ScoreLabel.Length > 0 ? ", " + ScoreLabel : "")}"
            : $"{LevelId}: failed ({FailReason}) after {ElapsedSeconds:F1}s, {Collisions} collisions";
    }
}
