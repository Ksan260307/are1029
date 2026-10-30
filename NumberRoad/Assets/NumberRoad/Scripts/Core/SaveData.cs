using System;
using System.Collections.Generic;
using UnityEngine;

namespace NumberRoad.Core
{
    public enum UpgradeKind
    {
        Squad,
        Power,
        Rate,
        Income,
        Crit,
        Armor,
        /// <summary>The war wagon's toughness (took the place of the old VOLLEY upgrade).</summary>
        Base,
        Recruit,
    }

    public enum Hint
    {
        Roulette = 1 << 0,
        Mystery = 1 << 1,
        Lock = 1 << 2,
        Ranged = 1 << 3,
        ClassUp = 1 << 5,
        Recruit = 1 << 6,
        Shootable = 1 << 7,
        Burning = 1 << 8,
        Shield = 1 << 9,
        Base = 1 << 10,
        Dash = 1 << 11,
    }

    public enum Quality
    {
        Auto,
        High,
        Low,
    }

    /// <summary>Everything that survives between sessions. Serialized with JsonUtility into PlayerPrefs.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public int level = 1;
        public int bestLevel = 1;
        public int coins;
        public int gems;

        public int squadLevel;
        public int powerLevel;
        public int rateLevel;
        public int incomeLevel;
        public int critLevel;
        public int armorLevel;
        /// <summary>BASE upgrade (the war wagon).</summary>
        public int baseLevel;
        /// <summary>Levels of the old VOLLEY upgrade; moved into <see cref="baseLevel"/> when a save is loaded.</summary>
        public int skillLevel;
        public int recruitLevel;

        public bool sound = true;
        public bool music = true;
        public bool vibration = true;
        public int quality;
        public int startLine;
        /// <summary>Game speed while playing: 1 or 2 (the x2 button).</summary>
        public int gameSpeed = 1;
        /// <summary>FAST MARCH: the squad hurries along while no enemy is near.</summary>
        public bool fastMarch;
        /// <summary>Most bosses beaten in one endless run.</summary>
        public int endlessBest;
        /// <summary>UI language (see <see cref="Language"/>; Japanese unless switched in the settings).</summary>
        public int language;

        /// <summary>Best stars per level (index = level - 1).</summary>
        public List<int> stars = new List<int>();
        /// <summary>Kills per enemy type id (bestiary). A type counts as discovered once seen.</summary>
        public List<int> enemyKills = new List<int>();
        public int enemiesSeenMask;
        /// <summary>Bit per unit class that has ever fought in the squad (barracks).</summary>
        public int unitsSeenMask = 1;
        public int hintsSeen;

        // statistics / achievements
        public long statKills;
        public int statWins;
        public int statLosses;
        public int statBosses;
        public int statMaxArmy;
        public int statBestCombo;
        public int statGates;
        public int statPerfect;
        public int statClassUps;
        public int statGoldGoblins;
        public int statChests;
        public long statCoinsEarned;
        public float statPlaySeconds;
        public List<string> claimedAchievements = new List<string>();

        // daily
        public int loginDay;
        public int loginStreak;
        public int lastLoginRewardDay = -1;
        public int lastLuckyDay = -1;
        public int lastChallengeDay = -1;
        public long lastSeenUnix;
        public bool doubleCoinsNextRun;

        /// <summary>Best runs (see <see cref="Ranking"/>).</summary>
        public List<RankEntry> ranking = new List<RankEntry>();

        public int GetUpgrade(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Squad: return squadLevel;
                case UpgradeKind.Power: return powerLevel;
                case UpgradeKind.Rate: return rateLevel;
                case UpgradeKind.Income: return incomeLevel;
                case UpgradeKind.Crit: return critLevel;
                case UpgradeKind.Armor: return armorLevel;
                case UpgradeKind.Base: return baseLevel;
                default: return recruitLevel;
            }
        }

        public void SetUpgrade(UpgradeKind kind, int value)
        {
            value = Mathf.Max(0, value);
            switch (kind)
            {
                case UpgradeKind.Squad: squadLevel = value; break;
                case UpgradeKind.Power: powerLevel = value; break;
                case UpgradeKind.Rate: rateLevel = value; break;
                case UpgradeKind.Income: incomeLevel = value; break;
                case UpgradeKind.Crit: critLevel = value; break;
                case UpgradeKind.Armor: armorLevel = value; break;
                case UpgradeKind.Base: baseLevel = value; break;
                default: recruitLevel = value; break;
            }
        }

        public int UpgradeCost(UpgradeKind kind) => Balance.UpgradeCost(GetUpgrade(kind));

        public bool CanBuy(UpgradeKind kind) => coins >= UpgradeCost(kind);

        public bool AnyUpgradeAffordable
        {
            get
            {
                foreach (UpgradeKind k in Enum.GetValues(typeof(UpgradeKind)))
                {
                    if (CanBuy(k)) return true;
                }
                return false;
            }
        }

        public bool TryBuy(UpgradeKind kind)
        {
            int cost = UpgradeCost(kind);
            if (coins < cost) return false;
            coins -= cost;
            SetUpgrade(kind, GetUpgrade(kind) + 1);
            return true;
        }

        public UnitLine StartLine
        {
            get
            {
                var line = (UnitLine)Mathf.Clamp(startLine, 0, Units.Lines - 1);
                return Units.IsUnlocked(line, bestLevel) ? line : UnitLine.Bow;
            }
        }

        public int StartSquad => Balance.StartSquad(squadLevel);
        public float IncomeMultiplier => Balance.IncomeMultiplier(incomeLevel);
        public float GuardBonus => Balance.GuardBonus(armorLevel);
        public float RecruitBonus => Balance.RecruitBonus(recruitLevel);

        public WeaponStats StartWeapon =>
            WeaponStats.Create(Balance.RateMultiplier(rateLevel), Balance.PowerMultiplier(powerLevel), Balance.CritChance(critLevel));

        public int StarsFor(int lvl) => lvl >= 1 && lvl <= stars.Count ? stars[lvl - 1] : 0;

        public int TotalStars
        {
            get
            {
                int sum = 0;
                foreach (int s in stars) sum += s;
                return sum;
            }
        }

        public void RecordStars(int lvl, int value)
        {
            if (lvl < 1) return;
            while (stars.Count < lvl) stars.Add(0);
            stars[lvl - 1] = Mathf.Max(stars[lvl - 1], value);
        }

        public int KillsOf(int enemyType) => enemyType >= 0 && enemyType < enemyKills.Count ? enemyKills[enemyType] : 0;

        public void AddKills(int enemyType, int n)
        {
            if (enemyType < 0) return;
            while (enemyKills.Count <= enemyType) enemyKills.Add(0);
            enemyKills[enemyType] += n;
        }

        public bool HasSeenEnemy(int enemyType) => (enemiesSeenMask & (1 << enemyType)) != 0;

        /// <summary>Marks an enemy type as discovered. Returns true the first time.</summary>
        public bool SeeEnemy(int enemyType)
        {
            if (HasSeenEnemy(enemyType)) return false;
            enemiesSeenMask |= 1 << enemyType;
            return true;
        }

        public int EnemiesDiscovered
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Enemies.All.Length; i++)
                {
                    if (HasSeenEnemy(i)) n++;
                }
                return n;
            }
        }

        public bool HasUnit(int classId) => (unitsSeenMask & (1 << classId)) != 0;
        public void SeeUnit(int classId) => unitsSeenMask |= 1 << classId;

        /// <summary>Shows a hint only the first time. Returns true when it should be shown now.</summary>
        public bool TakeHint(Hint hint)
        {
            if ((hintsSeen & (int)hint) != 0) return false;
            hintsSeen |= (int)hint;
            return true;
        }

        public void CompleteLevel()
        {
            level++;
            bestLevel = Mathf.Max(bestLevel, level);
        }

        /// <summary>Level select: any level up to the furthest one reached can be played (again).</summary>
        public void SelectLevel(int value) => level = Mathf.Clamp(value, 1, bestLevel);

        /// <summary>
        /// Starts over: levels, currencies, upgrades, records, awards and the ranking are cleared. Sound and
        /// display settings stay, and so does today's daily bonus state (a reset does not hand out a second one).
        /// </summary>
        public void ResetProgress()
        {
            var fresh = new SaveData
            {
                sound = sound,
                music = music,
                vibration = vibration,
                quality = quality,
                gameSpeed = gameSpeed,
                fastMarch = fastMarch,
                language = language,
                loginDay = loginDay,
                loginStreak = loginStreak,
                lastLoginRewardDay = lastLoginRewardDay,
                lastLuckyDay = lastLuckyDay,
                lastChallengeDay = lastChallengeDay,
                lastSeenUnix = lastSeenUnix,
            };
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), this);
        }

        public void Sanitize()
        {
            version = CurrentVersion;
            level = Mathf.Max(1, level);
            bestLevel = Mathf.Max(level, bestLevel);
            coins = Mathf.Max(0, coins);
            gems = Mathf.Max(0, gems);
            foreach (UpgradeKind k in Enum.GetValues(typeof(UpgradeKind))) SetUpgrade(k, GetUpgrade(k));
            startLine = Mathf.Clamp(startLine, 0, Units.Lines - 1);
            quality = Mathf.Clamp(quality, 0, 2);
            gameSpeed = Mathf.Clamp(gameSpeed, 1, 2);
            language = Mathf.Clamp(language, 0, Enum.GetValues(typeof(Language)).Length - 1);
            // VOLLEY is gone: what was spent on it carries over to the war wagon
            if (skillLevel > 0)
            {
                baseLevel += skillLevel;
                skillLevel = 0;
            }
            endlessBest = Mathf.Max(0, endlessBest);
            stars = stars ?? new List<int>();
            enemyKills = enemyKills ?? new List<int>();
            claimedAchievements = claimedAchievements ?? new List<string>();
            ranking = ranking ?? new List<RankEntry>();
            unitsSeenMask |= 1;
        }
    }

    public static class SaveSystem
    {
        public const string Key = "NumberRoad.Save";

        public static string ToJson(SaveData data) => JsonUtility.ToJson(data);

        public static SaveData FromJson(string json)
        {
            SaveData data = null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    data = JsonUtility.FromJson<SaveData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[NumberRoad] Save data was corrupt and has been reset: {e.Message}");
                }
            }
            data = data ?? new SaveData();
            data.Sanitize();
            return data;
        }

        public static SaveData Load() => FromJson(PlayerPrefs.GetString(Key, string.Empty));

        public static void Save(SaveData data)
        {
            PlayerPrefs.SetString(Key, ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
