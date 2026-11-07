using System;
using UnityEngine;
using UltimateCarParking.Levels;
using UltimateCarParking.Save;

namespace UltimateCarParking.Progression
{
    /// <summary>
    /// Gameplay-scene bridge: when the LevelManager reports a completion, records stars, pays coins and saves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProgressionRecorder : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;

        public event Action<LevelReward> RewardGranted;
        public LevelReward? LastReward { get; private set; }
        /// <summary>Country bonus paid by the last completion (World Tour), if any.</summary>
        public WorldTour.CountryReward? LastCountryReward { get; private set; }
        public event Action<WorldTour.CountryReward> CountryRewardGranted;

        private void Awake()
        {
            if (levelManager == null && !TryGetComponent(out levelManager))
                Debug.LogError("[ProgressionRecorder] No LevelManager assigned; progress will not be saved.", this);
        }

        private void OnEnable()
        {
            if (levelManager != null) levelManager.LevelCompleted += OnCompleted;
        }

        private void OnDisable()
        {
            if (levelManager != null) levelManager.LevelCompleted -= OnCompleted;
        }

        private void OnCompleted(LevelResult result)
        {
            SaveManager save = SaveManager.Current;
            LevelReward reward = ProgressionService.RecordCompletion(save.Data, levelManager.Level, result);
            WorldTour.CountryData country = LevelSelection.Country;
            LastCountryReward = null;
            if (country != null && country.IndexOfMission(levelManager.Level) >= 0)
            {
                WorldTour.CountryReward cr = WorldTour.CountryProgressManager.ClaimRewards(save.Data, country);
                if (cr.Any)
                {
                    LastCountryReward = cr;
                    Debug.Log($"[Progress] {country.DisplayName} bonus: +{cr.CompletionCoins} completion, +{cr.PerfectCoins} all stars");
                }
            }
            save.Save();
            LastReward = reward;
            Debug.Log($"[Progress] {result.LevelId}: +{reward.Coins} coins ({(reward.FirstCompletion ? "first clear" : "replay")}), " +
                      $"best {Mathf.Max(reward.PreviousBestStars, reward.Stars)}★, total {save.Data.coins} coins");
            RewardGranted?.Invoke(reward);
            if (LastCountryReward.HasValue) CountryRewardGranted?.Invoke(LastCountryReward.Value);
        }
    }
}
