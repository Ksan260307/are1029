using NumberRoad.Core;
using NUnit.Framework;
using UnityEngine;

namespace NumberRoad.Tests
{
    public class GateEffectTests
    {
        [TestCase(10, 5, 15)]
        [TestCase(0, 3, 3)]
        [TestCase(1, 0, 1)]
        public void Add_AddsSoldiers(int count, int value, int expected)
        {
            Assert.AreEqual(expected, GateEffect.Add(value).ApplyToCount(count));
        }

        [Test]
        public void Subtract_NeverGoesBelowZero()
        {
            Assert.AreEqual(0, GateEffect.Subtract(10).ApplyToCount(4));
            Assert.AreEqual(6, GateEffect.Subtract(4).ApplyToCount(10));
        }

        [Test]
        public void Multiply_IsClampedToMaxSquad()
        {
            Assert.AreEqual(24, GateEffect.Multiply(3).ApplyToCount(8));
            Assert.AreEqual(Balance.MaxSquad, GateEffect.Multiply(3).ApplyToCount(5000));
        }

        [Test]
        public void Divide_RoundsUp_SoTheLastSoldierSurvives()
        {
            Assert.AreEqual(1, GateEffect.Divide(2).ApplyToCount(1));
            Assert.AreEqual(4, GateEffect.Divide(2).ApplyToCount(7));
            Assert.AreEqual(4, GateEffect.Divide(3).ApplyToCount(10));
        }

        [Test]
        public void NegativeAdd_FromShootableGates_IsHarmful()
        {
            GateEffect effect = GateEffect.Add(-5);
            Assert.IsFalse(effect.IsBeneficial);
            Assert.AreEqual("-5", effect.Label);
            Assert.AreEqual(0, effect.ApplyToCount(3));
        }

        [Test]
        public void Labels_UseGameNotation()
        {
            Assert.AreEqual("+12", GateEffect.Add(12).Label);
            Assert.AreEqual("-3", GateEffect.Subtract(3).Label);
            Assert.AreEqual("×2", GateEffect.Multiply(2).Label);
            Assert.AreEqual("÷2", GateEffect.Divide(2).Label);
            Assert.AreEqual("MULTI+", GateEffect.MultiShot().Label);
        }

        [Test]
        public void WeaponEffects_LeaveCountAlone_AndStack()
        {
            WeaponStats weapon = WeaponStats.Create(1f, 1f);
            Assert.IsFalse(GateEffect.FireRate().AffectsCount);
            Assert.AreEqual(17, GateEffect.Damage().ApplyToCount(17));
            weapon = GateEffect.FireRate().ApplyToWeapon(weapon);
            weapon = GateEffect.Damage().ApplyToWeapon(weapon);
            weapon = GateEffect.MultiShot().ApplyToWeapon(weapon);
            Assert.AreEqual(1f + Balance.FireRateStep, weapon.fireRateMul, 1e-5f);
            Assert.AreEqual(1f + Balance.DamageStep, weapon.damageMul, 1e-5f);
            Assert.AreEqual(2, weapon.multiShot);
        }

        [Test]
        public void MultiShot_IsCapped()
        {
            WeaponStats weapon = WeaponStats.Create(1f, 1f);
            for (int i = 0; i < 10; i++) weapon = GateEffect.MultiShot().ApplyToWeapon(weapon);
            Assert.AreEqual(Balance.MaxMultiShot, weapon.multiShot);
        }
    }

    public class BalanceTests
    {
        [Test]
        public void UpgradeCost_StrictlyIncreases()
        {
            for (int i = 0; i < 40; i++)
            {
                Assert.Greater(Balance.UpgradeCost(i + 1), Balance.UpgradeCost(i), $"level {i}");
            }
        }

        [Test]
        public void Difficulty_IsMonotonicAndBounded()
        {
            float previous = 0f;
            for (int level = 1; level <= 200; level++)
            {
                float d = Balance.Difficulty(level);
                Assert.GreaterOrEqual(d, previous);
                Assert.That(d, Is.InRange(0.5f, 1f));
                previous = d;
            }
        }

        [TestCase(0.2f, 1)]
        [TestCase(7.4f, 7)]
        [TestCase(23.9f, 25)]
        [TestCase(-31f, -30)]
        [TestCase(437f, 440)]
        public void Nice_RoundsToFriendlyNumbers(float input, int expected)
        {
            Assert.AreEqual(expected, Balance.Nice(input));
        }

        [TestCase(1, 1f, 1f, 1)]
        [TestCase(25, 1.5f, 1.3f, 2)]
        [TestCase(40, 2f, 2f, 5)]
        [TestCase(900, 3f, 2.5f, 5)]
        public void FirePlan_DeliversExactlySquadDps(int count, float rate, float damage, int multi)
        {
            var weapon = new WeaponStats { fireRateMul = rate, damageMul = damage, multiShot = multi };
            int visible = Mathf.Min(count, Balance.MaxVisibleSoldiers);
            FirePlan plan = Balance.PlanFire(count, visible, weapon);
            float bulletsPerSecond = visible / plan.Interval * plan.Shots;
            Assert.LessOrEqual(bulletsPerSecond, Balance.MaxBulletsPerSecond + 0.01f);
            Assert.AreEqual(Balance.SquadDps(count, weapon), bulletsPerSecond * plan.Damage, 1e-3f * Balance.SquadDps(count, weapon));
        }
    }

    public class FormationTests
    {
        [Test]
        public void FirstSoldier_StandsInTheCentre()
        {
            Assert.AreEqual(Vector2.zero, Formation.Offset(0, 0.3f));
        }

        [Test]
        public void Slots_DoNotOverlap()
        {
            for (int i = 0; i < Balance.MaxVisibleSoldiers; i++)
            {
                for (int j = i + 1; j < Balance.MaxVisibleSoldiers; j++)
                {
                    float d = (Formation.Offset(i, Balance.FormationSpacing) - Formation.Offset(j, Balance.FormationSpacing)).magnitude;
                    Assert.Greater(d, Balance.FormationSpacing * 0.5f, $"{i} vs {j}");
                }
            }
        }

        [Test]
        public void Radius_GrowsWithCount_AndFitsTheRoad()
        {
            float previous = 0f;
            for (int n = 1; n <= Balance.MaxVisibleSoldiers; n++)
            {
                float r = Formation.Radius(n, Balance.FormationSpacing);
                Assert.GreaterOrEqual(r, previous);
                previous = r;
            }
            Assert.Less(previous * 2f, Balance.RoadHalfWidth * 2f * 0.75f);
        }
    }

    public class RngTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 200; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void Ranges_StayInBounds()
        {
            var rng = new Rng(7);
            for (int i = 0; i < 2000; i++)
            {
                Assert.That(rng.Value, Is.InRange(0f, 0.9999999f));
                Assert.That(rng.Range(-3, 4), Is.InRange(-3, 3));
                Assert.That(rng.Range(2f, 5f), Is.InRange(2f, 5f));
            }
        }
    }

    public class SaveDataTests
    {
        [Test]
        public void Json_RoundTrips()
        {
            var data = new SaveData { level = 12, bestLevel = 14, coins = 345, squadLevel = 3, powerLevel = 2, sound = false };
            SaveData copy = SaveSystem.FromJson(SaveSystem.ToJson(data));
            Assert.AreEqual(12, copy.level);
            Assert.AreEqual(14, copy.bestLevel);
            Assert.AreEqual(345, copy.coins);
            Assert.AreEqual(3, copy.squadLevel);
            Assert.AreEqual(2, copy.powerLevel);
            Assert.IsFalse(copy.sound);
            Assert.IsTrue(copy.music);
        }

        [Test]
        public void EmptyOrBrokenJson_GivesFreshSave()
        {
            Assert.AreEqual(1, SaveSystem.FromJson(string.Empty).level);
            Assert.AreEqual(1, SaveSystem.FromJson("{\"level\": -4, \"coins\": -10}").level);
            Assert.AreEqual(0, SaveSystem.FromJson("{\"level\": -4, \"coins\": -10}").coins);
        }

        [Test]
        public void TryBuy_SpendsCoins_AndLevelsUp()
        {
            var data = new SaveData { coins = Balance.UpgradeCost(0) };
            Assert.IsTrue(data.TryBuy(UpgradeKind.Squad));
            Assert.AreEqual(0, data.coins);
            Assert.AreEqual(1, data.squadLevel);
            Assert.AreEqual(2, data.StartSquad);
            Assert.IsFalse(data.TryBuy(UpgradeKind.Squad));
        }

        [Test]
        public void CompleteLevel_TracksBest()
        {
            var data = new SaveData();
            data.CompleteLevel();
            data.CompleteLevel();
            Assert.AreEqual(3, data.level);
            Assert.AreEqual(3, data.bestLevel);
        }
    }
}
