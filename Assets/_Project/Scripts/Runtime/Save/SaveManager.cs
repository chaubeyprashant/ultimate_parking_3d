using System;
using System.IO;
using UnityEngine;

namespace UltimateCarParking.Save
{
    /// <summary>
    /// Loads and writes <see cref="SaveData"/> as JSON in <c>Application.persistentDataPath</c>.
    /// Writes go to a temp file first and the previous save is kept as <c>.bak</c>, so a crash mid-write or a
    /// corrupt file falls back to the last good save instead of wiping progress.
    /// One shared instance (<see cref="Current"/>), created on first use from any scene.
    /// </summary>
    public sealed class SaveManager
    {
        public const string FileName = "ucp_save.json";

        private static SaveManager current;

        /// <summary>The app-wide save, loaded on first access.</summary>
        public static SaveManager Current => current ??= new SaveManager(DefaultPath);

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Replaces <see cref="Current"/> with a save at another path (tests, profiles).</summary>
        public static SaveManager Initialize(string path)
        {
            current = new SaveManager(path);
            return current;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => current = null;

        public string FilePath { get; }
        public string BackupPath => FilePath + ".bak";
        public SaveData Data { get; private set; }

        /// <summary>Raised after every successful <see cref="Save"/> (UI coin counters listen to this).</summary>
        public event Action<SaveData> Saved;

        public SaveManager(string filePath)
        {
            FilePath = filePath;
            Data = Load();
        }

        public SaveData Load()
        {
            SaveData data = TryRead(FilePath) ?? TryRead(BackupPath);
            if (data == null)
            {
                if (File.Exists(FilePath)) Debug.LogWarning($"[Save] {FilePath} unreadable and no usable backup; starting fresh.");
                data = new SaveData();
            }
            Migrate(data);
            return data;
        }

        public bool Save()
        {
            try
            {
                Data.version = SaveData.CurrentVersion;
                Data.lastSavedUtcTicks = DateTime.UtcNow.Ticks;
                string dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(Data, true));
                if (File.Exists(FilePath))
                {
                    File.Copy(FilePath, BackupPath, true);
                    File.Delete(FilePath);
                }
                File.Move(tmp, FilePath);
                Saved?.Invoke(Data);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Could not write {FilePath}: {e.Message}");
                return false;
            }
        }

        /// <summary>Wipes progress (debug / "reset progress" option).</summary>
        public void ResetProgress()
        {
            Data = new SaveData();
            Save();
        }

        /// <summary>Brings older saves up to <see cref="SaveData.CurrentVersion"/> and repairs missing lists.</summary>
        public static void Migrate(SaveData data)
        {
            data.ownedCarIds ??= new System.Collections.Generic.List<string>();
            data.levels ??= new System.Collections.Generic.List<LevelProgress>();
            data.levels.RemoveAll(l => l == null || string.IsNullOrEmpty(l.levelId));
            data.selectedCarId ??= "";
            data.settings ??= new UltimateCarParking.Settings.GameSettings();
            data.settings.Clamp();
            if (data.coins < 0) data.coins = 0;

            if (data.version > SaveData.CurrentVersion)
            {
                Debug.LogWarning($"[Save] Save version {data.version} is newer than this build ({SaveData.CurrentVersion}); reading what is understood.");
                return;
            }
            // Version 0 → 1: files written before versioning had no version field (JsonUtility leaves it 0).
            if (data.version < 1) data.version = 1;
            // Version 1 → 2: settings added (defaults already filled in above).
            if (data.version < 2) data.version = 2;
        }

        private static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not read {path}: {e.Message}");
                return null;
            }
        }
    }
}
