using UnityEngine;

namespace NumberRoad.Core
{
    /// <summary>
    /// An endless run: one stage after another on the same run until the squad falls. The army, its weapon
    /// power-ups and the buffs carry on from stage to stage, each stage's road is sized for the army that
    /// arrives, and every boss beaten makes the enemies 1% stronger (HP and hits) for the rest of the run.
    /// </summary>
    public sealed class EndlessRun
    {
        public const float PowerPerBoss = 0.01f;

        public readonly int StartLevel;
        public readonly int Seed;
        /// <summary>The record (bosses beaten) before this run started.</summary>
        public readonly int PreviousBest;
        /// <summary>The stage being played (1-based).</summary>
        public int Stage { get; private set; } = 1;
        public int Bosses { get; private set; }

        // what the cleared stages added up to (the stage in progress is not included)
        public int Kills { get; private set; }
        public int Coins { get; private set; }
        public int Peak { get; private set; }
        public int BestCombo { get; private set; }

        /// <summary>This run's ranking entry (updated, not duplicated, when the run ends again after a revive).</summary>
        public RankEntry Entry;

        public EndlessRun(int startLevel, int seed, int previousBest)
        {
            StartLevel = Mathf.Max(1, startLevel);
            Seed = seed;
            PreviousBest = previousBest;
        }

        /// <summary>The level whose road, enemies and boss the current stage uses.</summary>
        public int Level => StartLevel + Stage - 1;

        /// <summary>Varies the road of every stage and every run.</summary>
        public int Salt => Seed + Stage * 7;

        /// <summary>Enemy HP and hit multiplier: 1% more for every boss beaten in this run.</summary>
        public float EnemyPower => Power(Bosses);

        public int PowerPercent => Mathf.RoundToInt((EnemyPower - 1f) * 100f);

        public bool NewBest => Bosses > PreviousBest;

        public static float Power(int bosses) => Mathf.Pow(1f + PowerPerBoss, Mathf.Max(0, bosses));

        /// <summary>The boss of the current stage fell: its numbers join the run's totals and the next stage begins.</summary>
        public void StageCleared(int kills, int coins, int peak, int combo)
        {
            Kills += kills;
            Coins += coins;
            Peak = Mathf.Max(Peak, peak);
            BestCombo = Mathf.Max(BestCombo, combo);
            Bosses++;
            Stage++;
        }
    }
}
