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
        public const int CurrentVersion = 7;   // 2: + settings, 3: + World Tour selection/unlocks, 4: + country rewards, car upgrades, 5: + career rewards, best scores, 6: + car looks, 7: + Easy / Hard progress

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
        /// <summary>Career chapter rewards already paid: chapter id (completion) and id + "*" (mastery). Version 5.</summary>
        public List<string> careerRewardIds = new List<string>();
        /// <summary>Per-car customisation (paint, finish, rims, tint, headlights, plate, bought options). Version 6.</summary>
        public List<Cars.CarLook> carLooks = new List<Cars.CarLook>();
        /// <summary>Ad frequency state (see AdPolicy). Missing in older saves = defaults.</summary>
        public AdState ads = new AdState();
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
    public sealed class AdState
    {
        public string dayKey = "";
        public int interstitialsToday;
        public int freeCoinsToday;
        public long lastInterstitialTicks;
        public long lastRewardedTicks;
        public long lastFreeCoinsTicks;
        public int levelsSinceInterstitial;
        public int consecutiveFails;
        public string failLevelId = "";
        public long lastFailTicks;
    }

    [Serializable]
    public sealed class LevelProgress
    {
        public string levelId = "";
        public int bestStars;
        public float bestTimeSeconds = -1f;
        public int completions;
        /// <summary>Best mission score 0..1 (quality / parking scorecard); 0 = none recorded. Version 5.</summary>
        public float bestQuality;
        /// <summary>Easy and Hard results (version 7). The fields above are Medium, which is how all older progress was earned.</summary>
        public int easyStars, hardStars;
        public float easyTimeSeconds = -1f, hardTimeSeconds = -1f;
        public float easyQuality, hardQuality;
        public int easyCompletions, hardCompletions;

        public int StarsAt(Levels.Difficulty d) => d == Levels.Difficulty.Easy ? easyStars : d == Levels.Difficulty.Hard ? hardStars : bestStars;
        public float TimeAt(Levels.Difficulty d) => d == Levels.Difficulty.Easy ? easyTimeSeconds : d == Levels.Difficulty.Hard ? hardTimeSeconds : bestTimeSeconds;
        public float QualityAt(Levels.Difficulty d) => d == Levels.Difficulty.Easy ? easyQuality : d == Levels.Difficulty.Hard ? hardQuality : bestQuality;
        public int BestOfAll => System.Math.Max(bestStars, System.Math.Max(easyStars, hardStars));

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
