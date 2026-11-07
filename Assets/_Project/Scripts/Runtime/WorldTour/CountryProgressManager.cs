using UltimateCarParking.Levels;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;

namespace UltimateCarParking.WorldTour
{
    /// <summary>
    /// Country progress, read from the existing per-level save records (stars, completion) through
    /// <see cref="ProgressionService"/>. Nothing country-specific is stored for progress, so the old save format,
    /// level select and the World Tour can never disagree.
    /// </summary>
    /// <summary>Coins paid for finishing a country (each bonus once per save).</summary>
    public readonly struct CountryReward
    {
        public readonly string CountryId;
        public readonly int CompletionCoins;
        public readonly int PerfectCoins;
        public CountryReward(string countryId, int completionCoins, int perfectCoins)
        {
            CountryId = countryId;
            CompletionCoins = completionCoins;
            PerfectCoins = perfectCoins;
        }
        public int Coins => CompletionCoins + PerfectCoins;
        public bool Any => Coins > 0;
    }

    public static class CountryProgressManager
    {
        /// <summary>Completion percentage (0..100) shown on the World Tour card.</summary>
        public static int CompletionPercent(SaveData save, CountryData country) =>
            UnityEngine.Mathf.RoundToInt(CompletionFraction(save, country) * 100f);

        public static bool IsPerfect(SaveData save, CountryData country) =>
            country != null && country.MissionCount > 0 && Stars(save, country) == MaxStars(country);

        /// <summary>
        /// Pays any country bonus now earned and not yet paid: the completion bonus once every mission is
        /// completed, the all-stars bonus once every mission has three stars. Adds the coins; caller saves.
        /// </summary>
        public static CountryReward ClaimRewards(SaveData save, CountryData country)
        {
            if (save == null || country == null) return default;
            save.countryRewardIds ??= new System.Collections.Generic.List<string>();
            int done = 0, perfect = 0;
            if (IsCompleted(save, country) && !save.countryRewardIds.Contains(country.CountryId))
            {
                save.countryRewardIds.Add(country.CountryId);
                done = country.CompletionCoins;
            }
            if (IsPerfect(save, country) && !save.countryRewardIds.Contains(country.CountryId + "*"))
            {
                save.countryRewardIds.Add(country.CountryId + "*");
                perfect = country.PerfectCoins;
            }
            save.coins += done + perfect;
            return new CountryReward(country.CountryId, done, perfect);
        }

        public static int CompletedMissions(SaveData save, CountryData country)
        {
            if (save == null || country == null) return 0;
            int done = 0;
            for (int i = 0; i < country.MissionCount; i++)
                if (country.GetLevel(i) != null && ProgressionService.IsCompleted(save, country.GetLevel(i))) done++;
            return done;
        }

        public static int Stars(SaveData save, CountryData country)
        {
            if (save == null || country == null) return 0;
            int stars = 0;
            for (int i = 0; i < country.MissionCount; i++)
                if (country.GetLevel(i) != null) stars += ProgressionService.BestStars(save, country.GetLevel(i));
            return stars;
        }

        public static int MaxStars(CountryData country) => country != null ? country.MissionCount * 3 : 0;

        public static bool IsCompleted(SaveData save, CountryData country) =>
            country != null && country.MissionCount > 0 && CompletedMissions(save, country) == country.MissionCount;

        public static float CompletionFraction(SaveData save, CountryData country) =>
            country == null || country.MissionCount == 0 ? 0f : (float)CompletedMissions(save, country) / country.MissionCount;

        public static int TourStars(SaveData save, CountryCatalog tour)
        {
            int stars = 0;
            if (tour != null)
                for (int i = 0; i < tour.Count; i++) stars += Stars(save, tour.Get(i));
            return stars;
        }

        public static int CompletedCountries(SaveData save, CountryCatalog tour)
        {
            int n = 0;
            if (tour != null)
                for (int i = 0; i < tour.Count; i++) if (IsCompleted(save, tour.Get(i))) n++;
            return n;
        }

        /// <summary>Missions inside an open country unlock in order: the first is open, each next needs the previous one done.</summary>
        public static bool IsMissionUnlocked(SaveData save, CountryData country, int index)
        {
            if (country == null || index < 0 || index >= country.MissionCount) return false;
            if (index == 0 || Core.TestingUnlocks.UnlockAll) return true;
            LevelData prev = country.GetLevel(index - 1);
            return prev != null && ProgressionService.IsCompleted(save, prev);
        }

        /// <summary>The next mission to play in a country: the first unfinished one, else the first.</summary>
        public static LevelData NextMission(SaveData save, CountryData country)
        {
            if (country == null) return null;
            for (int i = 0; i < country.MissionCount; i++)
                if (country.GetLevel(i) != null && !ProgressionService.IsCompleted(save, country.GetLevel(i))) return country.GetLevel(i);
            return country.GetLevel(0);
        }
    }
}
