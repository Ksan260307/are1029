using System;
using UnityEngine;

namespace NumberRoad.Core
{
    public enum PrizeKind
    {
        Coins,
        Gems,
        FreeUpgrade,
        DoubleCoins,
    }

    public readonly struct Prize
    {
        public readonly PrizeKind Kind;
        public readonly int Amount;

        public Prize(PrizeKind kind, int amount)
        {
            Kind = kind;
            Amount = amount;
        }

        public string Label
        {
            get
            {
                switch (Kind)
                {
                    case PrizeKind.Gems: return Amount + " GEMS";
                    case PrizeKind.FreeUpgrade: return "FREE UPGRADE";
                    case PrizeKind.DoubleCoins: return "x2 COINS NEXT RUN";
                    default: return Fmt.Coins(Amount) + " COINS";
                }
            }
        }
    }

    /// <summary>Daily login streak, lucky draw, daily challenge and offline earnings. Days are local calendar days.</summary>
    public static class Daily
    {
        static readonly DateTime Epoch = new DateTime(2020, 1, 1);

        public static int DayNumber(DateTime localNow) => (int)(localNow.Date - Epoch).TotalDays;

        public static int Today => DayNumber(DateTime.Now);

        public static long NowUnix => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>Updates the login streak for today. Call once at start-up.</summary>
        public static void RegisterLogin(SaveData save, int today)
        {
            if (save.loginDay == today) return;
            save.loginStreak = save.loginDay == today - 1 ? save.loginStreak + 1 : 1;
            save.loginDay = today;
        }

        public static bool LoginRewardReady(SaveData save, int today) => save.lastLoginRewardDay != today;

        /// <summary>Day 1..7 of the weekly cycle (day 7 is the big one).</summary>
        public static int StreakDay(SaveData save) => (Mathf.Max(1, save.loginStreak) - 1) % 7 + 1;

        public static Prize LoginPrize(int streakDay, int level)
        {
            int scale = 1 + Mathf.Max(1, level) / 5;
            switch (streakDay)
            {
                case 1: return new Prize(PrizeKind.Coins, 100 * scale);
                case 2: return new Prize(PrizeKind.Coins, 200 * scale);
                case 3: return new Prize(PrizeKind.Gems, 5);
                case 4: return new Prize(PrizeKind.Coins, 400 * scale);
                case 5: return new Prize(PrizeKind.Gems, 10);
                case 6: return new Prize(PrizeKind.Coins, 800 * scale);
                default: return new Prize(PrizeKind.Gems, 25);
            }
        }

        public static bool LuckyReady(SaveData save, int today) => save.lastLuckyDay != today;

        public static Prize[] LuckyPrizes(int level)
        {
            int scale = 1 + Mathf.Max(1, level) / 5;
            return new[]
            {
                new Prize(PrizeKind.Coins, 50 * scale),
                new Prize(PrizeKind.Gems, 3),
                new Prize(PrizeKind.Coins, 150 * scale),
                new Prize(PrizeKind.FreeUpgrade, 1),
                new Prize(PrizeKind.Coins, 300 * scale),
                new Prize(PrizeKind.Gems, 8),
                new Prize(PrizeKind.DoubleCoins, 1),
                new Prize(PrizeKind.Coins, 600 * scale),
            };
        }

        /// <summary>Deterministic per day so the result can't be re-rolled by reopening the game.</summary>
        public static int LuckyIndex(int today, int count) => (int)(new Rng(today * 31 + 7).NextUInt() % (uint)count);

        public static bool ChallengeReady(SaveData save, int today) => save.lastChallengeDay != today;

        /// <summary>Today's challenge: a bit ahead of the player, with double hordes.</summary>
        public static int ChallengeLevel(SaveData save) => save.level + 3;

        public const int ChallengeGems = 5;

        /// <summary>Coins earned while away: per hour scales with level, capped at 8 hours, nothing under 10 minutes.</summary>
        public static int OfflineCoins(SaveData save, long nowUnix)
        {
            if (save.lastSeenUnix <= 0) return 0;
            double seconds = nowUnix - save.lastSeenUnix;
            if (seconds < 600) return 0;
            double hours = Math.Min(8.0, seconds / 3600.0);
            return (int)Math.Round(hours * (20 + save.level * 6) * save.IncomeMultiplier);
        }

        public static void Grant(SaveData save, Prize prize, UpgradeKind freeUpgrade)
        {
            switch (prize.Kind)
            {
                case PrizeKind.Coins: save.coins += prize.Amount; break;
                case PrizeKind.Gems: save.gems += prize.Amount; break;
                case PrizeKind.FreeUpgrade: save.SetUpgrade(freeUpgrade, save.GetUpgrade(freeUpgrade) + 1); break;
                case PrizeKind.DoubleCoins: save.doubleCoinsNextRun = true; break;
            }
        }
    }
}
