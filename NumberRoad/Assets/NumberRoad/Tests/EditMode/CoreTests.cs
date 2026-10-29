using System.Collections.Generic;
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
            Assert.AreEqual(Balance.MaxSquad, GateEffect.Multiply(3).ApplyToCount(500000));
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
            Assert.AreEqual("+12.3K", GateEffect.Add(12345).Label);
            Assert.AreEqual("-3", GateEffect.Subtract(3).Label);
            Assert.AreEqual("×2", GateEffect.Multiply(2).Label);
            Assert.AreEqual("÷2", GateEffect.Divide(2).Label);
            Assert.AreEqual("MULTI+", GateEffect.MultiShot().Label);
            Assert.AreEqual("+8 MAGE", GateEffect.Recruit(8, UnitLine.Magic).Label);
            Assert.AreEqual("CLASS UP", GateEffect.ClassUp().Label);
        }

        [Test]
        public void RecruitAndClassUp_AreGood_AndNotCountGates()
        {
            Assert.IsTrue(GateEffect.Recruit(5, UnitLine.Gun).IsBeneficial);
            Assert.IsTrue(GateEffect.ClassUp().IsBeneficial);
            Assert.IsFalse(GateEffect.ClassUp().AffectsCount);
            Assert.IsFalse(GateEffect.ClassUp().IsWeapon);
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

    public class ArmyTests
    {
        static Army Mixed()
        {
            var army = new Army();
            army.Add(Units.First(UnitLine.Bow).Id, 30);
            army.Add(Units.First(UnitLine.Magic).Id + 1, 12);
            army.Add(Units.First(UnitLine.Guard).Id + 2, 7);
            return army;
        }

        [TestCase(GateKind.Add, 25)]
        [TestCase(GateKind.Subtract, 18)]
        [TestCase(GateKind.Multiply, 3)]
        [TestCase(GateKind.Divide, 2)]
        [TestCase(GateKind.Divide, 3)]
        public void CountGates_MatchTheGateMaths(GateKind kind, int value)
        {
            Army army = Mixed();
            var effect = new GateEffect(kind, value);
            int expected = effect.ApplyToCount(army.Total);
            army.Apply(effect, UnitLine.Bow);
            Assert.AreEqual(expected, army.Total);
        }

        [Test]
        public void Remove_IsProportional_AndNeverNegative()
        {
            Army army = Mixed();
            army.Remove(24);
            Assert.AreEqual(25, army.Total);
            for (int i = 0; i < Units.Count; i++) Assert.GreaterOrEqual(army[i], 0);
            Assert.Greater(army[Units.First(UnitLine.Bow).Id], army[Units.First(UnitLine.Magic).Id + 1]);
            army.Remove(1000);
            Assert.AreEqual(0, army.Total);
        }

        [Test]
        public void Peak_RemembersTheLargestArmy()
        {
            Army army = Mixed();
            army.Apply(GateEffect.Multiply(2), UnitLine.Bow);
            army.Remove(90);
            Assert.AreEqual(98, army.Peak);
            Assert.AreEqual(8, army.Total);
        }

        [Test]
        public void Recruit_AddsTierOneOfTheLine()
        {
            var army = new Army(UnitLine.Bow, 5);
            army.Apply(GateEffect.Recruit(8, UnitLine.Gun), UnitLine.Bow);
            Assert.AreEqual(8, army[Units.First(UnitLine.Gun).Id]);
            Assert.AreEqual(13, army.Total);
        }

        [Test]
        public void ClassUp_PromotesEveryone_UntilTheTopTier()
        {
            var army = new Army(UnitLine.Bow, 10);
            for (int i = 0; i < Units.Tiers - 1; i++) Assert.IsTrue(army.ClassUp());
            Assert.AreEqual(10, army[Units.First(UnitLine.Bow).Id + Units.Tiers - 1]);
            Assert.IsFalse(army.ClassUp(), "everyone is already at the top tier");
            Assert.AreEqual(3, army.ClassUps);
            Assert.AreEqual(10, army.Total);
        }

        [Test]
        public void ClassUp_RaisesDamage()
        {
            WeaponStats weapon = WeaponStats.Create(1f, 1f);
            foreach (UnitLine line in new[] { UnitLine.Bow, UnitLine.Guard, UnitLine.Magic, UnitLine.Gun, UnitLine.Holy })
            {
                var army = new Army(line, 20);
                float before = army.Dps(weapon) * army.CrowdFactor;
                army.ClassUp();
                Assert.Greater(army.Dps(weapon) * army.CrowdFactor, before, line.ToString());
            }
        }

        [Test]
        public void VisiblePerClass_ShowsEveryClass_AndSumsUp()
        {
            Army army = Mixed();
            army.Add(Units.First(UnitLine.Holy).Id, 1);
            int[] visible = army.VisiblePerClass(20);
            int sum = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                sum += visible[i];
                if (army[i] > 0) Assert.GreaterOrEqual(visible[i], 1, Units.All[i].Name);
                else Assert.AreEqual(0, visible[i]);
                Assert.LessOrEqual(visible[i], army[i]);
            }
            Assert.AreEqual(20, sum);
        }

        [Test]
        public void GuardAndHeal_ComeFromTheirLines()
        {
            var archers = new Army(UnitLine.Bow, 10);
            var knights = new Army(UnitLine.Guard, 10);
            Assert.AreEqual(0f, archers.GuardChance(0f));
            Assert.Greater(knights.GuardChance(0f), 0.1f);
            Assert.LessOrEqual(knights.GuardChance(5f), 0.85f);
            Assert.AreEqual(0f, archers.HealPerSecond);
            Assert.Greater(new Army(UnitLine.Holy, 10).HealPerSecond, 0f);
        }

        [Test]
        public void MagicLine_IsBetterAgainstCrowds()
        {
            Assert.Greater(new Army(UnitLine.Magic, 10).CrowdFactor, new Army(UnitLine.Bow, 10).CrowdFactor);
        }
    }

    public class FireModelTests
    {
        static IEnumerable<TestCaseData> Armies()
        {
            yield return new TestCaseData(new[] { 0 }, new[] { 1 }, 1f, 1f, 1).SetName("OneArcher");
            yield return new TestCaseData(new[] { 0, 9 }, new[] { 25, 4 }, 1.5f, 1.3f, 2).SetName("ArchersAndMages");
            yield return new TestCaseData(new[] { 14, 3, 18 }, new[] { 300, 2, 40 }, 2f, 2f, 4).SetName("GatlingsSnipersBishops");
            yield return new TestCaseData(new[] { 12, 13, 14, 15 }, new[] { 50000, 30000, 10000, 1 }, 3f, 2.5f, 5).SetName("HugeGunLine");
        }

        [TestCaseSource(nameof(Armies))]
        public void DrawnBullets_DeliverExactlyTheArmyDps(int[] classes, int[] counts, float rate, float damage, int multi)
        {
            var army = new Army();
            for (int i = 0; i < classes.Length; i++) army.Add(classes[i], counts[i]);
            var weapon = new WeaponStats { fireRateMul = rate, damageMul = damage, multiShot = multi, critChance = 0.1f };
            int[] visible = army.VisiblePerClass(Mathf.Min(army.Total, Balance.MaxVisibleSoldiers));
            float throttle = FireModel.Throttle(visible, weapon);

            double bulletsPerSecond = 0, dps = 0;
            for (int c = 0; c < Units.Count; c++)
            {
                if (visible[c] == 0) continue;
                ClassFire plan = FireModel.Plan(Units.All[c], army[c], visible[c], weapon, throttle);
                double bps = visible[c] / plan.Interval * plan.Shots;
                bulletsPerSecond += bps;
                dps += bps * plan.Damage;
            }
            Assert.LessOrEqual(bulletsPerSecond, Balance.MaxBulletsPerSecond + 0.01);
            float expected = army.Dps(weapon) / weapon.ExpectedCrit;
            Assert.AreEqual(expected, dps, expected * 1e-3);
        }
    }

    public class UnitsTests
    {
        [Test]
        public void TwentyClasses_FiveLinesOfFour()
        {
            Assert.AreEqual(20, Units.All.Length);
            var models = new HashSet<string>();
            for (int i = 0; i < Units.All.Length; i++)
            {
                UnitClass u = Units.All[i];
                Assert.AreEqual(i, u.Id);
                Assert.AreEqual(i / Units.Tiers, (int)u.Line);
                Assert.AreEqual(i % Units.Tiers + 1, u.Tier);
                Assert.IsTrue(models.Add(u.Model), u.Model);
                Assert.AreEqual(u.Tier == Units.Tiers, u.IsMaxTier);
                if (!u.IsMaxTier) Assert.AreEqual(u.Line, u.Next.Line);
            }
        }

        [Test]
        public void Lines_UnlockOverTheFirstLevels()
        {
            Assert.IsTrue(Units.IsUnlocked(UnitLine.Bow, 1));
            Assert.IsFalse(Units.IsUnlocked(UnitLine.Holy, 1));
            Assert.AreEqual(UnitLine.Guard, Units.NextUnlock(1));
            Assert.IsNull(Units.NextUnlock(50));
        }
    }

    public class EnemiesTests
    {
        [Test]
        public void ThirtyOneTypes_SixBosses_UniqueKeys()
        {
            Assert.AreEqual(31, Enemies.All.Length);
            Assert.AreEqual(6, Enemies.Bosses.Length);
            var keys = new HashSet<string>();
            for (int i = 0; i < Enemies.All.Length; i++)
            {
                Assert.AreEqual(i, Enemies.All[i].Id);
                Assert.IsTrue(keys.Add(Enemies.All[i].Key), Enemies.All[i].Key);
                Assert.Greater(Enemies.All[i].Hp, 0f);
            }
            Assert.IsTrue(Enemies.Get(Enemies.GoldGoblin).Has(EnemyTrait.Runaway));
            Assert.IsTrue(Enemies.Get(Enemies.Mimic).Has(EnemyTrait.Ambush));
        }

        [Test]
        public void NewEnemies_KeepArriving_AndBossesRotate()
        {
            int previous = 0;
            int introductions = 0;
            for (int level = 1; level <= 30; level++)
            {
                int n = Enemies.Spawnable(level).Count;
                Assert.GreaterOrEqual(n, previous);
                previous = n;
                if (Enemies.IntroducedAt(level) != null) introductions++;
            }
            Assert.GreaterOrEqual(introductions, 25);
            Assert.AreNotEqual(Enemies.BossForLevel(1).Key, Enemies.BossForLevel(2).Key);
            Assert.AreEqual(Enemies.BossForLevel(1).Key, Enemies.BossForLevel(1 + Enemies.Bosses.Length).Key);
        }
    }

    public class FmtTests
    {
        [TestCase(0, "0")]
        [TestCase(9999, "9999")]
        [TestCase(12345, "12.3K")]
        [TestCase(99999, "99.9K")]
        [TestCase(100000, "100K")]
        [TestCase(1234567, "1.2M")]
        [TestCase(-42, "-42")]
        public void Num_ShortensBigNumbers(long value, string expected)
        {
            Assert.AreEqual(expected, Fmt.Num(value));
        }

        [Test]
        public void Coins_KeepSeparators_AndDurations()
        {
            Assert.AreEqual("12,345", Fmt.Coins(12345));
            Assert.AreEqual("+7", Fmt.Signed(7));
            Assert.AreEqual("-7", Fmt.Signed(-7));
            Assert.AreEqual("2m 05s", Fmt.Duration(125));
            Assert.AreEqual("1h 01m", Fmt.Duration(3660));
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

        [Test]
        public void HordeSize_GrowsExponentially_UpToTheCap()
        {
            Assert.AreEqual(18, Balance.HordeSize(1));
            Assert.Greater(Balance.HordeSize(11), Balance.HordeSize(1) * 4);
            Assert.Greater(Balance.HordeSize(21), Balance.HordeSize(11) * 4);
            Assert.AreEqual(Balance.MaxHordeWave, Balance.HordeSize(200));
        }

        [Test]
        public void Combo_AndStars()
        {
            Assert.AreEqual(1f, Balance.ComboMultiplier(0));
            Assert.AreEqual(3f, Balance.ComboMultiplier(5000));
            Assert.AreEqual(0, Balance.Stars(0, 10));
            Assert.AreEqual(1, Balance.Stars(1, 10));
            Assert.AreEqual(2, Balance.Stars(4, 10));
            Assert.AreEqual(3, Balance.Stars(9, 10));
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
            float spacing = Balance.FormationSpacing(Balance.MaxVisibleSoldiers);
            for (int i = 0; i < Balance.MaxVisibleSoldiers; i++)
            {
                for (int j = i + 1; j < Balance.MaxVisibleSoldiers; j++)
                {
                    float d = (Formation.Offset(i, spacing) - Formation.Offset(j, spacing)).magnitude;
                    Assert.Greater(d, spacing * 0.5f, $"{i} vs {j}");
                }
            }
        }

        [Test]
        public void Radius_GrowsWithCount_AndFitsTheRoad()
        {
            float previous = 0f;
            for (int n = 1; n <= Balance.MaxVisibleSoldiers; n++)
            {
                float r = Formation.Radius(n, Balance.FormationSpacing(n));
                Assert.GreaterOrEqual(r, previous - 0.02f);
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
            var data = new SaveData { level = 12, bestLevel = 14, coins = 345, gems = 9, squadLevel = 3, powerLevel = 2, critLevel = 4, sound = false };
            data.RecordStars(3, 2);
            data.AddKills(5, 17);
            data.SeeEnemy(5);
            SaveData copy = SaveSystem.FromJson(SaveSystem.ToJson(data));
            Assert.AreEqual(12, copy.level);
            Assert.AreEqual(14, copy.bestLevel);
            Assert.AreEqual(345, copy.coins);
            Assert.AreEqual(9, copy.gems);
            Assert.AreEqual(3, copy.squadLevel);
            Assert.AreEqual(4, copy.critLevel);
            Assert.AreEqual(2, copy.StarsFor(3));
            Assert.AreEqual(17, copy.KillsOf(5));
            Assert.IsTrue(copy.HasSeenEnemy(5));
            Assert.IsFalse(copy.sound);
            Assert.IsTrue(copy.music);
        }

        [Test]
        public void EmptyOrBrokenJson_GivesFreshSave()
        {
            Assert.AreEqual(1, SaveSystem.FromJson(string.Empty).level);
            Assert.AreEqual(1, SaveSystem.FromJson("{\"level\": -4, \"coins\": -10}").level);
            Assert.AreEqual(0, SaveSystem.FromJson("{\"level\": -4, \"coins\": -10}").coins);
            Assert.AreEqual(1, SaveSystem.FromJson("not json at all").level);
        }

        [Test]
        public void TryBuy_SpendsCoins_AndLevelsUp()
        {
            var data = new SaveData { coins = Balance.UpgradeCost(0) };
            Assert.IsTrue(data.TryBuy(UpgradeKind.Squad));
            Assert.AreEqual(0, data.coins);
            Assert.AreEqual(1, data.squadLevel);
            Assert.AreEqual(4, data.StartSquad);
            Assert.IsFalse(data.TryBuy(UpgradeKind.Squad));
        }

        [Test]
        public void EveryUpgradeKind_HasItsOwnLevel()
        {
            var data = new SaveData();
            foreach (UpgradeKind k in System.Enum.GetValues(typeof(UpgradeKind))) data.SetUpgrade(k, (int)k + 1);
            foreach (UpgradeKind k in System.Enum.GetValues(typeof(UpgradeKind))) Assert.AreEqual((int)k + 1, data.GetUpgrade(k), k.ToString());
        }

        [Test]
        public void StartLine_FallsBackWhileLocked()
        {
            var data = new SaveData { startLine = (int)UnitLine.Holy, level = 2 };
            Assert.AreEqual(UnitLine.Bow, data.StartLine);
            data.level = 9;
            Assert.AreEqual(UnitLine.Holy, data.StartLine);
        }

        [Test]
        public void Hints_AndDiscoveries_HappenOnce()
        {
            var data = new SaveData();
            Assert.IsTrue(data.TakeHint(Hint.Roulette));
            Assert.IsFalse(data.TakeHint(Hint.Roulette));
            Assert.IsTrue(data.SeeEnemy(3));
            Assert.IsFalse(data.SeeEnemy(3));
            Assert.AreEqual(1, data.EnemiesDiscovered);
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

    public class AchievementTests
    {
        [Test]
        public void ThirtyAchievements_WithUniqueIds()
        {
            Assert.AreEqual(30, Achievements.All.Count);
            var ids = new HashSet<string>();
            foreach (Achievement a in Achievements.All) Assert.IsTrue(ids.Add(a.Id), a.Id);
        }

        [Test]
        public void Claim_PaysOnce()
        {
            var save = new SaveData { statKills = 600 };
            Achievement first = Achievements.All[0];
            Assert.IsTrue(Achievements.IsReady(save, first));
            Assert.AreEqual(1, Achievements.ReadyCount(save));
            Assert.IsTrue(Achievements.Claim(save, first));
            Assert.AreEqual(first.Gems, save.gems);
            Assert.AreEqual(first.Coins, save.coins);
            Assert.IsFalse(Achievements.Claim(save, first));
            Assert.AreEqual(0, Achievements.ReadyCount(save));
        }
    }

    public class DailyTests
    {
        [Test]
        public void LoginStreak_GrowsDayByDay_AndResetsAfterAGap()
        {
            var save = new SaveData();
            Daily.RegisterLogin(save, 100);
            Daily.RegisterLogin(save, 100);
            Assert.AreEqual(1, save.loginStreak);
            Daily.RegisterLogin(save, 101);
            Daily.RegisterLogin(save, 102);
            Assert.AreEqual(3, save.loginStreak);
            Assert.AreEqual(3, Daily.StreakDay(save));
            Daily.RegisterLogin(save, 110);
            Assert.AreEqual(1, save.loginStreak);
        }

        [Test]
        public void StreakDay_CyclesThroughTheWeek()
        {
            var save = new SaveData { loginStreak = 8 };
            Assert.AreEqual(1, Daily.StreakDay(save));
            save.loginStreak = 7;
            Assert.AreEqual(7, Daily.StreakDay(save));
            Assert.AreEqual(PrizeKind.Gems, Daily.LoginPrize(7, 1).Kind);
        }

        [Test]
        public void OfflineCoins_NeedTenMinutes_AndCapAtEightHours()
        {
            var save = new SaveData { lastSeenUnix = 1000000 };
            Assert.AreEqual(0, Daily.OfflineCoins(save, 1000000 + 300));
            int eight = Daily.OfflineCoins(save, 1000000 + 8 * 3600);
            Assert.Greater(eight, 0);
            Assert.AreEqual(eight, Daily.OfflineCoins(save, 1000000 + 48 * 3600));
            Assert.AreEqual(0, Daily.OfflineCoins(new SaveData(), 1000000));
        }

        [Test]
        public void LuckyDraw_IsFixedPerDay()
        {
            int n = Daily.LuckyPrizes(1).Length;
            for (int day = 0; day < 50; day++)
            {
                int index = Daily.LuckyIndex(day, n);
                Assert.That(index, Is.InRange(0, n - 1));
                Assert.AreEqual(index, Daily.LuckyIndex(day, n));
            }
        }

        [Test]
        public void Grant_AppliesEveryPrizeKind()
        {
            var save = new SaveData();
            Daily.Grant(save, new Prize(PrizeKind.Coins, 50), UpgradeKind.Power);
            Daily.Grant(save, new Prize(PrizeKind.Gems, 3), UpgradeKind.Power);
            Daily.Grant(save, new Prize(PrizeKind.FreeUpgrade, 1), UpgradeKind.Power);
            Daily.Grant(save, new Prize(PrizeKind.DoubleCoins, 1), UpgradeKind.Power);
            Assert.AreEqual(50, save.coins);
            Assert.AreEqual(3, save.gems);
            Assert.AreEqual(1, save.powerLevel);
            Assert.IsTrue(save.doubleCoinsNextRun);
        }
    }
}
