using System;
using System.Collections.Generic;
using UnityEngine;

namespace NumberRoad.Core
{
    public enum BuffKind
    {
        Power,
        Enchant,
        Risky,
        Heal,
        Troops,
        Boost,
    }

    public enum BuffRarity
    {
        Common,
        Rare,
        Epic,
    }

    /// <summary>What a buff can do to the squad right away (implemented by the level session).</summary>
    public interface IBuffTarget
    {
        int Soldiers { get; }
        int Fallen { get; }
        bool LineUnlocked(UnitLine line);
        bool CanClassUp { get; }
        /// <summary>Adds soldiers of a line, or of the current mix when <paramref name="line"/> is null.</summary>
        void AddSoldiers(int n, UnitLine? line);
        void Revive(int n);
        void LoseShare(float share);
        bool ClassUp();
        /// <summary>The war wagon's health, 0..1.</summary>
        float BaseHealth { get; }
        void RepairBase(float share);
    }

    /// <summary>Everything the buffs picked so far change during this run.</summary>
    public sealed class RunMods
    {
        // squad
        public float Damage = 1f;
        public float Rate = 1f;
        public float CritChance;
        public float CritDamage;
        public float Range = 1f;
        public float ShotSpeed = 1f;
        public int ExtraShots;
        public int ExtraPierce;
        public float ExtraSplash;
        public float Guard;
        public float HealRate = 1f;
        /// <summary>Share of the army brought back per second while soldiers are down.</summary>
        public float Regen;
        public float Coins = 1f;
        public float ComboTime;
        /// <summary>Damage the war wagon takes (IRON PLATING lowers it).</summary>
        public float BaseArmor = 1f;
        /// <summary>Extra damage per 10 combo (capped at +60%).</summary>
        public float ComboDamage;

        // projectile enchantments
        public float Burn;
        public float Frost;
        public int ChainEvery;
        public int ChainJumps;
        public float Poison;
        public float Blast;
        public bool Ricochet;
        public bool Seeking;
        public float HolyBonus;
        public float Knockback;
        public int StealEvery;
        public int Shards;

        // enemies
        public float EnemySpeed = 1f;
        public float EnemyHp = 1f;

        // waves and gates
        public float WaveLoss;
        public float GateBonus = 1f;
        public int MultiplyBonus;
        /// <summary>Soldier losses that are still absorbed by a ward.</summary>
        public int Ward;
        public bool SecondWind;

        public readonly List<Buff> Taken = new List<Buff>();

        public int CountOf(Buff buff)
        {
            int n = 0;
            foreach (Buff b in Taken)
            {
                if (b == buff) n++;
            }
            return n;
        }

        /// <summary>Damage factor from the combo (BLOODLUST).</summary>
        public float ComboFactor(int combo) => 1f + Mathf.Min(0.6f, ComboDamage * (combo / 10));
    }

    public sealed class Buff
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        public readonly BuffKind Kind;
        public readonly BuffRarity Rarity;
        /// <summary>How often it can be picked in one run (0 = any number of times).</summary>
        public readonly int MaxStacks;
        readonly Action<RunMods, IBuffTarget> _apply;
        readonly Func<RunMods, IBuffTarget, bool> _usable;

        public Buff(string id, string name, string description, BuffKind kind, BuffRarity rarity, int maxStacks,
            Action<RunMods, IBuffTarget> apply, Func<RunMods, IBuffTarget, bool> usable = null)
        {
            Id = id;
            Name = name;
            Description = description;
            Kind = kind;
            Rarity = rarity;
            MaxStacks = maxStacks;
            _apply = apply;
            _usable = usable;
        }

        public bool Usable(RunMods mods, IBuffTarget target)
        {
            if (MaxStacks > 0 && mods.CountOf(this) >= MaxStacks) return false;
            return _usable == null || _usable(mods, target);
        }

        public void Apply(RunMods mods, IBuffTarget target)
        {
            mods.Taken.Add(this);
            _apply(mods, target);
        }

        public override string ToString() => Id;
    }

    /// <summary>The 53 buffs offered between waves (three random cards, pick one).</summary>
    public static class Buffs
    {
        static int Recruits(IBuffTarget t, float share) => Mathf.Max(5, Mathf.RoundToInt(t.Soldiers * share));

        static Buff B(string id, string name, string text, BuffKind kind, BuffRarity rarity, int max, Action<RunMods, IBuffTarget> apply,
            Func<RunMods, IBuffTarget, bool> usable = null) => new Buff(id, name, text, kind, rarity, max, apply, usable);

        static Buff Troops(string id, UnitLine line) =>
            B(id, Units.LineName(line) + " SQUAD", Units.LineName(line) + "S join you: +25% of your army (at least 5).", BuffKind.Troops,
                BuffRarity.Common, 0, (m, t) => t.AddSoldiers(Recruits(t, 0.25f), line), (m, t) => t.LineUnlocked(line));

        public static readonly IReadOnlyList<Buff> All = new[]
        {
            // --- power
            B("sharp", "SHARP TIPS", "Damage +20%.", BuffKind.Power, BuffRarity.Common, 0, (m, t) => m.Damage *= 1.2f),
            B("quick", "QUICK HANDS", "Fire rate +15%.", BuffKind.Power, BuffRarity.Common, 0, (m, t) => m.Rate *= 1.15f),
            B("eagle", "EAGLE EYE", "Critical chance +10%.", BuffKind.Power, BuffRarity.Common, 4, (m, t) => m.CritChance += 0.1f),
            B("deadly", "DEADLY AIM", "Critical hits deal +60% more.", BuffKind.Power, BuffRarity.Common, 3, (m, t) => m.CritDamage += 0.6f),
            B("long", "LONG SHOT", "Range +20% and shots fly 15% faster.", BuffKind.Power, BuffRarity.Common, 3, (m, t) =>
            {
                m.Range *= 1.2f;
                m.ShotSpeed *= 1.15f;
            }),
            B("extra", "EXTRA ARROW", "Every attack fires one more shot.", BuffKind.Power, BuffRarity.Rare, 3, (m, t) => m.ExtraShots++),
            B("heavy_hitter", "HEAVY HITTER", "Damage +35%.", BuffKind.Power, BuffRarity.Rare, 0, (m, t) => m.Damage *= 1.35f),
            B("rapid", "RAPID FIRE", "Fire rate +30%.", BuffKind.Power, BuffRarity.Rare, 0, (m, t) => m.Rate *= 1.3f),
            B("drill", "SHIELD DRILL", "Block chance +10%.", BuffKind.Power, BuffRarity.Common, 4, (m, t) => m.Guard += 0.1f),
            B("combo", "COMBO MASTER", "The combo lasts 1 second longer.", BuffKind.Power, BuffRarity.Common, 3, (m, t) => m.ComboTime += 1f),
            B("wide", "WIDE BLAST", "Area attacks are 0.6 m wider.", BuffKind.Power, BuffRarity.Rare, 3, (m, t) => m.ExtraSplash += 0.6f),
            B("training", "FIELD TRAINING", "Damage +10% and fire rate +10%.", BuffKind.Power, BuffRarity.Common, 0, (m, t) =>
            {
                m.Damage *= 1.1f;
                m.Rate *= 1.1f;
            }),

            // --- projectile enchantments
            B("flame", "FLAME ROUNDS", "Hits set enemies on fire: +40% damage over 2 seconds.", BuffKind.Enchant, BuffRarity.Common, 3, (m, t) => m.Burn += 0.4f),
            B("frost", "FROST ROUNDS", "Hits slow enemies by 35%.", BuffKind.Enchant, BuffRarity.Common, 1, (m, t) => m.Frost = 0.35f),
            B("thunder", "THUNDER ROUNDS", "Every 4th hit chains lightning to 2 more enemies.", BuffKind.Enchant, BuffRarity.Rare, 2, (m, t) =>
            {
                m.ChainEvery = 4;
                m.ChainJumps += 2;
            }),
            B("venom", "VENOM ROUNDS", "Hits poison: damage that stacks over time.", BuffKind.Enchant, BuffRarity.Common, 3, (m, t) => m.Poison += 0.25f),
            B("blast", "BLAST ROUNDS", "Hits explode for 40% damage around the target.", BuffKind.Enchant, BuffRarity.Rare, 3, (m, t) => m.Blast += 0.4f),
            B("pierce", "DRILL ROUNDS", "Shots go through one more enemy.", BuffKind.Enchant, BuffRarity.Common, 3, (m, t) => m.ExtraPierce++),
            B("bounce", "BOUNCING ROUNDS", "Shots bounce once to a nearby enemy.", BuffKind.Enchant, BuffRarity.Rare, 1, (m, t) => m.Ricochet = true),
            B("seeker", "SEEKER ROUNDS", "Straight shots curve towards enemies and never miss flyers.", BuffKind.Enchant, BuffRarity.Rare, 1, (m, t) => m.Seeking = true),
            B("holy_rounds", "HOLY ROUNDS", "Double damage to the undead. Hits ghosts even when they fade.", BuffKind.Enchant, BuffRarity.Common, 1, (m, t) => m.HolyBonus = 1f),
            B("impact", "IMPACT ROUNDS", "Hits knock enemies back.", BuffKind.Enchant, BuffRarity.Common, 2, (m, t) => m.Knockback += 0.6f),
            B("steal", "LIFE STEAL", "Every 25 kills, a soldier joins you.", BuffKind.Enchant, BuffRarity.Rare, 1, (m, t) => m.StealEvery = 25),
            B("shards", "SPLIT ROUNDS", "Kills burst into 2 shards that hit nearby enemies.", BuffKind.Enchant, BuffRarity.Rare, 2, (m, t) => m.Shards += 2),

            // --- with a drawback
            B("glass", "GLASS CANNON", "Damage +70%, but lose 15% of your soldiers now.", BuffKind.Risky, BuffRarity.Rare, 2, (m, t) =>
            {
                m.Damage *= 1.7f;
                t.LoseShare(0.15f);
            }),
            B("berserk", "BERSERK", "Fire rate +50%, but enemies move 20% faster.", BuffKind.Risky, BuffRarity.Rare, 2, (m, t) =>
            {
                m.Rate *= 1.5f;
                m.EnemySpeed *= 1.2f;
            }),
            B("heavy_ammo", "HEAVY AMMO", "Damage +80%, but fire rate -25%.", BuffKind.Risky, BuffRarity.Common, 2, (m, t) =>
            {
                m.Damage *= 1.8f;
                m.Rate *= 0.75f;
            }),
            B("greed", "GREED", "Coins +80%, but enemies have 25% more HP.", BuffKind.Risky, BuffRarity.Common, 2, (m, t) =>
            {
                m.Coins *= 1.8f;
                m.EnemyHp *= 1.25f;
            }),
            B("overdrive", "OVERDRIVE", "Fire rate +40%, but lose 5% of your soldiers every wave.", BuffKind.Risky, BuffRarity.Common, 2, (m, t) =>
            {
                m.Rate *= 1.4f;
                m.WaveLoss += 0.05f;
            }),
            B("gamble", "GAMBLE", "50%: your army doubles. 50%: you lose 30% of it.", BuffKind.Risky, BuffRarity.Rare, 0, (m, t) =>
            {
                if (UnityEngine.Random.value < 0.5f) t.AddSoldiers(t.Soldiers, null);
                else t.LoseShare(0.3f);
            }),
            B("bloodlust", "BLOODLUST", "+2% damage per 10 combo (up to +60%), but the combo ends 0.4 s sooner.", BuffKind.Risky, BuffRarity.Rare, 1, (m, t) =>
            {
                m.ComboDamage += 0.02f;
                m.ComboTime -= 0.4f;
            }),
            B("reckless", "RECKLESS", "Army x1.4, but block chance -20%.", BuffKind.Risky, BuffRarity.Rare, 2, (m, t) =>
            {
                t.AddSoldiers(Mathf.RoundToInt(t.Soldiers * 0.4f), null);
                m.Guard -= 0.2f;
            }),

            // --- healing
            B("medic", "FIELD MEDIC", "Half of your fallen soldiers get back up.", BuffKind.Heal, BuffRarity.Common, 0,
                (m, t) => t.Revive(Mathf.Max(1, t.Fallen / 2)), (m, t) => t.Fallen >= 4),
            B("regen", "REGENERATION", "Fallen soldiers return over time (2% of your army per second).", BuffKind.Heal, BuffRarity.Rare, 2, (m, t) => m.Regen += 0.02f),
            B("second", "SECOND WIND", "Once, when your army drops below 25%, half of the fallen return.", BuffKind.Heal, BuffRarity.Rare, 1,
                (m, t) => m.SecondWind = true),
            B("ward", "HOLY WARD", "The next losses up to 30% of your army are prevented.", BuffKind.Heal, BuffRarity.Common, 0,
                (m, t) => m.Ward += Mathf.Max(3, Mathf.RoundToInt(t.Soldiers * 0.3f))),
            B("repair", "FIELD REPAIRS", "Repairs 40% of the war wagon.", BuffKind.Heal, BuffRarity.Common, 0,
                (m, t) => t.RepairBase(0.4f), (m, t) => t.BaseHealth < 0.9f),
            B("plating", "IRON PLATING", "The war wagon takes 30% less damage.", BuffKind.Heal, BuffRarity.Rare, 2, (m, t) => m.BaseArmor *= 0.7f),

            // --- troops
            Troops("archers", UnitLine.Bow),
            Troops("knights", UnitLine.Guard),
            Troops("mages", UnitLine.Magic),
            Troops("gunners", UnitLine.Gun),
            Troops("clerics", UnitLine.Holy),
            B("conscription", "CONSCRIPTION", "Army +30%.", BuffKind.Troops, BuffRarity.Common, 0, (m, t) => t.AddSoldiers(Recruits(t, 0.3f), null)),
            B("promotion", "PROMOTION", "Every soldier is promoted one class.", BuffKind.Troops, BuffRarity.Rare, 0, (m, t) => t.ClassUp(), (m, t) => t.CanClassUp),
            B("mercs", "MERCENARIES", "Army +50%.", BuffKind.Troops, BuffRarity.Rare, 0, (m, t) => t.AddSoldiers(Recruits(t, 0.5f), null)),

            // --- big boosts
            B("double", "DOUBLE ARMY", "Your army doubles.", BuffKind.Boost, BuffRarity.Epic, 0, (m, t) => t.AddSoldiers(t.Soldiers, null)),
            B("golden", "GOLDEN RUN", "Coins x2 for the rest of this level.", BuffKind.Boost, BuffRarity.Rare, 1, (m, t) => m.Coins *= 2f),
            B("blessing", "GATE BLESSING", "Good number gates give 50% more.", BuffKind.Boost, BuffRarity.Rare, 2, (m, t) => m.GateBonus *= 1.5f),
            B("allin", "ALL IN", "Damage x1.5 and fire rate x1.2.", BuffKind.Boost, BuffRarity.Epic, 0, (m, t) =>
            {
                m.Damage *= 1.5f;
                m.Rate *= 1.2f;
            }),
            B("overflow", "OVERFLOW", "Multiply gates multiply by one more (x2 becomes x3).", BuffKind.Boost, BuffRarity.Epic, 1, (m, t) => m.MultiplyBonus++),
            B("slowtime", "SLOW TIME", "Enemies move 15% slower.", BuffKind.Boost, BuffRarity.Common, 2, (m, t) => m.EnemySpeed *= 0.85f),
            B("veteran", "VETERANS", "Damage +15% and block chance +5%.", BuffKind.Boost, BuffRarity.Common, 0, (m, t) =>
            {
                m.Damage *= 1.15f;
                m.Guard += 0.05f;
            }),
        };

        public static Buff Get(string id)
        {
            foreach (Buff b in All)
            {
                if (b.Id == id) return b;
            }
            return null;
        }

        static float Weight(BuffRarity rarity)
        {
            switch (rarity)
            {
                case BuffRarity.Epic: return 0.12f;
                case BuffRarity.Rare: return 0.33f;
                default: return 1f;
            }
        }

        /// <summary>Picks <paramref name="count"/> different usable buffs, rarer ones less often.</summary>
        public static Buff[] Roll(Rng rng, int count, RunMods mods, IBuffTarget target)
        {
            var pool = new List<Buff>();
            foreach (Buff b in All)
            {
                if (b.Usable(mods, target)) pool.Add(b);
            }
            var picks = new List<Buff>();
            while (picks.Count < count && pool.Count > 0)
            {
                float total = 0f;
                foreach (Buff b in pool) total += Weight(b.Rarity);
                float roll = rng.Value * total;
                int index = pool.Count - 1;
                for (int i = 0; i < pool.Count; i++)
                {
                    roll -= Weight(pool[i].Rarity);
                    if (roll < 0f)
                    {
                        index = i;
                        break;
                    }
                }
                picks.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return picks.ToArray();
        }

        /// <summary>The buffs taken so far, e.g. "SHARP TIPS x2, FLAME ROUNDS".</summary>
        public static string Summary(RunMods mods)
        {
            var parts = new List<string>();
            var seen = new HashSet<Buff>();
            foreach (Buff b in mods.Taken)
            {
                if (!seen.Add(b)) continue;
                int n = mods.CountOf(b);
                parts.Add(n > 1 ? Loc.T(b.Name) + " x" + n : Loc.T(b.Name));
            }
            return string.Join(Loc.T(", "), parts);
        }

        public static string KindName(BuffKind kind)
        {
            switch (kind)
            {
                case BuffKind.Enchant: return "ENCHANT";
                case BuffKind.Risky: return "RISKY";
                case BuffKind.Heal: return "HEAL";
                case BuffKind.Troops: return "TROOPS";
                case BuffKind.Boost: return "BOOST";
                default: return "POWER";
            }
        }

        public static Color KindColor(BuffKind kind)
        {
            switch (kind)
            {
                case BuffKind.Enchant: return new Color(0.62f, 0.4f, 1f);
                case BuffKind.Risky: return new Color(0.95f, 0.3f, 0.25f);
                case BuffKind.Heal: return new Color(0.3f, 0.8f, 0.45f);
                case BuffKind.Troops: return new Color(0.25f, 0.55f, 1f);
                case BuffKind.Boost: return new Color(1f, 0.72f, 0.15f);
                default: return new Color(1f, 0.55f, 0.2f);
            }
        }
    }
}
