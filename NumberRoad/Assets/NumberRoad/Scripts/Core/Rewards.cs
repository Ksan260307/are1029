using System;
using System.Collections.Generic;

namespace NumberRoad.Core
{
    /// <summary>What one press of CLAIM ALL handed out.</summary>
    public sealed class RewardSummary
    {
        public int Coins;
        public int Gems;
        public int Awards;
        public bool Login;
        public bool Lucky;
        /// <summary>One line per source, e.g. "DAILY BONUS: 120 COINS".</summary>
        public readonly List<string> Lines = new List<string>();

        public bool Any => Awards > 0 || Login || Lucky;
    }

    /// <summary>Everything that can be collected right now: finished awards, the daily login bonus and the lucky draw.</summary>
    public static class Rewards
    {
        public static int ReadyCount(SaveData save, int today)
        {
            int n = Achievements.ReadyCount(save);
            if (Daily.LoginRewardReady(save, today)) n++;
            if (Daily.LuckyReady(save, today)) n++;
            return n;
        }

        /// <summary>
        /// Collects every reward that is ready in one go. <paramref name="freeUpgrade"/> names the upgrade a
        /// FREE UPGRADE prize goes to (asked again for each prize, so two of them can land on different upgrades).
        /// </summary>
        public static RewardSummary ClaimAll(SaveData save, int today, Func<UpgradeKind> freeUpgrade)
        {
            var summary = new RewardSummary();
            int coins = save.coins, gems = save.gems;
            if (Daily.LoginRewardReady(save, today))
            {
                Prize prize = Daily.LoginPrize(Daily.StreakDay(save), save.bestLevel);
                save.lastLoginRewardDay = today;
                Daily.Grant(save, prize, freeUpgrade());
                summary.Login = true;
                summary.Lines.Add("DAILY BONUS: " + prize.Label);
            }
            if (Daily.LuckyReady(save, today))
            {
                Prize[] prizes = Daily.LuckyPrizes(save.bestLevel);
                Prize prize = prizes[Daily.LuckyIndex(today, prizes.Length)];
                save.lastLuckyDay = today;
                Daily.Grant(save, prize, freeUpgrade());
                summary.Lucky = true;
                summary.Lines.Add("LUCKY DRAW: " + prize.Label);
            }
            foreach (Achievement a in Achievements.All)
            {
                if (Achievements.Claim(save, a)) summary.Awards++;
            }
            if (summary.Awards > 0) summary.Lines.Add(summary.Awards == 1 ? "1 AWARD" : summary.Awards + " AWARDS");
            summary.Coins = save.coins - coins;
            summary.Gems = save.gems - gems;
            return summary;
        }
    }
}
