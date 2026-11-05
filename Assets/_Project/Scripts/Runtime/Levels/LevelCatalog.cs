using UnityEngine;

namespace UltimateCarParking.Levels
{
    /// <summary>The ordered list of levels. Order defines "next level".</summary>
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Ultimate Car Parking/Level Catalog", order = 1)]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] private LevelData[] levels = System.Array.Empty<LevelData>();

        public int Count => levels.Length;

        public LevelData Get(int index) => index >= 0 && index < levels.Length ? levels[index] : null;

        public int IndexOf(LevelData level) => level == null ? -1 : System.Array.IndexOf(levels, level);

        public LevelData FindById(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return null;
            foreach (LevelData l in levels)
                if (l != null && l.LevelId == levelId) return l;
            return null;
        }

        /// <summary>The level after <paramref name="level"/>, or null if it is the last (or not in the catalog).</summary>
        public LevelData Next(LevelData level)
        {
            int i = IndexOf(level);
            if (i < 0 && level != null) i = IndexOf(FindById(level.LevelId));
            return i < 0 ? null : Get(i + 1);
        }

        private void OnValidate()
        {
            for (int i = 0; i < levels.Length; i++)
                for (int j = i + 1; j < levels.Length; j++)
                    if (levels[i] != null && levels[j] != null && levels[i].LevelId == levels[j].LevelId)
                        Debug.LogWarning($"[LevelCatalog] duplicate level id '{levels[i].LevelId}' at {i} and {j}.", this);
        }
    }
}
