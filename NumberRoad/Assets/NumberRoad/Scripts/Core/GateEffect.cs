using System;
using UnityEngine;

namespace NumberRoad.Core
{
    public enum GateKind
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        FireRate,
        Damage,
        MultiShot,
    }

    /// <summary>Per-run weapon state, reset every level (meta upgrades set the starting point).</summary>
    [Serializable]
    public struct WeaponStats
    {
        public float fireRateMul;
        public float damageMul;
        public int multiShot;

        public static WeaponStats Create(float fireRateMul, float damageMul)
        {
            return new WeaponStats { fireRateMul = fireRateMul, damageMul = damageMul, multiShot = 1 };
        }

        /// <summary>0..3, drives bullet colour so upgrades are visible.</summary>
        public int Tier
        {
            get
            {
                float score = (fireRateMul - 1f) + (damageMul - 1f) + (multiShot - 1) * 0.6f;
                if (score >= 2.4f) return 3;
                if (score >= 1.2f) return 2;
                if (score >= 0.4f) return 1;
                return 0;
            }
        }
    }

    /// <summary>What a gate, a strip tile or a crate does to the squad.</summary>
    [Serializable]
    public struct GateEffect
    {
        public GateKind kind;
        public int value;

        public GateEffect(GateKind kind, int value = 0)
        {
            this.kind = kind;
            this.value = value;
        }

        public static GateEffect Add(int n) => new GateEffect(GateKind.Add, n);
        public static GateEffect Subtract(int n) => new GateEffect(GateKind.Subtract, n);
        public static GateEffect Multiply(int n) => new GateEffect(GateKind.Multiply, n);
        public static GateEffect Divide(int n) => new GateEffect(GateKind.Divide, n);
        public static GateEffect FireRate() => new GateEffect(GateKind.FireRate, 1);
        public static GateEffect Damage() => new GateEffect(GateKind.Damage, 1);
        public static GateEffect MultiShot() => new GateEffect(GateKind.MultiShot, 1);

        public bool AffectsCount => kind <= GateKind.Divide;

        /// <summary>True when passing through helps the player (drives blue vs red colouring).</summary>
        public bool IsBeneficial
        {
            get
            {
                switch (kind)
                {
                    case GateKind.Add: return value >= 0;
                    case GateKind.Subtract: return value <= 0;
                    case GateKind.Multiply: return value >= 1;
                    case GateKind.Divide: return value <= 1;
                    default: return true;
                }
            }
        }

        public string Label
        {
            get
            {
                switch (kind)
                {
                    case GateKind.Add: return value >= 0 ? "+" + value : value.ToString();
                    case GateKind.Subtract: return "-" + value;
                    case GateKind.Multiply: return "×" + value;
                    case GateKind.Divide: return "÷" + value;
                    case GateKind.FireRate: return "RATE+";
                    case GateKind.Damage: return "POWER+";
                    case GateKind.MultiShot: return "MULTI+";
                    default: return "?";
                }
            }
        }

        /// <summary>Short text for the floating popup after the effect is applied.</summary>
        public string PopupText
        {
            get
            {
                switch (kind)
                {
                    case GateKind.FireRate: return "FIRE RATE UP!";
                    case GateKind.Damage: return "POWER UP!";
                    case GateKind.MultiShot: return "MULTI SHOT!";
                    default: return Label;
                }
            }
        }

        public int ApplyToCount(int count)
        {
            long result = count;
            switch (kind)
            {
                case GateKind.Add: result = (long)count + value; break;
                case GateKind.Subtract: result = (long)count - value; break;
                case GateKind.Multiply: result = (long)count * Math.Max(0, value); break;
                case GateKind.Divide: result = value <= 1 ? count : (count + value - 1) / value; break;
            }
            return (int)Math.Max(0, Math.Min(Balance.MaxSquad, result));
        }

        public WeaponStats ApplyToWeapon(WeaponStats weapon)
        {
            switch (kind)
            {
                case GateKind.FireRate: weapon.fireRateMul += Balance.FireRateStep * Mathf.Max(1, value); break;
                case GateKind.Damage: weapon.damageMul += Balance.DamageStep * Mathf.Max(1, value); break;
                case GateKind.MultiShot: weapon.multiShot = Mathf.Min(Balance.MaxMultiShot, weapon.multiShot + Mathf.Max(1, value)); break;
            }
            return weapon;
        }

        public override string ToString() => $"{kind}({value})";
    }
}
