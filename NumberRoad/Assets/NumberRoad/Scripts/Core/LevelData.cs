using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NumberRoad.Core
{
    public enum SpawnKind
    {
        /// <summary>A row of 2-4 gates across the road; the squad passes through exactly one.</summary>
        GateRow,
        /// <summary>A lane of small "+n" tiles collected one by one.</summary>
        Strip,
        /// <summary>A block (often a long column) of mixed enemies that waits, then charges.</summary>
        Horde,
        /// <summary>A crate with HP; shooting it open grants its reward.</summary>
        Crate,
    }

    /// <summary>One gate inside a row.</summary>
    public sealed class GateSpec
    {
        public GateEffect effect;
        /// <summary>Value goes up by <see cref="step"/> per bullet hit, up to <see cref="maxValue"/>.</summary>
        public bool shootable;
        public int maxValue;
        public int step = 1;
        /// <summary>When set, the gate cycles through these effects every <see cref="RouletteStep"/> seconds.</summary>
        public GateEffect[] roulette;
        /// <summary>Shown as "?" until passed.</summary>
        public bool mystery;
        /// <summary>Lock HP; while locked the gate does nothing.</summary>
        public float lockHp;
        /// <summary>Golden shimmering x5.</summary>
        public bool jackpot;

        public const float RouletteStep = 0.45f;

        public override string ToString()
        {
            if (roulette != null)
            {
                var sb = new StringBuilder("roulette(");
                for (int i = 0; i < roulette.Length; i++) sb.Append(i > 0 ? "," : "").Append(roulette[i].Label);
                return sb.Append(')').ToString();
            }
            string s = mystery ? "?(" + effect.Label + ")" : effect.Label;
            if (shootable) s += ".." + maxValue + (step > 1 ? " +" + step + "/hit" : string.Empty);
            if (lockHp > 0) s += " lock" + Mathf.RoundToInt(lockHp);
            if (jackpot) s += " JACKPOT";
            return s;
        }
    }

    public struct HordeUnit
    {
        public int type;
        public int count;

        public HordeUnit(int type, int count)
        {
            this.type = type;
            this.count = count;
        }
    }

    /// <summary>One thing placed along the road.</summary>
    public sealed class SpawnEvent
    {
        public SpawnKind kind;
        public float z;
        public float x;
        public float width;

        // GateRow
        public GateSpec[] gates;
        public float slide;

        // Horde
        public HordeUnit[] units;
        public int columns;
        public float spacing;
        public float hp;
        public int smash;
        /// <summary>Soldiers per enemy body slam (see <see cref="Balance.ContactWeight"/>).</summary>
        public int contact = 1;
        public int elites;
        public bool laneLocked;
        public bool warning;

        // Strip / Crate
        public GateEffect effect;
        public int count;

        public int HordeTotal
        {
            get
            {
                if (units == null) return 0;
                int sum = 0;
                foreach (HordeUnit u in units) sum += u.count;
                return sum;
            }
        }

        public int Rows => columns <= 0 ? 0 : Mathf.CeilToInt(HordeTotal / (float)columns);
        public float Depth => Rows * spacing;

        public override string ToString()
        {
            switch (kind)
            {
                case SpawnKind.GateRow:
                {
                    var sb = new StringBuilder($"{z:0} Gates [");
                    for (int i = 0; i < gates.Length; i++) sb.Append(i > 0 ? " | " : "").Append(gates[i]);
                    sb.Append(']');
                    if (slide > 0f) sb.Append($" slide {slide:0.0}");
                    return sb.ToString();
                }
                case SpawnKind.Strip: return $"{z:0} Strip x{count} {effect.Label} @{x:0.0}";
                case SpawnKind.Crate: return $"{z:0} Crate hp={hp:0} {effect.Label} @{x:0.0}";
                default:
                {
                    var sb = new StringBuilder($"{z:0} Horde {HordeTotal} (");
                    for (int i = 0; i < units.Length; i++) sb.Append(i > 0 ? ", " : "").Append(units[i].count).Append(' ').Append(Enemies.Get(units[i].type).Key);
                    sb.Append($") {columns} cols hp={hp:0.0}");
                    if (contact > 1) sb.Append($" x{contact}");
                    if (elites > 0) sb.Append($" elites={elites}");
                    if (laneLocked) sb.Append(" lane");
                    return sb.Append($" @{x:0.0}").ToString();
                }
            }
        }
    }

    /// <summary>A complete level: events along the road plus the boss fight at <see cref="length"/>.</summary>
    public sealed class LevelDefinition
    {
        public int level;
        public bool daily;
        public float length;
        public readonly List<SpawnEvent> events = new List<SpawnEvent>();
        public int biome;
        public bool goldRush;
        public bool castle;
        /// <summary>Enemy type id introduced at this level (shown with a NEW ENEMY banner), or -1.</summary>
        public int introduces = -1;

        public int bossType;
        public float bossHp;
        public int bossSmash;
        public HordeUnit[] escorts;
        public float escortHp;
        public int escortContact = 1;

        /// <summary>Squad size and damage of the generator's reference player at the boss (diagnostics / tests).</summary>
        public float expectedCount;
        public float expectedDps;
        public int hordeTotal;

        public int GateRows
        {
            get
            {
                int n = 0;
                foreach (SpawnEvent e in events)
                {
                    if (e.kind == SpawnKind.GateRow) n++;
                }
                return n;
            }
        }
    }

    /// <summary>
    /// The generator's "reference player": a real <see cref="Army"/> that takes the best choice at every gate,
    /// so enemy toughness keeps pace with the squad a player can realistically have at that point.
    /// </summary>
    public sealed class PowerModel
    {
        public readonly Army Army;
        public WeaponStats Weapon;

        public PowerModel(Army army, WeaponStats weapon)
        {
            Army = army;
            Weapon = weapon;
        }

        public float Count => Army.Total;
        public float Dps => Army.Dps(Weapon);

        public float BulletsPerSecond
        {
            get
            {
                int visible = Mathf.Min(Army.Total, Balance.MaxVisibleSoldiers);
                int[] perClass = Army.VisiblePerClass(visible);
                float bps = 0f;
                for (int i = 0; i < Units.Count; i++)
                {
                    if (perClass[i] == 0) continue;
                    UnitClass unit = Units.All[i];
                    bps += perClass[i] * unit.Rate * Weapon.fireRateMul * (unit.Shots + Weapon.multiShot - 1);
                }
                return Mathf.Clamp(bps, 1f, Balance.MaxBulletsPerSecond);
            }
        }

        /// <summary>Average damage of one bullet.</summary>
        public float HitDamage => Dps / BulletsPerSecond;

        public void Apply(GateEffect effect)
        {
            if (effect.IsWeapon) Weapon = effect.ApplyToWeapon(Weapon);
            else Army.Apply(effect, UnitLine.Bow);
            if (Army.Total < 1) Army.Add(Units.First(UnitLine.Bow).Id, 1);
        }

        /// <summary>How good the squad would be after the effect (damage, weighted towards crowd control).</summary>
        public float ScoreAfter(GateEffect effect)
        {
            var copy = new PowerModel(Army.Clone(), Weapon);
            copy.Apply(effect);
            return copy.Dps * Mathf.Sqrt(copy.Army.CrowdFactor);
        }

        public int BestIndex(IReadOnlyList<GateEffect> options)
        {
            int best = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < options.Count; i++)
            {
                float s = ScoreAfter(options[i]);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                }
            }
            return best;
        }

        public void Lose(float soldiers)
        {
            Army.Remove(Mathf.RoundToInt(soldiers));
            if (Army.Total < 1) Army.Add(Units.First(UnitLine.Bow).Id, 1);
        }
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
