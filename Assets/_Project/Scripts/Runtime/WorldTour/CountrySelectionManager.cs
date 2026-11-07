using System;
using UnityEngine;
using UltimateCarParking.Save;

namespace UltimateCarParking.WorldTour
{
    /// <summary>
    /// The country the player has chosen to play, persisted in the save (<see cref="SaveData.selectedCountryId"/>)
    /// so it survives restarts. Only unlocked countries can be selected.
    /// </summary>
    public static class CountrySelectionManager
    {
        public static event Action<CountryData> SelectionChanged;

        private static SaveManager Saves => SaveManager.Current;

        public static string SelectedId => Saves != null ? Saves.Data.selectedCountryId ?? "" : "";

        /// <summary>The saved selection resolved against a catalog (null if none or the id is unknown).</summary>
        public static CountryData Selected(CountryCatalog tour) =>
            tour != null && !string.IsNullOrEmpty(SelectedId) ? tour.FindById(SelectedId) : null;

        /// <summary>The saved selection if it is still open, otherwise the current tour stop.</summary>
        public static CountryData SelectedOrCurrent(CountryCatalog tour)
        {
            if (tour == null || tour.Count == 0) return null;
            CountryData c = Selected(tour);
            if (c != null && CountryUnlockManager.IsUnlocked(Saves?.Data, tour, c)) return c;
            return tour.Get(WorldTourService.CurrentIndex(Saves?.Data, tour));
        }

        /// <summary>Selects and saves. Returns false (and changes nothing) for null or locked countries.</summary>
        public static bool Select(CountryCatalog tour, CountryData country)
        {
            if (country == null) { Debug.LogWarning("[Countries] Select(null) ignored."); return false; }
            if (Saves == null) { Debug.LogError("[Countries] No save loaded; cannot select a country."); return false; }
            if (!CountryUnlockManager.IsUnlocked(Saves.Data, tour, country))
            {
                Debug.LogWarning($"[Countries] {country.CountryId} is locked; selection unchanged.");
                return false;
            }
            if (Saves.Data.selectedCountryId != country.CountryId)
            {
                Saves.Data.selectedCountryId = country.CountryId;
                Saves.Save();
            }
            SelectionChanged?.Invoke(country);
            return true;
        }

        public static void Clear()
        {
            if (Saves == null || string.IsNullOrEmpty(Saves.Data.selectedCountryId)) return;
            Saves.Data.selectedCountryId = "";
            Saves.Save();
            SelectionChanged?.Invoke(null);
        }
    }
}
