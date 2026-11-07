namespace UltimateCarParking.Missions
{
    /// <summary>
    /// An objective that grades how well it was done (0..1), not just whether. The runner keeps the lowest grade of
    /// the completed objectives; levels with <see cref="Levels.ScoreMode.Quality"/> earn their stars from it.
    /// </summary>
    public interface IScoredObjective
    {
        /// <summary>0..1, final once the objective has completed.</summary>
        float Quality { get; }
        /// <summary>Results-screen label, e.g. "ACCURACY 92%".</summary>
        string ScoreLabel { get; }
    }
}
