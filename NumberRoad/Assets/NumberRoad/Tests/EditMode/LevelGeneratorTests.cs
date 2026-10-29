using NumberRoad.Core;
using NUnit.Framework;
using UnityEngine;

namespace NumberRoad.Tests
{
    public class LevelGeneratorTests
    {
        [Test]
        public void SameLevel_ProducesTheSameRoad()
        {
            LevelDefinition a = LevelGenerator.Generate(7);
            LevelDefinition b = LevelGenerator.Generate(7);
            Assert.AreEqual(Describe(a), Describe(b));
            Assert.AreEqual(a.bossHp, b.bossHp);
            Assert.AreEqual(a.length, b.length);
        }

        [Test]
        public void DifferentLevels_ProduceDifferentRoads()
        {
            Assert.AreNotEqual(Describe(LevelGenerator.Generate(5)), Describe(LevelGenerator.Generate(6)));
            Assert.AreNotEqual(Describe(LevelGenerator.Generate(5, true, 1)), Describe(LevelGenerator.Generate(5, true, 2)));
        }

        [Test]
        public void FirstLevel_OpensWithACountGateChoice()
        {
            LevelDefinition def = LevelGenerator.Generate(1);
            SpawnEvent first = def.events[0];
            Assert.AreEqual(SpawnKind.GateRow, first.kind);
            Assert.That(first.z, Is.InRange(10f, 30f));
            Assert.AreEqual(2, first.gates.Length);
            foreach (GateSpec g in first.gates) Assert.IsTrue(g.effect.AffectsCount && g.effect.IsBeneficial, g.ToString());
        }

        [Test]
        public void GeneratedLevels_AreWellFormed([NUnit.Framework.Range(1, 80, 3)] int level)
        {
            LevelDefinition def = LevelGenerator.Generate(level);
            Assert.Greater(def.events.Count, 5);
            Assert.Greater(def.length, 100f);
            Assert.Greater(def.bossHp, 0f);
            Assert.GreaterOrEqual(def.bossSmash, 1);
            Assert.Greater(def.escortHp, 0f);
            Assert.IsNotEmpty(def.escorts);

            float lastZ = float.MinValue;
            foreach (SpawnEvent ev in def.events)
            {
                Assert.GreaterOrEqual(ev.z, lastZ, "events must be sorted by z");
                lastZ = ev.z;
                Assert.That(ev.z, Is.InRange(0f, def.length), ev.ToString());
                Assert.LessOrEqual(Mathf.Abs(ev.x), Balance.RoadHalfWidth, ev.ToString());

                switch (ev.kind)
                {
                    case SpawnKind.GateRow:
                        Assert.That(ev.gates.Length, Is.InRange(2, 4), ev.ToString());
                        bool anyGood = false;
                        foreach (GateSpec g in ev.gates)
                        {
                            if (g.shootable)
                            {
                                Assert.Less(g.effect.value, 0, ev.ToString());
                                Assert.Greater(g.maxValue, 0, ev.ToString());
                                Assert.GreaterOrEqual(g.step, 1, ev.ToString());
                                // reachable in about 3 seconds of capped fire, however big the numbers get
                                Assert.LessOrEqual((g.maxValue - g.effect.value) / (float)g.step, 3f * Balance.MaxBulletsPerSecond, ev.ToString());
                                anyGood = true;
                            }
                            else if (g.roulette == null && !g.mystery && g.lockHp <= 0f && g.effect.IsBeneficial) anyGood = true;
                        }
                        Assert.IsTrue(anyGood, "a gate row needs at least one plainly good option: " + ev);
                        break;
                    case SpawnKind.Horde:
                        Assert.IsNotEmpty(ev.units, ev.ToString());
                        Assert.That(ev.HordeTotal, Is.InRange(1, Balance.MaxHordeWave), ev.ToString());
                        Assert.GreaterOrEqual(ev.columns, 1);
                        Assert.Greater(ev.spacing, 0f);
                        Assert.Greater(ev.hp, 0f);
                        break;
                    case SpawnKind.Strip:
                        Assert.Greater(ev.count, 0);
                        Assert.IsTrue(ev.effect.IsBeneficial);
                        break;
                    case SpawnKind.Crate:
                        Assert.Greater(ev.hp, 0f);
                        break;
                }
            }
        }

        [Test]
        public void Hordes_GrowExponentially()
        {
            Assert.Greater(LevelGenerator.Generate(12).hordeTotal, LevelGenerator.Generate(2).hordeTotal * 3);
            Assert.Greater(LevelGenerator.Generate(25).hordeTotal, LevelGenerator.Generate(12).hordeTotal * 3);
            Assert.Greater(LevelGenerator.Generate(8, true).hordeTotal, LevelGenerator.Generate(8).hordeTotal);
        }

        [Test]
        public void GatesGetMoreNumerous_AndWider()
        {
            Assert.Greater(LevelGenerator.Generate(30).GateRows, LevelGenerator.Generate(1).GateRows);
            Assert.Greater(AverageGatesPerRow(20, 30), AverageGatesPerRow(1, 3) + 0.8f);
        }

        [Test]
        public void EveryLevelFromTwo_HasARouletteGate([NUnit.Framework.Range(2, 40, 1)] int level)
        {
            bool roulette = false;
            foreach (SpawnEvent ev in LevelGenerator.Generate(level).events)
            {
                if (ev.kind != SpawnKind.GateRow) continue;
                foreach (GateSpec g in ev.gates)
                {
                    if (g.roulette == null) continue;
                    roulette = true;
                    bool good = false, bad = false;
                    foreach (GateEffect e in g.roulette)
                    {
                        good |= e.IsBeneficial;
                        bad |= !e.IsBeneficial;
                    }
                    Assert.IsTrue(good && bad, "a roulette mixes good and bad values: " + g);
                }
            }
            Assert.IsTrue(roulette, "level " + level);
        }

        [Test]
        public void NewEnemy_AppearsInItsLevel([NUnit.Framework.Range(2, 28, 1)] int level)
        {
            LevelDefinition def = LevelGenerator.Generate(level);
            if (def.introduces < 0) return;
            foreach (SpawnEvent ev in def.events)
            {
                if (ev.kind != SpawnKind.Horde) continue;
                foreach (HordeUnit u in ev.units)
                {
                    if (u.type == def.introduces) return;
                }
            }
            Assert.Fail("level " + level + " introduces " + Enemies.Get(def.introduces).Key + " but never spawns it");
        }

        [Test]
        public void SpecialLevels()
        {
            Assert.IsTrue(LevelGenerator.Generate(5).goldRush);
            Assert.IsTrue(LevelGenerator.Generate(10).castle);
            Assert.IsFalse(LevelGenerator.Generate(7).goldRush);
            Assert.AreNotEqual(LevelGenerator.Generate(1).biome, LevelGenerator.Generate(6).biome);
        }

        [Test]
        public void Boss_GetsTougherOverTime()
        {
            Assert.Less(LevelGenerator.Generate(1).bossHp, LevelGenerator.Generate(10).bossHp);
            Assert.Less(LevelGenerator.Generate(10).bossHp, LevelGenerator.Generate(30).bossHp);
            Assert.AreNotEqual(LevelGenerator.Generate(1).bossType, LevelGenerator.Generate(2).bossType);
        }

        [Test]
        public void ReferencePlayer_EndsWithAnArmy()
        {
            for (int level = 1; level <= 40; level++)
            {
                Assert.GreaterOrEqual(LevelGenerator.Generate(level).expectedCount, 1f);
            }
        }

        static float AverageGatesPerRow(int from, int to)
        {
            int rows = 0, gates = 0;
            for (int level = from; level <= to; level++)
            {
                foreach (SpawnEvent ev in LevelGenerator.Generate(level).events)
                {
                    if (ev.kind != SpawnKind.GateRow) continue;
                    rows++;
                    gates += ev.gates.Length;
                }
            }
            return gates / (float)Mathf.Max(1, rows);
        }

        static string Describe(LevelDefinition def)
        {
            var sb = new System.Text.StringBuilder();
            foreach (SpawnEvent ev in def.events) sb.AppendLine(ev.ToString());
            return sb.ToString();
        }
    }
}
