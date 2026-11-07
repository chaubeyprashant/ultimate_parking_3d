using System.Collections.Generic;
using UltimateCarParking.Save;

namespace UltimateCarParking.WorldTour
{
    /// <summary>Result of an unlock check: open or not, and (when locked) the first unmet requirement for the UI.</summary>
    public readonly struct UnlockCheck
    {
        public readonly bool Unlocked;
        public readonly string Reason;
        public UnlockCheck(bool unlocked, string reason) { Unlocked = unlocked; Reason = reason ?? ""; }
    }

    /// <summary>
    /// Evaluates <see cref="CountryUnlockRequirement"/>s against progress. Unlocks are sticky: once a country has
    /// opened it is recorded in the save (<see cref="SaveData.unlockedCountryIds"/>) and stays open even if the
    /// requirements are tightened by a later update.
    /// </summary>
    public static class CountryUnlockManager
    {
        public static bool IsUnlocked(SaveData save, CountryCatalog tour, CountryData country) => Evaluate(save, tour, country).Unlocked;

        public static UnlockCheck Evaluate(SaveData save, CountryCatalog tour, CountryData country)
        {
            if (country != null && Core.TestingUnlocks.UnlockAll) return new UnlockCheck(true, "");
            return EvaluateProgress(save, tour, country);
        }

        /// <summary>The real rule from progress (ignores the testing switch); used for sticky unlocks.</summary>
        public static UnlockCheck EvaluateProgress(SaveData save, CountryCatalog tour, CountryData country)
        {
            if (country == null) return new UnlockCheck(false, "UNKNOWN COUNTRY");
            if (save != null && save.unlockedCountryIds != null && save.unlockedCountryIds.Contains(country.CountryId))
                return new UnlockCheck(true, "");
            CountryUnlockRequirement r = country.Unlock;
            if (r == null || r.unlockedByDefault) return new UnlockCheck(true, "");

            if (r.requirePreviousCountry && tour != null)
            {
                int i = tour.IndexOf(country);
                CountryData prev = i > 0 ? tour.Get(i - 1) : null;
                if (prev != null && !CountryProgressManager.IsCompleted(save, prev))
                    return new UnlockCheck(false, $"FINISH {prev.DisplayName.ToUpperInvariant()} TO UNLOCK");
            }
            if (r.requiredCountryIds != null)
                foreach (string id in r.requiredCountryIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    CountryData need = tour != null ? tour.FindById(id) : null;
                    if (need == null) return new UnlockCheck(false, $"REQUIRES UNKNOWN COUNTRY '{id}'");
                    if (!CountryProgressManager.IsCompleted(save, need))
                        return new UnlockCheck(false, $"FINISH {need.DisplayName.ToUpperInvariant()} TO UNLOCK");
                }
            if (r.minTourStars > 0)
            {
                int stars = CountryProgressManager.TourStars(save, tour);
                if (stars < r.minTourStars) return new UnlockCheck(false, $"EARN {r.minTourStars - stars} MORE STARS TO UNLOCK");
            }
            if (r.minCompletedCountries > 0)
            {
                int done = CountryProgressManager.CompletedCountries(save, tour);
                if (done < r.minCompletedCountries) return new UnlockCheck(false, $"COMPLETE {r.minCompletedCountries} COUNTRIES TO UNLOCK");
            }
            return new UnlockCheck(true, "");
        }

        /// <summary>
        /// Records every newly opened country in the save (sticky) and returns them, e.g. for an "unlocked" banner.
        /// The caller saves.
        /// </summary>
        public static List<CountryData> RecordUnlocks(SaveData save, CountryCatalog tour)
        {
            var opened = new List<CountryData>();
            if (save == null || tour == null) return opened;
            save.unlockedCountryIds ??= new List<string>();
            for (int i = 0; i < tour.Count; i++)
            {
                CountryData c = tour.Get(i);
                if (c == null || save.unlockedCountryIds.Contains(c.CountryId)) continue;
                if (EvaluateProgress(save, tour, c).Unlocked)      // never record unlocks granted by the testing switch
                {
                    save.unlockedCountryIds.Add(c.CountryId);
                    opened.Add(c);
                }
            }
            return opened;
        }
    }
}
