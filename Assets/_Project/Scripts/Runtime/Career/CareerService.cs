using System.Collections.Generic;
using UnityEngine;
using UltimateCarParking.Core;
using UltimateCarParking.Levels;
using UltimateCarParking.Progression;
using UltimateCarParking.Save;

namespace UltimateCarParking.Career
{
    public enum ChapterState { Locked, Open, Completed, Mastered }

    /// <summary>A career reward just paid: a chapter completion or its mastery bonus.</summary>
    public readonly struct CareerReward
    {
        public readonly int ChapterIndex;
        public readonly string Title;
        public readonly int Coins;
        public readonly bool Mastery;
        public CareerReward(int chapter, string title, int coins, bool mastery) { ChapterIndex = chapter; Title = title; Coins = coins; Mastery = mastery; }
        public string Label => Mastery ? $"{Title} MASTERED +{Coins:N0}" : $"{Title} COMPLETE +{Coins:N0}";
    }

    /// <summary>
    /// Career rules, derived entirely from the existing per-level progress (stars, best times), so old saves need no
    /// conversion: a chapter opens once enough of the previous chapter is completed; inside a chapter a mission opens
    /// once the one before it is completed; completed missions are always replayable. Chapter and mastery rewards are
    /// paid once (ids in <see cref="SaveData.careerRewardIds"/>).
    /// </summary>
    public static class CareerService
    {
        public static int Completed(SaveData save, CareerChapter ch)
        {
            int n = 0;
            for (int i = 0; i < ch.Count; i++) if (ProgressionService.IsCompleted(save, ch.Get(i))) n++;
            return n;
        }

        public static int Stars(SaveData save, CareerChapter ch)
        {
            int n = 0;
            for (int i = 0; i < ch.Count; i++) n += ProgressionService.BestStars(save, ch.Get(i));
            return n;
        }

        public static bool IsChapterOpen(SaveData save, CareerCatalog career, int index)
        {
            if (TestingUnlocks.UnlockAll) return true;
            CareerChapter ch = career.Chapter(index);
            if (ch == null) return false;
            if (index == 0 || ch.unlockAfter <= 0) return true;
            CareerChapter prev = career.Chapter(index - 1);
            return Completed(save, prev) >= Mathf.Min(ch.unlockAfter, prev.Count) && IsChapterOpen(save, career, index - 1);
        }

        public static ChapterState State(SaveData save, CareerCatalog career, int index)
        {
            CareerChapter ch = career.Chapter(index);
            if (ch == null || !IsChapterOpen(save, career, index)) return ChapterState.Locked;
            if (ch.Count > 0 && Stars(save, ch) == ch.Count * 3) return ChapterState.Mastered;
            if (ch.Count > 0 && Completed(save, ch) == ch.Count) return ChapterState.Completed;
            return ChapterState.Open;
        }

        /// <summary>Open when the chapter is open and the mission is completed (replay) or the previous one is.</summary>
        public static bool IsMissionOpen(SaveData save, CareerCatalog career, int chapter, int mission)
        {
            if (!IsChapterOpen(save, career, chapter)) return false;
            CareerChapter ch = career.Chapter(chapter);
            LevelData l = ch.Get(mission);
            if (l == null) return false;
            if (TestingUnlocks.UnlockAll || mission == 0 || ProgressionService.IsCompleted(save, l)) return true;
            return ProgressionService.IsCompleted(save, ch.Get(mission - 1));
        }

        /// <summary>The first open, not-yet-completed mission of a chapter (or its first mission).</summary>
        public static int NextMission(SaveData save, CareerCatalog career, int chapter)
        {
            CareerChapter ch = career.Chapter(chapter);
            if (ch == null) return -1;
            for (int i = 0; i < ch.Count; i++)
                if (!ProgressionService.IsCompleted(save, ch.Get(i)) && IsMissionOpen(save, career, chapter, i)) return i;
            return 0;
        }

        /// <summary>Overall career progress: completed missions / all missions.</summary>
        public static (int done, int total) Progress(SaveData save, CareerCatalog career)
        {
            int d = 0, t = 0;
            for (int i = 0; i < career.ChapterCount; i++) { d += Completed(save, career.Chapter(i)); t += career.Chapter(i).Count; }
            return (d, t);
        }

        /// <summary>Pays every chapter / mastery reward now earned and not yet paid. Idempotent. Caller saves.</summary>
        public static List<CareerReward> ClaimRewards(SaveData save, CareerCatalog career)
        {
            var paid = new List<CareerReward>();
            if (save == null || career == null) return paid;
            save.careerRewardIds ??= new List<string>();
            for (int i = 0; i < career.ChapterCount; i++)
            {
                CareerChapter ch = career.Chapter(i);
                if (ch.Count == 0) continue;
                if (Completed(save, ch) == ch.Count && !save.careerRewardIds.Contains(ch.id))
                {
                    save.careerRewardIds.Add(ch.id);
                    save.coins += ch.completionCoins;
                    paid.Add(new CareerReward(i, ch.title, ch.completionCoins, false));
                }
                if (Stars(save, ch) == ch.Count * 3 && !save.careerRewardIds.Contains(ch.id + "*"))
                {
                    save.careerRewardIds.Add(ch.id + "*");
                    save.coins += ch.masteryCoins;
                    paid.Add(new CareerReward(i, ch.title, ch.masteryCoins, true));
                }
            }
            return paid;
        }
    }
}
