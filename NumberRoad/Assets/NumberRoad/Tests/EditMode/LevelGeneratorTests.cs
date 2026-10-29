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
            Assert.AreEqual(a.events.Count, b.events.Count);
            for (int i = 0; i < a.events.Count; i++) Assert.AreEqual(a.events[i].ToString(), b.events[i].ToString());
            Assert.AreEqual(a.bossHp, b.bossHp);
            Assert.AreEqual(a.length, b.length);
        }

        [Test]
        public void DifferentLevels_ProduceDifferentRoads()
        {
            Assert.AreNotEqual(Describe(LevelGenerator.Generate(5)), Describe(LevelGenerator.Generate(6)));
        }

        [Test]
        public void FirstLevel_OpensWithAGateChoice()
        {
            LevelDefinition def = LevelGenerator.Generate(1);
            Assert.AreEqual(SpawnKind.GatePair, def.events[0].kind);
            Assert.That(def.events[0].z, Is.InRange(10f, 30f));
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

            float lastZ = float.MinValue;
            foreach (SpawnEvent ev in def.events)
            {
                Assert.GreaterOrEqual(ev.z, lastZ, "events must be sorted by z");
                lastZ = ev.z;
                Assert.That(ev.z, Is.InRange(0f, def.length), ev.ToString());
                Assert.LessOrEqual(Mathf.Abs(ev.x) + ev.width * 0.5f, Balance.RoadHalfWidth + 0.01f, ev.ToString());

                switch (ev.kind)
                {
                    case SpawnKind.GatePair:
                        Assert.IsTrue(ev.left.IsBeneficial || ev.right.IsBeneficial, "a gate pair needs at least one good option: " + ev);
                        break;
                    case SpawnKind.ShootGatePair:
                        Assert.Less(ev.left.value, 0, ev.ToString());
                        Assert.Less(ev.right.value, 0, ev.ToString());
                        Assert.Greater(ev.leftMax, 0, ev.ToString());
                        Assert.Greater(ev.rightMax, 0, ev.ToString());
                        break;
                    case SpawnKind.Army:
                        Assert.GreaterOrEqual(ev.rows, 1);
                        Assert.GreaterOrEqual(ev.columns, 1);
                        Assert.Greater(ev.hp, 0f);
                        break;
                    case SpawnKind.Strip:
                        Assert.Greater(ev.count, 0);
                        Assert.IsTrue(ev.effect.IsBeneficial);
                        break;
                    case SpawnKind.Crate:
                    case SpawnKind.Brute:
                        Assert.Greater(ev.hp, 0f);
                        break;
                }
            }
        }

        [Test]
        public void Boss_GetsTougherOverTime()
        {
            Assert.Less(LevelGenerator.Generate(1).bossHp, LevelGenerator.Generate(10).bossHp);
            Assert.Less(LevelGenerator.Generate(10).bossHp, LevelGenerator.Generate(30).bossHp);
        }

        [Test]
        public void ReferencePlayer_EndsWithAnArmy()
        {
            for (int level = 1; level <= 40; level++)
            {
                Assert.GreaterOrEqual(LevelGenerator.Generate(level).expectedCount, 1f);
            }
        }

        static string Describe(LevelDefinition def)
        {
            var sb = new System.Text.StringBuilder();
            foreach (SpawnEvent ev in def.events) sb.AppendLine(ev.ToString());
            return sb.ToString();
        }
    }
}
