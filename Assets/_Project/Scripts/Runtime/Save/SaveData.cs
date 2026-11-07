using System;
using System.Collections.Generic;
using UltimateCarParking.Settings;

namespace UltimateCarParking.Save
{
    /// <summary>
    /// Everything persisted locally. Plain serializable data (JsonUtility); bump <see cref="CurrentVersion"/>
    /// and add a step to <see cref="SaveManager.Migrate"/> when the shape changes.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 4;   // 2: + settings, 3: + World Tour selection/unlocks, 4: + country rewards, car upgrades

        public int version = CurrentVersion;
        public int coins;
        public string selectedCarId = "";
        public List<string> ownedCarIds = new List<string>();
        public List<LevelProgress> levels = new List<LevelProgress>();
        public GameSettings settings = new GameSettings();
        /// <summary>World Tour: the country chosen to play ("" = none yet).</summary>
        public string selectedCountryId = "";
        /// <summary>World Tour: countries that have opened (sticky, see CountryUnlockManager).</summary>
        public List<string> unlockedCountryIds = new List<string>();
        /// <summary>Country rewards already paid: "in" = completion bonus, "in*" = all-stars bonus.</summary>
        public List<string> countryRewardIds = new List<string>();
        /// <summary>Per-car upgrade levels (cars without an entry are stock).</summary>
        public List<CarUpgradeState> carUpgrades = new List<CarUpgradeState>();
        public long lastSavedUtcTicks;

        public CarUpgradeState FindUpgrades(string carId)
        {
            if (string.IsNullOrEmpty(carId)) return null;
            foreach (CarUpgradeState u in carUpgrades)
                if (u != null && u.carId == carId) return u;
            return null;
        }

        public CarUpgradeState GetOrAddUpgrades(string carId)
        {
            CarUpgradeState u = FindUpgrades(carId);
            if (u != null) return u;
            u = new CarUpgradeState { carId = carId };
            carUpgrades.Add(u);
            return u;
        }

        public LevelProgress FindLevel(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return null;
            foreach (LevelProgress p in levels)
                if (p != null && p.levelId == levelId) return p;
            return null;
        }

        public LevelProgress GetOrAddLevel(string levelId)
        {
            LevelProgress p = FindLevel(levelId);
            if (p != null) return p;
            p = new LevelProgress { levelId = levelId };
            levels.Add(p);
            return p;
        }
    }

    [Serializable]
    public sealed class LevelProgress
    {
        public string levelId = "";
        public int bestStars;
        public float bestTimeSeconds = -1f;
        public int completions;

        public bool Completed => bestStars > 0;
    }

    /// <summary>Upgrade levels (0 = stock) of one car.</summary>
    [Serializable]
    public sealed class CarUpgradeState
    {
        public string carId = "";
        public int engine;
        public int brakes;
        public int steering;
    }
}
