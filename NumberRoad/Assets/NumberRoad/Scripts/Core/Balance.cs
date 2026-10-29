using UnityEngine;

namespace NumberRoad.Core
{
    public enum EnemyKind
    {
        Grunt,
        Runner,
        Brute,
        Boss,
    }

    /// <summary>Static description of an enemy type. Hit points come from the level generator.</summary>
    public readonly struct EnemyArchetype
    {
        public readonly float Speed;
        public readonly float Radius;
        public readonly float Scale;
        public readonly bool Melee;
        public readonly float AttackInterval;
        public readonly int Coins;
        public readonly string Model;

        public EnemyArchetype(float speed, float radius, float scale, bool melee, float attackInterval, int coins, string model)
        {
            Speed = speed;
            Radius = radius;
            Scale = scale;
            Melee = melee;
            AttackInterval = attackInterval;
            Coins = coins;
            Model = model;
        }
    }

    public readonly struct FirePlan
    {
        public readonly float Interval;
        public readonly float Damage;
        public readonly int Shots;

        public FirePlan(float interval, float damage, int shots)
        {
            Interval = interval;
            Damage = damage;
            Shots = shots;
        }
    }

    /// <summary>Every gameplay tuning number lives here so balancing is a one-file job.</summary>
    public static class Balance
    {
        // --- World -----------------------------------------------------------
        public const float RoadHalfWidth = 4f;
        public const float SquadMaxX = 3.2f;
        public const float SoldierEdgeX = 3.7f;
        public const float MarchSpeed = 5f;
        public const float SpawnAhead = 64f;
        public const float DespawnBehind = 14f;
        public const float EnemyWakeDistance = 25f;
        public const float BossArenaDistance = 24f;

        // --- Squad -----------------------------------------------------------
        public const int MaxSquad = 9999;
        public const int MaxVisibleSoldiers = 40;
        public const float FormationSpacing = 0.38f;
        public const float SoldierScale = 0.9f;

        // --- Weapons ---------------------------------------------------------
        public const float BaseFireRate = 2.0f;
        public const float BaseDamage = 1f;
        public const float BulletSpeed = 28f;
        public const float BulletRange = 27f;
        public const float MaxBulletsPerSecond = 110f;
        public const int MaxMultiShot = 5;
        public const float MultiShotBonus = 0.55f;
        public const float MultiShotSpreadDegrees = 7f;
        public const float FireRateStep = 0.25f;
        public const float DamageStep = 0.3f;

        // --- Economy ---------------------------------------------------------
        public const int CrateCoins = 3;

        public static EnemyArchetype Archetype(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Runner: return new EnemyArchetype(4.2f, 0.28f, 0.68f, false, 0f, 1, "grunt");
                case EnemyKind.Brute: return new EnemyArchetype(1.5f, 0.85f, 1.0f, true, 1.25f, 8, "brute");
                case EnemyKind.Boss: return new EnemyArchetype(0.95f, 1.45f, 1.75f, true, 1.6f, 30, "boss");
                default: return new EnemyArchetype(2.3f, 0.32f, 0.82f, false, 0f, 1, "grunt");
            }
        }

        /// <summary>Total extra damage factor for N bullets per shot (1 bullet = 1.0).</summary>
        public static float MultiShotFactor(int bullets) => 1f + MultiShotBonus * (Mathf.Clamp(bullets, 1, MaxMultiShot) - 1);

        /// <summary>Damage per second of a whole squad. The bullet system distributes exactly this.</summary>
        public static float SquadDps(float count, WeaponStats weapon)
        {
            return count * BaseFireRate * weapon.fireRateMul * BaseDamage * weapon.damageMul * MultiShotFactor(weapon.multiShot);
        }

        /// <summary>
        /// How the visible soldiers fire: each fires every <see cref="FirePlan.Interval"/> seconds,
        /// <see cref="FirePlan.Shots"/> bullets at a time. Bullet count is capped for performance and
        /// damage per bullet scaled so the total always equals <see cref="SquadDps"/>.
        /// </summary>
        public static FirePlan PlanFire(int count, int visible, WeaponStats weapon)
        {
            visible = Mathf.Max(1, visible);
            float rate = BaseFireRate * weapon.fireRateMul;
            int shots = Mathf.Clamp(weapon.multiShot, 1, MaxMultiShot);
            float throttle = Mathf.Max(1f, visible * rate * shots / MaxBulletsPerSecond);
            float damage = BaseDamage * weapon.damageMul * (count / (float)visible) * (MultiShotFactor(shots) / shots) * throttle;
            return new FirePlan(throttle / rate, damage, shots);
        }

        /// <summary>Bullets per second the squad actually emits (visual count, capped).</summary>
        public static float BulletsPerSecond(int count, WeaponStats weapon)
        {
            int visible = Mathf.Min(count, MaxVisibleSoldiers);
            return Mathf.Min(MaxBulletsPerSecond, visible * BaseFireRate * weapon.fireRateMul * weapon.multiShot);
        }

        /// <summary>0.6 at level 1, approaching 1.0. Scales enemy HP relative to the reference player.</summary>
        public static float Difficulty(int level)
        {
            return 0.6f + 0.4f * (1f - Mathf.Exp(-(Mathf.Max(1, level) - 1) / 8f));
        }

        // --- Meta upgrades (bought with coins on the title screen) -------------
        public static int StartSquad(float upgradeLevel) => 1 + Mathf.FloorToInt(upgradeLevel);
        public static float PowerMultiplier(float upgradeLevel) => 1f + 0.15f * upgradeLevel;
        public static float RateMultiplier(float upgradeLevel) => 1f + 0.08f * upgradeLevel;
        public static float IncomeMultiplier(float upgradeLevel) => 1f + 0.2f * upgradeLevel;

        public static int UpgradeCost(int currentLevel)
        {
            float raw = 40f * Mathf.Pow(1.35f, Mathf.Max(0, currentLevel));
            return Mathf.Max(5, Mathf.RoundToInt(raw / 5f) * 5);
        }

        public static int VictoryCoins(int level) => 30 + Mathf.Max(1, level) * 8;
        public static int BossCoins(int level) => 30 + Mathf.Max(1, level) * 5;

        /// <summary>Rounds to "game-looking" numbers: exact below 20, then multiples of 5 / 10.</summary>
        public static int Nice(float value)
        {
            float abs = Mathf.Abs(value);
            int sign = value < 0 ? -1 : 1;
            if (abs < 20f) return sign * Mathf.Max(1, Mathf.RoundToInt(abs));
            if (abs < 200f) return sign * Mathf.RoundToInt(abs / 5f) * 5;
            return sign * Mathf.RoundToInt(abs / 10f) * 10;
        }
    }
}
