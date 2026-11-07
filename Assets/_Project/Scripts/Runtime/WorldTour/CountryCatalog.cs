using UnityEngine;
using UltimateCarParking.Levels;

namespace UltimateCarParking.WorldTour
{
    /// <summary>The World Tour: countries in unlock order, plus the campaign catalog their levels belong to.</summary>
    [CreateAssetMenu(fileName = "CountryCatalog", menuName = "Ultimate Car Parking/World Tour Catalog", order = 31)]
    public sealed class CountryCatalog : ScriptableObject
    {
        [SerializeField] private LevelCatalog campaign;
        [SerializeField] private CountryData[] countries = System.Array.Empty<CountryData>();

        public LevelCatalog Campaign => campaign;
        public int Count => countries.Length;
        public CountryData Get(int index) => index >= 0 && index < countries.Length ? countries[index] : null;

        public int IndexOf(CountryData country) => System.Array.IndexOf(countries, country);

        public CountryData FindById(string id)
        {
            foreach (CountryData c in countries)
                if (c != null && c.CountryId == id) return c;
            return null;
        }

        /// <summary>The country a campaign level is played in (null when the level is not on the tour).</summary>
        public CountryData CountryOf(LevelData level)
        {
            foreach (CountryData c in countries)
                if (c != null)
                    for (int i = 0; i < c.MissionCount; i++)
                        if (c.GetLevel(i) == level) return c;
            return null;
        }
    }
}
