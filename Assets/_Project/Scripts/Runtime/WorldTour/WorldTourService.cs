using UltimateCarParking.Levels;
using UltimateCarParking.Save;

namespace UltimateCarParking.WorldTour
{
    public enum CountryState { Locked, Unlocked, Completed }

    /// <summary>
    /// Convenience façade used by the World Tour screen: country state from <see cref="CountryUnlockManager"/>
    /// (locks) and <see cref="CountryProgressManager"/> (progress).
    /// </summary>
    public static class WorldTourService
    {
        public static int CompletedMissions(SaveData save, CountryData country) => CountryProgressManager.CompletedMissions(save, country);
        public static int Stars(SaveData save, CountryData country) => CountryProgressManager.Stars(save, country);
        public static int CompletedCountries(SaveData save, CountryCatalog tour) => CountryProgressManager.CompletedCountries(save, tour);
        public static LevelData NextMission(SaveData save, CountryData country) => CountryProgressManager.NextMission(save, country);

        public static CountryState StateOf(SaveData save, CountryCatalog tour, CountryData country)
        {
            if (country == null || !CountryUnlockManager.IsUnlocked(save, tour, country)) return CountryState.Locked;
            return CountryProgressManager.IsCompleted(save, country) ? CountryState.Completed : CountryState.Unlocked;
        }

        /// <summary>The first unlocked, unfinished country (the "current stop"), or the last unlocked one.</summary>
        public static int CurrentIndex(SaveData save, CountryCatalog tour)
        {
            int lastOpen = 0;
            if (tour == null) return 0;
            for (int i = 0; i < tour.Count; i++)
            {
                CountryState s = StateOf(save, tour, tour.Get(i));
                if (s == CountryState.Unlocked) return i;
                if (s != CountryState.Locked) lastOpen = i;
            }
            return lastOpen;
        }
    }
}
