using System;
using System.Collections.Generic;
using UnityEngine;

namespace NumberRoad.Core
{
    public enum SpawnKind
    {
        /// <summary>Two gates side by side; the squad passes through exactly one.</summary>
        GatePair,
        /// <summary>Two Add gates that start negative and count up with every bullet hit.</summary>
        ShootGatePair,
        /// <summary>A lane of small "+n" tiles collected one by one.</summary>
        Strip,
        /// <summary>A block of foot soldiers that waits, then charges.</summary>
        Army,
        /// <summary>A crate with HP; shooting it open grants its reward.</summary>
        Crate,
        /// <summary>One or two melee giants.</summary>
        Brute,
    }

    /// <summary>One thing placed along the road. Flat on purpose: easy to serialize, test and debug.</summary>
    [Serializable]
    public sealed class SpawnEvent
    {
        public SpawnKind kind;
        public float z;
        public float x;
        public float width;
        public GateEffect left;
        public GateEffect right;
        public int leftMax;
        public int rightMax;
        public GateEffect effect;
        public int count;
        public int rows;
        public int columns;
        public EnemyKind enemy;
        public float hp;
        public int smash;
        public bool laneLocked;

        public override string ToString()
        {
            switch (kind)
            {
                case SpawnKind.GatePair: return $"{z:0} Gates [{left.Label} | {right.Label}]";
                case SpawnKind.ShootGatePair: return $"{z:0} ShootGates [{left.Label}..{leftMax} | {right.Label}..{rightMax}]";
                case SpawnKind.Strip: return $"{z:0} Strip x{count} {effect.Label} @{x:0.0}";
                case SpawnKind.Army: return $"{z:0} Army {rows}x{columns} {enemy} hp={hp:0.0}{(laneLocked ? " lane" : "")} @{x:0.0}";
                case SpawnKind.Crate: return $"{z:0} Crate hp={hp:0} {effect.Label} @{x:0.0}";
                case SpawnKind.Brute: return $"{z:0} Brute x{count} hp={hp:0} smash={smash}";
                default: return kind.ToString();
            }
        }
    }

    /// <summary>A complete level: events along the road plus the boss fight at <see cref="length"/>.</summary>
    public sealed class LevelDefinition
    {
        public int level;
        public float length;
        public readonly List<SpawnEvent> events = new List<SpawnEvent>();
        public float bossHp;
        public int bossSmash;
        public int escortCount;
        public float escortHp;

        /// <summary>Squad size of the generator's reference player at the boss (diagnostics / tests).</summary>
        public float expectedCount;
        public float expectedDps;
    }

    /// <summary>
    /// Rough model of a "reference player" used by the generator: it assumes the
    /// better gate is chosen and estimates losses, so enemy HP keeps pace with the
    /// squad the player can realistically have at that point of the level.
    /// </summary>
    public sealed class PowerModel
    {
        public float Count;
        public WeaponStats Weapon;

        public PowerModel(float count, WeaponStats weapon)
        {
            Count = Mathf.Max(1f, count);
            Weapon = weapon;
        }

        public float Dps => Balance.SquadDps(Count, Weapon);

        public void Apply(GateEffect effect)
        {
            if (effect.AffectsCount) Count = Mathf.Max(1f, effect.ApplyToCount(Mathf.RoundToInt(Count)));
            else Weapon = effect.ApplyToWeapon(Weapon);
        }

        public float DpsAfter(GateEffect effect)
        {
            var copy = new PowerModel(Count, Weapon);
            copy.Apply(effect);
            return copy.Dps;
        }

        public GateEffect Better(GateEffect a, GateEffect b) => DpsAfter(a) >= DpsAfter(b) ? a : b;

        public void Lose(float soldiers) => Count = Mathf.Max(1f, Count - soldiers);
    }

    /// <summary>Sunflower (phyllotaxis) packing: dense, round, stable as soldiers join or fall.</summary>
    public static class Formation
    {
        const float GoldenAngle = 2.39996323f;
        const float DepthSquash = 0.85f;

        public static Vector2 Offset(int index, float spacing)
        {
            if (index <= 0) return Vector2.zero;
            float r = spacing * Mathf.Sqrt(index);
            float a = index * GoldenAngle;
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * DepthSquash);
        }

        /// <summary>Approximate half width of the packed formation.</summary>
        public static float Radius(int visibleCount, float spacing)
        {
            if (visibleCount <= 1) return spacing * 0.6f;
            return spacing * Mathf.Sqrt(visibleCount - 1) + spacing * 0.5f;
        }
    }
}
