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
        /// <summary>Adds tier-1 soldiers of <see cref="GateEffect.line"/>.</summary>
        Recruit,
        /// <summary>Promotes every soldier one tier.</summary>
        ClassUp,
    }

    /// <summary>Per-run weapon state, reset every level (meta upgrades set the starting point).</summary>
    [Serializable]
    public struct WeaponStats
    {
        public float fireRateMul;
        public float damageMul;
        public int multiShot;
        public float critChance;

        public static WeaponStats Create(float fireRateMul, float damageMul, float critChance = Balance.BaseCrit)
        {
            return new WeaponStats { fireRateMul = fireRateMul, damageMul = damageMul, multiShot = 1, critChance = critChance };
        }

        /// <summary>Damage multiplier including the average value of critical hits.</summary>
        public float ExpectedCrit => 1f + critChance * (Balance.CritMultiplier - 1f);
    }

    /// <summary>What a gate, a strip tile or a crate does to the squad.</summary>
    [Serializable]
    public struct GateEffect
    {
        public GateKind kind;
        public int value;
        public UnitLine line;

        public GateEffect(GateKind kind, int value = 0, UnitLine line = UnitLine.Bow)
        {
            this.kind = kind;
            this.value = value;
            this.line = line;
        }

        public static GateEffect Add(int n) => new GateEffect(GateKind.Add, n);
        public static GateEffect Subtract(int n) => new GateEffect(GateKind.Subtract, n);
        public static GateEffect Multiply(int n) => new GateEffect(GateKind.Multiply, n);
        public static GateEffect Divide(int n) => new GateEffect(GateKind.Divide, n);
        public static GateEffect FireRate() => new GateEffect(GateKind.FireRate, 1);
        public static GateEffect Damage() => new GateEffect(GateKind.Damage, 1);
        public static GateEffect MultiShot() => new GateEffect(GateKind.MultiShot, 1);
        public static GateEffect Recruit(int n, UnitLine line) => new GateEffect(GateKind.Recruit, n, line);
        public static GateEffect ClassUp() => new GateEffect(GateKind.ClassUp, 1);

        /// <summary>Gates that change the number of soldiers (+, -, x, /).</summary>
        public bool AffectsCount => kind <= GateKind.Divide;

        public bool IsWeapon => kind == GateKind.FireRate || kind == GateKind.Damage || kind == GateKind.MultiShot;

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
                    case GateKind.Add: return value >= 0 ? "+" + Fmt.Num(value) : "-" + Fmt.Num(-(long)value);
                    case GateKind.Subtract: return "-" + Fmt.Num(value);
                    case GateKind.Multiply: return "×" + value;
                    case GateKind.Divide: return "÷" + value;
                    case GateKind.FireRate: return "RATE+";
                    case GateKind.Damage: return "POWER+";
                    case GateKind.MultiShot: return "MULTI+";
                    case GateKind.Recruit: return "+" + Fmt.Num(value) + " " + Units.LineName(line);
                    case GateKind.ClassUp: return "CLASS UP";
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
                    case GateKind.ClassUp: return "CLASS UP!";
                    default: return Label;
                }
            }
        }

        /// <summary>Soldier count after a count gate (+, -, x, /). Other kinds leave the count alone.</summary>
        public int ApplyToCount(int count)
        {
            long result = count;
            switch (kind)
            {
                case GateKind.Add: result = (long)count + value; break;
                case GateKind.Subtract: result = (long)count - value; break;
                case GateKind.Multiply: result = (long)count * Math.Max(0, value); break;
                case GateKind.Divide: result = value <= 1 ? count : (count + value - 1L) / value; break;
                case GateKind.Recruit: result = (long)count + Math.Max(0, value); break;
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

        public override string ToString() => kind == GateKind.Recruit ? $"Recruit({value} {line})" : $"{kind}({value})";
    }
}
