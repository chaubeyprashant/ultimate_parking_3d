namespace UltimateCarParking.Levels
{
    public enum LevelState { Idle, Starting, Playing, Completed, Failed }

    public enum FailReason { None, TimeUp, TooManyCollisions }

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

        public LevelResult(string levelId, bool success, FailReason failReason, int stars, float elapsedSeconds, int collisions, int coins)
        {
            LevelId = levelId;
            Success = success;
            FailReason = failReason;
            Stars = stars;
            ElapsedSeconds = elapsedSeconds;
            Collisions = collisions;
            Coins = coins;
        }

        public override string ToString() => Success
            ? $"{LevelId}: parked, {Stars}★, {ElapsedSeconds:F1}s, {Collisions} collisions, {Coins} coins"
            : $"{LevelId}: failed ({FailReason}) after {ElapsedSeconds:F1}s, {Collisions} collisions";
    }
}
