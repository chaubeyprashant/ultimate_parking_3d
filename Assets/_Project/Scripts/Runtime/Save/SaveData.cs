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
        public const int CurrentVersion = 2;   // 2: + settings

        public int version = CurrentVersion;
        public int coins;
        public string selectedCarId = "";
        public List<string> ownedCarIds = new List<string>();
        public List<LevelProgress> levels = new List<LevelProgress>();
        public GameSettings settings = new GameSettings();
        public long lastSavedUtcTicks;

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
}
