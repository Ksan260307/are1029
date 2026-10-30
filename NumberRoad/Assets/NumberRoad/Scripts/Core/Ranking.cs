using System;
using System.Collections.Generic;
using UnityEngine;

namespace NumberRoad.Core
{
    /// <summary>One finished run in the ranking (stored in <see cref="SaveData.ranking"/>).</summary>
    [Serializable]
    public sealed class RankEntry
    {
        public int score;
        public int level;
        public int kills;
        public int army;
        public int combo;
        public bool won;
        public bool daily;
        /// <summary>An endless run: <see cref="stage"/> is where it ended.</summary>
        public bool endless;
        public int stage;
        public long unix;
    }

    public enum RankMetric
    {
        Score,
        Army,
        Combo,
    }

    /// <summary>
    /// The on-device ranking: the best 10 runs by score, by largest army and by best combo.
    /// Only entries that are in the top 10 of at least one list are kept.
    /// </summary>
    public static class Ranking
    {
        public const int Size = 10;

        /// <summary>
        /// A run's score: kills, combo and army size, plus a bonus for clearing the level and its stars
        /// (and for every boss beaten in an endless run).
        /// </summary>
        public static int Score(int kills, int army, int combo, int level, bool won, int stars, int bosses = 0)
        {
            long score = (long)kills * 10 + (long)combo * 25 + Math.Min(army, Balance.MaxSquad)
                         + (won ? Mathf.Max(1, level) * 1000L + stars * 500L : Mathf.Max(1, level) * 100L)
                         + Math.Max(0, bosses) * 1500L;
            return (int)Math.Min(int.MaxValue, score);
        }

        public static long Value(RankEntry e, RankMetric metric)
        {
            switch (metric)
            {
                case RankMetric.Army: return e.army;
                case RankMetric.Combo: return e.combo;
                default: return e.score;
            }
        }

        /// <summary>The best runs for one list, highest first (ties: the newer run first).</summary>
        public static List<RankEntry> Top(SaveData save, RankMetric metric)
        {
            var list = new List<RankEntry>(save.ranking);
            list.Sort((a, b) =>
            {
                int c = Value(b, metric).CompareTo(Value(a, metric));
                return c != 0 ? c : b.unix.CompareTo(a.unix);
            });
            if (list.Count > Size) list.RemoveRange(Size, list.Count - Size);
            return list;
        }

        /// <summary>1-based place this entry has in a list, or 0 when it is outside the top 10.</summary>
        public static int PlaceOf(SaveData save, RankEntry entry, RankMetric metric)
        {
            List<RankEntry> top = Top(save, metric);
            int index = top.IndexOf(entry);
            return index >= 0 ? index + 1 : 0;
        }

        /// <summary>Adds a run (or updates it when the same run is recorded again after a revive) and prunes the rest.</summary>
        public static void Record(SaveData save, RankEntry entry)
        {
            if (!save.ranking.Contains(entry)) save.ranking.Add(entry);
            var keep = new HashSet<RankEntry>();
            foreach (RankMetric m in (RankMetric[])Enum.GetValues(typeof(RankMetric)))
            {
                foreach (RankEntry e in Top(save, m)) keep.Add(e);
            }
            save.ranking.RemoveAll(e => !keep.Contains(e));
        }
    }
}
