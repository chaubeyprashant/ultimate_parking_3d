using System;
using UnityEngine;
using UltimateCarParking.Levels;

namespace UltimateCarParking.Career
{
    /// <summary>One career chapter: a themed list of existing missions plus its rewards.</summary>
    [Serializable]
    public sealed class CareerChapter
    {
        [Tooltip("Stable id stored in the save (reward claims). Do not change after release.")]
        public string id = "ch1";
        public string title = "BEGINNER ACADEMY";
        [TextArea(1, 3)] public string subtitle = "";
        [Tooltip("The chapter's missions, in play order (references to existing campaign / challenge levels).")]
        public LevelCatalog missions;
        [Tooltip("Missions of the previous chapter that must be completed to open this one (0 = always open).")]
        [Min(0)] public int unlockAfter;
        [Tooltip("Paid once when every mission of the chapter is completed.")]
        [Min(0)] public int completionCoins = 1000;
        [Tooltip("Optional mastery challenge: every mission with three stars. Paid once.")]
        [Min(0)] public int masteryCoins = 500;
        public Color accent = new Color(0.71f, 1f, 0.18f);

        public int Count => missions != null ? missions.Count : 0;
        public LevelData Get(int i) => missions != null ? missions.Get(i) : null;
        public int IndexOf(LevelData level) => missions != null ? missions.IndexOf(level) : -1;
    }

    /// <summary>
    /// The career: five chapters over the existing missions (nothing is copied or rewritten, so level ids, stars and
    /// best times carry over) plus the Driving Academy lessons, which are always open.
    /// </summary>
    [CreateAssetMenu(fileName = "CareerCatalog", menuName = "Ultimate Car Parking/Career Catalog", order = 30)]
    public sealed class CareerCatalog : ScriptableObject
    {
        [SerializeField] private CareerChapter[] chapters = Array.Empty<CareerChapter>();
        [Tooltip("Driving Academy: short interactive lessons (always open, small first-time rewards).")]
        [SerializeField] private LevelCatalog academy;

        public int ChapterCount => chapters != null ? chapters.Length : 0;
        public CareerChapter Chapter(int i) => i >= 0 && i < ChapterCount ? chapters[i] : null;
        public LevelCatalog Academy => academy;

        /// <summary>Chapter index holding a level, or -1.</summary>
        public int ChapterOf(LevelData level)
        {
            for (int i = 0; i < ChapterCount; i++) if (chapters[i].IndexOf(level) >= 0) return i;
            return -1;
        }
    }
}
