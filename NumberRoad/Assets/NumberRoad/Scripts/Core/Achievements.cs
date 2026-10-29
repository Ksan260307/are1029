using System;
using System.Collections.Generic;

namespace NumberRoad.Core
{
    public enum AchievementMetric
    {
        Kills,
        LevelsCleared,
        MaxArmy,
        Bosses,
        BestCombo,
        Gates,
        Perfect,
        ClassUps,
        GoldGoblins,
        Discovered,
    }

    public sealed class Achievement
    {
        public readonly string Id;
        public readonly string Title;
        public readonly AchievementMetric Metric;
        public readonly long Target;
        public readonly int Gems;
        public readonly int Coins;

        public Achievement(string id, string title, AchievementMetric metric, long target, int gems, int coins)
        {
            Id = id;
            Title = title;
            Metric = metric;
            Target = target;
            Gems = gems;
            Coins = coins;
        }
    }

    /// <summary>30 achievements: 10 metrics x 3 tiers, each paying gems and coins once.</summary>
    public static class Achievements
    {
        static readonly (AchievementMetric metric, string name, long[] targets)[] Specs =
        {
            (AchievementMetric.Kills, "SLAYER", new long[] { 500, 5000, 50000 }),
            (AchievementMetric.LevelsCleared, "CONQUEROR", new long[] { 5, 20, 50 }),
            (AchievementMetric.MaxArmy, "WARLORD", new long[] { 100, 1000, 10000 }),
            (AchievementMetric.Bosses, "BOSS HUNTER", new long[] { 3, 15, 40 }),
            (AchievementMetric.BestCombo, "COMBO MASTER", new long[] { 50, 200, 500 }),
            (AchievementMetric.Gates, "GATEKEEPER", new long[] { 50, 300, 1000 }),
            (AchievementMetric.Perfect, "PERFECTIONIST", new long[] { 10, 50, 200 }),
            (AchievementMetric.ClassUps, "MENTOR", new long[] { 3, 20, 60 }),
            (AchievementMetric.GoldGoblins, "TREASURE HUNTER", new long[] { 1, 10, 30 }),
            (AchievementMetric.Discovered, "SCHOLAR", new long[] { 10, 20, Enemies.All.Length }),
        };

        static readonly int[] GemRewards = { 5, 10, 20 };
        static readonly int[] CoinRewards = { 200, 1000, 5000 };
        static readonly string[] Tiers = { "I", "II", "III" };

        public static readonly IReadOnlyList<Achievement> All = Build();

        static List<Achievement> Build()
        {
            var list = new List<Achievement>();
            foreach (var spec in Specs)
            {
                for (int t = 0; t < 3; t++)
                {
                    list.Add(new Achievement($"{spec.metric}_{t}", $"{spec.name} {Tiers[t]}", spec.metric, spec.targets[t], GemRewards[t], CoinRewards[t]));
                }
            }
            return list;
        }

        public static long Progress(SaveData save, AchievementMetric metric)
        {
            switch (metric)
            {
                case AchievementMetric.Kills: return save.statKills;
                case AchievementMetric.LevelsCleared: return save.statWins;
                case AchievementMetric.MaxArmy: return save.statMaxArmy;
                case AchievementMetric.Bosses: return save.statBosses;
                case AchievementMetric.BestCombo: return save.statBestCombo;
                case AchievementMetric.Gates: return save.statGates;
                case AchievementMetric.Perfect: return save.statPerfect;
                case AchievementMetric.ClassUps: return save.statClassUps;
                case AchievementMetric.GoldGoblins: return save.statGoldGoblins;
                default: return save.EnemiesDiscovered;
            }
        }

        public static string Describe(Achievement a)
        {
            switch (a.Metric)
            {
                case AchievementMetric.Kills: return $"Defeat {Fmt.Num(a.Target)} enemies";
                case AchievementMetric.LevelsCleared: return $"Clear {a.Target} levels";
                case AchievementMetric.MaxArmy: return $"Lead {Fmt.Num(a.Target)} soldiers at once";
                case AchievementMetric.Bosses: return $"Defeat {a.Target} bosses";
                case AchievementMetric.BestCombo: return $"Reach a {a.Target} kill combo";
                case AchievementMetric.Gates: return $"Pass {Fmt.Num(a.Target)} gates";
                case AchievementMetric.Perfect: return $"Pick the best gate {a.Target} times";
                case AchievementMetric.ClassUps: return $"Class up {a.Target} times";
                case AchievementMetric.GoldGoblins: return $"Catch {a.Target} gold goblins";
                default: return $"Discover {a.Target} kinds of enemies";
            }
        }

        public static bool IsClaimed(SaveData save, Achievement a) => save.claimedAchievements.Contains(a.Id);

        public static bool IsReady(SaveData save, Achievement a) => !IsClaimed(save, a) && Progress(save, a.Metric) >= a.Target;

        public static int ReadyCount(SaveData save)
        {
            int n = 0;
            foreach (Achievement a in All)
            {
                if (IsReady(save, a)) n++;
            }
            return n;
        }

        public static bool Claim(SaveData save, Achievement a)
        {
            if (!IsReady(save, a)) return false;
            save.claimedAchievements.Add(a.Id);
            save.gems += a.Gems;
            save.coins += a.Coins;
            return true;
        }
    }
}
