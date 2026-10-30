using UnityEngine;

namespace NumberRoad.Core
{
    /// <summary>Every gameplay tuning number lives here so balancing is a one-file job.</summary>
    public static class Balance
    {
        // --- World -----------------------------------------------------------
        public const float RoadHalfWidth = 4f;
        public const float SquadMaxX = 3.2f;
        public const float SoldierEdgeX = 3.7f;
        public const float MarchSpeed = 5f;
        /// <summary>FAST MARCH: how much faster the squad walks while no enemy is near.</summary>
        public const float FastMarch = 1.8f;
        public const float SpawnAhead = 64f;
        public const float DespawnBehind = 14f;
        /// <summary>Hordes start their charge this far ahead, just inside the squad's reach, so the whole crowd pours in under fire.</summary>
        public const float EnemyWakeDistance = 32f;
        public const float BossArenaDistance = 24f;
        /// <summary>Horde rows become live enemies only within this distance ahead; further back they are just a count.</summary>
        public const float SpawnReach = 86f;
        /// <summary>Walking enemies further ahead than this are updated every other frame (too far to touch anything).</summary>
        public const float FarUpdateDistance = 34f;

        // --- Squad -----------------------------------------------------------
        public const int MaxSquad = 999999;
        public const int MaxVisibleSoldiers = 80;
        public const int MaxAliveEnemies = 3000;
        public const int MaxHordeWave = 1400;

        /// <summary>Gap between drawn soldiers: roomy for a few, tightly packed for a big crowd.</summary>
        public static float FormationSpacing(int visible) => Mathf.Lerp(0.38f, 0.27f, Mathf.InverseLerp(10f, MaxVisibleSoldiers, visible));

        public static float SoldierScale(int visible) => Mathf.Lerp(0.9f, 0.78f, Mathf.InverseLerp(20f, MaxVisibleSoldiers, visible));

        // --- Weapons ---------------------------------------------------------
        public const float BulletSpeed = 28f;
        public const float MaxBulletsPerSecond = 240f;
        public const int MaxMultiShot = 5;
        public const float MultiShotBonus = 0.55f;
        public const float MultiShotSpreadDegrees = 7f;
        public const float FireRateStep = 0.25f;
        public const float DamageStep = 0.3f;
        public const float BaseCrit = 0.05f;
        public const float CritMultiplier = 2f;

        /// <summary>Total extra damage factor for N bullets per shot (1 bullet = 1.0).</summary>
        public static float MultiShotFactor(int bullets) => 1f + MultiShotBonus * (Mathf.Clamp(bullets, 1, MaxMultiShot) - 1);

        // --- Enemies ---------------------------------------------------------

        /// <summary>Enemies in one horde: a crowd from the very first level (200), growing exponentially (capped from Lv19).</summary>
        public static int HordeSize(int level) => Mathf.RoundToInt(Mathf.Min(MaxHordeWave, 200f * Mathf.Pow(1.115f, Mathf.Clamp(level, 1, 400) - 1)));

        /// <summary>
        /// HP of a basic horde enemy relative to the expected hit (times the level's difficulty, 0.6 to 1): one to one
        /// and a half hits, so a horde lasts long enough to charge in, while strong hits - crits, heavy classes,
        /// buffs - carry on through several (see <see cref="Combat.CarryOver"/>).
        /// </summary>
        public const float SwarmToughness = 1.6f;

        /// <summary>
        /// Soldiers one enemy knocks out when it reaches a big army: armies grow into the hundreds of thousands
        /// while a wave is capped, so each enemy is worth more as the squad grows.
        /// </summary>
        public static int ContactWeight(float squad) => Mathf.Max(1, Mathf.RoundToInt(squad / 400f));

        /// <summary>0.6 at level 1, approaching 1.0. Scales enemy toughness relative to the reference player.</summary>
        public static float Difficulty(int level) => 0.6f + 0.4f * (1f - Mathf.Exp(-(Mathf.Max(1, level) - 1) / 8f));

        public const float EliteHp = 5f;
        public const float EliteScale = 1.3f;
        public const int EliteCoins = 10;
        public const float EliteGemChance = 0.25f;

        // --- Combo / skill -----------------------------------------------------
        public const float ComboWindow = 1.5f;

        /// <summary>Coin multiplier from the kill combo: x2 at a 300 combo (hordes are hundreds strong).</summary>
        public static float ComboMultiplier(int combo) => 1f + Mathf.Min(combo, 300) / 300f;

        /// <summary>
        /// Coins one ordinary kill is worth: a fraction of a coin, since hordes are hundreds strong from the first
        /// level. It grows slowly; the bigger hordes and the coin upgrade make the total per level keep pace with
        /// the upgrade prices.
        /// </summary>
        public static float KillValue(int level) => Mathf.Min(1f, 0.07f * Mathf.Pow(1.035f, Mathf.Max(1, level) - 1));

        /// <summary>Seconds the expected army needs to bring the boss down: long enough for him to reach the front line.</summary>
        public static float BossSeconds(int level) => 9f + Mathf.Min(Mathf.Max(1, level), 25) * 0.3f;

        public static int SkillCharge(int level, int skillUpgrade) => Mathf.RoundToInt((40 + 6 * Mathf.Max(1, level)) * Mathf.Pow(0.93f, skillUpgrade));

        /// <summary>Seconds of full squad damage the VOLLEY deals to everything in front.</summary>
        public static float SkillPower(int skillUpgrade) => 4f + 0.8f * skillUpgrade;

        // --- Meta upgrades (bought with coins on the title screen) -------------
        /// <summary>Soldiers at the start of a run: three, so a missed strip early on is not an instant loss.</summary>
        public static int StartSquad(float upgradeLevel) => 3 + Mathf.FloorToInt(upgradeLevel);
        public static float PowerMultiplier(float upgradeLevel) => 1f + 0.15f * upgradeLevel;
        public static float RateMultiplier(float upgradeLevel) => 1f + 0.08f * upgradeLevel;
        public static float IncomeMultiplier(float upgradeLevel) => 1f + 0.2f * upgradeLevel;
        public static float CritChance(int upgradeLevel) => Mathf.Min(0.6f, BaseCrit + 0.02f * upgradeLevel);
        public static float GuardBonus(int upgradeLevel) => Mathf.Min(0.4f, 0.03f * upgradeLevel);
        public static float RecruitBonus(int upgradeLevel) => 1f + 0.08f * upgradeLevel;

        public static int UpgradeCost(int currentLevel)
        {
            float raw = 40f * Mathf.Pow(1.35f, Mathf.Max(0, currentLevel));
            if (raw > 1e9f) return 1000000000;
            return Mathf.Max(5, Mathf.RoundToInt(raw / 5f) * 5);
        }

        // --- Rewards -----------------------------------------------------------
        public const int CrateCoins = 3;
        /// <summary>Bonus for picking the best gate of a row.</summary>
        public static int PerfectCoins(int level) => 4 + Mathf.Max(1, level);
        /// <summary>Bonus for the n-th good gate in a row (n >= 2).</summary>
        public static int ChainCoins(int chain, int level) => (chain - 1) * (2 + Mathf.Max(1, level) / 3);
        public static int VictoryCoins(int level) => 30 + Mathf.Max(1, level) * 8;
        public static int BossCoins(int level) => 30 + Mathf.Max(1, level) * 5;
        public const int ReviveGems = 8;
        public static int ReviveCoins(int level) => 100 * Mathf.Max(1, level);
        public const int CastleGems = 10;
        public const float GoldRushCoins = 1.5f;

        /// <summary>1-3 stars from the share of the best squad size that survived.</summary>
        public static int Stars(int survivors, int peak)
        {
            if (peak <= 0 || survivors <= 0) return 0;
            float share = survivors / (float)peak;
            if (share >= 0.6f) return 3;
            if (share >= 0.3f) return 2;
            return 1;
        }

        /// <summary>Rounds to "game-looking" numbers: exact below 20, then multiples of 5 / 10 / ...</summary>
        public static int Nice(float value)
        {
            float abs = Mathf.Abs(value);
            int sign = value < 0 ? -1 : 1;
            if (abs < 20f) return sign * Mathf.Max(1, Mathf.RoundToInt(abs));
            if (abs < 200f) return sign * Mathf.RoundToInt(abs / 5f) * 5;
            if (abs < 2000f) return sign * Mathf.RoundToInt(abs / 10f) * 10;
            if (abs < 20000f) return sign * Mathf.RoundToInt(abs / 100f) * 100;
            return sign * Mathf.Min(MaxSquad, Mathf.RoundToInt(abs / 1000f) * 1000);
        }
    }
}
