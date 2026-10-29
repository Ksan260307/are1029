using System;
using UnityEngine;

namespace NumberRoad.Core
{
    public enum UpgradeKind
    {
        Squad,
        Power,
        Rate,
        Income,
    }

    /// <summary>Everything that survives between sessions. Serialized with JsonUtility into PlayerPrefs.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public int level = 1;
        public int bestLevel = 1;
        public int coins;
        public int squadLevel;
        public int powerLevel;
        public int rateLevel;
        public int incomeLevel;
        public bool sound = true;
        public bool music = true;

        public int GetUpgrade(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Squad: return squadLevel;
                case UpgradeKind.Power: return powerLevel;
                case UpgradeKind.Rate: return rateLevel;
                default: return incomeLevel;
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
                default: incomeLevel = value; break;
            }
        }

        public int UpgradeCost(UpgradeKind kind) => Balance.UpgradeCost(GetUpgrade(kind));

        public bool CanBuy(UpgradeKind kind) => coins >= UpgradeCost(kind);

        public bool TryBuy(UpgradeKind kind)
        {
            int cost = UpgradeCost(kind);
            if (coins < cost) return false;
            coins -= cost;
            SetUpgrade(kind, GetUpgrade(kind) + 1);
            return true;
        }

        public int StartSquad => Balance.StartSquad(squadLevel);
        public float IncomeMultiplier => Balance.IncomeMultiplier(incomeLevel);
        public WeaponStats StartWeapon => WeaponStats.Create(Balance.RateMultiplier(rateLevel), Balance.PowerMultiplier(powerLevel));

        public void CompleteLevel()
        {
            level++;
            bestLevel = Mathf.Max(bestLevel, level);
        }

        public void Sanitize()
        {
            level = Mathf.Max(1, level);
            bestLevel = Mathf.Max(level, bestLevel);
            coins = Mathf.Max(0, coins);
            squadLevel = Mathf.Max(0, squadLevel);
            powerLevel = Mathf.Max(0, powerLevel);
            rateLevel = Mathf.Max(0, rateLevel);
            incomeLevel = Mathf.Max(0, incomeLevel);
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
