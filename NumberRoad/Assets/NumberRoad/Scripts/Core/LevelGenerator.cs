using System.Collections.Generic;
using UnityEngine;

namespace NumberRoad.Core
{
    /// <summary>
    /// Builds a level from its number. Deterministic (same number, same road) and self-balancing:
    /// a <see cref="PowerModel"/> follows the best choices through the level, horde HP is set so its
    /// bullets drop basic enemies in one hit, and horde sizes grow exponentially but are capped so the
    /// reference player loses at most a difficulty-dependent share of its squad to any one wave.
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
            Horde,
            Crate,
            Melee,
            GatesThenHorde,
            GoldGoblin,
        }

        sealed class Ctx
        {
            public LevelDefinition Def;
            public PowerModel Power;
            public Rng Rng;
            public int Level;
            public float Difficulty;
            public float HordeScale;
            public bool IntroPlaced;
            public int Roulettes;
            public int ClassUps;
            public List<EnemyType> Spawnable;
        }

        /// <summary>Meta-upgrade levels the reference player is assumed to own at this level.</summary>
        public static float ReferenceUpgrades(int level) => (Mathf.Max(1, level) - 1) * 0.9f;

        public static LevelDefinition Generate(int level, bool daily = false, int salt = 0)
        {
            level = Mathf.Max(1, level);
            var rng = new Rng(level * 7919 + 17 + salt * 104729);
            var def = new LevelDefinition
            {
                level = level,
                daily = daily,
                biome = Biomes.IndexForLevel(level),
                goldRush = level % 5 == 0,
                castle = level % 10 == 0,
                bossType = Enemies.BossForLevel(level).Id,
            };
            EnemyType intro = Enemies.IntroducedAt(level);
            def.introduces = intro != null ? intro.Id : -1;

            float upgrades = ReferenceUpgrades(level);
            var power = new PowerModel(
                new Army(UnitLine.Bow, Balance.StartSquad(upgrades)),
                WeaponStats.Create(Balance.RateMultiplier(upgrades), Balance.PowerMultiplier(upgrades), Balance.CritChance(Mathf.FloorToInt(upgrades))));
            var ctx = new Ctx
            {
                Def = def,
                Power = power,
                Rng = rng,
                Level = level,
                Difficulty = Balance.Difficulty(level),
                HordeScale = daily ? 2f : 1f,
                Spawnable = Enemies.Spawnable(level),
            };

            // every plan opens with a gate pair that grows the squad, so the first fight is never hopeless
            List<Section> plan = Plan(level, rng, def.goldRush);
            float z = EmitGates(ctx, FirstEventZ, true);
            for (int i = 1; i < plan.Count; i++) z = Emit(ctx, plan[i], z);

            def.length = z + 4f;
            def.expectedCount = power.Count;
            def.expectedDps = power.Dps;
            float bossSeconds = 6.5f + Mathf.Min(level, 25) * 0.25f;
            // armoured bosses show less HP so every boss takes about as long to bring down
            float armor = Enemies.Bosses[def.bossType].Armor;
            def.bossHp = Mathf.Max(20f, Mathf.Round(power.Dps * bossSeconds * ctx.Difficulty * armor * (def.castle ? 1.25f : 1f)));
            def.bossSmash = Mathf.Max(2, Mathf.RoundToInt(power.Count * 0.08f));
            def.escortContact = Balance.ContactWeight(power.Count);
            int escorts = Mathf.Clamp(Mathf.RoundToInt(Balance.HordeSize(level) * 0.5f * ctx.HordeScale), 6, Balance.MaxHordeWave / 2);
            def.escorts = MixUnits(ctx, escorts, null);
            def.escortHp = BaseHp(ctx);
            def.events.Sort((a, b) => a.z.CompareTo(b.z));
            foreach (SpawnEvent e in def.events) def.hordeTotal += e.HordeTotal;
            return def;
        }

        static List<Section> Plan(int level, Rng rng, bool goldRush)
        {
            List<Section> plan;
            if (level == 1)
            {
                plan = new List<Section>
                {
                    Section.Gates, Section.StripLane, Section.Horde, Section.Crate,
                    Section.Gates, Section.ShootGates, Section.Horde, Section.Gates, Section.Horde,
                };
            }
            else if (level == 2)
            {
                plan = new List<Section>
                {
                    Section.Gates, Section.StripLane, Section.Horde, Section.Crate,
                    Section.GatesThenHorde, Section.ShootGates, Section.Gates, Section.Horde,
                };
            }
            else
            {
                int count = 9 + Mathf.Min(level, 24) / 2;
                plan = new List<Section> { Section.Gates, Section.StripLane };
                Section last = Section.StripLane;
                int guard = 0;
                while (plan.Count < count - 1 && guard++ < 200)
                {
                    Section next = Roll(level, rng);
                    if (next == last && next != Section.Gates) continue;
                    plan.Add(next);
                    last = next;
                }
                plan.Add(Section.Horde);

                // "more gates every level": guarantee a minimum number of gate rows and of hordes
                int gatesWanted = Mathf.Min(3 + level / 3, 12);
                int hordesWanted = Mathf.Min(2 + level / 6, 5);
                guard = 0;
                while (CountGateRows(plan) < gatesWanted && guard++ < 50) plan.Insert(rng.Range(2, plan.Count), Section.Gates);
                guard = 0;
                while (CountHordes(plan) < hordesWanted && guard++ < 50) plan.Insert(rng.Range(2, plan.Count), Section.Horde);
            }

            int goblins = goldRush ? 3 : level >= 3 && rng.Chance(0.3f) ? 1 : 0;
            for (int i = 0; i < goblins; i++) plan.Insert(rng.Range(2, plan.Count), Section.GoldGoblin);
            return plan;
        }

        static int CountGateRows(List<Section> plan)
        {
            int n = 0;
            foreach (Section s in plan)
            {
                if (s == Section.Gates || s == Section.ShootGates || s == Section.GatesThenHorde) n++;
            }
            return n;
        }

        static int CountHordes(List<Section> plan)
        {
            int n = 0;
            foreach (Section s in plan)
            {
                if (s == Section.Horde || s == Section.GatesThenHorde) n++;
            }
            return n;
        }

        static Section Roll(int level, Rng rng)
        {
            float[] weights =
            {
                3.5f + Mathf.Min(level, 20) * 0.1f, // Gates
                1.5f, // ShootGates
                1.5f, // StripLane
                2.5f, // Horde
                1.5f, // Crate
                level >= 3 ? 1.2f : 0f, // Melee
                2f, // GatesThenHorde
                0f, // GoldGoblin (placed separately)
            };
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

        static float Emit(Ctx c, Section section, float z)
        {
            switch (section)
            {
                case Section.Gates: return EmitGates(c, z, false);
                case Section.ShootGates: return EmitShootGates(c, z);
                case Section.StripLane: return EmitStripLane(c, z);
                case Section.Horde: return EmitHorde(c, z, c.Rng.Range(-1f, 1f), c.Rng.Range(4.6f, 7.4f), false, 1f);
                case Section.Crate: return EmitCrate(c, z);
                case Section.Melee: return EmitMelee(c, z);
                case Section.GatesThenHorde:
                    z = EmitGates(c, z, false);
                    return EmitHorde(c, z - 2f, c.Rng.Range(-1f, 1f), c.Rng.Range(4.6f, 7.4f), false, 0.8f);
                case Section.GoldGoblin: return EmitGoldGoblin(c, z);
                default: return z;
            }
        }

        // --- Gates -------------------------------------------------------------

        public static int GatesPerRow(int level, Rng rng)
        {
            if (level < 4) return 2;
            float r = rng.Value;
            if (level < 10) return r < 0.45f ? 3 : 2;
            if (level < 16) return r < 0.35f ? 4 : r < 0.8f ? 3 : 2;
            return r < 0.5f ? 4 : 3;
        }

        static float EmitGates(Ctx c, float z, bool countOnly)
        {
            int n = countOnly ? 2 : GatesPerRow(c.Level, c.Rng);
            var options = new List<GateEffect> { RandomGood(c, countOnly, null) };
            bool allowBad = !countOnly && c.Power.Count >= 4f;
            int guard = 0;
            while (options.Count < n && guard++ < 20)
            {
                bool bad = allowBad && c.Rng.Chance(options.Count == 1 ? 0.5f : 0.35f);
                options.Add(bad ? RandomBad(c) : RandomGood(c, countOnly, options));
            }
            for (int i = options.Count - 1; i > 0; i--)
            {
                int j = c.Rng.Range(0, i + 1);
                (options[i], options[j]) = (options[j], options[i]);
            }

            var specs = new GateSpec[options.Count];
            for (int i = 0; i < specs.Length; i++) specs[i] = new GateSpec { effect = options[i] };
            int best = c.Power.BestIndex(options);
            GateEffect taken = options[best];

            if (!countOnly)
            {
                int other = PickOther(c.Rng, specs.Length, best);
                if (c.Level >= 2 && other >= 0 && (c.Roulettes == 0 || c.Rng.Chance(0.25f)))
                {
                    specs[other].roulette = RouletteCycle(c);
                    specs[other].effect = specs[other].roulette[0];
                    c.Roulettes++;
                }
                else if (c.Level >= 6 && other >= 0 && c.Rng.Chance(0.12f))
                {
                    specs[other].mystery = true;
                    specs[other].effect = c.Rng.Chance(0.7f)
                        ? (c.Rng.Chance(0.5f) ? GateEffect.Multiply(c.Power.Count < 200 ? 3 : 2) : GateEffect.Add(Balance.Nice(c.Power.Count * 1.5f + 10)))
                        : (c.Rng.Chance(0.5f) ? GateEffect.Divide(2) : GateEffect.Subtract(Balance.Nice(c.Power.Count * 0.5f + 2)));
                }

                if (c.Level >= 6 && c.Power.Count < 3000 && c.Rng.Chance(0.06f))
                {
                    int g = PickOther(c.Rng, specs.Length, -1);
                    if (specs[g].roulette == null)
                    {
                        specs[g].effect = GateEffect.Multiply(5);
                        specs[g].jackpot = true;
                        specs[g].mystery = false;
                        taken = specs[g].effect;
                        best = g;
                    }
                }
                else if (c.Level >= 9 && c.Rng.Chance(0.14f) && specs[best].roulette == null)
                {
                    // the best gate becomes an even better prize, but it is locked until shot open
                    specs[best].effect = c.Power.Count < 100 ? GateEffect.Multiply(3) : GateEffect.Add(Balance.Nice(c.Power.Count * 1.6f));
                    specs[best].lockHp = Mathf.Max(10f, Mathf.Round(c.Power.Dps * 1.2f * c.Difficulty));
                    specs[best].mystery = false;
                    taken = specs[best].effect;
                }
            }

            EnsurePlainGood(c, specs, countOnly);

            c.Def.events.Add(new SpawnEvent
            {
                kind = SpawnKind.GateRow,
                z = z,
                width = Balance.RoadHalfWidth * 2f,
                gates = specs,
                slide = !countOnly && c.Level >= 8 && c.Rng.Chance(0.25f) ? c.Rng.Range(1.2f, 2f) : 0f,
            });
            c.Power.Apply(taken);
            return z + 18f;
        }

        /// <summary>
        /// Every row keeps at least one gate that is plainly good (not locked, not a roulette, not a mystery):
        /// a bad gate becomes a good one, or the lock comes off when there is no bad gate to swap.
        /// </summary>
        static void EnsurePlainGood(Ctx c, GateSpec[] specs, bool countOnly)
        {
            foreach (GateSpec g in specs)
            {
                if (g.roulette == null && !g.mystery && g.lockHp <= 0f && g.effect.IsBeneficial) return;
            }
            foreach (GateSpec g in specs)
            {
                if (g.roulette == null && !g.mystery && g.lockHp <= 0f)
                {
                    g.effect = RandomGood(c, countOnly, null);
                    return;
                }
            }
            foreach (GateSpec g in specs)
            {
                if (g.lockHp <= 0f) continue;
                g.lockHp = 0f;
                return;
            }
        }

        static int PickOther(Rng rng, int n, int exclude)
        {
            if (n <= 1) return exclude < 0 ? 0 : -1;
            int i;
            do i = rng.Range(0, n); while (i == exclude);
            return i;
        }

        static GateEffect[] RouletteCycle(Ctx c)
        {
            float count = c.Power.Count;
            var cycle = new List<GateEffect>
            {
                GateEffect.Add(Balance.Nice(count * 0.6f + 5)),
                GateEffect.Multiply(2),
                count >= 6 ? GateEffect.Subtract(Balance.Nice(count * 0.4f + 3)) : GateEffect.Divide(2),
                GateEffect.Add(Balance.Nice(count * 1.2f + 8)),
                GateEffect.Divide(2),
                count < 60 ? GateEffect.Multiply(3) : GateEffect.Subtract(Balance.Nice(count * 0.6f)),
            };
            for (int i = cycle.Count - 1; i > 0; i--)
            {
                int j = c.Rng.Range(0, i + 1);
                (cycle[i], cycle[j]) = (cycle[j], cycle[i]);
            }
            return cycle.ToArray();
        }

        static bool HasKind(List<GateEffect> list, GateKind kind)
        {
            if (list == null) return false;
            foreach (GateEffect e in list)
            {
                if (e.kind == kind) return true;
            }
            return false;
        }

        static GateEffect RandomGood(Ctx c, bool countOnly, List<GateEffect> existing)
        {
            float count = c.Power.Count;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float roll = countOnly ? c.Rng.Value * 0.6f : c.Rng.Value;
                GateEffect effect;
                if (roll < 0.36f)
                    effect = GateEffect.Add(Balance.Nice(Mathf.Max(3f, count * c.Rng.Range(0.35f, 0.8f) + c.Rng.Range(2f, 6f))));
                else if (roll < 0.6f)
                    effect = GateEffect.Multiply(count < 25f && c.Rng.Chance(0.35f) ? 3 : 2);
                else if (roll < 0.7f)
                    effect = GateEffect.FireRate();
                else if (roll < 0.78f)
                    effect = GateEffect.Damage();
                else if (roll < 0.84f)
                {
                    if (c.Power.Weapon.multiShot >= Balance.MaxMultiShot) continue;
                    effect = GateEffect.MultiShot();
                }
                else if (roll < 0.95f)
                {
                    if (c.Level < 2) continue;
                    effect = GateEffect.Recruit(Balance.Nice(Mathf.Max(4f, count * c.Rng.Range(0.3f, 0.6f) + 3f)), PickUnlockedLine(c));
                }
                else
                {
                    if (c.Level < 4 || c.ClassUps >= 1 + c.Level / 8 || !CanClassUp(c.Power.Army)) continue;
                    effect = GateEffect.ClassUp();
                    c.ClassUps++;
                }

                if (existing != null && effect.kind != GateKind.Add && HasKind(existing, effect.kind)) continue;
                return effect;
            }
            return GateEffect.Add(Balance.Nice(Mathf.Max(3f, count * 0.5f)));
        }

        static bool CanClassUp(Army army)
        {
            for (int i = 0; i < Units.Count; i++)
            {
                if (army[i] > 0 && !Units.All[i].IsMaxTier) return true;
            }
            return false;
        }

        static GateEffect RandomBad(Ctx c)
        {
            float count = c.Power.Count;
            if (count >= 6f && c.Rng.Chance(0.35f)) return GateEffect.Divide(count > 60f && c.Rng.Chance(0.4f) ? 3 : 2);
            return GateEffect.Subtract(Balance.Nice(Mathf.Max(2f, count * c.Rng.Range(0.3f, 0.6f))));
        }

        static float EmitShootGates(Ctx c, float z)
        {
            int n = Mathf.Max(2, GatesPerRow(c.Level, c.Rng));
            float scale = Mathf.Max(6f, c.Power.Count * 0.6f);
            var specs = new GateSpec[n];
            // hits the chosen gate takes in ~2.5 s (part of the squad stands in front of its neighbours);
            // each hit adds a step sized so that this is enough to reach the top value
            float hits = c.Power.BulletsPerSecond * 2.5f * (n >= 3 ? 0.55f : 0.7f);
            float bestGain = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                int start = -Balance.Nice(scale * c.Rng.Range(0.5f, 1.5f));
                int max = Balance.Nice(Mathf.Max(5f, c.Power.Count * c.Rng.Range(0.4f, 1.2f)));
                int step = Mathf.Max(1, Mathf.CeilToInt((max - start) / Mathf.Max(1f, hits)));
                specs[i] = new GateSpec { effect = GateEffect.Add(start), shootable = true, maxValue = max, step = step };
                bestGain = Mathf.Max(bestGain, Mathf.Min(max, start + hits * step));
            }
            c.Def.events.Add(new SpawnEvent { kind = SpawnKind.GateRow, z = z + 4f, width = Balance.RoadHalfWidth * 2f, gates = specs });
            if (bestGain >= 0f) c.Power.Army.AddMixed(Mathf.RoundToInt(bestGain), UnitLine.Bow);
            else c.Power.Lose(-bestGain);
            return z + 20f;
        }

        // --- Strips, crates -----------------------------------------------------

        static float EmitStripLane(Ctx c, float z)
        {
            int side = c.Rng.Sign();
            int tiles = Mathf.Min(6 + c.Level / 2, 14);
            int perTile = Mathf.Max(1, Mathf.RoundToInt(c.Power.Count / 30f));
            c.Def.events.Add(new SpawnEvent
            {
                kind = SpawnKind.Strip,
                z = z,
                x = side * LaneX,
                width = 2.6f,
                count = tiles,
                effect = GateEffect.Add(perTile),
            });

            // the other lane holds a squad that charges straight down its own lane
            EmitHorde(c, z + 8f, -side * LaneX, 2.2f, true, 0.3f);
            c.Power.Army.AddMixed(tiles * perTile, UnitLine.Bow);
            return z + tiles * StripPitch + 12f;
        }

        static float EmitCrate(Ctx c, float z)
        {
            float x = c.Rng.Range(-1, 2) * LaneX;
            float hp = Mathf.Max(3f, Mathf.Round(c.Power.Dps * 1.6f * c.Difficulty));
            if (c.Level >= Enemies.Get(Enemies.Mimic).Unlock && c.Rng.Chance(0.3f))
            {
                c.Def.events.Add(new SpawnEvent
                {
                    kind = SpawnKind.Horde,
                    z = z + 6f,
                    x = x,
                    width = 1f,
                    units = new[] { new HordeUnit(Enemies.Mimic, 1) },
                    columns = 1,
                    spacing = 1f,
                    hp = hp / Enemies.Get(Enemies.Mimic).Hp,
                    smash = 1,
                });
                return z + 18f;
            }

            GateEffect reward;
            float roll = c.Rng.Value;
            if (roll < 0.15f && c.Level >= 4 && CanClassUp(c.Power.Army)) reward = GateEffect.ClassUp();
            else if (roll < 0.4f && c.Level >= 2) reward = GateEffect.Recruit(Balance.Nice(Mathf.Max(5f, c.Power.Count * 0.4f)), PickUnlockedLine(c));
            else if (roll < 0.55f && c.Power.Weapon.multiShot < Balance.MaxMultiShot) reward = GateEffect.MultiShot();
            else if (roll < 0.7f) reward = GateEffect.FireRate();
            else if (roll < 0.85f) reward = GateEffect.Damage();
            else reward = GateEffect.Add(Balance.Nice(Mathf.Max(5f, c.Power.Count * 0.5f)));

            c.Def.events.Add(new SpawnEvent { kind = SpawnKind.Crate, z = z + 6f, x = x, effect = reward, hp = hp });
            c.Power.Apply(reward);
            return z + 18f;
        }

        static UnitLine PickUnlockedLine(Ctx c)
        {
            var lines = new List<UnitLine>();
            for (int l = 0; l < Units.Lines; l++)
            {
                if (Units.IsUnlocked((UnitLine)l, c.Level)) lines.Add((UnitLine)l);
            }
            return c.Rng.Pick(lines);
        }

        // --- Enemies ------------------------------------------------------------

        /// <summary>Per-enemy base HP (multiplied by the type's HP): one reference bullet drops a basic enemy.</summary>
        static float BaseHp(Ctx c) => Mathf.Max(0.5f, c.Power.HitDamage * 0.85f * c.Difficulty);

        static HordeUnit[] MixUnits(Ctx c, int total, EnemyType forcedMain)
        {
            List<EnemyType> pool = c.Spawnable;
            EnemyType main = forcedMain ?? WeightedPick(c, pool, null);
            var picks = new List<EnemyType> { main };
            int extra = c.Level < 4 ? 0 : c.Level < 12 ? 1 : 2;
            for (int i = 0; i < extra && picks.Count < pool.Count; i++)
            {
                EnemyType t = WeightedPick(c, pool, picks);
                if (t != null) picks.Add(t);
            }

            var units = new List<HordeUnit>();
            int left = total;
            float mainShare = picks.Count == 1 ? 1f : c.Rng.Range(0.6f, 0.8f);
            for (int i = 0; i < picks.Count; i++)
            {
                EnemyType t = picks[i];
                int n;
                if (i == picks.Count - 1) n = left;
                else if (i == 0) n = Mathf.RoundToInt(total * mainShare);
                else n = Mathf.RoundToInt(total * (1f - mainShare) / (picks.Count - 1));
                if (t.IsBig) n = Mathf.Min(n, Mathf.Max(1, total / 12));
                if (t.Has(EnemyTrait.Healer) || t.Has(EnemyTrait.Summoner) || t.Has(EnemyTrait.Drummer)) n = Mathf.Min(n, Mathf.Max(1, total / 10));
                n = Mathf.Clamp(n, 0, left);
                if (n > 0) units.Add(new HordeUnit(t.Id, n));
                left -= n;
            }
            if (left > 0)
            {
                // fill up with the plainest unlocked foot soldier
                int filler = 0;
                foreach (EnemyType t in pool)
                {
                    if (!t.IsBig && t.Traits == EnemyTrait.None) filler = t.Id;
                }
                units.Add(new HordeUnit(filler, left));
            }
            return units.ToArray();
        }

        static EnemyType WeightedPick(Ctx c, List<EnemyType> pool, List<EnemyType> exclude)
        {
            float total = 0f;
            foreach (EnemyType t in pool)
            {
                if (exclude != null && exclude.Contains(t)) continue;
                total += Freshness(c, t);
            }
            if (total <= 0f) return null;
            float roll = c.Rng.Value * total;
            foreach (EnemyType t in pool)
            {
                if (exclude != null && exclude.Contains(t)) continue;
                roll -= Freshness(c, t);
                if (roll < 0f) return t;
            }
            return pool[pool.Count - 1];
        }

        /// <summary>Recently unlocked enemies show up more often so every level feels a bit new.</summary>
        static float Freshness(Ctx c, EnemyType t) => t.Weight * (1f + Mathf.Max(0f, t.Unlock - (c.Level - 6)) * 0.3f);

        static float EmitHorde(Ctx c, float z, float x, float width, bool laneLocked, float sizeScale)
        {
            int size = Mathf.Clamp(Mathf.RoundToInt(Balance.HordeSize(c.Level) * c.Rng.Range(0.85f, 1.15f) * c.HordeScale * sizeScale), 6, Balance.MaxHordeWave);
            EnemyType intro = null;
            if (!c.IntroPlaced && !laneLocked && c.Def.introduces >= 0)
            {
                intro = Enemies.Get(c.Def.introduces);
                c.IntroPlaced = true;
            }
            HordeUnit[] units = MixUnits(c, size, intro);
            float hp = BaseHp(c);

            bool anyBig = false;
            float hits = 0f, speed = 0f, contact = 0f;
            foreach (HordeUnit u in units)
            {
                EnemyType t = Enemies.Get(u.type);
                anyBig |= t.IsBig;
                hits += u.count * Mathf.Max(1f, Mathf.Ceil(hp * t.Hp / Mathf.Max(0.01f, c.Power.HitDamage)) * (t.Has(EnemyTrait.Armor) ? 1.6f : 1f));
                speed += u.count * t.Speed;
                contact += u.count * Mathf.Max(1, t.Contact);
            }
            hits /= size;
            speed /= size;
            contact /= size;
            float spacing = anyBig ? 0.95f : 0.72f;
            int columns = Mathf.Clamp(Mathf.RoundToInt(width / spacing), 2, 11);
            float depth = Mathf.Ceil(size / (float)columns) * spacing;

            int weight = Balance.ContactWeight(c.Power.Count);
            if (!laneLocked)
            {
                float engage = (Balance.EnemyWakeDistance + depth) / (Balance.MarchSpeed + speed);
                float kills = c.Power.BulletsPerSecond * engage * c.Power.Army.CrowdFactor / hits;
                float perEnemy = Mathf.Max(0.05f, contact * weight * (1f - c.Power.Army.GuardChance(0f)));
                float maxLoss = c.Power.Count * Mathf.Lerp(0.2f, 0.5f, (c.Difficulty - 0.6f) / 0.4f);
                float maxLeak = maxLoss / perEnemy;
                if (size - kills > maxLeak)
                {
                    float scale = Mathf.Max(6f, kills + maxLeak) / size;
                    for (int i = 0; i < units.Length; i++) units[i].count = Mathf.Max(1, Mathf.RoundToInt(units[i].count * scale));
                    size = 0;
                    foreach (HordeUnit u in units) size += u.count;
                    depth = Mathf.Ceil(size / (float)columns) * spacing;
                }
                float leak = Mathf.Max(0f, size - kills);
                c.Power.Lose(leak * perEnemy);
            }

            c.Def.events.Add(new SpawnEvent
            {
                kind = SpawnKind.Horde,
                z = z + 6f,
                x = x,
                width = width,
                units = units,
                columns = columns,
                spacing = spacing,
                hp = hp,
                smash = Mathf.Max(1, Mathf.RoundToInt(c.Power.Count * 0.04f)),
                contact = weight,
                elites = !laneLocked && c.Level >= 6 ? Mathf.Min(4, 1 + c.Level / 10) : 0,
                laneLocked = laneLocked,
                warning = !laneLocked && size >= 150,
            });
            return z + 20f + depth * 0.7f;
        }

        static float EmitMelee(Ctx c, float z)
        {
            var melee = new List<EnemyType>();
            foreach (EnemyType t in c.Spawnable)
            {
                if (t.Has(EnemyTrait.Melee)) melee.Add(t);
            }
            if (melee.Count == 0) return EmitHorde(c, z, 0f, 6f, false, 1f);

            int n = Mathf.Clamp(1 + c.Level / 8, 1, 6);
            var units = new List<HordeUnit>();
            float hpUnits = 0f;
            for (int i = 0; i < n; i++)
            {
                EnemyType t = i == 0 ? melee[melee.Count - 1] : c.Rng.Pick(melee);
                units.Add(new HordeUnit(t.Id, 1));
                hpUnits += t.Hp;
            }
            float total = c.Power.Dps * 2.8f * c.Difficulty;
            c.Def.events.Add(new SpawnEvent
            {
                kind = SpawnKind.Horde,
                z = z + 10f,
                x = 0f,
                width = 5f,
                units = units.ToArray(),
                columns = Mathf.Min(n, 3),
                spacing = 2.2f,
                hp = Mathf.Max(1f, total / hpUnits),
                smash = Mathf.Max(2, Mathf.RoundToInt(c.Power.Count * 0.06f)),
            });
            EmitHorde(c, z + 4f, 0f, 3.2f, false, 0.25f);
            return z + 26f;
        }

        static float EmitGoldGoblin(Ctx c, float z)
        {
            EnemyType gold = Enemies.Get(Enemies.GoldGoblin);
            c.Def.events.Add(new SpawnEvent
            {
                kind = SpawnKind.Horde,
                z = z + 8f,
                x = c.Rng.Range(-2.5f, 2.5f),
                width = 1f,
                units = new[] { new HordeUnit(gold.Id, 1) },
                columns = 1,
                spacing = 1f,
                hp = Mathf.Max(1f, c.Power.Dps * 1.2f / gold.Hp),
            });
            return z + 14f;
        }
    }
}
