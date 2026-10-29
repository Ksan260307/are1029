using System.Collections.Generic;
using UnityEngine;

namespace NumberRoad.Core
{
    /// <summary>
    /// Builds a level from its number. Deterministic (same number, same road) and
    /// self-balancing: enemy HP is derived from a <see cref="PowerModel"/> that
    /// follows the best choices through the level, scaled by <see cref="Balance.Difficulty"/>.
    /// </summary>
    public static class LevelGenerator
    {
        public const float FirstEventZ = 18f;
        public const float StripPitch = 1.7f;
        public const float LaneX = 2.2f;

        enum Section
        {
            Gates,
            ShootGates,
            StripLane,
            Army,
            Crate,
            Brute,
            GatesThenArmy,
        }

        /// <summary>Meta-upgrade levels the reference player is assumed to own at this level.</summary>
        public static float ReferenceUpgrades(int level) => (Mathf.Max(1, level) - 1) * 0.6f;

        public static LevelDefinition Generate(int level)
        {
            level = Mathf.Max(1, level);
            var rng = new Rng(level * 7919 + 17);
            var def = new LevelDefinition { level = level };
            float upgrades = ReferenceUpgrades(level);
            var power = new PowerModel(
                Balance.StartSquad(upgrades),
                WeaponStats.Create(Balance.RateMultiplier(upgrades), Balance.PowerMultiplier(upgrades)));
            float difficulty = Balance.Difficulty(level);

            // every plan opens with a gate pair that grows the squad, so the first fight is never hopeless
            List<Section> plan = Plan(level, rng);
            float z = EmitGates(FirstEventZ, def, power, rng, true);
            for (int i = 1; i < plan.Count; i++)
            {
                z = Emit(plan[i], z, def, power, difficulty, level, rng);
            }

            def.length = z + 4f;
            def.expectedCount = power.Count;
            def.expectedDps = power.Dps;
            float bossSeconds = 9f + Mathf.Min(level, 25) * 0.35f;
            def.bossHp = Mathf.Max(20f, Mathf.Round(power.Dps * bossSeconds * difficulty));
            def.bossSmash = Mathf.Max(2, Mathf.RoundToInt(power.Count * 0.12f));
            def.escortCount = Mathf.Min(4 + level, 20);
            var escortArch = Balance.Archetype(EnemyKind.Grunt);
            float engage = Balance.EnemyWakeDistance / (Balance.MarchSpeed + escortArch.Speed);
            def.escortHp = Mathf.Max(1f, power.Dps * engage * difficulty * 0.5f / def.escortCount);
            def.events.Sort((a, b) => a.z.CompareTo(b.z));
            return def;
        }

        static List<Section> Plan(int level, Rng rng)
        {
            if (level == 1)
            {
                return new List<Section>
                {
                    Section.Gates, Section.StripLane, Section.Army, Section.Crate,
                    Section.Gates, Section.ShootGates, Section.Army,
                };
            }

            if (level == 2)
            {
                return new List<Section>
                {
                    Section.Gates, Section.StripLane, Section.Army, Section.Crate,
                    Section.GatesThenArmy, Section.Brute, Section.ShootGates, Section.Army,
                };
            }

            int count = 8 + Mathf.Min((level - 3) / 2, 6);
            var plan = new List<Section> { Section.Gates, Section.StripLane };
            Section last = Section.StripLane;
            int guard = 0;
            while (plan.Count < count - 1 && guard++ < 100)
            {
                Section next = Roll(level, rng);
                if (next == last && next != Section.Army) continue;
                plan.Add(next);
                last = next;
            }
            plan.Add(Section.Army);
            return plan;
        }

        static Section Roll(int level, Rng rng)
        {
            float bruteWeight = level >= 3 ? 1.5f : 0f;
            float[] weights = { 3f, 2f, 2f, 2.5f, 1.8f, bruteWeight, 2f };
            float total = 0f;
            foreach (float w in weights) total += w;
            float roll = rng.Value * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0f) return (Section)i;
            }
            return Section.Gates;
        }

        static float Emit(Section section, float z, LevelDefinition def, PowerModel power, float difficulty, int level, Rng rng)
        {
            switch (section)
            {
                case Section.Gates: return EmitGates(z, def, power, rng, false);
                case Section.ShootGates: return EmitShootGates(z, def, power, rng);
                case Section.StripLane: return EmitStripLane(z, def, power, difficulty, level, rng);
                case Section.Army: return EmitArmy(z, def, power, difficulty, level, rng);
                case Section.Crate: return EmitCrate(z, def, power, difficulty, rng);
                case Section.Brute: return EmitBrute(z, def, power, difficulty, level, rng);
                case Section.GatesThenArmy:
                    z = EmitGates(z, def, power, rng, false);
                    return EmitArmy(z - 2f, def, power, difficulty, level, rng);
                default: return z;
            }
        }

        // --- Gates -------------------------------------------------------------

        static float EmitGates(float z, LevelDefinition def, PowerModel power, Rng rng, bool countOnly)
        {
            GateEffect good = RandomGood(power, rng, null, countOnly);
            // a tiny squad would be wiped out by a wrong pick, so early choices are "good vs good"
            bool allowBad = power.Count >= 4f && !countOnly;
            GateEffect other = allowBad && rng.Chance(0.55f) ? RandomBad(power, rng) : RandomGood(power, rng, good.kind, countOnly);
            bool goodLeft = rng.Chance(0.5f);
            def.events.Add(new SpawnEvent
            {
                kind = SpawnKind.GatePair,
                z = z,
                width = Balance.RoadHalfWidth * 2f,
                left = goodLeft ? good : other,
                right = goodLeft ? other : good,
            });
            power.Apply(power.Better(good, other));
            return z + 18f;
        }

        static GateEffect RandomGood(PowerModel power, Rng rng, GateKind? exclude, bool countOnly)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                // the first 68% of the roll are the soldier-count gates (+n / xn)
                float roll = countOnly ? rng.Value * 0.68f : rng.Value;
                GateEffect effect;
                if (roll < 0.42f)
                    effect = GateEffect.Add(Balance.Nice(Mathf.Max(3f, power.Count * rng.Range(0.35f, 0.8f) + rng.Range(2f, 6f))));
                else if (roll < 0.68f)
                    effect = GateEffect.Multiply(power.Count < 25f && rng.Chance(0.35f) ? 3 : 2);
                else if (roll < 0.79f)
                    effect = GateEffect.FireRate();
                else if (roll < 0.9f)
                    effect = GateEffect.Damage();
                else if (power.Weapon.multiShot < Balance.MaxMultiShot)
                    effect = GateEffect.MultiShot();
                else
                    continue;

                if (exclude.HasValue && effect.kind == exclude.Value) continue;
                return effect;
            }
            return GateEffect.Add(Balance.Nice(Mathf.Max(3f, power.Count * 0.5f)));
        }

        static GateEffect RandomBad(PowerModel power, Rng rng)
        {
            if (power.Count >= 6f && rng.Chance(0.35f))
                return GateEffect.Divide(power.Count > 60f && rng.Chance(0.4f) ? 3 : 2);
            return GateEffect.Subtract(Balance.Nice(Mathf.Max(2f, power.Count * rng.Range(0.3f, 0.6f))));
        }

        static float EmitShootGates(float z, LevelDefinition def, PowerModel power, Rng rng)
        {
            float scale = Mathf.Max(6f, power.Count * 0.6f);
            int startA = -Balance.Nice(scale * rng.Range(0.5f, 1.1f));
            int startB = -Balance.Nice(scale * rng.Range(0.8f, 1.5f));
            int maxA = Balance.Nice(Mathf.Max(5f, power.Count * rng.Range(0.4f, 0.8f)));
            int maxB = Balance.Nice(Mathf.Max(8f, power.Count * rng.Range(0.7f, 1.2f)));
            bool swap = rng.Chance(0.5f);
            def.events.Add(new SpawnEvent
            {
                kind = SpawnKind.ShootGatePair,
                z = z + 4f,
                width = Balance.RoadHalfWidth * 2f,
                left = GateEffect.Add(swap ? startB : startA),
                leftMax = swap ? maxB : maxA,
                right = GateEffect.Add(swap ? startA : startB),
                rightMax = swap ? maxA : maxB,
            });

            // the reference player lines up with one gate for ~2.5 s of fire
            float hits = Balance.BulletsPerSecond(Mathf.RoundToInt(power.Count), power.Weapon) * 2.5f;
            float gainA = Mathf.Min(maxA, startA + hits);
            float gainB = Mathf.Min(maxB, startB + hits);
            power.Count = Mathf.Max(1f, power.Count + Mathf.Max(gainA, gainB));
            return z + 20f;
        }

        // --- Strips, crates ---------------------------------------------------

        static float EmitStripLane(float z, LevelDefinition def, PowerModel power, float difficulty, int level, Rng rng)
        {
            int side = rng.Sign();
            int tiles = Mathf.Min(6 + level / 2, 14);
            int perTile = Mathf.Max(1, Mathf.RoundToInt(power.Count / 30f));
            def.events.Add(new SpawnEvent
            {
                kind = SpawnKind.Strip,
                z = z,
                x = side * LaneX,
                width = 2.6f,
                count = tiles,
                effect = GateEffect.Add(perTile),
            });

            // the other lane holds a squad that charges straight down its own lane
            def.events.Add(MakeArmy(z + 8f, -side * LaneX, 2.4f, power, difficulty, level, rng, true, 0.7f));
            power.Count += tiles * perTile;
            return z + tiles * StripPitch + 12f;
        }

        static float EmitCrate(float z, LevelDefinition def, PowerModel power, float difficulty, Rng rng)
        {
            GateEffect reward;
            float roll = rng.Value;
            if (roll < 0.3f && power.Weapon.multiShot < Balance.MaxMultiShot) reward = GateEffect.MultiShot();
            else if (roll < 0.55f) reward = GateEffect.FireRate();
            else if (roll < 0.8f) reward = GateEffect.Damage();
            else reward = GateEffect.Add(Balance.Nice(Mathf.Max(5f, power.Count * 0.5f)));

            def.events.Add(new SpawnEvent
            {
                kind = SpawnKind.Crate,
                z = z + 6f,
                x = rng.Range(-1, 2) * LaneX,
                effect = reward,
                hp = Mathf.Max(3f, Mathf.Round(power.Dps * 1.6f * difficulty)),
            });
            power.Apply(reward);
            return z + 18f;
        }

        // --- Enemies ------------------------------------------------------------

        static SpawnEvent MakeArmy(float z, float x, float width, PowerModel power, float difficulty, int level, Rng rng,
            bool laneLocked, float strength)
        {
            EnemyKind kind = level >= 4 && rng.Chance(0.3f) ? EnemyKind.Runner : EnemyKind.Grunt;
            int columns = Mathf.Clamp(Mathf.RoundToInt(width / 0.8f), 2, 8);
            int rows = Mathf.Clamp(2 + level / 3 + rng.Range(0, 2), 2, laneLocked ? 5 : 8);
            int units = rows * columns;
            var arch = Balance.Archetype(kind);
            float engage = Balance.EnemyWakeDistance / (Balance.MarchSpeed + arch.Speed);
            float totalHp = power.Dps * engage * difficulty * strength;
            return new SpawnEvent
            {
                kind = SpawnKind.Army,
                z = z,
                x = x,
                width = width,
                rows = rows,
                columns = columns,
                enemy = kind,
                hp = Mathf.Max(1f, totalHp / units),
                laneLocked = laneLocked,
            };
        }

        /// <summary>Soldiers the reference player is expected to lose to an army.</summary>
        public static float ExpectedLeak(PowerModel power, SpawnEvent army)
        {
            var arch = Balance.Archetype(army.enemy);
            float engage = Balance.EnemyWakeDistance / (Balance.MarchSpeed + arch.Speed);
            float killable = power.Dps * engage / Mathf.Max(0.01f, army.hp);
            return Mathf.Max(0f, army.rows * army.columns - killable);
        }

        static float EmitArmy(float z, LevelDefinition def, PowerModel power, float difficulty, int level, Rng rng)
        {
            // a compact block marching down the middle walks straight into the squad's fire
            float width = rng.Range(3.2f, 4.8f);
            float x = rng.Range(-1f, 1f);
            SpawnEvent army = MakeArmy(z + 6f, x, width, power, difficulty, level, rng, false, 0.85f);
            def.events.Add(army);
            power.Lose(ExpectedLeak(power, army));
            return z + 20f + army.rows * 0.8f;
        }

        static float EmitBrute(float z, LevelDefinition def, PowerModel power, float difficulty, int level, Rng rng)
        {
            int brutes = level >= 8 && rng.Chance(0.4f) ? 2 : 1;
            def.events.Add(new SpawnEvent
            {
                kind = SpawnKind.Brute,
                z = z + 10f,
                x = brutes == 1 ? rng.Range(-1.5f, 1.5f) : 0f,
                count = brutes,
                hp = Mathf.Max(10f, Mathf.Round(power.Dps * 3.2f * difficulty / brutes)),
                smash = Mathf.Max(2, Mathf.RoundToInt(power.Count * 0.08f)),
            });
            return z + 24f;
        }
    }
}
